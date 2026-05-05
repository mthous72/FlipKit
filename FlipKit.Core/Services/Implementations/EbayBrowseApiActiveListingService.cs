using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations;

/// <summary>
/// Implementation of <see cref="ISoldPriceService"/> backed by the eBay Browse API
/// (<c>/buy/browse/v1/item_summary/search</c>). Returns active-listing asking prices
/// as competitive-pricing comps — these are not confirmed sold prices.
///
/// <see cref="FetchSoldPricesAsync"/> calls <see cref="IEbayBrowseApiClient"/>,
/// maps results to <see cref="ListingRecord"/>, purges stale cached records for
/// the card, then saves the fresh batch. Subsequent calls to
/// <see cref="FindMatchingRecordsAsync"/> and <see cref="CalculateMarketValue"/>
/// read from the local cache without hitting the network.
/// </summary>
public class EbayBrowseApiActiveListingService : ISoldPriceService
{
    // Sport enum name → eBay category ID (Sports Trading Cards = 212).
    private static readonly Dictionary<string, string> SportToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Baseball"]   = "213",
        ["Basketball"] = "214",
        ["Football"]   = "215",
        ["Hockey"]     = "217",
        ["Soccer"]     = "216",
    };

    private readonly FlipKitDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly IEbayBrowseApiClient _browseClient;
    private readonly ILogger<EbayBrowseApiActiveListingService> _logger;

    public EbayBrowseApiActiveListingService(
        FlipKitDbContext dbContext,
        ISettingsService settingsService,
        IEbayBrowseApiClient browseClient,
        ILogger<EbayBrowseApiActiveListingService> logger)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _browseClient = browseClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ListingRecord>> FindMatchingRecordsAsync(Card card)
    {
        var query = await _dbContext.ListingRecords
            .Where(r => r.Sport == card.Sport.ToString())
            .Where(r => r.Year == card.Year)
            .ToListAsync();

        // Fuzzy match on player name (threshold 0.85)
        query = query.Where(r =>
            FuzzyMatcher.Match(r.PlayerName ?? "", card.PlayerName ?? "") >= 0.85)
            .ToList();

        if (!string.IsNullOrEmpty(card.Brand))
        {
            query = query.Where(r =>
                string.IsNullOrEmpty(r.Brand) ||
                FuzzyMatcher.Match(r.Brand, card.Brand) >= 0.80)
                .ToList();
        }

        if (!string.IsNullOrEmpty(card.ParallelName))
        {
            query = query.Where(r =>
                string.IsNullOrEmpty(r.ParallelName) ||
                FuzzyMatcher.Match(r.ParallelName, card.ParallelName) >= 0.70)
                .ToList();
        }

        // Match graded vs raw with tiered approach.
        if (card.IsGraded)
        {
            var gradedOnly = query.Where(r => r.IsGraded).ToList();

            // Priority 1: exact match (same company + same grade).
            var exactMatches = gradedOnly.Where(r =>
                r.GradeCompany == card.GradeCompany &&
                r.GradeValue == card.GradeValue)
                .ToList();

            if (exactMatches.Count >= 3)
            {
                query = exactMatches;
                _logger.LogInformation(
                    "Found {Count} exact matches for {Company} {Value}",
                    exactMatches.Count, card.GradeCompany, card.GradeValue);
            }
            else
            {
                // Priority 2: similar grades from other graders (±0.5).
                var cardGradeNumeric = ParseGradeValue(card.GradeValue);
                var similarMatches = gradedOnly.Where(r =>
                    IsGradeEquivalent(cardGradeNumeric, ParseGradeValue(r.GradeValue)))
                    .ToList();
                query = similarMatches;
                _logger.LogInformation(
                    "Found {ExactCount} exact {Company} {Value} matches, expanded to {TotalCount} similar grade matches",
                    exactMatches.Count, card.GradeCompany, card.GradeValue, similarMatches.Count);
            }
        }
        else
        {
            query = query.Where(r => !r.IsGraded).ToList();
            _logger.LogDebug("Filtered to raw (ungraded) cards only ({Count} matches)", query.Count);
        }

        return query.OrderByDescending(r => r.SoldDate).ToList();
    }

    /// <inheritdoc />
    public async Task<FetchSoldPricesResult> FetchSoldPricesAsync(Card card, int maxResults = 20)
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.EbayClientId) ||
            string.IsNullOrWhiteSpace(settings.EbayClientSecret))
        {
            _logger.LogDebug("FetchSoldPricesAsync called but eBay Client ID/Secret not configured");
            return new FetchSoldPricesResult
            {
                Success = false,
                ConfigurationMissing = true,
                ErrorMessage = "Configure your eBay Browse API Client ID and Secret in Settings to enable competitive pricing lookups.",
            };
        }

        var query = BuildSearchQuery(card);
        var categoryId = card.Sport != null
            ? SportToCategory.GetValueOrDefault(card.Sport.ToString()!, "212")
            : "212";

        _logger.LogInformation(
            "Fetching eBay active listings for {Player} ({Year} {Brand}), query: {Query}",
            card.PlayerName, card.Year, card.Brand, query);

        IReadOnlyList<EbayListingSummary> listings;
        try
        {
            listings = await _browseClient.SearchAsync(query, categoryId, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "eBay Browse API call failed for {Player}", card.PlayerName);
            return new FetchSoldPricesResult
            {
                Success = false,
                ErrorMessage = $"eBay Browse API error: {ex.Message}",
            };
        }

        if (listings.Count == 0)
        {
            _logger.LogInformation("No active eBay listings found for query: {Query}", query);
            return new FetchSoldPricesResult { Success = true, RecordsFound = 0 };
        }

        // Purge stale records for this card, then save fresh batch.
        var stale = await _dbContext.ListingRecords
            .Where(r => r.PlayerName == card.PlayerName && r.Year == card.Year)
            .ToListAsync();
        _dbContext.ListingRecords.RemoveRange(stale);

        var now = DateTime.UtcNow;
        var sport = card.Sport?.ToString();
        foreach (var listing in listings)
        {
            _dbContext.ListingRecords.Add(new ListingRecord
            {
                PlayerName = card.PlayerName ?? string.Empty,
                Year       = card.Year,
                Brand      = card.Brand,
                ParallelName = card.ParallelName,
                Sport      = sport,
                SoldPrice  = listing.Price,
                SoldDate   = now,
                Platform   = "eBay",
                SaleType   = listing.BuyingOption,
                ListingTitle = listing.Title,
                SourceUrl  = listing.ItemUrl,
                ScrapedAt  = now,
            });
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Saved {Count} active eBay listings for {Player}", listings.Count, card.PlayerName);

        return new FetchSoldPricesResult { Success = true, RecordsFound = listings.Count };
    }

    /// <inheritdoc />
    public PriceLookupResult CalculateMarketValue(List<ListingRecord> records, Card card)
    {
        if (!records.Any())
        {
            return new PriceLookupResult
            {
                Success = false,
                Confidence = PriceConfidence.None,
                Source = "eBay Browse API (no matches)",
            };
        }

        var prices = records.Select(r => (double)r.SoldPrice).ToList();

        // Trim outliers > 2σ from the mean.
        var mean = prices.Average();
        var stdDev = Math.Sqrt(prices.Average(p => Math.Pow(p - mean, 2)));
        var filtered = prices.Where(p => Math.Abs(p - mean) <= 2 * stdDev).ToList();
        if (!filtered.Any()) filtered = prices;

        var sortedPrices = filtered.OrderBy(p => p).ToList();
        var median = sortedPrices.Count % 2 == 0
            ? (sortedPrices[sortedPrices.Count / 2 - 1] + sortedPrices[sortedPrices.Count / 2]) / 2
            : sortedPrices[sortedPrices.Count / 2];

        var average = filtered.Average();
        var low = filtered.Min();
        var high = filtered.Max();
        var mostRecent = records.Max(r => r.SoldDate);

        // For graded cards, distinguish exact-grade matches from similar-grade
        // expansions and surface that in the source label.
        var isMixedGraders = false;
        var sourceDetail = "";
        var exactMatches = 0;
        if (card.IsGraded)
        {
            exactMatches = records.Count(r =>
                r.GradeCompany == card.GradeCompany &&
                r.GradeValue == card.GradeValue);
            isMixedGraders = exactMatches < records.Count;

            if (exactMatches > 0 && isMixedGraders)
                sourceDetail = $" ({exactMatches} exact {card.GradeCompany} {card.GradeValue}, {records.Count - exactMatches} similar grades)";
            else if (isMixedGraders)
                sourceDetail = $" (similar grades: no exact {card.GradeCompany} {card.GradeValue} found)";
            else
                sourceDetail = $" (all {card.GradeCompany} {card.GradeValue})";
        }

        var daysOld = (DateTime.UtcNow - mostRecent).TotalDays;
        var baseConfidence = filtered.Count >= 5 && daysOld <= 30 ? PriceConfidence.High :
                             filtered.Count >= 2 && daysOld <= 60 ? PriceConfidence.Medium :
                             PriceConfidence.Low;

        var confidence = baseConfidence;
        if (card.IsGraded && isMixedGraders && exactMatches == 0)
        {
            if (confidence == PriceConfidence.High) confidence = PriceConfidence.Medium;
            else if (confidence == PriceConfidence.Medium) confidence = PriceConfidence.Low;
        }

        _logger.LogInformation(
            "Calculated market value for {Player}: Median=${Median:F2}, {Count} active listings, {Confidence} confidence{Detail}",
            card.PlayerName, median, filtered.Count, confidence, sourceDetail);

        return new PriceLookupResult
        {
            Success = true,
            MedianPrice = (decimal)median,
            AveragePrice = (decimal)average,
            LowPrice = (decimal)low,
            HighPrice = (decimal)high,
            SampleSize = filtered.Count,
            MostRecentSale = mostRecent,
            Confidence = confidence,
            Source = $"eBay Browse API ({filtered.Count} active listings{sourceDetail})",
        };
    }

    /// <inheritdoc />
    public async Task<bool> HasRecentDataAsync(Card card, int daysOld = 30)
    {
        if (string.IsNullOrEmpty(card.PlayerName)) return false;

        var cutoff = DateTime.UtcNow.AddDays(-daysOld);
        var hasRecent = await _dbContext.ListingRecords
            .Where(r => r.Sport == card.Sport.ToString())
            .Where(r => r.Year == card.Year)
            .Where(r => r.PlayerName == card.PlayerName)
            .Where(r => r.SoldDate >= cutoff)
            .AnyAsync();

        _logger.LogDebug(
            "HasRecentDataAsync for {Player}: {HasData} (within {Days} days)",
            card.PlayerName ?? "(null)", hasRecent, daysOld);

        return hasRecent;
    }

    // --- Query helpers ---

    public static string BuildSearchQuery(Card card)
    {
        var parts = new StringBuilder();

        if (card.Year.HasValue)
            parts.Append(card.Year).Append(' ');
        if (!string.IsNullOrWhiteSpace(card.Brand))
            parts.Append(card.Brand).Append(' ');
        if (!string.IsNullOrWhiteSpace(card.PlayerName))
            parts.Append(card.PlayerName).Append(' ');

        // Only append parallel when it's non-trivial.
        if (!string.IsNullOrWhiteSpace(card.ParallelName) &&
            !card.ParallelName.Equals("Base", StringComparison.OrdinalIgnoreCase))
            parts.Append(card.ParallelName);

        return parts.ToString().Trim();
    }

    public static double ParseGradeValue(string? gradeValue)
    {
        if (string.IsNullOrEmpty(gradeValue)) return 0;
        return double.TryParse(gradeValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    public static bool IsGradeEquivalent(double a, double b)
    {
        if (a == 0 || b == 0) return false;
        return Math.Abs(a - b) <= 0.5;
    }
}
