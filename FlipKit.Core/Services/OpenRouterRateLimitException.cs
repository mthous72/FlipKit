using System;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    public sealed class OpenRouterRateLimitException : Exception
    {
        public RateLimitScope Scope { get; }
        public string ModelId { get; }
        public int? RetryAfterSeconds { get; }

        public OpenRouterRateLimitException(
            RateLimitScope scope,
            string modelId,
            int? retryAfterSeconds = null)
            : base($"Rate limit [{scope}] on '{modelId}'. RetryAfter: {retryAfterSeconds?.ToString() ?? "?"}s")
        {
            Scope = scope;
            ModelId = modelId;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }
}
