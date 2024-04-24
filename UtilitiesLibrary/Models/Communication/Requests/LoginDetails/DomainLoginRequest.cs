using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to fetch, if any, the user login credentials for the current domain.
    /// Expected response is a <see cref="DomainLoginResponse"/>.
    /// </summary>
    internal class DomainLoginRequest
    {
        // The source ID, indicating which script is requesting the login credentials.
        [JsonInclude]
        internal int SourceId { get; set; } = 0;

        // The domain (website.com) for which the user is requesting login credentials.
        [JsonInclude]
        internal string Domain { get; set; } = string.Empty;

        // The username for which the user is requesting login credentials.
        // If it is null, return first login details found for the domain.
        [JsonInclude]
        internal string? Username { get; set; }
    }
}
