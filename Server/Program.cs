using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Server.Endpoints;
using UtilitiesLibrary.Models;

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

                if (app.Environment.IsEnvironment("Testing"))
                {
                    // If the app is running in a testing environment, create a new database for each test run.
                    dbContext.ChangeDatabasePath($"vault_test_{Guid.NewGuid()}.db");
                }

                dbContext.Database.EnsureCreated();
            }

            // Configure Forwarded Headers to allow for correct scheme usage behind reverse proxies
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Middleware to limit access to the local network
            if (!app.Environment.IsEnvironment("Testing"))
            {
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
            }

            var rootApi = app.MapGroup("/api/");
            rootApi.MapTestEndpoints();
            rootApi.MapLoginDetailsEndpoints();

            app.Run();
        }
    }

    [JsonSerializable(typeof(Response))]
    [JsonSerializable(typeof(DomainLoginRequest))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}
