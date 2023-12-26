using Server;
using Server.Models;

namespace Tests.TestUtilities
{
    public class TestDatabaseFixture
    {
        private static readonly object _lock = new();
        private static bool _databaseInitialized;

        public TestDatabaseFixture()
        {
            lock (_lock)
            {
                if (!_databaseInitialized)
                {
                    using (var context = CreateContext())
                    {
                        context.Database.EnsureDeleted();
                        context.Database.EnsureCreated();

                        context.AddRange(
                            new TestModel { Id = 1, Message = "Test Model 1" },
                            new TestModel { Id = 2, Message = "Test Model 2" }
                        );

                        context.SaveChanges();
                    }

                    _databaseInitialized = true;
                }
            }
        }

        internal SqlContext CreateContext()
        {
            SqlContext context = new SqlContext("testdatabase.db");
            return context;
        }
    }
}
