using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services
{
    public class BulkScanErrorLogger : IBulkScanErrorLogger
    {
        private readonly ILogger<BulkScanErrorLogger> _logger;
        private readonly string _logDirectory;
        private BulkScanSessionSummary? _currentSession;
        private string? _currentLogFile;
        private readonly object _lockObject = new();

        public BulkScanErrorLogger(ILogger<BulkScanErrorLogger> logger)
        {
            _logger = logger;

            // Use AppData folder for logs
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appDataPath, "FlipKit", "BulkScanLogs");

            // Ensure directory exists
            Directory.CreateDirectory(_logDirectory);
        }

        public void StartSession(int totalCards, string model)
        {
            lock (_lockObject)
            {
                // Create log file with timestamp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _currentLogFile = Path.Combine(_logDirectory, $"bulk-scan_{timestamp}.log");

                _currentSession = new BulkScanSessionSummary
                {
                    StartTime = DateTime.Now,
                    TotalCards = totalCards,
                    Model = model,
                    LogFilePath = _currentLogFile
                };

                // Write header
                var header = new StringBuilder();
                header.AppendLine("═══════════════════════════════════════════════════════════════");
                header.AppendLine($"  FlipKit Bulk Scan Error Log");
                header.AppendLine("═══════════════════════════════════════════════════════════════");
                header.AppendLine($"Session Start: {_currentSession.StartTime:yyyy-MM-dd HH:mm:ss}");
                header.AppendLine($"Total Cards:   {totalCards}");
                header.AppendLine($"Model:         {model}");
                header.AppendLine("═══════════════════════════════════════════════════════════════");
                header.AppendLine();

                File.WriteAllText(_currentLogFile, header.ToString());

                _logger.LogInformation("Started bulk scan error tracking session: {LogFile}", _currentLogFile);
            }
        }

        public void LogError(int cardIndex, string frontImagePath, string? backImagePath, Exception exception, string model)
        {
            if (_currentSession == null)
            {
                _logger.LogWarning("Attempted to log error without active session");
                return;
            }

            lock (_lockObject)
            {
                var error = new BulkScanError
                {
                    CardIndex = cardIndex,
                    Timestamp = DateTime.Now,
                    FrontImagePath = frontImagePath,
                    BackImagePath = backImagePath,
                    ErrorMessage = exception.Message,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException?.Message,
                    Model = model
                };

                _currentSession.Errors.Add(error);
                _currentSession.ErrorCount++;

                // Write detailed error to log file
                var errorLog = new StringBuilder();
                errorLog.AppendLine();
                errorLog.AppendLine($"┌─────────────────────────────────────────────────────────────┐");
                errorLog.AppendLine($"│ ERROR #{_currentSession.ErrorCount} - Card {cardIndex} of {_currentSession.TotalCards}");
                errorLog.AppendLine($"└─────────────────────────────────────────────────────────────┘");
                errorLog.AppendLine($"  Timestamp:     {error.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                errorLog.AppendLine($"  Model:         {model}");
                errorLog.AppendLine($"  Front Image:   {frontImagePath}");
                if (!string.IsNullOrEmpty(backImagePath))
                    errorLog.AppendLine($"  Back Image:    {backImagePath}");
                errorLog.AppendLine();
                errorLog.AppendLine("  Error Message:");
                errorLog.AppendLine($"    {exception.Message}");
                errorLog.AppendLine();

                if (!string.IsNullOrEmpty(error.InnerException))
                {
                    errorLog.AppendLine("  Inner Exception:");
                    errorLog.AppendLine($"    {error.InnerException}");
                    errorLog.AppendLine();
                }

                if (!string.IsNullOrEmpty(error.StackTrace))
                {
                    errorLog.AppendLine("  Stack Trace:");
                    var stackLines = error.StackTrace.Split('\n');
                    foreach (var line in stackLines.Take(10)) // Limit to 10 lines
                    {
                        errorLog.AppendLine($"    {line.Trim()}");
                    }
                    if (stackLines.Length > 10)
                    {
                        errorLog.AppendLine($"    ... ({stackLines.Length - 10} more lines)");
                    }
                    errorLog.AppendLine();
                }

                errorLog.AppendLine("───────────────────────────────────────────────────────────────");

                if (_currentLogFile != null)
                {
                    File.AppendAllText(_currentLogFile, errorLog.ToString());
                }

                _logger.LogError(exception, "Bulk scan error for card {CardIndex}: {Path}", cardIndex, frontImagePath);
            }
        }

        public void LogSuccess(int cardIndex, string frontImagePath, string playerName)
        {
            if (_currentSession == null)
            {
                _logger.LogWarning("Attempted to log success without active session");
                return;
            }

            lock (_lockObject)
            {
                _currentSession.SuccessCount++;
            }
        }

        public async Task EndSessionAsync()
        {
            if (_currentSession == null || _currentLogFile == null)
                return;

            lock (_lockObject)
            {
                _currentSession.EndTime = DateTime.Now;
            }

            // Write summary
            var duration = _currentSession.EndTime.Value - _currentSession.StartTime;
            var successRate = _currentSession.TotalCards > 0
                ? (_currentSession.SuccessCount / (double)_currentSession.TotalCards * 100)
                : 0;

            var summary = new StringBuilder();
            summary.AppendLine();
            summary.AppendLine();
            summary.AppendLine("═══════════════════════════════════════════════════════════════");
            summary.AppendLine("  SESSION SUMMARY");
            summary.AppendLine("═══════════════════════════════════════════════════════════════");
            summary.AppendLine($"  Start Time:    {_currentSession.StartTime:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine($"  End Time:      {_currentSession.EndTime:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine($"  Duration:      {duration.TotalMinutes:F1} minutes");
            summary.AppendLine($"  Model:         {_currentSession.Model}");
            summary.AppendLine();
            summary.AppendLine($"  Total Cards:   {_currentSession.TotalCards}");
            summary.AppendLine($"  ✓ Successful:  {_currentSession.SuccessCount} ({successRate:F1}%)");
            summary.AppendLine($"  ✗ Failed:      {_currentSession.ErrorCount} ({100 - successRate:F1}%)");
            summary.AppendLine("═══════════════════════════════════════════════════════════════");

            if (_currentSession.ErrorCount > 0)
            {
                summary.AppendLine();
                summary.AppendLine("  Failed Cards:");
                foreach (var error in _currentSession.Errors)
                {
                    summary.AppendLine($"    • Card {error.CardIndex}: {Path.GetFileName(error.FrontImagePath)}");
                    summary.AppendLine($"      Error: {error.ErrorMessage}");
                }
                summary.AppendLine();
                summary.AppendLine($"  Log File: {_currentLogFile}");
            }
            else
            {
                summary.AppendLine();
                summary.AppendLine("  🎉 All cards scanned successfully!");
                summary.AppendLine();

                // Delete log file if no errors
                try
                {
                    if (File.Exists(_currentLogFile))
                    {
                        File.Delete(_currentLogFile);
                        _logger.LogInformation("Deleted error log (no errors occurred): {LogFile}", _currentLogFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete clean log file: {LogFile}", _currentLogFile);
                }
            }

            summary.AppendLine("═══════════════════════════════════════════════════════════════");

            if (_currentSession.ErrorCount > 0)
            {
                await File.AppendAllTextAsync(_currentLogFile, summary.ToString());
            }

            _logger.LogInformation("Bulk scan session ended: {Success} success, {Errors} errors, {Duration:F1}min",
                _currentSession.SuccessCount, _currentSession.ErrorCount, duration.TotalMinutes);

            // Keep reference for GetSessionSummary but clear file reference
            _currentLogFile = null;
        }

        public string? GetCurrentLogFilePath()
        {
            return _currentLogFile;
        }

        public BulkScanSessionSummary GetSessionSummary()
        {
            return _currentSession ?? new BulkScanSessionSummary();
        }
    }
}
