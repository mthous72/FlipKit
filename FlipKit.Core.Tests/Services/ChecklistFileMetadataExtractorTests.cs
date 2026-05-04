using FlipKit.Core.Services;

namespace FlipKit.Core.Tests.Services;

public class ChecklistFileMetadataExtractorTests
{
    private readonly ChecklistFileMetadataExtractor _sut = new();

    [Theory]
    [InlineData("2025-Panini-Mosaic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", 2025, "Football", "Panini", "Mosaic")]
    [InlineData("2025-Panini-Phoenix-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", 2025, "Football", "Panini", "Phoenix")]
    [InlineData("2025-Panini-Absolute-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", 2025, "Football", "Panini", "Absolute")]
    [InlineData("2025-Donruss-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", 2025, "Football", "Panini", "Donruss")]
    [InlineData("2025-Donruss-Elite-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", 2025, "Football", "Panini", "Donruss Elite")]
    [InlineData("2025-Donruss-Optic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", 2025, "Football", "Panini", "Donruss Optic")]
    [InlineData("2026-Bowman-Baseball-Checklist-Downloads-Excel-spreadsheet.xlsx", 2026, "Baseball", "Topps", "Bowman")]
    [InlineData("2026-Bowman-Chrome-Baseball-Checklist-Downloads-Excel-spreadsheet.xlsx", 2026, "Baseball", "Topps", "Bowman Chrome")]
    public void Extract_ParsesYearSportManufacturerBrand_FromCanonicalFilenames(
        string fileName, int expectedYear, string expectedSport, string expectedManufacturer, string expectedBrand)
    {
        var meta = _sut.Extract(fileName);

        Assert.Equal(expectedYear, meta.Year);
        Assert.Equal(expectedSport, meta.Sport);
        Assert.Equal(expectedManufacturer, meta.Manufacturer);
        Assert.Equal(expectedBrand, meta.Brand);
        Assert.False(string.IsNullOrWhiteSpace(meta.SetName));
    }

    [Fact]
    public void Extract_RetainsSourceFileName_ForUI()
    {
        var meta = _sut.Extract("2025-Panini-Mosaic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx");

        Assert.Equal("2025-Panini-Mosaic-Football-Checklist-Downloads-Excel-spreadsheet.xlsx", meta.SourceFileName);
    }

    [Fact]
    public void Extract_HandlesSubjectToChangeSuffix()
    {
        var meta = _sut.Extract("2025-Panini-Mosaic-Football-Checklist-SUBJECT-TO-CHANGE.xlsx");

        Assert.Equal(2025, meta.Year);
        Assert.Equal("Football", meta.Sport);
        Assert.Equal("Mosaic", meta.Brand);
    }

    [Fact]
    public void Extract_TolerantOfUnknownNames()
    {
        var meta = _sut.Extract("Random-File.xlsx");

        // Should not throw; everything left for the user to fill in.
        Assert.Null(meta.Year);
        Assert.Null(meta.Sport);
    }

    [Fact]
    public void Extract_HandlesEmptyName()
    {
        var meta = _sut.Extract(string.Empty);

        Assert.Null(meta.Year);
        Assert.Null(meta.Sport);
    }
}
