using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Models.ReferenceData;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlipKit.Core.Tests.Services;

/// <summary>
/// Tests for <see cref="PlayerNameDirectory.BuildHintFromCard"/>. Seeds a
/// minimal in-memory DB with reference data + an imported checklist, lets
/// the directory refresh, then asserts which JSON keys land in the
/// hint's <c>VerifiedFieldNames</c> for various Card shapes.
/// </summary>
public class PlayerNameDirectoryTests
{
    private static async Task<PlayerNameDirectory> NewReadyDirectoryAsync(TestDbContext db)
    {
        // Seed reference rows (LeagueTeams, Manufacturers, Brands, etc.) and a
        // SetChecklist with one card so directory queries return matches.
        db.Context.LeagueTeams.AddRange(
            new LeagueTeam { Sport = "Football", TeamName = "Atlanta Falcons", City = "Atlanta", Mascot = "Falcons" },
            new LeagueTeam { Sport = "Baseball", TeamName = "New York Yankees", City = "New York", Mascot = "Yankees" });
        db.Context.KnownManufacturers.Add(new KnownManufacturer { Name = "Panini" });
        db.Context.KnownBrands.Add(new KnownBrand { Name = "Mosaic", Manufacturer = "Panini" });
        db.Context.KnownVariations.Add(new KnownVariation { Name = "Refractor", Type = "Parallel" });
        db.Context.GradingAuthorities.Add(new GradingAuthority { Code = "PSA", FullName = "Professional Sports Authenticator" });

        db.Context.SetChecklists.Add(new SetChecklist
        {
            Manufacturer = "Panini",
            Brand = "Mosaic",
            Year = 2024,
            Sport = "Football",
            Cards = new()
            {
                new ChecklistCard { CardNumber = "1", PlayerName = "Justin Herbert", Team = "Los Angeles Chargers", Subset = "Base" },
            },
        });
        await db.Context.SaveChangesAsync();

        var directory = new PlayerNameDirectory(db.ServiceProvider, NullLogger<PlayerNameDirectory>.Instance);
        await directory.RefreshAsync();
        return directory;
    }

    [Fact]
    public async Task BuildHintFromCard_VerifiesPlayerName_When_DirectoryHasMatch()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var card = new Card { PlayerName = "Justin Herbert" };
        var hint = directory.BuildHintFromCard(card);

