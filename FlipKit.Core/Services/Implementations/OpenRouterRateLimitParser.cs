using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// Parses OpenRouter 429 responses into an <see cref="OpenRouterRateLimitException"/>
    /// with the appropriate scope. Extracted as a static class so it can be unit-tested
    /// without HTTP infrastructure.
    /// </summary>
    internal static class OpenRouterRateLimitParser
    {
        internal static OpenRouterRateLimitException Parse(
            string responseBody,
            string? retryAfterHeader,
            string modelId)
        {
            return new OpenRouterRateLimitException(
                DetectScope(responseBody),
                modelId,
                ParseRetryAfter(retryAfterHeader));
        }

        internal static RateLimitScope DetectScope(string responseBody)
        {
            var lower = responseBody.ToLowerInvariant();

            // Provider-level throttling (upstream from OpenRouter)
            if (lower.Contains("provider") || lower.Contains("upstream"))
                return RateLimitScope.ProviderUpstream;

            // Daily budget / credit quota exhausted — don't walk chain
            if (lower.Contains("per day") || lower.Contains("daily")
                || lower.Contains("credit") || lower.Contains("quota"))
                return RateLimitScope.AccountPerDay;

            // Per-minute or per-second request limits
            if (lower.Contains("per minute") || lower.Contains("rpm")
                || lower.Contains("per second") || lower.Contains("rps"))
                return RateLimitScope.AccountPerMinute;

            return RateLimitScope.Unknown;
        }

        internal static int? ParseRetryAfter(string? header)
        {
            if (header == null) return null;
            return int.TryParse(header.Trim(), out var v) ? v : (int?)null;
        }
    }
}
