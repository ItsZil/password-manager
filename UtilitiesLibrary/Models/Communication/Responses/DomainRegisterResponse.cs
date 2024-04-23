using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A response to a <see cref="DomainRegisterRequest"/> containing the user's login credentials for the requested domain.
    /// </summary>
    internal class DomainRegisterResponse
    {
        [JsonInclude]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // The user's username for the requested domain.
        [JsonInclude]
        [JsonPropertyName("username")]
        internal string Username { get; set; }

        // The newly generated user's encrypted password byte array for the requested domain.
        [JsonInclude]
        [JsonPropertyName("password")]
        internal byte[] Password { get; set; }

        /// <summary>
        /// A constructor for when the user's login details have been successfully generated for the requested domain.
        /// </summary>
        /// <param name="username">The user's username (email, phone number, etc.)</param>
        /// <param name="password">The user's password</param>
        [JsonConstructor]
        internal DomainRegisterResponse(int id, string username, byte[] password)
        {
            Id = id;
            Username = username;
            Password = password;
        }
    }
}
