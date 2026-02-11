using System;
using System.Threading.Tasks;
using FlipKit.Core.Services;
using FlipKit.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Desktop.Services
{
    /// <summary>
    /// Avalonia-specific implementation of INavigationService.
    /// Navigates by switching ViewModels in MainWindowViewModel.CurrentPage.
    /// </summary>
    public class AvaloniaNavigationService : INavigationService
    {
        private readonly MainWindowViewModel _mainWindow;
        private readonly IServiceProvider _services;

        public AvaloniaNavigationService(
            MainWindowViewModel mainWindow,
            IServiceProvider services)
        {
            _mainWindow = mainWindow;
            _services = services;
        }

        public Task NavigateAsync(string pageName, object? parameter = null)
        {
            _mainWindow.CurrentPageName = pageName;
            _mainWindow.CurrentPage = pageName switch
            {
                "Scan" => _services.GetRequiredService<ScanViewModel>(),
                "BulkScan" => _services.GetRequiredService<BulkScanViewModel>(),
                "Inventory" => _services.GetRequiredService<InventoryViewModel>(),
                "Pricing" => _services.GetRequiredService<PricingViewModel>(),
                "Export" => _services.GetRequiredService<ExportViewModel>(),
                "Reports" => _services.GetRequiredService<ReportsViewModel>(),
                "SalesReport" => _services.GetRequiredService<ReportsViewModel>(), // Placeholder for future split
                "FinancialReport" => _services.GetRequiredService<ReportsViewModel>(), // Placeholder for future split
                "Checklists" => _services.GetRequiredService<ChecklistManagerViewModel>(),
                "Settings" => _services.GetRequiredService<SettingsViewModel>(),
                "Reprice" => _services.GetRequiredService<RepriceViewModel>(),
                _ => throw new ArgumentException($"Unknown page: {pageName}", nameof(pageName))
            };
            return Task.CompletedTask;
        }

        public Task NavigateToScanAsync()
        {
            return NavigateAsync("Scan");
        }

        public Task NavigateToBulkScanAsync()
        {
            return NavigateAsync("BulkScan");
        }

        public Task NavigateToInventoryAsync()
        {
            return NavigateAsync("Inventory");
        }

        public Task NavigateToPricingAsync(int cardId)
        {
            // Note: PricingViewModel doesn't have LoadCardAsync
            // User selects card from the Pricing page itself
            // For future enhancement, could add SetSelectedCard(cardId) method
            return NavigateAsync("Pricing");
        }

        public async Task NavigateToEditCardAsync(int cardId)
        {
            var vm = _services.GetRequiredService<EditCardViewModel>();
            await vm.LoadCardAsync(cardId);
            _mainWindow.CurrentPageName = "EditCard";
            _mainWindow.CurrentPage = vm;
        }

        public Task NavigateToVerifyVariationAsync(object parameter)
        {
            // VerifyVariationViewModel is created by ScanViewModel/BulkScanViewModel
            // and passed directly, so we just set it as CurrentPage
            if (parameter is ViewModelBase vm)
            {
                _mainWindow.CurrentPageName = "VerifyVariation";
                _mainWindow.CurrentPage = vm;
            }
            return Task.CompletedTask;
        }

        public Task NavigateToExportAsync()
        {
            return NavigateAsync("Export");
        }

        public Task NavigateToReportsAsync()
        {
            return NavigateAsync("Reports");
        }

        public Task NavigateToSalesReportAsync()
        {
            // Future: When SalesReportViewModel exists, create it here
            // For now, navigate to Reports page
            return NavigateAsync("Reports");
        }

        public Task NavigateToFinancialReportAsync()
        {
            // Future: When FinancialReportViewModel exists, create it here
            // For now, navigate to Reports page
            return NavigateAsync("Reports");
        }

        public Task NavigateToSettingsAsync()
        {
            return NavigateAsync("Settings");
        }

        public Task NavigateToChecklistManagerAsync()
        {
            return NavigateAsync("Checklists");
        }

        public async Task NavigateToEditChecklistAsync(int checklistId)
        {
            // Future: When EditChecklistViewModel has a LoadChecklistAsync method
            // For now, just navigate to checklist manager
            await NavigateToChecklistManagerAsync();
        }

        public Task NavigateToRepriceAsync()
        {
            return NavigateAsync("Reprice");
        }

        public Task GoBackAsync()
        {
            // Avalonia doesn't have built-in back navigation
            // Default to navigating to Inventory as a sensible fallback
            return NavigateToInventoryAsync();
        }
    }
}
