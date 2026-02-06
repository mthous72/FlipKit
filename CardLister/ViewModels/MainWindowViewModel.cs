using System;
using System.Threading.Tasks;
using CardLister.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace CardLister.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IServiceProvider _services;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private ViewModelBase _currentPage;

        [ObservableProperty]
        private string _currentPageName = "Scan";

        [ObservableProperty]
        private bool _showSidebar = true;

        public MainWindowViewModel(IServiceProvider services, ISettingsService settingsService)
        {
            _services = services;
            _settingsService = settingsService;

            if (!_settingsService.HasValidConfig())
            {
                ShowSidebar = false;
                var wizard = _services.GetRequiredService<SetupWizardViewModel>();
                wizard.OnSetupComplete = () =>
                {
                    ShowSidebar = true;
                    NavigateTo("Scan");
                };
                _currentPage = wizard;
            }
            else
            {
                _currentPage = _services.GetRequiredService<ScanViewModel>();
            }
        }

        [RelayCommand]
        private void NavigateTo(string page)
        {
            CurrentPageName = page;
            CurrentPage = page switch
            {
                "Scan" => _services.GetRequiredService<ScanViewModel>(),
                "BulkScan" => _services.GetRequiredService<BulkScanViewModel>(),
                "Inventory" => _services.GetRequiredService<InventoryViewModel>(),
                "Pricing" => _services.GetRequiredService<PricingViewModel>(),
                "Export" => _services.GetRequiredService<ExportViewModel>(),
                "Reports" => _services.GetRequiredService<ReportsViewModel>(),
                "Checklists" => _services.GetRequiredService<ChecklistManagerViewModel>(),
                "Settings" => _services.GetRequiredService<SettingsViewModel>(),
                "Reprice" => _services.GetRequiredService<RepriceViewModel>(),
                _ => CurrentPage
            };
        }

        public async Task NavigateToEditCardAsync(int cardId)
        {
            var editVm = _services.GetRequiredService<EditCardViewModel>();
            await editVm.LoadCardAsync(cardId);
            CurrentPageName = "EditCard";
            CurrentPage = editVm;
        }
    }
}
