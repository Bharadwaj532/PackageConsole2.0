using Microsoft.VisualBasic;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PackageConsole
{
    public partial class AppDeploymentTestPage : Page
    {
        private SystemSnapshot preSnapshot;
        private SystemSnapshot postSnapshot;
        private string lastReportPath;
        public static Action<string, string, Brush> GlobalLogAction;
        private const string PackageArchivePath = @"\\nasv0718.uhc.com\packagingarchive";
        private const string CompletedPackagesPath = @"\\nas00036pn\Cert-Staging\2_Completed Packages";



        public AppDeploymentTestPage()
        {
            InitializeComponent();
            cmbDeployType.SelectedIndex = 0;
            cmbAction.SelectedIndex = 0;
            GlobalLogAction = (type, message, brush) =>
            {
                Dispatcher.Invoke(() =>
                {
                    Paragraph p = new Paragraph();
                    p.Inlines.Add(new Run($"[{DateTime.Now:HH:mm:ss}] [{type}] ") { Foreground = Brushes.Gray });
                    p.Inlines.Add(new Run(message) { Foreground = brush ?? Brushes.White });
                    txtLog.Document.Blocks.Add(p);
                    txtLog.ScrollToEnd();
                });
            };

        }

        private void cmbDeployType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDeployType.SelectedItem is ComboBoxItem selected)
            {
                string deployType = selected.Content.ToString();
                mstPanel.Visibility = deployType == "MSI + MST" ? Visibility.Visible : Visibility.Collapsed;
                psadtkPanel.Visibility = deployType == "PSADTK" ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        private string BrowseForFolder(string initialDir = "")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                InitialDirectory = initialDir
            };

            if (dialog.ShowDialog() == true)
            {
                return Path.GetDirectoryName(dialog.FileName);
            }

            return null;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            string basePath = "";

            if (rbPackageArchive.IsChecked == true)
                basePath = PathManager.ArchivePath;
            else if (rbCompletedPackages.IsChecked == true)
                basePath = PathManager.CompletedPackagesPath;

            string selectedFolder = BrowseForFolder(basePath);
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                txtAppPath.Text = selectedFolder;
            }
        }

        private void QuickPath_Checked(object sender, RoutedEventArgs e)
        {
            if (rbPackageArchive.IsChecked == true)
                LogMessage("INFO", "📁 Selected Root: Package Archive");
            else if (rbCompletedPackages.IsChecked == true)
                LogMessage("INFO", "📁 Selected Root: Completed Packages");
        }

        private void btnBrowseMst_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select MST File",
                Filter = "MST Files (*.mst)|*.mst",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                txtMstPath.Text = dialog.FileName;
            }
        }

        private void btnBrowsePsadtk_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select any file inside your PSADTK folder",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                txtPsadtkPath.Text = folderPath;
            }
        }

        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            progressBar.Visibility = Visibility.Visible;
            LogMessage("INFO", "Starting deployment...");

            bool result = await Task.Run(() => RunDeploymentSafe());

            progressBar.Visibility = Visibility.Collapsed;

            LogMessage(result ? "SUCCESS" : "FAILURE",
                result ? "Deployment completed successfully." : "Deployment failed.",
                result ? Brushes.LawnGreen : Brushes.Red);
        }
        private bool RunDeploymentSafe()
        {
            try
            {
                string appPath = DispatcherInvoke(() => txtAppPath.Text.Trim());
                string mstPath = DispatcherInvoke(() => txtMstPath.Text.Trim());
                string psadtkPath = DispatcherInvoke(() => txtPsadtkPath.Text.Trim());
                string deployType = DispatcherInvoke(() => ((ComboBoxItem)cmbDeployType.SelectedItem).Content.ToString());
                string action = DispatcherInvoke(() => ((ComboBoxItem)cmbAction.SelectedItem).Content.ToString());

                if (!Directory.Exists(appPath))
                {
                    LogMessage("ERROR", "Invalid application folder path.", Brushes.Red);
                    return false;
                }

                // Determine copy destination path
                string destinationFolder = "";
                if (deployType == "MSI" || deployType == "MSI + MST")
                {
                    var msiPath = Directory.GetFiles(appPath, "*.msi").FirstOrDefault();
                    if (msiPath == null)
                    {
                        LogMessage("ERROR", "MSI file not found.", Brushes.Red);
                        return false;
                    }

                    var versionInfo = FileVersionInfo.GetVersionInfo(msiPath);
                    string appName = Path.GetFileNameWithoutExtension(msiPath);
                    string appVersion = versionInfo.FileVersion ?? "1.0.0.0";
                    destinationFolder = Path.Combine(PathManager.AppTestingRoot, $"{appName}_{appVersion}");
                }
                else if (deployType == "PSADTK")
                {
                    string iniPath = Path.Combine(psadtkPath, "SupportFiles", "Package.ini");
                    if (!File.Exists(iniPath))
                    {
                        LogMessage("ERROR", "Package.ini not found in SupportFiles folder.", Brushes.Red);
                        return false;
                    }

                    var ini = File.ReadAllLines(iniPath);
                    string appName = ini.FirstOrDefault(l => l.StartsWith("APPNAME=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1]?.Trim();
                    string appVer = ini.FirstOrDefault(l => l.StartsWith("APPVER=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1]?.Trim();

                    if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(appVer))
                    {
                        LogMessage("ERROR", "APPNAME or APPVER missing in Package.ini", Brushes.Red);
                        return false;
                    }

                    destinationFolder = Path.Combine(PathManager.AppTestingRoot, $"{appName}_{appVer}");
                }

                // Copy folder
                if (Directory.Exists(destinationFolder)) Directory.Delete(destinationFolder, true);
                CopyDirectory(appPath, destinationFolder);
                LogMessage("INFO", $"Copied files to: {destinationFolder}", Brushes.LightSkyBlue);
                Thread.Sleep(3000); // 🟡 Give OS time to flush disk operations (~3 seconds)

                // Build command
                string command = deployType switch
                {
                    "MSI" => GetMsiCommand(destinationFolder, action),
                    "MSI + MST" => GetMsiMstCommand(destinationFolder, mstPath),
                    "PSADTK" => GetPsadtkCommand(destinationFolder, action),
                    _ => throw new Exception("Unsupported deployment type.")
                };

                LogMessage("CMD", command, Brushes.Orange);
                LogMessage("SNAPSHOT", "Capturing pre-deployment system snapshot...", Brushes.LightBlue);
                var before = SystemScanner.Capture();
                SystemScanner.SaveSnapshot(before, "pre");
                LogMessage("SNAPSHOT", "Pre-deployment snapshot captured.");

                // Create and run task
                string taskName = $"TestDeploy_{action}";
                LogMessage("INFO", $"Task Name {taskName}", Brushes.Red);

                 //bool created = CreateAndRunTask(taskName, command);
                bool created = CreateAndRunTask_XmlBased(taskName, command);

                if (created)
                {
                    LogMessage("INFO", $"Task '{taskName}' created. Monitoring...");
                    bool result = MonitorTaskCompletion(taskName);

                    LogMessage("SNAPSHOT", "Capturing post-deployment system snapshot...", Brushes.LightBlue);
                    var after = SystemScanner.Capture();
                    SystemScanner.SaveSnapshot(after, "post");

                    lastReportPath = SystemScanner.GenerateHtmlReport(before, after);
                    LogMessage("INFO", $" Report saved at: {lastReportPath}", Brushes.LightSkyBlue);
                   // string path = @"C:\Temp\AppTesting\Reports\InstalledAppsList.tsv";
                   // SystemScanner.ExportInstalledAppsToTsv(path);

                    return result;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogMessage("ERROR", $"Exception: {ex.Message}", Brushes.Red);
                return false;
            }
        }
        private bool CreateAndRunTask_XmlBased(string taskName, string command)
        {
            try
            {
                // Create path to write XML
                string taskXmlPath = Path.Combine(Path.GetTempPath(), "TaskXml");
                Directory.CreateDirectory(taskXmlPath);

                string xmlFilePath = Path.Combine(taskXmlPath, $"{taskName}.xml");

                // Delete task if already exists
                Process.Start(new ProcessStartInfo("schtasks.exe", $"/Delete /TN \"{taskName}\" /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit();

                // Also clean old XML
                if (File.Exists(xmlFilePath))
                {
                    File.Delete(xmlFilePath);
                }

                // Prepare command arguments and encode for XML
                string encodedCmd = System.Security.SecurityElement.Escape(command);

                // Use start boundary in the past to run immediately
                string nowUtc = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-ddTHH:mm:ss");

                string xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>OptumPackageStudio</Author>
  </RegistrationInfo>
  <Triggers>
    <TimeTrigger>
      <StartBoundary>{DateTime.Now.AddMinutes(1):yyyy-MM-ddTHH:mm:ss}</StartBoundary>
      <Enabled>true</Enabled>
    </TimeTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>SYSTEM</UserId> <!-- SYSTEM account SID -->
      <LogonType>InteractiveToken</LogonType> <!-- Avoid using ServiceAccount -->
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>cmd.exe</Command>
      <Arguments>/c {encodedCmd}</Arguments>
    </Exec>
  </Actions>
</Task>";

                // Save the XML
                File.WriteAllText(xmlFilePath, xmlContent, Encoding.Unicode);
                LogMessage("XMLTASK", $"✔️ XML created for task registration.");

                // Register the task
                string createCmd = $"/Create /TN \"{taskName}\" /XML \"{xmlFilePath}\"";
                LogMessage("DEBUG", $"schtasks.exe {createCmd}", Brushes.Gold);

                ProcessStartInfo createInfo = new ProcessStartInfo("schtasks.exe", createCmd)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(createInfo))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    LogMessage("SCHED", "Output: " + output, Brushes.Cyan);
                    if (!string.IsNullOrWhiteSpace(error))
                        LogMessage("ERROR", error, Brushes.OrangeRed);

                    if (proc.ExitCode != 0)
                    {
                        LogMessage("ERROR", $"❌ Failed to create task. ExitCode: {proc.ExitCode}", Brushes.Red);
                        LogMessage("ERROR", $"XML Path used: {xmlFilePath}", Brushes.OrangeRed);
                        return false;
                    }
                }
                // Trigger it manually
                string runArgs = $"/Run /TN \"{taskName}\"";
                var runProc = Process.Start(new ProcessStartInfo("schtasks.exe", runArgs)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                string runOut = runProc.StandardOutput.ReadToEnd();
                runProc.WaitForExit();

                LogMessage("RUN", runOut, Brushes.LightGreen);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage("ERROR", $"Task setup failed: {ex.Message}", Brushes.Red);
                return false;
            }
        }
        private string GetMsiCommand(string folder, string action)
        {
            var msi = Directory.GetFiles(folder, "*.msi").FirstOrDefault();
            if (msi == null) throw new FileNotFoundException("MSI not found.");
            return action == "Install"
                ? $"msiexec.exe /i \"{msi}\" /qn"
                : $"msiexec.exe /x \"{msi}\" /qn";
        }
        private string GetMsiMstCommand(string folder, string mstPath)
        {
            var msi = Directory.GetFiles(folder, "*.msi").FirstOrDefault();
            if (msi == null) throw new FileNotFoundException("MSI not found.");
            if (!File.Exists(mstPath)) throw new FileNotFoundException("MST not found.");
            return $"msiexec.exe /i \"{msi}\" TRANSFORMS=\"{mstPath}\" /qn";
        }
        private string GetPsadtkCommand(string folderPath, string action)
        {
            //var exe = Directory.GetFiles(folderPath, "*.exe").FirstOrDefault();
            var exe = Directory.GetFiles(folderPath, "*.exe")
                       .FirstOrDefault(f => !Path.GetFileName(f).Equals("ServiceUI.exe", StringComparison.OrdinalIgnoreCase));
            var ps1 = Directory.GetFiles(folderPath, "*.ps1").FirstOrDefault();

            if (exe == null || ps1 == null)
                throw new FileNotFoundException("Missing either .exe or .ps1 required for PSADTK deployment.");

            string mode = action.Equals("Install", StringComparison.OrdinalIgnoreCase) ? "Install" : "Uninstall";
            return $"\"{exe}\" \"{ps1}\" {mode}";
        }
        private bool CreateAndRunTask(string taskName, string command)
        {
            try
            {
                // Define XML path
                string xmlPath = Path.Combine(Path.GetTempPath(), $"{taskName}_Task.xml");

                // Delete if XML already exists (❗️Critical)
                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                }

                // Construct the XML task definition
                string taskXml = $@"
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Triggers>
    <RegistrationTrigger>
      <Enabled>true</Enabled>
    </RegistrationTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <RunLevel>HighestAvailable</RunLevel>
      <UserId>S-1-5-18</UserId>
      <LogonType>ServiceAccount</LogonType>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>cmd.exe</Command>
      <Arguments>/c {System.Security.SecurityElement.Escape(command)}</Arguments>
    </Exec>
  </Actions>
</Task>";

                File.WriteAllText(xmlPath, taskXml);

                // Delete task if already exists
                Process.Start(new ProcessStartInfo("schtasks.exe", $"/Delete /TN \"{taskName}\" /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit();

                // Register new task from XML
                string createArgs = $"/Create /TN \"{taskName}\" /XML \"{xmlPath}\" /RU SYSTEM";
                ProcessStartInfo registerInfo = new ProcessStartInfo("schtasks.exe", createArgs)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var regProc = Process.Start(registerInfo))
                {
                    string regOutput = regProc.StandardOutput.ReadToEnd();
                    regProc.WaitForExit();

                    LogMessage("XMLTASK", regOutput, Brushes.LightBlue);

                    if (regProc.ExitCode != 0)
                    {
                        LogMessage("ERROR", "Failed to register XML-based task.", Brushes.Red);
                        return false;
                    }
                }

                // Manually run the task
                string runCmd = $"/Run /TN \"{taskName}\"";
                ProcessStartInfo runInfo = new ProcessStartInfo("schtasks.exe", runCmd)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var runProc = Process.Start(runInfo);
                string runOutput = runProc.StandardOutput.ReadToEnd();
                runProc.WaitForExit();

                LogMessage("RUN", runOutput, Brushes.LightGreen);

                return runProc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                LogMessage("ERROR", $"Task creation error: {ex.Message}", Brushes.Red);
                return false;
            }
        }
        private bool MonitorTaskCompletion(string taskName)
        {
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(3000);

                    string cmd = $"/Query /TN \"{taskName}\" /FO LIST";
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", cmd)
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    if (!output.Contains("Running"))
                    {
                        LogMessage("MONITOR", "Task finished.", Brushes.LightGray);
                        string exitCode = GetTaskResult(taskName);
                        LogMessage("RESULT", $"Exit Code: {exitCode}", Brushes.Orange);
                        return exitCode == "0";
                    }
                }

                LogMessage("ERROR", "Task monitor timed out.", Brushes.Red);
                return false;
            }
            catch (Exception ex)
            {
                LogMessage("ERROR", $"Monitor failed: {ex.Message}", Brushes.Red);
                return false;
            }
        }
        private string GetTaskResult(string taskName)
        {
            try
            {
                string cmd = $"/Query /TN \"{taskName}\" /V /FO LIST";
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", cmd)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                var match = System.Text.RegularExpressions.Regex.Match(output, @"Last Result:\s*(\d+)");
                return match.Success ? match.Groups[1].Value : "Unknown";
            }
            catch
            {
                return "Error";
            }
        }
        private T DispatcherInvoke<T>(Func<T> func) =>
            Dispatcher.Invoke(func);
        private void LogMessage(string type, string message, Brush color = null)
        {
            Dispatcher.Invoke(() =>
            {
                Paragraph p = new Paragraph();
                p.Inlines.Add(new Run($"[{DateTime.Now:HH:mm:ss}] [{type}] ") { Foreground = Brushes.Gray });
                p.Inlines.Add(new Run(message) { Foreground = color ?? Brushes.White });
                txtLog.Document.Blocks.Add(p);
                txtLog.ScrollToEnd();
            });
        }
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destPath = Path.Combine(targetDir, fileName);
                File.Copy(file, destPath, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(targetDir, subDirName);
                CopyDirectory(subDir, destSubDir);
            }
        }
        private void btnViewReport_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(lastReportPath) && File.Exists(lastReportPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = lastReportPath,
                    UseShellExecute = true // Launches with default browser
                });
            }
            else
            {
                LogMessage("ERROR", "No report found to open.", Brushes.Red);
            }
        }
        private void btnGenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(lastReportPath) && File.Exists(lastReportPath))
            {
                Process.Start(new ProcessStartInfo(lastReportPath) { UseShellExecute = true });
            }
            else
            {
                LogMessage("ERROR", "No report available to view.", Brushes.OrangeRed);
            }
        }
        private void btnExportTsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                string path = System.IO.Path.Combine(PathManager.ScannerReportPath, "InstalledAppsList.tsv");
                SystemScanner.ExportInstalledAppsToTsv(path);
                LogMessage("SUCCESS", $"TSV exported: {path}", Brushes.Green);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogMessage("ERROR", $"TSV export failed: {ex.Message}", Brushes.Red);
            }
        }

    }
}
