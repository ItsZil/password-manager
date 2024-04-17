using Server.Utilities;
using System.Linq;

namespace Server.Middleware
{
    public class KeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly List<string> exceptionEndpoints = new List<string> { "/api/handshake", "/api/hasexistingvault" };

        public KeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, KeyProvider keyProvider)
        {
            // Check if this is not a request to handshake
            if (!exceptionEndpoints.Contains(context.Request.Path) && context.Request.Method != "POST" && context.Request.Method != "HEAD")
            {
                // Check if KeyProvider.GetSharedSecret() is null
                if (!keyProvider.SharedSecretNotNull())
                {
                    // If it is null, return a 403 Forbidden response
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("No shared secret has been established.");
                    return;
                }
            }
            await _next(context);
        }
    }
}
