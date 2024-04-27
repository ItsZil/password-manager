using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Server.Endpoints;
using Server.Middleware;
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
            builder.Services.AddCors();
            builder.Services.AddResponseCaching();

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
            app.UseMiddleware<KeyMiddleware>();

            // Middleware to limit access to the local network
            bool internetAccessEnabled = ConfigUtil.GetVaultInternetAccess();
            if (!app.Environment.IsEnvironment("TEST_INTEGRATION") && !internetAccessEnabled)
            {
                app.UseMiddleware<InternetAccessMiddleware>();
            }

            // Only allow the Chrome extension to access the vault
            app.UseCors(builder =>
            {
                builder.WithOrigins("chrome-extension://icbeakhigcgladpiblnolcogihmcdoif")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });

            app.UseResponseCaching();

            var rootApi = app.MapGroup("/api/");
            rootApi.MapLoginDetailsEndpoints();
            rootApi.MapHandshakeEndpoints();
            rootApi.MapConfigurationEndpoints();
            rootApi.MapPasskeyEndpoints();
            rootApi.MapExtraAuthEndpoints();
            rootApi.MapPinCodeEndpoints();
            rootApi.MapAuthenticatorEndpoints();

            app.Run();
        }
    }

    [JsonSerializable(typeof(DomainLoginRequest))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}
