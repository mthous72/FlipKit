using FlipKit.Core.Models;
using FlipKit.Core.Services;
using NSubstitute;

namespace FlipKit.Core.Tests.Services;

public class ParallelCandidateProviderTests
{
    private static IParallelFamilyService EmptyFamilyService()
    {
        var s = Substitute.For<IParallelFamilyService>();
        s.GetParallels(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(Array.Empty<ParallelOption>());
        return s;
    }

    [Fact]
    public void Should_ReturnPaniniSpecificParallels_When_ManufacturerIsPanini()
    {
        var sut = new ParallelCandidateProvider(EmptyFamilyService());

        var candidates = sut.GetCandidates("Panini", brand: null, year: null, sport: null);

        // Panini-tagged entries from parallels.json: Prizm, Wave, Mojo, Sparkle, Disco, etc.
        Assert.Contains("Prizm", candidates);
        Assert.Contains("Wave", candidates);
        Assert.Contains("Mojo", candidates);
        // Should NOT include Topps-only entries.
        Assert.DoesNotContain("Refractor", candidates.Take(15)); // Refractor is Topps; would only show via universal block if at all
    }

    [Fact]
    public void Should_ReturnToppsSpecificParallels_When_ManufacturerIsTopps()
    {
        var sut = new ParallelCandidateProvider(EmptyFamilyService());

        var candidates = sut.GetCandidates("Topps", brand: null, year: null, sport: null);

        Assert.Contains("Refractor", candidates);
        Assert.Contains("X-Fractor", candidates);
        Assert.Contains("SuperFractor", candidates);
        Assert.Contains("Sapphire", candidates);
    }

    [Fact]
    public void Should_ResolveBrandToManufacturer_When_BrandSetButManufacturerNull()
    {
        // OcrHint commonly has Brand="Prizm" but Manufacturer null on a fresh AI scan.
        // BrandManufacturerMap should resolve Prizm -> Panini and surface Panini parallels.
        var sut = new ParallelCandidateProvider(EmptyFamilyService());

        var candidates = sut.GetCandidates(manufacturer: null, brand: "Prizm", year: null, sport: null);

        Assert.Contains("Wave", candidates);
        Assert.Contains("Mojo", candidates);
    }

    [Fact]
    public void Should_LayerFamilyServiceFirst_When_YearAndBrandKnown()
    {
        // The richest signal is the per-set family service. When it returns
        // entries, those should appear at the head of the list (most specific).
        var family = Substitute.For<IParallelFamilyService>();
        family.GetParallels(2025, "Prizm", "Football")
            .Returns(new[]
            {
                new ParallelOption { Name = "Silver Wave" },
                new ParallelOption { Name = "Gold /10" },
            });

        var sut = new ParallelCandidateProvider(family);

        var candidates = sut.GetCandidates("Panini", "Prizm", 2025, "Football");

        Assert.Equal("Silver Wave", candidates[0]);
        Assert.Equal("Gold /10", candidates[1]);
        // Manufacturer-wide entries follow.
        Assert.Contains("Wave", candidates);
    }

    [Fact]
    public void Should_DedupeCaseInsensitively_When_LayersOverlap()
    {
        // Family service returns "Wave" — that name is also in parallels.json.
        // Provider should keep the FAMILY entry (first one wins) and not double up.
        var family = Substitute.For<IParallelFamilyService>();
        family.GetParallels(2025, "Prizm", null)
            .Returns(new[] { new ParallelOption { Name = "Wave" } });

        var sut = new ParallelCandidateProvider(family);

        var candidates = sut.GetCandidates("Panini", "Prizm", 2025, null);

        var waves = candidates.Count(c => string.Equals(c, "Wave", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, waves);
    }

    [Fact]
    public void Should_ReturnUniversalEntriesOnly_When_ManufacturerUnknown()
    {
        // No manufacturer, no brand → only universal entries. The point is to
        // avoid blasting all 50+ names at the LLM when we have no signal.
        var sut = new ParallelCandidateProvider(EmptyFamilyService());

        var candidates = sut.GetCandidates(manufacturer: null, brand: null, year: null, sport: null);

        // Universal colors are present (manufacturer="" in parallels.json).
        Assert.Contains("Silver", candidates);
        Assert.Contains("Gold", candidates);
        // Manufacturer-specific are absent.
        Assert.DoesNotContain("Wave", candidates);
        Assert.DoesNotContain("Prizm", candidates);
    }

    [Fact]
    public void Should_CapAtFortyEntries_When_ManyCandidatesAvailable()
    {
        var sut = new ParallelCandidateProvider(EmptyFamilyService());

        var candidates = sut.GetCandidates("Panini", brand: null, year: null, sport: null);

        Assert.True(candidates.Count <= 40,
            $"expected <= 40 candidates to keep prompt tokens reasonable, got {candidates.Count}");
    }

    [Fact]
    public void Should_NotReturnInsertNames_When_FilteringForParallels()
    {
        // parallels.json mixes Type=Parallel and Type=Insert entries. The
        // candidate provider exists to constrain parallel_name — inserts
        // don't belong here (they're a separate variation_type entirely).
        var sut = new ParallelCandidateProvider(EmptyFamilyService());

        var candidates = sut.GetCandidates("Topps", brand: null, year: null, sport: null);

        Assert.DoesNotContain("Future Stars", candidates);    // Topps insert
        Assert.DoesNotContain("League Leaders", candidates);  // Topps insert
    }
}
