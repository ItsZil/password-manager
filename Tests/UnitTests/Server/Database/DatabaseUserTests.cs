using UtilitiesLibrary.Models;

namespace Tests.UnitTests.Server
{
    public class DatabaseUserTests : IClassFixture<TestDatabaseFixture>
    {
        public readonly TestDatabaseFixture _fixture;

        public DatabaseUserTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        /*[Fact]
        public async Task TestDatabaseUserCreation()
        {
            using var context = _fixture.CreateContext();
            var user = new User
            {
                Username = "testuser",
                MasterPasswordHash = new byte[32]
            };

            context.Add(user);

            Assert.Equal(1, await context.SaveChangesAsync());
        }*/
    }
}
