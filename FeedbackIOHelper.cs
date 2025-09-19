using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace PackageConsole
{
    public static class FeedbackIOHelper
    {
        private static readonly string feedbackDir = PathManager.FeedbackPath;

        public static void EnsureDirectory() => Directory.CreateDirectory(feedbackDir);

        public static List<FeedbackEntry> LoadLatestFeedbacks()
        {
            EnsureDirectory();
            var allEntries = new List<FeedbackEntry>();

            foreach (var file in Directory.GetFiles(feedbackDir, "feedback_*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    List<FeedbackEntry> entries;

                    if (json.TrimStart().StartsWith("["))
                    {
                        // JSON is an array
                        entries = JsonConvert.DeserializeObject<List<FeedbackEntry>>(json);
                    }
                    else
                    {
                        // JSON is a single FeedbackEntry object
                        var entry = JsonConvert.DeserializeObject<FeedbackEntry>(json);
                        entries = new List<FeedbackEntry> { entry };
                    }

                    allEntries.AddRange(entries);
                }
                catch (Exception ex)
                {
                    // Optional: log or handle bad files
                    Console.WriteLine($"Failed to parse file {file}: {ex.Message}");
                }
            }

            return allEntries.OrderByDescending(e => e.Time).Take(50).ToList();
        }

        public static void SaveFeedback(FeedbackEntry entry, string? screenshotPath = null)
        {
            EnsureDirectory();
            string filename = Path.Combine(feedbackDir, $"feedback_{entry.User}.json");

            List<FeedbackEntry> entries = File.Exists(filename)
                ? JsonConvert.DeserializeObject<List<FeedbackEntry>>(File.ReadAllText(filename))
                : new List<FeedbackEntry>();

            entries.Add(entry);
            File.WriteAllText(filename, JsonConvert.SerializeObject(entries, Newtonsoft.Json.Formatting.Indented));

            if (!string.IsNullOrEmpty(screenshotPath) && File.Exists(screenshotPath))
            {
                string dest = Path.Combine(feedbackDir, $"screenshot_{entry.Id}{Path.GetExtension(screenshotPath)}");
                File.Copy(screenshotPath, dest, true);
            }
        }

        public static bool UpdateFeedbackResponse(Guid entryId, string response, string status, string adminUser)
        {
            string[] files = Directory.GetFiles(feedbackDir, "feedback_*.json");

            foreach (string file in files)
            {
                var entries = JsonConvert.DeserializeObject<List<FeedbackEntry>>(File.ReadAllText(file));
                var entry = entries.FirstOrDefault(e => e.Id == entryId);

                if (entry != null)
                {
                    if (Environment.UserName != adminUser) return false;

                    entry.Response = response;
                    entry.Status = status;

                    File.WriteAllText(file, JsonConvert.SerializeObject(entries, Newtonsoft.Json.Formatting.Indented));
                    return true;
                }
            }
            return false;
        }
    }
}
