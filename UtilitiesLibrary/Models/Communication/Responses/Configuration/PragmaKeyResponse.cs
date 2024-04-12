using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class PragmaKeyResponse
    {
        [JsonInclude]
        [JsonPropertyName("key")]
        public string KeyBase64 { get; set; }
    }
}
