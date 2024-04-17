using Microsoft.AspNetCore.Authorization;
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

        [Authorize]
        internal async static Task<IResult> DomainRegisterRequest([FromBody] DomainRegisterRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            string domain = request.Domain;
            string username = request.Username;

            // Check if domain & username are valid
            if (domain.Length < 3 || domain.Length > 255 || username.Length < 1)
                return Results.BadRequest();

            // Check if there already are login details with the same username for this domain.
            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain && x.Username == username);
            if (detailsExist)
                return Results.Conflict();

            // If no password is provided, generate a random one.
            byte[] password = request.Password != null ? Convert.FromBase64String(request.Password) : PasswordUtil.GenerateSecurePassword();
            bool passwordIsEncrypted = request.Password != null;

            // TODO: password meets user rule requirements

            // Decrypt password with shared secret
            byte[] decryptedPasswordPlain = passwordIsEncrypted ? await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), password) : password;
            string decryptedPasswordPlainString = System.Text.Encoding.UTF8.GetString(decryptedPasswordPlain);

            // Encrypt password with long-term encryption key
            (byte[] encryptedPassword, byte[] salt) = await PasswordUtil.EncryptPassword(decryptedPasswordPlain);

            // Save it to vault
            await dbContext.LoginDetails.AddAsync(new LoginDetails
            {
                RootDomain = domain,
                Username = username,
                Password = encryptedPassword,
                Salt = salt
            });
            await dbContext.SaveChangesAsync();

            // Encrypt password with shared secret and send it back to the client
            byte[] encryptedPasswordShared = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(request.SourceId), decryptedPasswordPlain);
            DomainRegisterResponse response = new(domain, encryptedPasswordShared);

            return Results.Ok(response);
        }

        [Authorize]
        internal async static Task<IResult> DomainLoginRequest([FromBody] DomainLoginRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            // If the username is null, respond with first found login details for the domain
            string domain = request.Domain;
            string username = request.Username ?? string.Empty;
            bool lookupByUsername = !string.IsNullOrEmpty(username);

            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain);
            if (!detailsExist)
                return Results.NotFound();

            // TODO: per-detail auth

            LoginDetails? loginDetails = lookupByUsername ?
                await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.RootDomain == domain && x.Username == username) :
                await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.RootDomain == domain);

            if (loginDetails == null)
                return Results.NotFound();

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(loginDetails.Password, loginDetails.Salt);
            string decryptedPasswordPlainString = System.Text.Encoding.UTF8.GetString(decryptedPasswordPlain);

            byte[] encryptedPasswordShared = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(request.SourceId), decryptedPasswordPlain);

            DomainLoginResponse response = new(loginDetails.Username, Convert.ToBase64String(encryptedPasswordShared), false);
            return Results.Ok(response);
        }
    }
}
