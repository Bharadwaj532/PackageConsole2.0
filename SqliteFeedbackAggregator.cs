using System;
using System.Data.SQLite;
using System.IO;
using NLog;

namespace PackageConsole.Data
{
    public static class SqliteFeedbackAggregator
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(SqliteFeedbackAggregator));
        private static readonly string CentralFolder = Path.GetDirectoryName(PathManager.CentralUserDb);
        private static readonly string MasterDbPath = Path.Combine(CentralFolder ?? "", "FeedbackMaster.db");
        private static readonly string LastMergedFlag = Path.Combine(CentralFolder ?? "", "FeedbackMaster.last");

        public static void RunDailyMerge(bool force = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CentralFolder) || !Directory.Exists(CentralFolder))
                {
                    Logger.Warn("Central feedback folder not found.");
                    return;
                }

                if (!force && !ShouldMergeToday())
                {
                    Logger.Info("FeedbackMaster merge skipped (already done today).");
                    return;
                }

                try { if (File.Exists(MasterDbPath)) File.Delete(MasterDbPath); } catch { }

                using var masterConn = new SQLiteConnection($"Data Source={MasterDbPath};Version=3;");
                masterConn.Open();
                CreateFeedbackTable(masterConn);

                using var tx = masterConn.BeginTransaction();

                foreach (var dbFile in Directory.GetFiles(CentralFolder, "*_feedback.db"))
                {
                    if (dbFile.EndsWith("FeedbackMaster.db", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        using var sourceConn = new SQLiteConnection($"Data Source={dbFile};Version=3;Read Only=True;");
                        sourceConn.Open();

                        string select = "SELECT User, Type, Message, Time, Response, Severity, ScreenshotPath FROM Feedback";
                        using var cmd = new SQLiteCommand(select, sourceConn);
                        using var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            InsertIfNotExists(masterConn, tx, reader);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Skipping feedback DB file: {dbFile}");
                    }
                }

                tx.Commit();
                File.WriteAllText(LastMergedFlag, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Logger.Info($"FeedbackMaster merge completed at: {DateTime.Now}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to merge central feedback DBs.");
            }
        }

        public static string GetMasterDbPath() => MasterDbPath;

        public static DateTime? GetLastMergedTime()
        {
            try
            {
                if (!File.Exists(LastMergedFlag)) return null;
                var txt = File.ReadAllText(LastMergedFlag).Trim();
                if (DateTime.TryParse(txt, out var dt)) return dt;
            }
            catch { }
            return null;
        }

        private static bool ShouldMergeToday()
        {
            var last = GetLastMergedTime();
            return !(last.HasValue && last.Value.Date == DateTime.Now.Date);
        }

        private static void CreateFeedbackTable(SQLiteConnection conn)
        {
            string create = @"
                CREATE TABLE IF NOT EXISTS Feedback (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    User TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    Time TEXT NOT NULL,
                    Response TEXT,
                    Severity TEXT,
                    ScreenshotPath TEXT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_feedback ON Feedback(User, Time, Message);
            ";
            using var cmd = new SQLiteCommand(create, conn);
            cmd.ExecuteNonQuery();
        }

        private static void InsertIfNotExists(SQLiteConnection conn, SQLiteTransaction tx, SQLiteDataReader reader)
        {
            string insert = @"
                INSERT OR IGNORE INTO Feedback (User, Type, Message, Time, Response, Severity, ScreenshotPath)
                VALUES (@User, @Type, @Message, @Time, @Response, @Severity, @ScreenshotPath);";

            using var cmd = new SQLiteCommand(insert, conn, tx);
            cmd.Parameters.AddWithValue("@User", reader["User"]);
            cmd.Parameters.AddWithValue("@Type", reader["Type"]);
            cmd.Parameters.AddWithValue("@Message", reader["Message"]);
            cmd.Parameters.AddWithValue("@Time", reader["Time"]);
            cmd.Parameters.AddWithValue("@Response", reader["Response"]);
            cmd.Parameters.AddWithValue("@Severity", reader["Severity"]);
            cmd.Parameters.AddWithValue("@ScreenshotPath", reader["ScreenshotPath"]);
            cmd.ExecuteNonQuery();
        }
    }
}

