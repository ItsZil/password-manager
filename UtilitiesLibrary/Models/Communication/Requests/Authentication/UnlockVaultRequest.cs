using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class UnlockVaultRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; } = 1; // Default: popup

        [JsonInclude]
        [JsonPropertyName("passphraseBase64")]
        public string PassphraseBase64 { get; set; }
    }
}
