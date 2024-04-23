using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;
using System.Reflection;
using System.Text;
using UtilitiesLibrary.Models;

namespace Server.Endpoints
{
    internal static class ConfigurationEndpoints
    {
        internal static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/hasexistingvault", HasExistingVault);
            group.MapPost("/generatepassphrase", GeneratePassPhrase);
            group.MapGet("/generatepassword", GeneratePassword);
            group.MapPost("/isabsolutepathvalid", IsAbsolutePathValid);

            group.MapPost("/setupvault", SetupVault);
            group.MapPost("/unlockvault", UnlockVault);
            group.MapPost("/updatevaultpassphrase", UpdateVaultPassphrase);

            group.MapPost("/refreshtoken", RefreshToken);
            group.MapPost("/lockvault", LockVault);
            group.MapGet("/checkauth", CheckAuth);

            group.MapPost("/exportvault", ExportBackupVault);

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

        internal async static Task<IResult> GeneratePassPhrase([FromBody] PassphraseRequest passphraseRequest, KeyProvider keyProvider)
        {
            int wordCount = passphraseRequest.WordCount;

            if (wordCount < 4 || wordCount > 10)
            {
                return Results.BadRequest("Word count must be between 4 and 10.");
            }

            byte[] passphrasePlain = PasswordUtil.GeneratePassphrase(wordCount);
            byte[] passphraseEncrypted = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(passphraseRequest.SourceId), passphrasePlain);

            return Results.Ok(new PassphraseResponse { PassphraseBase64 = Convert.ToBase64String(passphraseEncrypted) });
        }

        internal async static Task<IResult> GeneratePassword(KeyProvider keyProvider, [FromQuery] int sourceId = 1)
        {
            byte[] plainPassword = PasswordUtil.GenerateSecurePassword(32);
            byte[] passwordEncrypted = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(sourceId), plainPassword);

            return Results.Ok(new GeneratedPasswordResponse { PasswordBase64 = Convert.ToBase64String(passwordEncrypted) });
        }

        internal static IResult IsAbsolutePathValid([FromBody] PathCheckRequest pathRequest)
        {
            string path = Uri.UnescapeDataString(pathRequest.AbsolutePathUri);
            string normalizedPath = Path.GetFullPath(path);

            bool isValid = Directory.Exists(normalizedPath);
            if (path.EndsWith(".db"))
                isValid = File.Exists(normalizedPath);

            return Results.Ok(new PathCheckResponse { PathValid = isValid });
        }

        internal async static Task<IResult> SetupVault([FromBody] SetupVaultRequest setupRequest, SqlContext sqlContext, KeyProvider keyProvider)
        {
            string dbPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // Use My Documents as default location
            if (setupRequest.AbsolutePathUri != null)
            {
                dbPath = Path.GetFullPath(Uri.UnescapeDataString(setupRequest.AbsolutePathUri));
            }

            byte[] encryptedPragmaKey = Convert.FromBase64String(setupRequest.VaultRawKeyBase64);
            byte[] plainPragmaKey = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(setupRequest.SourceId), encryptedPragmaKey);

            if (string.IsNullOrWhiteSpace(dbPath) || plainPragmaKey.Length == 0)
            {
                return Results.BadRequest("Database path or vault password is empty.");
            }

            // Update the database connection with the new path and pragma key
            string pragmaKeyString = Encoding.UTF8.GetString(plainPragmaKey);
            bool successfullyOpened = await sqlContext.UpdateDatabaseConnection(dbPath, pragmaKeyString);

            if (!successfullyOpened)
            {
                return Results.BadRequest("Failed to open vault connection.");
            }

            string accessToken = AuthUtil.GenerateJwtToken(ConfigUtil.GetJwtSecretKey());
            string refreshToken = await AuthUtil.GenerateRefreshToken(sqlContext);

            return Results.Created(string.Empty, new TokenResponse { AccessToken = accessToken, RefreshToken = refreshToken });
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

        [Authorize]
        internal async static Task<IResult> ExportBackupVault(PathCheckRequest request, SqlContext dbContext)
        {
            string absolutePath = Uri.UnescapeDataString(request.AbsolutePathUri);
            string normalizedPath = Path.GetFullPath(absolutePath);
            if (!Directory.Exists(normalizedPath))
                return Results.BadRequest();

            // Get the current date & time in a format that can be used in a file name
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string backupFileName = $"backupvault_{currentDate}.db";
            string backupPath = Path.Join(normalizedPath, backupFileName);

            // Close the existing database connection and make a backup of the vault file
            await dbContext.Database.CloseConnectionAsync();
            File.Copy(ConfigUtil.GetVaultLocation(), backupPath, true);

            // Check if the backup was successful
            if (!File.Exists(backupPath))
                return Results.BadRequest();

            return Results.Ok(new ExportVaultResponse { AbsolutePathUri = Uri.EscapeDataString(backupPath) });
        }
    }
}
