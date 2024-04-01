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
        internal DbSet<User> Users { get; set; }
        internal DbSet<LoginDetails> LoginDetails { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }

        internal string dbPath { get; private set; }
        internal byte[] hashedVaultPassword { get; private set; }
        internal byte[] encryptionKey { get; private set; }
        internal byte[] salt { get ; private set; }

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
        }

        public SqlContext(string databaseName)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            dbPath = Path.Join(folder, databaseName);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(InitializeConnection(dbPath));

        internal void ChangeDatabasePath(string newPath)
        {
            dbPath = newPath;
        }

        internal byte[] GetEncryptionKey()
        {
            if (Users.Count() == 0)
            {
                Users.Add(new() { EncryptionKey = encryptionKey });
            }
            SaveChanges();

            // TODO
            return Users.First().EncryptionKey;
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

        private SqliteConnection InitializeConnection(string databasePath)
        {
            SetVaultMasterPassword("Test123"); // TODO: Randomly generated master key instead
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
