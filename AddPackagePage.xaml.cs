using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using NLog;
using PackageConsole.Data;
using PackageConsole.Models;

namespace PackageConsole
{
    public partial class AddPackagePage : Page
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(AddPackagePage));
        private string selectedRebootOption = "No";
        private string selectedIniFilePath = null;

        public AddPackagePage()
        {
            InitializeComponent();
            Logger.Info("AddPackagePage loaded.");

            upgradeYesRadioButton.Checked += UpgradeYesRadioButton_Checked;
            upgradeNoRadioButton.Checked += UpgradeNoRadioButton_Checked;

            rebootSlider.Value = 3;
            rebootSlider.ValueChanged += RebootSlider_ValueChanged;
        }

        private void UpgradeYesRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            uploadINIButton.Visibility = Visibility.Visible;
            Logger.Info("Upgrade: Yes selected. Upload INI button shown.");
        }

        private void UpgradeNoRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            uploadINIButton.Visibility = Visibility.Collapsed;
            Logger.Info("Upgrade: No selected. Upload INI button hidden.");
        }

        private void RebootSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var labels = new[] { "Install", "Uninstall", "Always", "No" };
            int index = (int)rebootSlider.Value;
            selectedRebootOption = labels[Math.Clamp(index, 0, labels.Length - 1)];
            Logger.Info($"Reboot option set to: {selectedRebootOption}");
        }
        private string GetMsiProperty(dynamic database, string property)
        {
            var view = database.OpenView($"SELECT `Value` FROM `Property` WHERE `Property` = '{property}'");
            view.Execute();
            var record = view.Fetch();
            return record?.StringData(1);
        }
        private void UploadInstallerButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Installer Files (*.msi;*.exe)|*.msi;*.exe",
                Title = "Select an Installer File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string fullFilePath = openFileDialog.FileName;
                string fileExtension = System.IO.Path.GetExtension(fullFilePath).ToLower();

                fileNameTextBox.Text = System.IO.Path.GetFileName(fullFilePath);
                filePathTextBox.Text = System.IO.Path.GetDirectoryName(fullFilePath);

                try
                {
                    if (fileExtension == ".msi")
                    {
                        var installerType = Type.GetTypeFromProgID("WindowsInstaller.Installer");
                        dynamic installer = Activator.CreateInstance(installerType);
                        var database = installer.OpenDatabase(fullFilePath, 0);

                        fileVersionTextBox.Text = GetMsiProperty(database, "ProductVersion");
                        productNameTextBox.Text = GetMsiProperty(database, "ProductName");
                        vendorTextBox.Text = GetMsiProperty(database, "Manufacturer");
                        productCodeTextBox.Text = GetMsiProperty(database, "ProductCode");


                        Logger.Info("MSI metadata auto-filled successfully.");
                    }
                    else if (fileExtension == ".exe")
                    {
                        var fileInfo = FileVersionInfo.GetVersionInfo(fullFilePath);

                        productNameTextBox.Text = fileInfo.ProductName;
                        fileVersionTextBox.Text = fileInfo.ProductVersion;
                        vendorTextBox.Text = fileInfo.CompanyName;
                        productCodeTextBox.Text = fileInfo.ProductVersion; // fallback

                        Logger.Info("EXE metadata auto-filled successfully.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to extract installer metadata.");
                    MessageBox.Show("Failed to extract installer details. Please fill fields manually.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        private void UploadINIFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "INI Files (*.ini)|*.ini",
                Title = "Select an INI File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedIniFilePath = openFileDialog.FileName;
                Logger.Info($"INI file uploaded: {selectedIniFilePath}");
                MessageBox.Show($"INI file uploaded successfully from: {selectedIniFilePath}");
            }
        }
        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Submit button clicked.");

            string basePath = PathManager.BasePackagePath;
            string productName = productNameTextBox.Text.Trim();
            string productVersion = fileVersionTextBox.Text.Trim();
            string filePath = filePathTextBox.Text.Trim();
            string fileName = fileNameTextBox.Text.Trim();
            string vendor = vendorTextBox.Text.Trim();
            string productCode = productCodeTextBox.Text.Trim();
            string appKeyID = appKeyIDTextBox.Text.Trim();
            string drmBuild = drmBuildTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(productVersion))
            {
                MessageBox.Show("Product Name or Version cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Warn("Product Name or Version missing during submission.");
                return;
            }

            string productFolder = System.IO.Path.Combine(basePath, productName, productVersion, "Altiris", drmBuild);
            string supportFilesFolder = System.IO.Path.Combine(productFolder, "SupportFiles");
            string filesFolder = System.IO.Path.Combine(productFolder, "Files");
            string toolkitPath = PathManager.ToolkitPath;

            try
            {
                Directory.CreateDirectory(productFolder);
                Directory.CreateDirectory(supportFilesFolder);
                Directory.CreateDirectory(filesFolder);
                //CopyDirectory(toolkitPath, productFolder);
                if (Directory.Exists(toolkitPath))
                {
                    CopyDirectory(toolkitPath, productFolder);
                }

                if (!string.IsNullOrWhiteSpace(filePath) && Directory.Exists(filePath))
                {
                    CopyDirectory(filePath, filesFolder);
                }

                string iniFilePath = System.IO.Path.Combine(supportFilesFolder, "tmpPackage.ini");
                using (var writer = new StreamWriter(iniFilePath))
                {
                    writer.WriteLine("[PRODUCT INFO]");
                    writer.WriteLine($"APPVENDOR={vendor}");
                    writer.WriteLine($"APPNAME={productName}");
                    writer.WriteLine($"APPVER={productVersion}");
                    writer.WriteLine($"APPKEYID={appKeyID}");
                    writer.WriteLine($"DRMBUILD={drmBuild}");
                    writer.WriteLine($"APPGUID={productCode}");
                    writer.WriteLine($"REBOOTER={selectedRebootOption}");
                    writer.WriteLine();

                    //string fileName = System.IO.Path.GetFileName(fileNameTextBox.Text);
                    if (System.IO.Path.GetExtension(fileName).Equals(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteLine("[INSTALL1]");
                        writer.WriteLine("TYPE=MSI");
                        writer.WriteLine("SUBTYPE=");
                        writer.WriteLine($"NAME={productName}");
                        writer.WriteLine($"VER={productVersion}");
                        writer.WriteLine($"GUID={productCode}");
                        writer.WriteLine($"MSI={fileName}");
                        writer.WriteLine();

                        writer.WriteLine("[UNINSTALL1]");
                        writer.WriteLine("TYPE=MSI");
                        writer.WriteLine("SUBTYPE=");
                        writer.WriteLine($"NAME={productName}");
                        writer.WriteLine($"VER={productVersion}");
                        writer.WriteLine($"GUID={productCode}");
                        writer.WriteLine();
                    }
                    else if (System.IO.Path.GetExtension(fileName).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteLine("[INSTALL1]");
                        writer.WriteLine("TYPE=EXE");
                        writer.WriteLine("SUBTYPE=GENERIC");
                        writer.WriteLine($"NAME={productName}");
                        writer.WriteLine($"VER={productVersion}");
                        writer.WriteLine($"GUID={productCode}");
                        writer.WriteLine($"EXE={fileName}");
                        writer.WriteLine();

                        writer.WriteLine("[UNINSTALL1]");
                        writer.WriteLine("TYPE=EXE");
                        writer.WriteLine("SUBTYPE=INNO");
                        writer.WriteLine($"NAME={productName}");
                        writer.WriteLine($"VER={productVersion}");
                        writer.WriteLine($"GUID={productCode}");
                        writer.WriteLine();
                    }

                    if (!string.IsNullOrWhiteSpace(selectedIniFilePath) && File.Exists(selectedIniFilePath))
                    {
                        Logger.Info("Merging [UPGRADEx] and [UNINSTALLx] sections into sequential [UPGRADEx] sections, skipping sections with TYPE=MSI\\MSP\\EXE...");

                        var lines = File.ReadAllLines(selectedIniFilePath);
                        var mergedSections = new List<List<string>>();
                        List<string> currentSectionLines = null;
                        bool isRelevantSection = false;

                        foreach (string line in lines)
                        {
                            if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase))
                            {
                                // Section header encountered
                                if (line.StartsWith("[UPGRADE", StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith("[UNINSTALL", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Start capturing a relevant section; do NOT include the header line itself
                                    currentSectionLines = new List<string>();
                                    mergedSections.Add(currentSectionLines);
                                    isRelevantSection = true;
                                    continue; // skip adding this header line
                                }
                                // Any other section ends our capture
                                isRelevantSection = false;
                                currentSectionLines = null;
                                continue;
                            }

                            if (isRelevantSection && currentSectionLines != null)
                            {
                                var trimmed = line?.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmed))
                                {
                                    currentSectionLines.Add(trimmed);
                                }
                            }
                        }

                        // Write merged sections as [UPGRADE1], [UPGRADE2], ... only if TYPE is not MSI\MSP\EXE
                        int upgradeIndex = 1;
                        foreach (var section in mergedSections)
                        {
                            bool hasInvalidType = section.Any(line =>
                                line.TrimStart().StartsWith("TYPE=", StringComparison.OrdinalIgnoreCase) &&
                                line.Trim().Equals("TYPE=MSI\\MSP\\EXE", StringComparison.OrdinalIgnoreCase));

                            if (!hasInvalidType)
                            {
                                writer.WriteLine();
                                writer.WriteLine($"[UPGRADE{upgradeIndex++}]");
                                foreach (var l in section)
                                {
                                    // Defensive: skip any stray headers inside captured content
                                    if (!l.TrimStart().StartsWith("["))
                                        writer.WriteLine(l);
                                }
                            }
                        }

                        // Show an import status message on the page
                        int imported = upgradeIndex - 1;
                        txtUpgradeImportStatus.Visibility = Visibility.Visible;
                        txtUpgradeImportStatus.Text = imported > 0
                            ? $"Imported {imported} section(s): UPGRADE1..UPGRADE{imported}"
                            : "No UPGRADE sections imported from uploaded file.";
                    }

                }

                Logger.Info($"Package created at: {productFolder}");
                MessageBox.Show($"Package setup completed successfully at: {productFolder}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // ✅ Save metadata to SQLite
                var metadata = new PackageInfo
                {
                    AppKeyID = appKeyID,
                    AppName = productName,
                    AppVersion = productVersion,
                    ProductCode = productCode,
                    Vendor = vendor,
                    DRMBuild = drmBuild,
                    RebootOption = selectedRebootOption,
                    InstallerType = System.IO.Path.GetExtension(fileName).ToLower() == ".msi" ? "MSI" : "EXE",
                    InstallerFile = fileName,
                    SubmittedBy = Environment.UserName,
                    SubmittedOn = DateTime.Now,
                    PackageIniText = File.ReadAllText(iniFilePath)
                };

                SqlitePackageHelper.UpsertPackageMetadata(metadata);

                Logger.Info("Navigating to IniConsolePage after package creation.");
                var consolePage = new IniConsolePage(productFolder, supportFilesFolder);
                // Navigate via app-level NavigationService (MVVM shell)
                PackageConsole.Services.NavigationService.Instance.Navigate(consolePage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during package creation.");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Clear button clicked.");
            RequestIDTextBox.Text = string.Empty;
            filePathTextBox.Text = string.Empty;
            fileNameTextBox.Text = string.Empty;
            fileVersionTextBox.Text = string.Empty;
            productNameTextBox.Text = string.Empty;
            vendorTextBox.Text = string.Empty;
            productCodeTextBox.Text = string.Empty;
            appKeyIDTextBox.Text = string.Empty;
            drmBuildTextBox.Text = string.Empty;
            rebootSlider.Value = 3;
            upgradeNoRadioButton.IsChecked = true;
        }
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFilePath = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file));
                File.Copy(file, destFilePath, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}

