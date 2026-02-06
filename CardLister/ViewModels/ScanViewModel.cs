using System;
using System.Linq;
using System.Threading.Tasks;
using CardLister.Models;
using CardLister.Models.Enums;
using CardLister.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardLister.ViewModels
{
    public partial class ScanViewModel : ViewModelBase
    {
        private readonly IScannerService _scannerService;
        private readonly ICardRepository _cardRepository;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsService _settingsService;
        private readonly IVariationVerifier _variationVerifier;
        private readonly IChecklistLearningService _checklistLearningService;

        private ScanResult? _lastScanResult;

        [ObservableProperty] private string? _imagePath;
        [ObservableProperty] private string? _imagePathBack;
        [ObservableProperty] private CardDetailViewModel? _scannedCard;
        [ObservableProperty] private bool _isScanning;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private VerificationResult? _verificationResult;
        [ObservableProperty] private bool _isVerifying;
        [ObservableProperty] private string _verificationStatus = "";

        public ScanViewModel(
            IScannerService scannerService,
            ICardRepository cardRepository,
            IFileDialogService fileDialogService,
            ISettingsService settingsService,
            IVariationVerifier variationVerifier,
            IChecklistLearningService checklistLearningService)
        {
            _scannerService = scannerService;
            _cardRepository = cardRepository;
            _fileDialogService = fileDialogService;
            _settingsService = settingsService;
            _variationVerifier = variationVerifier;
            _checklistLearningService = checklistLearningService;
        }

        [RelayCommand]
        private async Task BrowseImageAsync()
        {
            var path = await _fileDialogService.OpenImageFileAsync();
            if (path != null)
            {
                ImagePath = path;
                ErrorMessage = null;
                SuccessMessage = null;
            }
        }

        [RelayCommand]
        private async Task BrowseBackImageAsync()
        {
            var path = await _fileDialogService.OpenImageFileAsync();
            if (path != null)
            {
                ImagePathBack = path;
            }
        }

        [RelayCommand]
        private void RemoveBackImage()
        {
            ImagePathBack = null;
        }

        [RelayCommand]
        private async Task ScanCardAsync()
        {
            if (string.IsNullOrEmpty(ImagePath))
                return;

            IsScanning = true;
            ErrorMessage = null;
            SuccessMessage = null;
            VerificationResult = null;
            VerificationStatus = "";

            try
            {
                var settings = _settingsService.Load();
                var scanResult = await _scannerService.ScanCardAsync(ImagePath, ImagePathBack, settings.DefaultModel);
                scanResult.Card.ImagePathFront = ImagePath;
                if (!string.IsNullOrEmpty(ImagePathBack))
                    scanResult.Card.ImagePathBack = ImagePathBack;
                _lastScanResult = scanResult;
                ScannedCard = CardDetailViewModel.FromCard(scanResult.Card);

                // Run verification pipeline if enabled
                if (settings.EnableVariationVerification)
                {
                    IsVerifying = true;
                    VerificationStatus = "Verifying against checklist...";

                    try
                    {
                        var verification = await _variationVerifier.VerifyCardAsync(scanResult, ImagePath);

                        // Run confirmation pass if needed and enabled
                        if (settings.RunConfirmationPass && _variationVerifier.NeedsConfirmationPass(verification))
                        {
                            VerificationStatus = "Running confirmation pass...";
                            verification = await _variationVerifier.RunConfirmationPassAsync(scanResult, verification, ImagePath);
                        }

                        VerificationResult = verification;

                        // Auto-apply high-confidence suggestions if enabled
                        if (settings.AutoApplyHighConfidenceSuggestions && ScannedCard != null)
                        {
                            if (verification.SuggestedPlayerName != null &&
                                verification.PlayerVerified == false &&
                                verification.FieldConfidences.Any(f =>
                                    f.FieldName == "player_name" &&
                                    f.Confidence == VerificationConfidence.Conflict))
                            {
                                ScannedCard.PlayerName = verification.SuggestedPlayerName;
                            }

                            if (verification.SuggestedVariation != null &&
                                verification.OverallConfidence != VerificationConfidence.Conflict)
                            {
                                ScannedCard.ParallelName = verification.SuggestedVariation;
                            }
                        }

                        VerificationStatus = verification.OverallConfidence switch
                        {
                            VerificationConfidence.High => "Verified",
                            VerificationConfidence.Medium => "Partially verified",
                            VerificationConfidence.Low => "Unverified",
                            VerificationConfidence.Conflict => "Conflicts detected",
                            _ => ""
                        };
                    }
                    catch (Exception ex)
                    {
                        VerificationStatus = $"Verification error: {ex.Message}";
                    }
                    finally
                    {
                        IsVerifying = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private void AcceptSuggestion(string suggestion)
        {
            if (VerificationResult == null || ScannedCard == null)
                return;

            if (VerificationResult.SuggestedPlayerName != null &&
                suggestion.Contains("Player name", StringComparison.OrdinalIgnoreCase))
            {
                ScannedCard.PlayerName = VerificationResult.SuggestedPlayerName;
            }

            if (VerificationResult.SuggestedVariation != null &&
                (suggestion.Contains("parallel", StringComparison.OrdinalIgnoreCase) ||
                 suggestion.Contains("variation", StringComparison.OrdinalIgnoreCase) ||
                 suggestion.Contains("did you mean", StringComparison.OrdinalIgnoreCase)))
            {
                ScannedCard.ParallelName = VerificationResult.SuggestedVariation;
            }

            if (suggestion.Contains("rookie", StringComparison.OrdinalIgnoreCase))
            {
                ScannedCard.IsRookie = true;
            }

            if (suggestion.Contains("auto", StringComparison.OrdinalIgnoreCase) &&
                suggestion.Contains("autograph", StringComparison.OrdinalIgnoreCase))
            {
                ScannedCard.IsAuto = true;
            }

            if (suggestion.Contains("relic", StringComparison.OrdinalIgnoreCase) ||
                suggestion.Contains("memorabilia", StringComparison.OrdinalIgnoreCase))
            {
                ScannedCard.IsRelic = true;
            }

            VerificationResult.Suggestions.Remove(suggestion);
            OnPropertyChanged(nameof(VerificationResult));
        }

        [RelayCommand]
        private void IgnoreSuggestion(string suggestion)
        {
            if (VerificationResult == null)
                return;

            VerificationResult.Suggestions.Remove(suggestion);
            OnPropertyChanged(nameof(VerificationResult));
        }

        [RelayCommand]
        private async Task SaveCardAsync()
        {
            if (ScannedCard == null)
                return;

            ErrorMessage = null;

            try
            {
                var card = ScannedCard.ToCard();
                card.ImagePathFront = ImagePath;
                card.ImagePathBack = ImagePathBack;
                card.Status = Models.Enums.CardStatus.Draft;
                await _cardRepository.InsertCardAsync(card);

                // Learn from saved card (fire-and-forget)
                _ = _checklistLearningService.LearnFromCardAsync(card);

                SuccessMessage = $"Saved {card.PlayerName} to My Cards!";
                Clear();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void EnterManually()
        {
            ScannedCard = new CardDetailViewModel();
            ErrorMessage = null;
            SuccessMessage = null;
            VerificationResult = null;
            VerificationStatus = "";
        }

        [RelayCommand]
        private void Clear()
        {
            ImagePath = null;
            ImagePathBack = null;
            ScannedCard = null;
            ErrorMessage = null;
            VerificationResult = null;
            VerificationStatus = "";
            _lastScanResult = null;
        }
    }
}
