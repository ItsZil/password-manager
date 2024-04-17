using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UtilitiesLibrary.Models;
using Server.Utilities;
using System.Data;

namespace Server
{
    internal class SqlContext : DbContext
    {
        internal DbSet<TestModel> TestModels { get; set; }
        internal DbSet<LoginDetails> LoginDetails { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }
        internal DbSet<RefreshToken> RefreshTokens { get; set; }

        private KeyProvider _keyProvider;

        private bool _isTestDatabase = false;
        private readonly string _defaultInitialPassword = PasswordUtil.HashPragmaKey("DoNotUseThisVault");

        internal string dbPath { get; private set; }

        public SqlContext(IConfiguration configuration, KeyProvider keyProvider)
        {
            string? testDbPath = configuration["TEST_INTEGRATION_DB_PATH"];
            if (testDbPath != null)
            {
                // This test is run in an integration test environment.
                dbPath = testDbPath;
                _isTestDatabase = true;

                keyProvider.SetVaultPragmaKeyHashed(_defaultInitialPassword);
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
        /// <returns>A boolean indicating if a database connection was successfully opened</return>
        internal async Task<bool> UpdateDatabaseConnection(string newPath, string plainMasterPassword)
        {
            if (!newPath.EndsWith(".db"))
            {
                newPath = Path.Join(newPath, "vault.db");
            }
            ConfigUtil.SetVaultLocation(newPath);
            dbPath = newPath;

            string hashedPragmaKeyB64 = PasswordUtil.HashPragmaKey(plainMasterPassword);
            var newConnectionString = CreateConnectionString(dbPath, hashedPragmaKeyB64);

            await using var connection = Database.GetDbConnection();
            
            // Close existing connection
            await connection.CloseAsync();

            // Re-open the connection with the new connection string
            connection.ConnectionString = newConnectionString;

            try
            {
                await Database.EnsureCreatedAsync();
                await connection.OpenAsync();
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
                _keyProvider.SetVaultPragmaKeyHashed(hashedPragmaKeyB64);
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
    }
}
