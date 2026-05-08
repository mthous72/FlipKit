using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations
{
    // EF-backed implementation of IModelScoreboard. Singleton — owns no state
    // beyond the IServiceProvider it uses to spin up scoped DbContexts on demand.
    public class ModelScoreboard : IModelScoreboard
    {
        // The most recent N records per model are used for score aggregation.
        // Older records stay in the table (so a "Reset" stays explicit) but
        // don't influence the live score, which keeps the rolling judgment
        // tied to current model behavior rather than ancient history.
        public const int WindowSize = 50;

        // Below this many samples, we don't show a score — the user sees
        // "Untested" instead. Avoids a wildly-confident "100% / 1 sample" tile.
        public const int MinSamplesForScore = 3;

        // Below this many samples, the score shows with a "Tentative (n)"
        // label so the user knows it's not yet a stable signal.
        public const int MinSamplesForFullConfidence = 10;

        // Field-count divisor for the user-correction penalty. The LLM produces
        // ~18 model-output fields per card (player_name, year, set, parallel,
        // etc. — see CardFieldDiff for the exact list). 18 corrections in one
        // edit = full penalty.
        public const int CorrectionDivisor = 18;

        private readonly IServiceProvider _services;
        private readonly ILogger<ModelScoreboard> _logger;

        public ModelScoreboard(IServiceProvider services, ILogger<ModelScoreboard> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task RecordSuccessAsync(string modelId, int? cardId, ScanResult result)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return;

            var highConfidence = result.Confidences?.Count(c => c.Confidence == VerificationConfidence.High) ?? 0;
            var totalConfidence = result.Confidences?.Count ?? 0;

            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

            db.ModelScanRecords.Add(new ModelScanRecord
            {
                ModelId = modelId,
                RecordedAt = DateTime.UtcNow,
                Outcome = ScanOutcome.Success,
                HighConfidenceFieldCount = totalConfidence > 0 ? highConfidence : null,
                TotalConfidenceFieldCount = totalConfidence > 0 ? totalConfidence : null,
                DriftEventCount = result.DriftEventCount,
                CardId = cardId,
            });
            await db.SaveChangesAsync();
        }

        public async Task RecordFailureAsync(string modelId, ScanOutcome outcome)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return;
            if (outcome == ScanOutcome.Success)
            {
                _logger.LogWarning("RecordFailureAsync called with Outcome=Success — use RecordSuccessAsync instead");
                return;
            }

            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

            db.ModelScanRecords.Add(new ModelScanRecord
            {
                ModelId = modelId,
                RecordedAt = DateTime.UtcNow,
                Outcome = outcome,
            });
            await db.SaveChangesAsync();
        }

        public async Task RecordUserCorrectionsAsync(int cardId, string modelId, int correctedFieldCount)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return;
            if (correctedFieldCount <= 0) return;

            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

            // Find the most recent Success record for this card and stamp the
            // correction count on it. If none exists (e.g. the scan happened
            // before this feature shipped, or the user reset history), insert
            // a synthetic row so the correction signal still lands.
            var record = await db.ModelScanRecords
                .Where(r => r.CardId == cardId && r.Outcome == ScanOutcome.Success)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                db.ModelScanRecords.Add(new ModelScanRecord
                {
                    ModelId = modelId,
                    RecordedAt = DateTime.UtcNow,
                    Outcome = ScanOutcome.Success,
                    CardId = cardId,
                    UserCorrectedFieldCount = correctedFieldCount,
                });
            }
            else
            {
                // Replace, don't accumulate — each edit is the user's *latest*
                // verdict on what the model got wrong. If they edit twice, we
                // want the second edit to be the source of truth.
                record.UserCorrectedFieldCount = correctedFieldCount;
            }

            await db.SaveChangesAsync();
        }

        public async Task ResetHistoryAsync(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return;

            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

            var rows = await db.ModelScanRecords.Where(r => r.ModelId == modelId).ToListAsync();
            if (rows.Count == 0) return;

            db.ModelScanRecords.RemoveRange(rows);
            await db.SaveChangesAsync();
        }

        public async Task<IReadOnlyDictionary<string, ModelQuality>> GetQualitiesAsync()
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();

            // Pull all records, group by model, take the most-recent WindowSize per model.
            // Volume is small (one row per scan, capped per model in practice) so we don't
            // need a fancier query.
            var distinctModelIds = await db.ModelScanRecords
                .Select(r => r.ModelId)
                .Distinct()
                .ToListAsync();

            var result = new Dictionary<string, ModelQuality>(StringComparer.OrdinalIgnoreCase);
            foreach (var modelId in distinctModelIds)
            {
                var quality = await ComputeQualityAsync(db, modelId);
                if (quality != null)
                    result[modelId] = quality;
            }
            return result;
        }

        public async Task<ModelQuality?> GetQualityAsync(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return null;

            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
            return await ComputeQualityAsync(db, modelId);
        }

        // === score math ===

        private static async Task<ModelQuality?> ComputeQualityAsync(FlipKitDbContext db, string modelId)
        {
            var records = await db.ModelScanRecords
                .Where(r => r.ModelId == modelId)
                .OrderByDescending(r => r.RecordedAt)
                .Take(WindowSize)
                .ToListAsync();

            if (records.Count == 0) return null;

            return ComputeQualityFromRecords(modelId, records);
        }

        // Internal pure-function variant — exposed for testing without an
        // in-memory DB. Callers above hand it an already-windowed list.
        public static ModelQuality ComputeQualityFromRecords(string modelId, IReadOnlyList<ModelScanRecord> records)
        {
            var sampleCount = records.Count;
            var successes = records.Where(r => r.Outcome == ScanOutcome.Success).ToList();
            var successCount = successes.Count;
            var lastUsedAt = records.Max(r => r.RecordedAt);

            decimal? avgCompleteness = null;
            decimal? avgDrift = null;
            if (successes.Count > 0)
            {
                var completenessRows = successes
                    .Where(r => r.TotalConfidenceFieldCount.HasValue && r.TotalConfidenceFieldCount.Value > 0)
                    .Select(r => (decimal)r.HighConfidenceFieldCount!.Value / r.TotalConfidenceFieldCount!.Value)
                    .ToList();
                if (completenessRows.Count > 0)
                    avgCompleteness = completenessRows.Average();

                var driftRows = successes
                    .Where(r => r.TotalConfidenceFieldCount.HasValue && r.TotalConfidenceFieldCount.Value > 0)
                    .Select(r => (decimal)(r.DriftEventCount ?? 0) / r.TotalConfidenceFieldCount!.Value)
                    .ToList();
                if (driftRows.Count > 0)
                    avgDrift = driftRows.Average();
            }

            decimal? avgCorrection = null;
            var correctionRows = records
                .Where(r => r.UserCorrectedFieldCount.HasValue && r.UserCorrectedFieldCount.Value > 0)
                .Select(r => Math.Min(1m, (decimal)r.UserCorrectedFieldCount!.Value / CorrectionDivisor))
                .ToList();
            if (correctionRows.Count > 0)
                avgCorrection = correctionRows.Average();

            decimal? score = null;
            string label;
            if (sampleCount < MinSamplesForScore)
            {
                label = "Untested";
            }
            else
            {
                var successRate = (decimal)successCount / sampleCount;
                var completeness = avgCompleteness ?? 0m;
                var driftPenalty = avgDrift ?? 0m;
                var correctionPenalty = avgCorrection ?? 0m;

                score = 100m * (
                    0.40m * successRate +
                    0.40m * completeness +
                    0.10m * (1m - driftPenalty) +
                    0.10m * (1m - correctionPenalty));

                // Clamp to [0, 100] just in case rounding nudges past the edges.
                score = Math.Max(0m, Math.Min(100m, score.Value));

                label = sampleCount < MinSamplesForFullConfidence
                    ? $"Tentative ({sampleCount})"
                    : "Healthy";
            }

            return new ModelQuality(
                ModelId: modelId,
                Score: score,
                SampleCount: sampleCount,
                SuccessCount: successCount,
                AverageCompleteness: avgCompleteness,
                LastUsedAt: lastUsedAt,
                ConfidenceLabel: label);
        }
    }
}
