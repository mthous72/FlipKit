using System;
using System.Collections.Generic;
using FlipKit.Core.Models.Enums;
using FlipKit.Desktop.Services;
using Xunit;

namespace FlipKit.Desktop.Tests.Services;

public class OcrTextParserTests
{
    [Fact]
    public void Extracts_TwoWord_Player_Name_AndNormalizesToTitleCase()
    {
        var (card, _) = OcrTextParser.Parse(new() { "ELI MANNING", "Giants", "QB" });
        Assert.Equal("Eli Manning", card.PlayerName);
    }

    [Fact]
    public void Extracts_HyphenatedName_PerSegmentTitleCase()
    {
        var (card, _) = OcrTextParser.Parse(new() { "JAXSON SMITH-NJIGBA" });
        Assert.Equal("Jaxson Smith-Njigba", card.PlayerName);
    }

    [Theory]
    [InlineData("C.J. KAYFUS", "C.J. Kayfus")]
    [InlineData("J.P. CRAWFORD", "J.P. Crawford")]
    [InlineData("O'NEAL HARRIS", "O'Neal Harris")]
    [InlineData("D'ANGELO RUSSELL", "D'Angelo Russell")]
    [InlineData("VLADIMIR GUERRERO", "Vladimir Guerrero")]
    public void NormalizesPlayerName_PreservesInitials_And_Punctuation(string ocrLine, string expected)
    {
        var (card, _) = OcrTextParser.Parse(new() { ocrLine });
        Assert.Equal(expected, card.PlayerName);
    }

    [Fact]
    public void Rejects_Bio_Sentence_As_PlayerName()
    {
        var lines = new List<string>
        {
            "Turning the ball over is the last thing an offense",
            "Justin Herbert"
        };
        var (card, _) = OcrTextParser.Parse(lines);
        Assert.Equal("Justin Herbert", card.PlayerName);
    }

    [Fact]
    public void Rejects_OcrNoise_Lines()
    {
        var lines = new List<string>
        {
            "Ill II Ill Ill II II II I ill I",
            "Patrick Mahomes"
        };
        var (card, _) = OcrTextParser.Parse(lines);
        Assert.Equal("Patrick Mahomes", card.PlayerName);
    }

    [Fact]
    public void Rejects_TeamName_AsPlayerName_When_TeamSuppliedInContext()
    {
        // ATLANTA FALCONS is a team, not a player. The parser only knows that
        // when team tokens are supplied via OcrParseContext (which the OCR
        // service builds from the imported checklists). Without a context,
        // the parser cannot distinguish team names from player names — that's
        // the cost of removing hardcoded catalog data.
        var context = new OcrParseContext
        {
            TeamTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ATLANTA", "FALCONS" },
        };
        var (card, _) = OcrTextParser.Parse(
            new() { "ATLANTA FALCONS" }, backLines: null, context);
        Assert.True(string.IsNullOrEmpty(card.PlayerName));
    }

    [Fact]
    public void Without_Context_TeamName_Is_NotRejected_AsPlayerName()
    {
        // Documents the intentional behavior: with no catalog data, "ATLANTA FALCONS"
        // looks like any other 2-word title-case candidate. The OCR service
        // supplies team data when available; tests that don't can't expect
        // catalog-driven gates to fire.
        var (card, _) = OcrTextParser.Parse(new() { "ATLANTA FALCONS" });
        Assert.False(string.IsNullOrEmpty(card.PlayerName));
    }

    [Fact]
    public void Rejects_GradingLabel_AsPlayerName()
    {
        // "CERTIFIED GUARANTY COMPANY" is the CGC slab label.
        var (card, _) = OcrTextParser.Parse(new() { "CERTIFIED GUARANTY COMPANY" });
        Assert.True(string.IsNullOrEmpty(card.PlayerName));
    }

    [Fact]
    public void Rejects_PlayerName_With_GradingWord()
    {
        // "JAXSON SMITH-NJIGBA MINT" — MINT is a condition word; whole line is
        // rejected. (We deliberately reject the whole line rather than try to
        // strip the bad token, since condition words mid-name signal noise.)
        var lines = new List<string> { "JAXSON SMITH-NJIGBA MINT" };
        var (card, _) = OcrTextParser.Parse(lines);
        Assert.True(string.IsNullOrEmpty(card.PlayerName));
    }

    [Fact]
    public void Prefers_TwoWord_Over_FourWord_Name()
    {
        // Both are valid candidates by our shape rules; shorter wins.
        var (card, _) = OcrTextParser.Parse(new()
        {
            "Some Long Random Phrase",
            "Justin Herbert"
        });
        Assert.Equal("Justin Herbert", card.PlayerName);
    }

