using System;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SetupWizardViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IBrowserService _browserService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowBack))]
        [NotifyPropertyChangedFor(nameof(ShowNext))]
        [NotifyPropertyChangedFor(nameof(ShowFinish))]
        private int _currentStep = 1;

        public bool ShowBack => CurrentStep > 1;
        public bool ShowNext => CurrentStep < 3;
        public bool ShowFinish => CurrentStep == 3;

        // Step 1 — OpenRouter
        [ObservableProperty] private string _openRouterApiKey = string.Empty;
        [ObservableProperty] private string _openRouterStatus = string.Empty;
        [ObservableProperty] private bool _isTestingOpenRouter;
        [ObservableProperty] private bool _openRouterValid;

        // Step 2 — ImgBB
        [ObservableProperty] private string _imgBBApiKey = string.Empty;
        [ObservableProperty] private string _imgBBStatus = string.Empty;
        [ObservableProperty] private bool _isTestingImgBB;
        [ObservableProperty] private bool _imgBBValid;

        // Step 3 — Preferences
        [ObservableProperty] private bool _isEbaySeller;
        [ObservableProperty] private string _defaultShippingProfile = "4 oz";
        [ObservableProperty] private string _defaultCondition = "Near Mint";

        public Action? OnSetupComplete { get; set; }

        public SetupWizardViewModel(ISettingsService settingsService, IBrowserService browserService)
        {
            _settingsService = settingsService;
            _browserService = browserService;
        }

        [RelayCommand]
        private void Next()
        {
            if (CurrentStep < 3)
                CurrentStep++;
        }

        [RelayCommand]
        private void Back()
        {
            if (CurrentStep > 1)
                CurrentStep--;
        }

        [RelayCommand]
        private void Finish()
        {
            var settings = new AppSettings
            {
                OpenRouterApiKey = OpenRouterApiKey,
                ImgBBApiKey = ImgBBApiKey,
                IsEbaySeller = IsEbaySeller,
                DefaultShippingProfile = DefaultShippingProfile,
                DefaultCondition = DefaultCondition
            };

            _settingsService.Save(settings);
            OnSetupComplete?.Invoke();
        }

        [RelayCommand]
        private async Task TestOpenRouterAsync()
        {
            IsTestingOpenRouter = true;
            OpenRouterStatus = "Testing...";

            OpenRouterValid = await _settingsService.TestOpenRouterConnectionAsync(OpenRouterApiKey);
            OpenRouterStatus = OpenRouterValid ? "Connected!" : "Connection failed. Check your key.";

            IsTestingOpenRouter = false;
        }

        [RelayCommand]
        private async Task TestImgBBAsync()
        {
            IsTestingImgBB = true;
            ImgBBStatus = "Testing...";

            ImgBBValid = await _settingsService.TestImgBBConnectionAsync(ImgBBApiKey);
            ImgBBStatus = ImgBBValid ? "Connected!" : "Connection failed. Check your key.";

            IsTestingImgBB = false;
        }

        [RelayCommand]
        private void OpenOpenRouterSignup()
        {
            _browserService.OpenUrl("https://openrouter.ai/keys");
        }

        [RelayCommand]
        private void OpenImgBBSignup()
        {
            _browserService.OpenUrl("https://api.imgbb.com/");
        }
    }
}