        Assert.Equal("Justin Herbert", hint.PlayerName);
        Assert.Contains("player_name", hint.VerifiedFieldNames);
    }

    [Fact]
    public async Task BuildHintFromCard_DoesNotVerifyPlayerName_When_NoDirectoryMatch()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var card = new Card { PlayerName = "Some Random Person" };
        var hint = directory.BuildHintFromCard(card);

        Assert.Equal("Some Random Person", hint.PlayerName);
        Assert.DoesNotContain("player_name", hint.VerifiedFieldNames);
    }

    [Fact]
    public async Task BuildHintFromCard_VerifiesYear_When_PresentInChecklist()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card { Year = 2024 });
        Assert.Contains("year", hint.VerifiedFieldNames);

        var hintMissing = directory.BuildHintFromCard(new Card { Year = 1899 });
        Assert.DoesNotContain("year", hintMissing.VerifiedFieldNames);
    }

    [Fact]
    public async Task BuildHintFromCard_VerifiesTeamAndSport_Together_When_DirectoryRecognizesTeam()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card
        {
            Team = "Atlanta Falcons",
            Sport = Sport.Football,
        });

        Assert.Contains("team", hint.VerifiedFieldNames);
        Assert.Contains("sport", hint.VerifiedFieldNames);
        Assert.Equal("Football", hint.Sport);
    }

    [Fact]
    public async Task BuildHintFromCard_VerifiesManufacturerAndBrand_When_PresentInDirectory()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card
        {
            Manufacturer = "Panini",
            Brand = "Mosaic",
        });

        Assert.Contains("manufacturer", hint.VerifiedFieldNames);
        Assert.Contains("brand", hint.VerifiedFieldNames);
    }

    [Fact]
    public async Task BuildHintFromCard_VerifiesParallelName_When_InSeededVariations()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card { ParallelName = "Refractor" });
        Assert.Contains("parallel_name", hint.VerifiedFieldNames);

        var hintUnknown = directory.BuildHintFromCard(new Card { ParallelName = "Made-Up Pattern" });
        Assert.DoesNotContain("parallel_name", hintUnknown.VerifiedFieldNames);
    }

    [Fact]
    public async Task BuildHintFromCard_VerifiesGradeCompany_When_InSeededAuthorities()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card { GradeCompany = "PSA" });
        Assert.Contains("grade_company", hint.VerifiedFieldNames);
    }

    [Theory]
    [InlineData("/99", true)]
    [InlineData("12/99", true)]
    [InlineData("1/1", true)]
    [InlineData("Authentic", false)]   // not the /N shape
    [InlineData("99", false)]          // bare number isn't serial-shaped
    public async Task BuildHintFromCard_VerifiesSerialNumbered_OnRegexShape(string serial, bool expectedVerified)
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card { SerialNumbered = serial });
        Assert.Equal(expectedVerified, hint.VerifiedFieldNames.Contains("serial_numbered"));
    }

    [Fact]
    public async Task BuildHintFromCard_OnlyVerifiesBoolFlags_When_True()
    {
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var hint = directory.BuildHintFromCard(new Card
        {
            IsRookie = true,
            IsAuto = false,
            IsRelic = true,
            IsGraded = false,
        });

        Assert.Contains("is_rookie", hint.VerifiedFieldNames);
        Assert.Contains("is_relic",  hint.VerifiedFieldNames);
        Assert.DoesNotContain("is_auto",   hint.VerifiedFieldNames);
        Assert.DoesNotContain("is_graded", hint.VerifiedFieldNames);
    }

    [Fact]
    public async Task BuildHintFromCard_PopulatesAllHintFields_FromCard()
    {
        // Independent of which fields land in VerifiedFieldNames, every Card
        // field should be copied onto the hint so the LLM can see the value
        // (verified or as a soft suggestion).
        using var db = TestDbContext.Create();
        var directory = await NewReadyDirectoryAsync(db);

        var card = new Card
        {
            PlayerName = "X",
            Year = 2024,
            CardNumber = "42",
            Manufacturer = "Panini",
            Brand = "Mosaic",
            SetName = "2024 Mosaic Football",
            Team = "Atlanta Falcons",
            Sport = Sport.Football,
            ParallelName = "Refractor",
            SerialNumbered = "/99",
            IsRookie = true,
            IsAuto = true,
            IsRelic = false,
            IsGraded = true,
            GradeCompany = "PSA",
            GradeValue = "10",
        };
        var hint = directory.BuildHintFromCard(card);

        Assert.Equal("X", hint.PlayerName);
        Assert.Equal(2024, hint.Year);
        Assert.Equal("42", hint.CardNumber);
        Assert.Equal("Panini", hint.Manufacturer);
        Assert.Equal("Mosaic", hint.Brand);
        Assert.Equal("2024 Mosaic Football", hint.SetName);
        Assert.Equal("Atlanta Falcons", hint.Team);
        Assert.Equal("Football", hint.Sport);
        Assert.Equal("Refractor", hint.ParallelName);
        Assert.Equal("/99", hint.SerialNumbered);
        Assert.True(hint.IsRookie);
        Assert.True(hint.IsAuto);
        Assert.False(hint.IsRelic);
        Assert.True(hint.IsGraded);
        Assert.Equal("PSA", hint.GradeCompany);
        Assert.Equal("10", hint.GradeValue);
    }

    [Fact]
    public async Task BuildHintFromCard_ReturnsEmptyVerifiedSet_When_DirectoryNotReady()
    {
        // Pre-RefreshAsync: BuildHintFromCard should return a hint with field
        // values copied but no VerifiedFieldNames populated — falling back to
        // soft-hint mode in the LLM call.
        using var db = TestDbContext.Create();
        var directory = new PlayerNameDirectory(db.ServiceProvider, NullLogger<PlayerNameDirectory>.Instance);
        // NOTE: did NOT call RefreshAsync.

        var hint = directory.BuildHintFromCard(new Card
        {
            PlayerName = "Justin Herbert",
            Brand = "Mosaic",
            Year = 2024,
        });

        Assert.Equal("Justin Herbert", hint.PlayerName);
        Assert.Empty(hint.VerifiedFieldNames);
    }
}
