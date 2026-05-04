using System.Linq;
using FlipKit.Core.Services;

namespace FlipKit.Core.Tests.Services;

public class ParallelFamilyServiceTests
{
    private readonly ParallelFamilyService _sut = new();

    [Fact]
    public void GetParallels_ReturnsKnownFamily_For2025MosaicFootball()
    {
        var parallels = _sut.GetParallels(2025, "Mosaic", "Football");

        Assert.NotEmpty(parallels);
        Assert.Contains(parallels, p => p.Name == "Mosaic Black" && p.Numbered && p.PrintRun == 1);
    }

    [Fact]
    public void GetParallels_IsCaseInsensitive_ForBrandAndSport()
    {
        var lower = _sut.GetParallels(2025, "mosaic", "football");
        var mixed = _sut.GetParallels(2025, "MOSAIC", "Football");

        Assert.Equal(lower.Count, mixed.Count);
    }

    [Fact]
    public void GetParallels_DistinguishesDonrussFromDonrussElite()
    {
        var donruss = _sut.GetParallels(2025, "Donruss", "Football");
        var elite = _sut.GetParallels(2025, "Donruss Elite", "Football");

        Assert.NotEmpty(donruss);
        Assert.NotEmpty(elite);
        Assert.Contains(donruss, p => p.Name.Contains("Press Proof"));
        Assert.Contains(elite, p => p.Name.Contains("Aspirations"));
        Assert.DoesNotContain(donruss, p => p.Name.Contains("Aspirations"));
    }

    [Fact]
    public void GetParallels_ReturnsEmpty_WhenSetIsNotInCatalog()
    {
        var result = _sut.GetParallels(1991, "Junk Wax", "Baseball");

        Assert.Empty(result);
    }

    [Fact]
    public void GetParallels_ReturnsEmpty_WhenYearOrBrandIsNull()
    {
        Assert.Empty(_sut.GetParallels(null, "Mosaic", "Football"));
        Assert.Empty(_sut.GetParallels(2025, null, "Football"));
        Assert.Empty(_sut.GetParallels(2025, " ", "Football"));
    }
}
