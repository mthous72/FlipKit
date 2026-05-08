using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FlipKit.Core.Helpers;

namespace FlipKit.Desktop.ViewModels
{
    public partial class BulkScanViewModel : ViewModelBase, IDisposable, IKeepAliveViewModel
    {
        private readonly IScannerService _scannerService;
        private readonly IOcrService _ocrService;
        private readonly ICardRepository _cardRepository;
        private readonly ISurpriseSetRepository _surpriseSetRepository;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsService _settingsService;
        private readonly IVariationVerifier _variationVerifier;
        private readonly IBulkScanErrorLogger _errorLogger;
        private readonly IOpenRouterModelCatalog _modelCatalog;
        private readonly IPaidModelConsentService _consentService;
        private readonly IAiScanConsentService _aiScanConsentService;
        private readonly IImageUploadService _imageUploadService;
        private readonly IAppNotificationService? _notificationService;
        private readonly IPlayerNameDirectory? _playerDirectory;
        private readonly ILogger<BulkScanViewModel> _logger;

        private CancellationTokenSource? _scanCts;

        // Thread-safe counter for in-flight scans. Backs ScanProgress without
        // having to mutate the source-generator-managed _scanProgress field
        // directly (which used to require an MVVMTK0034 suppression).
        private int _completedCount;

        [ObservableProperty] private bool _imagesArePairs = true;
        [ObservableProperty] private bool _isScanning;
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private int _scanProgress;
        [ObservableProperty] private int _scanTotal;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private BulkScanItem? _selectedItem;
        [ObservableProperty] private string? _statusMessage;

        // Model selection and concurrency
        [ObservableProperty] private ModelOption? _selectedModel;
        [ObservableProperty] private int _maxConcurrentScans = 1;
        [ObservableProperty] private bool _isLoadingModels;
        [ObservableProperty] private string? _modelLoadError;

        // Surprise Set destination
        [ObservableProperty] private BulkScanDestination _destination = BulkScanDestination.Inventory;
        [ObservableProperty] private int? _destinationSurpriseSetId;
        [ObservableProperty] private ScanDepth _scanDepth = ScanDepth.Standard;

        // Saved Surprise Sets the user can target. Refreshed on construction
        // and after the inline Create-Set flow. Only Draft sets are listed —
        // Exported / Live / Completed sets reject AddCardAsync per the
        // repository's IsLockedAsync gate.
        public ObservableCollection<SurpriseSet> AvailableSurpriseSets { get; } = new();

        // Currently picked target. Two-way bound by the second dropdown that
        // appears when Destination = SurpriseSet. Mirrored into
        // DestinationSurpriseSetId so the existing SaveAllAsync gate
        // (DestinationSurpriseSetId.HasValue) keeps working unchanged.
        [ObservableProperty] private SurpriseSet? _selectedSurpriseSet;

        partial void OnSelectedSurpriseSetChanged(SurpriseSet? value)
        {
            DestinationSurpriseSetId = value?.Id;
            OnPropertyChanged(nameof(CanSave));
        }

        /// <summary>
        /// True only when the user has chosen Surprise Set as the destination.
        /// Drives the saved-set picker's IsVisible binding in the XAML.
        /// </summary>
        public bool RequiresSetSelection => Destination == BulkScanDestination.SurpriseSet;

        /// <summary>
        /// Save All gate. Forbids saving while a scan or save is already
        /// running, and forbids saving to Surprise-Set destination unless the
        /// user has actually picked a set. Without this, SaveAllAsync would
        /// silently fall through to the Inventory branch when
        /// DestinationSurpriseSetId is null.
        /// </summary>
        public bool CanSave =>
            !IsScanning && !IsSaving &&
            (Destination != BulkScanDestination.SurpriseSet || SelectedSurpriseSet != null);

        partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(CanSave));

        // Rate-limit banners
        [ObservableProperty] private bool _isRateLimitPaused;
        [ObservableProperty] private string? _rateLimitBannerMessage;

        // OCR mode + bulk enhance
        [ObservableProperty] private ScanMode _scanMode = ScanMode.Ai;
        [ObservableProperty] private bool _isEnhancing;
        [ObservableProperty] private int _enhanceProgress;
        [ObservableProperty] private int _enhanceTotal;

        // Live ticker for the enhance pipeline — bound by the BulkScan ticker panel
        // and the global persistent status bar in MainWindow.
        [ObservableProperty] private BulkScanItem? _currentEnhanceItem;
        [ObservableProperty] private string? _currentEnhanceModel;

        private CancellationTokenSource? _enhanceCts;

        public ObservableCollection<ModelOption> ModelOptions { get; } = new();

        // Typed option lists so ComboBox SelectedItem binds correctly (avoids ComboBoxItem cast exception).
        public static IReadOnlyList<DestinationOption> DestinationOptions { get; } = new[]
        {
            new DestinationOption(BulkScanDestination.Inventory, "Inventory"),
            new DestinationOption(BulkScanDestination.SurpriseSet, "Surprise Set"),
        };

        public static IReadOnlyList<ScanDepthOption> ScanDepthOptions { get; } = new[]
        {
            new ScanDepthOption(ScanDepth.Quick, "Quick (lot scanning)"),
            new ScanDepthOption(ScanDepth.Standard, "Standard (full detail)"),
        };

        public static IReadOnlyList<ScanModeOption> ScanModeOptions { get; } = new[]
        {
            new ScanModeOption(ScanMode.Ai,  "AI (Online)"),
            new ScanModeOption(ScanMode.Ocr, "OCR (Offline, Free)"),
        };

        public ScanModeOption SelectedScanModeOption
        {
            get => ScanModeOptions.First(o => o.Value == ScanMode);
            set => ScanMode = value.Value;
        }

        public bool IsOcrMode => ScanMode == ScanMode.Ocr;
        public bool IsAiMode => ScanMode == ScanMode.Ai;
        public bool IsOcrAvailable => _ocrService.IsAvailable;
        public bool IsOcrModeUnavailable => IsOcrMode && !IsOcrAvailable;
        public bool HasOcrScannedItems => Items.Any(i => i.ScanMode == ScanMode.Ocr && i.Status == BulkScanStatus.Scanned);

        public DestinationOption SelectedDestinationOption
        {
            get => DestinationOptions.First(o => o.Value == Destination);
            set => Destination = value.Value;
        }

        public ScanDepthOption SelectedScanDepthOption
        {
            get => ScanDepthOptions.First(o => o.Value == ScanDepth);
            set => ScanDepth = value.Value;
        }

