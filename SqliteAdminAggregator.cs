using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using NLog;

namespace PackageConsole.Data
{
    public static class SqliteAdminAggregator
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(SqliteAdminAggregator));

        public static List<FeedbackEntry> LoadAllFeedbacks()
        {
            var allEntries = new List<FeedbackEntry>();
            string centralDir = Path.GetDirectoryName(PathManager.CentralUserDb);

            if (!Directory.Exists(centralDir))
            {
                Logger.Warn("Central feedback directory not found: " + centralDir);
                return allEntries;
            }

            var dbFiles = Directory.GetFiles(centralDir, "*_feedback.db");
            foreach (var dbPath in dbFiles)
            {
                try
                {
                    using var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
                    conn.Open();

                    string query = "SELECT * FROM Feedback ORDER BY Time DESC LIMIT 100";
                    using var cmd = new SQLiteCommand(query, conn);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var entry = new FeedbackEntry
                        {
                            User = reader["User"].ToString(),
                            Type = reader["Type"].ToString(),
                            Message = reader["Message"].ToString(),
                            Time = DateTime.Parse(reader["Time"].ToString()),
                            Response = reader["Response"].ToString(),
                            Severity = reader["Severity"].ToString(),
                            ScreenshotPath = reader["ScreenshotPath"].ToString()
                        };
                        allEntries.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to load feedback from: {dbPath}");
                }
            }

            return allEntries;
        }
    }
}
