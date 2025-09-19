using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using NLog;
using PackageConsole.Data;
using PackageConsole.Models;

namespace PackageConsole.Pages
{
    public partial class PackageViewerPage : Page
    {
        private bool isAdminMode = false;

        private List<PackageInfo> allPackages = new();
        private static readonly Logger Logger = LogManager.GetLogger(nameof(PackageViewerPage));
        public PackageViewerPage()
        {
            InitializeComponent();
            if (AppUserRoles.IsCurrentUserAdmin())
            {
                AdminTogglePanel.Visibility = Visibility.Visible;
                LoadSubmittersFromCentralFolder();
                UpdateDbSourceInfo();
            }
            LoadPackages();
        }

        private void LoadPackages()
        {
            try
            {
                if (isAdminMode)
                {
                    var selected = (cbSubmittedBy?.SelectedItem as string) ?? "";
                    if (!string.IsNullOrWhiteSpace(selected) && selected != "All (Master)")
                    {
                        string centralFolder = System.IO.Path.GetDirectoryName(PathManager.CentralMetadataDb);
                        string dbPath = System.IO.Path.Combine(centralFolder, $"{selected}_packages.db");
                        allPackages = SqlitePackageHelper.GetPackagesFromCentralDbFile(dbPath);
                    }
                    else
                    {
                        allPackages = SqlitePackageHelper.GetPackagesFromMasterDb();
                    }
                }
                else
                {
                    allPackages = SqlitePackageHelper.GetAllPackages();
                }

                packageGrid.ItemsSource = allPackages;
                txtResultCount.Text = $"Results: {allPackages.Count}";

                // Update status message
                txtAdminStatus.Text = isAdminMode
                    ? ((cbSubmittedBy?.SelectedItem as string) == "All (Master)" || string.IsNullOrWhiteSpace(cbSubmittedBy?.SelectedItem as string)
                        ? "Viewing: All Packages (MasterDB)"
                        : $"Viewing: {cbSubmittedBy.SelectedItem} (Central)" )
                    : "Viewing: Personal Packages";

                UpdateDbSourceInfo();

                Logger.Info($"Loaded {allPackages.Count} packages. AdminMode={isAdminMode}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load packages.");
                MessageBox.Show("Error loading packages.");
                txtAdminStatus.Text = "Failed to load packages.";
            }
        }

        private void AdminModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            isAdminMode = true;
            SqliteAggregator.RunDailyMerge();
            LoadSubmittersFromCentralFolder();
            UpdateDbSourceInfo();
            LoadPackages();
        }

