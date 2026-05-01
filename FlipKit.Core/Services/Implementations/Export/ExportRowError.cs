namespace FlipKit.Core.Services.Export
{
    /// <summary>
    /// A single per-row export-rule violation. The exporter dispatcher collects these
    /// across all rows and surfaces them to the user before any file is written.
    /// </summary>
    public record ExportRowError(
        int CardId,
        string PlayerName,
        string Field,
        string Message,
        ExportErrorSeverity Severity = ExportErrorSeverity.Error)
    {
        public override string ToString() =>
            $"[{Severity}] Card {CardId} ({PlayerName}) — {Field}: {Message}";
    }

    public enum ExportErrorSeverity
    {
        /// <summary>Blocks the export. The dispatcher refuses to write the file if any errors are present.</summary>
        Error,
        /// <summary>Surfaced to the user but does not block export. Used for ambiguous cases like a custom shipping profile name.</summary>
        Warning,
    }
}
