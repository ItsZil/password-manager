using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class PathCheckResponse
    {
        [JsonInclude]
        [JsonPropertyName("pathValid")]
        public bool PathValid { get; set; }
    }
}
