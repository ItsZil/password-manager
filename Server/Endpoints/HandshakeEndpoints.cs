using UtilitiesLibrary.Models;
using Microsoft.AspNetCore.Mvc;
using Server.Utilities;

namespace Server.Endpoints
{
    internal static class HandshakeEndpoints
    {
        internal static RouteGroupBuilder MapHandshakeEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/handshake", ComputeSharedSecretKey);

            return group;
        }

        internal static IResult ComputeSharedSecretKey([FromBody] HandshakeRequest request, KeyProvider keyProvider)
        {
            byte[] clientPublicKey = Convert.FromBase64String(request.ClientPublicKey);
            byte[] serverPublicKey = keyProvider.ComputeSharedSecret(clientPublicKey);

            return Results.Ok(new HandshakeResponse { ServerPublicKey = Convert.ToBase64String(serverPublicKey) });
        }
    }
}