    [Fact]
    public void Detects_Grading_Slab()
    {
        var (card, _) = OcrTextParser.Parse(new() { "PSA 10", "GEM MINT" });
        Assert.True(card.IsGraded);
        Assert.Equal("PSA", card.GradeCompany);
    }

    [Theory]
    [InlineData("BECKETT GRADING SERVICES")]
    [InlineData("PROFESSIONAL SPORTS AUTHENTICATOR")]
    [InlineData("CARD GRADING AUTHORITY")]
    [InlineData("SUBGRADES SURFACE EDGES CORNERS")]
    [InlineData("FUTURE STARS")]
    [InlineData("HALL OF FAME")]
    [InlineData("ROOKIE OF THE YEAR")]
    [InlineData("ALL PRO TEAM")]
    [InlineData("RED HOT ROOKIES")]
    [InlineData("Red Hot Rookies")]
    [InlineData("Diamond Kings")]
    [InlineData("Rated Rookies")]
    [InlineData("League Leaders")]
    [InlineData("Combo Card")]
    public void Rejects_GradingSlab_And_PromoLabels(string slabLabel)
    {
        var (card, _) = OcrTextParser.Parse(new() { slabLabel });
        Assert.True(string.IsNullOrEmpty(card.PlayerName),
            $"Expected slab/promo label '{slabLabel}' to be rejected but got '{card.PlayerName}'");
    }

    [Fact]
    public void Repeated_Name_On_FrontAndBack_Wins_Over_NonRepeated_Candidate()
    {
        // The 4-word "Some Other Random Phrase" passes the shape gates and
        // would beat nothing else. But the repeated 2-word name should win
        // and earn Medium confidence instead of Low.
        var front = new List<string> { "Justin Herbert", "Some Other Phrase" };
        var back  = new List<string> { "Justin Herbert", "Quarterback" };
        var (card, confidences) = OcrTextParser.Parse(front, back);
        Assert.Equal("Justin Herbert", card.PlayerName);
        var nameConf = confidences.Single(c => c.FieldName == "player_name");
        Assert.Equal(VerificationConfidence.Medium, nameConf.Confidence);
    }

    [Fact]
    public void Repeated_Name_Wins_With_Different_Whitespace()
    {
        // OCR rarely produces byte-identical lines. The overlap check should
        // ignore extra spaces and case differences. Result is normalized to
        // title case for consistent display regardless of which side won.
        var front = new List<string> { "JUSTIN HERBERT" };
        var back  = new List<string> { "  Justin   Herbert  " };
        var (card, _) = OcrTextParser.Parse(front, back);
        Assert.Equal("Justin Herbert", card.PlayerName);
    }

    [Fact]
    public void Detects_Year()
    {
        var (card, _) = OcrTextParser.Parse(new() { "2024 Panini Prizm" });
        Assert.Equal(2024, card.Year);
    }

    [Fact]
    public void Extracts_Parallel_From_OcrLines_When_SeededInContext()
    {
        // Universal finishes ("Refractor", "Silver") in the OCR text should
        // populate ParallelName when the directory has them in its seed.
        var context = new OcrParseContext
        {
            Parallels = new[] { "Refractor", "Silver", "Gold", "Press Proof Silver" },
        };
        var (card, _) = OcrTextParser.Parse(
            new() { "Justin Herbert", "Topps Chrome Refractor #/99" },
            backLines: null,
            context);
        Assert.Equal("Refractor", card.ParallelName);
    }

    [Fact]
    public void Extracts_LongestMatching_Parallel_When_Multiple_Apply()
    {
        // "Press Proof Silver" must beat "Silver" — multi-word parallels are
        // more specific than the colors they contain.
        var context = new OcrParseContext
        {
            Parallels = new[] { "Silver", "Gold", "Press Proof Silver" },
        };
        var (card, _) = OcrTextParser.Parse(
            new() { "2025 Donruss Press Proof Silver" },
            backLines: null,
            context);
        Assert.Equal("Press Proof Silver", card.ParallelName);
    }

    [Fact]
    public void Without_Parallels_In_Context_ParallelName_StaysEmpty()
    {
        // Empty context = no parallel extraction; documents the no-data path.
        var (card, _) = OcrTextParser.Parse(
            new() { "Topps Chrome Refractor #/99" });
        Assert.True(string.IsNullOrEmpty(card.ParallelName));
    }

    [Fact]
    public void Detects_SerialNumber()
    {
        var (card, _) = OcrTextParser.Parse(new() { "12/99" });
        Assert.Equal("/99", card.SerialNumbered);
    }
}
