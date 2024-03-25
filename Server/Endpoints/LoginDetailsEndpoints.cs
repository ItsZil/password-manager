using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        internal async static Task<IResult> DomainLoginRequest([FromBody] DomainLoginRequest request, SqlContext dbContext)
        {
            var domain = request.Domain;

            // TODO: auth

            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain);
            if (!detailsExist)
                return Results.NotFound();

            // TODO: per-detail auth

            LoginDetails loginDetails = await dbContext.LoginDetails.FirstAsync(x => x.RootDomain == domain);

            DomainLoginResponse response = new(loginDetails.Username, loginDetails.Password, false); // TODO: check for 2FA

            return Results.Ok(response);
        }
    }
}
