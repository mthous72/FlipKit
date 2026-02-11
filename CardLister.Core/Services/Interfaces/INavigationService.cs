using System.Threading.Tasks;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Platform-agnostic navigation service for routing between pages.
    /// Desktop implementation uses MainWindowViewModel.CurrentPage switching.
    /// Web implementation uses ASP.NET Core MVC RedirectToAction.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Navigate to a page by name with optional parameter.
        /// </summary>
        /// <param name="pageName">The name of the page (e.g., "Scan", "Inventory")</param>
        /// <param name="parameter">Optional parameter to pass to the page</param>
        Task NavigateAsync(string pageName, object? parameter = null);

        /// <summary>
        /// Navigate to the Scan page for scanning new cards.
        /// </summary>
        Task NavigateToScanAsync();

        /// <summary>
        /// Navigate to the Bulk Scan page for batch scanning.
        /// </summary>
        Task NavigateToBulkScanAsync();

        /// <summary>
        /// Navigate to the Inventory page to view all cards.
        /// </summary>
        Task NavigateToInventoryAsync();

        /// <summary>
        /// Navigate to the Pricing page for a specific card.
        /// </summary>
        /// <param name="cardId">The ID of the card to price</param>
        Task NavigateToPricingAsync(int cardId);

        /// <summary>
        /// Navigate to the Edit Card page for a specific card.
        /// </summary>
        /// <param name="cardId">The ID of the card to edit</param>
        Task NavigateToEditCardAsync(int cardId);

        /// <summary>
        /// Navigate to the Verify Variation page with scan results.
        /// </summary>
        /// <param name="parameter">The verification data</param>
        Task NavigateToVerifyVariationAsync(object parameter);

        /// <summary>
        /// Navigate to the Export page to generate CSV files.
        /// </summary>
        Task NavigateToExportAsync();

        /// <summary>
        /// Navigate to the Reports page to view analytics.
        /// </summary>
        Task NavigateToReportsAsync();

        /// <summary>
        /// Navigate to the Sales Report page.
        /// </summary>
        Task NavigateToSalesReportAsync();

        /// <summary>
        /// Navigate to the Financial Report page.
        /// </summary>
        Task NavigateToFinancialReportAsync();

        /// <summary>
        /// Navigate to the Settings page.
        /// </summary>
        Task NavigateToSettingsAsync();

        /// <summary>
        /// Navigate to the Checklist Manager page.
        /// </summary>
        Task NavigateToChecklistManagerAsync();

        /// <summary>
        /// Navigate to the Edit Checklist page for a specific checklist.
        /// </summary>
        /// <param name="checklistId">The ID of the checklist to edit</param>
        Task NavigateToEditChecklistAsync(int checklistId);

        /// <summary>
        /// Navigate to the Reprice page to update pricing for cards.
        /// </summary>
        Task NavigateToRepriceAsync();

        /// <summary>
        /// Navigate back to the previous page (if supported by platform).
        /// </summary>
        Task GoBackAsync();
    }
}
