using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.Threading.Tasks;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Media;

namespace PackageConsole
{
    public partial class Testing : Page
    {
		private CancellationTokenSource _loadingTextCts;
        public string DefaultDeviceName { get; set; } = Environment.MachineName; // Default to current machine name
        //public object CircularProgressBar { get; private set; }
       // public object ProgressText { get; private set; }
        //public object LinearProgressText { get; private set; }

        private CancellationTokenSource _LoadingTextCts = new CancellationTokenSource();

        private string installCommand = string.Empty;
        private string uninstallCommand = string.Empty;
        private string silentinstallCommand = string.Empty;
        private string silentuninstallCommand = string.Empty;
        private readonly string logFolder = "C:\\Logs";

        public Testing()
        {
            InitializeComponent();
            // Set the default mode to "Same Device" programmatically
            DeviceNameTextBox.Text = DefaultDeviceName;
            DeviceNameTextBox.IsEnabled = false;
            // Populate ComboBoxes with parameter options
            //InstallParametersComboBox.ItemsSource = new string[] { "Install", "Install Silent"};
            //UninstallParametersComboBox.ItemsSource = new string[] { "Uninstall", "Uninstall Silent" };

        }

        private void TestMode_Checked(object sender, RoutedEventArgs e)
        {
            if (DeviceNameTextBox == null)
            {
                // UI element is not initialized yet, exit early
                return;
            }

            if ((sender as RadioButton)?.Content.ToString() == "Same Device")
            {
                DeviceNameTextBox.Text = DefaultDeviceName;
                DeviceNameTextBox.IsEnabled = false;
            }
            else
            {
                DeviceNameTextBox.Text = string.Empty;
                DeviceNameTextBox.IsEnabled = true;
            }
        }

        // Event handler for Ping Device button
        private void PingDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            string deviceName = DeviceNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                StatusTextBlock.Text = "Status: Device name cannot be empty.";
                return;
            }

            bool isPingSuccessful = PingDevice(deviceName);

