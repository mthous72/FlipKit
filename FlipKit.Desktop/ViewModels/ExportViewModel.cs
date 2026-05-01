using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Export;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    public partial class ExportViewModel : ViewModelBase
    {
        private readonly ICardRepository _cardRepository;
        private readonly IExportService _exportService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IBrowserService _browserService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ExportViewModel> _logger;

        private List<Card> _exportableCards = new();

        [ObservableProperty] private int _readyCardCount;
        [ObservableProperty] private int _needsPricingCount;
        [ObservableProperty] private decimal _totalValue;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private ExportPlatform _selectedExportPlatform;

        public List<ExportPlatform> ExportPlatformOptions { get; } = Enum.GetValues<ExportPlatform>().ToList();

        /// <summary>
        /// Per-row pre-flight validation errors from the most recent export attempt.
        /// Populated when validation blocks an export; cleared when an export succeeds
        /// or when the user clicks Refresh.
        /// </summary>
        public ObservableCollection<ExportRowError> RowErrors { get; } = new();
        public bool HasRowErrors => RowErrors.Count > 0;

        public ExportViewModel(
            ICardRepository cardRepository,
            IExportService exportService,
            IFileDialogService fileDialogService,
            IBrowserService browserService,
            ISettingsService settingsService,
            ILogger<ExportViewModel> logger)
        {
            _cardRepository = cardRepository;
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
            ReplaceRowErrors(System.Array.Empty<ExportRowError>());
            LoadExportDataAsync();
            await Task.CompletedTask;
        }

        // Manual image upload was removed — uploads now happen automatically when a
        // card is saved with both images and a price. Cards without hosted URLs at
        // export time are caught by the validator's "ImageUrl1 required" rule, with
        // a clear message pointing the user back to the Edit page to re-save.

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

            // Pre-flight validation — fail fast before opening the file dialog if any
            // blocking errors are present, and surface the structured per-row errors so
            // the user can fix all of them in one pass.
            var validationErrors = _exportService.ValidateBatch(exportCards, SelectedExportPlatform);
            ReplaceRowErrors(validationErrors);
            var blockers = validationErrors.Where(e => e.Severity == ExportErrorSeverity.Error).ToList();
            if (blockers.Count > 0)
            {
                ErrorMessage = $"Export blocked by {blockers.Count} validation error(s) — see list below.";
                StatusMessage = null;
                return;
            }

            var defaultName = SelectedExportPlatform == ExportPlatform.eBay
                ? $"ebay-export-{DateTime.Now:yyyy-MM-dd}.csv"
                : $"whatnot-export-{DateTime.Now:yyyy-MM-dd}.csv";
            var path = await _fileDialogService.SaveCsvFileAsync(defaultName);
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
                ReplaceRowErrors(System.Array.Empty<ExportRowError>());
            }
            catch (ExportValidationException vex)
            {
                _logger.LogWarning(vex, "Export blocked by validator (post-dialog)");
                ReplaceRowErrors(vex.Errors);
                ErrorMessage = $"Export blocked by {vex.Errors.Count} validation error(s) — see list below.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV export failed");
                ErrorMessage = $"Export failed: {ex.Message}";
            }
        }

        private void ReplaceRowErrors(IReadOnlyList<ExportRowError> errors)
        {
            RowErrors.Clear();
            foreach (var e in errors)
                RowErrors.Add(e);
            OnPropertyChanged(nameof(HasRowErrors));
        }

        [RelayCommand]
        private void OpenWhatnotSellerHub()
        {
            _browserService.OpenUrl("https://www.whatnot.com/dashboard/inventory");
        }
    }
}
