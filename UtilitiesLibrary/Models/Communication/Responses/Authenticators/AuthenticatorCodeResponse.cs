using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class AuthenticatorCodeResponse
    {
        [JsonInclude]
        [JsonPropertyName("code")]
        public string Code { get; set; }
    }
}
