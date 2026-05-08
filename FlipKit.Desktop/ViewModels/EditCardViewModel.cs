using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    public partial class EditCardViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly INavigationService _navigationService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IImageUploadService _imageUploadService;
        private readonly IWebcamCaptureDialogService _webcamCaptureDialog;
        private readonly ISettingsService _settingsService;
        private readonly IScannerService _scannerService;
        private readonly Services.IPaidScanGate _paidScanGate;
        private readonly Services.IAppNotificationService? _notificationService;
        private readonly IPlayerNameDirectory? _playerDirectory;
        private readonly ILogger<EditCardViewModel> _logger;

        private Card? _originalCard;

        [ObservableProperty] private CardDetailViewModel? _cardDetail;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private bool _isLoading;

        // Image previews from the original card
        [ObservableProperty] private string? _imagePathFront;
        [ObservableProperty] private string? _imagePathBack;
        [ObservableProperty] private string? _imageUrl1;
        [ObservableProperty] private string? _imageUrl2;

        // Phase 2 — surface the saved card's tier outcome in the editor so users can see
        // at a glance which checklist tier the card was committed under.
        [ObservableProperty] private string _tierBadgeText = string.Empty;
        [ObservableProperty] private string _tierBadgeColor = "#9E9E9E";

        // Enhance feature
        [ObservableProperty] private bool _isEnhancing;
        [ObservableProperty] private string? _enhanceMessage;

        public bool IsOcrSourced => _originalCard?.DataSource == CardDataSource.Ocr;

        // Webcam capture toggle — bound to the 📷 buttons' IsVisible.
        [ObservableProperty] private bool _isWebcamEnabled = true;

        // Prefer ImgBB URLs (used for Whatnot) over local paths
        public string? DisplayImageFront => !string.IsNullOrEmpty(ImageUrl1) ? ImageUrl1 : ImagePathFront;
        public string? DisplayImageBack => !string.IsNullOrEmpty(ImageUrl2) ? ImageUrl2 : ImagePathBack;

        // Additional photos (slots 3-8) — uploaded to ImgBB at export time but never sent to the LLM.
        public ObservableCollection<PhotoSlot> AdditionalPhotos { get; } = new();
        public const int MaxAdditionalPhotos = 6;

        public EditCardViewModel(
            ICardRepository cardRepository,
            INavigationService navigationService,
            IFileDialogService fileDialogService,
            IImageUploadService imageUploadService,
            IWebcamCaptureDialogService webcamCaptureDialog,
            ISettingsService settingsService,
            IScannerService scannerService,
            Services.IPaidScanGate paidScanGate,
            ILogger<EditCardViewModel> logger,
            IPlayerNameDirectory? playerDirectory = null,
            // Optional so existing test fixtures don't have to wire it up.
            Services.IAppNotificationService? notificationService = null)
        {
            _cardRepository = cardRepository;
            _navigationService = navigationService;
            _fileDialogService = fileDialogService;
            _imageUploadService = imageUploadService;
            _webcamCaptureDialog = webcamCaptureDialog;
            _settingsService = settingsService;
            _scannerService = scannerService;
            _paidScanGate = paidScanGate;
            _notificationService = notificationService;
            _playerDirectory = playerDirectory;
            _logger = logger;

            IsWebcamEnabled = settingsService.Load().WebcamCaptureEnabled;
        }

        public async Task LoadCardAsync(int cardId)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _originalCard = await _cardRepository.GetCardAsync(cardId);
                if (_originalCard == null)
                {
                    ErrorMessage = "Card not found.";
                    return;
                }

                CardDetail = CardDetailViewModel.FromCard(_originalCard);
                ImagePathFront = _originalCard.ImagePathFront;
                ImagePathBack = _originalCard.ImagePathBack;
                ImageUrl1 = _originalCard.ImageUrl1;
                ImageUrl2 = _originalCard.ImageUrl2;
                ApplyTierBadge(_originalCard);
                OnPropertyChanged(nameof(IsOcrSourced));

                AdditionalPhotos.Clear();
                AddSlotIfAny(_originalCard.ImagePath3, _originalCard.ImageUrl3);
                AddSlotIfAny(_originalCard.ImagePath4, _originalCard.ImageUrl4);
                AddSlotIfAny(_originalCard.ImagePath5, _originalCard.ImageUrl5);
                AddSlotIfAny(_originalCard.ImagePath6, _originalCard.ImageUrl6);
                AddSlotIfAny(_originalCard.ImagePath7, _originalCard.ImageUrl7);
                AddSlotIfAny(_originalCard.ImagePath8, _originalCard.ImageUrl8);

                // Notify that display properties changed
                OnPropertyChanged(nameof(DisplayImageFront));
                OnPropertyChanged(nameof(DisplayImageBack));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load card {CardId} for editing", cardId);
                ErrorMessage = $"Failed to load card: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddAdditionalPhotoAsync()
        {
            if (AdditionalPhotos.Count >= MaxAdditionalPhotos)
                return;

            var path = await _fileDialogService.OpenImageFileAsync();
            if (!string.IsNullOrEmpty(path))
                AdditionalPhotos.Add(new PhotoSlot(path));
        }

        [RelayCommand]
        private async Task CaptureAdditionalPhotoAsync()
        {
            if (AdditionalPhotos.Count >= MaxAdditionalPhotos)
                return;

            var path = await _webcamCaptureDialog.CaptureAsync();
            if (!string.IsNullOrEmpty(path))
                AdditionalPhotos.Add(new PhotoSlot(path));
        }

        [RelayCommand]
        private async Task ReplaceFrontImageBrowseAsync()
        {
            var path = await _fileDialogService.OpenImageFileAsync();
            if (!string.IsNullOrEmpty(path))
                ApplyNewFrontImage(path);
        }

        [RelayCommand]
        private async Task ReplaceFrontImageWebcamAsync()
        {
            var path = await _webcamCaptureDialog.CaptureAsync();
            if (!string.IsNullOrEmpty(path))
                ApplyNewFrontImage(path);
        }

        [RelayCommand]
        private async Task ReplaceBackImageBrowseAsync()
        {
            var path = await _fileDialogService.OpenImageFileAsync();
            if (!string.IsNullOrEmpty(path))
                ApplyNewBackImage(path);
        }

        [RelayCommand]
        private async Task ReplaceBackImageWebcamAsync()
        {
            var path = await _webcamCaptureDialog.CaptureAsync();
            if (!string.IsNullOrEmpty(path))
                ApplyNewBackImage(path);
        }

        private void ApplyNewFrontImage(string path)
        {
            ImagePathFront = path;
            // Clear the hosted URL so DisplayImageFront falls back to the new local file
            // instead of showing the stale ImgBB image. SaveAsync's TryUploadMissingUrlsAsync
            // will re-upload and repopulate ImageUrl1.
            ImageUrl1 = null;
            OnPropertyChanged(nameof(DisplayImageFront));
        }

        private void ApplyNewBackImage(string path)
        {
            ImagePathBack = path;
            ImageUrl2 = null;
            OnPropertyChanged(nameof(DisplayImageBack));
        }

        [RelayCommand]
        private void RemoveAdditionalPhoto(PhotoSlot? slot)
        {
            if (slot != null)
                AdditionalPhotos.Remove(slot);
        }

        private void AddSlotIfAny(string? path, string? url)
        {
            if (!string.IsNullOrEmpty(path) || !string.IsNullOrEmpty(url))
                AdditionalPhotos.Add(new PhotoSlot(path, url));
        }

        private void ApplyTierBadge(Card card)
        {
            switch (card.VerificationStatus)
            {
                case Core.Models.Enums.VerificationStatus.Verified:
                    TierBadgeText = "✓ Verified against checklist";
                    TierBadgeColor = "#43A047";
                    break;
                case Core.Models.Enums.VerificationStatus.BestGuess:
                    TierBadgeText = "⚠ Best guess (Tier 2)";
                    TierBadgeColor = "#FB8C00";
                    break;
                case Core.Models.Enums.VerificationStatus.UserCorrected:
                    TierBadgeText = "✎ User corrected from picker";
                    TierBadgeColor = "#1E88E5";
                    break;
                case Core.Models.Enums.VerificationStatus.NoMatchFound:
                    TierBadgeText = "❓ No match — saved with AI guess";
                    TierBadgeColor = "#E53935";
                    break;
                default:
                    TierBadgeText = string.Empty;
                    TierBadgeColor = "#9E9E9E";
                    break;
            }
        }

        [RelayCommand]
        private async Task EnhanceAsync()
        {
            if (_originalCard == null || CardDetail == null)
                return;

            var frontPath = _originalCard.ImagePathFront ?? ImagePathFront;
            if (string.IsNullOrEmpty(frontPath) || !File.Exists(frontPath))
            {
                ErrorMessage = "Cannot enhance: front image file not found.";
                return;
            }

            IsEnhancing = true;
            EnhanceMessage = null;
            ErrorMessage = null;

            try
            {
                // Reconstruct verified-fields hint from the saved Card by
                // re-querying the directory. Falls back to the legacy 6-field
                // soft hint when the directory isn't ready (fresh install,
                // pre-seed). Cards that weren't OCR-sourced still benefit —
                // any directory-anchored field gets locked.
                OcrHint? hint = null;
                if (_playerDirectory?.IsReady == true)
                {
                    hint = _playerDirectory.BuildHintFromCard(CardDetail.ToCard());
                }
                else if (_originalCard.DataSource == CardDataSource.Ocr)
                {
                    hint = new OcrHint
                    {
                        PlayerName = CardDetail.PlayerName,
                        Year = CardDetail.Year,
                        CardNumber = CardDetail.CardNumber,
                        Manufacturer = CardDetail.Manufacturer,
                        Brand = CardDetail.Brand,
                        SetName = CardDetail.SetName,
                    };
                }

                var settings = _settingsService.Load();
                // Resolve the UI's "auto" sentinel, then gate paid models through
                // the picker. Free models pass through silently.
                var resolved = OpenRouterModelDefaults.ResolveModelId(settings.DefaultModel);
                var gated = await _paidScanGate.GateAsync(
                    resolved,
                    "About to enhance this card using a paid model. Pick which paid model to use, or cancel.");
                if (gated == null)
                {
                    IsEnhancing = false;
                    EnhanceMessage = "Enhance cancelled — no paid model used.";
                    return;
                }
                var model = gated;

                var result = await _scannerService.ScanCardAsync(
                    frontPath,
                    _originalCard.ImagePathBack ?? ImagePathBack,
                    model,
                    scanDepth: ScanDepth.Standard,
                    ocrHint: hint);

                var e = result.Card;
                CardDetail.PlayerName = e.PlayerName ?? CardDetail.PlayerName;
                CardDetail.Year = e.Year ?? CardDetail.Year;
                CardDetail.Manufacturer = e.Manufacturer ?? CardDetail.Manufacturer;
                CardDetail.Brand = e.Brand ?? CardDetail.Brand;
                CardDetail.SetName = e.SetName ?? CardDetail.SetName;
                CardDetail.CardNumber = e.CardNumber ?? CardDetail.CardNumber;
                CardDetail.Team = e.Team ?? CardDetail.Team;
                CardDetail.VariationType = e.VariationType ?? CardDetail.VariationType;
                CardDetail.ParallelName = e.ParallelName ?? CardDetail.ParallelName;
                CardDetail.SerialNumbered = e.SerialNumbered ?? CardDetail.SerialNumbered;
                CardDetail.IsRookie = e.IsRookie;
                CardDetail.IsAuto = e.IsAuto;
                CardDetail.IsRelic = e.IsRelic;
                CardDetail.IsGraded = e.IsGraded;
                CardDetail.GradeCompany = e.GradeCompany ?? CardDetail.GradeCompany;
                CardDetail.GradeValue = e.GradeValue ?? CardDetail.GradeValue;
                if (e.Sport.HasValue) CardDetail.Sport = e.Sport;

                _originalCard.DataSource = CardDataSource.Ai;
                OnPropertyChanged(nameof(IsOcrSourced));

                EnhanceMessage = "Enhanced with AI — review fields and save.";
                _logger.LogInformation("Enhanced card {CardId} with AI", _originalCard.Id);
            }
            catch (OpenRouterPaymentRequiredException pEx)
            {
                _logger.LogError(pEx, "Payment Required during edit-card enhance");
                ErrorMessage = pEx.Message;
                _notificationService?.NotifyPaymentRequired(pEx.ModelId, pEx.ResponseBody);
            }
            catch (OpenRouterRateLimitException rlEx)
            {
                _logger.LogError(rlEx, "Rate limit during edit-card enhance");
                ErrorMessage = rlEx.Message;
                _notificationService?.NotifyRateLimit(rlEx.ModelId, rlEx.Scope, rlEx.RetryAfterSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enhance failed for card {CardId}", _originalCard?.Id);
                ErrorMessage = $"Enhance failed: {ex.Message}";
            }
            finally
            {
                IsEnhancing = false;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (CardDetail == null || _originalCard == null)
                return;

            try
            {
                ErrorMessage = null;
                SuccessMessage = null;

                // Update the tracked entity's properties instead of creating a new instance
                _originalCard.PlayerName = CardDetail.PlayerName ?? string.Empty;
                _originalCard.Sport = CardDetail.Sport;
                _originalCard.Brand = CardDetail.Brand;
                _originalCard.Manufacturer = CardDetail.Manufacturer;
                _originalCard.Year = CardDetail.Year;
                _originalCard.CardNumber = CardDetail.CardNumber;
                _originalCard.Team = CardDetail.Team;
                _originalCard.SetName = CardDetail.SetName;
                _originalCard.VariationType = CardDetail.VariationType;
                _originalCard.ParallelName = CardDetail.ParallelName;
                _originalCard.SerialNumbered = CardDetail.SerialNumbered;
                _originalCard.IsShortPrint = CardDetail.IsShortPrint;
                _originalCard.IsSSP = CardDetail.IsSSP;
                _originalCard.IsRookie = CardDetail.IsRookie;
                _originalCard.IsAuto = CardDetail.IsAuto;
                _originalCard.IsRelic = CardDetail.IsRelic;
                _originalCard.Condition = CardDetail.Condition;
                _originalCard.IsGraded = CardDetail.IsGraded;
                _originalCard.GradeCompany = CardDetail.GradeCompany;
                _originalCard.GradeValue = CardDetail.GradeValue;
                _originalCard.CertNumber = CardDetail.CertNumber;
                _originalCard.AutoGrade = CardDetail.AutoGrade;
                _originalCard.CostBasis = CardDetail.CostBasis;
                _originalCard.CostSource = CardDetail.CostSource;
                _originalCard.CostDate = CardDetail.CostDate;
                _originalCard.CostNotes = CardDetail.CostNotes;
                _originalCard.Quantity = CardDetail.Quantity;
                _originalCard.ListingType = CardDetail.ListingType;
                _originalCard.Offerable = CardDetail.Offerable;
                _originalCard.ShippingProfile = CardDetail.ShippingProfile;
                _originalCard.WhatnotCategory = CardDetail.WhatnotCategory;
                _originalCard.WhatnotSubcategory = CardDetail.WhatnotSubcategory;
                _originalCard.Notes = CardDetail.Notes;
                _originalCard.UpdatedAt = DateTime.UtcNow;

                // Front/back image swaps from the new Replace flow. We also clear the
                // hosted URL when the local path changes so TryUploadMissingUrlsAsync
                // re-uploads with the new image (otherwise the row keeps the stale URL).
                _originalCard.ImagePathFront = ImagePathFront;
                _originalCard.ImagePathBack = ImagePathBack;
                _originalCard.ImageUrl1 = ImageUrl1;
                _originalCard.ImageUrl2 = ImageUrl2;

                // Sync slots 3-8 from the AdditionalPhotos collection. Slots beyond the
                // current collection size are cleared (handles user removing a photo).
                ApplyAdditionalPhotosToCard(_originalCard);

                // Auto-fill Whatnot category/subcategory from Sport when blank.
                WhatnotCategoryDefaulter.ApplyDefaults(_originalCard);

                // Auto-upload any local images that don't yet have a hosted URL — saves
                // the user from a separate "Upload Images" step on the Export page.
                await TryUploadMissingUrlsAsync(_originalCard);

                // Auto-status: Ready if both images and price are present; Draft otherwise.
                // Listed/Sold are preserved by the evaluator.
                _originalCard.Status = CardStatusEvaluator.Evaluate(_originalCard);

                await _cardRepository.UpdateCardAsync(_originalCard);

                _logger.LogInformation("Card {CardId} updated: {PlayerName}", _originalCard.Id, _originalCard.PlayerName);

                // Navigate back to Inventory
                await _navigationService.NavigateToInventoryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save card {CardId}", _originalCard?.Id);
                ErrorMessage = $"Failed to save: {ex.Message}";
            }
        }

        private void ApplyAdditionalPhotosToCard(Card card)
        {
            for (int slotIdx = 0; slotIdx < MaxAdditionalPhotos; slotIdx++)
            {
                var path = slotIdx < AdditionalPhotos.Count ? AdditionalPhotos[slotIdx].Path : null;
                var url = slotIdx < AdditionalPhotos.Count ? AdditionalPhotos[slotIdx].Url : null;

                switch (slotIdx + 3)
                {
                    case 3: card.ImagePath3 = path; card.ImageUrl3 = url; break;
                    case 4: card.ImagePath4 = path; card.ImageUrl4 = url; break;
                    case 5: card.ImagePath5 = path; card.ImageUrl5 = url; break;
                    case 6: card.ImagePath6 = path; card.ImageUrl6 = url; break;
                    case 7: card.ImagePath7 = path; card.ImageUrl7 = url; break;
                    case 8: card.ImagePath8 = path; card.ImageUrl8 = url; break;
                }
            }
        }

        [RelayCommand]
        private async Task Cancel()
        {
            await _navigationService.NavigateToInventoryAsync();
        }

        /// <summary>
        /// Uploads any local image paths that don't yet have a corresponding hosted URL.
        /// Updates the card's <c>ImageUrl{N}</c> fields in place. Network errors are
        /// swallowed — partial uploads are fine; the save still proceeds.
        /// </summary>
        private async Task TryUploadMissingUrlsAsync(Card card)
        {
            var paths = new[] { card.ImagePathFront, card.ImagePathBack,
                                card.ImagePath3, card.ImagePath4, card.ImagePath5,
                                card.ImagePath6, card.ImagePath7, card.ImagePath8 };
            var urls  = new[] { card.ImageUrl1, card.ImageUrl2,
                                card.ImageUrl3, card.ImageUrl4, card.ImageUrl5,
                                card.ImageUrl6, card.ImageUrl7, card.ImageUrl8 };

            var pathsToUpload = new List<string?>(8);
            for (int i = 0; i < 8; i++)
                pathsToUpload.Add(string.IsNullOrEmpty(urls[i]) ? paths[i] : null);

            if (!pathsToUpload.Any(p => !string.IsNullOrEmpty(p))) return;

            try
            {
                var newUrls = await _imageUploadService.UploadCardImagesAsync(pathsToUpload);
                if (newUrls[0] != null) card.ImageUrl1 = newUrls[0];
                if (newUrls[1] != null) card.ImageUrl2 = newUrls[1];
                if (newUrls[2] != null) card.ImageUrl3 = newUrls[2];
                if (newUrls[3] != null) card.ImageUrl4 = newUrls[3];
                if (newUrls[4] != null) card.ImageUrl5 = newUrls[4];
                if (newUrls[5] != null) card.ImageUrl6 = newUrls[5];
                if (newUrls[6] != null) card.ImageUrl7 = newUrls[6];
                if (newUrls[7] != null) card.ImageUrl8 = newUrls[7];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image upload during save failed for card {Id}.", card.Id);
            }
        }
    }
}
