using Microsoft.AspNetCore.Mvc.Testing;
using UtilitiesLibrary.Models;
using Server;
using Microsoft.AspNetCore.Hosting;
using Server.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(PinCodeTests))]
    public class PinCodeTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        public PinCodeTests()
        {
            // Ensure we have no leftover config file
            if (File.Exists(ConfigUtil.GetFileLocation()))
                File.Delete(ConfigUtil.GetFileLocation());

            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TEST_INTEGRATION");
            });
            _client = _factory.CreateClient();
            _sharedSecretKey = CompleteTestHandshake.GetSharedSecret(_client, 1);
            _accessToken = CompleteTestAuth.GetAccessToken(_client, _sharedSecretKey);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        public void Dispose()
        {
            // Ensure that the server's default database file is deleted after each test run.
            var service = _factory.Services.GetService(typeof(SqlContext));
            if (service is SqlContext context)
                context.Database.EnsureDeleted();

            // Delete the generated config file
            if (File.Exists(ConfigUtil.GetFileLocation()))
                File.Delete(ConfigUtil.GetFileLocation());
        }

        private async Task<HttpResponseMessage> RegisterDomainAsync(string domain)
        {
            var registerApiEndpoint = "/api/domainregisterrequest";
            var registerRequest = new DomainRegisterRequest { SourceId = 1, Domain = domain, Username = "passkeytests" };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(registerApiEndpoint, registerRequestContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return response;
        }

        private async Task<int> GetLoginDetailsId(HttpResponseMessage registerDomainResponse)
        {
            var domainResponseContent = await registerDomainResponse.Content.ReadAsStringAsync();
            Assert.NotNull(domainResponseContent);

            var domainRegisterResponse = JsonSerializer.Deserialize<DomainRegisterResponse>(domainResponseContent);
            Assert.NotNull(domainRegisterResponse);

            return domainRegisterResponse.Id;
        }

        private async Task<HttpResponseMessage> GetPinCodeAsync(int loginDetailsId)
        {
            var getPinCodeApiEndpoint = $"/api/pincode?sourceId=1&loginDetailsId={loginDetailsId}";
            var response = await _client.GetAsync(getPinCodeApiEndpoint);
            return response;
        }

        private async Task<HttpResponseMessage> SetPinCodeAsync(CreatePinCodeRequest request)
        {
            var setPinCodeApiEndpoint = "/api/pincode";
            var setPinCodeRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(setPinCodeApiEndpoint, setPinCodeRequestContent);
            return response;
        }

        private async Task<HttpResponseMessage> DeletePinCodeAsync(int loginDetailsId)
        {
            var deletePinCodeApiEndpoint = $"/api/pincode?loginDetailsId={loginDetailsId}";
            var response = await _client.DeleteAsync(deletePinCodeApiEndpoint);
            return response;
        }

        [Fact]
        public async Task TestSetPinCodeReturnsOk()
        {
            // Create a domain
            var domainResponse = await RegisterDomainAsync("extraauthtests.com");
            Assert.Equal(HttpStatusCode.OK, domainResponse.StatusCode);

            // Encrypt 1234 pin code
            byte[] pinCodeBytes = Encoding.UTF8.GetBytes("1234");
            byte[] encryptedPinCodeBytes = await PasswordUtil.EncryptMessage(_sharedSecretKey, pinCodeBytes);
            string encryptedPinCode = Convert.ToBase64String(encryptedPinCodeBytes);

            CreatePinCodeRequest setPinCodeRequest = new()
            {
                LoginDetailsId = 1,
                PinCode = encryptedPinCode
            };

            var setPinCodeResponse = await SetPinCodeAsync(setPinCodeRequest);
            Assert.Equal(HttpStatusCode.Created, setPinCodeResponse.StatusCode);

            // Check if the pin code was actually set
            var getPinCodeResponse = await GetPinCodeAsync(1);
            Assert.Equal(HttpStatusCode.OK, getPinCodeResponse.StatusCode);
        }

        [Fact]
        public async Task TestSetPinCodeNonExistingLoginDetailsReturnsNotFound()
        { 
            CreatePinCodeRequest setPinCodeRequest = new()
            {
                LoginDetailsId = 1, // Does not exist
                PinCode = "doesntmatter" // Check for this won't be reached
            };

            var setPinCodeResponse = await SetPinCodeAsync(setPinCodeRequest);
            Assert.Equal(HttpStatusCode.NotFound, setPinCodeResponse.StatusCode);
        }

        [Fact]
        public async Task TestSetPinCodeIncorrectCodeReturnsNotFound()
        {
            // Create a domain
            _ = await RegisterDomainAsync("extraauthtests.com");

            // Encrypt 123 pin code, which is not 4 digits
            byte[] pinCodeBytes = Encoding.UTF8.GetBytes("123");
            byte[] encryptedPinCodeBytes = await PasswordUtil.EncryptMessage(_sharedSecretKey, pinCodeBytes);
            string encryptedPinCode = Convert.ToBase64String(encryptedPinCodeBytes);

            CreatePinCodeRequest setPinCodeRequest = new()
            {
                LoginDetailsId = 1,
                PinCode = encryptedPinCode
            };

            var setPinCodeResponse = await SetPinCodeAsync(setPinCodeRequest);
            Assert.Equal(HttpStatusCode.BadRequest, setPinCodeResponse.StatusCode);
        }

        [Fact]
        public async Task TestGetPinCodeReturnsOk()
        {
            _ = await RegisterDomainAsync("test.com");

            // Encrypt 1234 pin code
            byte[] pinCodeBytes = Encoding.UTF8.GetBytes("1234");
            byte[] encryptedPinCodeBytes = await PasswordUtil.EncryptMessage(_sharedSecretKey, pinCodeBytes);
            string encryptedPinCode = Convert.ToBase64String(encryptedPinCodeBytes);

            var setPinCodeResponse = await SetPinCodeAsync(new() { LoginDetailsId = 1, PinCode = encryptedPinCode });
            Assert.Equal(HttpStatusCode.Created, setPinCodeResponse.StatusCode);

            var getPinCodeResponse = await GetPinCodeAsync(1);
            Assert.Equal(HttpStatusCode.OK, getPinCodeResponse.StatusCode);

            var getPinCodeContent = await getPinCodeResponse.Content.ReadAsStringAsync();
            Assert.NotNull(getPinCodeContent);

            var getPinCodeResponseObject = JsonSerializer.Deserialize<GetPinCodeResponse>(getPinCodeContent);
            Assert.NotNull(getPinCodeResponseObject);
        }

        [Fact]
        public async Task TestGetNonExistingPinCodeReturnsNotFound()
        {
            var extraAuthResponse = await GetPinCodeAsync(1);
            Assert.Equal(HttpStatusCode.NotFound, extraAuthResponse.StatusCode);
        }

        [Fact]
        public async Task TestDeletePinCodeReturnsNoContent()
        {
            _ = await RegisterDomainAsync("test.com");

            // Encrypt 1234 pin code
            byte[] pinCodeBytes = Encoding.UTF8.GetBytes("1234");
            byte[] encryptedPinCodeBytes = await PasswordUtil.EncryptMessage(_sharedSecretKey, pinCodeBytes);
            string encryptedPinCode = Convert.ToBase64String(encryptedPinCodeBytes);

            var setPinCodeResponse = await SetPinCodeAsync(new() { LoginDetailsId = 1, PinCode = encryptedPinCode });
            Assert.Equal(HttpStatusCode.Created, setPinCodeResponse.StatusCode);

            var deletePinCodeResponse = await DeletePinCodeAsync(1);
            Assert.Equal(HttpStatusCode.NoContent, deletePinCodeResponse.StatusCode);

            // Check if the pin code was actually deleted
            var getPinCodeResponse = await GetPinCodeAsync(1);
            Assert.Equal(HttpStatusCode.NotFound, getPinCodeResponse.StatusCode);
        }

        [Fact]
        public async Task TestDeleteNonExistingPinCodeReturnsNotFound()
        {
            var deletePinCodeResponse = await DeletePinCodeAsync(1);
            Assert.Equal(HttpStatusCode.NotFound, deletePinCodeResponse.StatusCode);
        }

    }
}
