using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server;
using Server.Endpoints;
using UtilitiesLibrary.Models;
using UtilitiesLibrary.Utilities;

namespace Tests.IntegrationTests.Server
{
    public class RegisterRequestTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        public RegisterRequestTests()
        {
            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
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
        public async Task TestNoDomainRegisterRequestReturns401()
        {
            var apiEndpoint = "/api/domainregisterrequest";

            var response = await _client.PostAsync(apiEndpoint, null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task TestEmptyDomainRegisterRequestReturns401()
        {
            var apiEndpoint = "/api/domainregisterrequest";
            DomainRegisterRequest request = new();
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private async Task<HttpResponseMessage> RegisterDomainAsync(string domain)
        {
            var registerApiEndpoint = "/api/domainregisterrequest";
            var registerRequest = new DomainRegisterRequest { Domain = domain, Username = "registerrequesttestsusername", Password = PasswordUtil.ByteArrayFromPlain("registerrequesttestspassword") };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");
            return await _client.PostAsync(registerApiEndpoint, registerRequestContent);
        }

        [Fact]
        public async Task TestNewDomainRegisterRequestReturnsOk()
        {
            var registerResponse = await RegisterDomainAsync("newdomain.com");

            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        }

        [Fact]
        public async Task TestNewDomainRegisterRequestReturnsResponseObj()
        {
            var registerResponse = await RegisterDomainAsync("newdomain.com");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            string responseString = await registerResponse.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainRegisterResponse? responseObj = JsonSerializer.Deserialize<DomainRegisterResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainRegisterResponse>(responseObj);
        }

        [Fact]
        public async Task TestExistingDomainRegisterRequestReturns409()
        {
            var initialRegisterResponse = await RegisterDomainAsync("newdomain.com");
            Assert.Equal(HttpStatusCode.OK, initialRegisterResponse.StatusCode);

            var existingRegisterResponse = await RegisterDomainAsync("newdomain.com");

            Assert.Equal(HttpStatusCode.Conflict, existingRegisterResponse.StatusCode);
        }
    }
}