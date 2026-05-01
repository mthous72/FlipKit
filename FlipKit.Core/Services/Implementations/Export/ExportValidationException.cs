using System;
using System.Collections.Generic;
using System.Linq;

namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// Thrown by <c>IExportService.ExportCsvAsync</c> when one or more pre-flight rules
    /// fail. Carries the structured error list so callers can render per-row details
    /// rather than relying on a single string message.
    /// </summary>
    public class ExportValidationException : Exception
    {
        public IReadOnlyList<ExportRowError> Errors { get; }

        public ExportValidationException(IReadOnlyList<ExportRowError> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors;
        }

        private static string BuildMessage(IReadOnlyList<ExportRowError> errors)
        {
            var blockers = errors.Count(e => e.Severity == ExportErrorSeverity.Error);
            var preview = string.Join("; ", errors.Take(3).Select(e => $"{e.PlayerName}: {e.Field}"));
            return $"Export blocked by {blockers} validation error(s). First few: {preview}";
        }
    }
}
