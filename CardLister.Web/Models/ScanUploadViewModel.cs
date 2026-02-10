using CardLister.Core.Services;

namespace CardLister.Web.Models
{
    /// <summary>
    /// View model for the scan upload page.
    /// </summary>
    public class ScanUploadViewModel
    {
        public string SelectedModel { get; set; } = "google/gemini-flash-1.5:free";

        // Use centralized model list from OpenRouterScannerService
        public List<string> AvailableModels { get; set; } = OpenRouterScannerService.AllVisionModels.ToList();
    }
}
