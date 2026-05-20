using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Services.ApiModels;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="ICardsightSubscriptionService"/>.
    /// Hits <c>GET https://api.cardsight.ai/v1/subscription</c> with the user's
    /// saved CardSight API key (same key as the scanner) and parses the
    /// <c>{ "calls": int, "api_keys": [...] }</c> response shape.
    ///
    /// Reuses the singleton <see cref="HttpClient"/> registered in DI (same as the
    /// scanner / OpenRouterKeyInfoService). Status-code → failure-reason mapping
    /// mirrors <see cref="CardsightScannerService"/> so error semantics stay
    /// consistent across the scan path and the Settings panel. No internal cache.
    /// </summary>
    public class CardsightSubscriptionService : ICardsightSubscriptionService
    {
        private const string SubscriptionUrl = "https://api.cardsight.ai/v1/subscription";

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;

        public CardsightSubscriptionService(HttpClient httpClient, ISettingsService settingsService)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
        }

        public async Task<CardsightSubscriptionStatus> GetAsync(CancellationToken ct = default)
        {
            var apiKey = _settingsService.Load().CardsightApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new CardsightException(
                    CardsightFailureReason.NotConfigured,
                    "CardSight API key is not configured. Go to Settings to enter your key.");

            using var request = new HttpRequestMessage(HttpMethod.Get, SubscriptionUrl);
            request.Headers.Add("X-API-Key", apiKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new CardsightException(
                    CardsightFailureReason.Transient,
                    $"CardSight HTTP error: {ex.Message}",
                    inner: ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    ThrowForStatus(response.StatusCode, body);

                CardsightSubscriptionInfo? info;
                try
                {
                    info = JsonSerializer.Deserialize<CardsightSubscriptionInfo>(body);
                }
                catch (JsonException ex)
                {
                    throw new CardsightException(
                        CardsightFailureReason.Unknown,
                        "Failed to parse CardSight subscription response.",
                        responseBody: body,
                        inner: ex);
                }

                if (info is null)
                    throw new CardsightException(
                        CardsightFailureReason.Unknown,
                        "CardSight subscription endpoint returned an empty body.",
                        responseBody: body);

                const int quota = ICardsightSubscriptionService.DefaultFreeTierMonthlyQuota;
                var used = info.Calls;
                var remaining = Math.Max(0, quota - used);

                return new CardsightSubscriptionStatus(
                    CallsUsed: used,
                    FreeTierMonthlyQuota: quota,
                    CallsRemaining: remaining,
                    ApiKeys: info.ApiKeys ?? new List<CardsightApiKeyUsage>(),
                    FetchedAt: DateTimeOffset.UtcNow);
            }
        }

        // Mirrors CardsightScannerService.ThrowForStatus so the subscription
        // endpoint and the identify endpoint surface identical failure reasons.
        private static void ThrowForStatus(HttpStatusCode status, string body)
        {
            var reason = status switch
            {
                HttpStatusCode.Unauthorized => CardsightFailureReason.InvalidKey,
                HttpStatusCode.PaymentRequired => CardsightFailureReason.QuotaExceeded,
                HttpStatusCode.TooManyRequests => CardsightFailureReason.RateLimited,
                HttpStatusCode.BadRequest => CardsightFailureReason.BadRequest,
                HttpStatusCode.NotFound => CardsightFailureReason.NoMatch,
                HttpStatusCode.RequestTimeout => CardsightFailureReason.Transient,
                HttpStatusCode.InternalServerError => CardsightFailureReason.Transient,
                HttpStatusCode.BadGateway => CardsightFailureReason.Transient,
                HttpStatusCode.ServiceUnavailable => CardsightFailureReason.Transient,
                HttpStatusCode.GatewayTimeout => CardsightFailureReason.Transient,
                _ => CardsightFailureReason.Unknown
            };
            throw new CardsightException(reason, $"CardSight returned {(int)status} {status}.", (int)status, body);
        }
    }
}
