using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Server;

namespace Tests.IntegrationTests.Server
{
    public class ServerCommunicationTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        public ServerCommunicationTests()
        {
            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TEST_INTEGRATION");
            });
            _client = _factory.CreateClient();

            // Ensure handshake has been completed before making requests.
            CompleteTestHandshake.GetSharedSecret(_client);
        }

        public void Dispose()
        {
            // Ensure that the server's database file is deleted after each test run.
            var service = _factory.Services.GetService(typeof(SqlContext));
            if (service is SqlContext context)
                context.Database.EnsureDeleted();
        }

        [Fact]
        public async Task ServerIsRunningAndResponding()
        {
            var response = await _client.GetAsync("/api/test");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TestEndpointReturnsExpectedStatusCode()
        {
            var apiEndpoint = "/api/test";

            var response = await _client.GetAsync(apiEndpoint);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TestEndpointReturnsExpectedContentType()
        {
            var apiEndpoint = "/api/test";
            var expectedContentType = "application/json";

            var response = await _client.GetAsync(apiEndpoint);

            Assert.NotNull(response.Content.Headers.ContentType);
            Assert.Equal(expectedContentType, response.Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task TestEndpointReturnsNotNullContent()
        {
            var apiEndpoint = "/api/test";

            var response = await _client.GetAsync(apiEndpoint);

            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);
        }
    }
}
