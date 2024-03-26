using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server;
using UtilitiesLibrary.Models;
using UtilitiesLibrary.Utilities;

namespace Tests.IntegrationTests.Server
{
    public class LoginRequestTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        public LoginRequestTests()
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
        public async Task TestNoDomainLoginRequestReturns401()
        {
            var apiEndpoint = "/api/domainloginrequest";

            var response = await _client.PostAsync(apiEndpoint, null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task TestEmptyDomainLoginRequestReturns404()
        {
            var apiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new();
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

        private async Task<HttpResponseMessage> RegisterDomainAsync(string domain)
        {
            var registerApiEndpoint = "/api/domainregisterrequest";
            var registerRequest = new DomainRegisterRequest { Domain = domain, Username = "loginrequesttestsusername", Password = PasswordUtil.ByteArrayFromPlain("loginrequesttestspassword") };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");
            return await _client.PostAsync(registerApiEndpoint, registerRequestContent);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsOk()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            var loginApiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(loginApiEndpoint, requestContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsResponseObj()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            var apiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsCorrectPassword()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            var apiEndpoint = "/api/domainloginrequest";
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
            Assert.NotNull(responseObj.Password);
            Assert.Equal("loginrequesttestspassword", PasswordUtil.PlainFromByteArray(responseObj.Password));
        }
    }
}