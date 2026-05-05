using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;

namespace FlipKit.Core.Services;

/// <summary>
/// Service for managing sold price data and automated market value lookups.
/// Implementations may source records from the eBay Browse API
/// (<see cref="Implementations.EbayBrowseApiActiveListingService"/>) or any
/// future provider — the interface is provider-agnostic.
/// </summary>
public interface ISoldPriceService
{
    /// <summary>
    /// Search local database for sold price records matching a card.
    /// Uses fuzzy matching on player name, brand, and parallel.
    /// </summary>
    Task<List<ListingRecord>> FindMatchingRecordsAsync(Card card);

    /// <summary>
    /// Fetch sold listings for a card from the configured upstream provider
    /// and upsert them into the local <c>ListingRecords</c> table.
    /// </summary>
    Task<FetchSoldPricesResult> FetchSoldPricesAsync(Card card, int maxResults = 20);

    /// <summary>
    /// Calculate market value from a collection of sold price records.
    /// Uses statistical analysis (median, outlier removal, confidence scoring).
    /// </summary>
    PriceLookupResult CalculateMarketValue(List<ListingRecord> records, Card card);

    /// <summary>
    /// Check if recent local data exists for a card (avoid unnecessary upstream calls).
    /// </summary>
    Task<bool> HasRecentDataAsync(Card card, int daysOld = 30);
}

/// <summary>
/// Result of a sold-prices fetch from an upstream provider.
/// </summary>
public class FetchSoldPricesResult
{
    public bool Success { get; set; }
    public int RecordsFound { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>True when the failure was due to missing/invalid configuration
    /// (e.g. no eBay App ID set) rather than a transient network issue. Lets
    /// the UI deflect to "configure your API key" instead of "try again."</summary>
    public bool ConfigurationMissing { get; set; }
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
