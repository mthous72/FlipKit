using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

        private BulkScanViewModel? _bulkScanVm;

        /// <summary>
        /// Singleton bulk-scan view model exposed for the persistent global status bar.
        /// Lazy-resolved so the model catalog HTTP call doesn't fire on app launch —
        /// only when the status bar binding first reads it (after navigation occurs).
        /// </summary>
        public BulkScanViewModel BulkScanVm => _bulkScanVm ??= _services.GetRequiredService<BulkScanViewModel>();

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
            // Dispose old page if it implements IDisposable — but skip IKeepAliveViewModel
            // singletons (e.g. BulkScanViewModel) so in-flight scans survive tab switches.
            // The DI container disposes singletons correctly on app shutdown.
#pragma warning disable MVVMTK0034
            if (_currentPage is IDisposable disposable && _currentPage is not IKeepAliveViewModel)
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
            // Cancel any active scans FIRST so in-flight HTTP requests abort
            // before the server processes and HttpClient are torn down.
            if (_services.GetService<BulkScanViewModel>() is { } bulkScanVm)
                bulkScanVm.Dispose();

            // Stop servers before triggering shutdown
            await _serverManagement.StopWebServerAsync();
            await _serverManagement.StopApiServerAsync();

            // Use Avalonia's shutdown so ShutdownRequested fires and the DI
            // container is disposed cleanly, rather than a hard process exit.
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime appLifetime)
                appLifetime.Shutdown();
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
            // Explicitly cancel the BulkScanViewModel singleton so its in-flight scans
            // abort even when it is not the current page.
            if (_services.GetService<BulkScanViewModel>() is IDisposable bulkScanDisposable)
                bulkScanDisposable.Dispose();

            if (CurrentPage is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
