using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        // Master list of every card we've loaded — kept independent of the filtered
        // view so toggling filters doesn't refetch from the DB.
        private List<ExportableCard> _allItems = new();

        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private ExportPlatform _selectedExportPlatform;

        // Filter state — defaults: Ready + Listed visible, Drafts/Sold hidden, no
        // sport filter, no date range, no search. Filters reset to these defaults
        // on every app launch (no persistence).
        [ObservableProperty] private bool _showReady = true;
        [ObservableProperty] private bool _showListed = true;
        [ObservableProperty] private bool _showDraft = false;
        [ObservableProperty] private bool _showSold = false;
        [ObservableProperty] private string _sportFilter = AllSportsLabel;
        [ObservableProperty] private DateTimeOffset? _addedFrom;
        [ObservableProperty] private DateTimeOffset? _addedTo;
        [ObservableProperty] private string _searchText = string.Empty;

        public List<ExportPlatform> ExportPlatformOptions { get; } = Enum.GetValues<ExportPlatform>().ToList();

        public const string AllSportsLabel = "All sports";
        public List<string> SportFilterOptions { get; } = new List<string> { AllSportsLabel }
            .Concat(Enum.GetValues<Sport>().Select(s => s.ToString()))
            .ToList();

        /// <summary>
        /// The cards currently visible in the grid, after applying every filter.
        /// </summary>
        public ObservableCollection<ExportableCard> Items { get; } = new();

        /// <summary>
        /// Per-row pre-flight validation errors from the most recent export attempt.
        /// </summary>
        public ObservableCollection<ExportRowError> RowErrors { get; } = new();
        public bool HasRowErrors => RowErrors.Count > 0;

        // Computed counters bound to the View.
        public int VisibleCount => Items.Count;
        public int SelectedCount => Items.Count(i => i.IsSelected);
        public decimal SelectedValue => Items.Where(i => i.IsSelected && i.ListingPrice.HasValue)
                                              .Sum(i => i.ListingPrice!.Value);

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

            var settings = _settingsService.Load();
            _selectedExportPlatform = settings.ActiveExportPlatform;

            _ = LoadAsync();
        }

        // === filter property reactions ===

        partial void OnShowReadyChanged(bool value) => ApplyFilters();
        partial void OnShowListedChanged(bool value) => ApplyFilters();
        partial void OnShowDraftChanged(bool value) => ApplyFilters();
        partial void OnShowSoldChanged(bool value) => ApplyFilters();
        partial void OnSportFilterChanged(string value) => ApplyFilters();
        partial void OnAddedFromChanged(DateTimeOffset? value) => ApplyFilters();
        partial void OnAddedToChanged(DateTimeOffset? value) => ApplyFilters();
        partial void OnSearchTextChanged(string value) => ApplyFilters();

        // === load + filter ===

        private async Task LoadAsync()
        {
            try
            {
                ErrorMessage = null;
                var allCards = await _cardRepository.GetAllCardsAsync();

                // Newest-first by default — matches the user's "I want to export the
                // batch I just scanned" expectation.
                var ordered = allCards.OrderByDescending(c => c.CreatedAt).ToList();

                _allItems = ordered.Select(c =>
                {
                    var item = new ExportableCard(c, isSelected: c.Status == CardStatus.Ready);
                    item.PropertyChanged += OnItemPropertyChanged;
                    return item;
                }).ToList();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load export data");
                ErrorMessage = "Failed to load export data.";
            }
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExportableCard.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(SelectedValue));
            }
        }

        private void ApplyFilters()
        {
            var visible = _allItems.Where(MatchesFilters).ToList();

            Items.Clear();
            foreach (var i in visible) Items.Add(i);

            OnPropertyChanged(nameof(VisibleCount));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedValue));
        }

        private bool MatchesFilters(ExportableCard item)
        {
            var status = item.Card.Status;
            var statusOk = (status == CardStatus.Ready  && ShowReady)
                        || (status == CardStatus.Listed && ShowListed)
                        || (status == CardStatus.Draft  && ShowDraft)
                        || (status == CardStatus.Priced && ShowReady)   // legacy "Priced" rolls into Ready
                        || (status == CardStatus.Sold   && ShowSold);
            if (!statusOk) return false;

            if (!string.Equals(SportFilter, AllSportsLabel, StringComparison.Ordinal))
            {
                if (item.Card.Sport?.ToString() != SportFilter) return false;
            }

            if (AddedFrom.HasValue && item.Card.CreatedAt < AddedFrom.Value.UtcDateTime) return false;
            if (AddedTo.HasValue && item.Card.CreatedAt > AddedTo.Value.UtcDateTime.AddDays(1)) return false;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim();
                bool hit =
                    (item.Card.PlayerName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (item.Card.SetName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (item.Card.Brand?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (item.Card.CardNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (item.Card.Sku?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!hit) return false;
            }

            return true;
        }

        // === commands ===

        [RelayCommand]
        private async Task RefreshAsync()
        {
            ErrorMessage = null;
            StatusMessage = null;
            ReplaceRowErrors(Array.Empty<ExportRowError>());
            await LoadAsync();
        }

        [RelayCommand]
        private void SelectAllVisible()
        {
            foreach (var i in Items) i.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var i in Items) i.IsSelected = false;
        }

        [RelayCommand]
        private void ClearFilters()
        {
            ShowReady = true;
            ShowListed = true;
            ShowDraft = false;
            ShowSold = false;
            SportFilter = AllSportsLabel;
            AddedFrom = null;
            AddedTo = null;
            SearchText = string.Empty;
        }

        [RelayCommand]
        private async Task ExportCsvAsync()
        {
            var selected = Items.Where(i => i.IsSelected).Select(i => i.Card).ToList();
            if (selected.Count == 0)
            {
                ErrorMessage = "Pick at least one card to export.";
                return;
            }

            // Pre-flight validation against the chosen platform.
            var validationErrors = _exportService.ValidateBatch(selected, SelectedExportPlatform);
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
                await _exportService.ExportCsvAsync(selected, path, SelectedExportPlatform);

                // Promote Ready → Listed (informational tracking). Listed cards stay
                // Listed on re-export. Draft/Sold also pass through unchanged.
                foreach (var card in selected)
                {
                    if (card.Status == CardStatus.Ready || card.Status == CardStatus.Priced)
                    {
                        card.Status = CardStatus.Listed;
                        await _cardRepository.UpdateCardAsync(card);
                    }
                }

                StatusMessage = $"Exported {selected.Count} cards to CSV for {SelectedExportPlatform}.";
                ErrorMessage = null;
                ReplaceRowErrors(Array.Empty<ExportRowError>());

                // Reflect the status changes in the grid without a full DB reload.
                OnPropertyChanged(nameof(SelectedCount));
                ApplyFilters();
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

        [RelayCommand]
        private void OpenWhatnotSellerHub()
        {
            _browserService.OpenUrl("https://www.whatnot.com/dashboard/inventory");
        }

        // === helpers ===

        private void ReplaceRowErrors(IReadOnlyList<ExportRowError> errors)
        {
            RowErrors.Clear();
            foreach (var e in errors) RowErrors.Add(e);
            OnPropertyChanged(nameof(HasRowErrors));
        }
    }
}
