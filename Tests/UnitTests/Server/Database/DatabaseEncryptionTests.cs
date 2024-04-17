using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;

namespace Tests.UnitTests.Server
{
    public class DatabaseEncryptionTests : IClassFixture<TestDatabaseFixture>
    {
        public readonly TestDatabaseFixture _fixture;

        public DatabaseEncryptionTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private int AttemptCommand(SqliteConnection connection)
        {
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM LoginDetails";

            // If the database is encrypted, then this should throw an SqliteException.
            using var reader = command.ExecuteReader();

            reader.Read();
            int count = reader.GetInt32(0);

            connection.Close();
            return count;
        }

        [Fact]
        public void TestDatabaseIsEncryptedNoPassword()
        {
            using var context = _fixture.CreateContext();
            string dbPath = context.Database.GetDbConnection().DataSource;

            bool threwException = false;
            try
            {
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                AttemptCommand(connection);
            }
            catch (SqliteException ex)
            {
                Assert.True(ex.SqliteErrorCode == 26, $"Thrown exception does not match NOTADB: {ex.Message}"); // Expecting a SQLITE_NOTADB error code.
                threwException = true;
            }
            Assert.True(threwException, "No exception was thrown.");
        }

        [Fact]
        public void TestDatabaseEncryptedIncorrectPassword()
        {
            using var context = _fixture.CreateContext();
            string dbPath = context.Database.GetDbConnection().DataSource;

            bool threwException = false;
            try
            {
                using var connection = new SqliteConnection($"Data Source={dbPath};Password=IncorrectPassword");
                AttemptCommand(connection);
            }
            catch (SqliteException ex)
            {
                Assert.True(ex.SqliteErrorCode == 26, $"Thrown exception does not match NOTADB: {ex.Message}"); // Expecting a SQLITE_NOTADB error code.
                threwException = true;
            }
            Assert.True(threwException, "No exception was thrown.");
        }

        [Fact]
        public async Task TestDatabaseEncryptedCorrectPassword()
        {
            using var context = _fixture.CreateContext();
            await context.Database.EnsureCreatedAsync();
            string dbPath = context.Database.GetDbConnection().DataSource;

            using var connection = new SqliteConnection($"Data Source={dbPath};Password={PasswordUtil.HashPragmaKey("DoNotUseThisVault")}");
            int result = AttemptCommand(connection);

            // Expecting 0 configuration in the database.
            Assert.Equal(0, result);
        }
    }
}
