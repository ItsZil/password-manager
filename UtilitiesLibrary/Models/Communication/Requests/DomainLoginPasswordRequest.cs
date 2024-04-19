using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to fetch the user's password for a domain.
    /// </summary>
    internal class DomainLoginPasswordRequest
    {
        // The source ID, indicating which script is requesting registration.
        [JsonInclude]
        internal int SourceId { get; set; } = 0;

        // The login details ID for which to retrieve the password.
        [JsonInclude]
        internal int LoginDetailsId { get; set; }
    }
}
