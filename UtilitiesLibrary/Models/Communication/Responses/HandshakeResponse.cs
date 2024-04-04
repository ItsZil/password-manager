using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class HandshakeResponse
    {
        [JsonInclude]
        [JsonPropertyName("serverPublicKey")]
        public string ServerPublicKey { get; set; }
    }
}
