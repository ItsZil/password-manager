using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;
using System.Reflection;
using System.Text;
using UtilitiesLibrary.Models;

namespace Server.Endpoints
{
    internal static class AuthenticationEndpoints
    {
        internal static RouteGroupBuilder MapAuthenticationEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/unlockvault", UnlockVault);
            group.MapPost("/updatevaultpassphrase", UpdateVaultPassphrase);

            group.MapPost("/refreshtoken", RefreshToken);
            group.MapPost("/lockvault", LockVault);
            group.MapGet("/checkauth", CheckAuth);

            return group;
        }

        internal static IResult HasExistingVault()
        {
            string currentAssembly = Assembly.GetExecutingAssembly().Location;
            string currentDirectory = Path.GetDirectoryName(currentAssembly) ?? string.Empty;
            string configPath = Path.Join(currentDirectory, "config.json");

            if (File.Exists(configPath))
            {
                string vaultLocation = ConfigUtil.GetVaultLocation();
                if (File.Exists(vaultLocation) && !vaultLocation.Contains("initialvault.db"))
                {
                    return Results.Ok(true);
                }
            }
            return Results.Ok(false);
        }

        internal async static Task<IResult> UnlockVault([FromBody] UnlockVaultRequest unlockRequest, SqlContext sqlContext, KeyProvider keyProvider)
        {
            string passphraseEncrypted = unlockRequest.PassphraseBase64;
            if (passphraseEncrypted == null)
            {
                return Results.BadRequest("Passphrase is empty.");
            }

            byte[] passphrasePlain = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(unlockRequest.SourceId), Convert.FromBase64String(passphraseEncrypted));
            string passphraseString = Encoding.UTF8.GetString(passphrasePlain);

            // Attempt to unlock the vault
            bool successfullyOpened = await sqlContext.UpdateDatabaseConnection(ConfigUtil.GetVaultLocation(), passphraseString);
            if (!successfullyOpened)
            {
                return Results.Forbid();
            }

            // Generate a JWT token
            var jwtToken = AuthUtil.GenerateJwtToken(ConfigUtil.GetJwtSecretKey());
            var refreshToken = await AuthUtil.GenerateRefreshToken(sqlContext);

            return Results.Created(string.Empty, new TokenResponse { AccessToken = jwtToken, RefreshToken = refreshToken });
        }

        [Authorize]
        internal async static Task<IResult> UpdateVaultPassphrase([FromBody] UpdateVaultPassphraseRequest updateRequest, SqlContext sqlContext, KeyProvider keyProvider)
        {
            string newPassphraseEncrypted = updateRequest.VaultRawKeyBase64;
            if (newPassphraseEncrypted == null)
            {
                return Results.BadRequest();
            }

            byte[] newPassphrasePlain = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(updateRequest.SourceId), Convert.FromBase64String(newPassphraseEncrypted));
            string newPassphraseString = Encoding.UTF8.GetString(newPassphrasePlain);

            // Checck if the new passphrase is between 4 and 10 space separated words
            string[] words = newPassphraseString.Split(' ');
            if (words.Length < 4 || words.Length > 10)
            {
                return Results.BadRequest();
            }

            // Update the vault passphrase
            bool updated = await sqlContext.UpdateDatabasePragmaKey(newPassphraseString);
            if (!updated)
            {
                return Results.BadRequest("Failed to update vault passphrase.");
            }

            return Results.NoContent();
        }

        internal static async Task<IResult> RefreshToken([FromBody] RefreshTokenRequest request, SqlContext sqlContext, KeyProvider keyProvider)
        {
            if (!keyProvider.HasVaultPragmaKey())
            {
                return Results.BadRequest("Vault is not unlocked.");
            }

            string oldRefreshToken = request.RefreshToken;

            if (!AuthUtil.ValidateRefreshToken(oldRefreshToken, sqlContext))
            {
                return Results.BadRequest("Invalid refresh token.");
            }

            var jwtToken = AuthUtil.GenerateJwtToken(ConfigUtil.GetJwtSecretKey());
            var newRefreshToken = await AuthUtil.GenerateRefreshToken(sqlContext);

            await AuthUtil.UpdateRefreshToken(oldRefreshToken, newRefreshToken, sqlContext);

            return Results.Created(string.Empty, new TokenResponse { AccessToken = jwtToken, RefreshToken = newRefreshToken });
        }

        [Authorize]
        internal static async Task<IResult> LockVault(SqlContext sqlContext, KeyProvider keyProvider)
        {
            var validRefreshTokens = sqlContext.RefreshTokens.Where(rt => rt.ExpiryDate > DateTime.UtcNow);
            if (validRefreshTokens.Count() > 0)
            {
                // Invalidate all refresh tokens.
                await validRefreshTokens.ForEachAsync(rt => rt.ExpiryDate = DateTime.UtcNow);
                await sqlContext.SaveChangesAsync();
            }

            // Reset the JWT secret key
            ConfigUtil.ResetJwtSecretKey();

            // Disconnect from the vault
            await sqlContext.Database.CloseConnectionAsync();

            // Clear the pragma key from memory
            keyProvider.ClearPragmaKey();

            return Results.NoContent();
        }

        [Authorize]
        internal static IResult CheckAuth(KeyProvider keyProvider)
        {
            if (keyProvider.HasVaultPragmaKey())
            {
                return Results.Ok();
            }
            return Results.Forbid();
        }
    }
}
