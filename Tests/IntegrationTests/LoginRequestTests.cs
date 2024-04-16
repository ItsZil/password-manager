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
    [Collection(nameof(LoginRequestTests))]
    public class LoginRequestTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        public LoginRequestTests()
        {
            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TEST_INTEGRATION");
            });
            _client = _factory.CreateClient();
            _sharedSecretKey = CompleteTestHandshake.GetSharedSecret(_client);

            byte[] unlockSharedSecret = CompleteTestHandshake.GetSharedSecret(_client, 2);
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

        private async Task<HttpResponseMessage> RegisterDomainAsync(string domain)
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("loginrequesttestspassword");
            byte[] encryptedSharedKeyPassword = await PasswordUtil.EncryptPassword(_sharedSecretKey, plainPassword);

            var registerApiEndpoint = "/api/domainregisterrequest";
            var registerRequest = new DomainRegisterRequest { Domain = domain, Username = "loginrequesttestsusername", Password = Convert.ToBase64String(encryptedSharedKeyPassword) };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");
            return await _client.PostAsync(registerApiEndpoint, registerRequestContent);
        }

        private async Task<HttpResponseMessage> LoginDomainAsync(DomainLoginRequest request)
        {
            var apiEndpoint = "/api/domainloginrequest";
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);
            return response;
        }

        [Fact]
        public async Task TestNoDomainLoginRequestReturns401()
        {
            var response = await LoginDomainAsync(null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        
        [Fact]
        public async Task TestEmptyDomainLoginRequestReturns404()
        {
            var response = await LoginDomainAsync(new());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestUnknownDomainLoginRequestReturns404()
        {
            DomainLoginRequest request = new DomainLoginRequest { Domain = "unknowndomain.404" };

            var response = await LoginDomainAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsOk()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsResponseObj()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

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
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
            Assert.NotNull(responseObj.Password);

            byte[] decryptedPasword = await PasswordUtil.DecryptPassword(_sharedSecretKey, Convert.FromBase64String(responseObj.Password));
            string decryptedPasswordString = PasswordUtil.PlainFromContainer(decryptedPasword);

            Assert.Equal("loginrequesttestspassword", decryptedPasswordString);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsIncorrectPassword()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
            Assert.NotNull(responseObj.Password);

            byte[] decryptedPasword = await PasswordUtil.DecryptPassword(_sharedSecretKey, Convert.FromBase64String(responseObj.Password));
            string decryptedPasswordString = PasswordUtil.PlainFromContainer(decryptedPasword);

            Assert.NotEqual("loginrequesttestspassword123", decryptedPasswordString);
        }
    }
}