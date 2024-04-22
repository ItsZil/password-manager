using Microsoft.AspNetCore.Mvc.Testing;
using UtilitiesLibrary.Models;
using Server;
using Microsoft.AspNetCore.Hosting;
using Server.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Geralt;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(ExtraAuthTests))]
    public class ExtraAuthTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        public ExtraAuthTests()
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

        private async Task<HttpResponseMessage> GetExtraAuthAsync(int loginDetailsId)
        {
            var getExtraAuthApiEndpoint = $"/api/extraauth?loginDetailsId={loginDetailsId}";
            var response = await _client.GetAsync(getExtraAuthApiEndpoint);
            return response;
        }

        private async Task<HttpResponseMessage> SetExtraAuthAsync(SetExtraAuthRequest request)
        {
            var setExtraAuthApiEndpoint = "/api/extraauth";
            var setExtraAuthRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _client.PutAsync(setExtraAuthApiEndpoint, setExtraAuthRequestContent);
            return response;
        }

        private async Task<HttpResponseMessage> RemoveExtraAuthAsync(int loginDetailsId)
        {
            var deleteExtraAuthApiEndpoint = $"/api/extraauth?loginDetailsId={loginDetailsId}";
            var response = await _client.DeleteAsync(deleteExtraAuthApiEndpoint);
            return response;
        }

        private async Task<HttpResponseMessage> CreatePasskeyAsync(PasskeyCreationRequest request)
        {
            var createPasskeyApiEndpoint = "/api/passkey";
            var createPasskeyRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            return await _client.PostAsync(createPasskeyApiEndpoint, createPasskeyRequestContent);
        }

        private async Task<HttpResponseMessage> SetPinCodeAsync(CreatePinCodeRequest request)
        {
            // Encrypt the pin code
            byte[] pinCodeBytes = Encoding.UTF8.GetBytes(request.PinCode);
            byte[] encryptedPinCodeBytes = await PasswordUtil.EncryptMessage(_sharedSecretKey, pinCodeBytes);
            string encryptedPinCode = Convert.ToBase64String(encryptedPinCodeBytes);
            request.PinCode = encryptedPinCode;

            var setPinCodeApiEndpoint = "/api/pincode";
            var setPinCodeRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(setPinCodeApiEndpoint, setPinCodeRequestContent);
            return response;
        }

        private async Task<PasskeyCreationRequest> CreatePasskeyCreationRequest()
        {
            // Create a domain
            _ = await RegisterDomainAsync("extraauthtests.com");

            // Generate a random challenge
            byte[] randomChallenge = new byte[16];
            SecureRandom.Fill(randomChallenge);
            byte[] encryptedChallenge = await PasswordUtil.EncryptMessage(_sharedSecretKey, randomChallenge);

            PasskeyCreationRequest request = new()
            {
                SourceId = 1,
                AlgorithmId = -7,
                LoginDetailsId = 1,
                PublicKeyB64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAER9VPZ8utqJ/jv7UW/9WVISlwVFpz9bZ2Ln0bfV/kManuAgr19LN+btGIVsCEuVEJ2MURJwhL7Ysh3L8MHysR8w==",
                UserIdB64 = "6qzHUyNUwpqntsW/20xp/A==",
                CredentialIdB64 = "AbnMIjb6LVvQ0Ph2JhXMIJO5iH7GddrJ6H07X9cdhx_9TSRolyT-ofMqcoCmRqjyOxCJxeRDLlgwGLPST7550EU",
                ChallengeB64 = Convert.ToBase64String(encryptedChallenge)
            };
            return request;
        }

        private async Task<HttpResponseMessage> GetPasskeyCredentialsAsync()
        {
            var getPasskeyCredentialApiEndpoint = "/api/passkey?sourceId=1&loginDetailsId=1";
            return await _client.GetAsync(getPasskeyCredentialApiEndpoint);
        }

        [Fact]
        public async Task TestGetExtraAuthTypeReturnsOk()
        {
            _ = await RegisterDomainAsync("test.com");

            var extraAuthResponse = await GetExtraAuthAsync(1);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);
        }

        [Fact]
        public async Task TestGetDefaultExtraAuthTypeReturnsNoneType()
        {
            // Create a domain
            _ = await RegisterDomainAsync("test.com");

            var extraAuthResponse = await GetExtraAuthAsync(1);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);

            var extraAuthContent = await extraAuthResponse.Content.ReadAsStringAsync();
            Assert.NotNull(extraAuthContent);

            var extraAuthId = int.Parse(extraAuthContent);
            Assert.Equal(1, extraAuthId);
        }

        [Fact]
        public async Task TestGetExtraAuthTypeReturnsNotFound()
        {
            var extraAuthResponse = await GetExtraAuthAsync(1);
            Assert.Equal(HttpStatusCode.NotFound, extraAuthResponse.StatusCode);
        }

        [Fact]
        public async Task TestSetPinCodeExtraAuthReturnsNoContent()
        {
            // Create a domain
            _ = await RegisterDomainAsync("test.com");

            CreatePinCodeRequest setPinCodeRequest = new()
            {
                LoginDetailsId = 1,
                PinCode = "1234"
            };
            await SetPinCodeAsync(setPinCodeRequest);

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = 1,
                ExtraAuthId = 2 // Pin code
            };
            var response = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task TestSetPassphraseExtraAuthReturnsNoContent()
        {
            var domainResponse = await RegisterDomainAsync("test.com");

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = 1,
                ExtraAuthId = 4 // Passphrase
            };

            var setExtraAuthResponse = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.NoContent, setExtraAuthResponse.StatusCode);

            // Verify that the login details now has the correct extra auth method
            var extraAuthResponse = await GetExtraAuthAsync(1);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);

            var extraAuthContent = await extraAuthResponse.Content.ReadAsStringAsync();
            Assert.NotNull(extraAuthContent);

            var extraAuthId = int.Parse(extraAuthContent);
            Assert.Equal(4, extraAuthId);
        }

        [Fact]
        public async Task TestSetPasskeyExtraAuthReturnsNoContent()
        {
            PasskeyCreationRequest passkeyCreationRequest = await CreatePasskeyCreationRequest();
            int loginDetailsId = passkeyCreationRequest.LoginDetailsId;

            var passkeyCreationResponse = await CreatePasskeyAsync(passkeyCreationRequest);
            Assert.Equal(HttpStatusCode.Created, passkeyCreationResponse.StatusCode);

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = loginDetailsId,
                ExtraAuthId = 3 // Passkey
            };

            var setExtraAuthResponse = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.NoContent, setExtraAuthResponse.StatusCode);

            // Verify that the login details now has the correct extra auth method
            var extraAuthResponse = await GetExtraAuthAsync(loginDetailsId);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);

            var extraAuthContent = await extraAuthResponse.Content.ReadAsStringAsync();
            Assert.NotNull(extraAuthContent);

            var extraAuthId = int.Parse(extraAuthContent);
            Assert.Equal(3, extraAuthId);
        }

        [Fact]
        public async Task TestSetIncorrectExtraAuthReturnsBadRequest()
        {
            // Create a domain
            _ = await RegisterDomainAsync("extraauthtests.com");

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = 1,
                ExtraAuthId = 5 // Non-existing extra auth type
            };

            var setExtraAuthResponse = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.BadRequest, setExtraAuthResponse.StatusCode);
        }

        [Fact]
        public async Task TestDeleteNoneExtraAuthReturnsNoContent()
        {
            // Create a domain
            _ = await RegisterDomainAsync("extraauthtests.com");

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = 1,
                ExtraAuthId = 1 // None
            };

            var setExtraAuthResponse = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.NoContent, setExtraAuthResponse.StatusCode);

            // Remove the extra auth method
            var removeExtraAuthResponse = await RemoveExtraAuthAsync(1);
            Assert.Equal(HttpStatusCode.NoContent, removeExtraAuthResponse.StatusCode);

            // Verify that the login details now has the correct extra auth method
            var extraAuthResponse = await GetExtraAuthAsync(1);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);

            var extraAuthContent = await extraAuthResponse.Content.ReadAsStringAsync();
            Assert.NotNull(extraAuthContent);

            var extraAuthId = int.Parse(extraAuthContent);
            Assert.Equal(1, extraAuthId);
        }

        [Fact]
        public async Task TestDeletePasskeyExtraAuthReturnsNoContent()
        {
            PasskeyCreationRequest passkeyCreationRequest = await CreatePasskeyCreationRequest();
            int loginDetailsId = passkeyCreationRequest.LoginDetailsId;

            var passkeyCreationResponse = await CreatePasskeyAsync(passkeyCreationRequest);
            Assert.Equal(HttpStatusCode.Created, passkeyCreationResponse.StatusCode);

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = loginDetailsId,
                ExtraAuthId = 3 // Passphrase
            };

            var setExtraAuthResponse = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.NoContent, setExtraAuthResponse.StatusCode);

            // Remove the extra auth method
            var removeExtraAuthResponse = await RemoveExtraAuthAsync(loginDetailsId);
            Assert.Equal(HttpStatusCode.NoContent, removeExtraAuthResponse.StatusCode);

            // Verify that the login details now has the correct extra auth method
            var extraAuthResponse = await GetExtraAuthAsync(loginDetailsId);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);

            var extraAuthContent = await extraAuthResponse.Content.ReadAsStringAsync();
            Assert.NotNull(extraAuthContent);

            var extraAuthId = int.Parse(extraAuthContent);
            Assert.Equal(1, extraAuthId);

            // Verify that the passkey no longer exists
            var passkeyResponse = await GetPasskeyCredentialsAsync();
            Assert.Equal(HttpStatusCode.NotFound, passkeyResponse.StatusCode);

        }

        [Fact]
        public async Task TestDeletePassphraseExtraAuthReturnsNoContent()
        {
            // Create a domain
            var domainResponse = await RegisterDomainAsync("extraauthtests.com");
            Assert.Equal(HttpStatusCode.OK, domainResponse.StatusCode);

            // Retrieve domain ID
            var domainResponseContent = await domainResponse.Content.ReadAsStringAsync();
            var domainRegisterResponse = JsonSerializer.Deserialize<DomainRegisterResponse>(domainResponseContent);
            Assert.NotNull(domainRegisterResponse);
            int loginDetailsId = domainRegisterResponse.Id;

            SetExtraAuthRequest setExtraAuthRequest = new()
            {
                LoginDetailsId = loginDetailsId,
                ExtraAuthId = 4 // Passphrase
            };

            var setExtraAuthResponse = await SetExtraAuthAsync(setExtraAuthRequest);
            Assert.Equal(HttpStatusCode.NoContent, setExtraAuthResponse.StatusCode);

            // Remove the extra auth method
            var removeExtraAuthResponse = await RemoveExtraAuthAsync(loginDetailsId);
            Assert.Equal(HttpStatusCode.NoContent, removeExtraAuthResponse.StatusCode);

            // Verify that the login details now has the correct extra auth method
            var extraAuthResponse = await GetExtraAuthAsync(loginDetailsId);
            Assert.Equal(HttpStatusCode.OK, extraAuthResponse.StatusCode);

            var extraAuthContent = await extraAuthResponse.Content.ReadAsStringAsync();
            Assert.NotNull(extraAuthContent);

            var extraAuthId = int.Parse(extraAuthContent);
            Assert.Equal(1, extraAuthId);
        }
    }
}
