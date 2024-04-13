using Microsoft.AspNetCore.Mvc;
using Server.Utilities;
using UtilitiesLibrary.Models;

namespace Server.Endpoints
{
    internal static class ConfigurationEndpoints
    {
        internal static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/generatepassphrase", GeneratePassPhrase);
            group.MapGet("/generatepragmakey", GeneratePragmaKey);
            group.MapPost("/isabsolutepathvalid", IsAbsolutePathValid);
            group.MapPost("/setupvault", SetupVault);

            return group;
        }

        internal static IResult GeneratePassPhrase([FromBody] PassphraseRequest passphraseRequest, KeyProvider keyProvider)
        {
            int wordCount = passphraseRequest.WordCount;

            byte[] passphrasePlain = PasswordUtil.GeneratePassphrase(wordCount);
            byte[] passphraseEncrypted = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(1), passphrasePlain);

            return Results.Ok(new PassphraseResponse { PassphraseBase64 = Convert.ToBase64String(passphraseEncrypted) });
        }

        internal static IResult GeneratePragmaKey(KeyProvider keyProvider)
        {
            byte[] plainPragmaKey = PasswordUtil.GenerateSecurePassword();
            byte[] pragmaKeyShared = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(1), plainPragmaKey);

            return Results.Ok(new PragmaKeyResponse { KeyBase64 = Convert.ToBase64String(pragmaKeyShared) });
        }

        internal static IResult IsAbsolutePathValid([FromBody] PathCheckRequest pathRequest)
        {
            string path = Uri.UnescapeDataString(pathRequest.AbsolutePathUri);
            string normalizedPath = Path.GetFullPath(path);

            bool isValid = Directory.Exists(normalizedPath);

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
            await sqlContext.UpdateDatabaseConnection(dbPath, plainPragmaKey);

            return Results.Ok();
        }
    }
}
