using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IOpenRouterKeyInfoService"/>.
    /// Hits <c>GET /api/v1/key</c> with the user's saved API key and parses the
    /// response shape documented at https://openrouter.ai/docs/api/reference/limits.
    ///
    /// Reuses the singleton <see cref="HttpClient"/> registered in DI (same as the
    /// scanner) and attaches the same Authorization + X-Title headers per request.
    /// No internal cache — the SettingsViewModel decides when to fetch.
    /// </summary>
    public class OpenRouterKeyInfoService : IOpenRouterKeyInfoService
    {
        private const string KeyInfoUrl = "https://openrouter.ai/api/v1/key";
        // The 402 exception's ModelId field is meaningful for scan-path errors
        // (which model just got rejected). For the key-info endpoint there's no
        // model in play, so use this sentinel — toast handlers can check for it
        // if they want to phrase the message differently.
        public const string KeyEndpointSentinel = "openrouter/key";

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;

        public OpenRouterKeyInfoService(HttpClient httpClient, ISettingsService settingsService)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
        }

        public async Task<OpenRouterKeyInfo> GetAsync(CancellationToken ct = default)
        {
            var apiKey = _settingsService.Load().OpenRouterApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "OpenRouter API key is not configured. Go to Settings to enter your key.");

            using var request = new HttpRequestMessage(HttpMethod.Get, KeyInfoUrl);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("X-Title", "FlipKit");

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            // Type the well-known billing/quota statuses before falling through
            // to the generic non-success throw — toasts and the Settings panel
            // both branch on these.
            if (response.StatusCode == HttpStatusCode.PaymentRequired)
                throw new OpenRouterPaymentRequiredException(KeyEndpointSentinel, body);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                response.Headers.TryGetValues("Retry-After", out var retryAfterValues);
                throw OpenRouterRateLimitParser.Parse(
                    body,
                    retryAfterValues is null ? null : string.Join(",", retryAfterValues),
                    KeyEndpointSentinel);
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"OpenRouter /api/v1/key error ({(int)response.StatusCode} {response.StatusCode}): {body}");

            var parsed = JsonSerializer.Deserialize<KeyInfoEnvelope>(body)
                ?? throw new InvalidOperationException("OpenRouter /api/v1/key returned an empty body.");

            var d = parsed.Data
                ?? throw new InvalidOperationException("OpenRouter /api/v1/key returned no `data` field.");

            return new OpenRouterKeyInfo(
                Label: d.Label,
                Limit: d.Limit,
                LimitRemaining: d.LimitRemaining,
                LimitReset: ParseDateTimeOffsetSafe(d.LimitReset),
                Usage: d.Usage,
                UsageDaily: d.UsageDaily,
                UsageWeekly: d.UsageWeekly,
                UsageMonthly: d.UsageMonthly,
                IsFreeTier: d.IsFreeTier,
                FetchedAt: DateTimeOffset.UtcNow);
        }

        private static DateTimeOffset? ParseDateTimeOffsetSafe(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return DateTimeOffset.TryParse(raw, out var dt) ? dt : null;
        }

        // Mirrors the Key envelope from the OpenRouter docs. Snake-case fields
        // map via JsonPropertyName attributes so the property naming policy is
        // local to this DTO.
        private sealed class KeyInfoEnvelope
        {
            [JsonPropertyName("data")] public KeyInfoData? Data { get; set; }
        }

        private sealed class KeyInfoData
        {
            [JsonPropertyName("label")]            public string? Label { get; set; }
            [JsonPropertyName("limit")]            public decimal? Limit { get; set; }
            [JsonPropertyName("limit_remaining")]  public decimal? LimitRemaining { get; set; }
            // limit_reset is documented as a string (e.g. "monthly", "daily") OR
            // a timestamp depending on the key. We parse as DateTimeOffset and
            // tolerate non-timestamp strings by returning null — UI will just
            // omit the timestamp.
            [JsonPropertyName("limit_reset")]      public string? LimitReset { get; set; }
            [JsonPropertyName("usage")]            public decimal Usage { get; set; }
            [JsonPropertyName("usage_daily")]      public decimal UsageDaily { get; set; }
            [JsonPropertyName("usage_weekly")]     public decimal UsageWeekly { get; set; }
            [JsonPropertyName("usage_monthly")]    public decimal UsageMonthly { get; set; }
            [JsonPropertyName("is_free_tier")]     public bool IsFreeTier { get; set; }
        }
    }
}
