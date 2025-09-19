using Microsoft.Win32;
using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;

namespace PackageConsole
{
    public partial class Postconfig : Window
    {
        private Dictionary<string, List<string>> savedData = new();
        private Dictionary<string, List<string>> savedTagData = new();
        //private IniConsolePage iniConsolePage;
        //private PreviousApps PreviousApps;
        private IIniContext iniContext;
        private static readonly Logger Logger = LogManager.GetLogger(nameof(Postconfig));

        public Postconfig(Page pageContext)
        {
            InitializeComponent();

            if (pageContext is IIniContext context)
            {
                iniContext = context;
                LoadIniSections();
                Logger.Info("Postconfig window initialized with valid INI context.");
            }
            else
            {
                Logger.Error("Invalid page context passed to Postconfig window.");
                MessageBox.Show("Unsupported page type passed to Postconfig window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // Overload to preselect a section and open with the correct UI immediately
        public Postconfig(Page pageContext, string initialSection)
            : this(pageContext)
        {
            try
            {
                // Ensure section exists in list; LoadIniSections already set ItemsSource
                if (cmbSection.ItemsSource is System.Collections.IEnumerable)
                {
                    var match = cmbSection.Items.Cast<object>()
                        .FirstOrDefault(it => string.Equals(it?.ToString(), initialSection, System.StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        cmbSection.SelectedItem = match;
                    }
                    else
                    {
                        // If not present (shouldn’t happen), fall back to setting text
                        cmbSection.SelectedItem = initialSection;
                    }
                }
                else
                {
                    cmbSection.SelectedItem = initialSection;
                }

                // Trigger the layout for the selected section
                cmbSection_SelectionChanged(null, null);
            }
            catch (System.Exception ex)
            {
                Logger.Warn(ex, $"Failed to preselect section '{initialSection}' in Postconfig");
            }
        }


        private void LoadIniSections()
        {
            var sectionNames = iniContext?.GetSectionNames();
            if (sectionNames != null)
            {
                var excludedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "PRODUCT INFO", "APP CONTROL", "HEALTH CHECK", "FOLDERPERM",
                        "FILEPERM", "REGPERM", "FILELOCK"
                    };

                var filtered = sectionNames
                    .Where(section => !excludedSections.Contains(section))
                    .ToList();

                cmbSection.ItemsSource = filtered;
                dynamicFieldsPanel.Visibility = Visibility.Collapsed;
                Logger.Info($"INI sections loaded into Postconfig: {string.Join(", ", sectionNames)}");
            }
        }

        private void AddTextBox(string labelText)
        {
            Grid grid = new() { Margin = new Thickness(0, 5, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

            TextBlock label = new()
            {
                Text = labelText + ":",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBox textBox = new() { Width = 200, FontSize = 14, Name = "txt" + labelText.Replace(" ", "") , ToolTip = VariableHelper.GetTooltipText() };
            VariableHelper.AttachAutocompleteBehavior(textBox);
            Grid.SetColumn(label, 0);
            Grid.SetColumn(textBox, 1);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            stackDynamicInputs.Children.Add(grid);
        }

        // SERVICECONTROL specific inputs: Service Name + Action
        private void AddServiceControlFields()
        {
            Grid grid = new() { Margin = new Thickness(0, 5, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

            // Service Name
            TextBlock lblService = new()
            {
                Text = "Service Name:",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBox txtService = new() { Name = "txtServiceName", Width = 200, FontSize = 14, ToolTip = VariableHelper.GetTooltipText() };
            VariableHelper.AttachAutocompleteBehavior(txtService);
            Grid.SetColumn(lblService, 0);
            Grid.SetColumn(txtService, 1);

            // Action
            TextBlock lblAction = new()
            {
                Text = "Action:",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            ComboBox cmbAction = new() { Name = "cmbServiceAction", Width = 200, Height = 20 };
            foreach (var action in new[] { "STOP", "START", "RESUME", "RESTART", "SUSPEND" })
            {
                cmbAction.Items.Add(new ComboBoxItem { Content = action });
            }
            Grid.SetColumn(lblAction, 3);
            Grid.SetColumn(cmbAction, 4);

            grid.Children.Add(lblService);
            grid.Children.Add(txtService);
            grid.Children.Add(lblAction);
            grid.Children.Add(cmbAction);

            stackDynamicInputs.Children.Add(grid);
        }
        private void cmbSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSection.SelectedItem == null) return;

            string selectedSection = cmbSection.SelectedItem.ToString();
            stackDynamicInputs.Children.Clear(); //  Clear previous dynamic inputs

            //  MACHINESPECIFIC & USERSPECIFIC
            if (selectedSection.Equals("MACHINESPECIFIC", StringComparison.OrdinalIgnoreCase) ||
                selectedSection.Equals("USERSPECIFIC", StringComparison.OrdinalIgnoreCase))
            {
                dynamicFieldsPanel.Visibility = Visibility.Visible;
                addSectionPanel.Visibility = Visibility.Collapsed;
                txtNewSection.Visibility = Visibility.Collapsed;
                lblNewSection.Visibility = Visibility.Collapsed;
                tagSectionPanel.Visibility = Visibility.Collapsed;
                stackDynamicInputs.Visibility = Visibility.Visible;

                // Show ValueType controls for these sections
                lblValueType.Visibility = Visibility.Visible;
                cmbValueType.Visibility = Visibility.Visible;

                // Load and rehydrate from INI
                savedData.Clear();

                if (iniContext.HasSection(selectedSection))
                {
                    var values = iniContext.GetKeyValues(selectedSection);

                    foreach (var kvp in values)
                    {
                        if (!kvp.Key.StartsWith(";") && !string.IsNullOrWhiteSpace(kvp.Key))
                        {
                            string pathType = new string(kvp.Key.TakeWhile(char.IsLetter).ToArray());
                            string formattedLine = $"{kvp.Key} = {kvp.Value}";

                            if (!savedData.ContainsKey(pathType))
                                savedData[pathType] = new List<string>();

                            savedData[pathType].Add(formattedLine);
                        }
                    }

                    txtPostconfigValues.Text = string.Join("\n", savedData.SelectMany(kvp => kvp.Value));
                }
            }
            // SERVICECONTROL (similar to MACHINESPECIFIC/USERSPECIFIC but uses Service fields instead of ValueType)
            else if (selectedSection.Equals("SERVICECONTROL", StringComparison.OrdinalIgnoreCase))
            {
                dynamicFieldsPanel.Visibility = Visibility.Visible; // show PathType + Key row
                addSectionPanel.Visibility = Visibility.Collapsed;
                txtNewSection.Visibility = Visibility.Collapsed;
                lblNewSection.Visibility = Visibility.Collapsed;
                tagSectionPanel.Visibility = Visibility.Collapsed;
                stackDynamicInputs.Visibility = Visibility.Visible;

                // Hide ValueType controls; our own fields will be shown
                lblValueType.Visibility = Visibility.Collapsed;
                cmbValueType.Visibility = Visibility.Collapsed;

                // Rehydrate preview from existing INI
                savedData.Clear();
                if (iniContext.HasSection(selectedSection))
                {
                    var values = iniContext.GetKeyValues(selectedSection);
                    foreach (var kvp in values)
                    {
                        if (!kvp.Key.StartsWith(";") && !string.IsNullOrWhiteSpace(kvp.Key))
                        {
                            string pathType = new string(kvp.Key.TakeWhile(char.IsLetter).ToArray());
                            string formattedLine = $"{kvp.Key} = {kvp.Value}";
                            if (!savedData.ContainsKey(pathType))
                                savedData[pathType] = new List<string>();
                            savedData[pathType].Add(formattedLine);
                        }
                    }
                    txtPostconfigValues.Text = string.Join("\n", savedData.SelectMany(kvp => kvp.Value));
                }

                // Show ServiceName + Action inputs
                AddServiceControlFields();
            }
            //  INSTALL1, UNINSTALL1, ARP1, UPGRADE1
            else if (selectedSection.StartsWith("INSTALL") || selectedSection.StartsWith("UNINSTALL") ||
                     selectedSection.StartsWith("ARP") || selectedSection.StartsWith("UPGRADE"))
            {
                dynamicFieldsPanel.Visibility = Visibility.Collapsed;
                addSectionPanel.Visibility = Visibility.Visible;
                txtNewSection.Visibility = Visibility.Collapsed;
                lblNewSection.Visibility = Visibility.Collapsed;
                tagSectionPanel.Visibility = Visibility.Collapsed;
                PopulateSectionData(selectedSection);
            }
            // TAG
            else if (selectedSection.Equals("TAG", StringComparison.OrdinalIgnoreCase))
            {
                dynamicFieldsPanel.Visibility = Visibility.Collapsed;
                addSectionPanel.Visibility = Visibility.Collapsed;
                stackDynamicInputs.Visibility = Visibility.Collapsed;

                // Show TAG section fields
                tagSectionPanel.Visibility = Visibility.Visible;

                // Auto-assign next Tag key
                txtTagKey.Text = GetNextTagKey();

            }
            else
            {
                //  Reset to default when other sections are selected
                dynamicFieldsPanel.Visibility = Visibility.Collapsed;
                addSectionPanel.Visibility = Visibility.Collapsed;
                txtNewSection.Visibility = Visibility.Collapsed;
                lblNewSection.Visibility = Visibility.Collapsed;
                tagSectionPanel.Visibility = Visibility.Collapsed;
                stackDynamicInputs.Children.Clear();
            }
        }
        private void PopulateSectionData(string selectedSection)
        {
            stackDynamicInputs.Children.Clear();

            if (!iniContext.HasSection(selectedSection))
            {
                MessageBox.Show($"No data found for {selectedSection}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Dictionary<string, string> keyValues = iniContext.GetKeyValues(selectedSection);

            foreach (var kvp in keyValues)
            {
                AddLabelTextBox(kvp.Key, kvp.Value);
            }

            // Determine next available section name
            txtNewSection.Text = GetNextSectionName(selectedSection);
        }
        private string GetNextSectionName(string baseSection)
        {
            string sectionPrefix = new string(baseSection.TakeWhile(char.IsLetter).ToArray()); // Extract "UPGRADE"
            int sectionNumber = 1;

            // Find the highest existing number
            while (iniContext.HasSection($"{sectionPrefix}{sectionNumber}"))
            {
                sectionNumber++;
            }

            return $"{sectionPrefix}{sectionNumber}";  //  Correctly returns "Upgrade2", "Upgrade3"...
        }
        private void btnAddSection_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSection.SelectedItem == null) return;

            string originalSection = cmbSection.SelectedItem.ToString();

            //  Ensure this logic only applies to INSTALL1, UNINSTALL1, etc.
            if (!(originalSection.StartsWith("INSTALL") || originalSection.StartsWith("UNINSTALL") ||
                  originalSection.StartsWith("ARP") || originalSection.StartsWith("UPGRADE")))
            {
                return;
            }

            string newSection = GetNextSectionName(originalSection);  //  Generate section name only when clicked
            string existingSections = string.Join(", ", iniContext.GetSectionNames());
          //  MessageBox.Show($"Existing Sections: {existingSections}", "Debug");
            if (iniContext.HasSection(newSection))
            {
                MessageBox.Show($"Section '{newSection}' already exists! Existing sections: {existingSections}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtNewSection.Text = newSection;  //  Display new section name in UI
            lblNewSection.Visibility = Visibility.Visible;  //  Show "New Section" label
            txtNewSection.Visibility = Visibility.Visible;  // Show "New Section" textbox

            CreateNewSection(originalSection, newSection);

            //  Refresh UI
            cmbSection.ItemsSource = iniContext.GetSectionNames();
            cmbSection.SelectedItem = newSection;
        }
        public void CreateNewSection(string originalSection, string newSection)
        {
            if (!iniContext.IniSections .ContainsKey(originalSection))
            {
                MessageBox.Show($"Error: Section '{originalSection}' not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Warn($"Original section '{originalSection}' not found.");
                return;
            }

            // Debugging: Show existing sections before checking
            string existingSections = string.Join(", ", iniContext.IniSections .Keys);
            MessageBox.Show($"Checking for duplicate section: '{newSection}'\nExisting Sections: {existingSections}", "Debug");

            if (iniContext.IniSections .ContainsKey(newSection))
            {
                Logger.Warn($"Attempted to create duplicate section: {newSection}");
                MessageBox.Show($"Section already exists '{newSection}'!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //  Copy key-value pairs from original section
            Dictionary<string, string> copiedValues = new Dictionary<string, string>(iniContext.IniSections [originalSection]);

            //  Insert new section immediately after the original section
            Dictionary<string, Dictionary<string, string>> newIniSections  = new();
            bool sectionInserted = false;

            foreach (var entry in iniContext.IniSections )
            {
                newIniSections[entry.Key] = entry.Value;

                if (entry.Key == originalSection && !sectionInserted)
                {
                    newIniSections[newSection] = copiedValues;  // Insert right after the original
                    sectionInserted = true;
                }
            }

            iniContext.UpdateIniSections(newIniSections);  //  Use the update method
            iniContext.SaveIniFile();  // Save the updated order to the INI file
            iniContext.RefreshIniContent();  //  Update UI
            Logger.Info($"New section '{newSection}' created after '{originalSection}' with {copiedValues.Count} keys.");
        }
        private void AddLabelTextBox(string label, string value)
        {
            Grid grid = new() { Margin = new Thickness(0, 5, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

            TextBlock lbl = new()
            {
                Text = label + ":",
                Width= 150,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBox txtBox = new()
            {
                Width = 200,
                FontSize = 12,
                Name = "txt" + label.Replace(" ", ""),
                Text = value,
                ToolTip = VariableHelper.GetTooltipText()
            };
            // Add auto-complete behavior
            VariableHelper.AttachAutocompleteBehavior(txtBox);
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(txtBox, 1);

            grid.Children.Add(lbl);
            grid.Children.Add(txtBox);
            stackDynamicInputs.Children.Add(grid);
        }
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSection.SelectedItem == null) return;

            string selectedSection = cmbSection.SelectedItem.ToString();
            txtPostconfigValues.Text = ""; //  Clear previous section data before saving new one

            // Keep existing logic for MACHINESPECIFIC & USERSPECIFIC
            if (selectedSection.Equals("MACHINESPECIFIC", StringComparison.OrdinalIgnoreCase) ||
                selectedSection.Equals("USERSPECIFIC", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info($"Saving data for section: {selectedSection}");
                if (cmbPathType.SelectedItem is ComboBoxItem pathTypeItem &&
                    cmbValueType.SelectedItem is ComboBoxItem valueTypeItem)
                {
                    string pathType = pathTypeItem.Content?.ToString();
                    if (string.IsNullOrEmpty(pathType)) return;

                    string key = txtKey.Text;

                    List<string> values = stackDynamicInputs.Children
                        .OfType<Grid>()
                        .Select(g => g.Children.Count > 1 && g.Children[1] is TextBox textBox ? textBox.Text.Trim() : "")
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();

                    if (!values.Any())
                    {
                        MessageBox.Show("Please fill all fields!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string formattedValue = $"{key} = {string.Join(",", values)}";

                    if (!savedData.ContainsKey(pathType))
                        savedData[pathType] = new List<string>();

                    savedData[pathType].Add(formattedValue);
                    txtPostconfigValues.Text = string.Join("\n", savedData.SelectMany(kvp => kvp.Value));

                    txtKey.Text = $"{pathType}{savedData[pathType].Count + 1}";
                    stackDynamicInputs.Children.Clear();
                    cmbValueType.SelectedIndex = -1;
                    Logger.Info($"Section '{selectedSection}' updated with dynamic key-value entries.");
                }
            }
            else if (selectedSection.Equals("SERVICECONTROL", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info("Saving data for section: SERVICECONTROL");

                if (cmbPathType.SelectedItem is not ComboBoxItem pathTypeItem)
                {
                    MessageBox.Show("Please select Path Type (PRE/POST phases).", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string pathType = pathTypeItem.Content?.ToString();
                if (string.IsNullOrWhiteSpace(pathType)) return;

                string key = txtKey.Text?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageBox.Show("Key is empty. Select a Path Type to auto-generate the key.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Retrieve ServiceName and Action from dynamic inputs
                var serviceNameTB = stackDynamicInputs.Children
                    .OfType<Grid>()
                    .SelectMany(g => g.Children.OfType<TextBox>())
                    .FirstOrDefault(tb => tb.Name == "txtServiceName");
                var actionCB = stackDynamicInputs.Children
                    .OfType<Grid>()
                    .SelectMany(g => g.Children.OfType<ComboBox>())
                    .FirstOrDefault(cb => cb.Name == "cmbServiceAction");

                string serviceName = serviceNameTB?.Text.Trim() ?? string.Empty;
                string action = (actionCB?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(action))
                {
                    MessageBox.Show("Please provide Service Name and select Action.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string formattedValue = $"{key} = {serviceName},{action}";

                if (!savedData.ContainsKey(pathType))
                    savedData[pathType] = new List<string>();

                savedData[pathType].Add(formattedValue);
                txtPostconfigValues.Text = string.Join("\n", savedData.SelectMany(kvp => kvp.Value));

                // Prepare next key and reset inputs
                txtKey.Text = $"{pathType}{savedData[pathType].Count + 1}";
                stackDynamicInputs.Children.Clear();
                AddServiceControlFields();
                Logger.Info("SERVICECONTROL entry added: " + formattedValue);
            }
            else if (selectedSection.StartsWith("INSTALL") || selectedSection.StartsWith("UNINSTALL") ||
                     selectedSection.StartsWith("ARP") || selectedSection.StartsWith("UPGRADE"))
            {
                Logger.Info($"Section '{selectedSection}' updated with dynamic key-value entries.");
                Dictionary<string, string> newValues = new();

                foreach (Grid grid in stackDynamicInputs.Children.OfType<Grid>())
                {
                    if (grid.Children[1] is TextBox txtBox)
                    {
                        string key = ((TextBlock)grid.Children[0]).Text.Replace(":", "").Trim();
                        newValues[key] = txtBox.Text.Trim();
                    }
                }

                txtPostconfigValues.Text = string.Join("\n", newValues.Select(kvp => $"{kvp.Key}={kvp.Value}")); //  Update display with correct section's values

                iniContext.UpdateIniSection(selectedSection, newValues); //  Save changes in INI
                Logger.Info($"Updated {selectedSection} with new values.");
            }
            //  New Logic: Save TAG values in incremental format
            else if (selectedSection.Equals("TAG", StringComparison.OrdinalIgnoreCase))
            {
                string tagKey = txtTagKey.Text.Trim();
                string appName = txtAppName.Text.Trim();
                string appGuid = txtAppGuid.Text.Trim();
                string enabled = (cmbTagEnabled.SelectedItem as ComboBoxItem)?.Content.ToString();

                if (string.IsNullOrWhiteSpace(appName) || string.IsNullOrWhiteSpace(appGuid))
                {
                    MessageBox.Show("Please fill APPNAME and APPGUID.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string formattedValue = $"{tagKey} = {appName},{appGuid},{enabled}";

                // Store to savedTagData
                if (!savedTagData.ContainsKey("TAG"))
                    savedTagData["TAG"] = new List<string>();

                savedTagData["TAG"].Add(formattedValue);

                //  Update txtPostconfigValues
                txtPostconfigValues.Text = string.Join("\n", savedTagData["TAG"]);

                //  Update TagKey to TAG{count+1}
                txtTagKey.Text = $"TAG{savedTagData["TAG"].Count + 1}";

                //  Clear inputs
                txtAppName.Clear();
                txtAppGuid.Clear();
                cmbTagEnabled.SelectedIndex = 0;
                Logger.Info($"New TAG entry added: {formattedValue}");
            }
        }
        private string GetNextTagKey()
        {
            int tagCounter = 1;
            var lines = txtPostconfigValues.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            while (lines.Any(line => line.StartsWith($"TAG{tagCounter}=", StringComparison.OrdinalIgnoreCase)))
            {
                tagCounter++;
            }

            return $"TAG{tagCounter}";
        }
        public void AppendKeyValues(string section, Dictionary<string, string> newEntries)
        {
            if (!iniContext.IniSections.ContainsKey(section))
                iniContext.IniSections[section] = new Dictionary<string, string>();

            foreach (var entry in newEntries)
            {
                iniContext.IniSections[section][entry.Key] = entry.Value;
                Logger.Info($"Appended key '{entry.Key}' to section '{section}'");
            }

            iniContext.SaveIniFile();
            iniContext.RefreshIniContent();

        }
        private void AutoPopulateKeyValues(string selectedSection)
        {
            stackDynamicInputs.Children.Clear(); //  Clear previous fields

            if (!iniContext.HasSection(selectedSection)) return;

            Dictionary<string, string> keyValues = iniContext.GetKeyValues(selectedSection);

            foreach (var kvp in keyValues)
            {
                AddLabelTextBox(kvp.Key, kvp.Value); //  Add labels & text boxes
            }

            btnSave.Visibility = Visibility.Visible; //  Show Save button
        }
        private void cmbPathType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPathType.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedType = selectedItem.Content?.ToString();
                if (string.IsNullOrEmpty(selectedType)) return;

                if (!savedData.ContainsKey(selectedType))
                    savedData[selectedType] = new List<string>();

                int newKeyIndex = savedData[selectedType].Count + 1;
                txtKey.Text = $"{selectedType}{newKeyIndex}";
            }
        }
        private void cmbValueType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            stackDynamicInputs.Children.Clear();

            if (cmbValueType.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedValueType = selectedItem.Content?.ToString();
                if (string.IsNullOrEmpty(selectedValueType)) return;

                AddDynamicFields(selectedValueType);
            }
        }
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtPostconfigValues.Clear();  //  Clear TextBox
            cmbSection.SelectedIndex = -1;  //  Clear Section ComboBox
            cmbPathType.SelectedIndex = -1;  // Clear Path Type
            cmbValueType.SelectedIndex = -1;  //  Clear Value Type
            txtKey.Text = "";  //  Clear Key field
            stackDynamicInputs.Children.Clear(); //  Clear dynamically added fields
            Logger.Info("Postconfig fields cleared by user.");
        }
        private void btnLoadToINI_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSection.SelectedItem == null) return;

            string selectedSection = cmbSection.SelectedItem.ToString();
            Dictionary<string, string> updatedValues = new();

            // Parse key-values from `txtPostconfigValues` (ignore comments and blanks)
            var lines = txtPostconfigValues.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    updatedValues[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Update INI data (persists and refreshes UI via context)
            Logger.Info($"Loading Postconfig values into INI section: {selectedSection} with {updatedValues.Count} entries");
            iniContext.UpdateIniSection(selectedSection, updatedValues);
        }
        private void LoadExistingData()
        {
            cmbSection.Items.Clear(); //  Clear items before setting ItemsSource
            cmbSection.ItemsSource = iniContext.GetSectionNames();
        }
        private void AddDynamicFields(string valueType)
        {
            stackDynamicInputs.Children.Clear();

            switch (valueType)
            {
                case "FILE COPY":
                    AddTextBox("Source");
                    AddTextBox("Destination");
                    break;
                case "Delete File":
                    AddTextBox("Destination");
                    break;
                case "Directory Copy":
                    AddTextBox("Source");
                    AddTextBox("Destination");
                    break;
                case "RegWrite Value":
                    AddTextBox("Key");
                    AddTextBox("SubKey");
                    AddTextBox("Value");
                    AddTextBox("Type");
                    break;
                case "RegDelete Value":
                    AddTextBox("Key");
                    AddTextBox("SubKey");
                    break;
                case "RegDelete Key":
                    AddTextBox("Key");
                    break;
                default:
                    MessageBox.Show("Unknown Value Type!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
            }
        }
        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (txtPostconfigValues.SelectedText.Length == 0)
            {
                MessageBox.Show("Select a line to edit!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedEntry = txtPostconfigValues.SelectedText;
            txtKey.Text = selectedEntry.Split('=')[0].Trim();
            stackDynamicInputs.Children.Clear();

            foreach (var value in selectedEntry.Split('=')[1].Split(','))
            {
                AddTextBox(value.Trim());
            }
        }
        private void btnSaveInFile_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