        // Treats Auto and any explicit free pick as "free": forces concurrency = 1 to
        // avoid hammering rate limits.
        public bool IsSelectedModelFree =>
            SelectedModel == null
            || SelectedModel.IsAuto
            || (SelectedModel.Model?.IsFree ?? SelectedModel.Value.Contains(":free"));

        // Show the free-model info banner when not already paused on a rate limit.
        public bool ShowFreeModelBanner => IsSelectedModelFree && !IsRateLimitPaused;

        partial void OnSelectedModelChanged(ModelOption? value)
        {
            OnPropertyChanged(nameof(IsSelectedModelFree));
            OnPropertyChanged(nameof(ShowFreeModelBanner));
            if (IsSelectedModelFree) MaxConcurrentScans = 1;

            // Persist the explicit pick so it survives navigation and restarts.
            if (value == null || value.IsAuto) return;
            var settings = _settingsService.Load();
            if (settings.DefaultModel == value.Value) return;
            settings.DefaultModel = value.Value;
            _settingsService.Save(settings);
        }

        partial void OnIsRateLimitPausedChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowFreeModelBanner));
        }

        partial void OnScanModeChanged(ScanMode value)
        {
            if (value == ScanMode.Ocr)
                MaxConcurrentScans = 1;
            OnPropertyChanged(nameof(IsOcrMode));
            OnPropertyChanged(nameof(IsAiMode));
            OnPropertyChanged(nameof(IsOcrModeUnavailable));
            OnPropertyChanged(nameof(SelectedScanModeOption));
        }

        partial void OnDestinationChanged(BulkScanDestination value)
        {
            // Surprise Set bulk scans default to Quick depth — just enough to label each slot.
            // The user can override after the fact from the Inventory view.
            ScanDepth = value == BulkScanDestination.SurpriseSet ? ScanDepth.Quick : ScanDepth.Standard;
            OnPropertyChanged(nameof(SelectedDestinationOption));
            OnPropertyChanged(nameof(RequiresSetSelection));
            OnPropertyChanged(nameof(CanSave));
        }

        partial void OnScanDepthChanged(ScanDepth value)
        {
            OnPropertyChanged(nameof(SelectedScanDepthOption));
        }

        public ObservableCollection<BulkScanItem> Items { get; } = new();

        public CardDetailViewModel? SelectedCard => SelectedItem?.CardDetail;

        partial void OnSelectedItemChanged(BulkScanItem? value)
        {
            OnPropertyChanged(nameof(SelectedCard));
            RescanSelectedCommand.NotifyCanExecuteChanged();
        }

        public BulkScanViewModel(
            IScannerService scannerService,
            IOcrService ocrService,
            ICardRepository cardRepository,
            ISurpriseSetRepository surpriseSetRepository,
            IFileDialogService fileDialogService,
            ISettingsService settingsService,
            IVariationVerifier variationVerifier,
            IBulkScanErrorLogger errorLogger,
            IOpenRouterModelCatalog modelCatalog,
            IPaidModelConsentService consentService,
            IAiScanConsentService aiScanConsentService,
            IImageUploadService imageUploadService,
            ILogger<BulkScanViewModel> logger,
            IAppNotificationService? notificationService = null,
            IPlayerNameDirectory? playerDirectory = null)
        {
            _scannerService = scannerService;
            _ocrService = ocrService;
            _cardRepository = cardRepository;
            _surpriseSetRepository = surpriseSetRepository;
            _fileDialogService = fileDialogService;
            _settingsService = settingsService;
            _variationVerifier = variationVerifier;
            _errorLogger = errorLogger;
            _modelCatalog = modelCatalog;
            _consentService = consentService;
            _aiScanConsentService = aiScanConsentService;
            _imageUploadService = imageUploadService;
            _notificationService = notificationService;
            _playerDirectory = playerDirectory;
            _logger = logger;

            // Subscribe to per-item PropertyChanged so HasSelectedOcrItems /
            // HasOcrScannedItems / Enhance command CanExecute re-evaluate when
            // checkboxes flip or scan-status changes. Items only ever grow
            // (no item is recreated for an existing index), so attach-on-add
            // is sufficient and we never need to detach.
            Items.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (BulkScanItem item in e.NewItems)
                        item.PropertyChanged += (_, args) => OnBulkScanItemPropertyChanged(args.PropertyName);
                }
            };

            // Initialize from settings
            var settings = _settingsService.Load();
            _maxConcurrentScans = settings.MaxConcurrentScans;

            _ = LoadModelsAsync();
            _ = LoadAvailableSurpriseSetsAsync();
        }

        /// <summary>
        /// Refreshes <see cref="AvailableSurpriseSets"/> from
        /// <c>GetDraftSetsAsync</c>. Called on construction and after the
        /// inline Create-Set flow so the picker stays current. Only Draft
        /// sets are listed because Exported / Live / Completed sets reject
        /// AddCardAsync at the repository layer.
        /// </summary>
        private async Task LoadAvailableSurpriseSetsAsync()
        {
            try
            {
                var drafts = await _surpriseSetRepository.GetDraftSetsAsync();
                AvailableSurpriseSets.Clear();
                foreach (var set in drafts) AvailableSurpriseSets.Add(set);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Draft surprise sets for the BulkScan picker");
            }
        }

        /// <summary>
        /// Opens the New-Surprise-Set dialog from inside the BulkScan tab so
        /// the user doesn't have to switch context. On success, refreshes the
        /// picker and auto-selects the new set as the current target.
        /// </summary>
        [RelayCommand]
        private async Task CreateSetInlineAsync()
        {
            var vm = new NewSurpriseSetViewModel();
            var dialog = new Views.NewSurpriseSetDialog { DataContext = vm };
            var owner = (Avalonia.Application.Current?.ApplicationLifetime
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner != null)
                await dialog.ShowDialog(owner);
            else
                dialog.Show();

            if (!vm.Confirmed || !vm.IsValid) return;

            try
            {
                var newSet = vm.BuildSet();
                await _surpriseSetRepository.InsertAsync(newSet);
                await LoadAvailableSurpriseSetsAsync();
                // Re-select the freshly created set so the user can hit Save All immediately.
                SelectedSurpriseSet = AvailableSurpriseSets.FirstOrDefault(s => s.Id == newSet.Id);
                StatusMessage = $"Created \"{newSet.Name}\".";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inline Create-Surprise-Set failed");
                ErrorMessage = $"Could not create set: {ex.Message}";
            }
        }

        private async Task LoadModelsAsync()
        {
            IsLoadingModels = true;
            ModelLoadError = null;
            try
            {
                var catalog = await _modelCatalog.GetAsync();
                ModelOptions.Clear();
                ModelOptions.Add(ModelOption.Auto());
                foreach (var m in catalog.FreeVisionModels) ModelOptions.Add(ModelOption.FromCatalog(m));
                foreach (var m in catalog.PaidVisionModels) ModelOptions.Add(ModelOption.FromCatalog(m));

                var savedId = _settingsService.Load().DefaultModel;
                ModelOption? choice = null;
                if (!string.IsNullOrWhiteSpace(savedId) && savedId != ModelOption.AutoValue)
                    choice = ModelOptions.FirstOrDefault(o => o.Value == savedId);
                SelectedModel = choice ?? ModelOptions.First(); // First() = Auto when not in catalog

                if (catalog.IsEmpty)
                    ModelLoadError = "Couldn't reach OpenRouter for the live model list.";
            }
            catch (Exception ex)
            {
                ModelLoadError = ex.Message;
                ModelOptions.Clear();
                ModelOptions.Add(ModelOption.Auto());
                SelectedModel = ModelOptions[0];
            }
            finally
            {
                IsLoadingModels = false;
            }
        }

        [RelayCommand]
        private async Task SelectImagesAsync()
        {
            var paths = await _fileDialogService.OpenImageFilesAsync();
            if (paths.Count == 0)
                return;

            ErrorMessage = null;
            SuccessMessage = null;

            paths.Sort(StringComparer.OrdinalIgnoreCase);

            if (ImagesArePairs)
            {
                // Pair consecutive images as front/back
                for (int i = 0; i < paths.Count; i += 2)
                {
                    var item = new BulkScanItem
                    {
                        Index = Items.Count + 1,
                        FrontImagePath = paths[i],
                        BackImagePath = i + 1 < paths.Count ? paths[i + 1] : null,
                        DisplayName = $"Card {Items.Count + 1}"
                    };
                    Items.Add(item);
                }
            }
            else
            {
                // Each image is a separate card (front only)
                foreach (var path in paths)
                {
                    var item = new BulkScanItem
                    {
                        Index = Items.Count + 1,
                        FrontImagePath = path,
                        DisplayName = $"Card {Items.Count + 1}"
                    };
                    Items.Add(item);
                }
            }
        }

        [RelayCommand]
        private async Task ScanAllAsync()
        {
            if (Items.Count == 0)
                return;

            // Reset Error cards to Pending so they are retried on each scan run.
            foreach (var failed in Items.Where(i => i.Status == BulkScanStatus.Error))
            {
                failed.Status = BulkScanStatus.Pending;
                failed.ErrorMessage = null;
            }

            var pending = Items.Where(i => i.Status == BulkScanStatus.Pending).ToList();
            if (pending.Count == 0)
                return;

            await ScanItemsAsync(pending);
        }

        /// <summary>
        /// Re-scans just the currently selected card through the active scan mode.
        /// Useful when a single card returned bad fields (OCR garbage, bad AI guess)
        /// and the user wants to redo it without re-running the whole batch.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRescanSelected))]
        private async Task RescanSelectedAsync()
        {
            if (SelectedItem == null) return;
            var item = SelectedItem;
            item.Status = BulkScanStatus.Pending;
            item.ErrorMessage = null;
            await ScanItemsAsync(new List<BulkScanItem> { item });
        }

        private bool CanRescanSelected() =>
            SelectedItem != null && !IsScanning && !IsEnhancing;

        partial void OnIsScanningChanged(bool value)
        {
            RescanSelectedCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanSave));
        }

        partial void OnIsEnhancingChanged(bool value)
        {
            RescanSelectedCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Runs the active scan mode against the supplied items. Encapsulates the
        /// IsScanning / rate-limit / error-tracking lifecycle so callers (ScanAll
        /// and RescanSelected) don't duplicate it.
        /// </summary>
        private async Task ScanItemsAsync(List<BulkScanItem> pending)
        {
            if (pending.Count == 0) return;

            IsScanning = true;
            IsRateLimitPaused = false;
            RateLimitBannerMessage = null;
            ErrorMessage = null;
            SuccessMessage = null;

            // OCR mode: bypass AI consent, model chain, and rate-limit logic entirely.
            if (ScanMode == ScanMode.Ocr)
            {
                await ScanAllOcrAsync(pending);
                IsScanning = false;
                return;
            }

            ScanProgress = 0;
            Interlocked.Exchange(ref _completedCount, 0);
            ScanTotal = pending.Count;
            _scanCts = new CancellationTokenSource();

            var settings = _settingsService.Load();

            // First-run consent gate — same check as single-card ScanViewModel.
            if (!settings.AiScanConsentGiven)
            {
                var consent = await _aiScanConsentService.AskAsync();
                if (!consent.Proceed)
                {
                    IsScanning = false;
                    _scanCts = null;
                    StatusMessage = null;
                    return;
                }
                if (consent.Remember)
                {
                    settings.AiScanConsentGiven = true;
                    _settingsService.Save(settings);
                }
            }

            var isFreeModel = IsSelectedModelFree;

            // Phase 1: scan with free models only.
            // Explicit pick → single-element chain; Auto → all free vision models from catalog.
            // Paid consent is never asked upfront — only after free models are actually exhausted.
            var (freeChain, cheapestPaid) = await BuildFreeChainAsync();

            if (freeChain.Count == 0)
            {
                IsScanning = false;
                _scanCts = null;
                ErrorMessage = "No usable models available. Check your network connection or pick a model manually.";
                return;
            }

            // Auto mode tracks per-card exhaustion; explicit picks go straight to Error on failure.
            var freeChainOnly = SelectedModel == null || SelectedModel.IsAuto;

            // For free chains, force concurrency to 1 to respect rate limits.
            var maxConcurrency = isFreeModel ? 1 : MaxConcurrentScans;

            _logger.LogInformation("Starting bulk scan of {Count} cards with model chain {Models} (max concurrency {Concurrency})",
                pending.Count, string.Join(",", freeChain), maxConcurrency);

            // Start error tracking session — log the head model for context.
            _errorLogger.StartSession(pending.Count, freeChain[0]);

            // Create semaphore to limit concurrent scans (Moss Machine pattern)
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            // Process all items concurrently with semaphore limiting. Pass the
            // CTS through explicitly so ProcessItemAsync doesn't read the
            // nullable _scanCts field (eliminates the CS8602 suppression that
            // used to wrap the whole method body). Capture the Token as a
            // separate value: CancellationToken is a struct, so reading
            // IsCancellationRequested keeps working even if the source CTS gets
            // disposed before the final notification fires (real crash seen
            // when the VM was disposed mid-scan and the user kicked off a
            // rescan as the prior scan's tail still ran).
            var cts = _scanCts;
            var scanToken = cts.Token;
            var currentScanDepth = ScanDepth;
            var tasks = pending.Select(item => ProcessItemAsync(item, semaphore, settings, freeChain, isFreeModel, currentScanDepth, cts, freeChainOnly));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Bulk scan cancelled by user");
            }

            // Phase 2: If auto-mode left FreeFailed items, ask once for paid consent.
            // Only offered after ALL free models are exhausted — never upfront.
            if (freeChainOnly && !IsRateLimitPaused && !cts.Token.IsCancellationRequested)
            {
                var failedItems = Items.Where(i => i.Status == BulkScanStatus.FreeFailed).ToList();
                if (failedItems.Count > 0 && cheapestPaid != null)
                {
                    // Surface a toast so the user notices on other tabs that
                    // free models are exhausted and a paid prompt is incoming.
                    _notificationService?.NotifyFreeModelsExhausted(failedItems.Count);
                    StatusMessage = $"Asking about paid fallback for {failedItems.Count} card(s)...";
                    // Pull the full paid-vision-model list so the user can pick a
                    // different model than the suggested cheapest if they prefer.
                    var paidCatalog = (await _modelCatalog.GetAsync()).PaidVisionModels;
                    var chosenPaid = await _consentService.AskAsync(
                        paidCatalog,
                        cheapestPaid,
                        $"{failedItems.Count} card(s) exhausted all {freeChain.Count} free model(s). " +
                        $"Pick a paid model to retry with — or cancel to leave the cards as failed.\n\n" +
                        $"⚠️ {failedItems.Count} paid scan(s) will be charged at the selected model's rate.");

                    if (chosenPaid != null)
                    {
                        _logger.LogInformation("Paid consent granted; retrying {Count} FreeFailed items with {Model}.",
                            failedItems.Count, chosenPaid.Id);
                        StatusMessage = $"Retrying {failedItems.Count} card(s) with {chosenPaid.DisplayName}...";
                        foreach (var item in failedItems)
                        {
                            item.Status = BulkScanStatus.Pending;
                            item.ErrorMessage = null;
                        }
                        var paidChain = new List<string> { chosenPaid.Id };
                        using var paidSemaphore = new SemaphoreSlim(MaxConcurrentScans, MaxConcurrentScans);
                        var retryTasks = failedItems.Select(item =>
                            ProcessItemAsync(item, paidSemaphore, settings, paidChain, false, currentScanDepth, cts, freeChainOnly: false));
                        try { await Task.WhenAll(retryTasks); }
                        catch (OperationCanceledException) { }
                    }
                    else
                    {
                        foreach (var item in failedItems)
                        {
                            item.Status = BulkScanStatus.Error;
                            item.ErrorMessage = "All free models exhausted — paid fallback declined.";
                            _errorLogger.LogError(item.Index, item.FrontImagePath, item.BackImagePath,
                                new Exception(item.ErrorMessage), freeChain[0]);
                        }
                    }
                }
                else if (failedItems.Count > 0)
                {
                    // No paid models available — convert to errors
                    foreach (var item in failedItems)
                    {
                        item.Status = BulkScanStatus.Error;
                        item.ErrorMessage = "All free models exhausted — no paid models available.";
                    }
                }
            }

            // Ensure any remaining FreeFailed (e.g. scan cancelled during phase 1) become errors.
            foreach (var item in Items.Where(i => i.Status == BulkScanStatus.FreeFailed).ToList())
            {
                item.Status = BulkScanStatus.Error;
                item.ErrorMessage = "All free models exhausted — scan cancelled before paid fallback was offered.";
            }

            var scanned = Items.Count(i => i.Status == BulkScanStatus.Scanned);
            var errors = Items.Count(i => i.Status == BulkScanStatus.Error);
            var rateLimited = Items.Count(i => i.Status == BulkScanStatus.RateLimited);

            // Get log path BEFORE ending session (which clears the path)
            var logPath = _errorLogger.GetCurrentLogFilePath();

            // End error tracking session and generate summary
            await _errorLogger.EndSessionAsync();

            IsScanning = false;
            _scanCts = null;
            StatusMessage = null;

            // Notify even when the user is on another tab so they know the batch is done.
            // Use the captured Token (struct) — by this point the source CTS may have
            // been disposed by VM.Dispose() if the user navigated away, and reading
            // .Token on a disposed CTS throws ObjectDisposedException.
            if (!scanToken.IsCancellationRequested)
                _notificationService?.NotifyBulkScanComplete(scanned, errors);

            if (IsRateLimitPaused)
            {
                ErrorMessage = $"Scanned {scanned} cards, then hit the daily OpenRouter rate limit. " +
                               $"{rateLimited} card(s) pending. Add credits at openrouter.ai, then click Resume.";
            }
            else if (errors > 0)
            {
                if (!string.IsNullOrEmpty(logPath))
                    ErrorMessage = $"Scanned {scanned} cards, {errors} failed.\n\nError log saved to:\n{logPath}";
                else
                    ErrorMessage = $"Scanned {scanned} cards, {errors} failed";
            }
            else
                SuccessMessage = $"Scanned {scanned} cards successfully";
        }

        /// <summary>
        /// Returns the free-only model chain plus the cheapest paid model (for Phase 2
        /// consent, if needed). Explicit picks return a single-element chain with no
        /// paid model — those fail as errors, not FreeFailed, when the chain is exhausted.
        /// </summary>
        private async Task<(IReadOnlyList<string> FreeChain, OpenRouterModel? CheapestPaid)> BuildFreeChainAsync()
        {
            if (SelectedModel != null && !SelectedModel.IsAuto)
                return (new[] { SelectedModel.Value }, null);

            var catalog = await _modelCatalog.GetAsync();
            if (catalog.IsEmpty) return (Array.Empty<string>(), null);

            var chain = catalog.FreeVisionModels.Select(m => m.Id).ToList();
            // Prefer schema-capable for the suggestion since the picker now
            // shows the full schema-capable list anyway. Fall back to any paid
            // model if nothing schema-capable is in the catalog (deprecated
            // models, fetch failure).
            var cheapestPaid = catalog.PaidVisionModels.FirstOrDefault(m => m.SupportsJsonSchema)
                ?? (catalog.PaidVisionModels.Count > 0 ? catalog.PaidVisionModels[0] : null);
            return (chain, cheapestPaid);
        }

        private async Task ProcessItemAsync(
            BulkScanItem item,
            SemaphoreSlim semaphore,
            AppSettings settings,
            IReadOnlyList<string> modelChain,
            bool isFreeModel,
            ScanDepth scanDepth,
            CancellationTokenSource cts,
            bool freeChainOnly = false)
        {
            // Wait for semaphore slot (rate limiting)
            await semaphore.WaitAsync(cts.Token);

            try
            {
                if (cts.Token.IsCancellationRequested)
                    return;

                item.Status = BulkScanStatus.Scanning;
                StatusMessage = $"Scanning card {item.Index} of {ScanTotal}...";
                _logger.LogInformation("Scanning card {Index}: {Path}", item.Index, item.FrontImagePath);

                // OCR pre-pass — runs Windows OCR silently before the LLM so the
                // model gets a rich OcrHint anchored on player / brand / manufacturer
                // pulled from the physical card text. Without this, the parallel
                // candidate provider sees no manufacturer signal and falls back to
                // universal-only entries — the LLM has to guess the brand from
                // visual cues alone, which is exactly what was failing on parallels.
                // Best-effort: failure here just means scanning without a hint.
                OcrHint? ocrHint = null;
                if (_ocrService.IsAvailable)
                {
                    try
                    {
                        var ocrResult = await _ocrService.ScanCardAsync(item.FrontImagePath, item.BackImagePath);
                        ocrHint = _playerDirectory?.IsReady == true
                            ? _playerDirectory.BuildHintFromCard(ocrResult.Card)
                            : new OcrHint
                            {
                                PlayerName = ocrResult.Card.PlayerName,
                                Year = ocrResult.Card.Year,
                                Manufacturer = ocrResult.Card.Manufacturer,
                                Brand = ocrResult.Card.Brand,
                                SetName = ocrResult.Card.SetName,
                            };
                        ocrHint.AllVisibleText = ocrResult.AllVisibleText ?? new System.Collections.Generic.List<string>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "OCR pre-pass failed for card {Index}; LLM will scan without hint.", item.Index);
                    }
                }

                try
                {
                    // Walk the model chain — first model that succeeds wins. If every model
                    // throws, the outer catch records the final error.
                    // AccountPerDay rate limits are re-thrown so the outer catch can pause.
                    ScanResult? scanResult = null;
                    Exception? lastError = null;
                    string usedModel = modelChain[0];
                    foreach (var modelId in modelChain)
                    {
                        try
                        {
                            scanResult = await _scannerService.ScanCardAsync(
                                item.FrontImagePath, item.BackImagePath, modelId,
                                scanDepth: scanDepth, ocrHint: ocrHint, ct: cts.Token);
                            usedModel = modelId;
                            break;
                        }
                        catch (OpenRouterRateLimitException rlEx)
                            when (rlEx.Scope == RateLimitScope.AccountPerDay)
                        {
                            throw; // propagate to outer catch — pauses the whole run
                        }
                        catch (OperationCanceledException)
                        {
                            throw; // user cancelled — don't silently walk to next model
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            _logger.LogWarning("Card {Index}: model {Model} failed ({Reason}), trying next.",
                                item.Index, modelId, ex.Message);
                        }
                    }
                    if (scanResult != null)
                    {
                        scanResult.Card.ImagePathFront = item.FrontImagePath;
                        if (!string.IsNullOrEmpty(item.BackImagePath))
                            scanResult.Card.ImagePathBack = item.BackImagePath;

                        item.CardDetail = CardDetailViewModel.FromCard(scanResult.Card);

                        // Run verification pipeline if enabled (same as regular Scan view)
                        if (settings.EnableVariationVerification && item.CardDetail != null)
                        {
                            try
                            {
                                var verification = await _variationVerifier.VerifyCardAsync(scanResult, item.FrontImagePath);

                                // Run confirmation pass if needed and enabled
                                if (settings.RunConfirmationPass && _variationVerifier.NeedsConfirmationPass(verification))
                                {
                                    verification = await _variationVerifier.RunConfirmationPassAsync(scanResult, verification, item.FrontImagePath);
                                }

                                // Auto-apply high-confidence suggestions if enabled
                                if (settings.AutoApplyHighConfidenceSuggestions)
                                {
                                    if (verification.SuggestedPlayerName != null &&
                                        verification.PlayerVerified == false &&
                                        verification.FieldConfidences.Any(f =>
                                            f.FieldName == "player_name" &&
                                            f.Confidence == VerificationConfidence.Conflict))
                                    {
                                        item.CardDetail.PlayerName = verification.SuggestedPlayerName;
                                    }

                                    if (verification.SuggestedVariation != null &&
                                        verification.OverallConfidence != VerificationConfidence.Conflict)
                                    {
                                        item.CardDetail.ParallelName = verification.SuggestedVariation;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Verification failed for card {Index}, using unverified scan", item.Index);
                            }
                        }

                        item.DisplayName = !string.IsNullOrEmpty(item.CardDetail?.PlayerName)
                            ? item.CardDetail.PlayerName
                            : $"Card {item.Index}";
                        item.Status = BulkScanStatus.Scanned;
                        _logger.LogInformation("Successfully scanned card {Index}: {PlayerName}", item.Index, item.DisplayName);

                        // Log success for tracking
                        _errorLogger.LogSuccess(item.Index, item.FrontImagePath, item.DisplayName);
                    }
                    else if (freeChainOnly)
                    {
                        // All free models exhausted — defer to Phase 2 paid-consent prompt.
                        item.Status = BulkScanStatus.FreeFailed;
                        item.ErrorMessage = lastError?.Message ?? "All free models exhausted.";
                        _logger.LogWarning("Card {Index}: all free models exhausted — will prompt for paid tier after free run.", item.Index);
                    }
                    else
                    {
                        throw lastError ?? new Exception("All models in chain failed.");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // propagate to Task.WhenAll so ScanAllAsync sees the cancel
                }
                catch (OpenRouterRateLimitException rlEx)
                    when (rlEx.Scope == RateLimitScope.AccountPerDay)
                {
                    _logger.LogError("Daily OpenRouter rate limit reached on card {Index}. Pausing bulk scan.", item.Index);
                    item.Status = BulkScanStatus.RateLimited;
                    item.ErrorMessage = "Daily OpenRouter rate limit reached. Add credits, then click Resume.";
                    IsRateLimitPaused = true;
                    RateLimitBannerMessage =
                        "Daily OpenRouter rate limit reached. Add credits at openrouter.ai, then click Resume to continue.";
                    // Toast in addition to the inline banner — surfaces the
                    // problem when the user is on a different tab.
                    _notificationService?.NotifyRateLimit(rlEx.ModelId, rlEx.Scope, rlEx.RetryAfterSeconds);
                    cts.Cancel(); // stop remaining pending items
                }
                catch (OpenRouterPaymentRequiredException pEx)
                {
                    // 402 — credits exhausted. Mark the card as errored, fire
                    // a sticky red toast, and stop the batch (further attempts
                    // will only multiply the symptom).
                    _logger.LogError("OpenRouter Payment Required on card {Index}. Stopping bulk scan.", item.Index);
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = pEx.Message;
                    _errorLogger.LogError(item.Index, item.FrontImagePath, item.BackImagePath, pEx, modelChain[0]);
                    _notificationService?.NotifyPaymentRequired(pEx.ModelId, pEx.ResponseBody);
                    cts.Cancel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scan card {Index}: {Path}", item.Index, item.FrontImagePath);
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = ex.Message;

                    // Log detailed error for tracking
                    _errorLogger.LogError(item.Index, item.FrontImagePath, item.BackImagePath, ex, modelChain[0]);
                }

                // Thread-safe increment via a dedicated counter so we don't
                // mutate the source-generator-managed _scanProgress backing
                // field. Then publish the new value through the public setter
                // so PropertyChanged fires correctly on the UI thread later.
                var newCount = Interlocked.Increment(ref _completedCount);
                ScanProgress = newCount;

                // Add delay ONLY for free models to avoid rate limiting
                // For paid models, the semaphore already limits concurrency
                if (isFreeModel && !cts.Token.IsCancellationRequested)
                {
                    StatusMessage = "Waiting 4 seconds to avoid free tier rate limits...";
                    _logger.LogInformation("Waiting 4 seconds before releasing semaphore slot to avoid rate limits...");
                    await Task.Delay(4000, cts.Token);
                }
            }
            finally
            {
                // Release semaphore slot
                semaphore.Release();
            }
        }

        [RelayCommand]
        private void CancelScan()
        {
            if (_scanCts == null || _scanCts.IsCancellationRequested) return;
            StatusMessage = "Cancelling scan — waiting for current requests to finish...";
            _scanCts.Cancel();
        }

        /// <summary>
        /// Resets rate-limited items to Pending and restarts the scan.
        /// Called after the user adds OpenRouter credits or waits for the daily reset.
        /// </summary>
        [RelayCommand]
        private async Task ResumeBulkScanAsync()
        {
            foreach (var item in Items.Where(i => i.Status == BulkScanStatus.RateLimited))
                item.Status = BulkScanStatus.Pending;

            IsRateLimitPaused = false;
            RateLimitBannerMessage = null;

            await ScanAllAsync();
        }

        private async Task ScanAllOcrAsync(List<BulkScanItem> pending)
        {
            ScanProgress = 0;
            Interlocked.Exchange(ref _completedCount, 0);
            ScanTotal = pending.Count;
            _scanCts = new CancellationTokenSource();
            var cts = _scanCts;

            foreach (var item in pending)
            {
                if (cts.Token.IsCancellationRequested) break;
                item.Status = BulkScanStatus.Scanning;
                StatusMessage = $"OCR scanning card {item.Index} of {ScanTotal}...";
                try
                {
                    var result = await _ocrService.ScanCardAsync(item.FrontImagePath, item.BackImagePath);
                    result.Card.ImagePathFront = item.FrontImagePath;
                    if (!string.IsNullOrEmpty(item.BackImagePath))
                        result.Card.ImagePathBack = item.BackImagePath;
                    item.CardDetail = CardDetailViewModel.FromCard(result.Card);
                    item.DisplayName = !string.IsNullOrEmpty(item.CardDetail?.PlayerName)
                        ? item.CardDetail.PlayerName : $"Card {item.Index}";
                    item.ScanMode = ScanMode.Ocr;
                    item.Confidences = result.Confidences ?? new List<FieldConfidence>();
                    item.OcrText = result.AllVisibleText ?? new List<string>();
                    item.Status = BulkScanStatus.Scanned;
                    _logger.LogInformation("OCR scan succeeded for card {Index}: {Name}", item.Index, item.DisplayName);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "OCR scan failed for card {Index}", item.Index);
                }

                var newCount = Interlocked.Increment(ref _completedCount);
                ScanProgress = newCount;
            }

            _scanCts = null;
            StatusMessage = null;

            var scanned = Items.Count(i => i.Status == BulkScanStatus.Scanned);
            var errors = Items.Count(i => i.Status == BulkScanStatus.Error);
            OnPropertyChanged(nameof(HasOcrScannedItems));

            if (errors > 0)
                ErrorMessage = $"OCR scanned {scanned} cards, {errors} failed.";
            else
                SuccessMessage = $"OCR scanned {scanned} cards successfully.";
        }

        [RelayCommand(CanExecute = nameof(CanEnhance))]
        private async Task EnhanceAllAsync()
        {
            var ocrItems = Items
                .Where(i => i.ScanMode == ScanMode.Ocr && i.Status == BulkScanStatus.Scanned)
                .ToList();
            await RunBulkEnhanceAsync(ocrItems);
        }

        /// <summary>
        /// Enhances every CHECKED OCR-scanned card. If nothing is checked,
        /// the button is hidden (CanEnhanceSelected returns false). The
        /// previous behavior — single-select via ListBox.SelectedItem —
        /// only ever processed one card at a time even when the user wanted
        /// a subset; the new model is "tick the rows you want, click Enhance Selected."
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanEnhanceSelected))]
        private async Task EnhanceSelectedAsync()
        {
            var picked = SelectedOcrItems;
            if (picked.Count == 0) return;
            await RunBulkEnhanceAsync(picked);
        }

        /// <summary>OCR-scanned items the user has CHECKED (separate from ListBox focus).</summary>
        public List<BulkScanItem> SelectedOcrItems =>
            Items.Where(i => i.IsSelected && i.ScanMode == ScanMode.Ocr && i.Status == BulkScanStatus.Scanned)
                 .ToList();

        public bool HasSelectedOcrItems => SelectedOcrItems.Count > 0;

        private bool CanEnhance() => !IsEnhancing && HasOcrScannedItems;
        private bool CanEnhanceSelected() => !IsEnhancing && HasSelectedOcrItems;

        // The Items collection's per-item PropertyChanged events don't bubble up
        // to the parent VM, so HasSelectedOcrItems / HasOcrScannedItems wouldn't
        // re-evaluate when an item's IsSelected / Status / ScanMode flips.
        // Subscribe to each item's PropertyChanged in the constructor (see ctor)
        // and re-broadcast when relevant fields change.
        internal void OnBulkScanItemPropertyChanged(string? propName)
        {
            if (propName is nameof(BulkScanItem.IsSelected)
                or nameof(BulkScanItem.Status)
                or nameof(BulkScanItem.ScanMode))
            {
                OnPropertyChanged(nameof(HasSelectedOcrItems));
                OnPropertyChanged(nameof(HasOcrScannedItems));
                EnhanceSelectedCommand.NotifyCanExecuteChanged();
                EnhanceAllCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private void CancelEnhance()
        {
            if (_enhanceCts == null || _enhanceCts.IsCancellationRequested) return;
            StatusMessage = "Cancelling enhance — waiting for current request to finish...";
            _enhanceCts.Cancel();
        }

        /// <summary>
        /// Builds the OCR hint sent to the LLM during Enhance. Catalog-anchored
        /// fields (player name matched against the checklist, brand normalized,
        /// sport inferred from team, year validated, etc.) are marked
        /// "verified" so the LLM echoes them verbatim and spends its token
        /// budget on visual-pattern fields it actually has to look at the image
        /// for (parallel pattern, refractor type, foil, border, etc.). Other
        /// populated fields ride along as soft suggestions the LLM can override.
        /// </summary>
        private static OcrHint BuildEnhanceHint(BulkScanItem item)
        {
            var d = item.CardDetail;
            var hint = new OcrHint
            {
                PlayerName = d?.PlayerName,
                Year = d?.Year,
                CardNumber = d?.CardNumber,
                Manufacturer = d?.Manufacturer,
                Brand = d?.Brand,
                SetName = d?.SetName,
                Team = d?.Team,
                Sport = d?.Sport?.ToString(),
                ParallelName = d?.ParallelName,
                SerialNumbered = d?.SerialNumbered,
                IsRookie = d?.IsRookie,
                IsAuto = d?.IsAuto,
                IsRelic = d?.IsRelic,
                IsGraded = d?.IsGraded,
                GradeCompany = d?.GradeCompany,
                GradeValue = d?.GradeValue,
                AllVisibleText = item.OcrText.ToList(),
            };

            // Promote any High/Medium-confidence field from the OCR scan to
            // "verified" status — the LLM will echo these. Low-confidence
            // values stay as suggestive (LLM may override). Field names match
            // the JSON schema keys the prompt uses.
            foreach (var conf in item.Confidences)
            {
                if (conf.Confidence == VerificationConfidence.High
                    || conf.Confidence == VerificationConfidence.Medium)
                {
                    hint.VerifiedFieldNames.Add(conf.FieldName);
                }
            }
            return hint;
        }

        private async Task RunBulkEnhanceAsync(List<BulkScanItem> items)
        {
            if (items.Count == 0) return;

            IsEnhancing = true;
            EnhanceProgress = 0;
            EnhanceTotal = items.Count;
            ErrorMessage = null;
            SuccessMessage = null;
            _enhanceCts = new CancellationTokenSource();
            var ct = _enhanceCts.Token;

            var (freeChain, _) = await BuildFreeChainAsync();
            if (freeChain.Count == 0)
                freeChain = new[] { OpenRouterModelDefaults.DefaultFreeModelId };

            int succeeded = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    var item = items[i];
                    if (item.CardDetail == null) { EnhanceProgress++; continue; }

                    item.Status = BulkScanStatus.Enhancing;
                    CurrentEnhanceItem = item;
                    StatusMessage = $"Enhancing \"{item.DisplayName}\"...";
                    _logger.LogInformation("Enhancing card {Index} ({Name}) of {Total}", i + 1, item.DisplayName, EnhanceTotal);

                    try
                    {
                        // Build a rich OcrHint from the post-validation Card. The
                        // OCR pass already wrote High/Medium-confidence values for
                        // catalog-anchored fields (player matched against checklist,
                        // brand normalized, sport inferred from team, etc.); marking
                        // those as "verified" tells the LLM to echo them rather than
                        // re-derive from the image. Low-confidence values are still
                        // sent as suggestions so the LLM has context.
                        var hint = BuildEnhanceHint(item);

                        ScanResult? result = null;
                        foreach (var modelId in freeChain)
                        {
                            if (ct.IsCancellationRequested) break;
                            CurrentEnhanceModel = modelId;
                            try
                            {
                                result = await _scannerService.ScanCardAsync(
                                    item.FrontImagePath, item.BackImagePath, modelId,
                                    scanDepth: ScanDepth.Standard, ocrHint: hint, ct: ct);
                                break;
                            }
                            catch (OpenRouterRateLimitException rlEx)
                                when (rlEx.Scope == RateLimitScope.AccountPerDay)
                            {
                                item.Status = BulkScanStatus.Scanned; // restore — still has OCR data
                                ErrorMessage = "Daily rate limit hit during enhance. Add credits and try again.";
                                return;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Enhance: model {Model} failed for card {Index}, trying next.", modelId, item.Index);
                            }
                        }

                        if (result != null)
                        {
                            result.Card.ImagePathFront = item.FrontImagePath;
                            if (!string.IsNullOrEmpty(item.BackImagePath))
                                result.Card.ImagePathBack = item.BackImagePath;
                            item.CardDetail = CardDetailViewModel.FromCard(result.Card);
                            item.DisplayName = !string.IsNullOrEmpty(item.CardDetail?.PlayerName)
                                ? item.CardDetail.PlayerName : $"Card {item.Index}";
                            item.ScanMode = ScanMode.Ai;
                            item.Confidences = result.Confidences ?? new List<FieldConfidence>();
                            item.Status = BulkScanStatus.Scanned;
                            item.ErrorMessage = null;
                            succeeded++;
                        }
                        else
                        {
                            _logger.LogWarning("Enhance: all models exhausted for card {Index}.", item.Index);
                            item.Status = BulkScanStatus.Scanned; // keep OCR data
                            item.ErrorMessage = "Enhance failed — all free models exhausted.";
                            failed++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = BulkScanStatus.Scanned; // restore — still has OCR data
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Enhance failed for card {Index}.", item.Index);
                        item.Status = BulkScanStatus.Scanned; // restore — still has OCR data
                        item.ErrorMessage = $"Enhance error: {ex.Message}";
                        failed++;
                    }

                    EnhanceProgress++;
                    OnPropertyChanged(nameof(HasOcrScannedItems));

                    if (i < items.Count - 1 && !ct.IsCancellationRequested)
                    {
                        try { await Task.Delay(2000, ct); }
                        catch (OperationCanceledException) { break; }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Bulk enhance cancelled by user after {Count} of {Total}.", EnhanceProgress, EnhanceTotal);
            }
            finally
            {
                IsEnhancing = false;
                StatusMessage = null;
                CurrentEnhanceItem = null;
                CurrentEnhanceModel = null;
                _enhanceCts?.Dispose();
                _enhanceCts = null;
                OnPropertyChanged(nameof(HasOcrScannedItems));
            }

            if (ct.IsCancellationRequested)
                ErrorMessage = $"Enhance cancelled after {succeeded} card(s). {items.Count - EnhanceProgress} skipped.";
            else if (failed > 0)
                ErrorMessage = $"Enhanced {succeeded} of {items.Count} card(s). {failed} failed — see card details for messages.";
            else
                SuccessMessage = $"Enhanced {succeeded} card(s) with AI.";
        }

        [RelayCommand]
        private async Task SaveAllAsync()
        {
            var ready = Items.Where(i => i.Status == BulkScanStatus.Scanned && i.CardDetail != null).ToList();
            if (ready.Count == 0)
                return;

            IsSaving = true;
            ErrorMessage = null;
            SuccessMessage = null;
            int saved = 0;

            var isSurpriseSetDestination =
                Destination == BulkScanDestination.SurpriseSet && DestinationSurpriseSetId.HasValue;

            foreach (var item in ready)
            {
                try
                {
                    var card = item.CardDetail!.ToCard();
                    card.ImagePathFront = item.FrontImagePath;
                    card.ImagePathBack = item.BackImagePath;
                    card.DataSource = item.ScanMode == ScanMode.Ocr ? CardDataSource.Ocr : CardDataSource.Ai;

                    WhatnotCategoryDefaulter.ApplyDefaults(card);
                    await TryUploadMissingUrlsAsync(card);

                    if (isSurpriseSetDestination)
                    {
                        // Skip individual-listing price evaluation — the set handles revenue allocation.
                        card.Status = CardStatus.ReservedForSet;
                        await _cardRepository.InsertCardAsync(card);
                        await _surpriseSetRepository.AddCardAsync(DestinationSurpriseSetId!.Value, card);
                    }
                    else
                    {
                        card.Status = CardStatusEvaluator.Evaluate(card);
                        await _cardRepository.InsertCardAsync(card);
                    }

                    item.Status = BulkScanStatus.Saved;
                    saved++;
                }
                catch (Exception ex)
                {
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = ex.Message;
                }
            }

            IsSaving = false;
            SuccessMessage = isSurpriseSetDestination
                ? $"Added {saved} cards to Surprise Set!"
                : $"Saved {saved} cards to My Cards!";

            // Refresh the directory cache so the newly-saved cards' player /
            // brand / team / set / year values are immediately available to
            // future OCR scans in the same session — the directory grows from
            // real usage, not just bootstrap data and imports.
            if (saved > 0 && _playerDirectory != null)
            {
                _ = _playerDirectory.RefreshAsync();
            }
        }

        /// <summary>
        /// Uploads any local image paths that don't yet have a corresponding hosted URL.
        /// Updates the card's <c>ImageUrl{N}</c> fields in place. Network errors are
        /// swallowed — the card still saves with whatever URLs were obtained.
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
                _logger.LogWarning(ex, "Image upload during bulk save failed for card.");
            }
        }

        [RelayCommand]
        private void RemoveSelected()
        {
            if (SelectedItem == null)
                return;

            Items.Remove(SelectedItem);
            SelectedItem = null;

            // Re-index
            for (int i = 0; i < Items.Count; i++)
                Items[i].Index = i + 1;
        }

        [RelayCommand]
        private void ClearAll()
        {
            Items.Clear();
            SelectedItem = null;
            ErrorMessage = null;
            SuccessMessage = null;
            ScanProgress = 0;
            ScanTotal = 0;
        }

        public void Dispose()
        {
            // Cancel any running scan operation
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = null;

            _enhanceCts?.Cancel();
            _enhanceCts?.Dispose();
            _enhanceCts = null;
        }
    }

    public enum BulkScanStatus
    {
        Pending,
        Scanning,
        Scanned,
        Saved,
        Error,
        FreeFailed,  // All free models exhausted — awaiting paid-consent prompt at end of phase 1
        RateLimited, // Daily limit hit — reset to Pending via ResumeBulkScan
        Enhancing,   // OCR-scanned card currently being re-scanned through AI
    }

    public enum BulkScanDestination
    {
        Inventory,   // Save as standalone cards in My Cards (default)
        SurpriseSet, // Add directly to a specific Surprise Set
    }

    public record DestinationOption(BulkScanDestination Value, string Label);
    public record ScanDepthOption(ScanDepth Value, string Label);
    public record ScanModeOption(ScanMode Value, string Label);

    public partial class BulkScanItem : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private string _displayName = "Pending";
        [ObservableProperty] private BulkScanStatus _status = BulkScanStatus.Pending;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private CardDetailViewModel? _cardDetail;

        // Per-row checkbox state for multi-select Enhance / batch operations.
        // ListBox SelectedItem still drives which card is shown in the detail
        // pane; IsSelected is independent so the user can check several cards
        // without losing the detail focus.
        [ObservableProperty] private bool _isSelected;

        // ScanMode used to be a plain auto-property — changes never fired
        // PropertyChanged, so the XAML binding `SelectedItem.IsOcrScanned`
        // got stuck at the value at-selection time and the Enhance button
        // never appeared after a fresh OCR scan completed. Promoting it to
        // an ObservableProperty + cascading the notification to IsOcrScanned
        // / ShowLowConfidenceBanner fixes that.
        [ObservableProperty] private ScanMode _scanMode = ScanMode.Ai;

        public string FrontImagePath { get; set; } = string.Empty;
        public string? BackImagePath { get; set; }

        public List<FieldConfidence> Confidences { get; set; } = new();

        /// <summary>Raw OCR text captured at scan time. Surfaced in the enhance ticker.</summary>
        public List<string> OcrText { get; set; } = new();
        public string OcrTextPreview =>
            OcrText.Count == 0 ? string.Empty : string.Join("\n", OcrText.Take(20));

        public bool IsOcrScanned => ScanMode == ScanMode.Ocr && Status == BulkScanStatus.Scanned;

        public bool ShowLowConfidenceBanner =>
            ScanMode == ScanMode.Ocr &&
            Confidences.Count > 0 &&
            Confidences.Count(c => c.Confidence == VerificationConfidence.Low) > Confidences.Count / 2;

        partial void OnStatusChanged(BulkScanStatus value)
        {
            OnPropertyChanged(nameof(IsOcrScanned));
            OnPropertyChanged(nameof(ShowLowConfidenceBanner));
        }

        partial void OnScanModeChanged(ScanMode value)
        {
            OnPropertyChanged(nameof(IsOcrScanned));
            OnPropertyChanged(nameof(ShowLowConfidenceBanner));
        }
    }
}
