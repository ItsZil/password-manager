using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class CreateAuthenticatorRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; } = 1;

        [JsonInclude]
        [JsonPropertyName("loginDetailsId")]
        public int LoginDetailsId { get; set; }

        [JsonInclude]
        [JsonPropertyName("secretKey")]
        public string SecretKey { get; set; }

        [JsonInclude]
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }
}
