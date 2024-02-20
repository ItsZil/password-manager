using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UtilitiesLibrary.Models;

namespace Server
{
    internal class SqlContext : DbContext
    {
        internal DbSet<TestModel> TestModels { get; set; }
        internal DbSet<User> Users { get; set; }
        internal DbSet<Account> Accounts { get; set; }
        internal DbSet<Authenticator> Authenticators { get; set; }

        private string _dbPath { get; }

        public SqlContext()
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            _dbPath = Path.Join(folder, "vault.db");
        }

        public SqlContext(string databaseName)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            _dbPath = Path.Join(folder, databaseName);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(InitializeConnection(_dbPath));

        private static SqliteConnection InitializeConnection(string databasePath)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Password = "Test123" // PRAGMA key is being sent from EF Core directly after opening the connection. TODO: Randomly generated master key instead.
            };
            return new SqliteConnection(connectionString.ToString());
        }
    }
}
