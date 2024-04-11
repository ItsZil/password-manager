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
            group.MapPost("/isabsolutepathvalid", IsAbsolutePathValid);

            return group;
        }

        internal static IResult GeneratePassPhrase([FromBody] PassphraseRequest passphraseRequest, KeyProvider keyProvider)
        {
            int wordCount = passphraseRequest.WordCount;

            byte[] passphrasePlain = PasswordUtil.GeneratePassphrase(wordCount);
            byte[] passphraseEncrypted = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(), passphrasePlain);

            return Results.Ok(new PassphraseResponse { PassphraseBase64 = Convert.ToBase64String(passphraseEncrypted) });
        }

        internal static IResult IsAbsolutePathValid([FromBody] PathCheckRequest pathRequest)
        {
            string path = Uri.UnescapeDataString(pathRequest.AbsolutePathUri);
            string normalizedPath = Path.GetFullPath(path);

            bool isValid = Directory.Exists(normalizedPath);

            return Results.Ok(new PathCheckResponse { PathValid = isValid });
        }
    }
}
