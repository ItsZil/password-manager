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

            group.MapPost("/exportvault", ExportBackupVault);
            group.MapGet("/vaultinternetaccess", GetVaultInternetAccessSetting);
            group.MapPut("/vaultinternetaccess", SetVaultInternetAccessSetting);

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

        [Authorize]
        internal async static Task<IResult> ExportBackupVault(PathCheckRequest request, SqlContext dbContext)
        {
            string absolutePath = Uri.UnescapeDataString(request.AbsolutePathUri) ?? Environment.CurrentDirectory;
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

        [Authorize]
        internal static IResult GetVaultInternetAccessSetting()
        {
            bool setting = ConfigUtil.GetVaultInternetAccess();
            return Results.Ok(setting);
        }

        [Authorize]
        internal static IResult SetVaultInternetAccessSetting([FromQuery] bool? setting)
        {
            if (setting == null)
                return Results.BadRequest();

            ConfigUtil.SetVaultInternetAccess(setting ?? false);
            return Results.NoContent();
        }
    }
}
