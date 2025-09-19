using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using NLog;
using PackageConsole.Data;
using PackageConsole.Models;

namespace PackageConsole
{
    public partial class IniConsolePage : Page, IIniContext , INotifyPropertyChanged
    {
        private string iniFilePath;
        private readonly string productFolder;
        private readonly string supportFilesFolder;
        // public Dictionary<string, Dictionary<string, string>> iniSections { get; private set; }
        public Dictionary<string, Dictionary<string, string>> iniSections { get; private set; } = new();
        public Dictionary<string, Dictionary<string, string>> IniSections => iniSections;
        private List<string>? rawContent;
        private static readonly Logger Logger = LogManager.GetLogger(nameof(IniConsolePage));
        private IniSection? selectedSection;
        private bool showAddKeyButton;
        public bool ShowAddKeyButton
        {
            get => showAddKeyButton;
            set { showAddKeyButton = value; OnPropertyChanged(nameof(ShowAddKeyButton)); }
        }

        public IniSection? SelectedSection
        {
            get => selectedSection;
            set { selectedSection = value; OnPropertyChanged(nameof(SelectedSection)); HandleSectionSelection(value); }
        }
        // Feature: Backup for Undo

        private const int MaxUndoSnapshots = 5;
        private readonly Stack<Dictionary<string, Dictionary<string, string>>> undoSnapshots = new();
        private readonly Stack<Dictionary<string, Dictionary<string, string>>> redoSnapshots = new();

