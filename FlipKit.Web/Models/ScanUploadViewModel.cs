using System.Collections.Generic;
using FlipKit.Core.Services;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the scan upload page.
    /// </summary>
    public class ScanUploadViewModel
    {
        /// <summary>
        /// Either a specific model id or the literal "auto" to trigger server-side
        /// free-model rotation.
        /// </summary>
        public string SelectedModel { get; set; } = WebModelOption.AutoValue;

        /// <summary>
        /// Free vision models from the live OpenRouter catalog. Populated by the controller.
        /// </summary>
        public IReadOnlyList<OpenRouterModel> FreeModels { get; set; } = new List<OpenRouterModel>();

        /// <summary>
        /// Paid vision models from the live OpenRouter catalog, sorted cheapest-first.
        /// </summary>
        public IReadOnlyList<OpenRouterModel> PaidModels { get; set; } = new List<OpenRouterModel>();

        public string? CatalogError { get; set; }

        public string ScanMode { get; set; } = "selling";

        /// <summary>
        /// Server-side path of the front image from a just-saved draft. Non-null means
        /// the page should pre-populate the image preview so the user can re-scan without
        /// re-uploading.
        /// </summary>
        public string? SavedDraftFrontImagePath { get; set; }

        /// <summary>
        /// Server-side path of the back image from a just-saved draft (optional).
        /// </summary>
        public string? SavedDraftBackImagePath { get; set; }

        /// <summary>
        /// Relative URL (/uploads/...) for the front image preview after a draft save.
        /// </summary>
        public string? SavedDraftFrontImageUrl { get; set; }

        /// <summary>
        /// Relative URL (/uploads/...) for the back image preview after a draft save.
        /// </summary>
        public string? SavedDraftBackImageUrl { get; set; }

        /// <summary>
        /// When true the view shows a consent banner and disables the scan button
        /// until the user acknowledges that images will be sent to CardSight/OpenRouter.
        /// </summary>
        public bool ConsentRequired { get; set; }
    }

    /// <summary>
    /// Constants and helpers for the Web model dropdown. Mirrors the Desktop ModelOption
    /// (kept simple — no class hierarchy needed since the Web side uses Razor optgroups).
    /// </summary>
    public static class WebModelOption
    {
        // Aliased to the Core-layer constant so the Auto sentinel matches the
        // Desktop ModelOption.AutoValue and the OpenRouter enricher's check.
        public const string AutoValue = OpenRouterModelDefaults.AutoModelValue;
        public const string AutoLabel = "Auto: try free models first, fall back to error if all fail";
    }
}