        private void AdminModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            isAdminMode = false;
            txtMasterInfo.Text = string.Empty;
            LoadPackages();
        }

        private void ApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            var appKey = txtAppKeyIDFilter.Text.Trim().ToLower();
            var appName = txtAppNameFilter.Text.Trim().ToLower();
            string? submittedBy = null;
            if (isAdminMode && cbSubmittedBy?.SelectedItem is string sel && sel != "All (Master)")
                submittedBy = sel.ToLower();

            var filtered = allPackages.Where(p =>
                (string.IsNullOrEmpty(appKey) || (p.AppKeyID?.ToLower().Contains(appKey) ?? false)) &&
                (string.IsNullOrEmpty(appName) || (p.AppName?.ToLower().Contains(appName) ?? false)) &&
                (string.IsNullOrEmpty(submittedBy) || (p.SubmittedBy?.ToLower().Contains(submittedBy) ?? false))
            ).ToList();

            packageGrid.ItemsSource = filtered;
            txtResultCount.Text = $"Results: {filtered.Count}";
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            txtAppKeyIDFilter.Text = "";
            txtAppNameFilter.Text = "";
            if (isAdminMode && cbSubmittedBy != null) cbSubmittedBy.SelectedIndex = 0;
            packageGrid.ItemsSource = allPackages;
            txtResultCount.Text = $"Results: {allPackages.Count}";
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var data = packageGrid.ItemsSource as IEnumerable<PackageInfo>;
            if (data == null || !data.Any()) return;

            var sb = new StringBuilder();
            sb.AppendLine("AppKeyID,AppName,AppVersion,InstallerType,SubmittedBy,SubmittedOn");
            foreach (var p in data)
            {
                sb.AppendLine($"{p.AppKeyID},{p.AppName},{p.AppVersion},{p.InstallerType},{p.SubmittedBy},{p.SubmittedOn:yyyy-MM-dd HH:mm:ss}");
            }

            var fileName = $"packages_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            File.WriteAllText(path, sb.ToString());
            MessageBox.Show($"CSV exported to desktop as {fileName}", "Export Complete");
        }

        private void ViewIni_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PackageInfo selected)
            {
                var scrollViewer = new ScrollViewer { Content = new TextBlock { Text = selected.PackageIniText, TextWrapping = TextWrapping.Wrap }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Width = 800, Height = 600 };
                var win = new Window { Title = $"INI for {selected.AppKeyID}", Content = scrollViewer, Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, SizeToContent = SizeToContent.WidthAndHeight };
                win.ShowDialog();
            }
        }
        private void AppKeyID_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PackageInfo selected)
            {
                string basePath = PathManager.BasePackagePath;
                string productFolder = Path.Combine(basePath, selected.AppName, selected.AppVersion, "Altiris");
                string supportFilesFolder = Path.Combine(productFolder, "1.0", "SupportFiles");
                string toolkitPath = PathManager.ToolkitPath;

                try
                {
                    Directory.CreateDirectory(supportFilesFolder);

                    // Copy Toolkit folder (except 'Files')
                    foreach (string dir in Directory.GetDirectories(toolkitPath, "*", SearchOption.AllDirectories))
                    {
                        if (!dir.Contains("Files"))
                        {
                            string targetDir = dir.Replace(toolkitPath, Path.Combine(productFolder, "1.0"));
                            Directory.CreateDirectory(targetDir);
                            foreach (var file in Directory.GetFiles(dir))
                            {
                                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                                File.Copy(file, destFile, overwrite: true);
                            }
                        }
                    }

                    Logger.Info($"Navigating to IniConsolePage for AppKeyID {selected.AppKeyID}");
                    var iniConsole = new IniConsolePage(productFolder, supportFilesFolder);
                    NavigationService?.Navigate(iniConsole);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to prepare folder structure for INI Console");
                    MessageBox.Show("Error loading INI Console: " + ex.Message);
                }
            }
        }


        private void LoadSubmittersFromCentralFolder()
        {
            try
            {
                if (cbSubmittedBy == null) return;
                var items = new List<string> { "All (Master)" };
                string centralFolder = System.IO.Path.GetDirectoryName(PathManager.CentralMetadataDb);
                if (Directory.Exists(centralFolder))
                {
                    foreach (var f in Directory.GetFiles(centralFolder, "*_packages.db"))
                    {
                        var name = System.IO.Path.GetFileNameWithoutExtension(f);
                        if (name.EndsWith("_packages", StringComparison.OrdinalIgnoreCase))
                            name = name[..^("_packages".Length)];
                        if (!items.Contains(name, StringComparer.OrdinalIgnoreCase))
                            items.Add(name);
                    }
                }
                cbSubmittedBy.ItemsSource = items;
                if (cbSubmittedBy.SelectedIndex < 0) cbSubmittedBy.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load submitters from central folder");
            }
        }

        private void SubmittedBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isAdminMode)
            {
                UpdateDbSourceInfo();
                LoadPackages();
            }
        }

        private void DownloadIni_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PackageInfo selected && !string.IsNullOrWhiteSpace(selected.PackageIniText))
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{selected.AppKeyID}.ini");
                File.WriteAllText(path, selected.PackageIniText);
                MessageBox.Show($"INI saved to desktop as {selected.AppKeyID}.ini", "Download Complete");
                }
            }


        private void UpdateDbSourceInfo()
        {
            try
            {
                if (isAdminMode)
                {
                    var selected = (cbSubmittedBy?.SelectedItem as string) ?? "";
                    if (!string.IsNullOrWhiteSpace(selected) && selected != "All (Master)")
                    {
                        string centralFolder = System.IO.Path.GetDirectoryName(PathManager.CentralMetadataDb);
                        string dbPath = System.IO.Path.Combine(centralFolder, $"{selected}_packages.db");
                        txtMasterInfo.Text = $"Source: {dbPath}";
                        return;
                    }
                }
                var path = SqliteAggregator.GetMasterDbPath();
                var last = SqliteAggregator.GetLastMergedTime();
                txtMasterInfo.Text = $"Master: {path}    Last merged: {(last.HasValue ? last.Value.ToString("yyyy-MM-dd HH:mm") : "never")}";
            }
            catch { txtMasterInfo.Text = string.Empty; }
        }

        private void RefreshMaster_Click(object sender, RoutedEventArgs e)
        {
            SqliteAggregator.RunDailyMerge(true);
            isAdminMode = true;
            if (cbSubmittedBy != null) cbSubmittedBy.SelectedIndex = 0; // reset to Master
            UpdateDbSourceInfo();
            LoadPackages();
        }

    }
}
