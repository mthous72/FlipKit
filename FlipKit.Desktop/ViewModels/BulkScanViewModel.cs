using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FlipKit.Core.Helpers;

namespace FlipKit.Desktop.ViewModels
{
    public partial class BulkScanViewModel : ViewModelBase, IDisposable
    {
        private readonly IScannerService _scannerService;
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

        // Rate-limit banners
        [ObservableProperty] private bool _isRateLimitPaused;
        [ObservableProperty] private string? _rateLimitBannerMessage;

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
        }

        partial void OnIsRateLimitPausedChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowFreeModelBanner));
        }

        partial void OnDestinationChanged(BulkScanDestination value)
        {
            // Surprise Set bulk scans default to Quick depth — just enough to label each slot.
            // The user can override after the fact from the Inventory view.
            ScanDepth = value == BulkScanDestination.SurpriseSet ? ScanDepth.Quick : ScanDepth.Standard;
            OnPropertyChanged(nameof(SelectedDestinationOption));
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
        }

        public BulkScanViewModel(
            IScannerService scannerService,
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
            ILogger<BulkScanViewModel> logger)
        {
            _scannerService = scannerService;
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
            _logger = logger;

            // Initialize from settings
            var settings = _settingsService.Load();
            _maxConcurrentScans = settings.MaxConcurrentScans;

            _ = LoadModelsAsync();
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
                if (choice == null && !string.IsNullOrWhiteSpace(savedId) && savedId != ModelOption.AutoValue)
                {
                    choice = ModelOption.Stale(savedId);
                    ModelOptions.Add(choice);
                }
                SelectedModel = choice ?? ModelOptions.First();

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

            var pending = Items.Where(i => i.Status == BulkScanStatus.Pending).ToList();
            if (pending.Count == 0)
                return;

            IsScanning = true;
            IsRateLimitPaused = false;
            RateLimitBannerMessage = null;
            ErrorMessage = null;
            SuccessMessage = null;
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

            // Resolve the model chain once for the whole bulk run.
            //   • Explicit pick → single-model chain.
            //   • Auto + paid not allowed → free-only chain.
            //   • Auto + paid allowed → free chain + cheapest paid as final fallback.
            var modelChain = await BuildBulkModelChainAsync();
            if (modelChain == null)
            {
                // User declined paid consent; cancel cleanly.
                IsScanning = false;
                _scanCts = null;
                StatusMessage = null;
                SuccessMessage = "Bulk scan canceled — no paid model was used.";
                return;
            }

            if (modelChain.Count == 0)
            {
                IsScanning = false;
                _scanCts = null;
                ErrorMessage = "No usable models available. Check your network connection or pick a model manually.";
                return;
            }

            // For free chains, force concurrency to 1 to respect rate limits.
            var maxConcurrency = isFreeModel ? 1 : MaxConcurrentScans;

            _logger.LogInformation("Starting bulk scan of {Count} cards with model chain {Models} (max concurrency {Concurrency})",
                pending.Count, string.Join(",", modelChain), maxConcurrency);

            // Start error tracking session — log the head model for context.
            _errorLogger.StartSession(pending.Count, modelChain[0]);

            // Create semaphore to limit concurrent scans (Moss Machine pattern)
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            // Process all items concurrently with semaphore limiting. Pass the
            // CTS through explicitly so ProcessItemAsync doesn't read the
            // nullable _scanCts field (eliminates the CS8602 suppression that
            // used to wrap the whole method body).
            var cts = _scanCts;
            var currentScanDepth = ScanDepth;
            var tasks = pending.Select(item => ProcessItemAsync(item, semaphore, settings, modelChain, isFreeModel, currentScanDepth, cts));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Bulk scan cancelled by user");
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
        /// Builds the per-card model chain for a bulk run. Returns null if the user
        /// declined paid consent, an empty list if no usable models exist, or a list
        /// of model ids to try in order otherwise.
        /// </summary>
        private async Task<IReadOnlyList<string>?> BuildBulkModelChainAsync()
        {
            // Explicit pick → trust it; single-element chain.
            if (SelectedModel != null && !SelectedModel.IsAuto)
                return new[] { SelectedModel.Value };

            var catalog = await _modelCatalog.GetAsync();
            if (catalog.IsEmpty) return Array.Empty<string>();

            var chain = catalog.FreeVisionModels.Select(m => m.Id).ToList();

            if (catalog.PaidVisionModels.Count > 0)
            {
                var cheapest = catalog.PaidVisionModels[0];
                var consented = await _consentService.AskAsync(
                    cheapest,
                    $"Bulk scan: when all {catalog.FreeVisionModels.Count} free models fail for a card, " +
                    $"should we try the cheapest paid model ({cheapest.DisplayName}) as a fallback? " +
                    "If you decline, cards that all free models fail on will be marked as errors.");
                if (!consented)
                {
                    // User said no — return null to signal "user canceled the whole run".
                    // (Alternative: continue with free-only and let cards fail. The current
                    // semantics of "Cancel" in the dialog reads as "don't run", so we honor that.)
                    return null;
                }
                chain.Add(cheapest.Id);
            }

            return chain;
        }

        private async Task ProcessItemAsync(
            BulkScanItem item,
            SemaphoreSlim semaphore,
            AppSettings settings,
            IReadOnlyList<string> modelChain,
            bool isFreeModel,
            ScanDepth scanDepth,
            CancellationTokenSource cts)
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
                                scanDepth: scanDepth);
                            usedModel = modelId;
                            break;
                        }
                        catch (OpenRouterRateLimitException rlEx)
                            when (rlEx.Scope == RateLimitScope.AccountPerDay)
                        {
                            throw; // propagate to outer catch — pauses the whole run
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            _logger.LogDebug("Card {Index}: model {Model} failed, trying next.", item.Index, modelId);
                        }
                    }
                    if (scanResult == null) throw lastError ?? new Exception("All models in chain failed.");

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
                catch (OpenRouterRateLimitException rlEx)
                    when (rlEx.Scope == RateLimitScope.AccountPerDay)
                {
                    _logger.LogError("Daily OpenRouter rate limit reached on card {Index}. Pausing bulk scan.", item.Index);
                    item.Status = BulkScanStatus.RateLimited;
                    item.ErrorMessage = "Daily OpenRouter rate limit reached. Add credits, then click Resume.";
                    IsRateLimitPaused = true;
                    RateLimitBannerMessage =
                        "Daily OpenRouter rate limit reached. Add credits at openrouter.ai, then click Resume to continue.";
                    cts.Cancel(); // stop remaining pending items
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
            _scanCts?.Cancel();
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
        }
    }

    public enum BulkScanStatus
    {
        Pending,
        Scanning,
        Scanned,
        Saved,
        Error,
        RateLimited, // Daily limit hit — reset to Pending via ResumeBulkScan
    }

    public enum BulkScanDestination
    {
        Inventory,   // Save as standalone cards in My Cards (default)
        SurpriseSet, // Add directly to a specific Surprise Set
    }

    public record DestinationOption(BulkScanDestination Value, string Label);
    public record ScanDepthOption(ScanDepth Value, string Label);

    public partial class BulkScanItem : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private string _displayName = "Pending";
        [ObservableProperty] private BulkScanStatus _status = BulkScanStatus.Pending;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private CardDetailViewModel? _cardDetail;

        public string FrontImagePath { get; set; } = string.Empty;
        public string? BackImagePath { get; set; }
    }
}
