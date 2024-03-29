using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;
using System;
using System.Reflection;
using UtilitiesLibrary.Models;
using UtilitiesLibrary.Utilities;

namespace Server
{
    internal class SqlContext : DbContext
    {
        internal DbSet<TestModel> TestModels { get; set; }
        internal DbSet<User> Users { get; set; }
        internal DbSet<LoginDetails> LoginDetails { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }

        internal string dbPath { get; private set; }
        internal string vaultPassword { get; private set; } = string.Empty;

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

        internal byte[] GetVaultMasterPassword()
        {
            return PasswordUtil.ByteArrayFromPlain(vaultPassword);
        }

        private SqliteConnection InitializeConnection(string databasePath)
        {
            vaultPassword = "Test123"; // TODO: Randomly generated master key instead.
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Password = vaultPassword // PRAGMA key gets sent from EF Core directly after opening the connection.
            };

            return new SqliteConnection(connectionString.ToString());
        }
    }
}
