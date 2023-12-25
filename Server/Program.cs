using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
            });

            // Configure the app to serve over HTTPS only
            builder.WebHost.UseKestrelHttpsConfiguration();

            var app = builder.Build();

            // Configure Forwarded Headers to allow for correct scheme usage behind reverse proxies
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Middleware to limit access to the local network
            app.Use(async (context, next) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                if (remoteIp != null && (remoteIp.Equals(IPAddress.Loopback) || remoteIp.Equals(IPAddress.IPv6Loopback) && context.Request.IsHttps))
                {
                    await next.Invoke();
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Access denied. This service is only available within the local network.");
                }
            });

            var testApi = app.MapGroup("/test");
            testApi.MapGet("/", () => $"Current time is {DateTime.Now}");

            app.Run();
        }
    }

    public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

    [JsonSerializable(typeof(Todo[]))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}
