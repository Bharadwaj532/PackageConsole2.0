using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using NLog;

namespace PackageConsole
{
    public partial class CopyPackage : Page
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(CopyPackage));

        // Centralized path usage
        private readonly string DefaultSourceRoot = PathManager.BasePackagePath;
        private readonly string DefaultArchiveRoot = PathManager.ArchivePath;
        private readonly string DefaultCompletedRoot = PathManager.CompletedPackagesPath;
        
        public CopyPackage()
        {
            InitializeComponent();
            Logger.Info("CopyPackage page initialized.");
        }

        // Browse Local Path and read Package.INI file
        private void BrowseSourcePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Source Application Path",
                Filter = "INI Files (*.ini)|*.ini|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                SourcePathTextBox.Text = dialog.FileName;
                Logger.Info($"INI file selected: {dialog.FileName}");

                PopulateDefaultPaths();
                // Read PRODUCT INFO Section
                var productInfo = ReadIniFile(dialog.FileName, "PRODUCT INFO");
                if (productInfo != null)
                {
                    string appVendor = productInfo["APPVENDOR"];
                    string appName = productInfo["APPNAME"];
                    string appVersion = productInfo["APPVER"];
                    string drmBuild = productInfo["DRMBUILD"];

                    Logger.Info($"Extracted PRODUCT INFO: Vendor={appVendor}, Name={appName}, Version={appVersion}, DRM={drmBuild}");

                    ProductInfoTextBlock.Text = $"APPVENDOR: {appVendor}, APPNAME: {appName}, APPVER: {appVersion}, DRMBUILD: {drmBuild}";

                    // Set up and validate paths
                    SetupFolderStructure(appVendor, appName, appVersion, drmBuild, Path.GetDirectoryName(dialog.FileName));
                }
                else
                {
                    Logger.Warn("PRODUCT INFO section not found in the selected INI.");
                    MessageBox.Show("PRODUCT INFO section not found in Package.INI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void PopulateDefaultPaths()
        {
            string sourceFilePath = SourcePathTextBox.Text;

            // Validate file path
            if (!File.Exists(sourceFilePath))
            {
                Logger.Error($"Invalid source file path: {sourceFilePath}");
                MessageBox.Show("Invalid Source Application Path. Please select a valid Package.INI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Read PRODUCT INFO from INI file
            var productInfo = ReadIniFile(sourceFilePath, "PRODUCT INFO");
            if (productInfo == null)
            {
                Logger.Warn("PRODUCT INFO section missing during default path population.");
                MessageBox.Show("PRODUCT INFO section is missing in Package.INI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string expectedFolderStructure = Path.Combine(
                productInfo["APPVENDOR"] ?? "Vendor_Unknown",
                productInfo["APPNAME"] ?? "App_Unknown",
                productInfo["APPVER"] ?? "Version_Unknown",
                "Altiris",
                productInfo["DRMBUILD"] ?? "DRM_Unknown"
            );

            ArchiveFolderLocationLabel.Text = "Archive Folder Location: " + DefaultArchiveRoot;
            CompletedPackageLocationLabel.Text = "Completed Package Location: " + DefaultCompletedRoot;

            ArchiveFolderTextBox.Text = Path.Combine(DefaultArchiveRoot, expectedFolderStructure);
            CompletedPackageTextBox.Text = Path.Combine(DefaultCompletedRoot, expectedFolderStructure);
        }
        // Validate Source Location
        //private void ValidateSource_Click(object sender, RoutedEventArgs e)
        //{
        //    string sourcePath = SourceLocationTextBox.Text;

        //    if (Directory.Exists(sourcePath))
        //    {
        //        MessageBox.Show("Source Location is valid.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
        //    }
        //    else
        //    {
        //        MessageBox.Show("Source Location does not exist. Please enter a valid path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        private void SetupFolderStructure(string appVendor, string appName, string appVersion, string drmBuild, string sourcePath)
        {
            try
            {
                string sourceFinalPath = Path.Combine(DefaultSourceRoot, appVendor, appName, appVersion, "Source");
                string archiveFinalPath = PathManager.BuildArchivePath(appVendor, appName, appVersion, drmBuild);
                string completedFinalPath = PathManager.BuildCompletedPath(appVendor, appName, appVersion, drmBuild);

               // EnsureDirectoryExists(sourceFinalPath);
               // EnsureDirectoryExists(archiveFinalPath);
               // EnsureDirectoryExists(completedFinalPath);

                //CopyDirectory(sourcePath, sourceFinalPath);
                //CopyDirectory(sourcePath, archiveFinalPath);
                //CopyDirectory(sourcePath, completedFinalPath);

                Logger.Info($"Folders created and content copied for {appName} {appVersion}.");
                MessageBox.Show("Folders created and content copied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during folder structure setup.");
                MessageBox.Show($"Failed to setup folder structure: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Ensure directory exists; create if it does not
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Logger.Info($"Created directory: {path}");
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            try
            {
                foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                    Directory.CreateDirectory(dirPath.Replace(sourceDir, destinationDir));

                foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
                {
                    string targetFilePath = filePath.Replace(sourceDir, destinationDir);
                    File.Copy(filePath, targetFilePath, true);
                }

                Logger.Info($"Copied content from {sourceDir} to {destinationDir}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to copy from {sourceDir} to {destinationDir}");
                throw;
            }
        }

        // Helper: Read INI file and extract section
        private Dictionary<string, string> ReadIniFile(string filePath, string section)
        {
            try
            {
                var result = new Dictionary<string, string>();
                string[] lines = File.ReadAllLines(filePath);
                bool isSection = false;

                foreach (string line in lines)
                {
                    if (line.Trim().Equals($"[{section}]"))
                    {
                        isSection = true;
                        continue;
                    }

                    if (isSection)
                    {
                        if (line.StartsWith("[")) break;

                        var keyValue = line.Split(new[] { '=' }, 2);
                        if (keyValue.Length == 2)
                        {
                            result[keyValue[0].Trim()] = keyValue[1].Trim();
                        }
                    }
                }

                return result.Count > 0 ? result : null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error reading INI file: {filePath}");
                return null;
            }
        }

        private void CreateArchive_Click(object sender, RoutedEventArgs e)
        {
            string sourceFilePath = SourcePathTextBox.Text;

            // Read PRODUCT INFO values
            var productInfo = ReadIniFile(sourceFilePath, "PRODUCT INFO");
            if (productInfo == null)
            {
                Logger.Warn("PRODUCT INFO not found when creating archive.");
                MessageBox.Show("PRODUCT INFO section is missing in Package.INI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetArchivePath = PathManager.BuildArchivePath(
                productInfo["APPVENDOR"],
                productInfo["APPNAME"],
                productInfo["APPVER"],
                productInfo["DRMBUILD"]
            );

            // Create directories and copy content
            CreateAndCopyContent(sourceFilePath, targetArchivePath);
            MessageBox.Show($"Package Archive created at: {targetArchivePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void FinalizePackage_Click(object sender, RoutedEventArgs e)
        {
            string sourceFilePath = SourcePathTextBox.Text;

            // Read PRODUCT INFO values
            var productInfo = ReadIniFile(sourceFilePath, "PRODUCT INFO");
            if (productInfo == null)
            {
                Logger.Warn("PRODUCT INFO missing for finalize package.");
                MessageBox.Show("PRODUCT INFO section is missing in Package.INI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetCompletedPath = PathManager.BuildCompletedPath(
                productInfo["APPVENDOR"],
                productInfo["APPNAME"],
                productInfo["APPVER"],
                productInfo["DRMBUILD"]
            );

            // Create directories and copy content
            CreateAndCopyContent(sourceFilePath, targetCompletedPath);
            MessageBox.Show($"Package Finalized at: {targetCompletedPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void CreateAndCopyContent(string sourceFilePath, string targetPath)
        {
            try
            {
                string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
                string sourceParentDirectory = Directory.GetParent(sourceDirectory)?.Parent?.FullName;

                if (sourceParentDirectory == null || !Directory.Exists(sourceParentDirectory))
                {
                    Logger.Error("Invalid source directory structure.");
                    MessageBox.Show("Invalid source directory structure. Cannot determine parent folders.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                EnsureDirectoryExists(targetPath);
                CopyDirectory(sourceParentDirectory, targetPath);
                Logger.Info($"Copied final package content to: {targetPath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to finalize package.");
                MessageBox.Show($"Failed to finalize package: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
