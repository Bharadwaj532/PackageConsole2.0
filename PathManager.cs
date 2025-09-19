using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PackageConsole
{
    public static class PathManager
    {
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

        public static string ToolkitPath => GetPath("ToolkitPath");
        public static string FeedbackPath => GetPath("FeedbackPath");
        public static string ArchivePath => GetPath("ArchivePath");
        public static string BasePath => GetPath("BasePath");
        public static string CentralBasePath => GetPath("CentralBasePath");
        public static string CompletedPackagesPath => GetPath("CompletedPackagesPath");
        public static string BasePackagePath => GetPath("BasePackagePath", @"C:\\Temp\\PackageConsole");
        public static string ScannerReportPath => GetPath("ScannerReportPath", @"C:\\Temp\\AppTesting\\Reports\\SystemScannerLogs");
        public static string UpdatesFilePath => GetPath("UpdatesFilePath", @"\\\\nasdslapps001\\drm_pkging\\Team\\TeamUtils\\PackageConsole\\Docs\\Updates.txt");
        public static string AppTestingRoot => GetPath("AppTestingRoot", @"C:\\Temp\\AppTesting");
        public static string LocalFeedbackDbFolder => GetPath("LocalFeedbackDbFolder", @"C:\\Temp\\PackageConsole\\Feedback");
        public static string CentralFeedbackDbFolder => GetPath("CentralFeedbackDbFolder", @"\\\\nasdslapps001\\drm_pkging\\Team\\TeamUtils\\PackageConsole\\Feedback");

        public static string LocalUserDb => Path.Combine(LocalFeedbackDbFolder, $"{Environment.UserName}_feedback.db");
        public static string CentralUserDb => Path.Combine(CentralFeedbackDbFolder, $"{Environment.UserName}_feedback.db");
        public static string MetadataDb => Path.Combine(BasePath, "Metadata", "packages.db");
        public static string CentralMetadataDb =>  Path.Combine(CentralBasePath, "Metadata", $@"{Environment.UserName}_packages.db");
        public static string TooltipsConfigPath => Path.Combine(BasePath, "Metadata", "tooltips.json");

        private static string GetPath(string key, string? defaultValue = null)
        {
            // Prefer appsettings.json (AppSettings section), fallback to App.config
            string? value = JsonConfig.Value[$"AppSettings:{key}"] ?? System.Configuration.ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue ?? throw new Exception($"Missing path config for key: {key}");

            return Environment.ExpandEnvironmentVariables(value);
        }

        public static string BuildProductFolder(string appName, string appVersion)
        {
            return Path.Combine(BasePackagePath, appName, appVersion, "Altiris");
        }

        public static string BuildSupportFilesFolder(string appName, string appVersion)
        {
            return Path.Combine(BuildProductFolder(appName, appVersion), "1.0", "SupportFiles");
        }

        public static string BuildArchivePath(string vendor, string name, string version, string build, string v = "Altiris")
        {
            return Path.Combine(ArchivePath, vendor, name, version, v);
        }

        public static string BuildCompletedPath(string vendor, string name, string version, string build, string v = "Altiris")
        {
            return Path.Combine(CompletedPackagesPath, vendor, name, version, v);
        }
    }
}
