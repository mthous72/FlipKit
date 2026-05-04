using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

/// <summary>
/// Phase 4e gap-fill — covers RunConfirmationPassAsync, the second-LLM-pass path
/// that gets triggered when initial verification is low/conflict confidence.
/// Original Phase 4b tests covered the first-pass verification; this finishes
/// the round-trip.
/// </summary>
public class VariationVerifierServiceFillInTests
{
    private static SetChecklist SeededChecklist() => new()
    {
        Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = "Baseball",
        Cards = new() { new ChecklistCard { CardNumber = "1", PlayerName = "Mike Trout" } },
        KnownVariations = new() { "Refractor" },
    };

    private static (VariationVerifierService svc, IScannerService scanner) Build(TestDbContext db)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { DefaultModel = "test/model:free" });
        var scanner = Substitute.For<IScannerService>();
        var svc = new VariationVerifierService(db.Context, scanner, settings);
        return (svc, scanner);
    }

    [Fact]
    public async Task Should_ApplyVariationConfirmation_When_ConfirmationPassReturnsVariationConfirmed()
    {
        using var db = TestDbContext.Create();
        var (svc, scanner) = Build(db);
        scanner.SendCustomPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
               .Returns(@"{""variation_confirmed"": ""Gold Refractor""}");

        var verification = new VerificationResult
        {
            OverallConfidence = VerificationConfidence.Conflict,
            FieldConfidences = { new FieldConfidence { FieldName = "parallel_name", Confidence = VerificationConfidence.Conflict, Reason = "x" } },
            Suggestions = { "AI identified parallel as 'X' — did you mean ..." },
        };
        var scanResult = new ScanResult { Card = new Card { PlayerName = "X" }, AllVisibleText = new(), Confidences = new() };

        var updated = await svc.RunConfirmationPassAsync(scanResult, verification, "/tmp/x.jpg");

        Assert.Equal("Gold Refractor", updated.SuggestedVariation);
        // Old "did you mean" suggestion was replaced by the confirmed-variation one.
        Assert.DoesNotContain(updated.Suggestions, s => s.Contains("did you mean"));
        Assert.Contains(updated.Suggestions, s => s.Contains("Gold Refractor"));
    }

    [Fact]
    public async Task Should_ApplyPlayerConfirmation_When_ConfirmationPassReturnsPlayerConfirmed()
    {
        using var db = TestDbContext.Create();
        var (svc, scanner) = Build(db);
        scanner.SendCustomPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
               .Returns(@"{""player_confirmed"": ""Mike Trout""}");

        var verification = new VerificationResult
        {
            OverallConfidence = VerificationConfidence.Conflict,
            SuggestedPlayerName = "Original Suggestion",
        };
        var scanResult = new ScanResult { Card = new Card { PlayerName = "Wrong" }, AllVisibleText = new(), Confidences = new() };

        var updated = await svc.RunConfirmationPassAsync(scanResult, verification, "/tmp/x.jpg");

        Assert.Equal("Mike Trout", updated.SuggestedPlayerName);
    }

    [Fact]
    public async Task Should_AddSerialNumberSuggestion_When_ConfirmationPassDetectsNumbering()
    {
        using var db = TestDbContext.Create();
        var (svc, scanner) = Build(db);
        scanner.SendCustomPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
               .Returns(@"{""is_numbered"": ""yes"", ""serial_text"": ""045/199""}");

        var verification = new VerificationResult();
        var scanResult = new ScanResult { Card = new Card(), AllVisibleText = new(), Confidences = new() };

        var updated = await svc.RunConfirmationPassAsync(scanResult, verification, "/tmp/x.jpg");

        Assert.Contains(updated.Suggestions, s => s.Contains("045/199"));
    }

    [Fact]
    public async Task Should_AddWarningButNotThrow_When_ConfirmationPassReturnsBadJson()
    {
        using var db = TestDbContext.Create();
        var (svc, scanner) = Build(db);
        scanner.SendCustomPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
               .Returns("not even json");

        var verification = new VerificationResult();
        var scanResult = new ScanResult { Card = new Card(), AllVisibleText = new(), Confidences = new() };

        var updated = await svc.RunConfirmationPassAsync(scanResult, verification, "/tmp/x.jpg");

        Assert.Contains(updated.Warnings, w => w.Contains("Confirmation pass failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Should_StripJsonCodeBlocksFromResponse_When_ConfirmationPassReturnsMarkdownWrappedJson()
    {
        using var db = TestDbContext.Create();
        var (svc, scanner) = Build(db);
        scanner.SendCustomPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
               .Returns("```json\n{\"variation_confirmed\": \"Refractor\"}\n```");

        var verification = new VerificationResult();
        var scanResult = new ScanResult { Card = new Card(), AllVisibleText = new(), Confidences = new() };

        var updated = await svc.RunConfirmationPassAsync(scanResult, verification, "/tmp/x.jpg");

        Assert.Equal("Refractor", updated.SuggestedVariation);
    }

    // === GetChecklistAsync round-trip ===

    [Fact]
    public async Task Should_ReturnChecklistMatchingAllFields_When_GetChecklistFiltersByMfgBrandYearSport()
    {
        using var db = TestDbContext.Create();
        db.Context.SetChecklists.Add(SeededChecklist());
        await db.Context.SaveChangesAsync();
        var (svc, _) = Build(db);

        var found = await svc.GetChecklistAsync("Topps", "Bowman", 2026, "Baseball");

        Assert.NotNull(found);
        Assert.Equal("Bowman", found!.Brand);
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetChecklistFindsNoMatch()
    {
        using var db = TestDbContext.Create();
        var (svc, _) = Build(db);

        var found = await svc.GetChecklistAsync("Topps", "Bowman", 2026, "Baseball");

        Assert.Null(found);
    }
}
