using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A response to a request to create a passkey.
    /// </summary>
    public class PasskeyCredentialResponse
    {
        [JsonInclude]
        [JsonPropertyName("credentialId")]
        public string CredentialIdB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("userId")]
        public string UserId { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("publicKey")]
        public string PublicKeyB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("challenge")]
        public string ChallengeB64 { get; set; } // Base64 encoded
    }
}
