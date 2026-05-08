using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Snapshot of OpenRouter key state from <c>GET /api/v1/key</c>. Surfaces in
    /// the Settings → OpenRouter Usage panel and is fetched on Settings open +
    /// after each scan batch (per planning). All money values are USD-equivalent
    /// credits as OpenRouter reports them; <see cref="Limit"/> and
    /// <see cref="LimitRemaining"/> are nullable because keys without an
    /// explicit credit limit (most paid users) report null.
    /// </summary>
    public sealed record OpenRouterKeyInfo(
        string? Label,
        decimal? Limit,
        decimal? LimitRemaining,
        DateTimeOffset? LimitReset,
        decimal Usage,
        decimal UsageDaily,
        decimal UsageWeekly,
        decimal UsageMonthly,
        bool IsFreeTier,
        DateTimeOffset FetchedAt);

    /// <summary>
    /// Wraps OpenRouter's <c>GET /api/v1/key</c> endpoint. Returns a typed
    /// <see cref="OpenRouterKeyInfo"/> snapshot or throws a typed exception:
    ///   * <see cref="OpenRouterPaymentRequiredException"/> on 402 — this is
    ///     how OpenRouter signals a negative credit balance.
    ///   * <see cref="OpenRouterRateLimitException"/> on 429.
    ///   * <see cref="InvalidOperationException"/> when the API key isn't
    ///     configured (Settings hasn't been filled in yet).
    /// No internal cache — the caller (SettingsViewModel) owns refresh cadence.
    /// </summary>
    public interface IOpenRouterKeyInfoService
    {
        Task<OpenRouterKeyInfo> GetAsync(CancellationToken ct = default);
    }
}
