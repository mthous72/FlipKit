using System;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Models
{
    // One row per scan attempt. Aggregated by IModelScoreboard into a per-model
    // quality score so pickers can sort better-performing models to the top.
    public class ModelScanRecord
    {
        public int Id { get; set; }

        // OpenRouter model id, e.g. "openai/gpt-4o-mini" or "google/gemini-2.0-flash-exp:free".
        public string ModelId { get; set; } = string.Empty;

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public ScanOutcome Outcome { get; set; }

        // Confidence breakdown — populated only on Outcome=Success. The completeness
        // signal in the score formula is HighConfidenceFieldCount / TotalConfidenceFieldCount.
        public int? HighConfidenceFieldCount { get; set; }
        public int? TotalConfidenceFieldCount { get; set; }

        // Number of fields the LLM disobeyed verified-fields hints on
        // (ApplyVerifiedFieldOverrides counts these). Drift penalty in the score formula.
        public int? DriftEventCount { get; set; }

        // Populated retroactively by EditCardViewModel / InventoryViewModel when the
        // user corrects fields the model produced. Correction penalty in the score formula.
        public int? UserCorrectedFieldCount { get; set; }

        // Optional FK back to the resulting card. Null when the scan failed before a
        // card was saved, or when a Cancelled / ParseFailure prevented persistence.
        public int? CardId { get; set; }
        public Card? Card { get; set; }
    }
}
