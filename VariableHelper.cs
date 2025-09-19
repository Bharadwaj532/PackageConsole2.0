using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PackageConsole
{
    public static class VariableHelper
    {
        private static readonly Dictionary<string, string> BuiltInVariables = new()
        {
            { "%(PkgFiles)s", @"C:\Temp\PackageConsole\<AppName>\<AppVer>\Altiris\Files" },
            { "%(ProgramFiles)s", @"C:\PrgramFiles"},
            { "%(ProgramFilesx86)s", @"C:\PrgramFiles x86"},
            { "%(ProgramData)s", @"C:\ProgramData" },
            { "%APPDATA%", @"C:\Users\<USERNAME>\AppData\Roaming" },
            { "%TEMP%", @"C:\Temp" },
            { "HKLM",@"HKLM\Software"}
        };

        public static Dictionary<string, Dictionary<string, string>> IniSections { get; set; }

        // Editable overrides persisted to disk. These take precedence over built-ins and INI VARS.
        private static Dictionary<string, string> TooltipOverrides = LoadOverrides();

        private static Dictionary<string, string> LoadOverrides()
        {
            try
            {
                var path = PathManager.TooltipsConfigPath;
                if (!File.Exists(path)) return new Dictionary<string, string>();
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return data ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public static void SaveTooltipOverrides(Dictionary<string, string> map)
        {
            var path = PathManager.TooltipsConfigPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            TooltipOverrides = new Dictionary<string, string>(map);
        }

        public static void ReloadTooltipOverrides()
        {
            TooltipOverrides = LoadOverrides();
        }

        public static Dictionary<string, string> GetEditableVariables()
        {
            // Merge order: Built-ins -> INI VARS -> Overrides (highest precedence)
            var result = new Dictionary<string, string>(BuiltInVariables);
            if (IniSections != null && IniSections.ContainsKey("VARS"))
            {
                foreach (var kvp in IniSections["VARS"])
                {
                    string key = $"%{kvp.Key}%";
                    result[key] = kvp.Value;
                }
            }
            foreach (var kv in TooltipOverrides)
            {
                result[kv.Key] = kv.Value;
            }
            return result;
        }

        public static Dictionary<string, string> GetAllVariables()
        {
            return GetEditableVariables();
        }
        public static string TooltipText => GetTooltipText();
        public static string GetTooltipText()
        {
            return string.Join(Environment.NewLine, GetAllVariables().Select(kvp => $"{kvp.Key} = {kvp.Value}"));
        }
        public static List<string> GetTokens() => GetAllVariables().Keys.ToList();
        public static void AttachAutocompleteBehavior(TextBox txtBox)
        {
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = txtBox,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var listBox = new ListBox
            {
                ItemsSource = GetTokens(),
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                MaxHeight = 100,
                Width = txtBox.Width,
                IsTabStop = false
            };

            popup.Child = listBox;

            txtBox.TextChanged += (s, e) =>
            {
                string text = txtBox.Text;
                int caretPos = txtBox.CaretIndex;
                string tokenPrefix = GetLastToken(text, caretPos);

                if (tokenPrefix.StartsWith("%"))
                {
                    var matches = GetTokens().Where(t => t.StartsWith(tokenPrefix, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (matches.Any())
                    {
                        listBox.ItemsSource = matches;
                        listBox.SelectedIndex = 0;
                        popup.IsOpen = true;
                    }
                    else
                    {
                        popup.IsOpen = false;
                    }
                }
                else
                {
                    popup.IsOpen = false;
                }
            };

            txtBox.PreviewKeyDown += (s, e) =>
            {
                if (!popup.IsOpen) return;

                switch (e.Key)
                {
                    case Key.Down:
                        listBox.SelectedIndex = (listBox.SelectedIndex + 1) % listBox.Items.Count;
                        listBox.ScrollIntoView(listBox.SelectedItem);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        listBox.SelectedIndex = (listBox.SelectedIndex - 1 + listBox.Items.Count) % listBox.Items.Count;
                        listBox.ScrollIntoView(listBox.SelectedItem);
                        e.Handled = true;
                        break;
                    case Key.Enter:
                    case Key.Tab:
                        if (listBox.SelectedItem is string selectedToken)
                        {
                            ReplaceTokenAtCaret(txtBox, selectedToken);
                            popup.IsOpen = false;
                            e.Handled = true;
                        }
                        break;
                    case Key.Escape:
                        popup.IsOpen = false;
                        e.Handled = true;
                        break;
                }
            };

            listBox.MouseUp += (s, e) =>
            {
                if (listBox.SelectedItem is string selectedToken)
                {
                    ReplaceTokenAtCaret(txtBox, selectedToken);
                    popup.IsOpen = false;
                }
            };
        }
        private static string GetLastToken(string text, int caretPos)
        {
            if (string.IsNullOrEmpty(text) || caretPos <= 0) return string.Empty;

            int start = text.LastIndexOf('%', Math.Min(caretPos - 1, text.Length - 1));
            if (start == -1 || start >= text.Length) return string.Empty;

            int end = text.IndexOfAny(new[] { ' ', '\t', '\n', '\r' }, start);
            if (end == -1 || end > text.Length) end = text.Length;

            return text.Substring(start, end - start);
        }
        private static void ReplaceTokenAtCaret(TextBox textBox, string token)
        {
            int caretPos = textBox.CaretIndex;
            int start = textBox.Text.LastIndexOf('%', caretPos - 1);
            if (start == -1) return;

            int end = textBox.Text.IndexOfAny(new[] { ' ', '\t', '\n', '\r' }, start);
            if (end == -1) end = textBox.Text.Length;

            string newText = textBox.Text.Remove(start, end - start).Insert(start, token);
            textBox.Text = newText;
            textBox.CaretIndex = start + token.Length;
        }
    }
}
