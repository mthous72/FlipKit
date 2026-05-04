using FlipKit.Core.Data;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Core.Tests.Services;

public class ChecklistVerificationMatcherTests
{
    private static SetChecklist BowmanChecklist() => new()
    {
        Manufacturer = "Topps",
        Brand = "Bowman",
        Year = 2026,
        Sport = "Baseball",
        Cards = new()
        {
            new ChecklistCard { CardNumber = "1", PlayerName = "Roman Anthony", Team = "Boston Red Sox", Subset = "Base" },
            new ChecklistCard { CardNumber = "2", PlayerName = "Jackson Holliday", Team = "Baltimore Orioles", Subset = "Base" },
            new ChecklistCard { CardNumber = "BCP1", PlayerName = "Roman Anthony", Team = "Boston Red Sox", Subset = "Chrome Prospects" },
        },
    };

    private static async Task SeedAsync(TestDbContext db, SetChecklist checklist)
    {
        using var scope = db.ServiceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
        ctx.SetChecklists.Add(checklist);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task MatchAsync_ReturnsChecklistMissing_WhenNoChecklistExistsForSet()
    {
        using var db = TestDbContext.Create();
        var sut = new ChecklistVerificationMatcher(db.ServiceProvider);

        var card = new Card { Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball, PlayerName = "X" };
        var result = await sut.MatchAsync(card);

        Assert.True(result.ChecklistMissing);
        Assert.Equal(VerificationTier.NoMatch, result.Tier);
    }

    [Fact]
    public async Task MatchAsync_ReturnsChecklistMissing_WhenCardLacksMetadata()
    {
        using var db = TestDbContext.Create();
        var sut = new ChecklistVerificationMatcher(db.ServiceProvider);

        var result = await sut.MatchAsync(new Card { PlayerName = "X" }); // no manufacturer/brand/year

        Assert.True(result.ChecklistMissing);
    }

    [Fact]
    public async Task MatchAsync_ReturnsTierVerified_OnExactMatchWithHighConfidences()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, BowmanChecklist());
        var sut = new ChecklistVerificationMatcher(db.ServiceProvider);

        var card = new Card
        {
            Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball,
            CardNumber = "1", PlayerName = "Roman Anthony",
        };

        var result = await sut.MatchAsync(card);

        Assert.Equal(VerificationTier.Verified, result.Tier);
        Assert.NotNull(result.ExactMatch);
        Assert.Equal("Roman Anthony", result.ExactMatch!.PlayerName);
        Assert.False(string.IsNullOrEmpty(result.MatchKey));
    }

    [Fact]
    public async Task MatchAsync_ReturnsTierBestGuess_OnExactCardNumberMatchButLowParallelConfidence()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, BowmanChecklist());
        var sut = new ChecklistVerificationMatcher(db.ServiceProvider);

        // Exact card-number + player match, but parallel name doesn't appear in subset.
        var card = new Card
        {
            Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball,
            CardNumber = "1", PlayerName = "Roman Anthony", ParallelName = "Mystery Refractor",
        };

        var result = await sut.MatchAsync(card);

        Assert.Equal(VerificationTier.BestGuess, result.Tier);
        Assert.NotNull(result.ExactMatch);
        Assert.Contains(result.FieldConfidences, f => f.FieldName == "parallel" && f.Confidence < 0.85);
    }

    [Fact]
    public async Task MatchAsync_ReturnsTierNoMatch_WithCandidates_WhenCardNumberNotInSet()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, BowmanChecklist());
        var sut = new ChecklistVerificationMatcher(db.ServiceProvider);

        var card = new Card
        {
            Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball,
            CardNumber = "999", PlayerName = "Roman Anthony",
        };

        var result = await sut.MatchAsync(card);

        Assert.Equal(VerificationTier.NoMatch, result.Tier);
        Assert.Null(result.ExactMatch);
        Assert.NotEmpty(result.Candidates);
        Assert.Contains(result.Candidates, c => c.PlayerName == "Roman Anthony");
    }

    [Fact]
    public async Task MatchAsync_PopulatesMatchKey_OnVerifiedMatch()
    {
        using var db = TestDbContext.Create();
        var checklist = BowmanChecklist();
        await SeedAsync(db, checklist);
        var sut = new ChecklistVerificationMatcher(db.ServiceProvider);

        var card = new Card
        {
            Manufacturer = "Topps", Brand = "Bowman", Year = 2026, Sport = Sport.Baseball,
            CardNumber = "1", PlayerName = "Roman Anthony",
        };

        var result = await sut.MatchAsync(card);

        Assert.NotEmpty(result.MatchKey);
        Assert.Contains(":1:", result.MatchKey); // composite key includes normalized card number
    }
}
