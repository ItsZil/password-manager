using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to initiate a handshake with the server.
    /// </summary>
    public class HandshakeRequest
    {
        [JsonInclude]
        [JsonPropertyName("clientPublicKey")]
        public string ClientPublicKeyBase64 { get; set; }
    }
}
