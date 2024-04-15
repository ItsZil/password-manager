using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class UnlockVaultRequest
    {
        [JsonInclude]
        [JsonPropertyName("passphraseBase64")]
        public string PassphraseBase64 { get; set; }
    }
}
