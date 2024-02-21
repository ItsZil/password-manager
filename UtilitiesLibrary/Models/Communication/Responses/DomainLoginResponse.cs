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
        internal bool HasCredentials { get; set; } = false;

        // A flag to indicate if the user has permission to access the login details. TODO: potentially convert to an int to indicate failure reason? If failed user agent or authenticator, enforce authentication etc.
        [JsonInclude]
        internal bool HasPermission { get; set; } = false;

        // A flag to indicate if the user has 2FA enabled for the requested domain.
        [JsonInclude]
        internal bool Has2FA { get; set; } = false;

        // The user's username for the requested domain.
        [JsonInclude]
        internal string Username { get; set; }

        // The user's password for the requested domain. TODO: not plain-text?
        [JsonInclude]
        internal string Password { get; set; }

        /// <summary>
        /// A constructor for when the user has login credentials for the requested domain and has successfully authenticated.
        /// </summary>
        /// <param name="username">The user's username (email, phone number, etc.)</param>
        /// <param name="password">The user's password</param>
        /// <param name="has2FA">A flag indicating if the user has 2FA enabled for the requested domain</param>
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
