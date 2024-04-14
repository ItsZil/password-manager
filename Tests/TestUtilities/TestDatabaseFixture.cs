using Server;

namespace Tests.TestUtilities
{
    public class TestDatabaseFixture
    {
        public string DatabaseName { get; private set; }

        public TestDatabaseFixture()
        {
            DatabaseName = $"vault_test_{Guid.NewGuid()}.db";
            using (var context = CreateContext())
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }
        }

        internal SqlContext CreateContext()
        {
            SqlContext context = new SqlContext(DatabaseName, new Server.Utilities.KeyProvider());
            return context;
        }
    }
}
