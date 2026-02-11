using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IBrowserService _browserService;
        private readonly IServiceProvider _services;

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

        public SettingsViewModel(ISettingsService settingsService, IBrowserService browserService, IServiceProvider services)
        {
            _settingsService = settingsService;
            _browserService = browserService;
            _services = services;

            LoadSettings();
            LoadCardCountAsync();
            UpdateDataAccessMode();
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
                SyncServerUrl = SyncServerUrl
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
    }
}
