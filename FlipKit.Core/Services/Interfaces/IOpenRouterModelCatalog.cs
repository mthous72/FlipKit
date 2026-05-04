using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Live catalog of OpenRouter vision-language models (image input → text output),
    /// fetched from the public /api/v1/models endpoint and cached for the app lifetime.
    /// </summary>
    public interface IOpenRouterModelCatalog
    {
        /// <summary>
        /// Returns the cached catalog, fetching it from OpenRouter on the first call.
        /// On network failure or empty response, returns a fallback catalog populated
        /// from <see cref="OpenRouterModelDefaults"/> with <c>IsFallback = true</c> so
        /// callers can surface a "using cached models" notice.
        /// </summary>
        Task<ModelCatalog> GetAsync(CancellationToken ct = default);

        /// <summary>
        /// Drops the cached result so the next <see cref="GetAsync"/> hits OpenRouter.
        /// Wired to a "Refresh model list" button in Settings.
        /// </summary>
        void InvalidateCache();
    }

    public sealed record ModelCatalog(
        IReadOnlyList<OpenRouterModel> FreeVisionModels,
        IReadOnlyList<OpenRouterModel> PaidVisionModels,
        System.DateTime FetchedAt,
        bool IsFallback = false)
    {
        public bool IsEmpty => FreeVisionModels.Count == 0 && PaidVisionModels.Count == 0;
    }

    public sealed record OpenRouterModel(
        string Id,
        string DisplayName,
        bool IsFree,
        decimal PromptPricePerMillion,
        decimal CompletionPricePerMillion,
        decimal? ImagePricePerImage,
        string Description);

    /// <summary>
    /// Static defaults for the OpenRouter scanner — the default model id used when
    /// no explicit pick is made, plus a hardcoded fallback catalog used when the live
    /// /api/v1/models endpoint is unreachable. Consolidated here in Phase 5b so that
    /// every consumer (the scanner, the catalog's fallback path, and any default-param
    /// references in interfaces) reads from one source of truth.
    ///
    /// When OpenRouter rotates models, this is the only place to update.
    /// </summary>
    public static class OpenRouterModelDefaults
    {
        /// <summary>
        /// Default free model used as the baseline when no explicit model is selected.
        /// Must be a <c>const</c> so it can serve as a default parameter value in
        /// <see cref="IScannerService"/> and downstream methods.
        /// </summary>
        public const string DefaultFreeModelId = "nvidia/nemotron-nano-12b-v2-vl:free";

        /// <summary>
        /// Hardcoded snapshot of free vision-language model ids — used by the scanner's
        /// retry chain and as the catalog fallback when the live endpoint is down.
        /// Verified Apr 2026.
        /// </summary>
        public static readonly string[] FallbackFreeModelIds = new[]
        {
            "google/gemma-4-31b-it:free",                             // 31B, best free model
            "google/gemma-4-26b-a4b-it:free",                         // 26B, strong vision
            "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",     // 30B, reasoning model
            "nvidia/nemotron-nano-12b-v2-vl:free",                    // 12B (DefaultFreeModelId)
            "google/gemma-3-27b-it:free",                             // 27B, reliable fallback
        };

        /// <summary>
        /// Hardcoded snapshot of paid vision model ids — surfaces as the fallback paid
        /// list when the live endpoint is down. Order matters: the first entry is what
        /// the auto-rotation flow asks consent for when all free models fail.
        /// </summary>
        public static readonly string[] FallbackPaidModelIds = new[]
        {
            "google/gemma-3-27b-it",                          // Cheapest paid Gemma — first in line
            "openai/gpt-4o-mini",                             // Cheap GPT-4o variant
            "google/gemini-2.0-flash-lite-001",               // Gemini lite, good value
            "meta-llama/llama-3.2-11b-vision-instruct",       // ~$0.05/M, decent quality
            "qwen/qwen2.5-vl-32b-instruct",                   // Qwen vision
            "openai/gpt-4o",                                  // Premium GPT-4o
            "anthropic/claude-3.5-sonnet",                    // Premium Claude
            "anthropic/claude-3-opus",                        // Most expensive
        };
    }
}
