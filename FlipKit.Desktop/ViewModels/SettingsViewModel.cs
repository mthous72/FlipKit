using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IBrowserService _browserService;
        private readonly IServiceProvider _services;
        private readonly IServerManagementService _serverManagement;
        private Timer? _statusRefreshTimer;

        // API Keys
        [ObservableProperty] private string _openRouterApiKey = string.Empty;
        [ObservableProperty] private string _imgBBApiKey = string.Empty;
        [ObservableProperty] private string _openRouterStatus = "Not configured";
        [ObservableProperty] private string _imgBBStatus = "Not configured";
        [ObservableProperty] private bool _isTestingOpenRouter;
        [ObservableProperty] private bool _isTestingImgBB;

        // Preferences
        [ObservableProperty] private bool _isEbaySeller;
        [ObservableProperty] private string _defaultShippingProfile = "4 oz";
        [ObservableProperty] private string _defaultCondition = "Near Mint";
        [ObservableProperty] private string _defaultModel = "google/gemma-3-27b-it:free";

        public List<string> ModelOptions { get; } = new(OpenRouterScannerService.AllVisionModels);

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

        public SettingsViewModel(ISettingsService settingsService, IBrowserService browserService,
            IServiceProvider services, IServerManagementService serverManagement)
        {
            _settingsService = settingsService;
            _browserService = browserService;
            _services = services;
            _serverManagement = serverManagement;

            LoadSettings();
            LoadCardCountAsync();
            UpdateDataAccessMode();
            UpdateServerStatus();
            UpdateLocalIpAddresses();

            // Refresh server status every 2 seconds
            _statusRefreshTimer = new Timer(_ =>
            {
                UpdateServerStatus();
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        private void LoadSettings()
        {
            var s = _settingsService.Load();

            OpenRouterApiKey = s.OpenRouterApiKey ?? string.Empty;
            ImgBBApiKey = s.ImgBBApiKey ?? string.Empty;
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

            UpdateServerStatus();
        }

        [RelayCommand]
        private async Task StopWebServerAsync()
        {
            WebServerStatus = "Stopping...";
            await _serverManagement.StopWebServerAsync();
            WebServerStatus = "Stopped";
            UpdateServerStatus();
        }

        [RelayCommand]
        private async Task StartApiServerAsync()
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

            UpdateServerStatus();
        }

        [RelayCommand]
        private async Task StopApiServerAsync()
        {
            ApiServerStatus = "Stopping...";
            await _serverManagement.StopApiServerAsync();
            ApiServerStatus = "Stopped";
            UpdateServerStatus();
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

            // Refresh logs if servers are running
            if (IsWebRunning || IsApiRunning)
            {
                RefreshServerLogs();
            }
        }

        private void UpdateLocalIpAddresses()
        {
            try
            {
                var localIPs = new List<string>();
                var tailscaleIPs = new List<string>();

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !ip.Address.ToString().StartsWith("127."))
                            {
                                var ipStr = ip.Address.ToString();

                                // Detect Tailscale IPs (100.64.0.0/10 CGNAT range)
                                if (ipStr.StartsWith("100."))
                                {
                                    tailscaleIPs.Add(ipStr);
                                }
                                // Regular network interfaces
                                else if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                         ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                                {
                                    localIPs.Add(ipStr);
                                }
                            }
                        }
                    }
                }

                if (IsWebRunning)
                {
                    string primaryIp;
                    string networkType;
                    string additionalInfo = "";

                    // Prioritize Tailscale if available
                    if (tailscaleIPs.Count > 0)
                    {
                        primaryIp = tailscaleIPs[0];
                        networkType = "🌐 Tailscale (Remote Access)";

                        if (localIPs.Count > 0)
                        {
                            var localIp = localIPs.FirstOrDefault(ip => ip.StartsWith("192.168.")) ?? localIPs[0];
                            additionalInfo = $"\n📱 Local Network: http://{localIp}:{ActualWebPort}";
                        }
                    }
                    else if (localIPs.Count > 0)
                    {
                        primaryIp = localIPs.FirstOrDefault(ip => ip.StartsWith("192.168.")) ?? localIPs[0];
                        networkType = "📱 Local Network";
                    }
                    else
                    {
                        LocalIpAddresses = "No network connection";
                        QrCodeBitmap = null;
                        return;
                    }

                    var url = $"http://{primaryIp}:{ActualWebPort}";
                    LocalIpAddresses = $"{networkType}\n{url}{additionalInfo}";

                    // Generate QR code for the primary URL
                    GenerateQrCode(url);
                }
                else
                {
                    if (tailscaleIPs.Count > 0)
                    {
                        LocalIpAddresses = $"🌐 Tailscale IP: {tailscaleIPs[0]}\n(Web server not running)";
                    }
                    else if (localIPs.Count > 0)
                    {
                        var localIp = localIPs.FirstOrDefault(ip => ip.StartsWith("192.168.")) ?? localIPs[0];
                        LocalIpAddresses = $"📱 Local IP: {localIp}\n(Web server not running)";
                    }
                    else
                    {
                        LocalIpAddresses = "No network connection";
                    }

                    QrCodeBitmap = null;
                }
            }
            catch (Exception ex)
            {
                LocalIpAddresses = $"Error detecting network: {ex.Message}";
                QrCodeBitmap = null;
            }
        }

        private void GenerateQrCode(string url)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20); // 20 pixels per module

                // Convert byte array to Avalonia Bitmap
                using var stream = new MemoryStream(qrCodeImage);
                QrCodeBitmap = new Bitmap(stream);
            }
            catch (Exception)
            {
                // Failed to generate QR code - silently fail and leave QR code as null
                QrCodeBitmap = null;
            }
        }

        public void Dispose()
        {
            _statusRefreshTimer?.Dispose();
        }
    }
}
