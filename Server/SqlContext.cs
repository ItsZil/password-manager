using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UtilitiesLibrary.Models;
using Server.Utilities;

namespace Server
{
    internal class SqlContext : DbContext
    {
        internal DbSet<TestModel> TestModels { get; set; }
        internal DbSet<Configuration> Configuration { get; set; }
        internal DbSet<LoginDetails> LoginDetails { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }

        internal string dbPath { get; private set; }
        internal byte[] hashedVaultPassword;
        private byte[] encryptionKey;
        private byte[] salt;

        public SqlContext(IConfiguration configuration)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            
            string? testDbPath = configuration["TEST_INTEGRATION_DB_PATH"];
            if (testDbPath != null)
            {
                // This test is run in an integration test environment.
                dbPath = testDbPath;
            }
            else
            {
                dbPath = Path.Join(folder, "vault.db");
            }

            Database.EnsureCreated(); // TODO: is this safe with existing vaults?
            InitializeConfiguration();
        }

        public SqlContext(string databaseName)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            dbPath = Path.Join(folder, databaseName);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(TryInitializeConnection(dbPath, "Test123"));

        internal void ChangeDatabasePath(string newPath)
        {
            dbPath = newPath;
        }

        internal byte[] GetEncryptionKey()
        {
            if (Configuration.Count() == 0)
            {
                throw new Exception("No configuration found in database. Did InitializeConfiguration not get called?");
            }
            return Configuration.First().EncryptionKey;
        }

        internal byte[] GetPragmaKey()
        {
            if (Configuration.Count() == 0)
            {
                throw new Exception("No configuration found in database. Did InitializeConfiguration not get called?");
            }
            return Configuration.First().MasterPasswordHash[..32];
        }

        private void SetVaultMasterPassword(string plainVaultPassword)
        {
            byte[] masterPassword = PasswordUtil.ByteArrayFromPlain(plainVaultPassword);
            ReadOnlySpan<byte> hashedMasterPassword = PasswordUtil.HashMasterPassword(masterPassword);

            hashedVaultPassword = PasswordUtil.ByteArrayFromSpan(hashedMasterPassword);

            Span<byte> generatedSalt = stackalloc byte[16];
            encryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(hashedVaultPassword, ref generatedSalt);
            salt = generatedSalt.ToArray();
        }

        private void InitializeConfiguration()
        {
            if (Configuration.Count() == 0)
            {
                Configuration.Add(new Configuration
                {
                    MasterPasswordHash = hashedVaultPassword,
                    Salt = salt,
                    EncryptionKey = encryptionKey
                });
                SaveChanges();
            }
        }

        private SqliteConnection TryInitializeConnection(string databasePath, string plainMasterPassword)
        {
            SetVaultMasterPassword(plainMasterPassword); // TODO: Randomly generated master key instead, or use one user enters
            string hashInHex = BitConverter.ToString(hashedVaultPassword).Replace("-", string.Empty);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Password = hashInHex[..32] // PRAGMA key gets sent from EF Core directly after opening the connection
            };
            return new SqliteConnection(connectionString.ToString());
        }
    }
}
