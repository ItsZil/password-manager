using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class GetAuthenticatorCodeResponse
    {
        [JsonInclude]
        [JsonPropertyName("authenticatorCode")]
        public string AuthenticatorCode { get; set; }
    }
}
