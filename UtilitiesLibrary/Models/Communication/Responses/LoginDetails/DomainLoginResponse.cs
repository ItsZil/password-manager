using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A response to a <see cref="DomainLoginRequest"/> containing the user's login credentials for the requested domain.
    /// </summary>
    internal class DomainLoginResponse
    {
        // The ID of the login details for the requested domain.
        [JsonInclude]
        [JsonPropertyName("loginDetailsId")]
        internal int LoginDetailsId { get; set; }

        // A flag to indicate if the user has permission to access the login details.
        [JsonInclude]
        [JsonPropertyName("needsExtraAuth")]
        internal bool NeedsExtraAuth { get; set; } = false;

        // The ID of the extra authentication method the user has set for the requested domain (if any). Defaults to 1 (none).
        [JsonInclude]
        [JsonPropertyName("extraAuthId")]
        internal int? ExtraAuthId { get; set; } = null;

        // A flag to indicate if the user has 2FA enabled for the requested domain.
        [JsonInclude]
        [JsonPropertyName("has2FA")]
        internal bool Has2FA { get; set; } = false;

        // The user's username for the requested domain.
        [JsonInclude]
        [JsonPropertyName("username")]
        internal string? Username { get; set; } = null;

        // The user's encrypted password (using shared secret key) base64 encoded string for the requested domain.
        [JsonInclude]
        [JsonPropertyName("password")]
        internal string? Password { get; set; } = null;

        /// <summary>
        /// A constructor for when the user has login credentials for the requested domain and has successfully authenticated.
        /// </summary>
        /// <param name="loginDetailsId">The ID of the login details.</param>
        /// <param name="username">The user's username (email, phone number, etc.)</param>
        /// <param name="password">The user's encrypted password as a base64 encoded string</param>
        /// <param name="has2FA">A flag indicating if the user has 2FA enabled for the requested domain</param>
        [JsonConstructor]
        internal DomainLoginResponse(int loginDetailsId, string username, string password, bool has2FA)
        {
            LoginDetailsId = loginDetailsId;
            Username = username;
            Password = password;
            Has2FA = has2FA;
        }

        internal DomainLoginResponse(int loginDetailsId, bool needsExtraAuth, int extraAuthId)
        {
            LoginDetailsId = loginDetailsId;
            NeedsExtraAuth = needsExtraAuth;
            ExtraAuthId = extraAuthId;
        }
    }
}
