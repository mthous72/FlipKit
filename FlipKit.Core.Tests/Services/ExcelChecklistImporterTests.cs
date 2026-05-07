using System.IO;
using System.Linq;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using FlipKit.Core.Tests.Infrastructure;

namespace FlipKit.Core.Tests.Services;

public class ExcelChecklistImporterTests
{
    private static ExcelChecklistImporter NewImporter()
        => new(new ChecklistFileMetadataExtractor());

    [Fact]
    public void Parse_ColumnASubsetFixture_DetectsFormatAndPopulatesCards()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "2025-Panini-Mosaic-Football.xlsx");

        Assert.Equal(ChecklistFileFormat.ColumnASubset, preview.DetectedFormat);
        Assert.True(preview.Cards.Count > 0);
        Assert.True(preview.SubsetCount > 1);
    }

    [Fact]
    public void Parse_PullsMetadataFromFilename_OnColumnASubsetFile()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "2025-Panini-Mosaic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx");

        Assert.Equal(2025, preview.Metadata.Year);
        Assert.Equal("Football", preview.Metadata.Sport);
        Assert.Equal("Panini", preview.Metadata.Manufacturer);
        Assert.Equal("Mosaic", preview.Metadata.Brand);
    }

    [Fact]
    public void Parse_TagsAutographSubsets()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "test.xlsx");
        var autoCards = preview.Cards.Where(c => c.IsAutograph).ToList();

        Assert.NotEmpty(autoCards);
        Assert.Contains(autoCards, c => c.Subset != null && c.Subset.Contains("Autograph", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_TagsParallelSubsets()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "test.xlsx");
        var parallels = preview.Cards.Where(c => c.IsParallel).ToList();

        // Color suffixes (Black, Gold, Blue) should drive the parallel flag.
        Assert.NotEmpty(parallels);
    }

    [Fact]
    public void Parse_PreservesPlayerAndTeam_FromColumnASubsetRows()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "test.xlsx");

        var mahomes = preview.Cards.FirstOrDefault(c => c.PlayerName == "Patrick Mahomes" && c.CardNumber == "1" && c.Subset == "Base");
        Assert.NotNull(mahomes);
        Assert.Equal("Kansas City Chiefs", mahomes!.Team);
    }

    [Fact]
    public void Parse_StampsCardsWithChecklistInsiderSource()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "test.xlsx");

        Assert.NotEmpty(preview.Cards);
        Assert.All(preview.Cards, card => Assert.Equal("checklist-insider", card.Source));
    }

    [Theory]
    [InlineData("2025-Panini-Mosaic-Football.xlsx", 2025, "Football", "Panini", "Mosaic")]
    [InlineData("2025-Panini-Phoenix-Football.xlsx", 2025, "Football", "Panini", "Phoenix")]
    [InlineData("2025-Panini-Absolute-Football.xlsx", 2025, "Football", "Panini", "Absolute")]
    [InlineData("2025-Donruss-Football.xlsx", 2025, "Football", "Panini", "Donruss")]
    [InlineData("2025-Donruss-Elite-Football.xlsx", 2025, "Football", "Panini", "Donruss Elite")]
    [InlineData("2025-Donruss-Optic-Football.xlsx", 2025, "Football", "Panini", "Donruss Optic")]
    [InlineData("2026-Bowman-Baseball.xlsx", 2026, "Baseball", "Topps", "Bowman")]
    [InlineData("2026-Bowman-Chrome-Baseball.xlsx", 2026, "Baseball", "Topps", "Bowman Chrome")]
    public void Parse_FilenameDrivesMetadata_AcrossKnownReleases(
        string filename, int expectedYear, string expectedSport, string expectedManufacturer, string expectedBrand)
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, filename);

        Assert.Equal(expectedYear, preview.Metadata.Year);
        Assert.Equal(expectedSport, preview.Metadata.Sport);
        Assert.Equal(expectedManufacturer, preview.Metadata.Manufacturer);
        Assert.Equal(expectedBrand, preview.Metadata.Brand);
    }

    [Fact]
    public void Parse_InlineHeaderFixture_DetectsBowmanStyleFormat()
    {
        // Bowman 2026 layout: subsets announced by all-caps single-cell rows, data
        // rows have A=Card #, B=Player, C=Team, D=optional flag.
        using var stream = SyntheticChecklistXlsxBuilder.BuildInlineHeaderXlsx(new (string?, SyntheticChecklistXlsxBuilder.CardRow?)[]
        {
            ("BASE CARDS", null),
            (null, new("BASE CARDS", "1", "Roman Anthony", "Boston Red Sox", "Rookie")),
            (null, new("BASE CARDS", "2", "Jackson Holliday", "Baltimore Orioles", null)),
            ("CHROME PROSPECTS", null),
            (null, new("CHROME PROSPECTS", "BCP1", "Roman Anthony", "Boston Red Sox", null)),
            ("CHROME PROSPECT AUTOGRAPHS", null),
            (null, new("CHROME PROSPECT AUTOGRAPHS", "BCPA-RA", "Roman Anthony", "Boston Red Sox", null)),
        });

        var preview = NewImporter().Parse(stream, "2026-Bowman-Baseball.xlsx");

        Assert.Equal(ChecklistFileFormat.InlineHeader, preview.DetectedFormat);
        Assert.True(preview.Cards.Count >= 3);
        Assert.Contains(preview.Cards, c => c.Subset != null && c.Subset.Contains("Chrome", System.StringComparison.OrdinalIgnoreCase));
        // The autograph header row should flag those cards as autographs.
        Assert.Contains(preview.Cards, c => c.IsAutograph && c.PlayerName == "Roman Anthony");
        // The "Rookie" flag in column D should be detected.
        Assert.Contains(preview.Cards, c => c.IsRookie && c.PlayerName == "Roman Anthony");
    }

    [Theory]
    [InlineData("2023 ALL TOPPS TEAM")]
    [InlineData("1990 TOPPS BASEBALL")]
    [InlineData("Retail Exclusive")]
    [InlineData("Hobby Only Exclusive")]
    [InlineData("BASE CARDS PLAYER NUMBER VARIATION")]
    [InlineData("CHROME STARS OF MLB")]
    public void Parse_InlineHeader_Treats_Mixed_And_DigitPrefixed_Headers_AsSubsets(string header)
    {
        // Real Topps baseball files insert dividers between subsets in many
        // shapes: digit-prefixed ("2023 ALL TOPPS TEAM"), mixed-case
        // distribution-channel labels ("Retail Exclusive"), or all-caps multi-
        // word names. The original parser only accepted all-caps-letters-only
        // headers and surfaced everything else as a "missing card # / player
        // name; skipped" warning, alarming users even when the actual cards
        // imported correctly. They should silently become the current subset
        // instead of generating warnings.
        using var stream = SyntheticChecklistXlsxBuilder.BuildInlineHeaderXlsx(new (string?, SyntheticChecklistXlsxBuilder.CardRow?)[]
        {
            (header, null),
            (null, new(header, "1", "Aaron Judge", "New York Yankees", null)),
        });

        var preview = NewImporter().Parse(stream, "synthetic.xlsx");

        Assert.Equal(ChecklistFileFormat.InlineHeader, preview.DetectedFormat);
        Assert.Single(preview.Cards);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public void Parse_InlineHeader_Allows_BlankCardNumber_For_RedemptionEntries()
    {
        // Some Topps subsets (sweepstakes redemptions, "75 YEARS OF TOPPS BASEBALL
        // GIFTS") ship rows with a player/prize description in column B but no
        // card number in column A. Those are real entries and should land in
        // Cards with an empty CardNumber, not generate a "missing card number"
        // warning that masks the actual import.
        using var stream = SyntheticChecklistXlsxBuilder.BuildInlineHeaderXlsx(new (string?, SyntheticChecklistXlsxBuilder.CardRow?)[]
        {
            ("75 YEARS OF TOPPS BASEBALL GIFTS", null),
            (null, new("75 YEARS OF TOPPS BASEBALL GIFTS", "", "2 Tickets to the World Series", "Major League Baseball", null)),
            (null, new("75 YEARS OF TOPPS BASEBALL GIFTS", "", "2 Tickets to the Home Run Derby", "Major League Baseball", null)),
        });

        var preview = NewImporter().Parse(stream, "synthetic.xlsx");

        Assert.Equal(2, preview.Cards.Count);
        Assert.Contains(preview.Cards, c => c.PlayerName == "2 Tickets to the World Series" && c.CardNumber == "");
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public void Parse_InlineHeader_With_Unknown_PreambleRow_StillDetected()
    {
        // Future-proofing: when leading non-content text appears in a form we
        // haven't catalogued (date stamp, version note, etc.), the detector
        // should still find the real first format-classifiable row by walking
        // past anything unrecognized — not just rows in the disclaimer list.
        using var stream = SyntheticChecklistXlsxBuilder.BuildInlineHeaderXlsx(new (string?, SyntheticChecklistXlsxBuilder.CardRow?)[]
        {
            ("Updated 2026-02-15", null),  // not a disclaimer phrase, mixed case
            (null, null),
            ("BASE SET", null),
            (null, new("BASE SET", "1", "Mookie Betts", "Los Angeles Dodgers", null)),
        });

        var preview = NewImporter().Parse(stream, "2026-Mystery-Format.xlsx");

        Assert.Equal(ChecklistFileFormat.InlineHeader, preview.DetectedFormat);
        Assert.Contains(preview.Cards, c => c.PlayerName == "Mookie Betts");
    }

    [Fact]
    public void Parse_InlineHeader_With_Leading_DisclaimerRow_StillDetected()
    {
        // Newer Topps baseball files (2025 / 2026) lead with a row like
        // "*SUBJECT TO CHANGE", then have the real subset header on the next
        // populated row. Format detection must skip the disclaimer instead of
        // treating it as the first row, otherwise the file misclassifies and
        // no cards get parsed.
        using var stream = SyntheticChecklistXlsxBuilder.BuildInlineHeaderXlsx(new (string?, SyntheticChecklistXlsxBuilder.CardRow?)[]
        {
            ("*SUBJECT TO CHANGE", null),
            (null, null), // blank row, mirrors real-world layout
            ("BASE SET", null),
            (null, new("BASE SET", "1", "Aaron Judge", "New York Yankees", null)),
            (null, new("BASE SET", "2", "Mookie Betts", "Los Angeles Dodgers", null)),
        });

        var preview = NewImporter().Parse(stream, "2026-Topps-Series-1-Baseball.xlsx");

        Assert.Equal(ChecklistFileFormat.InlineHeader, preview.DetectedFormat);
        Assert.NotEmpty(preview.Cards);
        Assert.Contains(preview.Cards, c => c.PlayerName == "Aaron Judge" && c.Team == "New York Yankees");
    }

    [Theory]
    [InlineData("2024-Topps-Series-1-Baseball-Checklist-Downloads-Download-the-Excel-checklist-spreadsheet-1.xlsx")]
    [InlineData("2025-Topps-Series-2-Baseball-Checklist-Downloads-Excel-spreadsheet.xlsx")]
    [InlineData("2026-Topps-Series-1-Baseball-Checklist-Update-Feb.xlsx")]
    public void Parse_RealBaseballFixture_FromDocsReferences_ProducesCards(string fileName)
    {
        // Integration check against the real Topps baseball xlsx files dropped
        // into Docs/References. Skip silently if the fixture isn't present so
        // the test stays runnable on machines that haven't checked them in.
        var path = TryFindReferenceFile(fileName);
        if (path == null) return;

        using var stream = File.OpenRead(path);
        var preview = NewImporter().Parse(stream, fileName);

        Assert.Equal(ChecklistFileFormat.InlineHeader, preview.DetectedFormat);
        Assert.True(preview.Cards.Count > 50,
            $"Expected >50 cards from {fileName} but got {preview.Cards.Count}. Format: {preview.DetectedFormat}");
        Assert.All(preview.Cards.Take(20), c => Assert.False(string.IsNullOrWhiteSpace(c.PlayerName)));
    }

    private static string? TryFindReferenceFile(string fileName)
    {
        // Walk up from the test bin directory to find the repo root, then look
        // in Docs/References. Returns null when not found so the test no-ops.
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Docs", "References", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void Parse_InvalidStream_Throws()
    {
        // Surface contract: throw on unreadable workbook so the caller's error path runs.
        using var bad = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
        var importer = NewImporter();

        Assert.ThrowsAny<System.Exception>(() => importer.Parse(bad, "broken.xlsx"));
    }

    [Fact]
    public void Parse_ProducesIsValidPreview_OnHappyPath()
    {
        using var stream = SyntheticChecklistXlsxBuilder.MosaicLikeFixture();

        var preview = NewImporter().Parse(stream, "2025-Panini-Mosaic-Football.xlsx");

        Assert.True(preview.IsValid);
    }
}
