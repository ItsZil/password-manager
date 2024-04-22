using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class GetPinCodeResponse
    {
        [JsonInclude]
        [JsonPropertyName("pinCode")]
        public string PinCode { get; set; }
    }
}
