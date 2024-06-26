﻿using System.Text.Json.Serialization;

namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A response to request to get a list of login details for all domains.
    /// </summary>
    internal class LoginDetailsViewResponse
    {
        // The ID of the login details.
        [JsonInclude]
        internal int DetailsId { get; set; }

        // The domain (website.com)
        [JsonInclude]
        internal string Domain { get; set; }

        // The user's username
        [JsonInclude]
        internal string Username { get; set; }

        // The date when the login details were last used.
        [JsonInclude]
        public DateTime LastUsedDate { get; set; }

        // The extra auth type ID.
        [JsonInclude]
        public int ExtraAuthId { get; set; }
    }
}
