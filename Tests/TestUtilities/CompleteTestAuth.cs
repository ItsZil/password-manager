using Server.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;
using UtilitiesLibrary.Models;

namespace Tests.TestUtilities
{
    internal static class CompleteTestAuth
    {
        // Used in integration tests to retrieve a JWT access token for authorized requests
        internal static string GetAccessToken(HttpClient httpClient, byte[] sharedSecret)
        {
            var apiEndpoint = "/api/unlockvault";

            // Because the login and register request tests use the default vault, we can use the default password to unlock it.
            string defaultPassword = "DoNotUseThisVault";
            byte[] defaultPasswordBytes = PasswordUtil.ByteArrayFromPlain(defaultPassword);
            byte[] encryptedPassword = PasswordUtil.EncryptPassword(sharedSecret, defaultPasswordBytes).Result;

            UnlockVaultRequest request = new() { PassphraseBase64 = Convert.ToBase64String(encryptedPassword) };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = httpClient.PostAsync(apiEndpoint, requestContent).Result;
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var responseContent = response.Content.ReadAsStringAsync().Result;
            Assert.NotNull(responseContent);

            TokenResponse? tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            Assert.NotNull(tokenResponse);

            return tokenResponse.AccessToken;
        }
    }
}