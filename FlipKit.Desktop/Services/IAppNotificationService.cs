using Avalonia.Controls;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Shows in-app toast notifications. Must be initialized with the main window's
    /// TopLevel before use (called from App.axaml.cs after the window opens).
    ///
    /// Phase 2 of the OpenRouter usage panel work added the three billing-aware
    /// methods so any scan callsite can route a typed exception
    /// (<c>OpenRouterPaymentRequiredException</c>, <c>OpenRouterRateLimitException</c>)
    /// through a single chokepoint instead of duplicating toast UI in each VM.
    /// Clicks on these toasts navigate to Settings → OpenRouter Usage so the
    /// user can see credits remaining without hunting for the panel.
    /// </summary>
    public interface IAppNotificationService
    {
        void Initialize(TopLevel topLevel);
        void NotifyBulkScanComplete(int scanned, int errors);

        /// <summary>
        /// 402 Payment Required — sticky red toast (no expiration). Surfaces the
        /// model id and the server's body message ("balance is -$X.XX") so the
        /// user sees the actual problem.
        /// </summary>
        void NotifyPaymentRequired(string modelId, string? message);

        /// <summary>
        /// 429 rate-limit — 12s amber toast. Title varies by scope (daily quota
        /// vs per-minute) and the body includes Retry-After seconds when present.
        /// </summary>
        void NotifyRateLimit(string modelId, RateLimitScope scope, int? retryAfterSeconds);

        /// <summary>
        /// All free models are exhausted but the user hasn't (yet) opted into
        /// paid. 12s info toast with a "switch to paid?" hint — clicking
        /// navigates to Settings so the user can change their default model.
        /// </summary>
        void NotifyFreeModelsExhausted(int failedCount);
    }
}
