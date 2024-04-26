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

            group.MapGet("/authenticatorcount", AuthenticatorsCount);
            group.MapGet("/authenticatorview", GetAuthenticatorsView);
            group.MapGet("/authenticatorbydomain", GetAuthenticatorCodeByDomain);

            return group;
        }

        [Authorize]
        internal static async Task<IResult> GetAuthenticatorCode([FromQuery] int id, [FromQuery] string timestamp, SqlContext sqlContext, KeyProvider keyProvider)
        {
            // Check if an authenticator exists for the login details.
            var authenticator = await sqlContext.Authenticators.FirstOrDefaultAsync(x => x.AuthenticatorId == id);
            if (authenticator == null)
                return Results.NotFound();

            // Parse the timestamp into a DateTime object.
            timestamp = Uri.UnescapeDataString(timestamp);
            DateTime.TryParse(timestamp, out DateTime timestampTime);
            if (timestampTime == default)
                return Results.BadRequest();

            // Convert the timestamp to UTC time.
            DateTime timestampUtcTime = timestampTime.ToUniversalTime();

            // Decrypt the secret key and retrieve the TOTP code.
            byte[] secretKeyDecrypted = await PasswordUtil.DecryptPassword(authenticator.Secret, authenticator.Salt, keyProvider.GetVaultPragmaKeyBytes());
            string secretKeyDecryptedUTF8 = Encoding.UTF8.GetString(secretKeyDecrypted);
            byte[] secretKey = Base32Encoding.ToBytes(secretKeyDecryptedUTF8);

            TimeCorrection timeCorrection = new(DateTime.UtcNow);
            Totp totp = new(secretKey, timeCorrection: timeCorrection);

            string code = totp.ComputeTotp(timestampUtcTime);

            // Update the last accessed date.
            authenticator.LastUsedDate = timestampTime;
            await sqlContext.SaveChangesAsync();

            return Results.Ok(new AuthenticatorCodeResponse { Code = code });
        }

        [Authorize]
        internal static async Task<IResult> GetAuthenticatorCodeByDomain([FromQuery] string domain, [FromQuery] string timestamp, SqlContext sqlContext, KeyProvider keyProvider)
        {
            // Find the login details for the domain.
            var loginDetails = await sqlContext.LoginDetails.AsNoTracking().FirstOrDefaultAsync(x => x.RootDomain == domain);
            if (loginDetails == null)
                return Results.NotFound();

            // Check if an authenticator exists for the login details.
            var authenticator = await sqlContext.Authenticators.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetails.Id);
            if (authenticator == null)
                return Results.NotFound();

            // Parse the timestamp into a DateTime object.
            timestamp = Uri.UnescapeDataString(timestamp);
            DateTime.TryParse(timestamp, out DateTime timestampTime);
            if (timestampTime == default)
                return Results.BadRequest();

            // Convert the timestamp to UTC time.
            DateTime timestampUtcTime = timestampTime.ToUniversalTime();

            // Decrypt the secret key and retrieve the TOTP code.
            byte[] secretKeyDecrypted = await PasswordUtil.DecryptPassword(authenticator.Secret, authenticator.Salt, keyProvider.GetVaultPragmaKeyBytes());
            string secretKeyDecryptedUTF8 = Encoding.UTF8.GetString(secretKeyDecrypted);
            byte[] secretKey = Base32Encoding.ToBytes(secretKeyDecryptedUTF8);

            TimeCorrection timeCorrection = new(DateTime.UtcNow);
            Totp totp = new(secretKey, timeCorrection: timeCorrection);

            string code = totp.ComputeTotp(timestampUtcTime);

            // Update the last accessed date.
            authenticator.LastUsedDate = timestampTime;
            await sqlContext.SaveChangesAsync();

            return Results.Ok(new AuthenticatorCodeResponse { Code = code });
        }

        [Authorize]
        internal static async Task<IResult> CreateAuthenticator([FromBody] CreateAuthenticatorRequest request, SqlContext sqlContext, KeyProvider keyProvider)
        {
            // Check if the login details exist.
            var loginDetails = await sqlContext.LoginDetails.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.LoginDetailsId);
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
                AuthenticatorId = authenticatorDb.AuthenticatorId,
                Code = code
            };
            return Results.Ok(response);
        }

        [Authorize]
        internal static async Task<IResult> DeleteAuthenticator([FromQuery] int id, SqlContext sqlContext)
        {
            var authenticator = await sqlContext.Authenticators.FirstOrDefaultAsync(x => x.AuthenticatorId == id);
            if (authenticator == null)
                return Results.NotFound();

            sqlContext.Authenticators.Remove(authenticator);
            await sqlContext.SaveChangesAsync();

            return Results.NoContent();
        }

        [Authorize]
        internal async static Task<IResult>AuthenticatorsCount(SqlContext dbContext)
        {
            return Results.Ok(await dbContext.Authenticators.CountAsync());
        }

        [Authorize]
        internal async static Task<IResult> GetAuthenticatorsView(SqlContext dbContext, [FromQuery] int page = 1)
        {
            int pageSize = 10;
            int skip = (page - 1) * pageSize;

            var authenticators = await dbContext.Authenticators.Include(x => x.LoginDetails).Skip(skip).Take(pageSize).ToListAsync();

            List<AuthenticatorsViewResponse> results = new List<AuthenticatorsViewResponse>();
            foreach (var authenticator in authenticators)
            {
                results.Add(new AuthenticatorsViewResponse
                {
                    AuthenticatorId = authenticator.AuthenticatorId,
                    Domain = authenticator.LoginDetails.RootDomain,
                    Username = authenticator.LoginDetails.Username,
                    LastUsedDate = authenticator.LastUsedDate
                });
            }
            return Results.Ok(results);
        }
    }
}
