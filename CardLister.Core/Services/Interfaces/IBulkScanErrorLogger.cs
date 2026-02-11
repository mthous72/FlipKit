using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    public interface IBulkScanErrorLogger
    {
        /// <summary>
        /// Starts a new bulk scan session and creates a new log file.
        /// </summary>
        /// <param name="totalCards">Total number of cards in this batch</param>
        /// <param name="model">AI model being used</param>
        void StartSession(int totalCards, string model);

        /// <summary>
        /// Logs a scan error with full details.
        /// </summary>
        void LogError(int cardIndex, string frontImagePath, string? backImagePath, Exception exception, string model);

        /// <summary>
        /// Logs a successful scan for statistics.
        /// </summary>
        void LogSuccess(int cardIndex, string frontImagePath, string playerName);

        /// <summary>
        /// Ends the session and writes a summary.
        /// </summary>
        Task EndSessionAsync();

        /// <summary>
        /// Gets the path to the current session's log file.
        /// </summary>
        string? GetCurrentLogFilePath();

        /// <summary>
        /// Gets summary of current session.
        /// </summary>
        BulkScanSessionSummary GetSessionSummary();
    }

    public class BulkScanSessionSummary
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int TotalCards { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public string Model { get; set; } = string.Empty;
        public string? LogFilePath { get; set; }
        public List<BulkScanError> Errors { get; set; } = new();
    }

    public class BulkScanError
    {
        public int CardIndex { get; set; }
        public DateTime Timestamp { get; set; }
        public string FrontImagePath { get; set; } = string.Empty;
        public string? BackImagePath { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? InnerException { get; set; }
        public string Model { get; set; } = string.Empty;
    }
}
