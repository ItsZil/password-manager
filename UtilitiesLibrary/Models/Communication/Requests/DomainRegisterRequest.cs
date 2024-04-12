using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to fetch, if any, the user login credentials for the current domain.
    /// Expected response is a <see cref="DomainRegisterResponse"/>.
    /// </summary>
    internal class DomainRegisterRequest
    {
        // The source ID, indicating which script is requesting registration.
        [JsonInclude]
        internal int SourceId { get; set; } = 0;

        // The domain (website.com) for which the user is requesting registration.
        [JsonInclude]
        internal string Domain { get; set; } = string.Empty;

        // The user's username for the requested domain.
        [JsonInclude]
        internal string Username { get; set; }

        // The user's client-side encrypted base64 encoded password for the requested domain.
        // If null, generate a new secure password for the user.
        [JsonInclude]
        internal string? Password { get; set; }
    }
}
