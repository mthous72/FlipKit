using System;
using System.IO;
using Serilog;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Handles one-time migration of user data from CardLister to FlipKit.
    /// </summary>
    public static class LegacyMigrator
    {
        private const string LegacyAppName = "CardLister";
        private const string NewAppName = "FlipKit";

        /// <summary>
        /// Checks if CardLister data exists in the old location.
        /// </summary>
        public static bool HasCardListerData()
        {
            var legacyFolder = GetLegacyDataFolder();
            if (!Directory.Exists(legacyFolder))
            {
                return false;
            }

            // Check for key files that indicate actual data exists
            var dbPath = Path.Combine(legacyFolder, "cards.db");
            var settingsPath = Path.Combine(legacyFolder, "settings.json");

            return File.Exists(dbPath) || File.Exists(settingsPath);
        }

        /// <summary>
        /// Migrates all data from CardLister folder to FlipKit folder.
        /// </summary>
        /// <returns>True if migration was successful, false otherwise.</returns>
        public static bool MigrateFromCardLister()
        {
            try
            {
                var legacyFolder = GetLegacyDataFolder();
                var newFolder = GetNewDataFolder();

                if (!Directory.Exists(legacyFolder))
                {
                    Log.Warning("Legacy CardLister folder not found, skipping migration");
                    return false;
                }

                if (Directory.Exists(newFolder))
                {
                    // FlipKit folder already exists - check if it has data
                    var newDbPath = Path.Combine(newFolder, "cards.db");
                    if (File.Exists(newDbPath))
                    {
                        Log.Information("FlipKit data already exists, skipping migration");
                        return true; // Not an error, just already migrated
                    }
                }
                else
                {
                    Directory.CreateDirectory(newFolder);
                }

                Log.Information("Migrating data from CardLister to FlipKit...");

                // Copy all files from legacy folder to new folder
                CopyDirectory(legacyFolder, newFolder, recursive: true);

                Log.Information("Migration complete! Data copied from {LegacyFolder} to {NewFolder}",
                    legacyFolder, newFolder);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to migrate CardLister data to FlipKit");
                return false;
            }
        }

        private static string GetLegacyDataFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                LegacyAppName);
        }

        private static string GetNewDataFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                NewAppName);
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }

            // Create destination directory
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (var file in dir.GetFiles())
            {
                var destFile = Path.Combine(destDir, file.Name);
                file.CopyTo(destFile, overwrite: false); // Don't overwrite existing files
                Log.Debug("Copied: {FileName}", file.Name);
            }

            // Copy subdirectories recursively
            if (recursive)
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    var destSubDir = Path.Combine(destDir, subDir.Name);
                    CopyDirectory(subDir.FullName, destSubDir, recursive: true);
                }
            }
        }
    }
}
