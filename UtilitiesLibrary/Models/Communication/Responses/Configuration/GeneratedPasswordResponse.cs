using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    public class GeneratedPasswordResponse
    {
        [JsonInclude]
        [JsonPropertyName("password")]
        public string PasswordBase64 { get; set; }
    }
}
