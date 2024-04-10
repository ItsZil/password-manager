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

            return group;
        }

        internal static IResult GeneratePassPhrase([FromBody] PassphraseRequest passphraseRequest, KeyProvider keyProvider)
        {
            int wordCount = passphraseRequest.WordCount;

            byte[] passphrasePlain = PasswordUtil.GeneratePassphrase(wordCount);
            byte[] passphraseEncrypted = PasswordUtil.EncryptPassword(keyProvider.GetSharedSecret(), passphrasePlain);

            return Results.Ok(new PassphraseResponse { PassphraseBase64 = Convert.ToBase64String(passphraseEncrypted) });
        }
    }
}
