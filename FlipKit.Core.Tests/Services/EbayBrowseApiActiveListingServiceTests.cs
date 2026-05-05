using System.Net;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Implementations;
using FlipKit.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FlipKit.Core.Tests.Services;

public class EbayBrowseApiActiveListingServiceTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.Create();

    public void Dispose() => _db.Dispose();

    private EbayBrowseApiActiveListingService Build(
        ISettingsService? settings = null,
        IEbayBrowseApiClient? browseClient = null)
    {
        settings ??= SettingsWithCredentials();
        browseClient ??= EmptyBrowseClient();
        return new EbayBrowseApiActiveListingService(
            _db.Context, settings,
            browseClient,
            NullLogger<EbayBrowseApiActiveListingService>.Instance);
    }

    private static ISettingsService SettingsWithCredentials(string id = "cid", string secret = "csec")
    {
        var s = Substitute.For<ISettingsService>();
        s.Load().Returns(new AppSettings { EbayClientId = id, EbayClientSecret = secret });
        return s;
    }

    private static IEbayBrowseApiClient EmptyBrowseClient()
    {
        var c = Substitute.For<IEbayBrowseApiClient>();
        c.SearchAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Array.Empty<EbayListingSummary>());
        return c;
    }

    // --- BuildSearchQuery ---

    [Fact]
    public void BuildSearchQuery_IncludesYearBrandPlayer()
    {
        var card = new Card { Year = 2023, Brand = "Prizm", PlayerName = "Justin Jefferson" };
        var q = EbayBrowseApiActiveListingService.BuildSearchQuery(card);
        Assert.Equal("2023 Prizm Justin Jefferson", q);
    }

    [Fact]
    public void BuildSearchQuery_AppendsParallel_WhenNonBase()
    {
        var card = new Card { Year = 2022, Brand = "Prizm", PlayerName = "Josh Allen", ParallelName = "Silver" };
        var q = EbayBrowseApiActiveListingService.BuildSearchQuery(card);
        Assert.Equal("2022 Prizm Josh Allen Silver", q);
    }

    [Fact]
    public void BuildSearchQuery_OmitsParallel_WhenBase()
    {
        var card = new Card { Year = 2021, Brand = "Donruss", PlayerName = "Patrick Mahomes", ParallelName = "Base" };
        var q = EbayBrowseApiActiveListingService.BuildSearchQuery(card);
        Assert.Equal("2021 Donruss Patrick Mahomes", q);
    }

    // --- FetchSoldPricesAsync — ConfigurationMissing ---

    [Fact]
    public async Task FetchSoldPricesAsync_ReturnsConfigurationMissing_WhenClientIdEmpty()
    {
        var svc = Build(settings: SettingsWithCredentials(id: ""));

        var result = await svc.FetchSoldPricesAsync(new Card { PlayerName = "X" });

        Assert.False(result.Success);
        Assert.True(result.ConfigurationMissing);
    }

    [Fact]
    public async Task FetchSoldPricesAsync_ReturnsConfigurationMissing_WhenSecretEmpty()
    {
        var svc = Build(settings: SettingsWithCredentials(secret: ""));

        var result = await svc.FetchSoldPricesAsync(new Card { PlayerName = "X" });

        Assert.False(result.Success);
        Assert.True(result.ConfigurationMissing);
    }

    // --- FetchSoldPricesAsync — no listings ---

    [Fact]
    public async Task FetchSoldPricesAsync_ReturnsSuccessZero_WhenNoListingsFound()
    {
        var svc = Build();  // browseClient returns empty list

        var result = await svc.FetchSoldPricesAsync(new Card { PlayerName = "Unknown Player" });

        Assert.True(result.Success);
        Assert.Equal(0, result.RecordsFound);
    }

    // --- FetchSoldPricesAsync — saves records ---

    [Fact]
    public async Task FetchSoldPricesAsync_SavesListingRecordsToDb()
    {
        var browseClient = Substitute.For<IEbayBrowseApiClient>();
        browseClient.SearchAsync(default!, default!, default, default).ReturnsForAnyArgs(
            new[]
            {
                new EbayListingSummary("2023 Prizm Justin Jefferson Silver", 14.99m, "USD",
                    "Used", "https://ebay.com/itm/1", "FIXED_PRICE"),
                new EbayListingSummary("2023 Prizm Justin Jefferson Silver PSA 10", 89.00m, "USD",
                    "Used", "https://ebay.com/itm/2", "FIXED_PRICE"),
            });

        var svc = Build(browseClient: browseClient);
        var card = new Card { PlayerName = "Justin Jefferson", Year = 2023, Brand = "Prizm", Sport = Sport.Football };

        var result = await svc.FetchSoldPricesAsync(card);

        Assert.True(result.Success);
        Assert.Equal(2, result.RecordsFound);

        var saved = _db.Context.ListingRecords.ToList();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, r => Assert.Equal("Justin Jefferson", r.PlayerName));
        Assert.All(saved, r => Assert.Equal(2023, r.Year));
    }

    [Fact]
    public async Task FetchSoldPricesAsync_PurgesStaleRecordsBeforeSaving()
    {
        // Seed one stale record.
        _db.Context.ListingRecords.Add(new ListingRecord
        {
            PlayerName = "Justin Jefferson", Year = 2023, SoldPrice = 5.00m,
            SoldDate = DateTime.UtcNow.AddDays(-60), Platform = "eBay",
        });
        await _db.Context.SaveChangesAsync();

        var browseClient = Substitute.For<IEbayBrowseApiClient>();
        browseClient.SearchAsync(default!, default!, default, default).ReturnsForAnyArgs(
            new[]
            {
                new EbayListingSummary("Card", 20.00m, "USD", null, "https://ebay.com/itm/99", null),
            });

        var svc = Build(browseClient: browseClient);
        var card = new Card { PlayerName = "Justin Jefferson", Year = 2023 };

        await svc.FetchSoldPricesAsync(card);

        // Only the 1 fresh record should remain (stale purged).
        Assert.Equal(1, _db.Context.ListingRecords.Count());
        Assert.Equal(20.00m, _db.Context.ListingRecords.Single().SoldPrice);
    }

    // --- FetchSoldPricesAsync — network error ---

    [Fact]
    public async Task FetchSoldPricesAsync_ReturnsFailure_OnNetworkException()
    {
        var browseClient = Substitute.For<IEbayBrowseApiClient>();
        browseClient.SearchAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new HttpRequestException("Connection refused"));

        var svc = Build(browseClient: browseClient);
        var result = await svc.FetchSoldPricesAsync(new Card { PlayerName = "X" });

        Assert.False(result.Success);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    // --- Category mapping ---

    [Theory]
    [InlineData(Sport.Football,   "215")]
    [InlineData(Sport.Baseball,   "213")]
    [InlineData(Sport.Basketball, "214")]
    [InlineData(Sport.Hockey,     "217")]
    [InlineData(Sport.Soccer,     "216")]
    public async Task FetchSoldPricesAsync_PassesCorrectCategoryId(Sport sport, string expectedCategory)
    {
        var browseClient = Substitute.For<IEbayBrowseApiClient>();
        browseClient.SearchAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Array.Empty<EbayListingSummary>());

        var svc = Build(browseClient: browseClient);
        var card = new Card { PlayerName = "Player", Sport = sport };

        await svc.FetchSoldPricesAsync(card);

        await browseClient.Received(1).SearchAsync(
            Arg.Any<string>(),
            expectedCategory,
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    // --- CalculateMarketValue ---

    [Fact]
    public void CalculateMarketValue_ReturnsNone_WhenNoRecords()
    {
        var svc = Build();
        var result = svc.CalculateMarketValue(new List<ListingRecord>(), new Card());
        Assert.False(result.Success);
        Assert.Equal(PriceConfidence.None, result.Confidence);
    }

    [Fact]
    public void CalculateMarketValue_ComputesCorrectMedianAndConfidence()
    {
        var records = Enumerable.Range(1, 5).Select(i => new ListingRecord
        {
            PlayerName = "Player",
            SoldPrice = 10m * i,   // 10, 20, 30, 40, 50
            SoldDate = DateTime.UtcNow.AddDays(-1),
            Platform = "eBay",
        }).ToList();

        var svc = Build();
        var result = svc.CalculateMarketValue(records, new Card());

        Assert.True(result.Success);
        Assert.Equal(30m, result.MedianPrice);
        Assert.Equal(5, result.SampleSize);
        Assert.Equal(PriceConfidence.High, result.Confidence);
        Assert.Contains("eBay Browse API", result.Source);
        Assert.Contains("active listings", result.Source);
    }

    [Fact]
    public void CalculateMarketValue_TrimsOutliers()
    {
        var records = new List<ListingRecord>
        {
            new() { PlayerName = "P", SoldPrice = 10m, SoldDate = DateTime.UtcNow, Platform = "eBay" },
            new() { PlayerName = "P", SoldPrice = 12m, SoldDate = DateTime.UtcNow, Platform = "eBay" },
            new() { PlayerName = "P", SoldPrice = 11m, SoldDate = DateTime.UtcNow, Platform = "eBay" },
            new() { PlayerName = "P", SoldPrice = 500m, SoldDate = DateTime.UtcNow, Platform = "eBay" }, // outlier
        };

        var svc = Build();
        var result = svc.CalculateMarketValue(records, new Card());

        // 500 is > 2σ from the mean of {10,11,12,500} ≈ 133; trimmed to {10,11,12}.
        Assert.True(result.MedianPrice < 20m);
    }

    // --- HasRecentDataAsync ---

    [Fact]
    public async Task HasRecentDataAsync_ReturnsFalse_WhenNoCachedRecords()
    {
        var svc = Build();
        var card = new Card { PlayerName = "No One", Year = 2023, Sport = Sport.Football };
        Assert.False(await svc.HasRecentDataAsync(card));
    }

    [Fact]
    public async Task HasRecentDataAsync_ReturnsTrue_WhenFreshRecordExists()
    {
        _db.Context.ListingRecords.Add(new ListingRecord
        {
            PlayerName = "Justin Jefferson", Year = 2023,
            Sport = "Football", SoldDate = DateTime.UtcNow.AddDays(-5),
            SoldPrice = 15m, Platform = "eBay",
        });
        await _db.Context.SaveChangesAsync();

        var svc = Build();
        var card = new Card { PlayerName = "Justin Jefferson", Year = 2023, Sport = Sport.Football };
        Assert.True(await svc.HasRecentDataAsync(card, daysOld: 30));
    }

    [Fact]
    public async Task HasRecentDataAsync_ReturnsFalse_WhenRecordTooOld()
    {
        _db.Context.ListingRecords.Add(new ListingRecord
        {
            PlayerName = "Justin Jefferson", Year = 2023,
            Sport = "Football", SoldDate = DateTime.UtcNow.AddDays(-60),
            SoldPrice = 15m, Platform = "eBay",
        });
        await _db.Context.SaveChangesAsync();

        var svc = Build();
        var card = new Card { PlayerName = "Justin Jefferson", Year = 2023, Sport = Sport.Football };
        Assert.False(await svc.HasRecentDataAsync(card, daysOld: 30));
    }
}
