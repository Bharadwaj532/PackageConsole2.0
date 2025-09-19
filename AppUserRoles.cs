using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NLog;

namespace PackageConsole
{
    public static class AppUserRoles
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(AppUserRoles));

        private static HashSet<string>? _adminUsers;
        private static HashSet<string>? _devUsers;

        // Prefer appsettings.json, fallback to App.config
        private static readonly Lazy<IConfigurationRoot> JsonConfig = new(() =>
        {
            try
            {
                return new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();
            }
            catch
            {
                return new ConfigurationBuilder().Build();
            }
        });

        public static HashSet<string> AdminUsers => _adminUsers ??= LoadUsers("AdminUsers");
        public static HashSet<string> DevUsers => _devUsers ??= LoadUsers("DevUsers");
        public static bool IsCurrentUserAdmin()
        {
            string currentUser = Environment.UserName?.Trim().ToLowerInvariant() ?? string.Empty;
            return AdminUsers.Contains(currentUser);
        }
        private static HashSet<string> LoadUsers(string configKey)
        {
            try
            {
                string? value = JsonConfig.Value[$"AppSettings:{configKey}"] ?? System.Configuration.ConfigurationManager.AppSettings[configKey];
                if (string.IsNullOrWhiteSpace(value))
                {
                    Logger.Warn($"{configKey} not found or empty in App.config.");
                    return new HashSet<string>();
                }

                var users = value.Split(',')
                                 .Select(u => u.Trim().ToLower())
                                 .Where(u => !string.IsNullOrWhiteSpace(u))
                                 .ToHashSet();

                Logger.Info($"{configKey} loaded: {string.Join(", ", users)}");
                return users;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error loading {configKey}");
                return new HashSet<string>();
            }
        }
    }

}
