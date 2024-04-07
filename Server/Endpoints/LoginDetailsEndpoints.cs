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
            string domain = request.Domain;
            string username = request.Username;

            // If no password is provided, generate a random one.
            byte[] password = request.Password ?? PasswordUtil.GenerateSecurePassword();
            bool passwordIsEncrypted = request.Password != null;

            // Check if domain & username are valid
            if (domain.Length < 3 || domain.Length > 255 || username.Length < 1)
                return Results.BadRequest();

            // Check if there already are login details with the same username for this domain.
            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain && x.Username == username);
            if (detailsExist)
                return Results.Conflict();

            // TODO: password meets user rule requirements

            // Decrypt password with shared secret
            byte[] decryptedPasswordPlain = passwordIsEncrypted ? PasswordUtil.DecryptPassword(keyProvider.GetSharedSecret(), password) : password;

            // Encrypt password with long-term encryption key
            byte[] encryptedPassword = PasswordUtil.EncryptPassword(dbContext.GetEncryptionKey(), decryptedPasswordPlain);

            // Save it to vault
            await dbContext.LoginDetails.AddAsync(new LoginDetails
            {
                RootDomain = domain,
                Username = username,
                Password = encryptedPassword
            });
            await dbContext.SaveChangesAsync();

            // Encrypt password with shared secret and send it back to the client
            byte[] encryptedPasswordShared = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(), decryptedPasswordPlain);
            DomainRegisterResponse response = new(domain, encryptedPasswordShared);

            return Results.Ok(response);
        }

        internal async static Task<IResult> DomainLoginRequest([FromBody] DomainLoginRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            // If the username is null, respond with first found login details for the domain
            string domain = request.Domain;
            string username = request.Username ?? string.Empty;
            bool lookupByUsername = !string.IsNullOrEmpty(username);

            // TODO: auth

            var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == domain);
            if (!detailsExist)
                return Results.NotFound();

            // TODO: per-detail auth

            LoginDetails? loginDetails = lookupByUsername ?
                await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.RootDomain == domain && x.Username == username) :
                await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.RootDomain == domain);

            if (loginDetails == null)
                return Results.NotFound();

            byte[] decryptedPasswordPlain = PasswordUtil.DecryptPassword(dbContext.GetEncryptionKey(), loginDetails.Password);
            byte[] encryptedPasswordShared = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(), decryptedPasswordPlain);

            DomainLoginResponse response = new(loginDetails.Username, encryptedPasswordShared, false); // TODO: check for 2FA

            return Results.Ok(response);
        }
    }
}
