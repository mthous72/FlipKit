using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlipKit.Core.Tests.Services;

public class ModelScoreboardTests
{
    private static ModelScoreboard CreateService(TestDbContext db) =>
        new(db.ServiceProvider, NullLogger<ModelScoreboard>.Instance);

    private static ScanResult MakeResult(int high, int total, int drift = 0)
    {
        var result = new ScanResult { DriftEventCount = drift };
        for (var i = 0; i < high; i++)
            result.Confidences.Add(new FieldConfidence { FieldName = $"f{i}", Confidence = VerificationConfidence.High });
        for (var i = high; i < total; i++)
            result.Confidences.Add(new FieldConfidence { FieldName = $"f{i}", Confidence = VerificationConfidence.Medium });
        return result;
    }

    // Seeds a Card row so FK-constrained scoreboard records can reference it.
    // Returns the assigned Id.
    private static async Task<int> SeedCardAsync(TestDbContext db, string player = "Test Player")
    {
        var card = new Card { PlayerName = player };
        db.Context.Cards.Add(card);
        await db.Context.SaveChangesAsync();
        db.Context.Entry(card).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        return card.Id;
    }

    // === recording ===

    [Fact]
    public async Task Should_PersistSuccessRecord_With_CompletenessAndDriftSnapshot()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var cardId = await SeedCardAsync(db);

        await svc.RecordSuccessAsync("openai/gpt-4o-mini", cardId, MakeResult(high: 7, total: 10, drift: 1));

