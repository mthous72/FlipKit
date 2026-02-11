using System;
using System.Threading.Tasks;
using FlipKit.Core.Services;

namespace FlipKit.Web.Services
{
    /// <summary>
    /// Web implementation of INavigationService.
    /// In MVC applications, navigation is handled via RedirectToAction in controllers,
    /// not through a service. This implementation is provided for interface compatibility
    /// but most navigation methods are not supported.
    ///
    /// Controllers should use: return RedirectToAction("Action", "Controller", new { id = ... });
    /// </summary>
    public class MvcNavigationService : INavigationService
    {
        public Task NavigateAsync(string pageName, object? parameter = null)
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use RedirectToAction in controllers.");
        }

        public Task NavigateToScanAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Scan\");");
        }

        public Task NavigateToBulkScanAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"BulkScan\");");
        }

        public Task NavigateToInventoryAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Inventory\");");
        }

        public Task NavigateToPricingAsync(int cardId)
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Research\", \"Pricing\", new { id = cardId });");
        }

        public Task NavigateToEditCardAsync(int cardId)
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Edit\", \"Inventory\", new { id = cardId });");
        }

        public Task NavigateToVerifyVariationAsync(object parameter)
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Verify\", \"Variation\", parameter);");
        }

        public Task NavigateToExportAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Export\");");
        }

        public Task NavigateToReportsAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Reports\");");
        }

        public Task NavigateToSalesReportAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Sales\", \"Reports\");");
        }

        public Task NavigateToFinancialReportAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Financial\", \"Reports\");");
        }

        public Task NavigateToSettingsAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Settings\");");
        }

        public Task NavigateToChecklistManagerAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Checklist\");");
        }

        public Task NavigateToEditChecklistAsync(int checklistId)
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Edit\", \"Checklist\", new { id = checklistId });");
        }

        public Task NavigateToRepriceAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use: return RedirectToAction(\"Index\", \"Reprice\");");
        }

        public Task GoBackAsync()
        {
            throw new NotSupportedException(
                "MVC navigation is controller-based. Use JavaScript: history.back(); or redirect to previous page.");
        }
    }
}
