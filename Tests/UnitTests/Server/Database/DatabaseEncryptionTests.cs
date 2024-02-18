using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

            // Attempt to read from the database to verify that it is encrypted.
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users";

            // If the database is encrypted, the command.ExecuteReader() call should throw an exception.
            using var reader = command.ExecuteReader();

            // If the command is successful, let's get the count of users. This should be 0.
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
        public void TestDatabaseEncryptedCorrectPassword()
        {
            using var context = _fixture.CreateContext();
            string dbPath = context.Database.GetDbConnection().DataSource;

            using var connection = new SqliteConnection($"Data Source={dbPath};Password=Test123");
            int result = AttemptCommand(connection);

            // Expecting 0 users in the database.
            Assert.Equal(0, result);
        }
    }
}
