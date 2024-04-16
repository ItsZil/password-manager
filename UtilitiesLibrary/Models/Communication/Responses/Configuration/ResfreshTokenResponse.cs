using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class RefreshTokenResponse
    {
        [JsonInclude]
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }

        [JsonInclude]
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }
}
