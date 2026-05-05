using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlipKit.Core.Services;

/// <summary>
/// Thin wrapper around the eBay Browse API <c>/buy/browse/v1/item_summary/search</c>
/// endpoint. Handles OAuth token acquisition and caching. Returns active listing
/// summaries only — these are asking prices, not confirmed sold prices.
/// </summary>
public interface IEbayBrowseApiClient
{
    /// <summary>
    /// Search eBay active listings matching <paramref name="query"/>.
    /// Returns an empty list when the response contains no items; throws on
    /// network errors or non-success HTTP status codes.
    /// </summary>
    Task<IReadOnlyList<EbayListingSummary>> SearchAsync(
        string query,
        string categoryId,
        int limit = 20,
        CancellationToken ct = default);
}

/// <summary>
/// One item returned by a Browse API <c>item_summary/search</c> call.
/// </summary>
public record EbayListingSummary(
    string Title,
    decimal Price,
    string Currency,
    string? Condition,
    string ItemUrl,
    string? BuyingOption);
