using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services;

/// <summary>
/// Service for managing sold price data and automated market value lookups.
/// </summary>
public interface ISoldPriceService
{
    /// <summary>
    /// Search local database for sold price records matching a card.
    /// Uses fuzzy matching on player name, brand, and parallel.
    /// </summary>
    Task<List<SoldPriceRecord>> FindMatchingRecordsAsync(Card card);

    /// <summary>
    /// Scrape 130point.com for sold listings and store locally.
    /// Rate-limited to 1 request per 10 seconds.
    /// </summary>
    Task<ScrapedResult> ScrapeSoldPricesAsync(Card card, int maxResults = 20);

    /// <summary>
    /// Calculate market value from a collection of sold price records.
    /// Uses statistical analysis (median, outlier removal, confidence scoring).
    /// </summary>
    PriceLookupResult CalculateMarketValue(List<SoldPriceRecord> records, Card card);

    /// <summary>
    /// Check if recent local data exists for a card (avoid unnecessary scraping).
    /// </summary>
    Task<bool> HasRecentDataAsync(Card card, int daysOld = 30);
}

/// <summary>
/// Result of a web scraping operation.
/// </summary>
public class ScrapedResult
{
    public bool Success { get; set; }
    public int RecordsFound { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a price lookup with statistical analysis.
/// </summary>
public class PriceLookupResult
{
    public bool Success { get; set; }
    public decimal? MedianPrice { get; set; }
    public decimal? AveragePrice { get; set; }
    public decimal? LowPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public int SampleSize { get; set; }
    public DateTime? MostRecentSale { get; set; }
    public PriceConfidence Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Confidence level of a price lookup based on match quality and data freshness.
/// </summary>
public enum PriceConfidence
{
    None,      // No matches found
    Low,       // 1 match or weak matches only
    Medium,    // 2-4 matches or older data (30-60 days)
    High       // 5+ exact matches within 30 days
}
