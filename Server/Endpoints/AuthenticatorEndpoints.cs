using UtilitiesLibrary.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;
using System.Text;
using OtpNet;

namespace Server.Endpoints
{
    internal static class AuthenticatorEndpoints
    {
        internal static RouteGroupBuilder MapAuthenticatorEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/authenticator", GetAuthenticatorCode);
            group.MapPost("/authenticator", CreateAuthenticator);
            group.MapDelete("/authenticator", DeleteAuthenticator);

            return group;
        }

        [Authorize]
        internal static async Task<IResult> GetAuthenticatorCode([FromQuery] int sourceId, [FromQuery] int loginDetailsId, [FromQuery] string timestamp, SqlContext sqlContext, KeyProvider keyProvider)
        {
            // Check if an authenticator exists for the login details.
            var authenticator = await sqlContext.Authenticators.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
            if (authenticator == null)
                return Results.NotFound();

            // Parse the timestamp into a DateTime object.
            DateTime.TryParse(timestamp, out DateTime timestampTime);
            if (timestampTime == default)
                return Results.BadRequest();

            // Decrypt the secret key and retrieve the TOTP code.
            byte[] secretKeyDecrypted = await PasswordUtil.DecryptPassword(authenticator.Secret, authenticator.Salt, keyProvider.GetVaultPragmaKeyBytes());

            Totp totp = new(authenticator.Secret);
            string code = totp.ComputeTotp(timestampTime);

            return Results.Ok(new AuthenticatorCodeResponse { Code = code });
        }

        [Authorize]
        internal static async Task<IResult> CreateAuthenticator([FromBody] CreateAuthenticatorRequest request, SqlContext sqlContext, KeyProvider keyProvider)
        {
            // Check if the login details exist.
            var loginDetails = await sqlContext.LoginDetails.FirstOrDefaultAsync(x => x.Id == request.LoginDetailsId);
            if (loginDetails == null)
                return Results.NotFound();

            // Check if an authenticator already exists for the login details.
            var authenticator = await sqlContext.Authenticators.FirstOrDefaultAsync(x => x.LoginDetailsId == request.LoginDetailsId);
            if (authenticator != null)
                return Results.Conflict();

            if (request.SecretKey == null || request.Timestamp == null)
                return Results.BadRequest();

            // Parse the timestamp into a DateTime object.
            DateTime.TryParse(request.Timestamp, out DateTime timestamp);
            if (timestamp == default)
                return Results.BadRequest();

            // Decrypt the secret key into a base32 string.
            byte[] secretKeyEncrypted = Convert.FromBase64String(request.SecretKey);
            byte[] secretKeyDecrypted = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), secretKeyEncrypted);
            string secretKey = Encoding.UTF8.GetString(secretKeyDecrypted);

            // Create an authenticator and retrieve the first code.
            Totp totp = new(secretKeyDecrypted);
            string code = totp.ComputeTotp(timestamp);

            // Encrypt the secret key and store it in the database.
            (byte[], byte[]) encryptedSecretKeyAndSalt = await PasswordUtil.EncryptPassword(secretKeyDecrypted, keyProvider.GetVaultPragmaKeyBytes());

            Authenticator authenticatorDb = new()
            {
                LoginDetailsId = request.LoginDetailsId,
                Secret = encryptedSecretKeyAndSalt.Item1,
                Salt = encryptedSecretKeyAndSalt.Item2
            };
            await sqlContext.Authenticators.AddAsync(authenticatorDb);
            await sqlContext.SaveChangesAsync();

            AuthenticatorCodeResponse response = new()
            {
                Code = code
            };
            return Results.Ok(response);
        }

        [Authorize]
        internal static async Task<IResult> DeleteAuthenticator([FromQuery] int loginDetailsId, SqlContext sqlContext)
        {
            var authenticator = await sqlContext.Authenticators.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
            if (authenticator == null)
                return Results.NotFound();

            sqlContext.Authenticators.Remove(authenticator);
            await sqlContext.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}
