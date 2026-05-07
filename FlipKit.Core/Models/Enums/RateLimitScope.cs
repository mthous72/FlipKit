namespace FlipKit.Core.Models.Enums
{
    public enum RateLimitScope
    {
        /// <summary>Provider (e.g., Google, Anthropic) is throttling OpenRouter — walk chain.</summary>
        ProviderUpstream,

        /// <summary>Our account hit the per-minute request limit — wait Retry-After then walk.</summary>
        AccountPerMinute,

        /// <summary>Daily budget or credit quota exhausted — stop scanning, do not walk chain.</summary>
        AccountPerDay,

        /// <summary>Scope cannot be determined from the response body.</summary>
        Unknown,
    }
}
