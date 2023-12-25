using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Server.Endpoints;

namespace Server
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
            });

            builder.Services.AddDbContext<SqlContext>();

            // Configure the app to serve over HTTPS only
            builder.WebHost.UseKestrelHttpsConfiguration();

            var app = builder.Build();

            // Ensure the SQLite database is created
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SqlContext>();
                dbContext.Database.EnsureCreated();
            }

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


            var rootApi = app.MapGroup("/api/");
            rootApi.MapTestEndpoints();

            app.Run();
        }
    }

    [JsonSerializable(typeof(Response))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}
