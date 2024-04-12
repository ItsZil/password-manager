using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UtilitiesLibrary.Models;
using Server.Utilities;
using System.Text.Json;
using System.Text;

namespace Server
{
    internal class SqlContext : DbContext
    {
        internal DbSet<TestModel> TestModels { get; set; }
        internal DbSet<Configuration> Configuration { get; set; }
        internal DbSet<LoginDetails> LoginDetails { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }

        internal string dbPath { get; private set; }
        internal byte[] hashedVaultPassword = Array.Empty<byte>();

        private byte[] _vaultEncryptionKey = Array.Empty<byte>();
        private byte[] _salt = Array.Empty<byte>();

        public SqlContext(IConfiguration configuration)
        {
            dbPath = ConfigUtil.GetVaultLocation();

            string? testDbPath = configuration["TEST_INTEGRATION_DB_PATH"];
            if (testDbPath != null)
            {
                // This test is run in an integration test environment.
                dbPath = testDbPath;
            }

            Database.EnsureCreated();
            InitializeConfiguration();
        }

        public SqlContext(string databaseName)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            dbPath = Path.Join(folder, databaseName);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(CreateConnectionString(dbPath, Encoding.UTF8.GetBytes("DefaultPassword")));

        /// <summary>
        /// Updates the database connection with a new path and master password and saves it to the configuration file for later retrieval.
        /// </summary>
        /// <param name="newPath">An absolute path to where the vault should be stored</param>
        /// <param name="plainMasterPassword">A byte array of the plain-text master password</param>
        internal async Task UpdateDatabaseConnection(string newPath, byte[] plainMasterPassword)
        {
            if (!newPath.EndsWith(".db"))
            {
                newPath = Path.Join(newPath, "vault.db");
            }
            ConfigUtil.SetVaultLocation(newPath);
            dbPath = newPath;

            SetVaultMasterPassword(plainMasterPassword);
            var newConnectionString = CreateConnectionString(dbPath, plainMasterPassword);

            await using var connection = Database.GetDbConnection();
            connection.ConnectionString = newConnectionString;

            await Database.EnsureCreatedAsync();
            await connection.OpenAsync(); // Re-open the connection with the new connection string
        }

        /// <summary>
        /// Creates a connection string for the SQLite database
        /// </summary>
        /// <param name="databasePath">An absolute path to where the vault should be stored</param>
        /// <param name="plainMasterPassword">A byte array of the plain-text master password</param>
        /// <returns>A connection string</returns>
        private string CreateConnectionString(string databasePath, byte[] plainMasterPassword)
        {
            SetVaultMasterPassword(plainMasterPassword);
            string hashInHex = BitConverter.ToString(hashedVaultPassword).Replace("-", string.Empty);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Password = hashInHex[..32] // PRAGMA key gets sent from EF Core directly after opening the connection
            };
            return connectionString.ToString();
        }

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
            return Configuration.First().VaultEncryptionKey;
        }

        internal byte[] GetPragmaKey()
        {
            if (Configuration.Count() == 0)
            {
                throw new Exception("No configuration found in database. Did InitializeConfiguration not get called?");
            }
            return Configuration.First().MasterPasswordHash[..32];
        }

        private void SetVaultMasterPassword(byte[] plainVaultPassword)
        {
            ReadOnlySpan<byte> hashedMasterPassword = PasswordUtil.HashMasterPassword(plainVaultPassword);

            hashedVaultPassword = PasswordUtil.ByteArrayFromSpan(hashedMasterPassword);

            Span<byte> generatedSalt = stackalloc byte[16];
            _vaultEncryptionKey = PasswordUtil.DeriveEncryptionKeyFromMasterPassword(hashedVaultPassword, ref generatedSalt);
            _salt = generatedSalt.ToArray();
        }

        private void InitializeConfiguration()
        {
            if (Configuration.Count() == 0)
            {
                Configuration.Add(new Configuration
                {
                    MasterPasswordHash = hashedVaultPassword,
                    Salt = _salt,
                    VaultEncryptionKey = _vaultEncryptionKey
                });
                SaveChanges();
            }
        }
    }
}
