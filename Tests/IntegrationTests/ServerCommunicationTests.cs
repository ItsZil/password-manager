using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
        public async Task TestServerEndpoint_ReturnsSuccess()
        {
            var apiEndpoint = "/api/test";

            var response = await _client.GetAsync(apiEndpoint);

            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);
        }
    }
}
