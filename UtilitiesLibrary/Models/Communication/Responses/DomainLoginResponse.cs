using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A response to a <see cref="DomainLoginRequest"/> containing the user's login credentials for the requested domain.
    /// </summary>
    internal class DomainLoginResponse
    {
        // A flag to indicate if the user has login credentials for the requested domain.
        [JsonInclude]
        [JsonPropertyName("hasCredentials")]
        internal bool HasCredentials { get; set; } = false;

        // A flag to indicate if the user has permission to access the login details. TODO: potentially convert to an int to indicate failure reason? If failed user agent or authenticator, enforce authentication etc.
        [JsonInclude]
        [JsonPropertyName("hasPermission")]
        internal bool HasPermission { get; set; } = false;

        // A flag to indicate if the user has 2FA enabled for the requested domain.
        [JsonInclude]
        [JsonPropertyName("has2FA")]
        internal bool Has2FA { get; set; } = false;

        // The user's username for the requested domain.
        [JsonInclude]
        [JsonPropertyName("username")]
        internal string Username { get; set; }

        // The user's encrypted password (using shared secret key) base64 encoded string for the requested domain.
        [JsonInclude]
        [JsonPropertyName("password")]
        internal string Password { get; set; }

        /// <summary>
        /// A constructor for when the user has login credentials for the requested domain and has successfully authenticated.
        /// </summary>
        /// <param name="username">The user's username (email, phone number, etc.)</param>
        /// <param name="password">The user's encrypted password as a base64 encoded string</param>
        /// <param name="has2FA">A flag indicating if the user has 2FA enabled for the requested domain</param>
        [JsonConstructor]
        internal DomainLoginResponse(string username, string password, bool has2FA)
        {
            HasCredentials = true;
            HasPermission = true;
            Has2FA = has2FA;

            Username = username;
            Password = password;
        }
    }
}
