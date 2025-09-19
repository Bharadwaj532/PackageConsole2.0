using System;
using System.Data.SQLite;
using System.IO;

namespace PackageConsole.Data
{
    public static class CentralDbSeeder
    {
        public static void GenerateSampleCentralPackageDbs()
        {
            string centralFolder = Path.GetDirectoryName(PathManager.CentralMetadataDb);
            if (string.IsNullOrWhiteSpace(centralFolder))
                throw new Exception("Central metadata folder is not configured.");

            Directory.CreateDirectory(centralFolder);

            string[] sampleUsers = new[] { "alice", "bob", "charlie" };
            int i = 1;
            var errors = new System.Text.StringBuilder();
            foreach (var user in sampleUsers)
            {
                string dbPath = Path.Combine(centralFolder, $"{user}_packages.db");
                try
                {
                    // Create/seed in a local temp DB, then copy to central to avoid opening SQLite over UNC
                    string tmp = Path.Combine(Path.GetTempPath(), $"{user}_packages_{Guid.NewGuid():N}.db");
                    CreateOrSeedPackageMetadata(tmp, user, i++);
                    File.Copy(tmp, dbPath, true);
                    try { File.Delete(tmp); } catch { /* ignore */ }
                }
                catch (Exception ex)
                {
                    errors.AppendLine($"- {dbPath} :: {ex.Message}");
                }
            }

            if (errors.Length > 0)
            {
                throw new Exception($"One or more sample DBs could not be created:\n{errors}");
            }
        }

        private static void CreateOrSeedPackageMetadata(string path, string user, int index)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var conn = new SQLiteConnection($"Data Source={path};Version=3;");
            conn.Open();

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
            using (var cmd = new SQLiteCommand(createTable, conn)) cmd.ExecuteNonQuery();

            // Insert a couple of rows
            for (int k = 1; k <= 2; k++)
            {
                string appKey = $"SAMPLE-{index}{k:00}";
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
                cmd.Parameters.AddWithValue("@AppKeyID", appKey);
                cmd.Parameters.AddWithValue("@AppName", $"SampleApp {index}-{k}");
                cmd.Parameters.AddWithValue("@AppVersion", "1.0.0");
                cmd.Parameters.AddWithValue("@ProductCode", Guid.NewGuid().ToString("B"));
                cmd.Parameters.AddWithValue("@Vendor", "Contoso");
                cmd.Parameters.AddWithValue("@DRMBuild", "N/A");
                cmd.Parameters.AddWithValue("@RebootOption", "No");
                cmd.Parameters.AddWithValue("@InstallerType", "MSI");
                cmd.Parameters.AddWithValue("@InstallerFile", "setup.msi");
                cmd.Parameters.AddWithValue("@SubmittedBy", user);
                cmd.Parameters.AddWithValue("@SubmittedOn", DateTime.Now.AddMinutes(-k).ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@PackageIniText", $"[META]\nAPPKEYID={appKey}\nAPPNAME=SampleApp {index}-{k}\n");
                cmd.ExecuteNonQuery();
            }
        }
    }
}

