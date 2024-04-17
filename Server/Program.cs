using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Server.Endpoints;
using Server.Utilities;
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

            builder.Services.AddScoped<ServiceCollection>();
            builder.Services.AddDbContext<SqlContext>();
            builder.Services.AddSingleton<KeyProvider>();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                    {
                        SymmetricSecurityKey issuerSigningKey = new SymmetricSecurityKey(ConfigUtil.GetJwtSecretKey());
                        return new List<SecurityKey>() { issuerSigningKey };
                    },
                    ClockSkew = TimeSpan.Zero
                };
            });

            builder.Services.AddAuthorization();

            // Configure the app to serve over HTTPS only
            builder.WebHost.UseKestrelHttpsConfiguration();

            // Use a consistent port
            builder.WebHost.UseUrls("https://localhost:54782");

            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();

            // Ensure the SQLite database is created
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SqlContext>();

                if (app.Environment.IsEnvironment("TEST_INTEGRATION"))
                {
                    // If the app is running in a testing environment, create a new database for each test run.
                    dbContext.ChangeDatabasePath($"vault_test_{Guid.NewGuid()}.db");

                    // When SqlContext is injected as a dependency for endpoints, it needs to know where the existing test database is located.
                    // To do this, we add the database path to the configuration, and then access it in the SqlContext constructor.
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    config["TEST_INTEGRATION_DB_PATH"] = dbContext.dbPath;
                }
            }

            // Configure Forwarded Headers to allow for correct scheme usage behind reverse proxies
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Map a HEAD method to allow for health checks
            app.MapMethods("", new[] { "HEAD" }, () => { });

            // Middleware to check if a shared secret key has been computed (handshake process complete)
            //app.UseMiddleware<KeyMiddleware>();

            // Middleware to limit access to the local network
            if (!app.Environment.IsEnvironment("TEST_INTEGRATION"))
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
            rootApi.MapLoginDetailsEndpoints();
            rootApi.MapHandshakeEndpoints();
            rootApi.MapConfigurationEndpoints();

            if (app.Environment.IsEnvironment("TEST_INTEGRATION"))
                rootApi.MapTestEndpoints();

            app.Run();
        }
    }

    [JsonSerializable(typeof(Response))]
    [JsonSerializable(typeof(DomainLoginRequest))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}