        private bool deleteModeEnabled = false;
        public bool DeleteModeEnabled
        {
            get => deleteModeEnabled;
            set { deleteModeEnabled = value; OnPropertyChanged(nameof(DeleteModeEnabled)); }
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


        public IniConsolePage(string productFolder, string supportFilesFolder)
        {
            InitializeComponent();
            this.productFolder = productFolder;
            this.supportFilesFolder = supportFilesFolder;
            Logger.Info($"IniConsolePage initialized at location: {supportFilesFolder}");
            richIniContent.Document.PageWidth = 1000; // Adjust width if needed

            iniFilePath = System.IO.Path.Combine(supportFilesFolder, "Package.ini");
            LoadIniFile();
            AutoMergeTmpIniIfPresent();
            RefreshIniContent();  //  Update UI
            DataContext = this;
        }

        private void AutoMergeTmpIniIfPresent()
        {
            try
            {
                string tmpIniPath = Path.Combine(supportFilesFolder, "tmpPackage.ini");
                if (!File.Exists(tmpIniPath))
                    return;

                var tmpSections = IniFileHelper.ParseIniFile(tmpIniPath, out _);
                Logger.Info("Auto-merging tmpPackage.ini into current INI on page load...");

                var upgradeSections = tmpSections
                    .Where(kvp => kvp.Key.StartsWith("UPGRADE", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(kvp => int.TryParse(kvp.Key.Substring(7), out int n) ? n : int.MaxValue)
                    .ToList();

                var newSectionList = new ObservableCollection<IniSection>();
                bool upgradesInserted = false;

                // Determine next available UPGRADE index to avoid duplicate names (e.g., existing UPGRADE1)
                int nextUpgradeIndex = IniSectionList
                    .Where(s => s.Name.StartsWith("UPGRADE", StringComparison.OrdinalIgnoreCase))
                    .Select(s => int.TryParse(s.Name.Substring(7), out var n) ? n : 0)
                    .DefaultIfEmpty(0)
                    .Max() + 1;

                foreach (var section in IniSectionList)
                {
                    if (!upgradesInserted && section.Name.Equals("UNINSTALL1", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var upgrade in upgradeSections)
                        {
                            var newUpgradeName = $"UPGRADE{nextUpgradeIndex++}";
                            var upgradeSection = new IniSection(newUpgradeName);
                            foreach (var kvp in upgrade.Value)
                                upgradeSection.Entries.Add(new IniEntry { Key = kvp.Key, Value = kvp.Value });
                            newSectionList.Add(upgradeSection);
                        }
                        upgradesInserted = true;
                    }

                    newSectionList.Add(section);
                }

                // Merge non-UPGRADE sections from tmp
                foreach (var kvp in tmpSections)
                {
                    var existing = newSectionList.FirstOrDefault(s => s.Name == kvp.Key);
                    if (existing != null)
                    {
                        foreach (var kv in kvp.Value)
                        {
                            var entry = existing.Entries.FirstOrDefault(e => e.Key == kv.Key);
                            if (entry != null)
                                entry.Value = kv.Value;
                            else
                                existing.Entries.Add(new IniEntry { Key = kv.Key, Value = kv.Value });
                        }
                    }
                    else if (!kvp.Key.StartsWith("UPGRADE", StringComparison.OrdinalIgnoreCase))
                    {
                        var newSec = new IniSection(kvp.Key);
                        foreach (var ent in kvp.Value)
                            newSec.Entries.Add(new IniEntry { Key = ent.Key, Value = ent.Value });
                        newSectionList.Add(newSec);
                    }
                }

                IniSectionList.Clear();
                foreach (var sec in newSectionList)
                    IniSectionList.Add(sec);

                SaveIniFile();
                Logger.Info("INI file updated with tmpPackage.ini content (auto-merge).");

                try
                {
                    File.Delete(tmpIniPath);
                    Logger.Info("tmpPackage.ini deleted after auto-merge.");
                }
                catch (Exception delEx)
                {
                    Logger.Warn(delEx, "Failed to delete tmpPackage.ini after auto-merge");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error auto-merging tmpPackage.ini on page load");
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public ObservableCollection<IniSection> IniSectionList { get; set; } = new();

        public void UpdateIniSection(string section, Dictionary<string, string> updatedValues)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                MessageBox.Show("Invalid section name!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Snapshot before change (clears redo)
            SaveSnapshot();

            // Ensure backing dict contains the section
            if (!iniSections.ContainsKey(section))
            {
                iniSections[section] = new Dictionary<string, string>();
            }

            // 1) Update backing dictionary
            iniSections[section] = new Dictionary<string, string>(updatedValues);

            // 2) Reflect the change into the UI model (IniSectionList)
            var uiSection = IniSectionList.FirstOrDefault(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase));
            if (uiSection == null)
            {
                uiSection = new IniSection(section);
                IniSectionList.Add(uiSection);
            }

            uiSection.Entries.Clear();
            foreach (var kv in updatedValues)
            {
                uiSection.Entries.Add(new IniEntry { Key = kv.Key, Value = kv.Value });
            }

            // 3) Persist and refresh UI
            SaveIniFile();  // Save based on IniSectionList
            RefreshIniContent();
        }
        public void UpdateIniSections(Dictionary<string, Dictionary<string, string>> updatedSections)
        {
            if (updatedSections == null)
                return;

            // Snapshot before change (clears redo)
            SaveSnapshot();

            // Replace backing dictionary
            iniSections = updatedSections.ToDictionary(k => k.Key, v => new Dictionary<string, string>(v.Value));

            // Rebuild UI collection to stay in sync
            IniSectionList.Clear();
            foreach (var section in iniSections)
            {
                var uiSec = new IniSection(section.Key);
                foreach (var kv in section.Value)
                    uiSec.Entries.Add(new IniEntry { Key = kv.Key, Value = kv.Value });
                IniSectionList.Add(uiSec);
            }

            SaveIniFile();
            RefreshIniContent();
        }
        private void LoadIniFile()
        {
            if (!File.Exists(iniFilePath))
            {
                Logger.Error($"INI file not found at: {iniFilePath}");
                MessageBox.Show($"INI file not found at: {iniFilePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var parsed = IniFileHelper.ParseIniFile(iniFilePath, out rawContent);

                // Keep backing dictionary in sync and inject ToolkitPath into VARS
                iniSections = parsed ?? new Dictionary<string, Dictionary<string, string>>();
                if (!iniSections.TryGetValue("VARS", out var vars))
                {
                    vars = new Dictionary<string, string>();
                    iniSections["VARS"] = vars;
                }
                if (!vars.ContainsKey("ToolkitPath"))
                {
                    try
                    {
                        vars["ToolkitPath"] = PathManager.ToolkitPath;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "ToolkitPath not available from configuration");
                    }
                }

                // Expose variables to autocomplete/tooltip helper
                VariableHelper.IniSections = iniSections;

                // Rebuild UI list from the authoritative dictionary
                IniSectionList.Clear();
                foreach (var section in iniSections)
                {
                    var iniSection = new IniSection(section.Key);
                    foreach (var kvp in section.Value)
                    {
                        iniSection.Entries.Add(new IniEntry { Key = kvp.Key, Value = kvp.Value });
                    }
                    IniSectionList.Add(iniSection);
                }

                // Select first section by default to populate the editor
                SelectedSection = IniSectionList.FirstOrDefault();

                Logger.Info($"INI file loaded with {IniSectionList.Count} sections (VARS keys: {string.Join(',', iniSections["VARS"].Keys)}).");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading INI file");
                MessageBox.Show($"Error loading INI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void cmbSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSections.SelectedItem is IniSection section)
            {
                SelectedSection = section;
            }
        }
        private void HandleSectionSelection(IniSection? section)
        {
            if (section == null)
            {
                ShowAddKeyButton = false;
                return;
            }

            // Enable [+] button only for permission sections (case-insensitive) and FILELOCK
            var secName = section.Name?.ToUpperInvariant() ?? string.Empty;
            ShowAddKeyButton = secName.Contains("PERM") || secName == "FILELOCK";

            Logger.Info($"Selected section: {section.Name}, ShowAddKeyButton = {ShowAddKeyButton}");
        }
        private void AutoCompleteBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                VariableHelper.AttachAutocompleteBehavior(tb);
            }
        }

        private void AddKeyValueRow(string key, string value, string section)
        {
            var grid = new Grid { Margin = new Thickness(5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            if (deleteModeEnabled)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            var lbl = new TextBlock
            {
                Text = key,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txt = new TextBox
            {
                Text = value,
                Tag = key,
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
                    Content = "❌",
                    Width = 25,
                    Height = 25,
                    Margin = new Thickness(5, 0, 0, 0),
                    Tag = key
                };
                btnDelete.Click += BtnDeleteKey_Click;

                Grid.SetColumn(btnDelete, 2);
                grid.Children.Add(btnDelete);
            }

            //stackKeyValueEditor.Children.Add(grid);
        }
        private void BtnAddKey_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSection == null)
                return;

            var secName = SelectedSection.Name.ToUpperInvariant();
            string prefix = secName switch
            {
                "FOLDERPERM" => "FLD",
                "FILEPERM"  => "FILE",
                "REGPERM"   => "REG",
                "FILELOCK"  => "FILE",
                _            => "KEY"
            };

            int nextIndex = 1;
            while (SelectedSection.Entries.Any(entry => entry.Key == $"{prefix}{nextIndex}"))
                nextIndex++;

            string newKey = $"{prefix}{nextIndex}";

            SelectedSection.Entries.Add(new IniEntry
            {
                Key = newKey,
                Value = ""
            });

            Logger.Info($"Added key '{newKey}' to section '{SelectedSection.Name}'");
        }

        private void DeleteToggle_Checked(object sender, RoutedEventArgs e)
        {
            DeleteModeEnabled = radioDeleteYes.IsChecked == true;
            // No need to rebuild template manually; binding will update, but we refresh just in case
            cmbSections_SelectionChanged(null, null); // Refresh editor
        }
        private void btnOpenPostconfig_Click(object sender, RoutedEventArgs e)
        {
            // Postconfig postconfigWindow = new(this);
            // postconfigWindow.Show();
            var postWindow = new Postconfig(this);
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

            var previousSelection = SelectedSection?.Name;
            iniSections = undoSnapshots.Pop();

            // Rebuild UI list to reflect restored dictionary
            IniSectionList.Clear();
            foreach (var section in iniSections)
            {
                var iniSection = new IniSection(section.Key);
                foreach (var kvp in section.Value)
                    iniSection.Entries.Add(new IniEntry { Key = kvp.Key, Value = kvp.Value });
                IniSectionList.Add(iniSection);
            }

            // Persist and refresh view
            SaveIniFile();
            RefreshIniContent();

            // Restore selection if possible
            SelectedSection = IniSectionList.FirstOrDefault(s => s.Name.Equals(previousSelection ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                              ?? IniSectionList.FirstOrDefault();

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

            var previousSelection = SelectedSection?.Name;
            iniSections = redoSnapshots.Pop();

            // Rebuild UI list from restored dict
            IniSectionList.Clear();
            foreach (var section in iniSections)
            {
                var iniSection = new IniSection(section.Key);
                foreach (var kvp in section.Value)
                    iniSection.Entries.Add(new IniEntry { Key = kvp.Key, Value = kvp.Value });
                IniSectionList.Add(iniSection);
            }

            SaveIniFile();
            RefreshIniContent();

            SelectedSection = IniSectionList.FirstOrDefault(s => s.Name.Equals(previousSelection ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                              ?? IniSectionList.FirstOrDefault();

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
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSection == null) return;

            // Create a snapshot before update for Undo
            var snapshotBeforeChange = IniSectionList
                .ToDictionary(
                    s => s.Name,
                    s => s.Entries.ToDictionary(e => e.Key, e => e.Value));

            // Push to undo stack
            undoSnapshots.Push(snapshotBeforeChange);
            while (undoSnapshots.Count > MaxUndoSnapshots)
                _ = undoSnapshots.Pop();
            // New change: invalidate redo
            redoSnapshots.Clear();

            // Keep backing dictionary in sync with UI model
            iniSections = IniSectionList.ToDictionary(
                s => s.Name,
                s => s.Entries.ToDictionary(e => e.Key, e => e.Value));

            // Save and refresh UI/content
            SaveIniFile();
            RefreshIniContent();

            Logger.Info($"Section '{SelectedSection.Name}' updated with {SelectedSection.Entries.Count} keys.");
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
            btnAddSection.ContextMenu.PlacementTarget = btnAddSection;
            btnAddSection.ContextMenu.IsOpen = true;
        }

        private string GetNextSectionName(string baseSection)
        {
            // Extract alphabetic prefix e.g., INSTALL from INSTALL1
            string prefix = new string(baseSection.TakeWhile(char.IsLetter).ToArray());
            int number = 1;
            while (iniSections.Keys.Any(k => k.Equals($"{prefix}{number}", StringComparison.OrdinalIgnoreCase)))
            {
                number++;
            }
            return $"{prefix}{number}";
        }

        private void CreateNewSection(string originalSection, string newSection)
        {
            // Ensure original exists
            var originalKey = iniSections.Keys.FirstOrDefault(k => k.Equals(originalSection, StringComparison.OrdinalIgnoreCase));
            if (originalKey == null)
            {
                MessageBox.Show($"Error: Section '{originalSection}' not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Snapshot before change (clears redo)
            SaveSnapshot();

            // Copy key-values
            var copiedValues = new Dictionary<string, string>(iniSections[originalKey]);

            // Insert into dictionary maintaining order via IniSectionList
            // Find index of original in UI list
            int idx = IniSectionList.ToList().FindIndex(s => s.Name.Equals(originalKey, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = IniSectionList.Count - 1;

            // Update backing dictionary
            iniSections[newSection] = new Dictionary<string, string>(copiedValues);

            // Update UI collection at the correct position (right after original)
            var newUiSection = new IniSection(newSection);
            foreach (var kv in copiedValues)
                newUiSection.Entries.Add(new IniEntry { Key = kv.Key, Value = kv.Value });

            IniSectionList.Insert(Math.Min(idx + 1, IniSectionList.Count), newUiSection);

            // Save and refresh
            SaveIniFile();
            RefreshIniContent();

            // Select the newly created section
            SelectedSection = newUiSection;
        }

        private void btnConfigure_Click(object sender, RoutedEventArgs e)
        {
            btnConfigure.ContextMenu.PlacementTarget = btnConfigure;
            btnConfigure.ContextMenu.IsOpen = true;
        }
        // ContextMenu actions for Add Section (INSTALL/UNINSTALL/UPGRADE/ARP)
        private void AddSection_INSTALL_Click(object sender, RoutedEventArgs e) => AddSectionForPrefix("INSTALL");
        private void AddSection_UNINSTALL_Click(object sender, RoutedEventArgs e) => AddSectionForPrefix("UNINSTALL");
        private void AddSection_UPGRADE_Click(object sender, RoutedEventArgs e) => AddSectionForPrefix("UPGRADE");
        private void AddSection_ARP_Click(object sender, RoutedEventArgs e) => AddSectionForPrefix("ARP");

        private void AddSectionForPrefix(string prefix)
        {
            var (latest, latestNumber) = GetLatestSectionForPrefix(prefix);
            string newSection = $"{prefix}{(latestNumber + 1 == 0 ? 1 : latestNumber + 1)}";

            if (latest != null)
            {
                CreateNewSection(latest, newSection);
            }
            else
            {
                CreateSectionWithValues(newSection, new Dictionary<string, string>());
            }
            Logger.Info($"AddSectionForPrefix('{prefix}') created '{newSection}'.");
        }

        private (string? latest, int latestNumber) GetLatestSectionForPrefix(string prefix)
        {
            int max = 0; string? latest = null;
            foreach (var k in iniSections.Keys)
            {
                if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var numPart = k.Substring(prefix.Length);
                    if (int.TryParse(numPart, out int n))
                    {
                        if (n >= max)
                        {
                            max = n;
                            latest = k;
                        }
                    }
                }
            }
            return (latest, max);
        }

        private void CreateSectionWithValues(string newSection, Dictionary<string, string> values)
        {
            if (iniSections.ContainsKey(newSection))
            {
                MessageBox.Show($"Section '{newSection}' already exists!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Snapshot before change (clears redo)
            SaveSnapshot();

            iniSections[newSection] = new Dictionary<string, string>(values);

            var uiSection = new IniSection(newSection);
            foreach (var kv in values)
                uiSection.Entries.Add(new IniEntry { Key = kv.Key, Value = kv.Value });

            // Place at end or after related groups (simple: append)
            IniSectionList.Add(uiSection);

            SaveIniFile();
            RefreshIniContent();
            SelectedSection = uiSection;
        }

        // ContextMenu actions for Configuration (MACHINESPECIFIC/USERSPECIFIC/SERVICECONTROL/TAG)
        private void Configure_MACHINESPECIFIC_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("MACHINESPECIFIC");
        private void Configure_USERSPECIFIC_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("USERSPECIFIC");
        private void Configure_SERVICECONTROL_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("SERVICECONTROL");
        private void Configure_TAG_Click(object sender, RoutedEventArgs e) => OpenConfigurationFor("TAG");

        private void OpenConfigurationFor(string sectionName)
        {
            var existingKey = iniSections.Keys.FirstOrDefault(k => k.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (existingKey == null)
            {
                CreateSectionWithValues(sectionName, new Dictionary<string, string>());
                existingKey = sectionName;
            }

            // Select the section in UI
            var ui = IniSectionList.FirstOrDefault(s => s.Name.Equals(existingKey, StringComparison.OrdinalIgnoreCase));
            if (ui != null)
                SelectedSection = ui;

            // Reuse Postconfig dialog logic; preselect the target section
            var postWindow = new Postconfig(this, existingKey);
            postWindow.Show();
            Logger.Info($"Configuration opened for '{existingKey}'.");
        }

        private void btnRemoveSection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSection == null)
            {
                Logger.Info("Select a section first before clicking on REMOVE SECTION BUTTON!");
                MessageBox.Show("Select a section first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string section = SelectedSection.Name;

            // Confirmation prompt before deletion
            var result = MessageBox.Show(
                $"Are you sure you want to remove the '{section}' section?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveSnapshot();

                // Remove from dictionary and UI list
                iniSections.Remove(section);
                var toRemove = IniSectionList.FirstOrDefault(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase));
                if (toRemove != null) IniSectionList.Remove(toRemove);

                // Adjust selection
                SelectedSection = IniSectionList.FirstOrDefault();

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
            // Any new change invalidates redo history
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
                IniSectionList.Select(section =>
                    $"[{section.Name}]{Environment.NewLine}" +
                    string.Join(Environment.NewLine, section.Entries.Select(e => $"{e.Key}={e.Value}"))
                )
            );

            HighlightIniContent(content); //  Use the rich text formatter here
        }

        public void SaveIniFile()
        {
            try
            {
                var dict = IniSectionList.ToDictionary(
                    sec => sec.Name,
                    sec => sec.Entries.ToDictionary(ent => ent.Key, ent => ent.Value)
                );

                IniFileHelper.SaveIniFile(iniFilePath, dict);
                Logger.Info("INI file saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving INI file");
                MessageBox.Show($"Error saving INI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (sender is Button btn && btn.Tag is string keyToRemove && SelectedSection != null)
            {
                string section = SelectedSection.Name;
                var result = MessageBox.Show($"Remove key '{keyToRemove}' from section '{section}'?", "Confirm Delete",
                                             MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (iniSections.ContainsKey(section) && iniSections[section].ContainsKey(keyToRemove))
                    {
                        var snapshotBeforeDelete = IniFileHelper.CloneIniSections(iniSections);
                        iniSections[section].Remove(keyToRemove);

                        // Reflect in UI model
                        var entry = SelectedSection.Entries.FirstOrDefault(e => e.Key == keyToRemove);
                        if (entry != null) SelectedSection.Entries.Remove(entry);

                        undoSnapshots.Push(snapshotBeforeDelete);
                        while (undoSnapshots.Count > MaxUndoSnapshots)
                            _ = undoSnapshots.Pop();
                        // New change invalidates redo
                        redoSnapshots.Clear();

                        SaveIniFile();
                        RefreshIniContent();
                        Logger.Info($"Key '{keyToRemove}' removed from section '{section}'");
                    }
                }
            }
        }

    }
}
