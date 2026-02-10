using CardLister.Core.Services;

namespace CardLister.Web.Models
{
    /// <summary>
    /// View model for the scan upload page.
    /// </summary>
    public class ScanUploadViewModel
    {
        public string SelectedModel { get; set; } = "meta-llama/llama-3.2-11b-vision-instruct:free";

        // Use centralized model list from OpenRouterScannerService
        public List<string> AvailableModels { get; set; } = OpenRouterScannerService.AllVisionModels.ToList();
    }
}
