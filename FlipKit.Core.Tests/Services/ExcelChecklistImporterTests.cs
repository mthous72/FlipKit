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
