using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations;

/// <summary>
/// Implementation of <see cref="ISoldPriceService"/> backed by the eBay
/// Finding API's <c>findCompletedItems</c> operation. Replaced the prior
/// <c>Point130SoldPriceService</c> on 2026-05-05 (HTML-scraping posture
/// was legal-gray and fragile to upstream layout changes).
///
/// The local-DB methods (<see cref="FindMatchingRecordsAsync"/>,
/// <see cref="HasRecentDataAsync"/>, <see cref="CalculateMarketValue"/>)
/// were ported verbatim from the Point130 implementation since they're
/// provider-agnostic — they read from <c>SoldPriceRecords</c> and run the
/// same fuzzy match + outlier-trimmed median.
///
/// <see cref="FetchSoldPricesAsync"/> currently returns
/// <see cref="FetchSoldPricesResult.ConfigurationMissing"/> = true regardless
/// of settings — the eBay HTTP client lands in PR B. Once it ships, the
/// guard becomes "App ID not configured."
/// </summary>
public class EbayFindingApiSoldPriceService : ISoldPriceService
{
    private readonly FlipKitDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<EbayFindingApiSoldPriceService> _logger;

    public EbayFindingApiSoldPriceService(
        FlipKitDbContext dbContext,
        ISettingsService settingsService,
        ILogger<EbayFindingApiSoldPriceService> logger)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<SoldPriceRecord>> FindMatchingRecordsAsync(Card card)
    {
        var query = await _dbContext.SoldPriceRecords
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
    public Task<FetchSoldPricesResult> FetchSoldPricesAsync(Card card, int maxResults = 20)
    {
        // PR A — eBay Finding API HTTP impl lands in PR B. Stubbed return
        // surfaces as "Configure your eBay App ID" in the UI rather than a
        // confusing zero-results-but-success state.
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.EbayFindingApiAppId))
        {
            _logger.LogDebug("FetchSoldPricesAsync called but EbayFindingApiAppId is empty");
            return Task.FromResult(new FetchSoldPricesResult
            {
                Success = false,
                ConfigurationMissing = true,
                ErrorMessage = "Configure your eBay Finding API App ID in Settings to enable automated price lookups.",
            });
        }

        // The HTTP client + response mapping lands in PR B.
        return Task.FromResult(new FetchSoldPricesResult
        {
            Success = false,
            ConfigurationMissing = false,
            ErrorMessage = "eBay Finding API client not yet implemented (PR B).",
        });
    }

    /// <inheritdoc />
    public PriceLookupResult CalculateMarketValue(List<SoldPriceRecord> records, Card card)
    {
        if (!records.Any())
        {
            return new PriceLookupResult
            {
                Success = false,
                Confidence = PriceConfidence.None,
                Source = "eBay Finding API (no matches)",
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
            // Knock confidence down a tier when we couldn't get exact-grade comps.
            if (confidence == PriceConfidence.High) confidence = PriceConfidence.Medium;
            else if (confidence == PriceConfidence.Medium) confidence = PriceConfidence.Low;
        }

        _logger.LogInformation(
            "Calculated market value for {Player}: Median=${Median:F2}, {Count} sales, {Confidence} confidence{Detail}",
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
            Source = $"eBay Finding API ({filtered.Count} sales{sourceDetail})",
        };
    }

    /// <inheritdoc />
    public async Task<bool> HasRecentDataAsync(Card card, int daysOld = 30)
    {
        if (string.IsNullOrEmpty(card.PlayerName)) return false;

        var cutoff = DateTime.UtcNow.AddDays(-daysOld);
        var hasRecent = await _dbContext.SoldPriceRecords
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

    /// <summary>
    /// Parse "10" → 10.0, "9.5" → 9.5. Returns 0 on unrecognised input.
    /// </summary>
    internal static double ParseGradeValue(string? gradeValue)
    {
        if (string.IsNullOrEmpty(gradeValue)) return 0;
        return double.TryParse(gradeValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    /// <summary>
    /// Cross-grader equivalence with ±0.5 tolerance: PSA 10 ≈ BGS 9.5/10/CGC 10.
    /// </summary>
    internal static bool IsGradeEquivalent(double a, double b)
    {
        if (a == 0 || b == 0) return false;
        return Math.Abs(a - b) <= 0.5;
    }
}
