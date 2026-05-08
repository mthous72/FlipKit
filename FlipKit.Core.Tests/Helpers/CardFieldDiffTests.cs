using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Tests.Helpers;

public class CardFieldDiffTests
{
    private static Card MakeAiCard() => new()
    {
        PlayerName = "Mike Trout",
        CardNumber = "1",
        Year = 2026,
        Sport = Sport.Baseball,
        Manufacturer = "Topps",
        Brand = "Bowman",
        SetName = "Bowman Chrome",
        Team = "Angels",
        VariationType = "Refractor",
        ParallelName = "Refractor",
        SerialNumbered = "/150",
        IsRookie = true,
        IsAuto = false,
        IsRelic = false,
        IsShortPrint = false,
        IsGraded = false,
    };

    [Fact]
    public void Should_ReturnZero_When_NothingChanged()
    {
        var before = MakeAiCard();
        var after = MakeAiCard();

        Assert.Equal(0, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_CountStringChanges()
    {
        var before = MakeAiCard();
        var after = MakeAiCard();
        after.PlayerName = "Mike Trout, Jr.";  // user fixed name typo
        after.SetName = "Bowman Chrome Sapphire"; // user picked correct sub-set
        after.ParallelName = "Refractor /150"; // refined parallel

        Assert.Equal(3, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_TreatNullAndEmpty_AsEquivalent()
    {
        var before = new Card { PlayerName = "Player", CardNumber = "" };
        var after = new Card { PlayerName = "Player", CardNumber = null };

        Assert.Equal(0, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_CountFilledInBlank_AsCorrection()
    {
        // Model didn't produce the parallel; user filled it in. That's one
        // correction — the model's omission counts against it.
        var before = MakeAiCard();
        before.ParallelName = null;
        var after = MakeAiCard();
        after.ParallelName = "Gold Refractor";

        Assert.Equal(1, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_CountClearedWrongValue_AsCorrection()
    {
        // User wiped out a wrong value the model produced. Counts.
        var before = MakeAiCard();
        before.ParallelName = "Wrong";
        var after = MakeAiCard();
        after.ParallelName = null;

        Assert.Equal(1, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_CountBooleanFlips()
    {
        var before = MakeAiCard();
        var after = MakeAiCard();
        after.IsRookie = false; // model said RC; user said no
        after.IsAuto = true;    // model missed the auto; user added

        Assert.Equal(2, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_CountYearAndSportChanges()
    {
        var before = MakeAiCard();
        var after = MakeAiCard();
        after.Year = 2025;        // model misread year
        after.Sport = Sport.Basketball;  // model misclassified sport

        Assert.Equal(2, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_IgnoreCostAndSalesFields_When_Diffing()
    {
        // Cost/sale/pricing fields are user-entered — the model never produces
        // them, so changes there should never count as model corrections.
        var before = MakeAiCard();
        var after = MakeAiCard();
        after.CostBasis = 5.00m;
        after.EstimatedValue = 25m;
        after.ListingPrice = 30m;
        after.SalePrice = 27m;
        after.Quantity = 5;
        after.Notes = "long notes here";
        after.ShippingProfile = "BMWT";

        Assert.Equal(0, CardFieldDiff.CountUserCorrections(before, after));
    }

    [Fact]
    public void Should_BeCaseInsensitive_ForStringCompare()
    {
        var before = MakeAiCard();
        before.Manufacturer = "TOPPS";
        var after = MakeAiCard();
        after.Manufacturer = "Topps"; // same word, different case — not a correction

        Assert.Equal(0, CardFieldDiff.CountUserCorrections(before, after));
    }
}
