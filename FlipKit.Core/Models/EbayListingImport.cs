using System.Collections.Generic;
using FlipKit.Core.Helpers;
using FlipKit.Core.Services;

namespace FlipKit.Core.Models
{
    /// <summary>
    /// Snapshot returned by <see cref="IEbayListingImportService.ParseAsync"/>.
    /// Each row carries the original CSV data, the rule-pass parse result, the
    /// LLM enrichment, and the proposed merged <see cref="Card"/> so the user
    /// can review before committing.
    /// </summary>
    public sealed class EbayListingImportPreview
    {
        public string SourceFileName { get; set; } = string.Empty;
        public List<EbayImportRowPreview> Rows { get; set; } = new();

        /// <summary>Rows whose EbayItemId matches an existing card (will update on commit).</summary>
        public int UpdateCount { get; set; }

        /// <summary>Rows whose EbayItemId is new (will insert on commit).</summary>
        public int InsertCount { get; set; }

        public List<string> Warnings { get; set; } = new();
    }

    public sealed class EbayImportRowPreview
    {
        public EbayListingRow CsvRow { get; set; } = new();
        public EbayParsedTitle ParsedTitle { get; set; } = new();
        public EbayTitleEnrichment? Enrichment { get; set; }
        public Card ProposedCard { get; set; } = new();
        public bool IsExistingMatch { get; set; }

        /// <summary>When true the user has unticked this row in the preview; commit skips it.</summary>
        public bool Skip { get; set; }
    }

    public sealed class EbayListingImportResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
