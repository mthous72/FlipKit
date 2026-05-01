using System.Collections.Generic;
using FlipKit.Core.Models.Enums;
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
        /// Controls how Ximilar recognition is used. Persisted in session.
        /// </summary>
        public XimilarScanMode XimilarMode { get; set; } = XimilarScanMode.Standard;
    }

    /// <summary>
    /// Constants and helpers for the Web model dropdown. Mirrors the Desktop ModelOption
    /// (kept simple — no class hierarchy needed since the Web side uses Razor optgroups).
    /// </summary>
    public static class WebModelOption
    {
        public const string AutoValue = "auto";
        public const string AutoLabel = "Auto: try free models first, fall back to error if all fail";
    }
}