            if (isPingSuccessful)
            {
                StatusTextBlock.Text = $"Status: Ping to {deviceName} successful.";
            }
            else
            {
                StatusTextBlock.Text = $"Status: Ping to {deviceName} failed.";
            }
        }

        private bool PingDevice(string deviceName)
        {
            try
            {
                var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(deviceName);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a folder",
                Filter = "Folders|*.", // Dummy filter to allow folder selection
                CheckFileExists = false, // Allow selection of non-existing paths
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedFolderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrWhiteSpace(selectedFolderPath))
                {
                    PackageFolderTextBox.Text = selectedFolderPath;
                    DeterminePackageType(selectedFolderPath);
                }
                else
                {
                    MessageBox.Show("Invalid folder selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeterminePackageType(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath);

                bool hasPSADTKPackage = files.Any(file => file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) &&
                                        files.Any(file => file.EndsWith(".exe.config", StringComparison.OrdinalIgnoreCase)) &&
                                        files.Any(file => file.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
                bool hasMSI = files.Any(file => file.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
                bool hasMST = files.Any(file => file.EndsWith(".mst", StringComparison.OrdinalIgnoreCase));

                if (hasPSADTKPackage)
                {
                    StatusTextBlock.Text = "Status: PSADTK package detected.";
                    // Call the method to test PSADTK package
                    SetInstallCommands(folderPath, "PSADTK");
                }
                else if (hasMSI && hasMST)
                {
                    StatusTextBlock.Text = "Status: MSI with MST files detected.";
                    // Call the method to test MSI with MST files
                    SetInstallCommands(folderPath, "MSI+MST");
                }
                else if (hasMSI)
                {
                    StatusTextBlock.Text = "Status: MSI application detected.";
                    // Call the method to test MSI application
                    SetInstallCommands(folderPath, "MSI");
                }
                else
                {
                    StatusTextBlock.Text = "Status: No recognizable package found.";
                }
            }
            else
            {
                StatusTextBlock.Text = "Status: Target directory does not exist.";
            }
        }
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            // Ensure the destination directory exists
            Directory.CreateDirectory(destinationDir);

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
        private void SetInstallCommands(string folderPath, string packageType)
        {
            installCommand = string.Empty;
            uninstallCommand = string.Empty;
            silentinstallCommand = string.Empty;
            silentuninstallCommand = string.Empty;
            string packageFolder = PackageFolderTextBox.Text.Trim();

            // Extract Application Name and Version from folder path
            string[] folderParts = packageFolder.Split(Path.DirectorySeparatorChar);
            if (folderParts.Length < 4)
            {
                LogMessage("Invalid package folder structure. Unable to determine application details.");
                return;
            }
            string applicationName = folderParts[^4];
            string applicationVersion = folderParts[^3];
            string build = folderParts[^1];

            // Create destination directory in C:\temp
            string tempDirectory = Path.Combine("C:\\temp", $"{applicationName}_{applicationVersion}", build);
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
                        
            CopyDirectory(packageFolder, tempDirectory);

            if (packageType == "PSADTK")
            {

                var exeFile = Directory.GetFiles(folderPath, "*.exe").FirstOrDefault();
                var ps1File = Directory.GetFiles(folderPath, "*.ps1").FirstOrDefault();
                if (exeFile != null && ps1File != null)
                {

                    string exeName = Path.GetFileNameWithoutExtension(exeFile);
                    string ps1Name = Path.GetFileName(ps1File);

                    installCommand   = $"\"{tempDirectory}\\{exeName}.exe\" \"{tempDirectory}\\{ps1Name}\" -DeploymentType 'Install'";
                    uninstallCommand = $"\"{tempDirectory}\\{exeName}.exe\" \"{tempDirectory}\\{ps1Name}\" -DeploymentType 'UnInstall'";

                    silentinstallCommand = $".\\{tempDirectory}\\{exeName}.exe .\\{tempDirectory}\\{ps1Name} -DeploymentType 'Install' -DeployMode 'Silent'";
                    silentuninstallCommand = $".\\{tempDirectory}\\{exeName}.exe .\\{tempDirectory}\\{ps1Name} -DeploymentType 'UnInstall' -DeployMode 'Silent'";
                }
            }
            else if (packageType == "MSI")
            {
                var msiFile = Directory.GetFiles(tempDirectory, "*.msi").FirstOrDefault();
                if (msiFile != null)
                {
                    string msiName = Path.GetFileName(msiFile);
                    installCommand = $"msiexec.exe /i \"{Path.Combine(tempDirectory, msiName)}\" /qb";
                    uninstallCommand = $"msiexec.exe /x \"{Path.Combine(tempDirectory, msiName)}\" /qb";
                    silentinstallCommand = $"msiexec.exe /i \"{Path.Combine(tempDirectory, msiName)}\" /qn";
                    silentuninstallCommand = $"msiexec.exe /x \"{Path.Combine(tempDirectory, msiName)}\" /qn";
                }
            }
            else if (packageType == "MSI+MST")
            {
                var msiFile = Directory.GetFiles(tempDirectory, "*.msi").FirstOrDefault();
                var mstFile = Directory.GetFiles(tempDirectory, "*.mst").FirstOrDefault();
                if (msiFile != null && mstFile != null)
                {
                    string msiName = Path.GetFileName(tempDirectory);
                    string mstName = Path.GetFileName(tempDirectory);
                    installCommand = $"msiexec.exe /i \"{Path.Combine(tempDirectory, msiName)}\" TRANSFORMS=\"{Path.Combine(tempDirectory, mstName)}\" /qb";
                    uninstallCommand = $"msiexec.exe /x \"{Path.Combine(tempDirectory, msiName)}\" /qb";
                    silentinstallCommand = $"msiexec.exe /i \"{Path.Combine(tempDirectory, msiName)}\" TRANSFORMS=\"{Path.Combine(tempDirectory, mstName)}\" /qn";
                    silentuninstallCommand = $"msiexec.exe /x \"{Path.Combine(tempDirectory, msiName)}\"  /qn";
                }
            }
        }

        private void RunUIInstall_Click(object sender, RoutedEventArgs e)
        {

            string packageFolder = PackageFolderTextBox.Text.Trim();

            // Extract Application Name and Version from folder path
            string[] folderParts = packageFolder.Split(Path.DirectorySeparatorChar);
            if (folderParts.Length < 4)
            {
                LogMessage("Invalid package folder structure. Unable to determine application details.");
                return;
            }
            string applicationName = folderParts[^4];
            string applicationVersion = folderParts[^3];
            string build = folderParts[^1];

            // Create destination directory in C:\temp
            string tempDirectory = Path.Combine("C:\\temp", $"{applicationName}_{applicationVersion}", build);

            if (!string.IsNullOrEmpty(installCommand))
            {
                StatusTextBlock.Text = $"Running UI Install: {installCommand}";
                LogMessage("Running UI Install command: " + installCommand);
                RunCommandAsSystem(installCommand, tempDirectory);
            }
            else
            {
                StatusTextBlock.Text = "Status: No install command available.";
            }
        }

        private void RunSilentInstall_Click(object sender, RoutedEventArgs e)
        {
            string packageFolder = PackageFolderTextBox.Text.Trim();

            // Extract Application Name and Version from folder path
            string[] folderParts = packageFolder.Split(Path.DirectorySeparatorChar);
            if (folderParts.Length < 4)
            {
                LogMessage("Invalid package folder structure. Unable to determine application details.");
                return;
            }
            string applicationName = folderParts[^4];
            string applicationVersion = folderParts[^3];
            string build = folderParts[^1];

            // Create destination directory in C:\temp
            string tempDirectory = Path.Combine("C:\\temp", $"{applicationName}_{applicationVersion}", build);
            if (!string.IsNullOrEmpty(silentinstallCommand))
            {
                StatusTextBlock.Text = $"Running Silent Install: {silentinstallCommand}";
                LogMessage("Running Silent Install command: " + silentinstallCommand );
                RunCommandAsSystem(silentinstallCommand, tempDirectory);
            }
            else
            {
                StatusTextBlock.Text = "Status: No install command available.";
            }
        }

        private void RunUIUnInstall_Click(object sender, RoutedEventArgs e)
        {
            string packageFolder = PackageFolderTextBox.Text.Trim();

            // Extract Application Name and Version from folder path
            string[] folderParts = packageFolder.Split(Path.DirectorySeparatorChar);
            if (folderParts.Length < 4)
            {
                LogMessage("Invalid package folder structure. Unable to determine application details.");
                return;
            }
            string applicationName = folderParts[^4];
            string applicationVersion = folderParts[^3];
            string build = folderParts[^1];

            // Create destination directory in C:\temp
            string tempDirectory = Path.Combine("C:\\temp", $"{applicationName}_{applicationVersion}", build);
            if (!string.IsNullOrEmpty(uninstallCommand))
            {
                StatusTextBlock.Text = $"Running UI Install: {uninstallCommand}";
                LogMessage("Running UnInstall command: " + uninstallCommand);
                RunCommandAsSystem(uninstallCommand, tempDirectory);
            }
            else
            {
                StatusTextBlock.Text = "Status: No install command available.";
            }
        }

        private void RunSilentUnInstall_Click(object sender, RoutedEventArgs e)
        {
            string packageFolder = PackageFolderTextBox.Text.Trim();

            // Extract Application Name and Version from folder path
            string[] folderParts = packageFolder.Split(Path.DirectorySeparatorChar);
            if (folderParts.Length < 4)
            {
                LogMessage("Invalid package folder structure. Unable to determine application details.");
                return;
            }
            string applicationName = folderParts[^4];
            string applicationVersion = folderParts[^3];
            string build = folderParts[^1];

            // Create destination directory in C:\temp
            string tempDirectory = Path.Combine("C:\\temp", $"{applicationName}_{applicationVersion}", build);
            if (!string.IsNullOrEmpty(silentuninstallCommand))
            {
                StatusTextBlock.Text = $"Running Silent Install: {silentuninstallCommand}";
                LogMessage("Running Silent UnInstall command: " + silentuninstallCommand);
                RunCommandAsSystem(silentuninstallCommand, tempDirectory);
            }
            else
            {
                StatusTextBlock.Text = "Status: No install command available.";
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                string logFilePath = Path.Combine(logFolder, "TaskSchedulerLog.txt");
                File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error writing to log: " + ex.Message);
            }
        }

        private void RunCommandAsSystem(string command, string folderPath)
        {
            if (!IsTaskSchedulerRunning())
            {
                StatusTextBlock.Text += "\nTask Scheduler service is not running.";
                LogMessage("Task Scheduler service is not running. Command execution aborted.");
                return;
            }

            try
            {

                using (TaskService taskService = new TaskService())
                {
                    string taskName = "RunInstallTask";

                    // Delete existing task if present
                    var existingTask = taskService.GetTask(taskName);
                    if (existingTask != null)
                    {
                        taskService.RootFolder.DeleteTask(taskName);
                        LogMessage("Existing task deleted: " + taskName);
                        StatusTextBlock.Text += "\nExisting task deleted.";
                    }

                    TaskDefinition td = taskService.NewTask();
                    td.Principal.UserId = "SYSTEM";
                    td.Principal.LogonType = TaskLogonType.ServiceAccount;
                    td.Principal.RunLevel = TaskRunLevel.Highest;

                    // Run task from C:\temp directory
                    td.Actions.Add(new ExecAction("cmd.exe", $"/C \"{command}\"", folderPath));
                    td.Triggers.Add(new TimeTrigger() { StartBoundary = DateTime.Now });

                    taskService.RootFolder.RegisterTaskDefinition(taskName, td);
                    LogMessage($"Task registered successfully: {command}");
                    StatusTextBlock.Text += "\nTask Registered. Executing...";

                    // Start Progress Animation
                    StartLoadingAnimation();

                    // Run the task
                    Microsoft.Win32.TaskScheduler.Task task = taskService.GetTask(taskName);
                    if (task != null)
                    {
                        task.Run();
                        StatusTextBlock.Text += "\nTask is running...";
                        LogMessage($"Task registered and executed immediately: {command} in {folderPath}");

                        // Monitor Task Completion
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            while (true)
                            {
                                Dispatcher.Invoke(() => StatusTextBlock.Text += "\nChecking task status...");
                                await System.Threading.Tasks.Task.Delay(2000);

                                // Detect if process is still running
                                string processName = command.Split(' ')[0];  // Extract process name from command
                                var runningProcesses = Process.GetProcessesByName(processName);

                                if (runningProcesses.Length == 0) // Process has exited
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        CircularProgressBar.StrokeDashArray = new DoubleCollection { 100, 0 };
                                        ProgressText.Text = "100%";
                                        StatusTextBlock.Text += "\nTask Completed Successfully.";
                                        StopLoadingAnimation();
                                    });
                                    LogMessage("Task Completed Successfully.");
                                    break;
                                }
                            }
                        });
                    }
                    else
                    {
                        StatusTextBlock.Text += "\nTask failed to start.";
                        LogMessage("Task failed to start: " + taskName);
                        StopLoadingAnimation();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text += "\nError: Task scheduling failed.";
                LogMessage("Error scheduling task: " + ex.Message);
                StopLoadingAnimation();
            }
        }

        private bool IsTaskSchedulerRunning()
        {
            try
            {
                var service = new ServiceController("Schedule");
                return service.Status == ServiceControllerStatus.Running;
            }
            catch (Exception ex)
            {
                LogMessage("Error checking Task Scheduler status: " + ex.Message);
                return false;
            }
        }

        private void StartLoadingAnimation()
        {
            StatusProgressBar.Visibility = Visibility.Visible;

            System.Threading.Tasks.Task.Run(async () =>  // Explicitly define Task namespace
            {
                for (int i = 0; i <= 100; i++)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Ensure CircularProgressBar is cast as Path
                        if (FindName("CircularProgressBar") is System.Windows.Shapes.Path circularProgress)
                        {
                            circularProgress.StrokeDashArray = new System.Windows.Media.DoubleCollection { i, 100 - i };
                        }

                        // Ensure ProgressText is cast as TextBlock
                        if (FindName("ProgressText") is TextBlock progressText)
                        {
                            progressText.Text = $"{i}%";
                        }

                        // Update Linear Progress Bar
                        StatusProgressBar.Value = i;
                        if (FindName("LinearProgressText") is TextBlock linearProgressText)
                        {
                            linearProgressText.Text = $"{i}%";
                        }
                    });
                    await System.Threading.Tasks.Task.Delay(50); // Simulates progress
                }
            });
        }

        private void StopLoadingAnimation()
        {
            Dispatcher.Invoke(() =>
            {
                // Ensure Circular Progress Bar Completes Fully Before Stopping
                if (FindName("CircularProgressBar") is System.Windows.Shapes.Path circularProgress)
                {
                    circularProgress.StrokeDashArray = new System.Windows.Media.DoubleCollection { 100, 0 };
                }

                // Ensure Progress Text Reaches 100% Before Stopping
                if (FindName("ProgressText") is TextBlock progressText)
                {
                    progressText.Text = "100%";
                }

                // Ensure Linear Progress Bar is Fully Completed Before Hiding
                StatusProgressBar.Value = 100;

                if (FindName("LinearProgressText") is TextBlock linearProgressText)
                {
                    linearProgressText.Text = "100%";
                }

                // Hide Progress Bar
                StatusProgressBar.Visibility = Visibility.Collapsed;
            });
        }


    }
}
