using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class ExportVaultResponse
    {
        [JsonInclude]
        [JsonPropertyName("absolutePathUri")]
        public string AbsolutePathUri { get; set; }
    }
}
