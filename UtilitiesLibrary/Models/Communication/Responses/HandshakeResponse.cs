using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class HandshakeResponse
    {
        [JsonInclude]
        [JsonPropertyName("serverPublicKey")]
        public byte[] ServerPublicKey { get; set; }
    }
}
