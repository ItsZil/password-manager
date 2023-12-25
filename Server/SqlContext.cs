using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server
{
    public class SqlContext : DbContext
    {
        public DbSet<TestModel> TestModels { get; set; }

        public string DbPath { get; }

        public SqlContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = Path.Join(path, "database.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");
    }
}
