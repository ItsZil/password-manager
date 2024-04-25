using Geralt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;
using System.Text;
using UtilitiesLibrary.Models;

namespace Server.Endpoints
{
    internal static class LoginDetailsEndpoints
    {
        internal static RouteGroupBuilder MapLoginDetailsEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/register", DomainRegisterRequest);
            group.MapPost("/login", DomainLoginRequest);

            group.MapGet("/logindetails", GetLoginDetailsView);
            group.MapGet("/logindetailsall", GetAllLoginDetailsView);
            group.MapGet("/logindetailscount", LoginDetailsCount);
            group.MapPost("/logindetailspassword", GetLoginDetailsPassword);

            group.MapPut("/logindetails", EditLoginDetails);
            group.MapDelete("/logindetails", DeleteLoginDetails);

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

            // Decrypt password with shared secret
            byte[] decryptedPasswordPlain = passwordIsEncrypted ? await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), password) : password;
            string decryptedPasswordPlainString = System.Text.Encoding.UTF8.GetString(decryptedPasswordPlain);

            // Encrypt password with long-term encryption key
            (byte[] encryptedPassword, byte[] salt) = await PasswordUtil.EncryptPassword(decryptedPasswordPlain, keyProvider.GetVaultPragmaKeyBytes());

            // Save it to vault
            var newLoginDetails = new LoginDetails
            {
                RootDomain = domain,
                Username = username,
                Password = encryptedPassword,
                Salt = salt
            };
            await dbContext.LoginDetails.AddAsync(newLoginDetails);
            await dbContext.SaveChangesAsync();

            // Encrypt password with shared secret and send it back to the client
            byte[] encryptedPasswordShared = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(request.SourceId), decryptedPasswordPlain);
            DomainRegisterResponse response = new(newLoginDetails.Id, domain, encryptedPasswordShared);

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

            LoginDetails? loginDetails = lookupByUsername ?
                await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.RootDomain == domain && x.Username == username) :
                await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.RootDomain == domain);

            if (loginDetails == null)
                return Results.NotFound();

            bool needsExtraAuth = loginDetails.ExtraAuthId > 1;
            if (needsExtraAuth)
            {
                if (loginDetails.ExtraAuthId == 2 && request.PinCode != null)
                {
                    // We already have the PIN code.
                    bool parsedPinCode = int.TryParse(request.PinCode, out int requestPinCode);
                    if (parsedPinCode && requestPinCode > 0 && requestPinCode < 10000)
                    {
                        var pinCode = await dbContext.PinCodes.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetails.Id);
                        if (pinCode == null || pinCode.Code != requestPinCode)
                            return Results.Unauthorized();
                    }
                    else
                    {
                        // Incorrect PIN code provided.
                        return Results.Unauthorized();
                    }
                }
                else if (loginDetails.ExtraAuthId == 4 && request.Passphrase != null)
                {
                    // We have the passphrase, decrypt it & verify its hash matches the vault pragma key.
                    byte[] decryptedPassphrase = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), Convert.FromBase64String(request.Passphrase));
                    string sharedSecretB64 = Convert.ToBase64String(keyProvider.GetSharedSecret(request.SourceId));
                    string decryptedPassphraseString = Encoding.UTF8.GetString(decryptedPassphrase);

                    if (string.IsNullOrEmpty(decryptedPassphraseString))
                        return Results.Unauthorized();

                    byte[] hash = new byte[32];
                    BLAKE2b.ComputeHash(hash, decryptedPassphrase);
                    string hashBase64 = Convert.ToBase64String(hash);

                    bool passphraseIsCorrect = keyProvider.GetVaultPragmaKey() == hashBase64;
                    if (!passphraseIsCorrect)
                        return Results.Unauthorized();
                }
                else
                {
                    // The user has extra authentication enabled, return the extra auth ID.
                    return Results.Ok(new DomainLoginResponse(loginDetails.Id, needsExtraAuth, loginDetails.ExtraAuthId));
                }
            }

            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(loginDetails.Password, loginDetails.Salt, keyProvider.GetVaultPragmaKeyBytes());
            string decryptedPasswordPlainString = System.Text.Encoding.UTF8.GetString(decryptedPasswordPlain);
            byte[] encryptedPasswordShared = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(request.SourceId), decryptedPasswordPlain);
            
            bool hasAuthenticator = await dbContext.Authenticators.AnyAsync(x => x.LoginDetailsId == loginDetails.Id);

            DomainLoginResponse response = new(loginDetails.Id, loginDetails.Username, Convert.ToBase64String(encryptedPasswordShared), hasAuthenticator);
            return Results.Ok(response);
        }

        [Authorize]
        internal async static Task<IResult> LoginDetailsCount(SqlContext dbContext)
        {
            return Results.Ok(await dbContext.LoginDetails.CountAsync());
        }

        [Authorize]
        internal async static Task<IResult> GetLoginDetailsView(SqlContext dbContext, [FromQuery] int page = 1)
        {
            int pageSize = 10;
            int skip = (page - 1) * pageSize;

            var details = await dbContext.LoginDetails.Skip(skip).Take(pageSize).ToListAsync();
            
            List<LoginDetailsViewResponse> results = new List<LoginDetailsViewResponse>();
            foreach (var loginDetails in details)
            {
                results.Add(new LoginDetailsViewResponse
                {
                    DetailsId = loginDetails.Id,
                    Domain = loginDetails.RootDomain,
                    Username = loginDetails.Username,
                    LastUsedDate = loginDetails.LastUsedDate,
                    ExtraAuthId = loginDetails.ExtraAuthId
                });
            }
            return Results.Ok(results);
        }

        [Authorize]
        internal async static Task<IResult> GetAllLoginDetailsView(SqlContext dbContext)
        {
            var details = await dbContext.LoginDetails.ToListAsync();

            List<LoginDetailsViewResponse> results = new List<LoginDetailsViewResponse>();
            foreach (var loginDetails in details)
            {
                results.Add(new LoginDetailsViewResponse
                {
                    DetailsId = loginDetails.Id,
                    Domain = loginDetails.RootDomain,
                    Username = loginDetails.Username,
                    LastUsedDate = loginDetails.LastUsedDate,
                    ExtraAuthId = loginDetails.ExtraAuthId
                });
            }
            return Results.Ok(results);
        }

        [Authorize]
        internal async static Task<IResult> GetLoginDetailsPassword([FromBody] DomainLoginPasswordRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            // Check if login details exist
            var loginDetails = await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.Id == request.LoginDetailsId);
            if (loginDetails == null)
                return Results.NotFound();

            // Decrypt password with long-term encryption key
            byte[] decryptedPasswordPlain = await PasswordUtil.DecryptPassword(loginDetails.Password, loginDetails.Salt, keyProvider.GetVaultPragmaKeyBytes());
            string decryptedPasswordPlainString = System.Text.Encoding.UTF8.GetString(decryptedPasswordPlain);

            // Encrypt password with shared secret and send it back to the client
            string encryptedPasswordShared = Convert.ToBase64String(await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(request.SourceId), decryptedPasswordPlain));

            return Results.Ok(new { passwordB64 = encryptedPasswordShared } );
        }

        [Authorize]
        internal async static Task<IResult> EditLoginDetails([FromBody] LoginDetailsEditRequest request, SqlContext dbContext, KeyProvider keyProvider)
        {
            // Check if login details exist
            var loginDetails = await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.Id == request.LoginDetailsId);
            if (loginDetails == null)
                return Results.NotFound();

            // If the username is different, check if there is already a login details with the same new username for this domain.
            if (request.Username != null && request.Username != loginDetails.Username)
            {
                var detailsExist = await dbContext.LoginDetails.AnyAsync(x => x.RootDomain == loginDetails.RootDomain && x.Username == request.Username);
                if (detailsExist)
                    return Results.Conflict();
            }

            // If the password is null, do not change it.
            if (request.Password != null)
            {
                byte[] newEncryptedPassword = Convert.FromBase64String(request.Password);
                byte[] newDecryptedPasswordPlain = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), newEncryptedPassword);

                if (newDecryptedPasswordPlain.Length == 0)
                    return Results.BadRequest(); // Decryption failed

                (byte[] encryptedPassword, byte[] salt) = await PasswordUtil.EncryptPassword(newDecryptedPasswordPlain, keyProvider.GetVaultPragmaKeyBytes());
                loginDetails.Password = encryptedPassword;
                loginDetails.Salt = salt;
            }
            loginDetails.Username = request.Username ?? loginDetails.Username;
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        }

        [Authorize]
        internal async static Task<IResult> DeleteLoginDetails([FromQuery] int id, SqlContext dbContext)
        {
            var loginDetails = await dbContext.LoginDetails.FirstOrDefaultAsync(x => x.Id == id);
            if (loginDetails == null)
                return Results.NotFound();

            dbContext.LoginDetails.Remove(loginDetails);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}
