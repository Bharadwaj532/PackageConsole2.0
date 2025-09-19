
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using NLog;

namespace PackageConsole.Data
{
    public static class SqliteAggregator
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(SqliteAggregator));
        private static readonly string CentralFolder = Path.GetDirectoryName(PathManager.CentralMetadataDb);
        private static readonly string MasterDbPath = Path.Combine(CentralFolder, "MasterDB.db");
        private static readonly string LastMergedFlag = Path.Combine(CentralFolder, "LastMerged.txt");

        public static void RunDailyMerge(bool force = false)
        {
            try
            {
                if (!force && !ShouldMergeToday())
                {
                    Logger.Info("MasterDB merge skipped (already done today).");
                    return;
                }

                if (!Directory.Exists(CentralFolder))
                {
                    Logger.Warn("Central DB folder not found.");
                    return;
                }

                // Build Master DB locally then copy to central to avoid opening SQLite over UNC
                Directory.CreateDirectory(CentralFolder);

                var localMaster = Path.Combine(Path.GetTempPath(), $"MasterDB_{Guid.NewGuid():N}.db");
                try
                {
                    using var masterConn = new SQLiteConnection($"Data Source={localMaster};Version=3;");
                    masterConn.Open();
                    CreatePackageMetaTable(masterConn);

                    using var tx = masterConn.BeginTransaction();

                    foreach (var dbFile in Directory.GetFiles(CentralFolder, "*_packages.db"))
                    {
                        if (dbFile.EndsWith("MasterDB.db", StringComparison.OrdinalIgnoreCase)) continue;

                        try
                        {
                            // Always work from a local copy to avoid UNC/WAL issues
                            string tmp = Path.Combine(Path.GetTempPath(), $"pkg_{Guid.NewGuid():N}.db");
                            try
                            {
                                File.Copy(dbFile, tmp, overwrite: true);
                            }
                            catch (Exception cex)
                            {
                                Logger.Warn(cex, $"Failed to copy {dbFile} to temp; will attempt direct open (may fail on UNC/WAL).");
                                tmp = dbFile;
                            }

                            using var sourceConn = new SQLiteConnection($"Data Source={tmp};Version=3;Read Only=True;");
                            sourceConn.Open();

                            // Read from per-user PackageMetadata schema and map to master schema
                            string select = "SELECT AppKeyID AS AppKeyId, AppName, AppVersion, Vendor, InstallerType, SubmittedBy, SubmittedOn, PackageIniText AS IniContent FROM PackageMetadata";
                            using var cmd = new SQLiteCommand(select, sourceConn);
                            using var reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                InsertIfNotExists(masterConn, tx, reader);
                            }

                            // Clean up local temp copy
                            try
                            {
                                if (!ReferenceEquals(dbFile, tmp) && tmp != dbFile && File.Exists(tmp)) File.Delete(tmp);
                            }
                            catch { }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"Skipping DB file: {dbFile}");
                        }
                    }

                    tx.Commit();

                    // Copy local master to central path (overwrite)
                    File.Copy(localMaster, MasterDbPath, overwrite: true);
                }
                finally
                {
                    try { if (File.Exists(localMaster)) File.Delete(localMaster); } catch { }
                }

                File.WriteAllText(LastMergedFlag, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Logger.Info($"MasterDB merge completed at: {DateTime.Now}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to merge central package DBs.");
            }
        }

        private static bool ShouldMergeToday()
        {
            var last = GetLastMergedTime();
            return !(last.HasValue && last.Value.Date == DateTime.Now.Date);
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

        private static void CreatePackageMetaTable(SQLiteConnection conn)
        {
            string create = @"
                CREATE TABLE IF NOT EXISTS PackageMeta (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AppKeyId TEXT UNIQUE,
                    AppName TEXT,
                    AppVersion TEXT,
                    Vendor TEXT,
                    InstallerType TEXT,
                    SubmittedBy TEXT,
                    SubmittedOn TEXT,
                    IniContent TEXT
                );";
            using var cmd = new SQLiteCommand(create, conn);
            cmd.ExecuteNonQuery();
        }

        private static void InsertIfNotExists(SQLiteConnection conn, SQLiteTransaction tx, SQLiteDataReader reader)
        {
            string appKeyId = reader["AppKeyId"].ToString();

            // check if already exists
            string existsQuery = "SELECT COUNT(1) FROM PackageMeta WHERE AppKeyId = @AppKeyId";
            using var existsCmd = new SQLiteCommand(existsQuery, conn, tx);
            existsCmd.Parameters.AddWithValue("@AppKeyId", appKeyId);
            long count = (long)existsCmd.ExecuteScalar();

            if (count > 0) return;

            string insert = @"
                INSERT INTO PackageMeta (AppKeyId, AppName, AppVersion, Vendor, InstallerType, SubmittedBy, SubmittedOn, IniContent)
                VALUES (@AppKeyId, @AppName, @AppVersion, @Vendor, @InstallerType, @SubmittedBy, @SubmittedOn, @IniContent);";

            using var insertCmd = new SQLiteCommand(insert, conn, tx);
            insertCmd.Parameters.AddWithValue("@AppKeyId", appKeyId);
            insertCmd.Parameters.AddWithValue("@AppName", reader["AppName"]);
            insertCmd.Parameters.AddWithValue("@AppVersion", reader["AppVersion"]);
            insertCmd.Parameters.AddWithValue("@Vendor", reader["Vendor"]);
            insertCmd.Parameters.AddWithValue("@InstallerType", reader["InstallerType"]);
            insertCmd.Parameters.AddWithValue("@SubmittedBy", reader["SubmittedBy"]);
            insertCmd.Parameters.AddWithValue("@SubmittedOn", reader["SubmittedOn"]);
            insertCmd.Parameters.AddWithValue("@IniContent", reader["IniContent"]);
            insertCmd.ExecuteNonQuery();
        }
    }
}
