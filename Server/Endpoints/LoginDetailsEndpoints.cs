using Microsoft.AspNetCore.Mvc;
using UtilitiesLibrary.Models;

namespace Server.Endpoints
{
    internal static class LoginDetailsEndpoints
    {
        internal static RouteGroupBuilder MapLoginDetailsEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/domainloginrequest", DomainLoginRequest);

            return group;
        }

        internal static IResult DomainLoginRequest([FromBody] DomainLoginRequest request)
        {
            string requestedDomain = request.Domain;
            DomainLoginResponse response = new($"testuser{requestedDomain}", "password", false);

            return Results.Ok(response);
        }
    }
}
