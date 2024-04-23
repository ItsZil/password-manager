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
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(PasskeyTests))]
    public class PasskeyTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        // Creation data
        private string PublicKeyB64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAER9VPZ8utqJ/jv7UW/9WVISlwVFpz9bZ2Ln0bfV/kManuAgr19LN+btGIVsCEuVEJ2MURJwhL7Ysh3L8MHysR8w==";
        private string UserIdB64 = "6qzHUyNUwpqntsW/20xp/A==";
        private string CredentialIdB64 = "AbnMIjb6LVvQ0Ph2JhXMIJO5iH7GddrJ6H07X9cdhx_9TSRolyT-ofMqcoCmRqjyOxCJxeRDLlgwGLPST7550EU";

        // Verification data
        private string AuthenticatorDataB64 = "jHEnCkUM4kun6g0gdVrlupVDryUPDszSQfvxqH2hjo8FAAAAAQ==";
        private string SignatureB64 = "MEUCID9thrhDKEUT7kBsroo3iatIGqRqUig9LtdK6eYZtijsAiEAkfPDn4qQW1AuNVXa+XQpwVhIQok493in8oc+Dhwrbjc=";

        public PasskeyTests()
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
            var registerApiEndpoint = "/api/register";
            var registerRequest = new DomainRegisterRequest { SourceId = 1, Domain = domain, Username = "passkeytests" };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");
            return await _client.PostAsync(registerApiEndpoint, registerRequestContent);
        }

        private async Task<HttpResponseMessage> CreatePasskeyAsync(PasskeyCreationRequest request)
        {
            var createPasskeyApiEndpoint = "/api/passkey";
            var createPasskeyRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            return await _client.PostAsync(createPasskeyApiEndpoint, createPasskeyRequestContent);
        }

        private async Task<HttpResponseMessage> GetPasskeyCredentialsAsync()
        {
            var getPasskeyCredentialApiEndpoint = "/api/passkey?sourceId=1&loginDetailsId=1";
            return await _client.GetAsync(getPasskeyCredentialApiEndpoint);
        }

        private async Task<HttpResponseMessage> VerifyPasskeyAsync(PasskeyVerificationRequest request)
        {
            var verifyPasskeyApiEndpoint = "/api/passkey/verify";
            var verifyPasskeyRequestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            return await _client.PostAsync(verifyPasskeyApiEndpoint, verifyPasskeyRequestContent);
        }

        private async Task<PasskeyCreationRequest> CreatePasskeyCreationRequest()
        {
            // Create a domain
            var domainResponse = await RegisterDomainAsync("passkeytests.com");
            Assert.Equal(HttpStatusCode.OK, domainResponse.StatusCode);

            // Retrieve domain ID
            var domainResponseContent = await domainResponse.Content.ReadAsStringAsync();
            var domainRegisterResponse = JsonSerializer.Deserialize<DomainRegisterResponse>(domainResponseContent);
            Assert.NotNull(domainRegisterResponse);

            // Generate a random challenge
            byte[] randomChallenge = new byte[16];
            SecureRandom.Fill(randomChallenge);
            byte[] encryptedChallenge = await PasswordUtil.EncryptMessage(_sharedSecretKey, randomChallenge);

            PasskeyCreationRequest request = new()
            {
                SourceId = 1,
                AlgorithmId = -7,
                LoginDetailsId = domainRegisterResponse.Id,
                PublicKeyB64 = PublicKeyB64,
                UserIdB64 = UserIdB64,
                CredentialIdB64 = CredentialIdB64,
                ChallengeB64 = Convert.ToBase64String(encryptedChallenge)
            };
            return request;
        }

        private async Task<PasskeyVerificationRequest> CreatePasskeyVerificationRequest(string origin = "chrome-extension://icbeakhigcgladpiblnolcogihmcdoif", byte[]? challenge = null)
        {
            PasskeyCreationRequest creationRequest = await CreatePasskeyCreationRequest();
            var createdPasskeyResponse = await CreatePasskeyAsync(creationRequest);
            Assert.Equal(HttpStatusCode.Created, createdPasskeyResponse.StatusCode);

            var passkeyCredentialResponse = await GetPasskeyCredentialsAsync();
            Assert.Equal(HttpStatusCode.OK, passkeyCredentialResponse.StatusCode);

            var passkeyCredentialResponseContent = await passkeyCredentialResponse.Content.ReadAsStringAsync();
            Assert.NotNull(passkeyCredentialResponseContent);

            PasskeyCredentialResponse? passkeyCredential = JsonSerializer.Deserialize<PasskeyCredentialResponse>(passkeyCredentialResponseContent);
            Assert.NotNull(passkeyCredential);

            if (challenge == null)
            {
                // Extract challenge from the PasskeyCredentialResponse.
                byte[] decryptedChallenge = await PasswordUtil.DecryptMessage(_sharedSecretKey, Convert.FromBase64String(passkeyCredential.ChallengeB64));
                challenge = decryptedChallenge;
            }

            // Encode the challenge to Base64Url
            string challengeB64Url = Base64UrlEncoder.Encode(challenge);

            ClientDataJson clientDataJson = new()
            {
                Origin = origin,
                Challenge = challengeB64Url
            };
            string clientDataJsonB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clientDataJson)));

            // Hash clientDataJson with SHA-256
            using SHA256 sha256 = SHA256.Create();
            byte[] clientDataHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clientDataJson)));
            string clientDataHashB64 = Convert.ToBase64String(clientDataHash);

            PasskeyVerificationRequest verificationRequest = new()
            {
                CredentialIdB64 = passkeyCredential.CredentialIdB64,
                SignatureB64 = SignatureB64,
                AuthenticatorDataB64 = AuthenticatorDataB64,
                clientDataJsonBase64 = clientDataJsonB64,
                ClientDataHashB64 = clientDataHashB64,
                LoginDetailsId = 1
            };
            return verificationRequest;
        }


        [Fact]
        public async Task TestCreatePasskeyReturnsCreated()
        {
            // Prepare a PasskeyCreationRequest object
            PasskeyCreationRequest request = await CreatePasskeyCreationRequest();

            // Create a passkey
            var passkeyResponse = await CreatePasskeyAsync(request);
            Assert.Equal(HttpStatusCode.Created, passkeyResponse.StatusCode);
        }

        [Fact]
        public async Task TestCreateOverwritePasskeyReturnsCreated()
        {
            // Prepare a PasskeyCreationRequest object
            PasskeyCreationRequest request = await CreatePasskeyCreationRequest();

            // Create an initial passkey
            var passkeyResponse = await CreatePasskeyAsync(request);
            Assert.Equal(HttpStatusCode.Created, passkeyResponse.StatusCode);

            // Create a second passkey that will overwrite the first
            var passkeyResponse2 = await CreatePasskeyAsync(request);
            Assert.Equal(HttpStatusCode.Created, passkeyResponse2.StatusCode);
        }

        [Fact]
        public async Task TestCreatePasskeyIncorrectCredentialReturnsBadRequest()
        {
            // Prepare an incorrect PasskeyCreationRequest object by sending an empty challenge byte array
            PasskeyCreationRequest request = await CreatePasskeyCreationRequest();
            byte[] emptyChallenge = Array.Empty<byte>();
            request.ChallengeB64 = Convert.ToBase64String(emptyChallenge);

            // Create a passkey
            var passkeyResponse = await CreatePasskeyAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, passkeyResponse.StatusCode);
        }

        [Fact]
        public async Task TestCreatePasskeyNonExistingLoginDetailsReturnsNotFound()
        {
            // Prepare an incorrect PasskeyCreationRequest object by passing in a non-existing LoginDetailsId
            PasskeyCreationRequest request = await CreatePasskeyCreationRequest();
            request.LoginDetailsId = 2;

            // Create a passkey
            var passkeyResponse = await CreatePasskeyAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, passkeyResponse.StatusCode);
        }

        [Fact]
        public async Task TestGetPasskeyCredentialsReturnsOk()
        {
            // Prepare a PasskeyCreationRequest object
            PasskeyCreationRequest request = await CreatePasskeyCreationRequest();

            // Create a passkey
            var passkeyResponse = await CreatePasskeyAsync(request);
            Assert.Equal(HttpStatusCode.Created, passkeyResponse.StatusCode);

            // Retrieve the passkey credentials
            var passkeyCredentialResponse = await GetPasskeyCredentialsAsync();
            Assert.Equal(HttpStatusCode.OK, passkeyCredentialResponse.StatusCode);

            // Deserialize the response
            var passkeyCredentialResponseContent = await passkeyCredentialResponse.Content.ReadAsStringAsync();
            var passkeyCredentialResponseObject = JsonSerializer.Deserialize<PasskeyCredentialResponse>(passkeyCredentialResponseContent);
            Assert.NotNull(passkeyCredentialResponseObject);
        }

        [Fact]
        public async Task TestGetPasskeyCredentialsReturnsNotFound()
        {
            // Retrieve non-existing passkey credentials
            var passkeyCredentialResponse = await GetPasskeyCredentialsAsync();
            Assert.Equal(HttpStatusCode.NotFound, passkeyCredentialResponse.StatusCode);
        }

        /*[Fact]
        public async Task TestVerifyPasskeyReturnsOk()
        {
            // Prepare a PasskeyVerificationRequest object
            PasskeyVerificationRequest request = await CreatePasskeyVerificationRequest();

            // Verify the passkey
            var passkeyVerificationResponse = await VerifyPasskeyAsync(request);
            Assert.Equal(HttpStatusCode.OK, passkeyVerificationResponse.StatusCode);
        }*/

        [Fact]
        public async Task TestVerifyPasskeyEmptyCredentialIdReturnsBadRequest()
        {
            // Prepare a PasskeyVerificationRequest object with an incorrect credential ID
            PasskeyVerificationRequest request = await CreatePasskeyVerificationRequest();
            byte[] credentialsId = Array.Empty<byte>();
            request.CredentialIdB64 = Convert.ToBase64String(credentialsId);

            // Verify the passkey
            var passkeyVerificationResponse = await VerifyPasskeyAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, passkeyVerificationResponse.StatusCode);
        }

        [Fact]
        public async Task TestVerifyNonExistingPasskeyReturnsUnauthorized()
        {
            // Prepare a PasskeyVerificationRequest object with a credential ID that does not exist in the database
            PasskeyVerificationRequest request = await CreatePasskeyVerificationRequest();
            request.CredentialIdB64 = Convert.ToBase64String(new byte[32]);

            // Verify the passkey
            var passkeyVerificationResponse = await VerifyPasskeyAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, passkeyVerificationResponse.StatusCode);
        }

        [Fact]
        public async Task TestVerifyIncorrectChallengePasskeyReturnsUnauthorized()
        {
            // Prepare a PasskeyVerificationRequest object with an incorrect challenge
            PasskeyVerificationRequest request = await CreatePasskeyVerificationRequest(challenge: new byte[32]);

            // Verify the passkey
            var passkeyVerificationResponse = await VerifyPasskeyAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, passkeyVerificationResponse.StatusCode);
        }
    }
}
