using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using PackageConsole.Models;
using NLog;

namespace PackageConsole.Data
{
    public static class SqlitePackageHelper
    {
        private static readonly Logger Logger = LogManager.GetLogger(nameof(SqlitePackageHelper));

        private static readonly string LocalDbPath = PathManager.MetadataDb;
        private static readonly string CentralDbPath = PathManager.CentralMetadataDb;

        static SqlitePackageHelper()
        {
            try
            {
                InitializeDatabase(LocalDbPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize PackageMetadata databases.");
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
                    CREATE TABLE IF NOT EXISTS PackageMetadata (
                        AppKeyID TEXT PRIMARY KEY,
                        AppName TEXT,
                        AppVersion TEXT,
                        ProductCode TEXT,
                        Vendor TEXT,
                        DRMBuild TEXT,
                        RebootOption TEXT,
                        InstallerType TEXT,
                        InstallerFile TEXT,
                        SubmittedBy TEXT,
                        SubmittedOn TEXT,
                        PackageIniText TEXT
                    );";

                using var cmd = new SQLiteCommand(createTable, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Unable to initialize database at {path}");
            }
        }


        public static void UpsertPackageMetadata(PackageInfo package)
        {
            try
            {
                WriteToDatabase(LocalDbPath, package);
                TryBackupToCentralDb();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to upsert package metadata.");
            }
        }

        private static void WriteToDatabase(string dbPath, PackageInfo package)
        {
            using var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            conn.Open();

            string upsert = @"
                INSERT INTO PackageMetadata (
                    AppKeyID, AppName, AppVersion, ProductCode, Vendor, DRMBuild,
                    RebootOption, InstallerType, InstallerFile, SubmittedBy, SubmittedOn, PackageIniText)
                VALUES (
                    @AppKeyID, @AppName, @AppVersion, @ProductCode, @Vendor, @DRMBuild,
                    @RebootOption, @InstallerType, @InstallerFile, @SubmittedBy, @SubmittedOn, @PackageIniText)
                ON CONFLICT(AppKeyID) DO UPDATE SET
                    AppName = excluded.AppName,
                    AppVersion = excluded.AppVersion,
                    ProductCode = excluded.ProductCode,
                    Vendor = excluded.Vendor,
                    DRMBuild = excluded.DRMBuild,
                    RebootOption = excluded.RebootOption,
                    InstallerType = excluded.InstallerType,
                    InstallerFile = excluded.InstallerFile,
                    SubmittedBy = excluded.SubmittedBy,
                    SubmittedOn = excluded.SubmittedOn,
                    PackageIniText = excluded.PackageIniText;";

            using var cmd = new SQLiteCommand(upsert, conn);
            cmd.Parameters.AddWithValue("@AppKeyID", package.AppKeyID);
            cmd.Parameters.AddWithValue("@AppName", package.AppName);
            cmd.Parameters.AddWithValue("@AppVersion", package.AppVersion);
            cmd.Parameters.AddWithValue("@ProductCode", package.ProductCode);
            cmd.Parameters.AddWithValue("@Vendor", package.Vendor);
            cmd.Parameters.AddWithValue("@DRMBuild", package.DRMBuild);
            cmd.Parameters.AddWithValue("@RebootOption", package.RebootOption);
            cmd.Parameters.AddWithValue("@InstallerType", package.InstallerType);
            cmd.Parameters.AddWithValue("@InstallerFile", package.InstallerFile);
            cmd.Parameters.AddWithValue("@SubmittedBy", package.SubmittedBy);
            cmd.Parameters.AddWithValue("@SubmittedOn", package.SubmittedOn.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@PackageIniText", package.PackageIniText);

            cmd.ExecuteNonQuery();
            Logger.Info($"✅ Package metadata upserted into DB: {dbPath}");
        }

        public static (bool success, string? error) MirrorLocalToCentralNow()
        {
            return TryBackupToCentralDb();
        }

        private static (bool success, string? error) TryBackupToCentralDb()
        {
            try
            {
                var centralDir = Path.GetDirectoryName(CentralDbPath);
                if (string.IsNullOrWhiteSpace(centralDir))
                    return (false, "Central metadata folder is not configured.");

                Directory.CreateDirectory(centralDir);

                // First try the simplest approach: direct file copy with overwrite
                try
                {
                    File.Copy(LocalDbPath, CentralDbPath, overwrite: true);
                    Logger.Info("✅ PackageMetadata DB copied to central (direct overwrite).");
                    return (true, null);
                }
                catch (Exception copyEx)
                {
                    Logger.Warn(copyEx, "Direct copy to central failed, will attempt snapshot+copy fallback.");
                }

                // Fallback: create a consistent local snapshot, then copy that file to central
                var localSnapshot = Path.Combine(Path.GetTempPath(), $"PackageMetadataBackup_{Guid.NewGuid():N}.db");
                try
                {
                    using (var src = new SQLiteConnection($"Data Source={LocalDbPath};Version=3;"))
                    using (var dst = new SQLiteConnection($"Data Source={localSnapshot};Version=3;"))
                    {
                        src.Open();
                        dst.Open();
                        src.BackupDatabase(dst, "main", "main", -1, null, 0);
                    }

                    File.Copy(localSnapshot, CentralDbPath, overwrite: true);
                    Logger.Info("✅ PackageMetadata DB mirrored to central via snapshot copy.");
                    return (true, null);
                }
                finally
                {
                    try { if (File.Exists(localSnapshot)) File.Delete(localSnapshot); } catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "❌ Failed to backup/mirror PackageMetadata DB to central location.");
                return (false, $"{ex.Message} (Central path: {CentralDbPath})");
            }
        }

        public static void UpdateIniTextOnly(string iniFilePath)
        {
            try
            {
                var lines = File.ReadAllLines(iniFilePath);
                var iniText = File.ReadAllText(iniFilePath);
                string appKey = lines.FirstOrDefault(line => line.StartsWith("APPKEYID=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1]?.Trim();

                if (string.IsNullOrWhiteSpace(appKey))
                {
                    Logger.Warn("APPKEYID not found in INI file. Skipping metadata update.");
                    return;
                }

                using var conn = new SQLiteConnection($"Data Source={LocalDbPath};Version=3;");
                conn.Open();

                string update = @"
                    UPDATE PackageMetadata
                    SET PackageIniText = @PackageIniText, SubmittedOn = @SubmittedOn
                    WHERE AppKeyID = @AppKeyID;";

                using var cmd = new SQLiteCommand(update, conn);
                cmd.Parameters.AddWithValue("@AppKeyID", appKey);
                cmd.Parameters.AddWithValue("@PackageIniText", iniText);
                cmd.Parameters.AddWithValue("@SubmittedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                int rows = cmd.ExecuteNonQuery();

                if (rows == 0)
                    Logger.Warn("No matching AppKeyID found to update INI text.");
                else
                    Logger.Info($"✅ INI text updated for AppKeyID: {appKey}");

                // Simple overwrite copy per request
                File.Copy(LocalDbPath, CentralDbPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update PackageIniText.");
            }
        }

        public static List<PackageInfo> GetAllPackages()
        {
            var packages = new List<PackageInfo>();
            try
            {
                using var conn = new SQLiteConnection($"Data Source={PathManager.MetadataDb};Version=3;");
                conn.Open();

                string query = "SELECT * FROM PackageMetadata ORDER BY SubmittedOn DESC;";
                using var cmd = new SQLiteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var pkg = new PackageInfo
                    {
                        AppKeyID = reader["AppKeyID"].ToString(),
                        AppName = reader["AppName"].ToString(),
                        AppVersion = reader["AppVersion"].ToString(),
                        ProductCode = reader["ProductCode"].ToString(),
                        Vendor = reader["Vendor"].ToString(),
                        DRMBuild = reader["DRMBuild"].ToString(),
                        RebootOption = reader["RebootOption"].ToString(),
                        InstallerType = reader["InstallerType"].ToString(),
                        InstallerFile = reader["InstallerFile"].ToString(),
                        SubmittedBy = reader["SubmittedBy"].ToString(),
                        SubmittedOn = DateTime.Parse(reader["SubmittedOn"].ToString()),
                        PackageIniText = reader["PackageIniText"].ToString()
                    };
                    packages.Add(pkg);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load packages from metadata DB.");
            }

            return packages;
        }

        public static List<PackageInfo> GetPackagesFromMasterDb()
        {
            var results = new List<PackageInfo>();
            try
            {
                string centralFolder = Path.GetDirectoryName(PathManager.CentralMetadataDb);
                string masterDbPath = Path.Combine(centralFolder, "MasterDB.db");
                if (!File.Exists(masterDbPath))
                {
                    Logger.Warn($"MasterDB not found at {masterDbPath}");
                    return results;
                }

                // Read from a local copy to avoid UNC/WAL issues
                string tmp = Path.Combine(Path.GetTempPath(), $"Master_{Guid.NewGuid():N}.db");
                try
                {
                    File.Copy(masterDbPath, tmp, overwrite: true);
                }
                catch (Exception exCopy)
                {
                    Logger.Warn(exCopy, $"Could not copy MasterDB to temp: {masterDbPath}. Will attempt direct open.");
                    tmp = masterDbPath;
                }

                using var conn = new SQLiteConnection($"Data Source={tmp};Version=3;Read Only=True;");
                conn.Open();

                string query = "SELECT AppKeyId, AppName, AppVersion, Vendor, InstallerType, SubmittedBy, SubmittedOn, IniContent FROM PackageMeta ORDER BY datetime(SubmittedOn) DESC;";
                using var cmd = new SQLiteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    results.Add(new PackageInfo
                    {
                        AppKeyID = reader["AppKeyId"].ToString(),
                        AppName = reader["AppName"].ToString(),
                        AppVersion = reader["AppVersion"].ToString(),
                        Vendor = reader["Vendor"].ToString(),
                        InstallerType = reader["InstallerType"].ToString(),
                        SubmittedBy = reader["SubmittedBy"].ToString(),
                        SubmittedOn = DateTime.TryParse(reader["SubmittedOn"].ToString(), out var dt) ? dt : DateTime.MinValue,
                        PackageIniText = reader["IniContent"].ToString()
                    });
                }

                try { if (tmp != masterDbPath && File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load packages from MasterDB.");
            }

            return results;
        }

        public static List<PackageInfo> GetPackagesFromCentralDbFile(string dbPath)
        {
            var list = new List<PackageInfo>();
            try
            {
                if (!File.Exists(dbPath)) return list;

                // Work from a local copy to avoid UNC/WAL issues
                string tmp = Path.Combine(Path.GetTempPath(), $"Pkg_{Guid.NewGuid():N}.db");
                try
                {
                    File.Copy(dbPath, tmp, overwrite: true);
                }
                catch (Exception exCopy)
                {
                    Logger.Warn(exCopy, $"Could not copy central DB to temp: {dbPath}. Will attempt direct open.");
                    tmp = dbPath;
                }

                using var conn = new SQLiteConnection($"Data Source={tmp};Version=3;Read Only=True;");
                conn.Open();

                // Prefer PackageMetadata (per-user db), but fall back to PackageMeta if needed
                string sql = "SELECT AppKeyID, AppName, AppVersion, Vendor, InstallerType, SubmittedBy, SubmittedOn, PackageIniText FROM PackageMetadata ORDER BY datetime(SubmittedOn) DESC;";
                try
                {
                    using var cmd = new SQLiteCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new PackageInfo
                        {
                            AppKeyID = reader["AppKeyID"].ToString(),
                            AppName = reader["AppName"].ToString(),
                            AppVersion = reader["AppVersion"].ToString(),
                            Vendor = reader["Vendor"].ToString(),
                            InstallerType = reader["InstallerType"].ToString(),
                            SubmittedBy = reader["SubmittedBy"].ToString(),
                            SubmittedOn = DateTime.TryParse(reader["SubmittedOn"].ToString(), out var dt) ? dt : DateTime.MinValue,
                            PackageIniText = reader["PackageIniText"].ToString()
                        });
                    }
                }
                catch (SQLiteException)
                {
                    // Fallback for unexpected dbs that already look like Master
                    using var cmd2 = new SQLiteCommand("SELECT AppKeyId, AppName, AppVersion, Vendor, InstallerType, SubmittedBy, SubmittedOn, IniContent FROM PackageMeta ORDER BY datetime(SubmittedOn) DESC;", conn);
                    using var reader2 = cmd2.ExecuteReader();
                    while (reader2.Read())
                    {
                        list.Add(new PackageInfo
                        {
                            AppKeyID = reader2["AppKeyId"].ToString(),
                            AppName = reader2["AppName"].ToString(),
                            AppVersion = reader2["AppVersion"].ToString(),
                            Vendor = reader2["Vendor"].ToString(),
                            InstallerType = reader2["InstallerType"].ToString(),
                            SubmittedBy = reader2["SubmittedBy"].ToString(),
                            SubmittedOn = DateTime.TryParse(reader2["SubmittedOn"].ToString(), out var dt) ? dt : DateTime.MinValue,
                            PackageIniText = reader2["IniContent"].ToString()
                        });
                    }
                }

                try { if (tmp != dbPath && File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to load from central DB file: {dbPath}");
            }
            return list;
        }


    }
}