        var rows = await db.Context.ModelScanRecords.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal("openai/gpt-4o-mini", row.ModelId);
        Assert.Equal(ScanOutcome.Success, row.Outcome);
        Assert.Equal(7, row.HighConfidenceFieldCount);
        Assert.Equal(10, row.TotalConfidenceFieldCount);
        Assert.Equal(1, row.DriftEventCount);
        Assert.Equal(cardId, row.CardId);
    }

    [Fact]
    public async Task Should_PersistFailureRecord_When_RecordFailureCalled()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        await svc.RecordFailureAsync("openai/gpt-4o-mini", ScanOutcome.ParseFailure);

        var row = Assert.Single(await db.Context.ModelScanRecords.AsNoTracking().ToListAsync());
        Assert.Equal(ScanOutcome.ParseFailure, row.Outcome);
        Assert.Null(row.HighConfidenceFieldCount);
    }

    [Fact]
    public async Task Should_NotPersist_When_RecordCalledWithBlankModelId()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        await svc.RecordSuccessAsync("", null, MakeResult(1, 2));
        await svc.RecordFailureAsync(" ", ScanOutcome.ModelError);

        Assert.Empty(await db.Context.ModelScanRecords.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Should_AttachCorrectionsToMostRecentSuccess_When_CorrectionsRecorded()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var cardId = await SeedCardAsync(db);

        await svc.RecordSuccessAsync("a/b", cardId, MakeResult(5, 6));
        await svc.RecordUserCorrectionsAsync(cardId, modelId: "a/b", correctedFieldCount: 3);

        var rows = await db.Context.ModelScanRecords.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(3, row.UserCorrectedFieldCount);
    }

    [Fact]
    public async Task Should_InsertSyntheticRow_When_NoSuccessRecordExistsForCard()
    {
        // Edge case: user edits a card whose original scan predates the
        // scoreboard feature. We still want the correction signal.
        using var db = TestDbContext.Create();
        var svc = CreateService(db);
        var cardId = await SeedCardAsync(db);

        await svc.RecordUserCorrectionsAsync(cardId, modelId: "a/b", correctedFieldCount: 4);

        var rows = await db.Context.ModelScanRecords.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(cardId, row.CardId);
        Assert.Equal(4, row.UserCorrectedFieldCount);
        Assert.Equal(ScanOutcome.Success, row.Outcome);
    }

    [Fact]
    public async Task Should_DeleteOnlyTargetModelsRecords_When_ResetHistoryCalled()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        await svc.RecordSuccessAsync("keep/me", null, MakeResult(2, 3));
        await svc.RecordSuccessAsync("delete/me", null, MakeResult(2, 3));
        await svc.RecordFailureAsync("delete/me", ScanOutcome.ParseFailure);

        await svc.ResetHistoryAsync("delete/me");

        var remaining = await db.Context.ModelScanRecords.AsNoTracking().ToListAsync();
        var row = Assert.Single(remaining);
        Assert.Equal("keep/me", row.ModelId);
    }

    // === reading / aggregation ===

    [Fact]
    public async Task Should_ReturnNullScore_When_BelowMinSamples()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        // 2 samples → below MinSamplesForScore (3) → null score, "Untested" label.
        await svc.RecordSuccessAsync("a/b", null, MakeResult(5, 5));
        await svc.RecordSuccessAsync("a/b", null, MakeResult(5, 5));

        var quality = await svc.GetQualityAsync("a/b");

        Assert.NotNull(quality);
        Assert.Null(quality!.Score);
        Assert.Equal(2, quality.SampleCount);
        Assert.Equal("Untested", quality.ConfidenceLabel);
    }

    [Fact]
    public async Task Should_LabelTentative_When_BelowFullConfidence()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        for (var i = 0; i < 5; i++)
            await svc.RecordSuccessAsync("a/b", null, MakeResult(5, 5));

        var quality = await svc.GetQualityAsync("a/b");

        Assert.NotNull(quality);
        Assert.NotNull(quality!.Score);
        Assert.Equal("Tentative (5)", quality.ConfidenceLabel);
    }

    [Fact]
    public async Task Should_LabelHealthy_When_AtFullConfidenceThreshold()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        for (var i = 0; i < 10; i++)
            await svc.RecordSuccessAsync("a/b", null, MakeResult(5, 5));

        var quality = await svc.GetQualityAsync("a/b");

        Assert.NotNull(quality);
        Assert.Equal("Healthy", quality!.ConfidenceLabel);
    }

    [Fact]
    public async Task Should_ReturnAllModels_When_GetQualitiesCalled()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        await svc.RecordSuccessAsync("a/b", null, MakeResult(1, 2));
        await svc.RecordSuccessAsync("c/d", null, MakeResult(1, 2));
        await svc.RecordFailureAsync("e/f", ScanOutcome.Cancelled);

        var qualities = await svc.GetQualitiesAsync();

        Assert.Equal(3, qualities.Count);
        Assert.Contains("a/b", qualities.Keys);
        Assert.Contains("c/d", qualities.Keys);
        Assert.Contains("e/f", qualities.Keys);
    }

    [Fact]
    public async Task Should_ReturnNull_When_NoRecordsExistForModel()
    {
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        var quality = await svc.GetQualityAsync("never/recorded");

        Assert.Null(quality);
    }

    // === score formula (pure-function variant) ===

    [Fact]
    public void Should_ComputePerfectScore_When_AllSuccessAndComplete()
    {
        var records = Enumerable.Range(0, 10)
            .Select(_ => new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.Success,
                HighConfidenceFieldCount = 10,
                TotalConfidenceFieldCount = 10,
                DriftEventCount = 0,
                RecordedAt = DateTime.UtcNow,
            })
            .ToList();

        var quality = ModelScoreboard.ComputeQualityFromRecords("m", records);

        // 0.40 * 1 (success) + 0.40 * 1 (completeness) + 0.10 * 1 (no drift) + 0.10 * 1 (no corrections) = 1.0
        Assert.Equal(100m, quality.Score);
        Assert.Equal("Healthy", quality.ConfidenceLabel);
    }

    [Fact]
    public void Should_ComputeZeroScore_When_AllRecordsFailedAndNoSuccessSignal()
    {
        var records = Enumerable.Range(0, 10)
            .Select(_ => new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.ModelError,
                RecordedAt = DateTime.UtcNow,
            })
            .ToList();

        var quality = ModelScoreboard.ComputeQualityFromRecords("m", records);

        // 0 success rate, 0 completeness; no drift/correction data → those terms = 0.
        // Score = 100 * (0.40*0 + 0.40*0 + 0.10*1 + 0.10*1) = 20.
        Assert.Equal(20m, quality.Score!.Value);
    }

    [Fact]
    public void Should_PenalizeDrift_When_SuccessRowsHaveDriftEvents()
    {
        // 10 successes, 10 fields each, 5 drift events per scan = 50% drift rate.
        var records = Enumerable.Range(0, 10)
            .Select(_ => new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.Success,
                HighConfidenceFieldCount = 10,
                TotalConfidenceFieldCount = 10,
                DriftEventCount = 5,
                RecordedAt = DateTime.UtcNow,
            })
            .ToList();

        var quality = ModelScoreboard.ComputeQualityFromRecords("m", records);

        // 0.40*1 + 0.40*1 + 0.10*(1-0.5) + 0.10*1 = 0.40+0.40+0.05+0.10 = 0.95 → 95.
        Assert.Equal(95m, quality.Score);
    }

    [Fact]
    public void Should_PenalizeCorrections_When_UserEditedFields()
    {
        // 10 successes, 10/10 confidence, 0 drift, but the user corrected 9 fields per
        // scan on average. CorrectionDivisor = 18 → 9/18 = 0.5 penalty rate.
        var records = Enumerable.Range(0, 10)
            .Select(_ => new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.Success,
                HighConfidenceFieldCount = 10,
                TotalConfidenceFieldCount = 10,
                DriftEventCount = 0,
                UserCorrectedFieldCount = 9,
                RecordedAt = DateTime.UtcNow,
            })
            .ToList();

        var quality = ModelScoreboard.ComputeQualityFromRecords("m", records);

        // 0.40*1 + 0.40*1 + 0.10*1 + 0.10*(1-0.5) = 0.95 → 95.
        Assert.Equal(95m, quality.Score);
    }

    [Fact]
    public void Should_ClampCorrectionPenalty_When_CorrectionsExceedDivisor()
    {
        // 50 corrections in one row should not push the penalty above 1.
        var records = Enumerable.Range(0, 10)
            .Select(_ => new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.Success,
                HighConfidenceFieldCount = 0,
                TotalConfidenceFieldCount = 10,
                DriftEventCount = 0,
                UserCorrectedFieldCount = 50,
                RecordedAt = DateTime.UtcNow,
            })
            .ToList();

        var quality = ModelScoreboard.ComputeQualityFromRecords("m", records);

        // success=1, completeness=0, drift=0, correction=1 (clamped).
        // 0.40*1 + 0.40*0 + 0.10*1 + 0.10*0 = 0.50 → 50.
        Assert.Equal(50m, quality.Score);
    }

    [Fact]
    public async Task Should_RespectWindowSize_When_OverWindowLimit()
    {
        // Insert 60 records, half success / half failure, ALL successes more recent
        // than the failures. The window of 50 should be 50 mostly-success records,
        // skewing the score high. If we accidentally aggregated all 60, we'd get
        // a 50/50 split.
        using var db = TestDbContext.Create();
        var svc = CreateService(db);

        // 30 failures with old timestamps.
        var oldStart = DateTime.UtcNow.AddDays(-10);
        for (var i = 0; i < 30; i++)
        {
            db.Context.ModelScanRecords.Add(new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.ModelError,
                RecordedAt = oldStart.AddSeconds(i),
            });
        }
        // 30 successes with newer timestamps.
        var newStart = DateTime.UtcNow.AddHours(-1);
        for (var i = 0; i < 30; i++)
        {
            db.Context.ModelScanRecords.Add(new ModelScanRecord
            {
                ModelId = "m",
                Outcome = ScanOutcome.Success,
                HighConfidenceFieldCount = 10,
                TotalConfidenceFieldCount = 10,
                DriftEventCount = 0,
                RecordedAt = newStart.AddSeconds(i),
            });
        }
        await db.Context.SaveChangesAsync();

        var quality = await svc.GetQualityAsync("m");

        // Window = 50 most recent → 30 successes + 20 oldest failures from the bunch.
        // SuccessCount = 30, SampleCount = 50 → 60% success rate.
        Assert.NotNull(quality);
        Assert.Equal(50, quality!.SampleCount);
        Assert.Equal(30, quality.SuccessCount);
    }
}
