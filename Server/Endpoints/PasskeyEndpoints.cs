using UtilitiesLibrary.Models;
using Microsoft.AspNetCore.Mvc;
using Server.Utilities;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Server.Endpoints
{
    internal static class PasskeyEndpoints
    {
        internal static RouteGroupBuilder MapPasskeyEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/passkey", CreatePasskey);
            group.MapGet("/passkey", GetPasskeyCredentials);

            return group;
        }

        [Authorize]
        internal static async Task<IResult> CreatePasskey([FromBody] PasskeyCreationRequest request, KeyProvider keyProvider, SqlContext sqlContext)
        {
            byte[] credentialId = Base64UrlEncoder.DecodeBytes(request.CredentialIdB64);
            byte[] userId = Convert.FromBase64String(request.UserIdB64);
            byte[] publicKey = Convert.FromBase64String(request.PublicKeyB64);
            byte[] challenge = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), Convert.FromBase64String(request.ChallengeB64));
            int loginDetailsId = request.LoginDetailsId;

            if (credentialId.Length < 32 || publicKey.Length < 32 || userId.Length < 16 || challenge.Length < 16)
                return Results.BadRequest();

            if (await sqlContext.LoginDetails.FindAsync(loginDetailsId) == null)
                return Results.NotFound();

            Passkey newPasskey = new Passkey
            {
                CredentialId = credentialId,
                UserId = userId,
                PublicKey = publicKey,
                Challenge = challenge,
                LoginDetailsId = loginDetailsId
            };

            await sqlContext.Passkeys.AddAsync(newPasskey);
            await sqlContext.SaveChangesAsync();

            return Results.Created();
        }

        [Authorize]
        internal static async Task<IResult> GetPasskeyCredentials([FromQuery] int sourceId, [FromQuery] int loginDetailsId, KeyProvider keyProvider, SqlContext sqlContext)
        {
            Passkey? passkey = await sqlContext.Passkeys.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);

            if (passkey == null)
                return Results.NotFound();

            byte[] encryptedChallenge = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(sourceId), passkey.Challenge);
            return Results.Ok(new PasskeyCredentialResponse
            {
                CredentialIdB64 = Convert.ToBase64String(passkey.CredentialId),
                UserIdB64 = Convert.ToBase64String(passkey.UserId),
                PublicKeyB64 = Convert.ToBase64String(passkey.PublicKey),
                ChallengeB64 = Convert.ToBase64String(encryptedChallenge)
            });
        }
    }
}
