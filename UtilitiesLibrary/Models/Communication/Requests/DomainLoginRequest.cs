﻿namespace UtilitiesLibrary.Models
{
    /// <summary>
    /// A request to fetch, if any, the user login credentials for the current domain.
    /// Expected response is a <see cref="DomainLoginResponse"/>.
    /// </summary>
    internal class DomainLoginRequest
    {
        // The domain (website.com) for which the user is requesting login credentials.
        internal string Domain { get; set; }

        // The user's browser's user agent.
        internal string UserAgent { get; set; }
    }
}
