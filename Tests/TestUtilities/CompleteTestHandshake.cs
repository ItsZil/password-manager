using Server.Utilities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UtilitiesLibrary.Models;

namespace Tests.TestUtilities
{
    internal static class CompleteTestHandshake
    {
        // Used in integration tests to get the shared secret key for encryption and decryption
        internal static byte[] GetSharedSecret(HttpClient httpClient)
        {
            KeyProvider keyProvider = new();

            ECDiffieHellman clientECDH;
            byte[] clientPublicKey = keyProvider.GenerateClientPublicKey(out clientECDH);

            var apiEndpoint = "/api/handshake";
            HandshakeRequest request = new() { ClientPublicKey = clientPublicKey };
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = httpClient.PostAsync(apiEndpoint, requestContent).Result;
            var responseContent = response.Content.ReadAsStringAsync().Result;        
            Assert.NotNull(responseContent);

            HandshakeResponse? handshakeResponse = JsonSerializer.Deserialize<HandshakeResponse>(responseContent);
            Assert.NotNull(handshakeResponse);

            byte[] serverPublicKey = handshakeResponse.ServerPublicKey;
            keyProvider.ComputeSharedSecretTests(clientECDH, serverPublicKey);

            return keyProvider.GetSharedSecret();
        }
    }
}
