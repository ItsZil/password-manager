using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class ClientDataJson
    {
        [JsonInclude]
        [JsonPropertyName("challenge")]
        public string Challenge { get; set; }

        [JsonInclude]
        [JsonPropertyName("origin")]
        public string Origin { get; set; }
    }
}
