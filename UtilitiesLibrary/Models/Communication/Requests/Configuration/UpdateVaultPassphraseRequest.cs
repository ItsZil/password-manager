using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class UpdateVaultPassphraseRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; }

        [JsonInclude]
        [JsonPropertyName("vaultRawKeyBase64")]
        public string VaultRawKeyBase64 { get; set; }
    }
}
