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
        /// On network failure, returns an empty catalog rather than throwing — callers
        /// should fall back gracefully (e.g. ScanViewModel can show an error banner).
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
        System.DateTime FetchedAt)
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
}
