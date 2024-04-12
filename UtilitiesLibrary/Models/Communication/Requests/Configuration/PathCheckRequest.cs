using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class PathCheckRequest
    {
        [JsonInclude]
        [JsonPropertyName("absolutePathUri")]
        public string AbsolutePathUri { get; set; }
    }
}
