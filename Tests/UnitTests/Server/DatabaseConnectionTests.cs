namespace Tests.UnitTests.Server
{
    public class DatabaseConnectionTests : IClassFixture<TestDatabaseFixture>, IDisposable
    {
        public readonly TestDatabaseFixture _fixture;

        public DatabaseConnectionTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public void Dispose()
        {
            using var context = _fixture.CreateContext();
            context.Database.EnsureDeleted();
        }

        [Fact]
        public void TestDatabaseConnection()
        {
            using var context = _fixture.CreateContext();

            Assert.True(context.Database.CanConnect());
        }

        [Fact]
        public void TestDatabaseCreated()
        {
            using var context = _fixture.CreateContext();
            context.Database.EnsureDeleted();

            Assert.True(context.Database.EnsureCreated());
        }
    }
}
