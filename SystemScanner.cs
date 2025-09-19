using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using Microsoft.Win32;
using PackageConsole;

namespace PackageConsole
{
    public class SystemSnapshot
    {
        public HashSet<string> Files { get; set; } = new();
        public HashSet<string> Folders { get; set; } = new();
        public HashSet<string> Shortcuts { get; set; } = new();
        public HashSet<string> RegistryKeys { get; set; } = new();
        public HashSet<string> Services { get; set; } = new();
    }

    public static class SystemScanner
    {
        private static readonly string[] FolderPathsToTrack = new[]
         {
            Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") ?? @"C:", ""),       // C:\
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),                // C:\Program Files
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),             // C:\Program Files (x86)
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)        // C:\ProgramData
        }
         .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))           // Ensure it's valid
         .Distinct(StringComparer.OrdinalIgnoreCase)                                           // Avoid duplicates
         .ToArray();

        private static readonly string[] FilePathsToTrack = new[]
        {
            @"C:\Windows", @"C:\Windows\System32"
        };

        private static readonly string[] ShortcutPathsToTrack = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), // Current user desktop
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),        // Current user Start Menu
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),         // Current user Programs menu
            Environment.GetEnvironmentVariable("PUBLIC") + @"\Desktop",            // Public (All users) Desktop
            Environment.GetEnvironmentVariable("ProgramData") + @"\Microsoft\Windows\Start Menu" // All users Start Menu
        };

        private static readonly RegistryKey[] RegistryRoots = new[]
        {
            Registry.LocalMachine,
            Registry.CurrentUser
        };

        public static SystemSnapshot Capture()
        {
            return new SystemSnapshot
            {
                Files = new HashSet<string>(CaptureFiles()),
                Folders = new HashSet<string>(CaptureFolders()),
                Shortcuts = new HashSet<string>(CaptureShortcuts()),
                RegistryKeys = new HashSet<string>(CaptureRegistryKeys()),
                Services = new HashSet<string>(CaptureServices())
            };
        }

        private static IEnumerable<string> CaptureFiles()
        {
            return FilePathsToTrack.SelectMany(path =>
            {
                try { return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories); }
                catch { return Enumerable.Empty<string>(); }
            });
        }

        private static IEnumerable<string> CaptureFolders()
        {
            var allFolders = new List<string>();

            foreach (var root in FolderPathsToTrack)
            {
                try
                {
                    Log("DEBUG", $"📁 Scanning: {root}", Brushes.SlateGray);
                    var dirs = SafeGetDirectories(root);
                    Log("DEBUG", $"✔️ Found {dirs.Count} subdirectories in {root}", Brushes.LightSeaGreen);

                    allFolders.Add(root);            // Include the root
                    allFolders.AddRange(dirs);       // Add subdirectories
                }
                catch (Exception ex)
                {
                    Log("WARN", $"⚠️ Could not process root folder {root}: {ex.Message}", Brushes.Goldenrod);
                }
            }

            return allFolders.Distinct();
        }

        private static List<string> SafeGetDirectories(string root)
        {
            var results = new List<string>();

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    results.Add(dir);
                    results.AddRange(SafeGetDirectories(dir));
                }
            }
            catch (Exception ex)
            {
                Log("SKIP", $"❌ Skipping root {root}: {ex.Message}", Brushes.DimGray);
            }

            return results;
        }

        private static void Log(string type, string message, Brush brush = null)
        {
            AppDeploymentTestPage.GlobalLogAction?.Invoke(type, message, brush ?? Brushes.White);
        }

        private static IEnumerable<string> CaptureShortcuts()
        {
            return ShortcutPathsToTrack.SelectMany(path =>
            {
                try { return Directory.GetFiles(path, "*.lnk", SearchOption.AllDirectories); }
                catch { return Enumerable.Empty<string>(); }
            });
        }

        private static IEnumerable<string> CaptureRegistryKeys()
        {
            var interestingKeys = new[]
            {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SYSTEM\CurrentControlSet\Services",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
    };

            var registryData = new HashSet<string>();

            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var subKeyPath in interestingKeys)
                {
                    try
                    {
                        using var key = root.OpenSubKey(subKeyPath);
                        if (key != null)
                        {
                            foreach (var sub in key.GetSubKeyNames())
                            {
                                registryData.Add($"{root.Name}\\{subKeyPath}\\{sub}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("WARN", $"🔐 Cannot access {root.Name}\\{subKeyPath} - {ex.Message}", Brushes.Gray);
                    }
                }
            }

            return registryData;
        }

        private static void CaptureRegistryKeysRecursive(RegistryKey root, string path, List<string> keys)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key == null) return;

                string fullPath = root.Name + "\\" + path;
                keys.Add(fullPath);

                foreach (var sub in key.GetSubKeyNames())
                {
                    string subPath = string.IsNullOrWhiteSpace(path) ? sub : $"{path}\\{sub}";
                    try
                    {
                        CaptureRegistryKeysRecursive(root, subPath, keys);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Log("REGISTRY", $"🔒 Access denied to {root.Name}\\{subPath}", Brushes.Gray);
                    }
                    catch (Exception ex)
                    {
                        Log("REGISTRY", $"⚠️ Failed on {root.Name}\\{subPath} : {ex.Message}", Brushes.Gray);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("REGISTRY", $"❌ Root path error {root.Name}\\{path}: {ex.Message}", Brushes.Gray);
            }
        }

        private static IEnumerable<string> CaptureServices()
        {
            return ServiceController.GetServices().Select(s => s.ServiceName);
        }

        public static string GenerateHtmlReport(SystemSnapshot before, SystemSnapshot after)
        {
            var html = new StringBuilder();
            html.AppendLine("<html><head><title>Application Deployment Report</title>");
            html.AppendLine("<style>body{font-family:Segoe UI;} h2{background:#eee;padding:10px;} ul{list-style:none;} li{margin:2px;}</style>");
            html.AppendLine("</head><body><h1>Deployment Delta Report</h1>");

            html.AppendLine(BuildSection("Folders", before.Folders, after.Folders));
            html.AppendLine(BuildSection("Files", before.Files, after.Files));
            html.AppendLine(BuildSection("Shortcuts", before.Shortcuts, after.Shortcuts));
            html.AppendLine(BuildSection("Registry", before.RegistryKeys, after.RegistryKeys));
            html.AppendLine(BuildSection("Services", before.Services, after.Services));

            html.AppendLine("</body></html>");

            string logsDir = PathManager.ScannerReportPath;
            Directory.CreateDirectory(logsDir);
            string path = Path.Combine(logsDir, $"DeploymentReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.WriteAllText(path, html.ToString());

            return path;
        }

        private static string BuildSection(string title, HashSet<string> before, HashSet<string> after)
        {
            var added = after.Except(before).ToList();
            var removed = before.Except(after).ToList();

            if (added.Count == 0 && removed.Count == 0) return $"<h2>{title}</h2><p>No changes</p>";

            var sb = new StringBuilder();
            sb.AppendLine($"<h2>{title}</h2><ul>");
            foreach (var item in added) sb.AppendLine($"<li style='color:green;'>+ {item}</li>");
            foreach (var item in removed) sb.AppendLine($"<li style='color:red;'>- {item}</li>");
            sb.AppendLine("</ul>");
            return sb.ToString();
        }

        public static void ExportInstalledAppsToTsv(string filePath)
        {
            var uninstallKeys = new[]
            {
                (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            var sb = new StringBuilder();
            sb.AppendLine("DisplayName\tVersion\tPublisher\tInstallDate\tProductCode\tRegistryPath");

            foreach (var (hive, subkey) in uninstallKeys)
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenSubKey(subkey);
                if (baseKey == null) continue;

                foreach (var sub in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = baseKey.OpenSubKey(sub);
                        if (appKey == null) continue;

                        string name = appKey.GetValue("DisplayName")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string version = appKey.GetValue("DisplayVersion")?.ToString() ?? "";
                        string publisher = appKey.GetValue("Publisher")?.ToString() ?? "";
                        string date = appKey.GetValue("InstallDate")?.ToString() ?? "";
                        string productCode = appKey.GetValue("ProductCode")?.ToString() ?? "";

                        sb.AppendLine($"{name}\t{version}\t{publisher}\t{date}\t{productCode}\t{hive}\\{subkey}\\{sub}");
                    }
                    catch (Exception ex)
                    {
                        Log("ERROR", $"❌ Reading key failed: {hive}\\{subkey}\\{sub} -> {ex.Message}", Brushes.OrangeRed);
                    }
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Log("EXPORT", $"Installed apps exported to TSV: {filePath}", Brushes.LightCyan);
        }

        public static void SaveSnapshot(SystemSnapshot snapshot, string tag)
        {
            string logsDir = PathManager.ScannerReportPath;
            Directory.CreateDirectory(logsDir);
            string path = Path.Combine(logsDir, $"snapshot_{tag}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }


    }
}
