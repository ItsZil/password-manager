using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            group.MapPost("/refreshtoken", RefreshToken);
            group.MapGet("/lockvault", LockVault);

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
            byte[] passphraseEncrypted = await PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(1), passphrasePlain);

            return Results.Ok(new PassphraseResponse { PassphraseBase64 = Convert.ToBase64String(passphraseEncrypted) });
        }

        internal async static Task<IResult> GeneratePassword(KeyProvider keyProvider)
        {
            byte[] plainPassword = PasswordUtil.GenerateSecurePassword();
            byte[] passwordEncrypted = await PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(1), plainPassword);

            return Results.Ok(new GeneratedPasswordResponse { PasswordBase64 = Convert.ToBase64String(passwordEncrypted) });
        }

        [Authorize]
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
            byte[] plainPragmaKey = await PasswordUtil.DecryptPassword(keyProvider.GetSharedSecret(1), encryptedPragmaKey);

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
            return Results.Ok();
        }

        internal async static Task<IResult> UnlockVault([FromBody] UnlockVaultRequest unlockRequest, SqlContext sqlContext, KeyProvider keyProvider)
        {
            string passphraseEncrypted = unlockRequest.PassphraseBase64;
            if (passphraseEncrypted == null)
            {
                return Results.BadRequest("Passphrase is empty.");
            }

            //byte[] passphrasePlain = await PasswordUtil.DecryptPassword(keyProvider.GetSharedSecret(2), Convert.FromBase64String(passphraseEncrypted));
            string passphraseString = "unison spill grading garment";//Encoding.UTF8.GetString(passphrasePlain);

            // Attempt to unlock the vault
            bool successfullyOpened = await sqlContext.UpdateDatabaseConnection(ConfigUtil.GetVaultLocation(), passphraseString);
            if (!successfullyOpened)
            {
                return Results.Forbid();
            }

            // Generate a JWT token
            var jwtToken = AuthUtil.GenerateJwtToken(ConfigUtil.GetJwtSecretKey());
            var refreshToken = AuthUtil.GenerateRefreshToken();

            await sqlContext.RefreshTokens.AddAsync(new RefreshToken { Token = refreshToken, ExpiryDate = DateTime.Now.AddDays(7) });
            await sqlContext.SaveChangesAsync();

            return Results.Created(string.Empty, new { Token = jwtToken, RefreshToken = refreshToken });
        }

        internal static async Task<IResult> RefreshToken([FromBody] RefreshTokenRequest request, SqlContext sqlContext, KeyProvider keyProvider)
        {
            string oldRefreshToken = request.RefreshToken;

            if (!AuthUtil.ValidateRefreshToken(oldRefreshToken, sqlContext))
            {
                return Results.BadRequest("Invalid refresh token.");
            }

            if (!keyProvider.HasVaultPragmaKey())
            {
                return Results.BadRequest("Vault is not unlocked.");
            }

            var jwtToken = AuthUtil.GenerateJwtToken(ConfigUtil.GetJwtSecretKey());
            var newRefreshToken = AuthUtil.GenerateRefreshToken();

            await AuthUtil.UpdateRefreshToken(oldRefreshToken, newRefreshToken, sqlContext);

            return Results.Created(string.Empty, new { Token = jwtToken, RefreshToken = newRefreshToken });
        }

        [Authorize]
        internal static async Task<IResult> LockVault(SqlContext sqlContext)
        {
            var refreshToken = sqlContext.RefreshTokens.FirstOrDefault(rt => rt.ExpiryDate > DateTime.UtcNow);
            if (refreshToken != null)
            {
                refreshToken.ExpiryDate = DateTime.UtcNow;
            }
            await sqlContext.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}
