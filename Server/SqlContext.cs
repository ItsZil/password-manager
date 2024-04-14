using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UtilitiesLibrary.Models;
using Server.Utilities;
using System.Text;
using System.Data;

namespace Server
{
    internal class SqlContext : DbContext
    {
        internal DbSet<TestModel> TestModels { get; set; }
        internal DbSet<Configuration> Configuration { get; set; }
        internal DbSet<LoginDetails> LoginDetails { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }

        private KeyProvider _keyProvider;

        private bool _isTestDatabase = false;
        private readonly string _defaultInitialPassword = "DoNotUseThisVault";

        internal string dbPath { get; private set; }
        internal byte[] hashedVaultPassword = Array.Empty<byte>();

        private byte[] _vaultEncryptionKey = Array.Empty<byte>();
        private byte[] _salt = Array.Empty<byte>();

        public SqlContext(IConfiguration configuration, KeyProvider keyProvider)
        {
            string? testDbPath = configuration["TEST_INTEGRATION_DB_PATH"];
            if (testDbPath != null)
            {
                // This test is run in an integration test environment.
                dbPath = testDbPath;
                _isTestDatabase = true;

                keyProvider.SetVaultPragmaKey(_defaultInitialPassword);
            }
            else if (File.Exists(ConfigUtil.GetVaultLocation()) && keyProvider.HasVaultPragmaKey())
            {
                // This is a non-default vault that the user has set up. We need to use the master password to connect, if we have it.
                // If we do not have the master password yet, fall through to the random path.
                dbPath = ConfigUtil.GetVaultLocation();
            }
            else
            {
                // No vault exists yet. Use a random path for initial connection.
                dbPath = Path.Join(Path.GetTempPath(), "initialvault.db");
            }
            _keyProvider = keyProvider;

            Database.EnsureCreated();
            InitializeConfiguration();
        }

        // This constructor is used in unit tests.
        public SqlContext(string databaseName, KeyProvider keyProvider)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            dbPath = Path.Join(folder, databaseName);
            _keyProvider = keyProvider;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(CreateConnectionString(dbPath, _defaultInitialPassword));

        /// <summary>
        /// Updates the database connection with a new path and master password and saves it to the configuration file for later retrieval.
        /// </summary>
        /// <param name="newPath">An absolute path to where the vault should be stored</param>
        /// <param name="plainMasterPassword">A plain-text master password</param>
        /// <param name="keyProvider">Key provider singleton to store pragma key in memory</param>
        /// <returns>A boolean indicating if a database connection was successfully opened</return>
        internal async Task<bool> UpdateDatabaseConnection(string newPath, string plainMasterPassword)
        {
            if (!newPath.EndsWith(".db"))
            {
                newPath = Path.Join(newPath, "vault.db");
            }
            ConfigUtil.SetVaultLocation(newPath);
            dbPath = newPath;

            SetVaultMasterPassword(Encoding.UTF8.GetBytes(plainMasterPassword)); // might only need to call this on first creation, so we can store the salt etc in configuration table

            var newConnectionString = CreateConnectionString(dbPath, plainMasterPassword);

            await using var connection = Database.GetDbConnection();
            connection.ConnectionString = newConnectionString;

            try
            {
                await Database.EnsureCreatedAsync();
                await connection.OpenAsync(); // Re-open the connection with the new connection string
            }
            catch (SqliteException ex)
            {
                if (ex.SqliteErrorCode != 26) // Expecting NOTADB if the password is incorrect.
                    throw;
                return false;
            }

            bool opened = connection.State == ConnectionState.Open;
            if (opened)
            {
                _keyProvider.SetVaultPragmaKey(plainMasterPassword);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a connection string for the SQLite database
        /// </summary>
        /// <param name="databasePath">An absolute path to where the vault should be stored</param>
        /// <param name="plainMasterPassword">A plain-text master password</param>
        /// <returns>A connection string</returns>
        private string CreateConnectionString(string databasePath, string plainMasterPassword)
        {
            string pragmaKey = String.Empty;
            if (_keyProvider.HasVaultPragmaKey() && !_isTestDatabase)
            {
                // We already have the pragma key, do not re-generate it using the plain password. 
                // This should be accessed after authentication, in endpoints.
                pragmaKey = _keyProvider.GetVaultPragmaKey();
            }
            else
            {
                // The initial initialization, setup, or the user has not provided the master password yet.
                pragmaKey = plainMasterPassword;
            }

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Password = pragmaKey // PRAGMA key gets sent from EF Core directly after opening the connection
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
            if (Configuration.Count() == 0 && _keyProvider.HasVaultPragmaKey())
            {
                SetVaultMasterPassword(Encoding.UTF8.GetBytes(_keyProvider.GetVaultPragmaKey()));
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
