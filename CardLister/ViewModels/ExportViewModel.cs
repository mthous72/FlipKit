using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardLister.Models;
using CardLister.Models.Enums;
using CardLister.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CardLister.ViewModels
{
    public partial class ExportViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IImageUploadService _imageUploadService;
        private readonly IExportService _exportService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IBrowserService _browserService;
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

        public ExportViewModel(
            ICardRepository cardRepository,
            IImageUploadService imageUploadService,
            IExportService exportService,
            IFileDialogService fileDialogService,
            IBrowserService browserService,
            ILogger<ExportViewModel> logger)
        {
            _cardRepository = cardRepository;
            _imageUploadService = imageUploadService;
            _exportService = exportService;
            _fileDialogService = fileDialogService;
            _browserService = browserService;
            _logger = logger;

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
                NeedsImageUploadCount = _exportableCards.Count(c => string.IsNullOrEmpty(c.ImageUrl1));
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
            var cardsNeedingUpload = _exportableCards
                .Where(c => string.IsNullOrEmpty(c.ImageUrl1) && !string.IsNullOrEmpty(c.ImagePathFront))
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
                        var (url1, url2) = await _imageUploadService.UploadCardImagesAsync(
                            card.ImagePathFront!, card.ImagePathBack);

                        if (url1 != null) card.ImageUrl1 = url1;
                        if (url2 != null) card.ImageUrl2 = url2;

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

                NeedsImageUploadCount = _exportableCards.Count(c => string.IsNullOrEmpty(c.ImageUrl1));
                StatusMessage = $"Uploaded {UploadProgress} images.";
            }
            finally
            {
                IsUploading = false;
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
                await _exportService.ExportCsvAsync(exportCards, path);

                // Mark as listed
                foreach (var card in exportCards)
                {
                    if (card.Status == CardStatus.Ready || card.Status == CardStatus.Priced)
                    {
                        card.Status = CardStatus.Listed;
                        await _cardRepository.UpdateCardAsync(card);
                    }
                }

                StatusMessage = $"Exported {exportCards.Count} cards to CSV!";
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
