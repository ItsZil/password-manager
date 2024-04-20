using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to store a new passkey in the database.
    /// </summary>
    public class PasskeyCreationRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; } // Expecting 1 (passwords)

        [JsonInclude]
        [JsonPropertyName("credentialId")]
        public string CredentialIdB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("userId")]
        public string UserIdB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("publicKey")]
        public string PublicKeyB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("challenge")]
        public string ChallengeB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("loginDetailsId")]
        public int LoginDetailsId { get; set; }
    }
}
