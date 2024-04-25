using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to verify a passkey.
    /// </summary>
    public class PasskeyVerificationRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; } = 1;

        [JsonInclude]
        [JsonPropertyName("credentialId")]
        public string CredentialIdB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("signature")]
        public string SignatureB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("authenticatorData")]
        public string AuthenticatorDataB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("clientDataJson")]
        public string clientDataJsonBase64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("clientDataHash")]
        public string ClientDataHashB64 { get; set; } // Base64 encoded

        [JsonInclude]
        [JsonPropertyName("loginDetailsId")]
        public int LoginDetailsId { get; set; }

        [JsonInclude]
        [JsonPropertyName("isForLogin")]
        public bool IsForLogin { get; set; } = false;
    }
}
