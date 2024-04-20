using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class SetupVaultRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; }

        [JsonInclude]
        [JsonPropertyName("absolutePathUri")]
        public string? AbsolutePathUri { get; set; } // If null, store in My Documents.

        [JsonInclude]
        [JsonPropertyName("vaultRawKeyBase64")]
        public string VaultRawKeyBase64 { get; set; }
    }
}
