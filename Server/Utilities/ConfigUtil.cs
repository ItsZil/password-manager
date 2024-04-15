using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Server.Utilities
{
    public static class ConfigUtil
    {
        private static string configPath;

        static ConfigUtil()
        {
            var path = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(path);
            configPath = Path.Join(folder, "config.json");

            if (Directory.Exists(folder) && !File.Exists(configPath))
            {
                CreateConfigurationFile(folder);
            }
        }

        public static string GetFileLocation()
        {
            return configPath;
        }

        public static void CreateConfigurationFile(string folder)
        {
            if (!File.Exists(configPath))
            {
                // Set default values
                var config = new Dictionary<string, string>
                {
                    { "VAULT_LOCATION", Path.Join(Path.GetTempPath(), "initialvault.db") },
                    { "JWT_SECRET_KEY", Encoding.UTF8.GetString(PasswordUtil.GenerateSecurePassword(64)) }
                };
                var configJson = JsonSerializer.Serialize(config);
                File.WriteAllText(configPath, configJson);
            }
        }

        public static string GetVaultLocation()
        {
            // Ensure we have a config file.
            CreateConfigurationFile(Path.GetDirectoryName(configPath) ?? configPath.Replace("config.json", ""));

            // Read the VAULT_LOCATION key from the config.json file
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);

            if (config != null && config.ContainsKey("VAULT_LOCATION"))
                return config["VAULT_LOCATION"];

            // This should never be reached
            return configPath.Insert(configPath.Length - 11, "vault.db");
        }

        public static void SetVaultLocation(string newPath)
        {
            if (!newPath.EndsWith(".db"))
            {
                newPath = Path.Join(newPath, "vault.db");
            }

            // Add it as the VAULT_LOCATION key in the config.json file
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);

            if (config != null)
            {
                config["VAULT_LOCATION"] = newPath;
                var newConfigJson = JsonSerializer.Serialize(config);
                File.WriteAllText(configPath, newConfigJson);
            }
        }

        internal static byte[] GetJwtSecretKey()
        {
            // Read the JWT_SECRET_KEY key from the config.json file
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);

            if (config != null && config.ContainsKey("JWT_SECRET_KEY"))
                return Encoding.UTF8.GetBytes(config["JWT_SECRET_KEY"]);

            // This should never be reached
            return new byte[] { };
        }
    }
}
