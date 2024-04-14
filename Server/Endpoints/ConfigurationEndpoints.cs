using Microsoft.AspNetCore.Mvc;
using Server.Utilities;
using System.Text;
using UtilitiesLibrary.Models;

namespace Server.Endpoints
{
    internal static class ConfigurationEndpoints
    {
        internal static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/generatepassphrase", GeneratePassPhrase);
            group.MapGet("/generatepassword", GeneratePassword);
            group.MapPost("/isabsolutepathvalid", IsAbsolutePathValid);
            group.MapPost("/setupvault", SetupVault);

            return group;
        }

        internal static IResult GeneratePassPhrase([FromBody] PassphraseRequest passphraseRequest, KeyProvider keyProvider)
        {
            int wordCount = passphraseRequest.WordCount;

            if (wordCount < 4 || wordCount > 10)
            {
                return Results.BadRequest("Word count must be between 4 and 10.");
            }

            byte[] passphrasePlain = PasswordUtil.GeneratePassphrase(wordCount);
            byte[] passphraseEncrypted = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(1), passphrasePlain);

            return Results.Ok(new PassphraseResponse { PassphraseBase64 = Convert.ToBase64String(passphraseEncrypted) });
        }

        internal static IResult GeneratePassword(KeyProvider keyProvider)
        {
            byte[] plainPassword = PasswordUtil.GenerateSecurePassword();
            byte[] passwordEncrypted = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(1), plainPassword);

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
            byte[] plainPragmaKey = PasswordUtil.DecryptPassword(keyProvider.GetSharedSecret(1), encryptedPragmaKey);
            
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
    }
}
