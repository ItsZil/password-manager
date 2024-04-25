using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class AuthenticatorCodeResponse
    {
        [JsonInclude]
        [JsonPropertyName("authenticatorId")]
        public int AuthenticatorId { get; set; }

        [JsonInclude]
        [JsonPropertyName("code")]
        public string Code { get; set; }
    }
}
