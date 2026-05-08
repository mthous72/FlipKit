using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Desktop.Models;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SurpriseSetDetailViewModel : ViewModelBase
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly ISurpriseSetValidator _validator;
        private readonly ISurpriseSetCsvExporter _csvExporter;
        private readonly ISurpriseSetCompletionService _completionService;
        private readonly IFileDialogService _fileDialog;
        private readonly INavigationService _navigation;
        private readonly ICardRepository _cardRepository;
        private readonly IScannerService _scannerService;
        private readonly ISettingsService _settingsService;
        private readonly Services.IPaidScanGate _paidScanGate;
        private readonly Services.IAppNotificationService? _notificationService;
        private readonly IPlayerNameDirectory? _playerDirectory;
        private CancellationTokenSource? _enhanceCts;

        [ObservableProperty] private SurpriseSet? _set;
        // Wraps each Card in a SelectableCard so the detail grid can host
        // checkboxes for the bulk-return flow. The wrapper exposes the
        // underlying Card via .Card; bindings inside the grid go through
        // {Binding Card.PlayerName}, etc.
        [ObservableProperty] private ObservableCollection<SelectableCard> _cards = new();
        [ObservableProperty] private ObservableCollection<SurpriseSetIssue> _issues = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isExporting;
        [ObservableProperty] private bool _isCompleting;
        [ObservableProperty] private bool _isEnhancing;
        [ObservableProperty] private int _enhanceProgress;
        [ObservableProperty] private int _enhanceTotal;
        [ObservableProperty] private string? _statusMessage;
        [ObservableProperty] private string? _exportStatusMessage;
        [ObservableProperty] private string? _completeStatusMessage;

        public bool HasOcrSourcedCards => Cards.Any(c => c.Card.DataSource == CardDataSource.Ocr);

        // True when at least one row in the grid is ticked. Drives the
        // visibility of "Return Selected to My Cards".
        public bool HasSelectedCards => Cards.Any(c => c.IsSelected);

        // Disband / Return-all should only fire on Draft sets — once a set is
        // Exported / Live / Completed the repository's IsLockedAsync gate
        // refuses RemoveCardAsync. Hiding the buttons keeps the flow honest.
        public bool CanReturnCards => Set?.State == SurpriseSetState.Draft && Cards.Count > 0;

        // Mark-completed form fields
        [ObservableProperty] private int _spotsSold;
        [ObservableProperty] private decimal _grossRevenue;
        [ObservableProperty] private decimal _totalFees;
        [ObservableProperty] private decimal _totalShipping;

        public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
        public int CardCount => Cards.Count;
        public bool CanComplete => Set?.State is SurpriseSetState.Draft
            or SurpriseSetState.Exported
            or SurpriseSetState.Live;

        public SurpriseSetDetailViewModel(
            ISurpriseSetRepository repository,
            ISurpriseSetValidator validator,
            ISurpriseSetCsvExporter csvExporter,
            ISurpriseSetCompletionService completionService,
            IFileDialogService fileDialog,
            INavigationService navigation,
            ICardRepository cardRepository,
            IScannerService scannerService,
            ISettingsService settingsService,
            Services.IPaidScanGate paidScanGate,
            IPlayerNameDirectory? playerDirectory = null,
            // Optional so existing test fixtures don't have to wire it up.
            Services.IAppNotificationService? notificationService = null)
        {
            _repository = repository;
            _validator = validator;
            _csvExporter = csvExporter;
            _completionService = completionService;
            _fileDialog = fileDialog;
            _navigation = navigation;
            _cardRepository = cardRepository;
            _scannerService = scannerService;
            _settingsService = settingsService;
            _paidScanGate = paidScanGate;
            _notificationService = notificationService;
            _playerDirectory = playerDirectory;
        }

        public async Task LoadAsync(int setId)
        {
            IsLoading = true;
            StatusMessage = null;
            ExportStatusMessage = null;
            CompleteStatusMessage = null;
            try
            {
                Set = await _repository.GetByIdWithCardsAsync(setId);
                if (Set == null)
                {
                    StatusMessage = $"Set {setId} not found.";
                    return;
                }

                var ordered = Set.Cards.OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue).ToList();
                var wrapped = ordered.Select(c =>
                {
                    var sc = new SelectableCard(c);
                    // Keep HasSelectedCards in sync as the user ticks rows so
                    // the bulk-return button shows / hides reactively.
                    sc.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(SelectableCard.IsSelected))
                            OnPropertyChanged(nameof(HasSelectedCards));
                    };
                    return sc;
                }).ToList();
                Cards = new ObservableCollection<SelectableCard>(wrapped);
                RefreshIssues(Set, ordered);
                OnPropertyChanged(nameof(HasOcrSourcedCards));
                OnPropertyChanged(nameof(HasSelectedCards));
                OnPropertyChanged(nameof(CanReturnCards));
                SpotsSold = ordered.Count; // default to full sell-through
                GrossRevenue = Set.GrossRevenue ?? Set.SpotPrice * ordered.Count;
                TotalFees = Set.TotalFees ?? 0m;
                TotalShipping = Set.TotalShipping ?? 0m;
                OnPropertyChanged(nameof(CanComplete));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Per-row "Return to My Cards" button. Takes either a Card or a
        // SelectableCard wrapper depending on what the XAML CommandParameter
        // resolves to — accepting object lets the same command serve both.
        // Returns the card to the primary inventory by clearing the FK + slot,
        // and re-evaluating CardStatus via CardStatusEvaluator.Evaluate (handled
        // inside SurpriseSetRepository.RemoveCardAsync).
        [RelayCommand]
        private async Task ReturnCardToInventoryAsync(object? param)
        {
            if (Set == null) return;
            var card = param switch
            {
                SelectableCard sc => sc.Card,
                Card c => c,
                _ => null,
            };
            if (card == null) return;
            try
            {
                await _repository.RemoveCardAsync(Set.Id, card.Id);
                await LoadAsync(Set.Id);
                StatusMessage = $"Returned {card.PlayerName} to My Cards.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not return card: {ex.Message}";
            }
        }

        /// <summary>
        /// Bulk variant of <see cref="ReturnCardToInventoryAsync"/> that runs
        /// over every ticked row. RemoveCardAsync re-evaluates each card's
        /// status via CardStatusEvaluator (so a previously priced card
        /// returns to Priced, etc.) and renumbers the remaining slots so the
        /// detail view stays contiguous.
        /// </summary>
        [RelayCommand]
        private async Task ReturnSelectedToInventoryAsync()
        {
            if (Set == null) return;
            var selected = Cards.Where(c => c.IsSelected).Select(c => c.Card).ToList();
            if (selected.Count == 0) return;

            var returned = 0;
            var failed = 0;
            foreach (var card in selected)
            {
                try
                {
                    await _repository.RemoveCardAsync(Set.Id, card.Id);
                    returned++;
                }
                catch (Exception)
                {
                    failed++;
                }
            }

            await LoadAsync(Set.Id);
            StatusMessage = failed > 0
                ? $"Returned {returned} card(s) to My Cards; {failed} failed."
                : $"Returned {returned} card(s) to My Cards.";
        }

        /// <summary>
        /// "Return all cards to My Cards" — the full-set flavor. Empties the
        /// set without deleting it so the user can repopulate or delete it
        /// afterward. Only legal on Draft sets (gated by CanReturnCards).
        /// </summary>
        [RelayCommand]
        private async Task DisbandAsync()
        {
            if (Set == null) return;
            var all = Cards.Select(c => c.Card).ToList();
            if (all.Count == 0) return;

            var returned = 0;
            var failed = 0;
            foreach (var card in all)
            {
                try
                {
                    await _repository.RemoveCardAsync(Set.Id, card.Id);
                    returned++;
                }
                catch (Exception)
                {
                    failed++;
                }
            }

            await LoadAsync(Set.Id);
            StatusMessage = failed > 0
                ? $"Returned {returned} card(s); {failed} failed."
                : $"Returned all {returned} card(s) to My Cards. Set is now empty.";
        }

        [RelayCommand]
        private async Task ExportCsvAsync()
        {
            if (Set == null) return;

            var path = await _fileDialog.SaveFileAsync(
                $"Export {Set.Name}",
                $"{Set.Name}-surprise-set.csv",
                new[] { "csv" });
            if (path == null) return;

            IsExporting = true;
            ExportStatusMessage = null;
            try
            {
                var result = await _csvExporter.ExportAsync(Set.Id, path);
                if (result.Success)
                {
                    ExportStatusMessage = $"Exported {result.RowsWritten} rows to {System.IO.Path.GetFileName(path)}.";
                    // Reload so the State badge updates to Exported.
                    await LoadAsync(Set.Id);
                }
                else
                {
                    var errorList = string.Join("; ", result.BlockingIssues.Select(i => i.Message));
                    ExportStatusMessage = $"Export blocked: {errorList}";
                }
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }

        [RelayCommand]
        private async Task MarkCompletedAsync()
        {
            if (Set == null) return;

            IsCompleting = true;
            CompleteStatusMessage = null;
            try
            {
                var result = await _completionService.CompleteAsync(Set.Id, new CompleteSetRequest
                {
                    SpotsSold = SpotsSold,
                    GrossRevenue = GrossRevenue,
                    TotalFees = TotalFees,
                    TotalShipping = TotalShipping,
                });

                if (result.Success)
                {
                    CompleteStatusMessage = $"Set marked Completed — {result.Allocations.Count(a => a.IsSold)} cards sold.";
                    await LoadAsync(Set.Id);
                }
                else
                {
                    CompleteStatusMessage = $"Error: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                CompleteStatusMessage = $"Failed: {ex.Message}";
            }
            finally
            {
                IsCompleting = false;
            }
        }

        [RelayCommand]
        private async Task EnhanceOcrCardsAsync()
        {
            if (Set == null) return;

            var ocrCards = Cards
                .Select(sc => sc.Card)
                .Where(c => c.DataSource == CardDataSource.Ocr
                         && !string.IsNullOrEmpty(c.ImagePathFront)
                         && File.Exists(c.ImagePathFront))
                .ToList();

            if (ocrCards.Count == 0) return;

            _enhanceCts = new CancellationTokenSource();
            IsEnhancing = true;
            EnhanceProgress = 0;
            EnhanceTotal = ocrCards.Count;
            StatusMessage = null;

            var settings = _settingsService.Load();
            // Resolve the UI's "auto" sentinel down to the free default, then gate
            // through the paid-model picker if the resolved id is paid. Free models
            // pass through silently. See OpenRouterModelDefaults.ResolveModelId and
            // PaidScanGate for the billing-safety rationale.
            var resolved = OpenRouterModelDefaults.ResolveModelId(settings.DefaultModel);
            var gated = await _paidScanGate.GateAsync(
                resolved,
                $"About to enhance {ocrCards.Count} card(s) in this set using a paid model. " +
                $"Pick which paid model to use, or cancel.");
            if (gated == null)
            {
                IsEnhancing = false;
                StatusMessage = "Enhance cancelled — no paid model used.";
                return;
            }
            var model = gated;

            try
            {
                foreach (var card in ocrCards)
                {
                    _enhanceCts.Token.ThrowIfCancellationRequested();

                    // Reconstruct verified-fields hint from the saved Card —
                    // re-querying the directory at Enhance time recovers the
                    // catalog-anchored fields (player, brand, sport-by-team,
                    // year, etc.) that the LLM should echo verbatim.
                    var hint = _playerDirectory?.IsReady == true
                        ? _playerDirectory.BuildHintFromCard(card)
                        : new OcrHint
                        {
                            PlayerName = card.PlayerName,
                            Year = card.Year,
                            CardNumber = card.CardNumber,
                            Manufacturer = card.Manufacturer,
                            Brand = card.Brand,
                            SetName = card.SetName,
                        };

                    var result = await _scannerService.ScanCardAsync(
                        card.ImagePathFront!,
                        card.ImagePathBack,
                        model,
                        scanDepth: ScanDepth.Standard,
                        ocrHint: hint,
                        ct: _enhanceCts.Token);

                    var e = result.Card;
                    card.PlayerName = e.PlayerName ?? card.PlayerName;
                    card.Year = e.Year ?? card.Year;
                    card.Manufacturer = e.Manufacturer ?? card.Manufacturer;
                    card.Brand = e.Brand ?? card.Brand;
                    card.SetName = e.SetName ?? card.SetName;
                    card.CardNumber = e.CardNumber ?? card.CardNumber;
                    card.Team = e.Team ?? card.Team;
                    card.VariationType = e.VariationType ?? card.VariationType;
                    card.ParallelName = e.ParallelName ?? card.ParallelName;
                    card.SerialNumbered = e.SerialNumbered ?? card.SerialNumbered;
                    card.IsRookie = e.IsRookie;
                    card.IsAuto = e.IsAuto;
                    card.IsRelic = e.IsRelic;
                    card.DataSource = CardDataSource.Ai;

                    await _cardRepository.UpdateCardAsync(card);
                    EnhanceProgress++;
                }

                StatusMessage = $"Enhanced {ocrCards.Count} card(s) with AI.";
                await LoadAsync(Set.Id);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Enhancement cancelled.";
            }
            catch (OpenRouterPaymentRequiredException pEx)
            {
                StatusMessage = pEx.Message;
                _notificationService?.NotifyPaymentRequired(pEx.ModelId, pEx.ResponseBody);
            }
            catch (OpenRouterRateLimitException rlEx)
            {
                StatusMessage = rlEx.Message;
                _notificationService?.NotifyRateLimit(rlEx.ModelId, rlEx.Scope, rlEx.RetryAfterSeconds);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Enhance failed: {ex.Message}";
            }
            finally
            {
                IsEnhancing = false;
                _enhanceCts?.Dispose();
                _enhanceCts = null;
                OnPropertyChanged(nameof(HasOcrSourcedCards));
            }
        }

        [RelayCommand]
        private async Task GoBackAsync() => await _navigation.NavigateAsync("SurpriseSets");

        private void RefreshIssues(SurpriseSet set, IList<Card> cards)
        {
            var found = _validator.Validate(set, cards);
            Issues = new ObservableCollection<SurpriseSetIssue>(found);
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(CardCount));
            OnPropertyChanged(nameof(CanComplete));
        }
    }
}
