using Server.Models;

namespace Tests.UnitTests.Server
{
    public class DatabaseUserTests : IClassFixture<TestDatabaseFixture>, IDisposable
    {
        public readonly TestDatabaseFixture _fixture;

        public DatabaseUserTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public void Dispose()
        {
            using var context = _fixture.CreateContext();
            context.Database.EnsureDeleted();
        }

        [Fact]
        public async Task TestDatabaseUserCreation()
        {
            using var context = _fixture.CreateContext();
            var user = new User
            {
                Username = "testuser",
                MasterPasswordHash = new byte[32],
                MasterPasswordSalt = new byte[32]
            };

            context.Add(user);

            Assert.Equal(1, await context.SaveChangesAsync());
        }
    }
}
