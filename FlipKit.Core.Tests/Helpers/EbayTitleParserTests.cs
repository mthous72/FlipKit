using FlipKit.Core.Helpers;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Tests.Helpers;

// Fixtures come from the real eBay Seller Hub CSV at
// Docs/references/eBay-all-active-listings-report-2026-04-30-12315361660.csv —
// each Theory case below is a verbatim title from that file. Don't "improve"
// or paraphrase them; the brittleness of the rule pass on real-world strings
// is exactly what these tests are guarding.
public class EbayTitleParserTests
{
    // Mirrors what the production code feeds in via IPlayerNameDirectory.Manufacturers
    // — a flat list of canonical manufacturer names. Tests that don't assert on the
    // Manufacturer field continue to use the no-arg Parse(title) overload, which
    // intentionally produces null Manufacturer (no dictionary supplied).
    private static readonly string[] TestManufacturers =
    {
        "Upper Deck", "Press Pass", "Stadium Club", "Panini", "Topps",
        "Bowman", "Fleer", "Donruss", "SkyBox", "Score", "Leaf", "Pinnacle",
    };

    // Mirrors what the seeded LeagueAcronyms table feeds into the directory
    // and through to InferSport. Tests own this dictionary explicitly so they
    // don't depend on the seed being loaded; same shape as production.
    private static readonly Dictionary<string, Sport> TestLeagueAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        { "NFL", Sport.Football },
        { "NCAAF", Sport.Football },
        { "NBA", Sport.Basketball },
        { "WNBA", Sport.Basketball },
        { "NCAAB", Sport.Basketball },
        { "MLB", Sport.Baseball },
        { "NHL", Sport.Hockey },
        { "MLS", Sport.Soccer },
        { "UFC", Sport.MMA },
        { "WWE", Sport.Wrestling },
        { "AEW", Sport.Wrestling },
        { "PGA", Sport.Golf },
        { "F1", Sport.Racing },
        { "Formula 1", Sport.Racing },
        { "NASCAR", Sport.Racing },
        { "ATP", Sport.Tennis },
        { "WTA", Sport.Tennis },
    };


    [Theory]
    [InlineData("2025 Panini Select - Premier Level Jonathan Taylor #132 Zebra Prizm", 2025)]
    [InlineData("2025 Panini Impeccable Sammy Sosa Auto 1998 MVP /98 Chicago Cubs", 2025)] // first year wins
    [InlineData("2026 Topps SERIES 1 75 YEARS DIE CUT JAZZ CHISHOLM JR. AUTO ON CARD 15/75", 2026)]
    [InlineData("Blake Corum RC 2024 National Treasures NFL Gear Dual Rookie Patch Auto 02/25", 2024)]
    [InlineData("1989 Score - Deion Sanders #246 (RC)", 1989)]
    public void Parses_SingleYear(string title, int expectedYear)
    {
        var parsed = EbayTitleParser.Parse(title);
        Assert.Equal(expectedYear, parsed.Year);
        Assert.Null(parsed.YearEnd);
    }

    [Theory]
    [InlineData("Tyrese Haliburton 2020-21 Panini Select Rookie Selections Silver PSA 9", 2020, 2021)]
    [InlineData("1997-98 SkyBox Z-Force Michael Jordan #23 Chicago Bulls 23KT Gold Serial Insert", 1997, 1998)]
    [InlineData("2019-20 Panini NBA Hoops #259 Ja Morant RC Rookie PSA 10 Gem", 2019, 2020)]
    public void Parses_TwoYearSeasons(string title, int expectedStart, int expectedEnd)
    {
        var parsed = EbayTitleParser.Parse(title);
        Assert.Equal(expectedStart, parsed.Year);
        Assert.Equal(expectedEnd, parsed.YearEnd);
    }

    [Fact]
    public void TypoYear_LeavesYearNull()
    {
        // Real title from the fixture — "2O2O" uses letter O, not zero. The
        // rule pass should leave Year null and the LLM/user fixes it later.
        var parsed = EbayTitleParser.Parse(
            "TYRESE MAXEY RED PRIZM CRUSADE WAVE ROOKIE CARD JERSEY #3 KENTUCKY RC 76ERS 2O2O");
        Assert.Null(parsed.Year);
        Assert.Contains(nameof(EbayParsedTitle.Year), parsed.LowConfidenceFields);
    }

    [Theory]
    [InlineData("2025 Panini Select - Premier Level Jonathan Taylor #132 Zebra Prizm", "Panini")]
    [InlineData("2026 Topps SERIES 1 75 YEARS DIE CUT JAZZ CHISHOLM JR. AUTO ON CARD 15/75", "Topps")]
    [InlineData("1991 Upper Deck #13 Brett Favre RC Green Bay Packers PSA 7 NM", "Upper Deck")]
    [InlineData("PSA 7 Aaron Rodgers 2005 Press Pass Big Numbers #25 RC Refractor Die Cut", "Press Pass")]
    [InlineData("1997-98 SkyBox Z-Force Michael Jordan #23 Chicago Bulls 23KT Gold Serial Insert", "SkyBox")]
    [InlineData("1989 Score - Deion Sanders #246 (RC)", "Score")]
    public void Identifies_Manufacturer(string title, string expected)
    {
        var parsed = EbayTitleParser.Parse(title, TestManufacturers);
        Assert.Equal(expected, parsed.Manufacturer);
    }

    [Fact]
    public void UpperDeck_BeatsScore_OnLongestMatch()
    {
        // Both "Upper Deck" and "Score" are in the manufacturer list; longest
        // multi-word entries must be tried first.
        var parsed = EbayTitleParser.Parse("1991 Upper Deck Score-Less Test #1", TestManufacturers);
        Assert.Equal("Upper Deck", parsed.Manufacturer);
    }

    [Fact]
    public void NoArg_Parse_LeavesManufacturerNull()
    {
        // Documents the intentional behavior of the no-arg overload: without a
        // manufacturer dictionary, the parser cannot fill the field, so it
        // stays null and is flagged for the LLM second pass / user.
        var parsed = EbayTitleParser.Parse("2025 Panini Prizm Whoever #1");
        Assert.Null(parsed.Manufacturer);
        Assert.Contains(nameof(EbayParsedTitle.Manufacturer), parsed.LowConfidenceFields);
    }

    [Theory]
    [InlineData("Cam Skattebo 2025 Panini Black Football Rookie Auto /199 NY Giants", "/199")]
    [InlineData("Travis Hunter 2025 National Treasures Collegiate Rookie Patch Auto #/49", "/49")]
    [InlineData("2024 Panini Donruss Optic - Mike Evans #186 Flex Prizm /149 SP", "/149")]
    [InlineData("Myles Garrett 2024 Panini Player Of The Day GOLD 5/10", "5/10")]
    [InlineData("2026 Topps SERIES 1 75 YEARS DIE CUT JAZZ CHISHOLM JR. AUTO ON CARD 15/75", "15/75")]
    [InlineData("Blake Corum RC 2024 National Treasures NFL Gear Dual Rookie Patch Auto 02/25", "02/25")]
    public void Parses_SerialNumbered(string title, string expected)
    {
        var parsed = EbayTitleParser.Parse(title);
        Assert.Equal(expected, parsed.SerialNumbered);
    }

    [Theory]
    [InlineData("2025 Panini Select - Premier Level Jonathan Taylor #132 Zebra Prizm", "132")]
    [InlineData("2024 Panini Absolute - Rookie Force Bo Nix #RF-BNX (MEM, RC)", "RF-BNX")]
    [InlineData("2018 Panini Donruss Optic Anthony Miller Downtown SSP Rookie RC #DT-18 Bears", "DT-18")]
    [InlineData("2025 Topps Chrome #F15-1 Aaron Judge Fortune 15 Green Refractors #/99", "F15-1")]
    [InlineData("2022-23 Panini Instant Nikola Jovic RPS FIRST LOOK Heat #RPS-24 RC SGC 10", "RPS-24")]
    [InlineData("Christian Yelich 2012 Topps Heritage Minors #13A BGS 9.5 Gem Mint Prospect Card", "13A")]
    public void Parses_CardNumber_FromHashPrefix(string title, string expected)
    {
        var parsed = EbayTitleParser.Parse(title);
        Assert.Equal(expected, parsed.CardNumber);
    }

    [Theory]
    [InlineData("Blake Corum RC 2024 National Treasures NFL Gear Dual Rookie Patch Auto 02/25", true, true, true, false, false)]
    [InlineData("Cam Skattebo 2025 Panini Black Football Rookie Auto /199 NY Giants", true, false, true, false, false)]
    [InlineData("2018 Panini Donruss Optic Anthony Miller Downtown SSP Rookie RC #DT-18 Bears", false, false, true, true, false)]
    [InlineData("2024 Panini Donruss Optic - Mike Evans #186 Flex Prizm /149 SP", false, false, false, false, true)]
    [InlineData("Shaquille O'Neal AUTOGRAPH & GAME USED JERSEY CARD 1/2!", true, true, false, false, false)]
    [InlineData("2025 Panini Mosaic Gabriel Davis England Games Genesis Mosaic Prizm SSP #255", false, false, false, true, false)]
    public void Detects_AttributeFlags(
        string title,
        bool isAuto,
        bool isRelic,
        bool isRookie,
        bool isSsp,
        bool isShortPrint)
    {
        var parsed = EbayTitleParser.Parse(title);
        Assert.Equal(isAuto, parsed.IsAuto);
        Assert.Equal(isRelic, parsed.IsRelic);
        Assert.Equal(isRookie, parsed.IsRookie);
        Assert.Equal(isSsp, parsed.IsSSP);
        Assert.Equal(isShortPrint, parsed.IsShortPrint);
    }

    [Fact]
    public void SSP_DoesNotAlsoFlagSP()
    {
        // "SSP" must not double-trigger the short-print flag — they're
        // mutually exclusive in collector vocabulary (SSP ⊃ SP).
        var parsed = EbayTitleParser.Parse(
            "2025 Panini Mosaic Gabriel Davis England Games Genesis Mosaic Prizm SSP #255");
        Assert.True(parsed.IsSSP);
        Assert.False(parsed.IsShortPrint);
    }

    [Fact]
    public void EmptyTitle_FlagsEverythingLowConfidence()
    {
        var parsed = EbayTitleParser.Parse("");
        Assert.Null(parsed.Year);
        Assert.Null(parsed.Manufacturer);
        Assert.Contains(nameof(EbayParsedTitle.PlayerName), parsed.LowConfidenceFields);
        Assert.Contains(nameof(EbayParsedTitle.Brand), parsed.LowConfidenceFields);
    }

    [Fact]
    public void PlayerAndBrand_AlwaysLowConfidence()
    {
        // PlayerName / Brand / SetName / ParallelName / Team are intentionally
        // out of scope for the rule pass — the LLM second pass owns them.
        var parsed = EbayTitleParser.Parse(
            "2025 Panini Select - Premier Level Jonathan Taylor #132 Zebra Prizm");
        Assert.Null(parsed.PlayerName);
        Assert.Null(parsed.Brand);
        Assert.Null(parsed.SetName);
        Assert.Null(parsed.ParallelName);
        Assert.Null(parsed.Team);
        Assert.Contains(nameof(EbayParsedTitle.PlayerName), parsed.LowConfidenceFields);
        Assert.Contains(nameof(EbayParsedTitle.Brand), parsed.LowConfidenceFields);
        Assert.Contains(nameof(EbayParsedTitle.SetName), parsed.LowConfidenceFields);
        Assert.Contains(nameof(EbayParsedTitle.ParallelName), parsed.LowConfidenceFields);
        Assert.Contains(nameof(EbayParsedTitle.Team), parsed.LowConfidenceFields);
    }

    // === Sport inference ===

    [Theory]
    [InlineData("Blake Corum RC 2024 National Treasures NFL Gear Dual Rookie Patch Auto 02/25", Sport.Football)]
    [InlineData("2025 Topps Chrome Mike Trout MLB Refractor", Sport.Baseball)]
    [InlineData("2024 Panini Prizm NBA Victor Wembanyama Silver", Sport.Basketball)]
    [InlineData("Connor Bedard NHL Upper Deck SP Authentic Rookie", Sport.Hockey)]
    [InlineData("Lionel Messi MLS Topps Chrome Refractor", Sport.Soccer)]
    [InlineData("Conor McGregor UFC Topps Chrome Auto", Sport.MMA)]
    [InlineData("Roman Reigns WWE Panini Chronicles", Sport.Wrestling)]
    [InlineData("Tiger Woods PGA Upper Deck SP Authentic", Sport.Golf)]
    [InlineData("Lewis Hamilton F1 Topps Chrome", Sport.Racing)]
    [InlineData("Dale Earnhardt Jr NASCAR Press Pass", Sport.Racing)]
    public void InferSport_LeagueAcronyms_MapToCorrectSport(string title, Sport expected)
    {
        Assert.Equal(expected, EbayTitleParser.InferSport(title, TestLeagueAcronyms));
    }

    [Fact]
    public void InferSport_NoArgOverload_ReturnsNull_Without_Dictionary()
    {
        // The no-arg overload always returns null because it's called with an
        // empty dictionary. Documents the intentional behavior — callers must
        // supply a dictionary (e.g. from IPlayerNameDirectory.LeagueAcronymToSport)
        // to get sport inference.
        Assert.Null(EbayTitleParser.InferSport("Blake Corum RC 2024 National Treasures NFL Gear"));
    }

    [Fact]
    public void InferSport_BrandOnlyTitle_NoLongerInferred_WithoutLeagueAcronym()
    {
        // The previous "Bowman → Baseball" / "Topps Chrome → Baseball" heuristic
        // biases were removed when SportKeywords moved to data. A title with no
        // league acronym now returns null — the LLM enrichment pass handles
        // those cases instead of a hardcoded brand→sport table.
        Assert.Null(EbayTitleParser.InferSport("2025 Bowman Chrome Roki Sasaki RC", TestLeagueAcronyms));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("2024 Panini Select Card Singles")]
    [InlineData("Random Card With No Identifying Tokens 2025")]
    public void InferSport_ReturnsNull_When_TitleIsBlankOrAmbiguous(string? title)
    {
        Assert.Null(EbayTitleParser.InferSport(title, TestLeagueAcronyms));
    }

    [Fact]
    public void InferSport_DoesNotFalseMatch_OnSubstring()
    {
        // "f1" inside "manufacturer" must NOT match "F1" (whole-word boundaries).
        Assert.Null(EbayTitleParser.InferSport("manufacturer f1ish", TestLeagueAcronyms));
        // "MLS" inside "MLST" must NOT match.
        Assert.Null(EbayTitleParser.InferSport("MLSTimes Card", TestLeagueAcronyms));
    }
}
