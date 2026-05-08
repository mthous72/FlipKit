using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FlipKit.Core.Services.Scanning
{
    /// <summary>
    /// Live model catalog backed by OpenRouter's public /api/v1/models endpoint.
    /// Filters to vision-language models (image input → text output), classifies
    /// each as free vs paid, and sorts the paid list cheapest-first by prompt token cost.
    ///
    /// Cache lifetime: app launch. Manual <see cref="InvalidateCache"/> via the Settings UI.
    /// </summary>
    public class OpenRouterModelCatalog : IOpenRouterModelCatalog
    {
        private const string ModelsUrl = "https://openrouter.ai/api/v1/models";

        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenRouterModelCatalog>? _logger;

        // Single-flight: concurrent callers share one fetch, and the result is cached
        // for the rest of the process lifetime unless InvalidateCache is called.
        private readonly SemaphoreSlim _fetchLock = new(1, 1);
        private ModelCatalog? _cached;

        public OpenRouterModelCatalog(HttpClient httpClient, ILogger<OpenRouterModelCatalog>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ModelCatalog> GetAsync(CancellationToken ct = default)
        {
            if (_cached != null) return _cached;

            await _fetchLock.WaitAsync(ct);
            try
            {
                if (_cached != null) return _cached;
                _cached = await FetchAsync(ct);
                return _cached;
            }
            finally
            {
                _fetchLock.Release();
            }
        }

        public void InvalidateCache()
        {
            _cached = null;
        }

        private async Task<ModelCatalog> FetchAsync(CancellationToken ct)
        {
            _logger?.LogInformation("Fetching OpenRouter model catalog from {Url}", ModelsUrl);

            ModelsResponse? response;
            try
            {
                response = await _httpClient.GetFromJsonAsync<ModelsResponse>(ModelsUrl, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OpenRouter model fetch failed; returning hardcoded fallback catalog from OpenRouterModelDefaults. Auto-rotation continues with the static list.");
                return BuildFallbackCatalog();
            }

            if (response?.Data == null)
            {
                _logger?.LogWarning("OpenRouter /api/v1/models returned null or empty data field; returning hardcoded fallback catalog.");
                return BuildFallbackCatalog();
            }

            var visionModels = response.Data
                .Where(IsVisionLanguageModel)
                .Select(ToOpenRouterModel)
                .Where(m => m != null)
                .Cast<OpenRouterModel>()
                .Where(IsLikelyRealVisionLanguageModel)   // drop routers, sentinel pricing, music gen
                .ToList();

            // Sort free models so our curated priority order (FallbackFreeModelIds) comes
            // first, then any additional models the live API returns that aren't in the
            // list. This prevents low-quality models (e.g. gemma-3-4b) from being tried
            // first just because the API returns them alphabetically before gemma-4.
            var preferredIds = OpenRouterModelDefaults.FallbackFreeModelIds;
            var free = visionModels
                .Where(m => m.IsFree)
                .OrderBy(m =>
                {
                    var idx = Array.IndexOf(preferredIds, m.Id);
                    return idx >= 0 ? idx : int.MaxValue;
                })
                .ThenBy(m => m.Id)
                .ToList();

            // Sort paid models by our curated preference order first (FallbackPaidModelIds),
            // then by price for any models not in the list. This prevents cheap low-quality
            // models (e.g. gemma-3-4b at $0.04/M) from becoming the default paid fallback
            // just because they undercut our preferred models on price.
            var preferredPaidIds = OpenRouterModelDefaults.FallbackPaidModelIds;
            var paid = visionModels
                .Where(m => !m.IsFree)
                .OrderBy(m =>
                {
                    var idx = Array.IndexOf(preferredPaidIds, m.Id);
                    return idx >= 0 ? idx : int.MaxValue;
                })
                .ThenBy(m => m.PromptPricePerMillion)
                .ThenBy(m => m.CompletionPricePerMillion)
                .ToList();

            _logger?.LogInformation(
                "OpenRouter catalog: {Free} free vision models, {Paid} paid vision models (filtered from {Total} total).",
                free.Count, paid.Count, response.Data.Count);

            // If the live response somehow filtered down to zero, treat it like a fetch
            // failure — the fallback list is more useful to the user than nothing.
            if (free.Count == 0 && paid.Count == 0)
            {
                _logger?.LogWarning("OpenRouter /api/v1/models returned data but no vision-language models survived filtering; returning fallback catalog.");
                return BuildFallbackCatalog();
            }

            return new ModelCatalog(free, paid, DateTime.UtcNow);
        }

        /// <summary>
        /// Builds a <see cref="ModelCatalog"/> from <see cref="OpenRouterModelDefaults"/>.
        /// Pricing is unknown for fallback entries (we don't have the live data) — set to
        /// 0 for free models and a sentinel positive value for paid (so the price-based
        /// filtering in IsLikelyRealVisionLanguageModel still passes when the live fetch
        /// later replaces this fallback). The caller can detect fallback via
        /// <see cref="ModelCatalog.IsFallback"/>.
        /// </summary>
        private static ModelCatalog BuildFallbackCatalog()
        {
            // Fallback used only when /api/v1/models is unreachable. Free models
            // are conservatively reported without response_format support — many
            // free providers don't honor it, and the schema is best-effort there
            // anyway. Paid fallback ids (gpt-4o-*, gemini-2.0-*, claude-3.5-*,
            // claude-3-opus) all support response_format per their docs as of the
            // last verification.
            var free = OpenRouterModelDefaults.FallbackFreeModelIds
                .Select(id => new OpenRouterModel(
                    Id: id,
                    DisplayName: id,
                    IsFree: true,
                    PromptPricePerMillion: 0m,
                    CompletionPricePerMillion: 0m,
                    ImagePricePerImage: null,
                    Description: "Fallback entry — live catalog unavailable.",
                    SupportedParameters: new List<string>()))
                .ToList();

            var paid = OpenRouterModelDefaults.FallbackPaidModelIds
                .Select(id => new OpenRouterModel(
                    Id: id,
                    DisplayName: id,
                    IsFree: false,
                    PromptPricePerMillion: 1m,    // unknown actual price; sentinel positive
                    CompletionPricePerMillion: 1m,
                    ImagePricePerImage: null,
                    Description: "Fallback entry — live catalog unavailable. Actual pricing unknown.",
                    SupportedParameters: new List<string> { "response_format" }))
                .ToList();

            return new ModelCatalog(free, paid, DateTime.UtcNow, IsFallback: true);
        }

        // === filtering / mapping ===

        private static bool IsVisionLanguageModel(ModelEntry m)
        {
            // Prefer the structured input_modalities/output_modalities arrays when present.
            if (m.Architecture?.InputModalities != null && m.Architecture.OutputModalities != null)
            {
                var inputs = m.Architecture.InputModalities;
                var outputs = m.Architecture.OutputModalities;
                return inputs.Any(s => string.Equals(s, "image", StringComparison.OrdinalIgnoreCase))
                    && outputs.Any(s => string.Equals(s, "text", StringComparison.OrdinalIgnoreCase));
            }

            // Fall back to the legacy modality string ("text+image->text", "text->text", etc.).
            var modality = m.Architecture?.Modality;
            if (string.IsNullOrEmpty(modality)) return false;
            var arrowParts = modality.Split("->", 2, StringSplitOptions.TrimEntries);
            if (arrowParts.Length != 2) return false;
            var input = arrowParts[0];
            var output = arrowParts[1];
            return input.Contains("image", StringComparison.OrdinalIgnoreCase)
                && output.Contains("text", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyRealVisionLanguageModel(OpenRouterModel m)
        {
            // Hard-block OpenRouter's meta-routers regardless of how their pricing is
            // reported. Auto Router routes the request to whatever model OpenRouter
            // picks (typically a premium one), which is how a user can be billed for
            // a model they didn't choose. We never want it in our catalog.
            var lowerId = m.Id.ToLowerInvariant();
            if (lowerId == "openrouter/auto"
                || lowerId.StartsWith("openrouter/auto:")
                || lowerId == "auto")
                return false;

            // Paid models must have actual positive prices — filters out OpenRouter's
            // "Auto Router" sentinel ($-1M) and any other meta-routers we don't want
            // to surface in the cost-sorted dropdown.
            if (!m.IsFree && (m.PromptPricePerMillion <= 0m || m.CompletionPricePerMillion <= 0m))
                return false;

            // Heuristic: drop entries whose name strongly suggests a non-vision use case
            // that OpenRouter happens to mis-classify as image-input (e.g. music
            // generation models like Google Lyria, video gen, audio gen). The user's
            // scanning workflow needs vision-language reasoning over a still card.
            var lowerName = m.DisplayName.ToLowerInvariant();
            string[] excludeTokens = { "lyria", "music", "audio gen", "tts", "speech",
                                        "image gen", "video", "embedding" };
            foreach (var t in excludeTokens)
                if (lowerName.Contains(t)) return false;

            return true;
        }

        private static OpenRouterModel? ToOpenRouterModel(ModelEntry m)
        {
            if (string.IsNullOrEmpty(m.Id)) return null;

            var promptPerToken = ParseDecimalSafe(m.Pricing?.Prompt) ?? 0m;
            var completionPerToken = ParseDecimalSafe(m.Pricing?.Completion) ?? 0m;
            var imagePerImage = ParseDecimalSafe(m.Pricing?.Image);

            var promptPerMillion = promptPerToken * 1_000_000m;
            var completionPerMillion = completionPerToken * 1_000_000m;

            var isFree = promptPerToken == 0m && completionPerToken == 0m;

            return new OpenRouterModel(
                Id: m.Id,
                DisplayName: !string.IsNullOrEmpty(m.Name) ? m.Name! : m.Id,
                IsFree: isFree,
                PromptPricePerMillion: promptPerMillion,
                CompletionPricePerMillion: completionPerMillion,
                ImagePricePerImage: imagePerImage,
                Description: m.Description ?? string.Empty,
                SupportedParameters: m.SupportedParameters ?? new List<string>());
        }

        private static decimal? ParseDecimalSafe(string? s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d : null;
        }

        // === DTOs for /api/v1/models ===

        private sealed class ModelsResponse
        {
            [JsonPropertyName("data")] public List<ModelEntry>? Data { get; set; }
        }

        private sealed class ModelEntry
        {
            [JsonPropertyName("id")]                   public string? Id { get; set; }
            [JsonPropertyName("name")]                 public string? Name { get; set; }
            [JsonPropertyName("description")]          public string? Description { get; set; }
            [JsonPropertyName("architecture")]         public ArchitectureInfo? Architecture { get; set; }
            [JsonPropertyName("pricing")]              public PricingInfo? Pricing { get; set; }
            // OpenRouter's response includes a per-model "supported_parameters"
            // array — values like "response_format", "tools", "tool_choice",
            // "structured_outputs". Drives the schema-capable picker filter.
            [JsonPropertyName("supported_parameters")] public List<string>? SupportedParameters { get; set; }
        }

        private sealed class ArchitectureInfo
        {
            [JsonPropertyName("modality")]          public string? Modality { get; set; }
            [JsonPropertyName("input_modalities")]  public List<string>? InputModalities { get; set; }
            [JsonPropertyName("output_modalities")] public List<string>? OutputModalities { get; set; }
        }

        private sealed class PricingInfo
        {
            [JsonPropertyName("prompt")]     public string? Prompt { get; set; }
            [JsonPropertyName("completion")] public string? Completion { get; set; }
            [JsonPropertyName("image")]      public string? Image { get; set; }
            [JsonPropertyName("request")]    public string? Request { get; set; }
        }
    }
}
