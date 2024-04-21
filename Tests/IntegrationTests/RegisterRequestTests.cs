using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server;
using Server.Utilities;
using UtilitiesLibrary.Models;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(RegisterRequestTests))]
    public class RegisterRequestTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        public RegisterRequestTests()
        {
            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TEST_INTEGRATION");
            });
            _client = _factory.CreateClient();
            _sharedSecretKey = CompleteTestHandshake.GetSharedSecret(_client);

            byte[] unlockSharedSecret = CompleteTestHandshake.GetSharedSecret(_client, 1);
            _accessToken = CompleteTestAuth.GetAccessToken(_client, unlockSharedSecret);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
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
            byte[] encryptedPassword = await PasswordUtil.EncryptMessage(_sharedSecretKey, PasswordUtil.ByteArrayFromPlain("registerrequesttestspassword"));

            var registerApiEndpoint = "/api/domainregisterrequest";
            var registerRequest = new DomainRegisterRequest { Domain = domain, Username = "registerrequesttestsusername", Password = Convert.ToBase64String(encryptedPassword) };
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