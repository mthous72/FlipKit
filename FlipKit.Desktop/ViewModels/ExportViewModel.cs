using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    public partial class ExportViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IImageUploadService _imageUploadService;
        private readonly IExportService _exportService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IBrowserService _browserService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ExportViewModel> _logger;

        private List<Card> _exportableCards = new();

        [ObservableProperty] private int _readyCardCount;
        [ObservableProperty] private int _needsPricingCount;
        [ObservableProperty] private int _needsImageUploadCount;
        [ObservableProperty] private decimal _totalValue;
        [ObservableProperty] private bool _isUploading;
        [ObservableProperty] private int _uploadProgress;
        [ObservableProperty] private int _uploadTotal;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private ExportPlatform _selectedExportPlatform;

        public List<ExportPlatform> ExportPlatformOptions { get; } = Enum.GetValues<ExportPlatform>().ToList();

        public ExportViewModel(
            ICardRepository cardRepository,
            IImageUploadService imageUploadService,
            IExportService exportService,
            IFileDialogService fileDialogService,
            IBrowserService browserService,
            ISettingsService settingsService,
            ILogger<ExportViewModel> logger)
        {
            _cardRepository = cardRepository;
            _imageUploadService = imageUploadService;
            _exportService = exportService;
            _fileDialogService = fileDialogService;
            _browserService = browserService;
            _settingsService = settingsService;
            _logger = logger;

            // Initialize selected platform from settings
            var settings = _settingsService.Load();
            _selectedExportPlatform = settings.ActiveExportPlatform;

            LoadExportDataAsync();
        }

        private async void LoadExportDataAsync()
        {
            try
            {
                var allCards = await _cardRepository.GetAllCardsAsync();

                _exportableCards = allCards.Where(c =>
                    c.Status == CardStatus.Priced || c.Status == CardStatus.Ready).ToList();

                ReadyCardCount = _exportableCards.Count;
                NeedsPricingCount = allCards.Count(c => c.Status == CardStatus.Draft);
                NeedsImageUploadCount = _exportableCards.Count(CardHasMissingUploads);
                TotalValue = _exportableCards.Where(c => c.ListingPrice.HasValue).Sum(c => c.ListingPrice!.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load export data");
                ErrorMessage = "Failed to load export data.";
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            ErrorMessage = null;
            StatusMessage = null;
            LoadExportDataAsync();
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task UploadImagesAsync()
        {
            // A card needs work if any slot has a local path but no hosted URL yet.
            var cardsNeedingUpload = _exportableCards
                .Where(CardHasMissingUploads)
                .ToList();

            if (cardsNeedingUpload.Count == 0)
            {
                StatusMessage = "No images to upload.";
                return;
            }

            IsUploading = true;
            UploadTotal = cardsNeedingUpload.Count;
            UploadProgress = 0;
            ErrorMessage = null;
            StatusMessage = null;

            try
            {
                foreach (var card in cardsNeedingUpload)
                {
                    try
                    {
                        var paths = GetImagePathSlots(card);
                        var existingUrls = GetImageUrlSlots(card);

                        // Only upload slots that have a path but no URL — preserves any URLs
                        // already filled in (e.g. from a partial earlier run).
                        var pathsToUpload = new List<string?>(8);
                        for (int i = 0; i < 8; i++)
                            pathsToUpload.Add(string.IsNullOrEmpty(existingUrls[i]) ? paths[i] : null);

                        var newUrls = await _imageUploadService.UploadCardImagesAsync(pathsToUpload);

                        for (int i = 0; i < 8; i++)
                            if (newUrls[i] != null)
                                SetImageUrlSlot(card, i, newUrls[i]);

                        if (card.Status == CardStatus.Priced)
                            card.Status = CardStatus.Ready;

                        await _cardRepository.UpdateCardAsync(card);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Image upload failed for {Player}", card.PlayerName);
                        ErrorMessage = $"Upload failed for {card.PlayerName}: {ex.Message}";
                    }

                    UploadProgress++;
                }

                NeedsImageUploadCount = _exportableCards.Count(CardHasMissingUploads);
                StatusMessage = $"Uploaded images for {UploadProgress} cards.";
            }
            finally
            {
                IsUploading = false;
            }
        }

        private static bool CardHasMissingUploads(Card card)
        {
            var paths = GetImagePathSlots(card);
            var urls = GetImageUrlSlots(card);
            for (int i = 0; i < 8; i++)
                if (!string.IsNullOrEmpty(paths[i]) && string.IsNullOrEmpty(urls[i]))
                    return true;
            return false;
        }

        private static string?[] GetImagePathSlots(Card card) => new[]
        {
            card.ImagePathFront, card.ImagePathBack,
            card.ImagePath3, card.ImagePath4, card.ImagePath5,
            card.ImagePath6, card.ImagePath7, card.ImagePath8,
        };

        private static string?[] GetImageUrlSlots(Card card) => new[]
        {
            card.ImageUrl1, card.ImageUrl2, card.ImageUrl3, card.ImageUrl4,
            card.ImageUrl5, card.ImageUrl6, card.ImageUrl7, card.ImageUrl8,
        };

        private static void SetImageUrlSlot(Card card, int index, string? url)
        {
            switch (index)
            {
                case 0: card.ImageUrl1 = url; break;
                case 1: card.ImageUrl2 = url; break;
                case 2: card.ImageUrl3 = url; break;
                case 3: card.ImageUrl4 = url; break;
                case 4: card.ImageUrl5 = url; break;
                case 5: card.ImageUrl6 = url; break;
                case 6: card.ImageUrl7 = url; break;
                case 7: card.ImageUrl8 = url; break;
            }
        }

        [RelayCommand]
        private async Task ExportCsvAsync()
        {
            var exportCards = _exportableCards
                .Where(c => c.ListingPrice.HasValue && c.ListingPrice > 0)
                .ToList();

            if (exportCards.Count == 0)
            {
                ErrorMessage = "No cards ready for export.";
                return;
            }

            // Validate
            var allErrors = new List<string>();
            foreach (var card in exportCards)
            {
                var errors = _exportService.ValidateCardForExport(card);
                if (errors.Count > 0)
                    allErrors.Add($"{card.PlayerName}: {string.Join(", ", errors)}");
            }

            if (allErrors.Count > 0)
            {
                ErrorMessage = $"Validation issues: {string.Join("; ", allErrors.Take(3))}";
                return;
            }

            var path = await _fileDialogService.SaveCsvFileAsync($"whatnot-export-{DateTime.Now:yyyy-MM-dd}.csv");
            if (path == null) return;

            try
            {
                await _exportService.ExportCsvAsync(exportCards, path, SelectedExportPlatform);

                // Mark as listed
                foreach (var card in exportCards)
                {
                    if (card.Status == CardStatus.Ready || card.Status == CardStatus.Priced)
                    {
                        card.Status = CardStatus.Listed;
                        await _cardRepository.UpdateCardAsync(card);
                    }
                }

                StatusMessage = $"Exported {exportCards.Count} cards to CSV for {SelectedExportPlatform}!";
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV export failed");
                ErrorMessage = $"Export failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenWhatnotSellerHub()
        {
            _browserService.OpenUrl("https://www.whatnot.com/dashboard/inventory");
        }
    }
}
