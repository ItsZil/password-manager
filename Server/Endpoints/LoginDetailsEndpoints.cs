using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;
using UtilitiesLibrary.Models;

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

        internal async static Task<IResult> DomainRegisterRequest([FromBody] DomainRegisterRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            var domain = request.Domain;
            var username = request.Username;

            if (domain.Length < 3 || domain.Length > 255 || username.Length < 1)
                return Results.BadRequest();

            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain);
            if (detailsExist)
                return Results.Conflict();

            // TODO: password meets user rule requirements

            byte[] encryptedPassword = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(), request.Password);

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

        internal async static Task<IResult> DomainLoginRequest([FromBody] DomainLoginRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            var domain = request.Domain;

            // TODO: auth

            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain);
            if (!detailsExist)
                return Results.NotFound();

            // TODO: per-detail auth

            LoginDetails loginDetails = await dbContext.LoginDetails.FirstAsync(x => x.RootDomain == domain);

            string decryptedPassword = PasswordUtil.DecryptPassword(keyProvider.GetSharedSecret(), loginDetails.Password);

            DomainLoginResponse response = new(loginDetails.Username, decryptedPassword, false); // TODO: check for 2FA

            return Results.Ok(response);
        }
    }
}
