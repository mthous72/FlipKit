using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Services.ApiModels;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Immutable snapshot of CardSight subscription / quota state from
    /// <c>GET /v1/subscription</c>. Surfaces in the Settings → CardSight Usage
    /// panel (Desktop + Web), mirroring the OpenRouter Usage panel.
    ///
    /// CardSight's response carries an aggregate call count but **no** quota
    /// limit, remaining, reset date, or plan/tier field. We frame usage against
    /// the documented free-tier allowance of 750 identifications/month — clearly
    /// labelled as the free-tier quota so paid users aren't misled into thinking
    /// 750 is their hard cap.
    /// </summary>
    public sealed record CardsightSubscriptionStatus(
        int CallsUsed,
        int FreeTierMonthlyQuota,
        int CallsRemaining,
        IReadOnlyList<CardsightApiKeyUsage> ApiKeys,
        DateTimeOffset FetchedAt);

    /// <summary>
    /// Wraps CardSight's <c>GET /v1/subscription</c> endpoint. Returns a typed
    /// <see cref="CardsightSubscriptionStatus"/> snapshot or throws a
    /// <see cref="CardsightException"/> with the matching
    /// <see cref="CardsightFailureReason"/>:
    ///   * <see cref="CardsightFailureReason.NotConfigured"/> when no API key is set.
    ///   * <see cref="CardsightFailureReason.InvalidKey"/> on 401.
    ///   * <see cref="CardsightFailureReason.QuotaExceeded"/> on 402.
    ///   * <see cref="CardsightFailureReason.RateLimited"/> on 429.
    ///   * <see cref="CardsightFailureReason.Transient"/> on 5xx / timeouts.
    /// No internal cache — the caller (SettingsViewModel / SettingsController)
    /// owns refresh cadence.
    /// </summary>
    public interface ICardsightSubscriptionService
    {
        /// <summary>The documented CardSight free-tier allowance (identifications/month).</summary>
        public const int DefaultFreeTierMonthlyQuota = 750;

        Task<CardsightSubscriptionStatus> GetAsync(CancellationToken ct = default);
    }
}
