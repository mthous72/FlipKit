using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;

namespace FlipKit.Web.Models
{
    /// <summary>
    /// View model for the scan upload page.
    /// </summary>
    public class ScanUploadViewModel
    {
        public string SelectedModel { get; set; } = "google/gemma-3-27b-it:free";

        // Use centralized model list from OpenRouterScannerService
        public List<string> AvailableModels { get; set; } = OpenRouterScannerService.AllVisionModels.ToList();

        public string ScanMode { get; set; } = "selling"; // Buying or Selling mode

        /// <summary>
        /// Controls how Ximilar recognition is used. Persisted in session.
        /// </summary>
        public XimilarScanMode XimilarMode { get; set; } = XimilarScanMode.Standard;
    }
}
