using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using NLog;
using PackageConsole.Data;

namespace PackageConsole
{
    public partial class PreviousApps : Page, IIniContext, System.ComponentModel.INotifyPropertyChanged
    {
        private string iniFilePath;
        public Dictionary<string, Dictionary<string, string>> iniSections { get; private set; } = new();
        public Dictionary<string, Dictionary<string, string>> IniSections => iniSections;

        private List<string> rawContent;
        private static readonly Logger Logger = LogManager.GetLogger(nameof(PreviousApps));

        // Feature: Backup for Undo

        private const int MaxUndoSnapshots = 5;
        private readonly Stack<Dictionary<string, Dictionary<string, string>>> undoSnapshots = new();
        private bool deleteModeEnabled = false;
        private readonly Stack<Dictionary<string, Dictionary<string, string>>> redoSnapshots = new();

        // UI state for inline [+ Key]
        private bool showAddKeyButton;
        public bool ShowAddKeyButton
        {
            get => showAddKeyButton;
            set { showAddKeyButton = value; OnPropertyChanged(nameof(ShowAddKeyButton)); }
        }


        public bool HasSection(string sectionName)
        {
            return iniSections.Keys.Any(sec => sec.Trim().Equals(sectionName.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        public Dictionary<string, string> GetKeyValues(string section)
        {
            if (iniSections != null && iniSections.ContainsKey(section))
            {
                return iniSections[section];
            }
            return new Dictionary<string, string>();
        }
        private Dictionary<string, Dictionary<string, string>> DeepCloneIniSections()
        {
            return iniSections.ToDictionary(
                section => section.Key,
                section => section.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            );
        }
        public PreviousApps()
        {
            InitializeComponent();
            DataContext = this;
        }
        private void LoadIniFile()
        {
            if (!File.Exists(iniFilePath))
            {
                MessageBox.Show($"INI file not found at: {iniFilePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                iniSections = IniFileHelper.ParseIniFile(iniFilePath, out rawContent);
                //BackupCurrentState(); // Save initial state
                cmbSections.ItemsSource = iniSections.Keys.ToList();
                // Display in RichTextBox
                richIniContent.Document.Blocks.Clear();
                richIniContent.Document.Blocks.Add(new Paragraph(new Run(
                    string.Join(Environment.NewLine, rawContent)
                )));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading INI file");
                MessageBox.Show($"Error loading INI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void UpdateIniSection(string section, Dictionary<string, string> updatedValues)
        {
            if (!iniSections.ContainsKey(section))
            {
                MessageBox.Show($"Error: Section '{section}' not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Snapshot before change (clears redo)
            SaveSnapshot();

            iniSections[section] = updatedValues;

            SaveIniFile();  //  Save changes to INI file
            RefreshIniContent();  //  Update UI
        }
        public void UpdateIniSections(Dictionary<string, Dictionary<string, string>> updatedSections)
        {
            // Snapshot before change (clears redo)
            SaveSnapshot();

            iniSections = updatedSections;
            SaveIniFile();  //  Save changes
            RefreshIniContent();  //  Update UI
        }
        private void btnLoadINI_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "INI files (*.ini)|*.ini",
                Title = "Select Package.ini file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                iniFilePath = openFileDialog.FileName;

                try
                {
                    // Step 1: Use helper to parse
                    iniSections = IniFileHelper.ParseIniFile(iniFilePath, out rawContent);

                    // Step 2: Get AppName and AppVer
                    if (iniSections.TryGetValue("PRODUCT INFO", out var productInfo))
                    {
                        var appName = productInfo.GetValueOrDefault("APPNAME", "UnknownApp");
                        var appVer = productInfo.GetValueOrDefault("APPVER", "0.0");

                        // Replace invalid filename characters with underscore
                        appName = string.Join("_", appName.Split(Path.GetInvalidFileNameChars()));
                        appVer = string.Join("_", appVer.Split(Path.GetInvalidFileNameChars()));

                        string mainFolder = Directory.GetParent(Path.GetDirectoryName(iniFilePath))!.FullName;
                        string targetDir = $@"C:\Temp\PackageConsole\{appName}_{appVer}";

                        if (Directory.Exists(targetDir))
                            Directory.Delete(targetDir, true);
                        CopyDirectory(mainFolder, targetDir);

                        richIniContent.Document.Blocks.Clear();
                        richIniContent.AppendText(string.Join(Environment.NewLine, rawContent));
                        LoadIniFile();
                        RefreshIniContent();
                    }
                    else
                    {
                        MessageBox.Show("PRODUCT INFO section is missing in the INI file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load INI file.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destPath = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, true);
            }
        }

        private void cmbSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            stackKeyValueEditor.Children.Clear();

            if (cmbSections.SelectedItem is string selectedSection)
            {
                // Control [+] visibility for permission sections and FILELOCK
                var secName = selectedSection?.ToUpperInvariant() ?? string.Empty;
                ShowAddKeyButton = secName.Contains("PERM") || secName == "FILELOCK";

                if (iniSections.TryGetValue(selectedSection, out Dictionary<string, string> keyValues))
                {
                    foreach (var kvp in keyValues)
                    {
                        var grid = new Grid { Margin = new Thickness(5) };
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition());

                        if (deleteModeEnabled)  // Add third column if delete mode is ON
                            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

                        var lbl = new TextBlock
                        {
                            Text = kvp.Key,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        var txt = new TextBox
                        {
                            Text = kvp.Value,
                            Tag = kvp.Key,
                            Margin = new Thickness(5, 0, 0, 0),
                            ToolTip = VariableHelper.GetTooltipText()
                        };
                        // Add auto-complete behavior
                        VariableHelper.AttachAutocompleteBehavior(txt);

                        Grid.SetColumn(lbl, 0);
                        Grid.SetColumn(txt, 1);

                        grid.Children.Add(lbl);
                        grid.Children.Add(txt);

                        if (deleteModeEnabled)
                        {
                            var btnDelete = new Button
                            {
                                Width = 28,
                                Height = 28,
                                Margin = new Thickness(2, 0, 0, 0),
                                Tag = kvp.Key,
                                ToolTip = "Delete key"
                            };
                            btnDelete.Style = (Style)FindResource("RoundIconButtonRed");
                            btnDelete.Content = new TextBlock { Text = "✖", FontSize = 14, FontWeight = FontWeights.Bold };
                            btnDelete.Click += BtnDeleteKey_Click;

                            Grid.SetColumn(btnDelete, 2);
                            grid.Children.Add(btnDelete);
                        }

                        stackKeyValueEditor.Children.Add(grid);
                    }
                }
            }
        }
        private void DeleteToggle_Checked(object sender, RoutedEventArgs e)
        {
            deleteModeEnabled = radioDeleteYes.IsChecked == true;
            cmbSections_SelectionChanged(null, null); // Refresh editor
        }
        private void btnOpenPostconfig_Click(object sender, RoutedEventArgs e)
        {
            var postWindow = new Postconfig(this); // 'this' is Previous Apps page
            postWindow.Show();
            Logger.Info("Post Config button is clicked");

        }
        private void UndoLastChange()
        {
            if (undoSnapshots.Count == 0)
            {
                MessageBox.Show("No undo history available.", "Undo", MessageBoxButton.OK, MessageBoxImage.Information);
                Logger.Warn("Undo requested, but history is empty.");
                return;
            }

            // Push current state to redo before undoing
            redoSnapshots.Push(IniFileHelper.CloneIniSections(iniSections));

            iniSections = undoSnapshots.Pop();
            SaveIniFile();
            RefreshIniContent();
            cmbSections.ItemsSource = iniSections.Keys.ToList();
            MessageBox.Show("Undo applied successfully.", "Undo", MessageBoxButton.OK, MessageBoxImage.Information);
            Logger.Info("Undo operation performed.");
        }
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "INI Files (*.ini)|*.ini|All Files (*.*)|*.*",
                FileName = "ExportedPackage.ini"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // Save to file
                TextRange range = new TextRange(
                    richIniContent.Document.ContentStart,
                    richIniContent.Document.ContentEnd
                );
                File.WriteAllText(saveFileDialog.FileName, range.Text);
                MessageBox.Show("INI file exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RedoLastChange()
        {
            if (redoSnapshots.Count == 0)
            {
                MessageBox.Show("No redo history available.", "Redo", MessageBoxButton.OK, MessageBoxImage.Information);
                Logger.Warn("Redo requested, but history is empty.");
                return;
            }

            // Push current state to undo before redoing
            undoSnapshots.Push(IniFileHelper.CloneIniSections(iniSections));
            while (undoSnapshots.Count > MaxUndoSnapshots)
                _ = undoSnapshots.Pop();

            iniSections = redoSnapshots.Pop();
            SaveIniFile();
            RefreshIniContent();
            cmbSections.ItemsSource = iniSections.Keys.ToList();
            MessageBox.Show("Redo applied successfully.", "Redo", MessageBoxButton.OK, MessageBoxImage.Information);
            Logger.Info("Redo operation performed.");
        }

        private void btnRedo_Click(object sender, RoutedEventArgs e)
        {
            RedoLastChange();
        }

        private void btnUndo_Click(object sender, RoutedEventArgs e)
        {
            UndoLastChange();
        }
        private void BtnAddKey_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSections.SelectedItem is not string selectedSection) return;

            var secName = selectedSection.ToUpperInvariant();
            string prefix = secName switch
            {
                "FOLDERPERM" => "FLD",
                "FILEPERM"  => "FILE",
                "REGPERM"   => "REG",
                "FILELOCK"  => "FILE",
                _            => "KEY"
            };

            // Only allow quick-add in the intended sections
            if (!(secName.Contains("PERM") || secName == "FILELOCK"))
                return;

            if (!iniSections.TryGetValue(selectedSection, out var dict)) return;

            // Snapshot before change (clears redo)
            SaveSnapshot();

            int nextIndex = 1;
            while (dict.ContainsKey($"{prefix}{nextIndex}")) nextIndex++;
            string newKey = $"{prefix}{nextIndex}";
            dict[newKey] = string.Empty;

            // Persist and refresh
            SaveIniFile();
            RefreshIniContent();
            cmbSections_SelectionChanged(null, null);
            MessageBox.Show($"Added key '{newKey}'", "Add Key", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSections.SelectedItem is not string selectedSection) return;
            if (!iniSections.ContainsKey(selectedSection)) return;

            var updatedValues = new Dictionary<string, string>();

            foreach (Grid grid in stackKeyValueEditor.Children.OfType<Grid>())
            {
                var txt = grid.Children.OfType<TextBox>().FirstOrDefault();
                if (txt != null && txt.Tag is string key)
                {
                    updatedValues[key] = txt.Text.Trim();
                }
            }

            var snapshotBeforeChange = IniFileHelper.CloneIniSections(iniSections);
            iniSections[selectedSection] = updatedValues;
            undoSnapshots.Push(snapshotBeforeChange);
            while (undoSnapshots.Count > MaxUndoSnapshots)
                _ = undoSnapshots.Pop();
            // New change invalidates redo
            redoSnapshots.Clear();

            // Save and refresh
            SaveIniFile();
            RefreshIniContent();

            //  Refresh section list in ComboBox
            cmbSections.ItemsSource = null;
            cmbSections.ItemsSource = iniSections.Keys.ToList();
            cmbSections.SelectedItem = selectedSection;
            Logger.Info($"Section '{selectedSection}' updated with {updatedValues.Count} keys.");
            MessageBox.Show("Section updated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
		 private void txtIniContent_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                iniFilePath = files[0]; // Load first dropped file
                LoadIniFile();
            }
        }
        private void btnAddSection_Click(object sender, RoutedEventArgs e)
        {
            var postWindow = new Postconfig(this);
            postWindow.Show();

        }
        private void btnConfigure_Click(object sender, RoutedEventArgs e)
        {
            if (btnConfigure?.ContextMenu != null)
            {
                btnConfigure.ContextMenu.PlacementTarget = btnConfigure;
                btnConfigure.ContextMenu.IsOpen = true;
            }
        }

        private void Configure_MACHINESPECIFIC_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("MACHINESPECIFIC");
        private void Configure_USERSPECIFIC_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("USERSPECIFIC");
        private void Configure_SERVICECONTROL_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("SERVICECONTROL");
        private void Configure_TAG_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("TAG");

        private void OpenConfigurationFor(string sectionName)
        {
            var existingKey = iniSections.Keys.FirstOrDefault(k => k.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (existingKey == null)
            {
                // Create the section if it doesn't exist
                SaveSnapshot();
                iniSections[sectionName] = new Dictionary<string, string>();

                // Refresh UI and persist
                cmbSections.ItemsSource = null;
                cmbSections.ItemsSource = iniSections.Keys.ToList();
                existingKey = sectionName;
                SaveIniFile();
                RefreshIniContent();
            }

            // Select the section in the combo
            cmbSections.SelectedItem = existingKey;

            // Open Postconfig with the section preselected
            var postWindow = new Postconfig(this, existingKey);
            postWindow.Show();
            Logger.Info($"Configuration opened for '{existingKey}'.");
        }

        private void btnRemoveSection_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSections.SelectedItem == null)
            {
                Logger.Info("Select a section first before clicking on REMOVE SECTION BUTTON!");
                MessageBox.Show("Select a section first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string section = cmbSections.SelectedItem.ToString();

            // Confirmation prompt before deletion
            var result = MessageBox.Show(
                $"Are you sure you want to remove the '{section}' section?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveSnapshot();


                cmbSections.ItemsSource = iniSections.Keys.ToList();  // Refresh ComboBox
                var snapshotBeforeChange = IniFileHelper.CloneIniSections(iniSections);
                iniSections.Remove(section);  // Remove from dictionary
                undoSnapshots.Push(snapshotBeforeChange);
                while (undoSnapshots.Count > MaxUndoSnapshots)
                    _ = undoSnapshots.Pop();
                SaveIniFile();      // Persist changes
                RefreshIniContent(); // Update the INI view
                Logger.Info($"Section '{section}' removed successfully!");
                MessageBox.Show($"Section '{section}' removed successfully!", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void SaveSnapshot()
        {
            undoSnapshots.Push(IniFileHelper.CloneIniSections(iniSections));
            while (undoSnapshots.Count > MaxUndoSnapshots)
                _ = undoSnapshots.Pop();
            // New change invalidates redo
            redoSnapshots.Clear();
        }
        private void btnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to save changes?", "Confirm Save",
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SaveIniFile();
            }
        }
        public void LoadDataFromPostconfig(string postconfigData, string selectedSection)
        {
            if (string.IsNullOrWhiteSpace(postconfigData) || string.IsNullOrWhiteSpace(selectedSection))
            {
                MessageBox.Show("Invalid data or section!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (iniSections.ContainsKey(selectedSection))
            {
                // Snapshot before change (clears redo)
                SaveSnapshot();

                var newEntries = postconfigData.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in newEntries)
                {
                    var parts = entry.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        // Insert into the correct section
                        iniSections[selectedSection][key] = value;
                    }
                }

                // Save changes and update UI
                SaveIniFile();
                RefreshIniContent();
                Logger.Info($"Data added to {selectedSection} successfully!");
                MessageBox.Show($"Data added to {selectedSection} successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Section not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public List<string> GetSectionNames()
        {
            return iniSections?.Keys.ToList() ?? new List<string>();
        }
        public void RefreshIniContent()
        {
            var content = string.Join(Environment.NewLine + Environment.NewLine,
            iniSections.Select(section =>
            $"[{section.Key}]{Environment.NewLine}" +
            string.Join(Environment.NewLine, section.Value.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                    )
                );

            HighlightIniContent(content); //  Use the rich text formatter here
        }
        private void RenderSections()
        {
            stackKeyValueEditor.Children.Clear();

            foreach (var section in iniSections)
            {
                // Section header
                var header = new TextBlock
                {
                    Text = $"[{section.Key}]",
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Orange,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                stackKeyValueEditor.Children.Add(header);

                // Key-Value entries
                foreach (var kvp in section.Value)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                    var keyBlock = new TextBlock
                    {
                        Text = kvp.Key,
                        Width = 150,
                        Foreground = Brushes.LightGray
                    };

                    var valueBox = new TextBox
                    {
                        Text = kvp.Value,
                        Width = 300,
                        Tag = new Tuple<string, string>(section.Key, kvp.Key),
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    panel.Children.Add(keyBlock);
                    panel.Children.Add(valueBox);
                    stackKeyValueEditor.Children.Add(panel);
                }
            }

            Logger.Info("Sections re-rendered.");
        }

        public void SaveIniFile()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var section in iniSections)
                {
                    lines.Add($"[{section.Key}]");
                    if (section.Value.Count == 0)
                        lines.Add("; (no keys)");

                    foreach (var kvp in section.Value)
                        lines.Add($"{kvp.Key}={kvp.Value}");
                    lines.Add("");
                }

                File.WriteAllLines(iniFilePath, lines);
                Logger.Info($"INI file saved successfully: {iniFilePath}");
                SqlitePackageHelper.UpdateIniTextOnly(iniFilePath);

            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to save INI file: {iniFilePath}");
                MessageBox.Show($"Failed to save INI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void HighlightIniContent(string content)
        {
            richIniContent.Document.Blocks.Clear();

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };

                if (line.TrimStart().StartsWith("[") && line.TrimEnd().EndsWith("]"))
                {
                    paragraph.Inlines.Add(new Run(line)
                    {
                        Foreground = Brushes.Orange,
                        FontWeight = FontWeights.Bold
                    });
                }
                else
                {
                    paragraph.Inlines.Add(new Run(line));
                }

                richIniContent.Document.Blocks.Add(paragraph);
            }
        }
        private void BtnDeleteKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string keyToRemove && cmbSections.SelectedItem is string section)
            {
                var result = MessageBox.Show($"Remove key '{keyToRemove}' from section '{section}'?", "Confirm Delete",
                                             MessageBoxButton.YesNo, MessageBoxImage.Warning);
                Logger.Info($"Key '{keyToRemove}' removed from section '{section}'");

                if (result == MessageBoxResult.Yes)
                {
                    if (iniSections.ContainsKey(section) && iniSections[section].ContainsKey(keyToRemove))
                    {
                        var snapshotBeforeDelete = IniFileHelper.CloneIniSections(iniSections);
                        iniSections[section].Remove(keyToRemove);
                        undoSnapshots.Push(snapshotBeforeDelete);
                        while (undoSnapshots.Count > MaxUndoSnapshots)
                            _ = undoSnapshots.Pop();
                        // New change invalidates redo
                        redoSnapshots.Clear();
                        SaveIniFile();
                        RefreshIniContent();
                        cmbSections_SelectionChanged(null, null);
                        Logger.Info($"Key '{keyToRemove}' removed from section '{section}'");
                    }
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

    }
}
