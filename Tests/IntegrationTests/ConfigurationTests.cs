using Microsoft.AspNetCore.Mvc.Testing;
using UtilitiesLibrary.Models;
using Server;
using Microsoft.AspNetCore.Hosting;
using Server.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(ConfigurationTests))]
    public class ConfigurationTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey1;
        private readonly byte[] _sharedSecretKey2;

        private readonly string _runningTestVaultLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"vault_{Guid.NewGuid()}");

        public ConfigurationTests()
        {
            // Ensure we have no leftover config file
            if (File.Exists(ConfigUtil.GetFileLocation()))
                File.Delete(ConfigUtil.GetFileLocation());

            Directory.CreateDirectory(_runningTestVaultLocation);

            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TEST_INTEGRATION");
            });
            _client = _factory.CreateClient();

            _sharedSecretKey1 = CompleteTestHandshake.GetSharedSecret(_client, 1);
            _sharedSecretKey2 = CompleteTestHandshake.GetSharedSecret(_client, 2);
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

            if (Directory.GetFiles(_runningTestVaultLocation).Length == 0) // SetupVault test creates a vault there.
                Directory.Delete(_runningTestVaultLocation, false);
        }

        private async Task<HttpResponseMessage> GeneratePassPhraseAsync(int wordCount)
        {
            var apiEndpoint = "api/generatepassphrase";
            var passphraseRequest = new PassphraseRequest { SourceId = 1, WordCount = wordCount };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(passphraseRequest), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);
            return response;
        }

        private async Task<HttpResponseMessage> GeneratePasswordAsync()
        {
            var apiEndpoint = "api/generatepassword";
            var response = await _client.GetAsync(apiEndpoint);
            return response;
        }

        private async Task<HttpResponseMessage> IsAbsolutePathValidAsync(string path)
        {
            var apiEndpoint = "api/isabsolutepathvalid";
            var pathCheckRequest = new PathCheckRequest { AbsolutePathUri = Uri.EscapeDataString(path) };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(pathCheckRequest), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);
            return response;
        }

        private async Task<HttpResponseMessage> SetupVaultAsync(string path, string vaultRawKeyBase64)
        {
            var apiEndpoint = "api/setupvault";
            var setupVaultRequest = new SetupVaultRequest { SourceId = 1, AbsolutePathUri = Uri.EscapeDataString(path), VaultRawKeyBase64 = vaultRawKeyBase64 };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(setupVaultRequest), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);
            return response;
        }

        private async Task<HttpResponseMessage> UnlockVaultAsync(string encryptedPassphraseB64)
        {
            var apiEndpoint = "api/unlockvault";
            var unlockVaultRequest = new UnlockVaultRequest { PassphraseBase64 = encryptedPassphraseB64 };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(unlockVaultRequest), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);
            return response;
        }

        [Fact]
        public async Task TestGenerateFourWordPassphraseReturnsOkAndPassphrase()
        {
            var response = await GeneratePassPhraseAsync(4);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            PassphraseResponse? responseObj = JsonSerializer.Deserialize<PassphraseResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<PassphraseResponse>(responseObj);
            Assert.False(string.IsNullOrWhiteSpace(responseObj.PassphraseBase64));
        }

        [Fact]
        public async Task TestGenerateIncorrectWordCountPassphraseReturnsBadRequest()
        {
            var response = await GeneratePassPhraseAsync(3);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task TestGeneratePasswordReturnsOkAndPassword()
        {
            var response = await GeneratePasswordAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            GeneratedPasswordResponse? responseObj = JsonSerializer.Deserialize<GeneratedPasswordResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<GeneratedPasswordResponse>(responseObj);
            Assert.False(string.IsNullOrWhiteSpace(responseObj.PasswordBase64));
        }

        [Fact]
        public async Task TestExistingAbsolutePathReturnsTrue()
        {
            var response = await IsAbsolutePathValidAsync(_runningTestVaultLocation);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            PathCheckResponse? responseObj = JsonSerializer.Deserialize<PathCheckResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<PathCheckResponse>(responseObj);
            Assert.True(responseObj.PathValid);
        }

        [Fact]
        public async Task TestNonExistingAbsolutePathReturnsFalse()
        {
            var response = await IsAbsolutePathValidAsync("D:\\Invalid\\Path");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            PathCheckResponse? responseObj = JsonSerializer.Deserialize<PathCheckResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<PathCheckResponse>(responseObj);
            Assert.False(responseObj.PathValid);
        }

        [Fact]
        public async Task TestSetupVaultReturnsOkAndExists()
        {
            string passphrase = "just a test passphrase";
            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] encryptedPassphrase = await PasswordUtil.EncryptMessage(_sharedSecretKey1, passphraseBytes);

            var response = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(encryptedPassphrase));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.True(File.Exists(Path.Combine(_runningTestVaultLocation, "vault.db")));
        }

        [Fact]
        public async Task TestImportVaultCorrectPassphraseReturnsOk()
        {
            string passphrase = "just a test passphrase";
            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] encryptedPassphrase = await PasswordUtil.EncryptMessage(_sharedSecretKey1, passphraseBytes);

            var setupVaultResponse = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(encryptedPassphrase));

            Assert.Equal(HttpStatusCode.Created, setupVaultResponse.StatusCode);
            Assert.True(File.Exists(Path.Combine(_runningTestVaultLocation, "vault.db")));

            var importVaultResponse = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(encryptedPassphrase));

            Assert.Equal(HttpStatusCode.Created, importVaultResponse.StatusCode);
        }

        [Fact]
        public async Task TestImportVaultIncorrectPassphraseReturnsBadRequest()
        {
            string passphrase = "just a test passphrase";
            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] encryptedPassphrase = await PasswordUtil.EncryptMessage(_sharedSecretKey1, passphraseBytes);

            var setupVaultResponse = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(encryptedPassphrase));

            Assert.Equal(HttpStatusCode.Created, setupVaultResponse.StatusCode);
            Assert.True(File.Exists(Path.Combine(_runningTestVaultLocation, "vault.db")));

            string incorrectPassphrase = "definitely not the same";
            byte[] incorrectPassphraseBytes = Encoding.UTF8.GetBytes(incorrectPassphrase);
            byte[] incorrectEncryptedPassphrase = await PasswordUtil.EncryptMessage(_sharedSecretKey1, incorrectPassphraseBytes);

            var importVaultResponse = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(incorrectEncryptedPassphrase));

            Assert.Equal(HttpStatusCode.BadRequest, importVaultResponse.StatusCode);
        }

        [Fact]
        public async Task TestUnlockVaultCorrectPassphraseReturnsTokens()
        {
            string passphrase = "just a test passphrase";
            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] encryptedPassphraseSetup = await PasswordUtil.EncryptMessage(_sharedSecretKey1, passphraseBytes);

            var setupVaultResponse = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(encryptedPassphraseSetup));

            Assert.Equal(HttpStatusCode.Created, setupVaultResponse.StatusCode);
            Assert.True(File.Exists(Path.Combine(_runningTestVaultLocation, "vault.db")));

            byte[] encryptedPassphraseUnlock = await PasswordUtil.EncryptMessage(_sharedSecretKey1, passphraseBytes);
            var unlockVaultResponse = await UnlockVaultAsync(Convert.ToBase64String(encryptedPassphraseUnlock));

            Assert.Equal(HttpStatusCode.Created, unlockVaultResponse.StatusCode);

            string responseString = await unlockVaultResponse.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            TokenResponse? responseObj = JsonSerializer.Deserialize<TokenResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<TokenResponse>(responseObj);
            Assert.False(string.IsNullOrWhiteSpace(responseObj.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(responseObj.RefreshToken));
        }


        [Fact]
        public async Task TestUnlockVaultIncorrectPassphraseReturnsForbidden()
        {
            string passphrase = "just a test passphrase";
            byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] encryptedPassphraseSetup = await PasswordUtil.EncryptMessage(_sharedSecretKey1, passphraseBytes);

            var setupVaultResponse = await SetupVaultAsync(_runningTestVaultLocation, Convert.ToBase64String(encryptedPassphraseSetup));

            Assert.Equal(HttpStatusCode.Created, setupVaultResponse.StatusCode);
            Assert.True(File.Exists(Path.Combine(_runningTestVaultLocation, "vault.db")));

            string incorrectPassphrase = "just an incorrect passphrase";
            byte[] incorrectPassphraseBytes = Encoding.UTF8.GetBytes(incorrectPassphrase);
            byte[] encryptedPassphraseUnlock = await PasswordUtil.EncryptMessage(_sharedSecretKey2, incorrectPassphraseBytes);

            var unlockVaultResponse = await UnlockVaultAsync(Convert.ToBase64String(encryptedPassphraseUnlock));

            Assert.Equal(HttpStatusCode.Forbidden, unlockVaultResponse.StatusCode);
        }
    }
}
