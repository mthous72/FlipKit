using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services;

/// <summary>
/// Implementation of ISoldPriceService that scrapes 130point.com for eBay sold listings.
/// </summary>
public class Point130SoldPriceService : ISoldPriceService
{
    private readonly HttpClient _httpClient;
    private readonly FlipKitDbContext _dbContext;
    private readonly ILogger<Point130SoldPriceService> _logger;

    // Rate limiting: Max 1 request per 15 seconds (130point blocks after 10 requests/minute)
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private const int RateLimitSeconds = 15;

    public Point130SoldPriceService(
        HttpClient httpClient,
        FlipKitDbContext dbContext,
        ILogger<Point130SoldPriceService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Find matching sold price records in local database using fuzzy matching.
    /// </summary>
    public async Task<List<SoldPriceRecord>> FindMatchingRecordsAsync(Card card)
    {
        // Query database for cards in same sport and year
        var query = await _dbContext.SoldPriceRecords
            .Where(r => r.Sport == card.Sport.ToString())
            .Where(r => r.Year == card.Year)
            .ToListAsync();

        // Fuzzy match on player name (threshold 0.85)
        query = query.Where(r =>
            FuzzyMatcher.Match(r.PlayerName ?? "", card.PlayerName ?? "") >= 0.85)
            .ToList();

        // Fuzzy match on brand (threshold 0.80)
        if (!string.IsNullOrEmpty(card.Brand))
        {
            query = query.Where(r =>
                string.IsNullOrEmpty(r.Brand) ||
                FuzzyMatcher.Match(r.Brand, card.Brand) >= 0.80)
                .ToList();
        }

        // Fuzzy match on parallel (threshold 0.70)
        if (!string.IsNullOrEmpty(card.ParallelName))
        {
            query = query.Where(r =>
                string.IsNullOrEmpty(r.ParallelName) ||
                FuzzyMatcher.Match(r.ParallelName, card.ParallelName) >= 0.70)
                .ToList();
        }

        // Match graded vs raw with tiered approach
        if (card.IsGraded)
        {
            // First, filter to graded cards only (never mix with raw)
            var gradedOnly = query.Where(r => r.IsGraded).ToList();

            // Priority 1: Try exact match (same company + same grade)
            var exactMatches = gradedOnly.Where(r =>
                r.GradeCompany == card.GradeCompany &&
                r.GradeValue == card.GradeValue)
                .ToList();

            if (exactMatches.Count >= 3)
            {
                // We have enough exact matches - use only those
                query = exactMatches;
                _logger.LogInformation(
                    "Found {Count} exact matches for {Company} {Value}",
                    exactMatches.Count, card.GradeCompany, card.GradeValue);
            }
            else
            {
                // Priority 2: Expand to similar grades from other graders
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

        // Return most recent first
        return query.OrderByDescending(r => r.SoldDate).ToList();
    }

    /// <summary>
    /// Scrape 130point.com for sold listings via backend API.
    /// Rate-limited to 1 request per 15 seconds to avoid IP bans.
    /// </summary>
    public async Task<ScrapedResult> ScrapeSoldPricesAsync(Card card, int maxResults = 20)
    {
        // Rate limiting: Wait at least 15 seconds between requests
        await _rateLimiter.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest.TotalSeconds < RateLimitSeconds)
            {
                var delaySeconds = RateLimitSeconds - (int)timeSinceLastRequest.TotalSeconds;
                _logger.LogDebug("Rate limiting: waiting {Seconds}s before request", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }

            // Build search query
            var searchQuery = BuildSearchQuery(card);
            _logger.LogInformation("Scraping 130point for: {Query}", searchQuery);

            // Prepare POST request to 130point backend API
            var formData = new Dictionary<string, string>
            {
                { "query", searchQuery },
                { "type", "1" },  // Sort order (1 = newest first)
                { "tab_id", "1" }  // Tab identifier
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://back.130point.com/sales/")
            {
                Content = new FormUrlEncodedContent(formData)
            };

            // Add headers to look like a real browser
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("Referer", "https://130point.com/sales/");

            // Send request
            var response = await _httpClient.SendAsync(request);
            _lastRequestTime = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Scrape failed with status {Status}", response.StatusCode);
                return new ScrapedResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}"
                };
            }

            // Parse HTML response
            var html = await response.Content.ReadAsStringAsync();

            // Check for rate limit block message
            if (html.Contains("You have been blocked"))
            {
                _logger.LogError("IP blocked by 130point.com for rate limiting");
                return new ScrapedResult
                {
                    Success = false,
                    ErrorMessage = "Rate limit exceeded - IP blocked for 1 hour"
                };
            }

            // Parse sold listings from HTML
            var records = ParseSoldListings(html, card);

            if (!records.Any())
            {
                _logger.LogInformation("No sold listings found for {Query}", searchQuery);
                return new ScrapedResult
                {
                    Success = true,
                    RecordsFound = 0
                };
            }

            // Save to database (limit to maxResults)
            var recordsToSave = records.Take(maxResults).ToList();
            foreach (var record in recordsToSave)
            {
                // Check if already exists (avoid duplicates)
                var exists = await _dbContext.SoldPriceRecords
                    .AnyAsync(r => r.SourceUrl == record.SourceUrl);

                if (!exists)
                {
                    _dbContext.SoldPriceRecords.Add(record);
                }
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Scraped and saved {Count} sold price records", recordsToSave.Count);

            return new ScrapedResult
            {
                Success = true,
                RecordsFound = recordsToSave.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scraping error for {Player}", card.PlayerName);
            return new ScrapedResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <summary>
    /// Calculate market value from sold price records using statistical analysis.
    /// </summary>
    public PriceLookupResult CalculateMarketValue(List<SoldPriceRecord> records, Card card)
    {
        if (!records.Any())
        {
            return new PriceLookupResult
            {
                Success = false,
                Confidence = PriceConfidence.None,
                Source = "130point (no matches)"
            };
        }

        // Convert to double for statistical calculations
        var prices = records.Select(r => (double)r.SoldPrice).ToList();

        // Remove outliers (prices more than 2 standard deviations from mean)
        var mean = prices.Average();
        var stdDev = Math.Sqrt(prices.Average(p => Math.Pow(p - mean, 2)));
        var filtered = prices.Where(p => Math.Abs(p - mean) <= 2 * stdDev).ToList();

        if (!filtered.Any())
        {
            // All data was outliers, use original
            filtered = prices;
        }

        // Calculate statistics
        var sortedPrices = filtered.OrderBy(p => p).ToList();
        var median = sortedPrices.Count % 2 == 0
            ? (sortedPrices[sortedPrices.Count / 2 - 1] + sortedPrices[sortedPrices.Count / 2]) / 2
            : sortedPrices[sortedPrices.Count / 2];

        var average = filtered.Average();
        var low = filtered.Min();
        var high = filtered.Max();
        var mostRecent = records.Max(r => r.SoldDate);

        // Check if we have mixed graders (for graded cards)
        var isMixedGraders = false;
        var sourceDetail = "";
        if (card.IsGraded)
        {
            var exactMatches = records.Count(r =>
                r.GradeCompany == card.GradeCompany &&
                r.GradeValue == card.GradeValue);

            isMixedGraders = exactMatches < records.Count;

            if (exactMatches > 0 && isMixedGraders)
            {
                sourceDetail = $" ({exactMatches} exact {card.GradeCompany} {card.GradeValue}, {records.Count - exactMatches} similar grades)";
            }
            else if (isMixedGraders)
            {
                sourceDetail = $" (similar grades: no exact {card.GradeCompany} {card.GradeValue} found)";
            }
            else
            {
                sourceDetail = $" (all {card.GradeCompany} {card.GradeValue})";
            }
        }

        // Determine confidence based on sample size, data freshness, and grader consistency
        var daysOld = (DateTime.UtcNow - mostRecent).TotalDays;
        var baseConfidence = filtered.Count >= 5 && daysOld <= 30 ? PriceConfidence.High :
                             filtered.Count >= 2 && daysOld <= 60 ? PriceConfidence.Medium :
                             PriceConfidence.Low;

        // Lower confidence by one level if using only mixed graders (no exact matches)
        var confidence = baseConfidence;
        if (card.IsGraded && isMixedGraders)
        {
            var exactMatches = records.Count(r =>
                r.GradeCompany == card.GradeCompany &&
                r.GradeValue == card.GradeValue);

            if (exactMatches == 0 && confidence == PriceConfidence.High)
                confidence = PriceConfidence.Medium;
            else if (exactMatches == 0 && confidence == PriceConfidence.Medium)
                confidence = PriceConfidence.Low;
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
            Source = $"130point ({filtered.Count} sales{sourceDetail})"
        };
    }

    /// <summary>
    /// Check if recent sold price data exists in local database.
    /// </summary>
    public async Task<bool> HasRecentDataAsync(Card card, int daysOld = 30)
    {
        if (string.IsNullOrEmpty(card.PlayerName))
            return false;

        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

        var hasRecent = await _dbContext.SoldPriceRecords
            .Where(r => r.Sport == card.Sport.ToString())
            .Where(r => r.Year == card.Year)
            .Where(r => r.PlayerName == card.PlayerName)
            .Where(r => r.SoldDate >= cutoffDate)
            .AnyAsync();

        _logger.LogDebug(
            "HasRecentDataAsync for {Player}: {HasData} (within {Days} days)",
            card.PlayerName ?? "(null)", hasRecent, daysOld);

        return hasRecent;
    }

    /// <summary>
    /// Build search query string from card details.
    /// IMPORTANT: Includes ALL available card attributes for accurate matching.
    /// </summary>
    private string BuildSearchQuery(Card card)
    {
        var parts = new List<string>();

        // Core identification (in order of importance)
        if (card.Year.HasValue)
            parts.Add(card.Year.Value.ToString());

        if (!string.IsNullOrEmpty(card.Manufacturer))
            parts.Add(card.Manufacturer);

        if (!string.IsNullOrEmpty(card.Brand))
            parts.Add(card.Brand);

        if (!string.IsNullOrEmpty(card.PlayerName))
            parts.Add(card.PlayerName);

        if (!string.IsNullOrEmpty(card.CardNumber))
            parts.Add($"#{card.CardNumber}");

        // Variation details
        if (!string.IsNullOrEmpty(card.ParallelName))
            parts.Add(card.ParallelName);

        if (!string.IsNullOrEmpty(card.Team))
            parts.Add(card.Team);

        // Grading information (search across all graders for broader results)
        if (card.IsGraded && !string.IsNullOrEmpty(card.GradeValue))
        {
            // Include "graded" + grade value (not company) to find all comparable grades
            // Example: "graded 10" matches PSA 10, BGS 10, CGC 10, SGC 10
            // The matching filter will still enforce exact grade matching
            parts.Add("graded");
            parts.Add(card.GradeValue);
        }

        var query = string.Join(" ", parts);
        _logger.LogDebug("Built search query: '{Query}' (IsGraded: {IsGraded}, Grade: {Grade})",
            query, card.IsGraded, card.GradeValue ?? "N/A");
        return query;
    }

    /// <summary>
    /// Parse HTML response and extract sold listing records.
    /// </summary>
    private List<SoldPriceRecord> ParseSoldListings(string html, Card card)
    {
        var records = new List<SoldPriceRecord>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find all table rows (each <tr> contains one sold listing)
            var rows = doc.DocumentNode.SelectNodes("//table[@id='salesDataTable-1']//tr");

            if (rows == null || rows.Count == 0)
            {
                _logger.LogDebug("No table rows found in HTML response");
                return records;
            }

            foreach (var row in rows)
            {
                try
                {
                    // Extract title and eBay URL
                    var titleNode = row.SelectSingleNode(".//span[@id='titleText']/a");
                    if (titleNode == null) continue;

                    var title = titleNode.InnerText?.Trim();
                    var ebayUrl = titleNode.GetAttributeValue("href", "");

                    if (string.IsNullOrEmpty(title)) continue;

                    // Extract sale price from data attribute
                    var dataColNode = row.SelectSingleNode(".//div[@id='dataCol']");
                    var priceStr = dataColNode?.GetAttributeValue("data-price", "");
                    if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var salePrice))
                        continue;

                    // Extract currency
                    var currency = dataColNode?.GetAttributeValue("data-currency", "USD");

                    // Extract date
                    var dateNode = row.SelectSingleNode(".//span[@id='dateText']");
                    var dateText = dateNode?.InnerText?.Replace("Date:", "").Trim();
                    if (!TryParseDate(dateText, out var saleDate))
                        continue;

                    // Extract sale type and bid count from props-data
                    var propsNode = row.SelectSingleNode(".//span[@class='props-data']");
                    var propsData = propsNode?.InnerText ?? "";

                    var saleType = ExtractProperty(propsData, "Sale Type");
                    var bidCountStr = ExtractProperty(propsData, "Bids");
                    int.TryParse(bidCountStr, out var bidCount);

                    // Parse grading information from title
                    var isGraded = ParseGradeInfo(title, out var gradeCompany, out var gradeValue);

                    // Extract parallel from title (if not already in card)
                    var parallel = string.IsNullOrEmpty(card.ParallelName)
                        ? ExtractParallelFromTitle(title)
                        : card.ParallelName;

                    // Create record
                    var record = new SoldPriceRecord
                    {
                        PlayerName = card.PlayerName ?? "",
                        Year = card.Year,
                        Manufacturer = card.Manufacturer,
                        Brand = ExtractBrandFromTitle(title) ?? card.Brand,
                        CardNumber = card.CardNumber,
                        ParallelName = parallel,
                        Condition = isGraded ? "Graded" : "Raw",
                        IsGraded = isGraded,
                        GradeCompany = gradeCompany,
                        GradeValue = gradeValue,
                        SoldPrice = salePrice,
                        SoldDate = saleDate,
                        Platform = "eBay",
                        SaleType = NormalizeSaleType(saleType),
                        BidCount = bidCount > 0 ? bidCount : null,
                        ListingTitle = title,
                        SourceUrl = ebayUrl,
                        ScrapedAt = DateTime.UtcNow,
                        Sport = card.Sport.ToString()
                    };

                    records.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse individual listing row");
                }
            }

            _logger.LogDebug("Parsed {Count} records from HTML", records.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML response");
        }

        return records;
    }

    /// <summary>
    /// Parse grading information from listing title.
    /// Returns true if graded, with company and value in out parameters.
    /// </summary>
    private bool ParseGradeInfo(string title, out string? company, out string? value)
    {
        company = null;
        value = null;

        if (string.IsNullOrEmpty(title))
            return false;

        // Match common grading patterns: PSA 10, BGS 9.5, CGC 9, SGC 10
        var gradePatterns = new[]
        {
            @"PSA\s+(\d+(?:\.\d+)?)",
            @"BGS\s+(\d+(?:\.\d+)?)",
            @"CGC\s+(\d+(?:\.\d+)?)",
            @"SGC\s+(\d+(?:\.\d+)?)"
        };

        foreach (var pattern in gradePatterns)
        {
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Extract company name (PSA, BGS, etc.)
                company = match.Value.Split(' ')[0].ToUpper();
                value = match.Groups[1].Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to parse date in format "Sat 07 Feb 2026 11:25:16 GMT"
    /// </summary>
    private bool TryParseDate(string? dateText, out DateTime date)
    {
        date = DateTime.MinValue;

        if (string.IsNullOrEmpty(dateText))
            return false;

        try
        {
            // Format: "Sat 07 Feb 2026 11:25:16 GMT"
            date = DateTime.ParseExact(
                dateText,
                "ddd dd MMM yyyy HH:mm:ss 'GMT'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return true;
        }
        catch
        {
            _logger.LogWarning("Failed to parse date: {DateText}", dateText);
            return false;
        }
    }

    /// <summary>
    /// Extract a property value from props-data string.
    /// Format: "Sale Price: 11 - Best Offer Price: 11 - Bids: 0 - Sale Type: bestoffer"
    /// </summary>
    private string? ExtractProperty(string propsData, string propertyName)
    {
        var pattern = $@"{Regex.Escape(propertyName)}:\s*([^-]+?)(?:\s*-|$)";
        var match = Regex.Match(propsData, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extract brand from listing title (look for Panini, Topps, etc.)
    /// </summary>
    private string? ExtractBrandFromTitle(string title)
    {
        var commonBrands = new[]
        {
            "Panini", "Prizm", "Select", "Donruss", "Optic", "Mosaic",
            "Topps", "Bowman", "Upper Deck", "Fleer", "Score"
        };

        foreach (var brand in commonBrands)
        {
            if (title.Contains(brand, StringComparison.OrdinalIgnoreCase))
                return brand;
        }

        return null;
    }

    /// <summary>
    /// Extract parallel name from listing title (Silver, Gold, etc.)
    /// </summary>
    private string? ExtractParallelFromTitle(string title)
    {
        var commonParallels = new[]
        {
            "Silver", "Gold", "Orange", "Green", "Blue", "Red", "Purple",
            "Bronze", "Black", "White", "Pink", "Yellow",
            "Lazer", "Hyper", "Mojo", "Refractor", "Shimmer", "Nebula",
            "Tiger Stripe", "Zebra", "Camo", "Disco", "Wave", "Ice"
        };

        foreach (var parallel in commonParallels)
        {
            if (title.Contains(parallel, StringComparison.OrdinalIgnoreCase))
                return parallel;
        }

        return null;
    }

    /// <summary>
    /// Normalize sale type to consistent values.
    /// </summary>
    private string? NormalizeSaleType(string? saleType)
    {
        if (string.IsNullOrEmpty(saleType))
            return null;

        return saleType.ToLower() switch
        {
            "auction" => "Auction",
            "bestoffer" => "Best Offer",
            "fixedprice" => "Buy It Now",
            _ => saleType
        };
    }

    /// <summary>
    /// Parse grade value to numeric (e.g., "10" → 10.0, "9.5" → 9.5)
    /// </summary>
    private double ParseGradeValue(string? gradeValue)
    {
        if (string.IsNullOrEmpty(gradeValue))
            return 0;

        if (double.TryParse(gradeValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric))
            return numeric;

        return 0;
    }

    /// <summary>
    /// Check if two grades are equivalent across different grading companies.
    /// Uses ±0.5 tolerance to account for grading differences.
    /// Examples:
    ///   PSA 10 matches BGS 9.5, BGS 10, CGC 10, SGC 10
    ///   PSA 9 matches BGS 8.5, BGS 9, BGS 9.5, CGC 9, SGC 9
    /// </summary>
    private bool IsGradeEquivalent(double grade1, double grade2)
    {
        if (grade1 == 0 || grade2 == 0)
            return false;

        // Allow ±0.5 tolerance for cross-grader equivalency
        var difference = Math.Abs(grade1 - grade2);
        return difference <= 0.5;
    }
}
