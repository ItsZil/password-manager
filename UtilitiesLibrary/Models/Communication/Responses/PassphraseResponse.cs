using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class PassphraseResponse
    {
        [JsonInclude]
        [JsonPropertyName("passphrase")]
        public string PassphraseBase64 { get; set; }
    }
}
