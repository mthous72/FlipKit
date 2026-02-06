using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CardLister.Data;
using CardLister.Models;
using CardLister.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardLister.ViewModels
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
        [ObservableProperty] private string _defaultModel = "nvidia/nemotron-nano-12b-v2-vl:free";

        public List<string> ModelOptions { get; } = new(OpenRouterScannerService.FreeVisionModels);

        // Card Scanning
        [ObservableProperty] private bool _enableVariationVerification = true;
        [ObservableProperty] private bool _autoApplyHighConfidenceSuggestions = true;
        [ObservableProperty] private bool _runConfirmationPass = true;
        [ObservableProperty] private bool _enableChecklistLearning = true;

        // Financial
        [ObservableProperty] private decimal _whatnotFeePercent = 11.0m;
        [ObservableProperty] private decimal _ebayFeePercent = 13.25m;
        [ObservableProperty] private decimal _defaultShippingCostPwe = 1.00m;
        [ObservableProperty] private decimal _defaultShippingCostBmwt = 4.50m;
        [ObservableProperty] private int _priceStalenessThresholdDays = 30;

        // Data Info
        [ObservableProperty] private int _cardCount;
        [ObservableProperty] private string _dbPath = string.Empty;

        // Save feedback
        [ObservableProperty] private string _saveMessage = string.Empty;

        public SettingsViewModel(ISettingsService settingsService, IBrowserService browserService, IServiceProvider services)
        {
            _settingsService = settingsService;
            _browserService = browserService;
            _services = services;

            LoadSettings();
            LoadCardCountAsync();
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
            WhatnotFeePercent = s.WhatnotFeePercent;
            EbayFeePercent = s.EbayFeePercent;
            DefaultShippingCostPwe = s.DefaultShippingCostPwe;
            DefaultShippingCostBmwt = s.DefaultShippingCostBmwt;
            PriceStalenessThresholdDays = s.PriceStalenessThresholdDays;

            OpenRouterStatus = string.IsNullOrWhiteSpace(OpenRouterApiKey) ? "Not configured" : "Configured (not tested)";
            ImgBBStatus = string.IsNullOrWhiteSpace(ImgBBApiKey) ? "Not configured" : "Configured (not tested)";

            DbPath = CardListerDbContext.GetDbPath();
        }

        private async void LoadCardCountAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CardListerDbContext>();
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
                WhatnotFeePercent = WhatnotFeePercent,
                EbayFeePercent = EbayFeePercent,
                DefaultShippingCostPwe = DefaultShippingCostPwe,
                DefaultShippingCostBmwt = DefaultShippingCostBmwt,
                PriceStalenessThresholdDays = PriceStalenessThresholdDays
            };

            _settingsService.Save(s);
            SaveMessage = "Settings saved!";
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
    }
}
