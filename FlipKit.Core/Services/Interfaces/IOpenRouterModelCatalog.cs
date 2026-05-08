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
        string Description,
        // Defaulted so legacy callers (test fixtures, fallback catalog construction
        // older than the schema-aware filter) keep compiling. Production code
        // should always set this — empty here means "we don't know what's
        // supported", and SupportsJsonSchema returns false in that case.
        IReadOnlyList<string>? SupportedParameters = null)
    {
        public IReadOnlyList<string> SupportedParameters { get; init; } =
            SupportedParameters ?? System.Array.Empty<string>();

        /// <summary>
        /// True when the model accepts <c>response_format</c> with a strict
        /// json_schema. Drives the paid-model picker's filter so the user
        /// doesn't pick a model that can't enforce the parallel-name enum.
        /// OpenRouter's <c>/api/v1/models</c> reports both <c>response_format</c>
        /// and <c>structured_outputs</c> as the capability flag; we accept either.
        /// </summary>
        public bool SupportsJsonSchema =>
            SupportedParameters.Contains("response_format", System.StringComparer.OrdinalIgnoreCase)
            || SupportedParameters.Contains("structured_outputs", System.StringComparer.OrdinalIgnoreCase);
    }

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
        /// Sentinel string the UI uses to mean "auto-rotate through models" instead of
        /// pinning a specific id. The Desktop and Web layers have their own
        /// <c>AutoValue</c> constants that reference this so all three stay in sync.
        /// </summary>
        public const string AutoModelValue = "auto";

        /// <summary>
        /// Resolves a saved settings value (or any UI-layer model id) into a concrete
        /// OpenRouter model id that's safe to put on the wire. The literal string
        /// <c>"auto"</c> is a UI sentinel meaning "let the app pick" — it must NEVER
        /// be sent to OpenRouter, because OpenRouter has a real "Auto Router" provider
        /// that interprets <c>"auto"</c> as routing to whatever model it picks
        /// (typically a premium one), which is how a user can be billed for an
        /// expensive model they didn't choose. Returning the free default keeps the
        /// non-rotating callsites cheap; callers that want rotation should branch on
        /// <see cref="AutoModelValue"/> explicitly before reaching here.
        /// </summary>
        public static string ResolveModelId(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue) || rawValue == AutoModelValue)
                return DefaultFreeModelId;
            return rawValue;
        }

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
        };

        /// <summary>
        /// Hardcoded snapshot of paid vision model ids — surfaces as the fallback paid
        /// list when the live endpoint is down. Order matters: the first entry is what
        /// the auto-rotation flow asks consent for when all free models fail.
        /// </summary>
        public static readonly string[] FallbackPaidModelIds = new[]
        {
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
