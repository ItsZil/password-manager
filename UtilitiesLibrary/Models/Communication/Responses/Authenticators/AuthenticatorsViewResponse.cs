using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A response to request to get a list of authenticators for all login details.
    /// </summary>
    internal class AuthenticatorsViewResponse
    {
        // The ID of the authenticator.
        [JsonInclude]
        internal int AuthenticatorId { get; set; }

        // The domain (website.com)
        [JsonInclude]
        internal string Domain { get; set; }

        // The user's username
        [JsonInclude]
        internal string Username { get; set; }

        // The date when the authenticator was last used.
        [JsonInclude]
        public DateTime LastUsedDate { get; set; }
    }
}
