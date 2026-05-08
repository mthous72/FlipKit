using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    // Persists per-model scan telemetry (ModelScanRecord rows) and aggregates them
    // into a quality score the pickers can sort by. Local-only — SQLite is not
    // synced across devices, which matches the per-device "what models work for ME"
    // intent of this feature.
    public interface IModelScoreboard
    {
        // === recording ===

        // Persist a successful scan. Pulls completeness (high-confidence field
        // count / total) and drift count straight out of ScanResult.
        Task RecordSuccessAsync(string modelId, int? cardId, ScanResult result);

        // Persist a non-billing failure. Outcomes: ParseFailure | ModelError |
        // Cancelled. 402 / 429 belong elsewhere — they don't penalize accuracy.
        Task RecordFailureAsync(string modelId, ScanOutcome outcome);

        // User corrected fields after the model produced them. Updates the most
        // recent Success record for the card if one exists; otherwise inserts a
        // bookkeeping row so the signal still counts.
        Task RecordUserCorrectionsAsync(int cardId, string modelId, int correctedFieldCount);

        // Wipe all telemetry for a single model. Powers the per-row "Reset"
        // button on the leaderboard.
        Task ResetHistoryAsync(string modelId);

        // === reading ===

        // Aggregate score per model. Empty dictionary when no records exist.
        Task<IReadOnlyDictionary<string, ModelQuality>> GetQualitiesAsync();

        // Single-model lookup. Null when no records exist for that model.
        Task<ModelQuality?> GetQualityAsync(string modelId);
    }

    // Aggregated per-model quality snapshot.
    public sealed record ModelQuality(
        string ModelId,
        decimal? Score,                  // 0–100, null when SampleCount < MinSamplesForScore
        int SampleCount,
        int SuccessCount,
        decimal? AverageCompleteness,    // 0–1, null when no successes
        DateTime? LastUsedAt,
        string ConfidenceLabel);         // "Untested" | "Tentative (n samples)" | "Healthy"
}
