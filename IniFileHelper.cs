using System;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace PackageConsole
{
    public static class IniFileHelper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        //private static string currentSection;

        /// <summary>
        /// Parses the given INI file and returns both structured sections and raw content.
        /// </summary>
        /// <param name="filePath">The path of the INI file to parse.</param>
        /// <param name="rawContent">The raw content of the INI file, including comments and blank lines.</param>
        /// <returns>A dictionary containing parsed sections and their key-value pairs.</returns>
        public static Dictionary<string, Dictionary<string, string>> ParseIniFile(string filePath, out List<string> rawContent)
        {
            var sections = new Dictionary<string, Dictionary<string, string>>();
            rawContent = new List<string>();

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"INI file not found: {filePath}");

            string currentSection = null;

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmedLine = line.Trim();

                // Add every line to raw content for the full display in the INI Content Area
                rawContent.Add(line);

                // Ignore empty lines or comments for parsing key-value pairs
               // if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                 //   continue;

                // Detect section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Trim('[', ']');
                    if (!sections.ContainsKey(currentSection))
                        sections[currentSection] = new Dictionary<string, string>();
                }
                else if (currentSection != null && trimmedLine.Contains("="))
                {
                    // Parse key-value pairs
                    var parts = trimmedLine.Split(new[] { '=' }, 2); // Split into two parts
                    var key = parts[0].Trim();
                    var value = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                    if (!string.IsNullOrEmpty(key))
                    {
                        sections[currentSection][key] = value; // Allow empty values
                    }
                    else
                    {
                        Logger.Warn($"Key is missing in section [{currentSection}]: {trimmedLine}");
                    }
                }
                else
                {
                    Logger.Warn($"Malformed line in section [{currentSection}]: {trimmedLine}");
                }
            }

            return sections;
        }

        /// <summary>
        /// Saves the given INI sections and their key-value pairs to a file.
        /// </summary>
        /// <param name="filePath">The path of the file to save to.</param>
        /// <param name="sections">The sections and their key-value pairs to save.</param>
        public static void SaveIniFile(string filePath, Dictionary<string, Dictionary<string, string>> sections)
        {
            try
            {
                var iniContent = new List<string>();

                foreach (var section in sections)
                {
                    iniContent.Add($"[{section.Key}]");
                    foreach (var kvp in section.Value)
                    {
                        iniContent.Add($"{kvp.Key}={kvp.Value}");
                    }
                    iniContent.Add(""); // Add a blank line between sections
                }

                File.WriteAllText(filePath, string.Join(Environment.NewLine, iniContent));
                Logger.Info($"INI file saved successfully to: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving INI file: {ex}");
                throw;
            }
        }

        //Summary - this function will help you to save the file as is with comments. 
        public static void SaveIniFileWithComments(string filePath, Dictionary<string, Dictionary<string, string>> sections, List<string> rawContent)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    string currentSection = null;

                    foreach (string line in rawContent)
                    {
                        if (line.Trim().StartsWith(";") || string.IsNullOrWhiteSpace(line))
                        {
                            // Write comments and empty lines as is
                            writer.WriteLine(line);
                        }
                        else if (line.Trim().StartsWith("["))
                        {
                            // Write the section headers
                            writer.WriteLine(line);
                            currentSection = line.Trim().Trim('[', ']');
                        }
                        else
                        {
                            var keyValue = line.Split(new char[] { '=' }, 2);
                            if (keyValue.Length == 2 && currentSection != null && sections.ContainsKey(currentSection) && sections[currentSection].ContainsKey(keyValue[0].Trim()))
                            {
                                writer.WriteLine($"{keyValue[0].Trim()}={sections[currentSection][keyValue[0].Trim()]}");
                            }
                            else
                            {
                                writer.WriteLine(line);
                            }
                        }
                    }
                }

                Logger.Info($"INI file saved successfully to: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving INI file: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Deep clones a nested dictionary of INI sections and key-values.
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> CloneIniSections(Dictionary<string, Dictionary<string, string>> original)
        {
            var clone = new Dictionary<string, Dictionary<string, string>>();
            foreach (var section in original)
            {
                clone[section.Key] = new Dictionary<string, string>(section.Value);
            }
            return clone;
        }

    }
}
