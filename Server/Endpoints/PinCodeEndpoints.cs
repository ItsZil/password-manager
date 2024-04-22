using UtilitiesLibrary.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Server.Utilities;
using System.Text;

namespace Server.Endpoints
{
    internal static class PinCodeEdnpoints
    {
        internal static RouteGroupBuilder MapPinCodeEndpoints(this RouteGroupBuilder group)
        {
            group.MapGet("/pincode", GetPinCode);
            group.MapPost("/pincode", CreatePinCode);
            group.MapDelete("/pincode", RemovePinCode);

            return group;
        }

        [Authorize]
        internal static async Task<IResult> GetPinCode([FromQuery] int sourceId, [FromQuery] int loginDetailsId, SqlContext sqlContext, KeyProvider keyProvider)
        {
            var pinCode = await sqlContext.PinCodes.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
            if (pinCode == null)
                return Results.NotFound();

            byte[] pinCodeBytes = BitConverter.GetBytes(pinCode.Code);
            byte[] encryptedPinCodeBytes = await PasswordUtil.EncryptMessage(keyProvider.GetSharedSecret(sourceId), pinCodeBytes);
            string encryptedPinCode = Convert.ToBase64String(encryptedPinCodeBytes);

            return Results.Ok(new GetPinCodeResponse { PinCode = encryptedPinCode });
        }

        [Authorize]
        internal static async Task<IResult> CreatePinCode([FromBody] SetPinCodeRequest request, SqlContext sqlContext, KeyProvider keyProvider)
        {
            // Check if the login details exist.
            if (!await sqlContext.LoginDetails.AnyAsync(x => x.Id == request.LoginDetailsId))
                return Results.NotFound();

            byte[] encryptedPinCode = Convert.FromBase64String(request.PinCode);
            byte[] decryptedPinCodeBytes = await PasswordUtil.DecryptMessage(keyProvider.GetSharedSecret(request.SourceId), encryptedPinCode);
            string decryptedPinCodeString = Encoding.UTF8.GetString(decryptedPinCodeBytes);

            // Check if the decrypted PIN code is a valid integer and is exactly 4 digits long.
            if (!int.TryParse(decryptedPinCodeString, out int newPinCode) || decryptedPinCodeString.Length != 4)
                return Results.BadRequest();

            var oldpinCode = await sqlContext.PinCodes.FirstOrDefaultAsync(x => x.LoginDetailsId == request.LoginDetailsId);
            if (oldpinCode == null)
            {
                // Create a new PIN code.
                oldpinCode = new PinCode
                {
                    LoginDetailsId = request.LoginDetailsId,
                    Code = newPinCode
                };
                await sqlContext.PinCodes.AddAsync(oldpinCode);
            }
            else
            {
                // Replace the old PIN code with the new one.
                oldpinCode.Code = newPinCode;
            }
            await sqlContext.SaveChangesAsync();
            return Results.Created();
        }

        [Authorize]
        internal static async Task<IResult> RemovePinCode([FromQuery] int loginDetailsId, SqlContext sqlContext)
        {
            var pinCode = await sqlContext.PinCodes.FirstOrDefaultAsync(x => x.LoginDetailsId == loginDetailsId);
            if (pinCode == null)
                return Results.NotFound();

            sqlContext.PinCodes.Remove(pinCode);
            await sqlContext.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}
