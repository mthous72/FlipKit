using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class VariationVerifierServiceTests
{
    private static ISettingsService Settings()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings());
        return settings;
    }

    private static IScannerService MockScanner() => Substitute.For<IScannerService>();

    private static SetChecklist SeededChecklist()
    {
        return new SetChecklist
        {
            Manufacturer = "Topps",
            Brand = "Bowman",
            Year = 2026,
            Sport = "Baseball",
            TotalBaseCards = 3,
            Cards = new List<ChecklistCard>
            {
                new() { CardNumber = "1", PlayerName = "Mike Trout", IsRookie = false },
                new() { CardNumber = "2", PlayerName = "Aaron Judge", IsRookie = false },
                new() { CardNumber = "3", PlayerName = "Shohei Ohtani", IsRookie = false },
            },
            KnownVariations = new List<string> { "Base", "Refractor", "Gold Refractor" },
        };
    }

    private static ScanResult ScanResultFor(Card card) => new()
    {
        Card = card,
        VisualCues = null,
        AllVisibleText = new(),
        Confidences = new(),
    };

    // === Missing metadata path ===

    [Fact]
    public async Task Should_ReturnLowConfidence_When_CardIsMissingManufacturerOrBrandOrYear()
    {
        using var db = TestDbContext.Create();
        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card { PlayerName = "Unknown", Manufacturer = null, Brand = null };

        var result = await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.Equal(VerificationConfidence.Low, result.OverallConfidence);
        Assert.Contains(result.Warnings, w => w.Contains("Missing"));
    }

    // === No checklist path ===

    [Fact]
    public async Task Should_LogMissingChecklist_When_NoSetChecklistMatches()
    {
        using var db = TestDbContext.Create();
        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card { PlayerName = "X", Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball };

        var result = await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.Equal(VerificationConfidence.Low, result.OverallConfidence);
        Assert.Single(db.Context.MissingChecklists);
        Assert.Equal(1, db.Context.MissingChecklists.First().HitCount);
    }

    [Fact]
    public async Task Should_IncrementHitCount_When_MissingChecklistAlreadyLogged()
    {
        using var db = TestDbContext.Create();
        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card { PlayerName = "X", Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball };

        await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");
        await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.Single(db.Context.MissingChecklists);
        Assert.Equal(2, db.Context.MissingChecklists.First().HitCount);
    }

    // === Checklist match path ===

    [Fact]
    public async Task Should_VerifyCardNumber_When_NumberMatchesChecklist()
    {
        using var db = TestDbContext.Create();
        db.Context.SetChecklists.Add(SeededChecklist());
        await db.Context.SaveChangesAsync();

        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card
        {
            PlayerName = "Mike Trout", CardNumber = "1", Manufacturer = "Topps",
            Brand = "Bowman", Year = 2026, Sport = Sport.Baseball,
            VariationType = "Base", ParallelName = null,
        };

        var result = await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.True(result.CardNumberVerified);
        Assert.True(result.PlayerVerified);
    }

    [Fact]
    public async Task Should_FlagPlayerConflict_When_NumberMatchesButPlayerDoesnt()
    {
        using var db = TestDbContext.Create();
        db.Context.SetChecklists.Add(SeededChecklist());
        await db.Context.SaveChangesAsync();

        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card
        {
            PlayerName = "Wrong Player", CardNumber = "1", // checklist has "Mike Trout" at #1
            Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball,
            VariationType = "Base",
        };

        var result = await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.Equal(VerificationConfidence.Conflict, result.OverallConfidence);
        Assert.Equal("Mike Trout", result.SuggestedPlayerName);
    }

    [Fact]
    public async Task Should_VerifyKnownVariation_When_ParallelMatchesChecklist()
    {
        using var db = TestDbContext.Create();
        db.Context.SetChecklists.Add(SeededChecklist());
        await db.Context.SaveChangesAsync();

        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card
        {
            PlayerName = "Mike Trout", CardNumber = "1", ParallelName = "Refractor",
            VariationType = "Refractor", Manufacturer = "Topps", Brand = "Bowman",
            Year = 2026, Sport = Sport.Baseball,
        };

        var result = await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.True(result.VariationVerified);
    }

    [Fact]
    public async Task Should_FlagConflict_When_ParallelIsHallucinated()
    {
        using var db = TestDbContext.Create();
        db.Context.SetChecklists.Add(SeededChecklist());
        await db.Context.SaveChangesAsync();

        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());
        var card = new Card
        {
            PlayerName = "Mike Trout", CardNumber = "1",
            ParallelName = "Sparkly Glittery Diamond", // not in known variations, fuzzy below threshold
            VariationType = "Insert", Manufacturer = "Topps", Brand = "Bowman",
            Year = 2026, Sport = Sport.Baseball,
        };

        var result = await verifier.VerifyCardAsync(ScanResultFor(card), "img.jpg");

        Assert.Equal(VerificationConfidence.Conflict, result.OverallConfidence);
        Assert.Contains(result.Warnings, w => w.Contains("hallucination", StringComparison.OrdinalIgnoreCase));
    }

    // === NeedsConfirmationPass ===

    [Fact]
    public void Should_NeedConfirmationPass_When_OverallIsLowOrConflict()
    {
        using var db = TestDbContext.Create();
        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());

        var low = new VerificationResult { OverallConfidence = VerificationConfidence.Low };
        var conflict = new VerificationResult { OverallConfidence = VerificationConfidence.Conflict };
        var high = new VerificationResult { OverallConfidence = VerificationConfidence.High };

        Assert.True(verifier.NeedsConfirmationPass(low));
        Assert.True(verifier.NeedsConfirmationPass(conflict));
        Assert.False(verifier.NeedsConfirmationPass(high));
    }

    [Fact]
    public void Should_NeedConfirmationPass_When_AnyFieldIsConflictOrSuggestionsExist()
    {
        using var db = TestDbContext.Create();
        var verifier = new VariationVerifierService(db.Context, MockScanner(), Settings());

        var withSuggestion = new VerificationResult
        {
            OverallConfidence = VerificationConfidence.High,
            Suggestions = { "consider X" },
        };

        Assert.True(verifier.NeedsConfirmationPass(withSuggestion));
    }
}
