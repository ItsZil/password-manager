using UtilitiesLibrary.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Server.Endpoints
{
    internal static class ExtraAuthEndpoints
    {
        internal static RouteGroupBuilder MapExtraAuthEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/extraauth", GetExtraAuthTypeId);
            group.MapPut("/extraauth", SetExtraAuth);
            group.MapDelete("/extraauth", RemoveExtraAuth);

            return group;
        }

        [Authorize]
        internal static async Task<IResult> GetExtraAuthTypeId([FromQuery] int loginDetailsId, SqlContext sqlContext)
        {
            var loginDetails = await sqlContext.LoginDetails.Include(x => x.ExtraAuth).FirstOrDefaultAsync(x => x.Id == loginDetailsId);
            if (loginDetails == null || loginDetails.ExtraAuth == null)
                return Results.NotFound();

            return Results.Ok(loginDetails.ExtraAuth.Id);
        }

        [Authorize]
        internal static async Task<IResult> SetExtraAuth([FromBody] SetExtraAuthRequest request, SqlContext sqlContext)
        {
            var loginDetails = await sqlContext.LoginDetails.Include(x => x.ExtraAuth).FirstOrDefaultAsync(x => x.Id == request.LoginDetailsId);
            if (loginDetails == null || loginDetails.ExtraAuth == null)
                return Results.NotFound();

            if (request.ExtraAuthId == loginDetails.ExtraAuthId)
                return Results.NoContent();

            // Remove the existing extra authentication.
            await RemoveExistingExtraAuth(loginDetails, sqlContext);

            switch (request.ExtraAuthId)
            {
                case 2:
                    // User wants to use a PIN code. Verify it exists in the database.
                    var pinCode = await sqlContext.PinCodes.FirstOrDefaultAsync(x => x.LoginDetailsId == request.LoginDetailsId);
                    if (pinCode == null)
                        return Results.NotFound();
                    break;
                case 3:
                    // User wants to set extra auth method to passkey, retrieve it from the database to confirm it exists.
                    var passkeyToUse = await sqlContext.Passkeys.FirstOrDefaultAsync(x => x.LoginDetailsId == request.LoginDetailsId);
                    if (passkeyToUse == null)
                        return Results.NotFound();
                    break;
                case 4:
                    // User wants to set extra auth method to passphrase. This is also supported, so do not fall through to default.
                    break;
                default:
                    return Results.BadRequest();
            };

            loginDetails.ExtraAuthId = request.ExtraAuthId;
            await sqlContext.SaveChangesAsync();
            return Results.NoContent();
        }

        [Authorize]
        internal static async Task<IResult> RemoveExtraAuth([FromQuery] int loginDetailsId, SqlContext sqlContext)
        {
            var loginDetails = await sqlContext.LoginDetails.Include(x => x.ExtraAuth).FirstOrDefaultAsync(x => x.Id == loginDetailsId);
            if (loginDetails == null || loginDetails.ExtraAuth == null)
                return Results.NotFound();

            bool removed = await RemoveExistingExtraAuth(loginDetails, sqlContext);
            loginDetails.ExtraAuthId = 1;
            await sqlContext.SaveChangesAsync();

            if (removed)
                return Results.NoContent();
            else
                return Results.StatusCode(500);
        }


        internal static async Task<bool> RemoveExistingExtraAuth(LoginDetails loginDetails, SqlContext sqlContext)
        {
            int loginDetailsId = loginDetails.Id;
            int currentExtraAuthMethod = loginDetails.ExtraAuthId;
            if (currentExtraAuthMethod == 1)
            {
                // Extra auth is already set to None, nothing to do.
                return true;
            }
            if (currentExtraAuthMethod == 2)
            {
                // Remove the PIN code from the database.
                var pinCode = await sqlContext.PinCodes.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
                if (pinCode != null)
                    sqlContext.PinCodes.Remove(pinCode);
            }
            else if (currentExtraAuthMethod == 3)
            {
                // Remove the passkey from the database.
                var passkeyToRemove = await sqlContext.Passkeys.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
                if (passkeyToRemove != null)
                    sqlContext.Passkeys.Remove(passkeyToRemove);
            }
            return true;
        }
    }
}
