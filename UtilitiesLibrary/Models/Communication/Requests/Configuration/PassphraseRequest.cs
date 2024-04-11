using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class PassphraseRequest
    {
        [JsonInclude]
        [JsonPropertyName("wordCount")]
        public int WordCount { get; set; }
    }
}
