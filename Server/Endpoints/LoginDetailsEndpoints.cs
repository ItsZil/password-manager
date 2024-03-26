using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UtilitiesLibrary.Models;
using UtilitiesLibrary.Utilities;

namespace Server.Endpoints
{
    internal static class LoginDetailsEndpoints
    {
        internal static RouteGroupBuilder MapLoginDetailsEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/domainregisterrequest", DomainRegisterRequest);
            group.MapPost("/domainloginrequest", DomainLoginRequest);

            return group;
        }

        internal async static Task<IResult> DomainRegisterRequest([FromBody] DomainRegisterRequest request, SqlContext dbContext)
        {
            var domain = request.Domain;
            var username = request.Username;

            if (domain.Length < 3 || domain.Length > 255 || username.Length < 1)
                return Results.BadRequest();

            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain);
            if (detailsExist)
                return Results.Conflict();

            // TODO: password meets user rule requirements

            byte[] encryptedPassword = await PasswordUtil.EncryptPassword(request.Password);

            await dbContext.LoginDetails.AddAsync(new LoginDetails
            {
                RootDomain = domain,
                Username = username,
                Password = encryptedPassword
            });
            await dbContext.SaveChangesAsync();

            DomainRegisterResponse response = new(domain, encryptedPassword);

            return Results.Ok(response);
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
