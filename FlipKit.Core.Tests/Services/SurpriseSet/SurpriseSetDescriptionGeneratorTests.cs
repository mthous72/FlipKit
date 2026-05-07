using System.Collections.Generic;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.Implementations.SurpriseSets;

namespace FlipKit.Core.Tests.Services.SurpriseSet;

public class SurpriseSetDescriptionGeneratorTests
{
    private static readonly SurpriseSetDescriptionGenerator _gen = new();

    private static Models.SurpriseSet BaseSet() => new()
    {
        Name = "My Set",
        SpotPrice = 10m,
        SharedCondition = "Near Mint",
        SharedShippingProfile = "Standard",
    };

    private static Card RawCard(Sport? sport = null) => new()
    {
        PlayerName = "Test Player",
        IsGraded = false,
        Sport = sport,
    };

    private static Card GradedCard(Sport? sport = null) => new()
    {
        PlayerName = "Graded Player",
        IsGraded = true,
        Sport = sport,
    };

    // === Header ===

    [Fact]
    public void Should_UseShowName_When_ShowNameIsSet()
    {
        var set = BaseSet();
        set.ShowName = "The Big Show";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.StartsWith("The Big Show — Surprise Set", result);
    }

    [Fact]
    public void Should_UseName_When_ShowNameIsNullOrWhiteSpace()
    {
        var set = BaseSet();
        set.ShowName = null;
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.StartsWith("My Set — Surprise Set", result);
    }

    [Fact]
    public void Should_UseName_When_ShowNameIsWhitespace()
    {
        var set = BaseSet();
        set.ShowName = "   ";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.StartsWith("My Set — Surprise Set", result);
    }

    // === Card count ===

