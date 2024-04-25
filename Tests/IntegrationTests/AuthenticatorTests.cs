using Microsoft.AspNetCore.Mvc.Testing;
using UtilitiesLibrary.Models;
using Server;
using Microsoft.AspNetCore.Hosting;
using Server.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using OtpNet;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(AuthenticatorTests))]
    public class AuthenticatorTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        public AuthenticatorTests()
        {
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

        public async void Dispose()
        {
            // Delete any created authenticator.
            await DeleteAuthenticatorAsync(1);

            // Ensure that the server's default database file is deleted after each test run.
            var service = _factory.Services.GetService(typeof(SqlContext));
            if (service is SqlContext context)
                context.Database.EnsureDeleted();
        }

        private async Task<HttpResponseMessage> RegisterDomainAsync(string domain)
        {
            var registerApiEndpoint = "/api/register";
            var registerRequest = new DomainRegisterRequest { SourceId = 1, Domain = domain, Username = "passkeytests" };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(registerApiEndpoint, registerRequestContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return response;
        }

        private async Task<HttpResponseMessage> GetAuthenticatorCodeAsync(int authenticatorId, string timestamp)
        {
            var getAuthenticatorCodeApiEndpoint = $"/api/authenticator?sourceId=1&id={authenticatorId}&timestamp={timestamp}";
            var response = await _client.GetAsync(getAuthenticatorCodeApiEndpoint);
            return response;
        }

        private async Task<HttpResponseMessage> CreateAuthenticatorAsync(CreateAuthenticatorRequest request)
        {
            var createAuthenticatorApiEndpoint = "/api/authenticator";
            var createAuthenticatorRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(createAuthenticatorApiEndpoint, createAuthenticatorRequestContent);
            return response;
        }

        private async Task<HttpResponseMessage> DeleteAuthenticatorAsync(int id)
        {
            var deleteAuthenticatorApiEndpoint = $"/api/authenticator?id={id}";
            var response = await _client.DeleteAsync(deleteAuthenticatorApiEndpoint);
            return response;
        }

        [Fact]
        public async Task TestCreateAuthenticatorReturnsOk()
        {
            // Create a domain
            await RegisterDomainAsync("authenticatortests.com");

            // Generate a random secret key
            byte[] secretKey = PasswordUtil.GenerateSecurePassword(32);
            string secretKeyBase32 = Base32Encoding.ToString(secretKey);
            byte[] encryptedSecretKey = await PasswordUtil.EncryptMessage(_sharedSecretKey, Encoding.UTF8.GetBytes(secretKeyBase32));

            CreateAuthenticatorRequest createAuthenticatorRequest = new()
            {
                LoginDetailsId = 1,
                SecretKey = Convert.ToBase64String(encryptedSecretKey),
                Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var createAuthenticatorResponse = await CreateAuthenticatorAsync(createAuthenticatorRequest);
            Assert.Equal(HttpStatusCode.OK, createAuthenticatorResponse.StatusCode);

            // Verify that the response contains the authenticator code
            var createAuthenticatorResponseContent = await createAuthenticatorResponse.Content.ReadAsStringAsync();
            Assert.NotNull(createAuthenticatorResponseContent);
            
            var createAuthenticatorResponseObject = JsonSerializer.Deserialize<AuthenticatorCodeResponse>(createAuthenticatorResponseContent);
            Assert.NotNull(createAuthenticatorResponseObject);
            Assert.NotNull(createAuthenticatorResponseObject.Code);
        }

        [Fact]
        public async Task TestCreateNonExistingDomainLoginDetailsReturnsNotFound()
        {
            CreateAuthenticatorRequest createAuthenticatorRequest = new()
            {
                LoginDetailsId = 1
            };

            var createAuthenticatorResponse = await CreateAuthenticatorAsync(createAuthenticatorRequest);
            Assert.Equal(HttpStatusCode.NotFound, createAuthenticatorResponse.StatusCode);
        }

        [Fact]
        public async Task TestCreateIncorrectTimestampAuthenticatorReturnsBadRequest()
        {
            // Create a domain
            await RegisterDomainAsync("authenticatortests.com");

            // Generate a random secret key
            byte[] secretKey = PasswordUtil.GenerateSecurePassword(32);
            string secretKeyBase32 = Base32Encoding.ToString(secretKey);
            byte[] encryptedSecretKey = await PasswordUtil.EncryptMessage(_sharedSecretKey, Encoding.UTF8.GetBytes(secretKeyBase32));

            CreateAuthenticatorRequest createAuthenticatorRequest = new()
            {
                LoginDetailsId = 1,
                SecretKey = Convert.ToBase64String(encryptedSecretKey),
                Timestamp = "invalid"
            };

            var createAuthenticatorResponse = await CreateAuthenticatorAsync(createAuthenticatorRequest);
            Assert.Equal(HttpStatusCode.BadRequest, createAuthenticatorResponse.StatusCode);
        }

        [Fact]
        public async Task TestGetAuthenticatorReturnsOk()
        {
            // Create a domain
            await RegisterDomainAsync("authenticatortests.com");

            // Generate a random secret key
            byte[] secretKey = PasswordUtil.GenerateSecurePassword(32);
            string secretKeyBase32 = Base32Encoding.ToString(secretKey);
            byte[] encryptedSecretKey = await PasswordUtil.EncryptMessage(_sharedSecretKey, Encoding.UTF8.GetBytes(secretKeyBase32));

            CreateAuthenticatorRequest createAuthenticatorRequest = new()
            {
                LoginDetailsId = 1,
                SecretKey = Convert.ToBase64String(encryptedSecretKey),
                Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var createAuthenticatorResponse = await CreateAuthenticatorAsync(createAuthenticatorRequest);
            Assert.Equal(HttpStatusCode.OK, createAuthenticatorResponse.StatusCode);

            // Verify that the response contains the authenticator code
            var createAuthenticatorResponseContent = await createAuthenticatorResponse.Content.ReadAsStringAsync();
            Assert.NotNull(createAuthenticatorResponseContent);
            
            var createAuthenticatorResponseObject = JsonSerializer.Deserialize<AuthenticatorCodeResponse>(createAuthenticatorResponseContent);
            Assert.NotNull(createAuthenticatorResponseObject);
            Assert.NotNull(createAuthenticatorResponseObject.Code);

            // Get the authenticator code
            var getAuthenticatorCodeResponse = await GetAuthenticatorCodeAsync(1, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            Assert.Equal(HttpStatusCode.OK, getAuthenticatorCodeResponse.StatusCode);

            // Verify that the response contains the authenticator code
            var getAuthenticatorCodeResponseContent = await getAuthenticatorCodeResponse.Content.ReadAsStringAsync();
            Assert.NotNull(getAuthenticatorCodeResponseContent);
            
            var getAuthenticatorCodeResponseObject = JsonSerializer.Deserialize<AuthenticatorCodeResponse>(getAuthenticatorCodeResponseContent);
            Assert.NotNull(getAuthenticatorCodeResponseObject);
            Assert.NotNull(getAuthenticatorCodeResponseObject.Code);
        }

        [Fact]
        public async Task TestGetIncorrectTimestampAuthenticatorReturnsBadRequest()
        {
            // Create a domain
            await RegisterDomainAsync("authenticatortests.com");

            // Generate a random secret key
            byte[] secretKey = PasswordUtil.GenerateSecurePassword(32);
            string secretKeyBase32 = Base32Encoding.ToString(secretKey);
            byte[] encryptedSecretKey = await PasswordUtil.EncryptMessage(_sharedSecretKey, Encoding.UTF8.GetBytes(secretKeyBase32));

            CreateAuthenticatorRequest createAuthenticatorRequest = new()
            {
                LoginDetailsId = 1,
                SecretKey = Convert.ToBase64String(encryptedSecretKey),
                Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var createAuthenticatorResponse = await CreateAuthenticatorAsync(createAuthenticatorRequest);
            Assert.Equal(HttpStatusCode.OK, createAuthenticatorResponse.StatusCode);

            // Verify that the response contains the authenticator code
            var createAuthenticatorResponseContent = await createAuthenticatorResponse.Content.ReadAsStringAsync();
            Assert.NotNull(createAuthenticatorResponseContent);
            
            var createAuthenticatorResponseObject = JsonSerializer.Deserialize<AuthenticatorCodeResponse>(createAuthenticatorResponseContent);
            Assert.NotNull(createAuthenticatorResponseObject);
            Assert.NotNull(createAuthenticatorResponseObject.Code);

            // Get the authenticator code
            var getAuthenticatorCodeResponse = await GetAuthenticatorCodeAsync(1, "invalid");
            Assert.Equal(HttpStatusCode.BadRequest, getAuthenticatorCodeResponse.StatusCode);
        }

        [Fact]
        public async Task TestGetNonExistingAuthenticatorReturnsNotFound()
        {
            // Get the authenticator code
            var getAuthenticatorCodeResponse = await GetAuthenticatorCodeAsync(1, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            Assert.Equal(HttpStatusCode.NotFound, getAuthenticatorCodeResponse.StatusCode);
        }

        [Fact]
        public async Task TestDeleteAuthenticatorReturnsOk()
        {
            // Create a domain
            await RegisterDomainAsync("authenticatortests.com");

            // Generate a random secret key
            byte[] secretKey = PasswordUtil.GenerateSecurePassword(32);
            string secretKeyBase32 = Base32Encoding.ToString(secretKey);
            byte[] encryptedSecretKey = await PasswordUtil.EncryptMessage(_sharedSecretKey, Encoding.UTF8.GetBytes(secretKeyBase32));

            CreateAuthenticatorRequest createAuthenticatorRequest = new()
            {
                LoginDetailsId = 1,
                SecretKey = Convert.ToBase64String(encryptedSecretKey),
                Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var createAuthenticatorResponse = await CreateAuthenticatorAsync(createAuthenticatorRequest);
            Assert.Equal(HttpStatusCode.OK, createAuthenticatorResponse.StatusCode);

            // Verify that the response contains the authenticator code
            var createAuthenticatorResponseContent = await createAuthenticatorResponse.Content.ReadAsStringAsync();
            Assert.NotNull(createAuthenticatorResponseContent);
            
            var createAuthenticatorResponseObject = JsonSerializer.Deserialize<AuthenticatorCodeResponse>(createAuthenticatorResponseContent);
            Assert.NotNull(createAuthenticatorResponseObject);
            Assert.NotNull(createAuthenticatorResponseObject.Code);

            // Delete the authenticator
            var deleteAuthenticatorResponse = await DeleteAuthenticatorAsync(1);
            Assert.Equal(HttpStatusCode.NoContent, deleteAuthenticatorResponse.StatusCode);
        }

        [Fact]
        public async Task TestDeleteNonExistingAuthenticatorReturnsNotFound()
        {
            // Delete a non-existing authenticator
            var deleteAuthenticatorResponse = await DeleteAuthenticatorAsync(1);
            Assert.Equal(HttpStatusCode.NotFound, deleteAuthenticatorResponse.StatusCode);
        }
    }
}
