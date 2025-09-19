using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using NLog;
using PackageConsole;

namespace PackageConsole.Data
{
    public static class SqliteHelper
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(SqliteHelper));

        private static readonly string DbPath = PathManager.LocalUserDb;

        static SqliteHelper()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
                InitializeDatabase(DbPath);
                InitializeDatabase(PathManager.CentralUserDb);  // attempt central too
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize feedback databases.");
            }
        }

        private static void InitializeDatabase(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var conn = new SQLiteConnection($"Data Source={path};Version=3;");
                conn.Open();

                using var _pragma = new SQLiteCommand("PRAGMA journal_mode=WAL;", conn);
                _pragma.ExecuteNonQuery();

                string createTable = @"
                    CREATE TABLE IF NOT EXISTS Feedback (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        User TEXT NOT NULL,
                        Type TEXT NOT NULL,
                        Message TEXT NOT NULL,
                        Time TEXT NOT NULL,
                        Response TEXT,
                        Severity TEXT,
                        ScreenshotPath TEXT
                    );";

                using var cmd = new SQLiteCommand(createTable, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Unable to initialize database at {path}");
            }
        }
        private static void CopyToCentralBackup()
        {
            try
            {
                using var sourceConn = new SQLiteConnection($"Data Source={PathManager.LocalUserDb};Version=3;");
                using var targetConn = new SQLiteConnection($"Data Source={PathManager.CentralUserDb};Version=3;");
                sourceConn.Open();
                targetConn.Open();

                sourceConn.BackupDatabase(targetConn, "main", "main", -1, null, 0);
                Logger.Info("SQLite backup to central DB completed successfully.");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to perform SQLite backup to central DB.");
            }

        }

        private static void TestCentralWriteAccess()
        {
            try
            {
                string centralFolder = Path.GetDirectoryName(PathManager.CentralUserDb);
                Directory.CreateDirectory(centralFolder);

                string testFilePath = Path.Combine(centralFolder, $"test_{Environment.UserName}_{DateTime.Now:yyyyMMddHHmmss}.txt");

                File.WriteAllText(testFilePath, "Central path test write succeeded at " + DateTime.Now);
                Logger.Debug($"✅ Test file written successfully to central path: {testFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "❌ Failed to write test file to central path.");
            }
        }

        public static void InsertFeedback(FeedbackEntry entry)
        {
            try
            {
                var localPath = PathManager.LocalUserDb;
                var centralPath = PathManager.CentralUserDb;

                // Local write
                using (var conn = new SQLiteConnection($"Data Source={localPath};Version=3;"))
                {
                    conn.Open();

                    string insert = @"
                INSERT INTO Feedback (User, Type, Message, Time, Response, Severity, ScreenshotPath)
                VALUES (@User, @Type, @Message, @Time, @Response, @Severity, @ScreenshotPath);";

                    using var cmd = new SQLiteCommand(insert, conn);
                    cmd.Parameters.AddWithValue("@User", entry.User);
                    cmd.Parameters.AddWithValue("@Type", entry.Type);
                    cmd.Parameters.AddWithValue("@Message", entry.Message);
                    cmd.Parameters.AddWithValue("@Time", entry.Time.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@Response", entry.Response);
                    cmd.Parameters.AddWithValue("@Severity", entry.Severity);
                    cmd.Parameters.AddWithValue("@ScreenshotPath", entry.ScreenshotPath);
                    cmd.ExecuteNonQuery();

                    Logger.Info($"Feedback inserted into local DB: {localPath}");
                }

                // ✅ Now safely mirror to central (backup + atomic replace)
                Directory.CreateDirectory(Path.GetDirectoryName(centralPath));
                CopyToCentralBackup();
                var tempPath = centralPath + ".tmp";
                File.Copy(localPath, tempPath, true);
                if (File.Exists(centralPath)) File.Delete(centralPath);
                File.Move(tempPath, centralPath);
                Logger.Info($"✅ Local DB mirrored atomically to central: {centralPath}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "❌ Error inserting feedback or copying to central.");
            }
        }

        private static void BackupToCentralDb()
        {
            try
            {
                var sourcePath = PathManager.LocalUserDb;
                var targetPath = PathManager.CentralUserDb;

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                using var sourceConn = new SQLiteConnection($"Data Source={sourcePath};Version=3;");
                using var targetConn = new SQLiteConnection($"Data Source={targetPath};Version=3;");

                sourceConn.Open();
                targetConn.Open();

                // ✅ Perform the safe backup
                sourceConn.BackupDatabase(targetConn, "main", "main", -1, null, 0);

                Logger.Info($"✅ SQLite backup completed from local to central: {targetPath}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "❌ Failed to perform SQLite backup to central location.");
            }
        }


        public static List<FeedbackEntry> GetLatestFeedbacks(int count = 100)
        {
            var list = new List<FeedbackEntry>();

            try
            {
                using var conn = new SQLiteConnection($"Data Source={DbPath};Version=3;");
                conn.Open();

                string query = @"SELECT * FROM Feedback ORDER BY Time DESC LIMIT @Count;";
                using var cmd = new SQLiteCommand(query, conn);
                cmd.Parameters.AddWithValue("@Count", count);
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
                    list.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load feedback from local DB.");
            }

            return list;
        }
    }
}
