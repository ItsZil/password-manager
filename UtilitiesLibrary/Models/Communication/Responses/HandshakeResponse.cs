using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class HandshakeResponse
    {
        [JsonInclude]
        [JsonPropertyName("serverPublicKey")]
        public string ServerPublicKeyBase64 { get; set; }
    }
}
