using System;
using System.Collections.Generic;

namespace FlipKit.Core.Models
{
    public enum ChecklistFileFormat
    {
        Unknown = 0,
        InlineHeader,    // Subset announced by all-caps single-cell rows in column A (Bowman 2026)
        ColumnASubset,   // Header row + subset name in column A on every data row (Mosaic 2025)
    }

    public class ChecklistImportMetadata
    {
        public int? Year { get; set; }
        public string? Sport { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? SetName { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
    }

    public class ChecklistImportPreview
    {
        public ChecklistImportMetadata Metadata { get; set; } = new();
        public ChecklistFileFormat DetectedFormat { get; set; }
        public List<ChecklistCard> Cards { get; set; } = new();
        public List<string> SubsetNames { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int TotalRowsRead { get; set; }
        public int RowsSkipped { get; set; }

        public int CardCount => Cards.Count;
        public int SubsetCount => SubsetNames.Count;
        public bool IsValid => Cards.Count > 0
                               && Metadata.Year.HasValue
                               && !string.IsNullOrWhiteSpace(Metadata.Brand);
    }

    public class ChecklistImportCommitResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int CardsImported { get; set; }
        public int SubsetCount { get; set; }
        public bool ReplacedExisting { get; set; }
        public int? ChecklistId { get; set; }
    }
}
