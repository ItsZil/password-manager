using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class PassphraseRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; }

        [JsonInclude]
        [JsonPropertyName("wordCount")]
        public int WordCount { get; set; }
    }
}
