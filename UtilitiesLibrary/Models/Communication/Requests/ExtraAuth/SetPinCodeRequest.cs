using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class SetPinCodeRequest
    {
        [JsonInclude]
        [JsonPropertyName("sourceId")]
        public int SourceId { get; set; } = 1;

        [JsonInclude]
        [JsonPropertyName("loginDetailsId")]
        public int LoginDetailsId { get; set; }

        [JsonInclude]
        [JsonPropertyName("pinCode")]
        public string PinCode { get; set; }
    }
}
