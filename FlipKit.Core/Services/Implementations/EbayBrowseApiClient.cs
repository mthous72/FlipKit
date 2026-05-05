using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Implementations;

/// <summary>
/// eBay Browse API HTTP client. Acquires an OAuth <c>client_credentials</c> token,
/// caches it for up to 2 hours (eBay's token TTL), and exposes
/// <see cref="SearchAsync"/> for keyword searches against active listings.
/// </summary>
public class EbayBrowseApiClient : IEbayBrowseApiClient
{
    private static readonly Uri TokenEndpoint =
        new("https://api.ebay.com/identity/v1/oauth2/token");

    private static readonly Uri SearchBaseUri =
        new("https://api.ebay.com/buy/browse/v1/item_summary/search");

    private const string BrowseScope = "https://api.ebay.com/oauth/api_scope";
    private const string MarketplaceId = "EBAY_US";

    private readonly HttpClient _http;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<EbayBrowseApiClient> _logger;

    // Token cache — valid until _tokenExpiry minus a safety buffer.
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public EbayBrowseApiClient(
        HttpClient http,
        ISettingsService settingsService,
        ILogger<EbayBrowseApiClient> logger)
    {
        _http = http;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EbayListingSummary>> SearchAsync(
        string query,
        string categoryId,
        int limit = 20,
        CancellationToken ct = default)
    {
        var token = await GetOrRefreshTokenAsync(ct);

        var url = BuildSearchUrl(query, categoryId, Math.Clamp(limit, 1, 200));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", MarketplaceId);

        _logger.LogDebug("Browse API search: {Url}", url);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseSearchResponse(json);
    }

    // --- Token management ---

    public async Task<string> GetOrRefreshTokenAsync(CancellationToken ct)
    {
        // Fast path: cached token still valid (with 5-minute buffer).
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // Re-check after acquiring lock (another thread may have refreshed).
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            (_cachedToken, _tokenExpiry) = await FetchTokenAsync(ct);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<(string Token, DateTimeOffset Expiry)> FetchTokenAsync(CancellationToken ct)
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.EbayClientId) ||
            string.IsNullOrWhiteSpace(settings.EbayClientSecret))
        {
            throw new InvalidOperationException(
                "EbayClientId and EbayClientSecret must be configured before calling the Browse API.");
        }

        var encoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{settings.EbayClientId}:{settings.EbayClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("scope", BrowseScope),
        });

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var token = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("eBay token response missing access_token.");
        var expiresIn = root.GetProperty("expires_in").GetInt32();

        // Subtract 5 minutes so we never use an about-to-expire token.
        var expiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 300);

        _logger.LogInformation(
            "eBay OAuth token acquired, expires in {ExpiresIn}s (cached until {Expiry:HH:mm:ss} UTC)",
            expiresIn, expiry);

        return (token, expiry);
    }

    // --- Search response parsing ---

    public static IReadOnlyList<EbayListingSummary> ParseSearchResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("itemSummaries", out var items))
            return Array.Empty<EbayListingSummary>();

        var results = new List<EbayListingSummary>();
        foreach (var item in items.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (title is null) continue;

            decimal price = 0;
            string currency = "USD";
            if (item.TryGetProperty("price", out var priceEl))
            {
                if (priceEl.TryGetProperty("value", out var v) &&
                    decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    price = parsed;
                if (priceEl.TryGetProperty("currency", out var c))
                    currency = c.GetString() ?? "USD";
            }

            var condition = item.TryGetProperty("condition", out var cond) ? cond.GetString() : null;
            var itemUrl = item.TryGetProperty("itemWebUrl", out var url) ? url.GetString() : null;
            if (itemUrl is null) continue;

            string? buyingOption = null;
            if (item.TryGetProperty("buyingOptions", out var opts) &&
                opts.GetArrayLength() > 0)
                buyingOption = opts[0].GetString();

            results.Add(new EbayListingSummary(title, price, currency, condition, itemUrl, buyingOption));
        }

        return results;
    }

    public static string BuildSearchUrl(string query, string categoryId, int limit)
    {
        var encoded = Uri.EscapeDataString(query);
        return $"{SearchBaseUri}?q={encoded}&category_ids={categoryId}&limit={limit}";
    }
}
