using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using Server;

namespace Tests.IntegrationTests.Server
{
    public class ServerCommunicationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private HttpClient _client;

        public ServerCommunicationTests()
        { 
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureTestServices(services =>
                    {

                    });
                    
                });
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task ServerIsRunningAndResponding()
        {
            var response = await _client.GetAsync("/");

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
