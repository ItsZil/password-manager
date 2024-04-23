using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    internal class SetExtraAuthRequest
    {
        [JsonInclude]
        [JsonPropertyName("loginDetailsId")]
        public int LoginDetailsId { get; set; }

        [JsonInclude]
        [JsonPropertyName("extraAuthId")]
        public int ExtraAuthId { get; set; }
    }
}