    [Fact]
    public void Should_UseSingular_When_OneCard()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("- 1 card in this set", result);
    }

    [Fact]
    public void Should_UsePlural_When_MultipleCards()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard(), RawCard() });
        Assert.Contains("- 2 cards in this set", result);
    }

    [Fact]
    public void Should_ProduceOutput_When_EmptyCardList()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card>());
        Assert.Contains("- 0 cards in this set", result);
        Assert.DoesNotContain("graded", result);
    }

    // === Spot price ===

    [Fact]
    public void Should_IncludeSpotPrice_When_GreaterThanZero()
    {
        var set = BaseSet();
        set.SpotPrice = 24.99m;
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("Spot price:", result);
        Assert.Contains("24", result); // currency-formatted value present
    }

    [Fact]
    public void Should_OmitSpotPrice_When_Zero()
    {
        var set = BaseSet();
        set.SpotPrice = 0m;
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.DoesNotContain("Spot price:", result);
    }

    // === Condition ===

    [Fact]
    public void Should_IncludeCondition_When_Set()
    {
        var set = BaseSet();
        set.SharedCondition = "Lightly Played";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("Condition: Lightly Played", result);
    }

    [Fact]
    public void Should_OmitCondition_When_Empty()
    {
        var set = BaseSet();
        set.SharedCondition = "";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.DoesNotContain("Condition:", result);
    }

    // === Graded / raw breakdown ===

    [Fact]
    public void Should_SayAllGraded_When_AllCardsAreGraded()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { GradedCard(), GradedCard() });
        Assert.Contains("All cards are professionally graded", result);
    }

    [Fact]
    public void Should_SayAllRaw_When_NoCardsAreGraded()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard(), RawCard() });
        Assert.Contains("Raw (ungraded) cards", result);
    }

    [Fact]
    public void Should_ShowMixedBreakdown_When_SomeGradedSomeRaw()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { GradedCard(), RawCard(), RawCard() });
        Assert.Contains("1 graded, 2 raw (ungraded)", result);
    }

    // === Sports ===

    [Fact]
    public void Should_ShowSingleSport_When_OneDistinctSport()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card>
        {
            RawCard(Sport.Baseball),
            RawCard(Sport.Baseball),
        });
        Assert.Contains("Sport: Baseball", result);
    }

    [Fact]
    public void Should_ShowMultipleSports_When_MixedSports()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card>
        {
            RawCard(Sport.Baseball),
            RawCard(Sport.Basketball),
        });
        Assert.Contains("Sports:", result);
        Assert.Contains("Baseball", result);
        Assert.Contains("Basketball", result);
    }

    [Fact]
    public void Should_OmitSportLine_When_AllSportsAreNull()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard(null), RawCard(null) });
        Assert.DoesNotContain("Sport:", result);
    }

    // === Highlights ===

    [Fact]
    public void Should_IncludeAutographs_When_AnyCardIsAuto()
    {
        var set = BaseSet();
        var card = RawCard();
        card.IsAuto = true;
        var result = _gen.Generate(set, new List<Card> { card });
        Assert.Contains("autographs", result);
    }

    [Fact]
    public void Should_IncludeRookies_When_AnyCardIsRookie()
    {
        var set = BaseSet();
        var card = RawCard();
        card.IsRookie = true;
        var result = _gen.Generate(set, new List<Card> { card });
        Assert.Contains("rookies", result);
    }

    [Fact]
    public void Should_IncludeRelics_When_AnyCardIsRelic()
    {
        var set = BaseSet();
        var card = RawCard();
        card.IsRelic = true;
        var result = _gen.Generate(set, new List<Card> { card });
        Assert.Contains("relics", result);
    }

    [Fact]
    public void Should_IncludeSerialNumbered_When_AnyCardHasSerialNumber()
    {
        var set = BaseSet();
        var card = RawCard();
        card.SerialNumbered = "25/50";
        var result = _gen.Generate(set, new List<Card> { card });
        Assert.Contains("serial-numbered cards", result);
    }

    [Fact]
    public void Should_OmitHighlightsLine_When_NoSpecialAttributes()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.DoesNotContain("Highlights:", result);
    }

    [Fact]
    public void Should_ListAllHighlights_When_MultiplePresentOnSameCard()
    {
        var set = BaseSet();
        var card = RawCard();
        card.IsAuto = true;
        card.IsRookie = true;
        card.IsRelic = true;
        card.SerialNumbered = "10/25";
        var result = _gen.Generate(set, new List<Card> { card });
        Assert.Contains("autographs", result);
        Assert.Contains("rookies", result);
        Assert.Contains("relics", result);
        Assert.Contains("serial-numbered cards", result);
    }

    // === Shipping ===

    [Fact]
    public void Should_IncludeShipping_When_ProfileIsSet()
    {
        var set = BaseSet();
        set.SharedShippingProfile = "PWE";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("Shipping: PWE", result);
    }

    [Fact]
    public void Should_OmitShipping_When_ProfileIsEmpty()
    {
        var set = BaseSet();
        set.SharedShippingProfile = "";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.DoesNotContain("Shipping:", result);
    }

    // === Notes ===

    [Fact]
    public void Should_AppendNotes_When_Present()
    {
        var set = BaseSet();
        set.Notes = "All cards from the same lot. No duplicates.";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("All cards from the same lot. No duplicates.", result);
    }

    [Fact]
    public void Should_TrimNotes_When_HasLeadingTrailingWhitespace()
    {
        var set = BaseSet();
        set.Notes = "  trimmed note  ";
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("trimmed note", result);
        Assert.DoesNotContain("  trimmed note  ", result);
    }

    [Fact]
    public void Should_OmitNotesSection_When_NotesIsNull()
    {
        var set = BaseSet();
        set.Notes = null;
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("Spots are randomly assigned", result);
    }

    // === Footer ===

    [Fact]
    public void Should_AlwaysIncludeStandardFooter()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.Contains("Cards ship securely packaged.", result);
        Assert.Contains("Spots are randomly assigned", result);
        Assert.Contains("equal chance", result);
    }

    [Fact]
    public void Should_NotEndWithTrailingNewline()
    {
        var set = BaseSet();
        var result = _gen.Generate(set, new List<Card> { RawCard() });
        Assert.False(result.EndsWith("\n") || result.EndsWith("\r"), "Output should not end with a newline");
    }
}
