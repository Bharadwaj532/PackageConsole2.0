using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using PackageConsole.Data;

namespace PackageConsole
{
    public partial class AdminSettingsPage : Page
    {
        private readonly string AppSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        private ObservableCollection<VarRow> _tooltipRows = new();
        public class VarRow { public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }


        public AdminSettingsPage()
        {
            InitializeComponent();

            if (!AppUserRoles.IsCurrentUserAdmin())
            {
                MessageBox.Show("You do not have permission to access Admin Settings.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                // Navigate back to Home
                Services.NavigationService.Instance.Navigate(new HomePage());
                return;
            }

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                // Load JSON if present, fallback values from App.config via PathManager properties
                JObject root = File.Exists(AppSettingsPath)
                    ? JObject.Parse(File.ReadAllText(AppSettingsPath))
                    : new JObject();

                JObject app = (JObject?)root["AppSettings"] ?? new JObject();

                ToolkitPathText.Text = GetValue(app, "ToolkitPath", PathManager.ToolkitPath);
                FeedbackPathText.Text = GetValue(app, "FeedbackPath", PathManager.FeedbackPath);
                ArchivePathText.Text = GetValue(app, "ArchivePath", PathManager.ArchivePath);
                BasePathText.Text = GetValue(app, "BasePath", PathManager.BasePath);
                CentralBasePathText.Text = GetValue(app, "CentralBasePath", PathManager.CentralBasePath);
                CompletedPackagesPathText.Text = GetValue(app, "CompletedPackagesPath", PathManager.CompletedPackagesPath);
                BasePackagePathText.Text = GetValue(app, "BasePackagePath", PathManager.BasePackagePath);
                ScannerReportPathText.Text = GetValue(app, "ScannerReportPath", PathManager.ScannerReportPath);
                UpdatesFilePathText.Text = GetValue(app, "UpdatesFilePath", PathManager.UpdatesFilePath);
                AppTestingRootText.Text = GetValue(app, "AppTestingRoot", PathManager.AppTestingRoot);
                LocalFeedbackDbFolderText.Text = GetValue(app, "LocalFeedbackDbFolder", PathManager.LocalFeedbackDbFolder);
                CentralFeedbackDbFolderText.Text = GetValue(app, "CentralFeedbackDbFolder", PathManager.CentralFeedbackDbFolder);

                AdminUsersText.Text = GetValue(app, "AdminUsers", string.Join(",", AppUserRoles.AdminUsers));
                DevUsersText.Text = GetValue(app, "DevUsers", string.Join(",", AppUserRoles.DevUsers));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            try
            {
                // Populate DB info
                TxtLocalPkgDb.Text = PathManager.MetadataDb;
                TxtCentralPkgDb.Text = PathManager.CentralMetadataDb;
                TxtMasterPkgDb.Text = Data.SqliteAggregator.GetMasterDbPath();
                TxtLocalFbkDb.Text = PathManager.LocalUserDb;

            // Load tooltip variables grid
            try { LoadTooltipRows(); } catch { }

                TxtCentralFbkDb.Text = PathManager.CentralUserDb;
                TxtCentralFolder.Text = System.IO.Path.GetDirectoryName(PathManager.CentralMetadataDb);
            }
            catch { }
        }

        private static string GetValue(JObject app, string key, string fallback)
        {
            var token = app[key];
            return token?.ToString() ?? fallback;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                JObject root = File.Exists(AppSettingsPath)
                    ? JObject.Parse(File.ReadAllText(AppSettingsPath))
                    : new JObject();

                if (root["AppSettings"] == null) root["AppSettings"] = new JObject();
                var app = (JObject)root["AppSettings"]!;

                app["ToolkitPath"] = ToolkitPathText.Text.Trim();
                app["FeedbackPath"] = FeedbackPathText.Text.Trim();
                app["ArchivePath"] = ArchivePathText.Text.Trim();
                app["BasePath"] = BasePathText.Text.Trim();
                app["CentralBasePath"] = CentralBasePathText.Text.Trim();
                app["AppTestingRoot"] = AppTestingRootText.Text.Trim();
                app["LocalFeedbackDbFolder"] = LocalFeedbackDbFolderText.Text.Trim();
                app["CentralFeedbackDbFolder"] = CentralFeedbackDbFolderText.Text.Trim();

                app["CompletedPackagesPath"] = CompletedPackagesPathText.Text.Trim();
                app["BasePackagePath"] = BasePackagePathText.Text.Trim();
                app["ScannerReportPath"] = ScannerReportPathText.Text.Trim();
                app["UpdatesFilePath"] = UpdatesFilePathText.Text.Trim();
                app["AdminUsers"] = AdminUsersText.Text.Trim();
                app["DevUsers"] = DevUsersText.Text.Trim();

                File.WriteAllText(AppSettingsPath, root.ToString(Formatting.Indented));

                MessageBox.Show("Settings saved. Some changes might require reopening certain pages.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCentralFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = System.IO.Path.GetDirectoryName(PathManager.CentralMetadataDb);
                if (string.IsNullOrWhiteSpace(folder))
                {
                    MessageBox.Show("Central metadata folder is not configured.");
                    return;
                }
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder: {ex.Message}");
            }
        }

        private void GenerateSample_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CentralDbSeeder.GenerateSampleCentralPackageDbs();
                MessageBox.Show("Sample central package DBs created. Use 'Refresh Master' on Package Dashboard to aggregate.", "Done");
            }
            catch (Exception ex)
            {
                var centralFolder = System.IO.Path.GetDirectoryName(PathManager.CentralMetadataDb) ?? "<null>";
                MessageBox.Show($"Failed to create sample DBs at '{centralFolder}': {ex.Message}", "Error");
            }
        }

        private void CreateMyCentral_Click(object sender, RoutedEventArgs e)
        {
            var result = SqlitePackageHelper.MirrorLocalToCentralNow();
            if (result.success)
            {
                MessageBox.Show("Your central package DB has been created/updated from local.", "Success");
            }
            else
            {
                var centralPath = PathManager.CentralMetadataDb;
                MessageBox.Show($"Failed to mirror to central at '{centralPath}': {result.error}", "Error");
            }
        }

        private void LoadTooltipRows()
        {
            var dict = VariableHelper.GetEditableVariables();
            _tooltipRows = new ObservableCollection<VarRow>(dict.Select(kvp => new VarRow { Key = kvp.Key, Value = kvp.Value }));
            TooltipGrid.ItemsSource = _tooltipRows;
        }

        private void SaveTooltips_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in _tooltipRows)
                {
                    if (row == null) continue;
                    var key = (row.Key ?? string.Empty).Trim();
                    var val = (row.Value ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val)) continue;
                    map[key] = val;
                }
                VariableHelper.SaveTooltipOverrides(map);
                MessageBox.Show($"Saved {map.Count} tooltip variables to: {PathManager.TooltipsConfigPath}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save tooltip variables: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadTooltips_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VariableHelper.ReloadTooltipOverrides();
                LoadTooltipRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reload tooltip variables: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
