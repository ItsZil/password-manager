using Server.Utilities;

namespace Server.Middleware
{
    public class KeyMiddleware
    {
        private readonly RequestDelegate _next;

        public KeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, KeyProvider keyProvider)
        {
            // Check if this is not a request to handshake
            if (!context.Request.Path.StartsWithSegments("/api/handshake") && context.Request.Method != "POST")
            {
                // Check if KeyProvider.GetSharedSecret() is null
                if (keyProvider.GetSharedSecret() == null)
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
