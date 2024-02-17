using Microsoft.EntityFrameworkCore;
using Server.Models;
using System.Reflection;

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
            _dbPath = Path.Join(folder, "database.db");
        }

        public SqlContext(string databaseName)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            _dbPath = Path.Join(folder, databaseName);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={_dbPath}");
    }
}
