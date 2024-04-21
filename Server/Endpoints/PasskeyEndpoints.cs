using UtilitiesLibrary.Models;
using Microsoft.AspNetCore.Mvc;
using Server.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Geralt;
using System.Text;
using System.Text.Json;

namespace Server.Endpoints
{
    internal static class PasskeyEndpoints
    {
        internal static RouteGroupBuilder MapPasskeyEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/passkey", CreatePasskey);
            group.MapGet("/passkey", GetPasskeyCredentials);
            group.MapPost("/passkeyverify", VerifyPasskeyCredentials);

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

            if (credentialId.Length < 32 || publicKey.Length < 32 || challenge.Length < 16)
                return Results.BadRequest();

            if (await sqlContext.LoginDetails.FirstOrDefaultAsync(x => x.Id == loginDetailsId) == null)
                return Results.NotFound();

            var existingPasskey = await sqlContext.Passkeys.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
            if (existingPasskey != null)
            {
                // Default behaviour is to overwrite the existing passkey
                sqlContext.Passkeys.Remove(existingPasskey);
                await sqlContext.SaveChangesAsync();
            }

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

            // Use a random challenge to prevent replay attacks
            byte[] randomChallenge = new byte[16];
            SecureRandom.Fill(randomChallenge);

            passkey.Challenge = randomChallenge;
            await sqlContext.SaveChangesAsync();

            byte[] encryptedChallenge = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(sourceId), passkey.Challenge);
            return Results.Ok(new PasskeyCredentialResponse
            {
                CredentialIdB64 = Convert.ToBase64String(passkey.CredentialId),
                UserId = Convert.ToBase64String(passkey.UserId),
                PublicKeyB64 = Convert.ToBase64String(passkey.PublicKey),
                ChallengeB64 = Convert.ToBase64String(encryptedChallenge)
            });
        }

        [Authorize]
        internal static async Task<IResult> VerifyPasskeyCredentials([FromBody] PasskeyVerificationRequest request, KeyProvider keyProvider, SqlContext sqlContext)
        {
            byte[] credentialId = Base64UrlEncoder.DecodeBytes(request.CredentialIdB64);

            if (credentialId.Length < 32)
                return Results.BadRequest();

            // Check if the user verification flag is set
            bool uvFlagSet = PasskeyUtil.IsUserVerificationCompleted(request.AuthenticatorDataB64);
            if (!uvFlagSet)
                return Results.Unauthorized();

            // Convert the clientDataJSON into a ClientDataJson object
            string clientDataJson = Encoding.UTF8.GetString(Convert.FromBase64String(request.clientDataJsonBase64));
            var clientData = JsonSerializer.Deserialize<ClientDataJson>(clientDataJson);

            if (clientData == null)
                return Results.BadRequest();

            // Retrieve the passkey for this credential from the database
            Passkey? passkey = await sqlContext.Passkeys.FirstOrDefaultAsync(x => x.CredentialId == credentialId && x.LoginDetailsId == request.LoginDetailsId);

            if (passkey == null)
                return Results.Unauthorized();

            // Check if the challenge from the client matches the challenge stored in the database
            byte[] clientChallenge = Base64UrlEncoder.DecodeBytes(clientData.Challenge);
            if (!clientChallenge.SequenceEqual(passkey.Challenge))
                return Results.Unauthorized();

            byte[] passkeyChallenge = passkey.Challenge;
            byte[] publicKey = passkey.PublicKey;

            // Replace the challenge with a new one to prevent replay attacks
            byte[] newChallenge = new byte[16];
            SecureRandom.Fill(newChallenge);

            passkey.Challenge = newChallenge;
            await sqlContext.SaveChangesAsync();

            // Check if the origin is the extension
            if (clientData.Origin != "chrome-extension://icbeakhigcgladpiblnolcogihmcdoif")
                return Results.Unauthorized();

            // Concatenate the authenticator data and the clientDataJSON hash
            byte[] authenticatorData = Convert.FromBase64String(request.AuthenticatorDataB64);
            byte[] clientDataHash = Convert.FromBase64String(request.ClientDataHashB64);
            byte[] data = PasskeyUtil.ConcatenateArrays(authenticatorData, clientDataHash);

            // Verify the signature
            byte[] signature = Convert.FromBase64String(request.SignatureB64);
            bool signatureVerified = PasskeyUtil.VerifyPasskeySignature(publicKey, data, signature);

            if (signatureVerified)
                return Results.Ok();
            else
                return Results.Unauthorized();
        }
    }
}
