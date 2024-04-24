using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to edit login details.
    /// Expected response is a <see cref="LoginDetailsEditResponse"/>.
    /// </summary>
    internal class LoginDetailsEditRequest
    {
        [JsonInclude]
        internal int SourceId { get; set; } = 0;

        [JsonInclude]
        internal int LoginDetailsId;

        [JsonInclude]
        internal string? Username { get; set; }

        [JsonInclude]
        internal string? Password { get; set; }
    }
}
