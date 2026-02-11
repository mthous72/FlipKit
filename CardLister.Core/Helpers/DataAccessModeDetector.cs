using System;
using FlipKit.Core.Models;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Determines the optimal data access mode based on configuration.
    /// </summary>
    public enum DataAccessMode
    {
        /// <summary>
        /// Direct SQLite database access (same computer as database)
        /// </summary>
        Local,

        /// <summary>
        /// API access via Tailscale (remote computer)
        /// </summary>
        ApiRemote
    }

    public static class DataAccessModeDetector
    {
        /// <summary>
        /// Automatically detect whether to use local database or remote API.
        /// </summary>
        public static DataAccessMode DetectMode(AppSettings settings)
        {
            // If no API URL configured, use local database
            if (string.IsNullOrWhiteSpace(settings.SyncServerUrl))
                return DataAccessMode.Local;

            var url = settings.SyncServerUrl.ToLowerInvariant();

            // If API URL is localhost or 127.0.0.1, use local database
            if (url.Contains("localhost") || url.Contains("127.0.0.1"))
                return DataAccessMode.Local;

            // Otherwise, use remote API (Tailscale IP)
            return DataAccessMode.ApiRemote;
        }

        /// <summary>
        /// Get a human-readable description of the data access mode.
        /// </summary>
        public static string GetModeDescription(DataAccessMode mode)
        {
            return mode switch
            {
                DataAccessMode.Local => "Local Database (Direct access - fast)",
                DataAccessMode.ApiRemote => "Remote API (Tailscale network - shared data)",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Check if the current mode is using remote API.
        /// </summary>
        public static bool IsRemoteMode(AppSettings settings)
        {
            return DetectMode(settings) == DataAccessMode.ApiRemote;
        }

        /// <summary>
        /// Check if the current mode is using local database.
        /// </summary>
        public static bool IsLocalMode(AppSettings settings)
        {
            return DetectMode(settings) == DataAccessMode.Local;
        }
    }
}
