using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Server;
using UtilitiesLibrary.Models;

namespace Tests.IntegrationTests.Server
{
    public class LoginRequestTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        public LoginRequestTests()
        { 
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    // TODO: should look into another way to bypass the local network check.
                    builder.UseEnvironment("TEST_INTEGRATION");                    
                });
            _client = _factory.CreateClient();
        }

        public void Dispose()
        {
            // Ensure that the server's database file is deleted after each test run.
            var service = _factory.Services.GetService(typeof(SqlContext));
            if (service is SqlContext context)
                context.Database.EnsureDeleted();
        }

        [Fact]
        public async Task TestEmptyDomainLoginRequestReturnsOk()
        {
            var apiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new();
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TestEmptyDomainLoginRequestReturnsRequestObject()
        {
            var apiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new();
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
        }

        [Fact]
        public async Task TestUnknownDomainLoginRequestReturns404()
        {
            var apiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new DomainLoginRequest { Domain = "unknowndomain.404" };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
