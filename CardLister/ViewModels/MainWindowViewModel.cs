using System;
using System.Threading.Tasks;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Desktop.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ISettingsService _settingsService;
        private INavigationService? _navigationService;

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
                    // Fire-and-forget navigation is acceptable for UI callbacks
                    _ = NavigateTo("Scan");
                };
                _currentPage = wizard;
            }
            else
            {
                _currentPage = _services.GetRequiredService<ScanViewModel>();
            }
        }

        partial void OnCurrentPageChanging(ViewModelBase value)
        {
            // Dispose old page if it implements IDisposable
            // Note: Intentionally using backing field here since this is called before property change
#pragma warning disable MVVMTK0034
            if (_currentPage is IDisposable disposable)
            {
                disposable.Dispose();
            }
#pragma warning restore MVVMTK0034
        }

        [RelayCommand]
        private async Task NavigateTo(string page)
        {
            // Lazy-resolve navigation service to avoid circular dependency
            _navigationService ??= _services.GetRequiredService<INavigationService>();
            await _navigationService.NavigateAsync(page);
        }

        public async Task NavigateToEditCardAsync(int cardId)
        {
            // Lazy-resolve navigation service to avoid circular dependency
            _navigationService ??= _services.GetRequiredService<INavigationService>();
            await _navigationService.NavigateToEditCardAsync(cardId);
        }

        public void Dispose()
        {
            // Dispose the current page on window close
            if (CurrentPage is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
