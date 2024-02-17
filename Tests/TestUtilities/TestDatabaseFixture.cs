using Server;
using Server.Models;

namespace Tests.TestUtilities
{
    public class TestDatabaseFixture
    {
        public string DatabaseName { get; private set; }

        public TestDatabaseFixture()
        {
            DatabaseName = $"testdatabase{Guid.NewGuid()}.db";
            using (var context = CreateContext())
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }
        }

        internal SqlContext CreateContext()
        {
            SqlContext context = new SqlContext(DatabaseName);
            return context;
        }
    }
}
