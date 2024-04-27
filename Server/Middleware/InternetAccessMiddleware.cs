using System.Net;

namespace Server.Middleware
{
    public class InternetAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public InternetAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp != null && (remoteIp.Equals(IPAddress.Loopback) || remoteIp.Equals(IPAddress.IPv6Loopback) && context.Request.IsHttps))
            {
                await _next(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Access denied. The vault is only available within the local network.");
            }
        }
    }
}
