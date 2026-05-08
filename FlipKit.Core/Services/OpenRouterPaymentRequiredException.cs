using System;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Thrown when OpenRouter returns 402 Payment Required — typically when the
    /// account credit balance has gone negative. The desktop app routes this to
    /// a sticky red toast so the user sees the billing problem even if they're
    /// on a different tab when it fires.
    /// </summary>
    public sealed class OpenRouterPaymentRequiredException : Exception
    {
        /// <summary>The model id the call was attempting to use, or <c>"key"</c>
        /// when the 402 came from the key-info endpoint instead of a scan.</summary>
        public string ModelId { get; }

        /// <summary>Server-supplied body text, when available — useful for
        /// surfacing the exact "balance is -$X.XX" message OpenRouter sends.</summary>
        public string? ResponseBody { get; }

        public OpenRouterPaymentRequiredException(string modelId, string? responseBody = null)
            : base($"OpenRouter 402 Payment Required on '{modelId}'. Add credits at openrouter.ai. {responseBody}")
        {
            ModelId = modelId;
            ResponseBody = responseBody;
        }
    }
}
