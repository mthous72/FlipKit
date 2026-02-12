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
        private readonly IServerManagementService _serverManagement;
        private INavigationService? _navigationService;

        [ObservableProperty]
        private ViewModelBase _currentPage;

        [ObservableProperty]
        private string _currentPageName = "Scan";

        [ObservableProperty]
        private bool _showSidebar = true;

        [ObservableProperty]
        private string _trayTooltip = "FlipKit Hub";

        [ObservableProperty]
        private bool _isWindowVisible = true;

        public MainWindowViewModel(IServiceProvider services, ISettingsService settingsService,
            IServerManagementService serverManagement)
        {
            _services = services;
            _settingsService = settingsService;
            _serverManagement = serverManagement;

            // Start tray tooltip updater
            UpdateTrayTooltip();

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

        [RelayCommand]
        private void ShowWindow()
        {
            IsWindowVisible = true;
        }

        [RelayCommand]
        private void HideWindow()
        {
            IsWindowVisible = false;
        }

        [RelayCommand]
        private void ToggleWindow()
        {
            IsWindowVisible = !IsWindowVisible;
        }

        [RelayCommand]
        private async Task StartWebServerFromTray()
        {
            var settings = _settingsService.Load();
            await _serverManagement.StartWebServerAsync(settings.WebServerPort);
            UpdateTrayTooltip();
        }

        [RelayCommand]
        private async Task StopWebServerFromTray()
        {
            await _serverManagement.StopWebServerAsync();
            UpdateTrayTooltip();
        }

        [RelayCommand]
        private async Task StartApiServerFromTray()
        {
            var settings = _settingsService.Load();
            await _serverManagement.StartApiServerAsync(settings.ApiServerPort);
            UpdateTrayTooltip();
        }

        [RelayCommand]
        private async Task StopApiServerFromTray()
        {
            await _serverManagement.StopApiServerAsync();
            UpdateTrayTooltip();
        }

        [RelayCommand]
        private void OpenWebBrowser()
        {
            var status = _serverManagement.GetServerStatus();
            if (status.IsWebRunning)
            {
                var browser = _services.GetRequiredService<IBrowserService>();
                browser.OpenUrl($"http://localhost:{status.WebPort}");
            }
        }

        [RelayCommand]
        private async Task ExitApplication()
        {
            // Stop servers before exiting
            await _serverManagement.StopWebServerAsync();
            await _serverManagement.StopApiServerAsync();

            // Small delay to ensure servers are fully stopped
            await Task.Delay(300);

            // Now exit the application
            System.Environment.Exit(0);
        }

        private void UpdateTrayTooltip()
        {
            var status = _serverManagement.GetServerStatus();
            var webStatus = status.IsWebRunning ? "●" : "○";
            var apiStatus = status.IsApiRunning ? "●" : "○";
            TrayTooltip = $"FlipKit Hub - Web: {webStatus} API: {apiStatus}";
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
