using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class RefreshTokenRequest
    {
        [JsonInclude]
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }
}
