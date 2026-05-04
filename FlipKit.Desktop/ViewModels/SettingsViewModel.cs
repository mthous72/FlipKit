using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IBrowserService _browserService;
        private readonly IServiceProvider _services;
        private readonly IServerManagementService _serverManagement;
        private readonly INetworkAddressProvider _networkAddresses;
        private Timer? _statusRefreshTimer;

        // Phase 5.10 fix — gate the 2-second Timer's UpdateServerStatus while an explicit
        // user Start/Stop command is in flight, so the Timer doesn't clobber the success
        // or failure message before the user sees it. See AUDIT-2026-05 §7.10.
        private volatile bool _explicitOperationInProgress;

        // API Keys
        [ObservableProperty] private string _openRouterApiKey = string.Empty;
        [ObservableProperty] private string _imgBBApiKey = string.Empty;
        [ObservableProperty] private string _ximilarApiKey = string.Empty;
        [ObservableProperty] private string _openRouterStatus = "Not configured";
        [ObservableProperty] private string _imgBBStatus = "Not configured";
        [ObservableProperty] private string _ximilarStatus = "Not configured";
        [ObservableProperty] private bool _isTestingOpenRouter;
        [ObservableProperty] private bool _isTestingImgBB;
        [ObservableProperty] private bool _isTestingXimilar;

        // Preferences
        [ObservableProperty] private bool _isEbaySeller;
        [ObservableProperty] private string _defaultShippingProfile = "4 oz";
        [ObservableProperty] private string _defaultCondition = "Near Mint";
        [ObservableProperty] private string _defaultModel = ModelOption.AutoValue;
        [ObservableProperty] private ModelOption? _selectedDefaultModel;
        [ObservableProperty] private bool _isLoadingModels;
        [ObservableProperty] private string? _modelLoadError;
        [ObservableProperty] private string? _modelLoadStatus;

        public ObservableCollection<ModelOption> ModelOptions { get; } = new();

        // Card Scanning
        [ObservableProperty] private bool _enableVariationVerification = true;
        [ObservableProperty] private bool _autoApplyHighConfidenceSuggestions = true;
        [ObservableProperty] private bool _runConfirmationPass = true;
        [ObservableProperty] private bool _enableChecklistLearning = true;
        [ObservableProperty] private int _maxConcurrentScans = 1;

        // Financial
        [ObservableProperty] private decimal _whatnotFeePercent = 11.0m;
        [ObservableProperty] private decimal _ebayFeePercent = 13.25m;
        [ObservableProperty] private decimal _defaultShippingCostPwe = 1.00m;
        [ObservableProperty] private decimal _defaultShippingCostBmwt = 4.50m;
        [ObservableProperty] private int _priceStalenessThresholdDays = 30;

        // Data Info
        [ObservableProperty] private int _cardCount;
        [ObservableProperty] private string _dbPath = string.Empty;

        // Title Templates (SEO-optimized for each platform)
        [ObservableProperty] private string _whatnotTitleTemplate = string.Empty;
        [ObservableProperty] private string _ebayTitleTemplate = string.Empty;
        [ObservableProperty] private string _comcTitleTemplate = string.Empty;
        [ObservableProperty] private string _genericTitleTemplate = string.Empty;
        [ObservableProperty] private ExportPlatform _activeExportPlatform = ExportPlatform.Whatnot;
        [ObservableProperty] private string _templateValidationMessage = string.Empty;
        [ObservableProperty] private string _templatePreview = string.Empty;

        public List<ExportPlatform> ExportPlatformOptions { get; } = Enum.GetValues<ExportPlatform>().ToList();
        public string PlaceholderHelpText => TitleTemplateService.GetPlaceholderHelpText();

        // Search Query Templates (for pricing research)
        [ObservableProperty] private string _terapeakSearchTemplate = string.Empty;
        [ObservableProperty] private string _ebaySearchTemplate = string.Empty;
        [ObservableProperty] private string _searchTemplateValidationMessage = string.Empty;

        // eBay export defaults — populate before exporting eBay listings.
        [ObservableProperty] private string _ebaySellerLocation = string.Empty;
        [ObservableProperty] private int _ebayDispatchTimeMax = 2;
        [ObservableProperty] private bool _ebayReturnsAccepted = true;
        [ObservableProperty] private bool _ebayUseVerifyAdd;
        [ObservableProperty] private string _searchTemplatePreview = string.Empty;

        // Save feedback
        [ObservableProperty] private string _saveMessage = string.Empty;

        // Data Access Mode
        [ObservableProperty] private string? _syncServerUrl;
        [ObservableProperty] private string _dataAccessMode = "Local Database (Direct access)";
        [ObservableProperty] private string _dataAccessModeColor = "Green";

        // Server Management (FlipKit Hub)
        [ObservableProperty] private bool _autoStartWebServer = true;
        [ObservableProperty] private bool _autoStartApiServer = true;
        [ObservableProperty] private int _webServerPort = 5000;
        [ObservableProperty] private int _apiServerPort = 5001;
        [ObservableProperty] private bool _minimizeToTray = true;
        [ObservableProperty] private bool _autoOpenBrowser = true;

        [ObservableProperty] private bool _isWebRunning;
        [ObservableProperty] private bool _isApiRunning;
        [ObservableProperty] private int _actualWebPort;
        [ObservableProperty] private int _actualApiPort;
        [ObservableProperty] private string _webServerStatus = "Stopped";
        [ObservableProperty] private string _apiServerStatus = "Stopped";
        [ObservableProperty] private string _localIpAddresses = "No network connection";
        [ObservableProperty] private string _webServerLogs = string.Empty;
        [ObservableProperty] private string _apiServerLogs = string.Empty;
        [ObservableProperty] private Bitmap? _qrCodeBitmap;

        // Dual QR Code Support
        [ObservableProperty] private string? _localNetworkIp;
        [ObservableProperty] private string? _tailscaleIp;
        [ObservableProperty] private string _localNetworkUrl = string.Empty;
        [ObservableProperty] private string _tailscaleUrl = string.Empty;
        [ObservableProperty] private bool _isLocalNetworkAvailable;
        [ObservableProperty] private bool _isTailscaleAvailable;
        [ObservableProperty] private Bitmap? _localQrCodeBitmap;
        [ObservableProperty] private Bitmap? _tailscaleQrCodeBitmap;
        [ObservableProperty] private string _localNetworkStatus = "Checking...";
        [ObservableProperty] private string _tailscaleStatus = "Not configured";

        private readonly IOpenRouterModelCatalog? _modelCatalog;

        public SettingsViewModel(ISettingsService settingsService, IBrowserService browserService,
            IServiceProvider services, IServerManagementService serverManagement,
            INetworkAddressProvider networkAddresses)
        {
            _settingsService = settingsService;
            _browserService = browserService;
            _services = services;
            _serverManagement = serverManagement;
            _networkAddresses = networkAddresses;

            // Optional resolution — Settings page should still load even if catalog fails to register.
            _modelCatalog = services.GetService(typeof(IOpenRouterModelCatalog)) as IOpenRouterModelCatalog;

            LoadSettings();
            LoadCardCountAsync();
            UpdateDataAccessMode();
            UpdateServerStatus();
            UpdateLocalIpAddresses();

            _ = LoadModelsAsync();

            // Refresh server status every 2 seconds
            _statusRefreshTimer = new Timer(_ =>
            {
                UpdateServerStatus();
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        private async Task LoadModelsAsync(bool forceRefresh = false)
        {
            if (_modelCatalog == null) return;
            IsLoadingModels = true;
            ModelLoadError = null;
            ModelLoadStatus = forceRefresh ? "Refreshing model list from OpenRouter..." : "Loading model catalog...";
            try
            {
                if (forceRefresh) _modelCatalog.InvalidateCache();
                var catalog = await _modelCatalog.GetAsync();

                ModelOptions.Clear();
                ModelOptions.Add(ModelOption.Auto());
                foreach (var m in catalog.FreeVisionModels) ModelOptions.Add(ModelOption.FromCatalog(m));
                foreach (var m in catalog.PaidVisionModels) ModelOptions.Add(ModelOption.FromCatalog(m));

                var savedId = string.IsNullOrWhiteSpace(DefaultModel) ? ModelOption.AutoValue : DefaultModel;
                ModelOption? choice = ModelOptions.FirstOrDefault(o => o.Value == savedId);
                if (choice == null && savedId != ModelOption.AutoValue)
                {
                    choice = ModelOption.Stale(savedId);
                    ModelOptions.Add(choice);
                }
                SelectedDefaultModel = choice ?? ModelOptions.First();

                ModelLoadStatus = catalog.IsEmpty
                    ? "Couldn't reach OpenRouter — using saved default only."
                    : $"Loaded {catalog.FreeVisionModels.Count} free + {catalog.PaidVisionModels.Count} paid vision models.";
            }
            catch (Exception ex)
            {
                ModelLoadError = ex.Message;
                ModelLoadStatus = null;
            }
            finally
            {
                IsLoadingModels = false;
            }
        }

        [RelayCommand]
        private async Task RefreshModelsAsync()
        {
            await LoadModelsAsync(forceRefresh: true);
        }

        partial void OnSelectedDefaultModelChanged(ModelOption? value)
        {
            // By design: DefaultModel string field stays in sync with SelectedDefaultModel
            // ModelOption. LoadModelsAsync resolves saved DefaultModel → ModelOption, the
            // assignment fires this partial, and DefaultModel gets written back to the
            // resolved value. If saved value matches an existing option no change; if it
            // doesn't, we get the Stale stub's value, which is what we want.
            if (value != null) DefaultModel = value.Value;
        }

        private void LoadSettings()
        {
            var s = _settingsService.Load();

            OpenRouterApiKey = s.OpenRouterApiKey ?? string.Empty;
            ImgBBApiKey = s.ImgBBApiKey ?? string.Empty;
            XimilarApiKey = s.XimilarApiKey ?? string.Empty;
            IsEbaySeller = s.IsEbaySeller;
            DefaultShippingProfile = s.DefaultShippingProfile;
            DefaultCondition = s.DefaultCondition;
            DefaultModel = s.DefaultModel;
            EnableVariationVerification = s.EnableVariationVerification;
            AutoApplyHighConfidenceSuggestions = s.AutoApplyHighConfidenceSuggestions;
            RunConfirmationPass = s.RunConfirmationPass;
            EnableChecklistLearning = s.EnableChecklistLearning;
            MaxConcurrentScans = s.MaxConcurrentScans;
            WhatnotFeePercent = s.WhatnotFeePercent;
            EbayFeePercent = s.EbayFeePercent;
            DefaultShippingCostPwe = s.DefaultShippingCostPwe;
            DefaultShippingCostBmwt = s.DefaultShippingCostBmwt;
            PriceStalenessThresholdDays = s.PriceStalenessThresholdDays;

            // Title Templates
            WhatnotTitleTemplate = s.WhatnotTitleTemplate;
            EbayTitleTemplate = s.EbayTitleTemplate;
            ComcTitleTemplate = s.ComcTitleTemplate;
            GenericTitleTemplate = s.GenericTitleTemplate;
            ActiveExportPlatform = s.ActiveExportPlatform;

            // Search Query Templates
            TerapeakSearchTemplate = s.TerapeakSearchTemplate;
            EbaySearchTemplate = s.EbaySearchTemplate;

            // eBay export defaults
            EbaySellerLocation = s.EbaySellerLocation;
            EbayDispatchTimeMax = s.EbayDispatchTimeMax;
            EbayReturnsAccepted = s.EbayReturnsAccepted;
            EbayUseVerifyAdd = s.EbayUseVerifyAdd;

            // API Server URL
            SyncServerUrl = s.SyncServerUrl;

            // Server Management (FlipKit Hub)
            AutoStartWebServer = s.AutoStartWebServer;
            AutoStartApiServer = s.AutoStartApiServer;
            WebServerPort = s.WebServerPort;
            ApiServerPort = s.ApiServerPort;
            MinimizeToTray = s.MinimizeToTray;
            AutoOpenBrowser = s.AutoOpenBrowser;

            OpenRouterStatus = string.IsNullOrWhiteSpace(OpenRouterApiKey) ? "Not configured" : "Configured (not tested)";
            ImgBBStatus = string.IsNullOrWhiteSpace(ImgBBApiKey) ? "Not configured" : "Configured (not tested)";
            XimilarStatus = string.IsNullOrWhiteSpace(XimilarApiKey) ? "Not configured" : "Configured (not tested)";

            DbPath = FlipKitDbContext.GetDbPath();
        }

        private async void LoadCardCountAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                CardCount = await db.Cards.CountAsync();
            }
            catch
            {
                CardCount = 0;
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            // Validate all templates before saving
            var templates = new[]
            {
                ("Whatnot Title", WhatnotTitleTemplate),
                ("eBay Title", EbayTitleTemplate),
                ("COMC Title", ComcTitleTemplate),
                ("Generic Title", GenericTitleTemplate),
                ("Terapeak Search", TerapeakSearchTemplate),
                ("eBay Search", EbaySearchTemplate)
            };

            foreach (var (name, template) in templates)
            {
                var (isValid, errorMessage) = TitleTemplateService.ValidateTemplate(template);
                if (!isValid)
                {
                    SaveMessage = $"{name} template error: {errorMessage}";
                    TemplateValidationMessage = SaveMessage;
                    SearchTemplateValidationMessage = SaveMessage;
                    return;
                }
            }

            var s = new AppSettings
            {
                OpenRouterApiKey = OpenRouterApiKey,
                ImgBBApiKey = ImgBBApiKey,
                XimilarApiKey = XimilarApiKey,
                IsEbaySeller = IsEbaySeller,
                DefaultShippingProfile = DefaultShippingProfile,
                DefaultCondition = DefaultCondition,
                DefaultModel = DefaultModel,
                EnableVariationVerification = EnableVariationVerification,
                AutoApplyHighConfidenceSuggestions = AutoApplyHighConfidenceSuggestions,
                RunConfirmationPass = RunConfirmationPass,
                EnableChecklistLearning = EnableChecklistLearning,
                MaxConcurrentScans = MaxConcurrentScans,
                WhatnotFeePercent = WhatnotFeePercent,
                EbayFeePercent = EbayFeePercent,
                DefaultShippingCostPwe = DefaultShippingCostPwe,
                DefaultShippingCostBmwt = DefaultShippingCostBmwt,
                PriceStalenessThresholdDays = PriceStalenessThresholdDays,
                WhatnotTitleTemplate = WhatnotTitleTemplate,
                EbayTitleTemplate = EbayTitleTemplate,
                ComcTitleTemplate = ComcTitleTemplate,
                GenericTitleTemplate = GenericTitleTemplate,
                ActiveExportPlatform = ActiveExportPlatform,
                TerapeakSearchTemplate = TerapeakSearchTemplate,
                EbaySearchTemplate = EbaySearchTemplate,
                EbaySellerLocation = EbaySellerLocation,
                EbayDispatchTimeMax = EbayDispatchTimeMax,
                EbayReturnsAccepted = EbayReturnsAccepted,
                EbayUseVerifyAdd = EbayUseVerifyAdd,
                SyncServerUrl = SyncServerUrl,
                AutoStartWebServer = AutoStartWebServer,
                AutoStartApiServer = AutoStartApiServer,
                WebServerPort = WebServerPort,
                ApiServerPort = ApiServerPort,
                MinimizeToTray = MinimizeToTray,
                AutoOpenBrowser = AutoOpenBrowser
            };

            _settingsService.Save(s);
            UpdateDataAccessMode();
            SaveMessage = "Settings saved!";
            TemplateValidationMessage = string.Empty;
        }

        [RelayCommand]
        private async Task TestOpenRouterAsync()
        {
            IsTestingOpenRouter = true;
            OpenRouterStatus = "Testing...";

            var success = await _settingsService.TestOpenRouterConnectionAsync(OpenRouterApiKey);
            OpenRouterStatus = success ? "Connected!" : "Connection failed";

            IsTestingOpenRouter = false;
        }

        [RelayCommand]
        private async Task TestImgBBAsync()
        {
            IsTestingImgBB = true;
            ImgBBStatus = "Testing...";

            var success = await _settingsService.TestImgBBConnectionAsync(ImgBBApiKey);
            ImgBBStatus = success ? "Connected!" : "Connection failed";

            IsTestingImgBB = false;
        }

        [RelayCommand]
        private async Task TestXimilarAsync()
        {
            IsTestingXimilar = true;
            XimilarStatus = "Testing...";

            var success = await _settingsService.TestXimilarConnectionAsync(XimilarApiKey);
            XimilarStatus = success ? "Connected!" : "Connection failed";

            IsTestingXimilar = false;
        }

        [RelayCommand]
        private void OpenDataFolder()
        {
            var folder = Path.GetDirectoryName(DbPath);
            if (folder != null && Directory.Exists(folder))
            {
                _browserService.OpenUrl(folder);
            }
        }

        [RelayCommand]
        private void ResetTitleTemplates()
        {
            WhatnotTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Whatnot);
            EbayTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.eBay);
            ComcTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.COMC);
            GenericTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Generic);
            TemplateValidationMessage = "Templates reset to defaults";
        }

        [RelayCommand]
        private void ValidateCurrentTemplate()
        {
            var template = ActiveExportPlatform switch
            {
                ExportPlatform.Whatnot => WhatnotTitleTemplate,
                ExportPlatform.eBay => EbayTitleTemplate,
                ExportPlatform.COMC => ComcTitleTemplate,
                _ => GenericTitleTemplate
            };

            var (isValid, errorMessage) = TitleTemplateService.ValidateTemplate(template);
            TemplateValidationMessage = isValid ? "✓ Template is valid" : $"✗ {errorMessage}";
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            // Get a sample card from the database for preview
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                var sampleCard = await db.Cards.FirstOrDefaultAsync();

                if (sampleCard == null)
                {
                    TemplatePreview = "No cards in database. Add a card to see preview.";
                    return;
                }

                var template = ActiveExportPlatform switch
                {
                    ExportPlatform.Whatnot => WhatnotTitleTemplate,
                    ExportPlatform.eBay => EbayTitleTemplate,
                    ExportPlatform.COMC => ComcTitleTemplate,
                    _ => GenericTitleTemplate
                };

                var titleService = new TitleTemplateService();
                TemplatePreview = $"Preview: {titleService.GenerateTitle(sampleCard, template)}";
            }
            catch (Exception ex)
            {
                TemplatePreview = $"Error generating preview: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ResetSearchTemplates()
        {
            TerapeakSearchTemplate = "{Year} {Brand} {Player} {Parallel} {Attributes} {Grade}";
            EbaySearchTemplate = "{Year} {Manufacturer} {Brand} {Player} {Team} {Parallel} {Attributes} {Grade}";
            SearchTemplateValidationMessage = "Search templates reset to defaults";
        }

        [RelayCommand]
        private void ValidateSearchTemplates()
        {
            var templates = new[]
            {
                ("Terapeak", TerapeakSearchTemplate),
                ("eBay", EbaySearchTemplate)
            };

            foreach (var (name, template) in templates)
            {
                var (isValid, errorMessage) = TitleTemplateService.ValidateTemplate(template);
                if (!isValid)
                {
                    SearchTemplateValidationMessage = $"✗ {name}: {errorMessage}";
                    return;
                }
            }

            SearchTemplateValidationMessage = "✓ All search templates are valid";
        }

        [RelayCommand]
        private async Task GenerateSearchPreviewAsync()
        {
            // Get a sample card from the database for preview
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                var sampleCard = await db.Cards.FirstOrDefaultAsync();

                if (sampleCard == null)
                {
                    SearchTemplatePreview = "No cards in database. Add a card to see preview.";
                    return;
                }

                var titleService = new TitleTemplateService();
                var terapeakQuery = titleService.GenerateTitle(sampleCard, TerapeakSearchTemplate);
                var ebayQuery = titleService.GenerateTitle(sampleCard, EbaySearchTemplate);

                SearchTemplatePreview = $"Terapeak: {terapeakQuery}\neBay: {ebayQuery}";
            }
            catch (Exception ex)
            {
                SearchTemplatePreview = $"Error generating preview: {ex.Message}";
            }
        }

        private void UpdateDataAccessMode()
        {
            var settings = _settingsService.Load();
            var mode = DataAccessModeDetector.DetectMode(settings);

            DataAccessMode = DataAccessModeDetector.GetModeDescription(mode);
            DataAccessModeColor = mode == Core.Helpers.DataAccessMode.Local ? "Green" : "Blue";
        }

        // Server Management Commands

        [RelayCommand]
        private async Task StartWebServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                WebServerStatus = "Starting...";
                var result = await _serverManagement.StartWebServerAsync(WebServerPort);

                if (result.Success)
                {
                    ActualWebPort = result.ActualPort;
                    WebServerStatus = $"Running on port {result.ActualPort}";
                    UpdateLocalIpAddresses();

                    if (AutoOpenBrowser && result.ActualPort > 0)
                    {
                        _browserService.OpenUrl($"http://localhost:{result.ActualPort}");
                    }
                }
                else
                {
                    WebServerStatus = $"Failed: {result.ErrorMessage}";
                }
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task StopWebServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                WebServerStatus = "Stopping...";
                await _serverManagement.StopWebServerAsync();
                WebServerStatus = "Stopped";
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task StartApiServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                ApiServerStatus = "Starting...";
                var result = await _serverManagement.StartApiServerAsync(ApiServerPort);

                if (result.Success)
                {
                    ActualApiPort = result.ActualPort;
                    ApiServerStatus = $"Running on port {result.ActualPort}";
                }
                else
                {
                    ApiServerStatus = $"Failed: {result.ErrorMessage}";
                }
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task StopApiServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                ApiServerStatus = "Stopping...";
                await _serverManagement.StopApiServerAsync();
                ApiServerStatus = "Stopped";
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private void OpenWebBrowser()
        {
            var port = IsWebRunning ? ActualWebPort : WebServerPort;
            _browserService.OpenUrl($"http://localhost:{port}");
        }

        [RelayCommand]
        private void RefreshServerLogs()
        {
            var webLogs = _serverManagement.GetWebServerLogs();
            var apiLogs = _serverManagement.GetApiServerLogs();

            WebServerLogs = string.Join(Environment.NewLine, webLogs);
            ApiServerLogs = string.Join(Environment.NewLine, apiLogs);
        }

        [RelayCommand]
        private void ClearWebLogs()
        {
            _serverManagement.ClearWebServerLogs();
            WebServerLogs = string.Empty;
        }

        [RelayCommand]
        private void ClearApiLogs()
        {
            _serverManagement.ClearApiServerLogs();
            ApiServerLogs = string.Empty;
        }

        private void UpdateServerStatus()
        {
            var status = _serverManagement.GetServerStatus();

            IsWebRunning = status.IsWebRunning;
            IsApiRunning = status.IsApiRunning;

            // Phase 5.10 fix — when an explicit user Start/Stop command is mid-flight,
            // skip the status-message overwrite. The command sets its own success/failure
            // message; the periodic Timer must not clobber it. Without this gate,
            // IsWebRunning=false (lagging) caused "Failed: port in use" to be replaced
            // by "Stopped" within ~2 seconds.
            if (!_explicitOperationInProgress)
            {
                if (status.IsWebRunning)
                {
                    ActualWebPort = status.WebPort;
                    WebServerStatus = $"Running on port {status.WebPort}";
                }
                else if (WebServerStatus != "Starting..." && WebServerStatus != "Stopping...")
                {
                    WebServerStatus = "Stopped";
                }

                if (status.IsApiRunning)
                {
                    ActualApiPort = status.ApiPort;
                    ApiServerStatus = $"Running on port {status.ApiPort}";
                }
                else if (ApiServerStatus != "Starting..." && ApiServerStatus != "Stopping...")
                {
                    ApiServerStatus = "Stopped";
                }
            }

            // Refresh logs if servers are running
            if (IsWebRunning || IsApiRunning)
            {
                RefreshServerLogs();
            }
        }

        private void UpdateLocalIpAddresses()
        {
            // Phase 5c — delegated to INetworkAddressProvider so the testable parts of
            // the network/QR logic live outside the VM. The VM's only remaining job is
            // to apply the provider's snapshot to its bindable [ObservableProperty]
            // fields (XAML/test contract preserved).
            var info = _networkAddresses.GetCurrent(ActualWebPort, IsWebRunning);

            LocalNetworkIp = info.LocalNetworkIp;
            TailscaleIp = info.TailscaleIp;
            IsLocalNetworkAvailable = info.IsLocalNetworkAvailable;
            IsTailscaleAvailable = info.IsTailscaleAvailable;
            LocalNetworkStatus = info.LocalNetworkStatus;
            TailscaleStatus = info.TailscaleStatus;
            LocalNetworkUrl = info.LocalNetworkUrl;
            TailscaleUrl = info.TailscaleUrl;
            LocalQrCodeBitmap = info.LocalQrCodeBitmap;
            TailscaleQrCodeBitmap = info.TailscaleQrCodeBitmap;
            LocalIpAddresses = info.LegacyLocalIpAddresses;
            QrCodeBitmap = info.LegacyQrCodeBitmap;
        }

        [RelayCommand]
        private void OpenTailscaleGuide()
        {
            // Detect OS and open appropriate guide
            var guidePath = OperatingSystem.IsWindows() ? "Tailscale-Setup-Windows.md"
                : OperatingSystem.IsMacOS() ? "Tailscale-Setup-Mac.md"
                : "Tailscale-Setup-Linux.md";

            // Try to open local documentation first
            var docsPath = Path.Combine(AppContext.BaseDirectory, "Docs", guidePath);
            if (File.Exists(docsPath))
            {
                _browserService.OpenUrl(docsPath);
            }
            else
            {
                // Fall back to Tailscale download page
                _browserService.OpenUrl("https://tailscale.com/download");
            }
        }

        [RelayCommand]
        private void RefreshNetworkStatus()
        {
            UpdateLocalIpAddresses();
        }

        public void Dispose()
        {
            _statusRefreshTimer?.Dispose();
        }
    }
}
