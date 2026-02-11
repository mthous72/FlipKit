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

namespace FlipKit.Desktop.ViewModels
{
    public partial class BulkScanViewModel : ViewModelBase, IDisposable
    {
        private readonly IScannerService _scannerService;
        private readonly ICardRepository _cardRepository;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsService _settingsService;
        private readonly IVariationVerifier _variationVerifier;
        private readonly IBulkScanErrorLogger _errorLogger;
        private readonly ILogger<BulkScanViewModel> _logger;

        private CancellationTokenSource? _scanCts;

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
        [ObservableProperty] private string _selectedModel = string.Empty;
        [ObservableProperty] private int _maxConcurrentScans = 1;

        public List<string> ModelOptions { get; } = new(OpenRouterScannerService.AllVisionModels);

        // Computed property: is the selected model a free model?
        public bool IsSelectedModelFree => SelectedModel.Contains(":free");

        partial void OnSelectedModelChanged(string value)
        {
            OnPropertyChanged(nameof(IsSelectedModelFree));
            // If switching to free model, force concurrency to 1
            if (IsSelectedModelFree)
            {
                MaxConcurrentScans = 1;
            }
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
            IFileDialogService fileDialogService,
            ISettingsService settingsService,
            IVariationVerifier variationVerifier,
            IBulkScanErrorLogger errorLogger,
            ILogger<BulkScanViewModel> logger)
        {
            _scannerService = scannerService;
            _cardRepository = cardRepository;
            _fileDialogService = fileDialogService;
            _settingsService = settingsService;
            _variationVerifier = variationVerifier;
            _errorLogger = errorLogger;
            _logger = logger;

            // Initialize from settings
            var settings = _settingsService.Load();
            _selectedModel = settings.DefaultModel;
            _maxConcurrentScans = settings.MaxConcurrentScans;
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
            ErrorMessage = null;
            SuccessMessage = null;
            ScanProgress = 0;
            ScanTotal = pending.Count;
            _scanCts = new CancellationTokenSource();

            var settings = _settingsService.Load();
            var isFreeModel = IsSelectedModelFree;

            // For free models, force concurrency to 1 to respect rate limits
            var maxConcurrency = isFreeModel ? 1 : MaxConcurrentScans;

            _logger.LogInformation("Starting bulk scan of {Count} cards with max concurrency {Concurrency}",
                pending.Count, maxConcurrency);

            // Start error tracking session
            _errorLogger.StartSession(pending.Count, SelectedModel);

            // Create semaphore to limit concurrent scans (Moss Machine pattern)
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            // Process all items concurrently with semaphore limiting
            var tasks = pending.Select(item => ProcessItemAsync(item, semaphore, settings, SelectedModel, isFreeModel));

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

            // Get log path BEFORE ending session (which clears the path)
            var logPath = _errorLogger.GetCurrentLogFilePath();

            // End error tracking session and generate summary
            await _errorLogger.EndSessionAsync();

            IsScanning = false;
            _scanCts = null;
            StatusMessage = null;

            if (errors > 0)
            {
                if (!string.IsNullOrEmpty(logPath))
                {
                    ErrorMessage = $"Scanned {scanned} cards, {errors} failed.\n\nError log saved to:\n{logPath}";
                }
                else
                {
                    ErrorMessage = $"Scanned {scanned} cards, {errors} failed";
                }
            }
            else
                SuccessMessage = $"Scanned {scanned} cards successfully";
        }

        private async Task ProcessItemAsync(BulkScanItem item, SemaphoreSlim semaphore, AppSettings settings, string modelToUse, bool isFreeModel)
        {
            // _scanCts is guaranteed non-null when this method is called from ScanAllAsync
#pragma warning disable CS8602
            // Wait for semaphore slot (rate limiting)
            await semaphore.WaitAsync(_scanCts.Token);

            try
            {
                if (_scanCts.Token.IsCancellationRequested)
                    return;

                item.Status = BulkScanStatus.Scanning;
                StatusMessage = $"Scanning card {item.Index} of {ScanTotal}...";
                _logger.LogInformation("Scanning card {Index}: {Path}", item.Index, item.FrontImagePath);

                try
                {
                    var scanResult = await _scannerService.ScanCardAsync(
                        item.FrontImagePath, item.BackImagePath, modelToUse);

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

                    item.DisplayName = !string.IsNullOrEmpty(item.CardDetail.PlayerName)
                        ? item.CardDetail.PlayerName
                        : $"Card {item.Index}";
                    item.Status = BulkScanStatus.Scanned;
                    _logger.LogInformation("Successfully scanned card {Index}: {PlayerName}", item.Index, item.DisplayName);

                    // Log success for tracking
                    _errorLogger.LogSuccess(item.Index, item.FrontImagePath, item.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scan card {Index}: {Path}", item.Index, item.FrontImagePath);
                    item.Status = BulkScanStatus.Error;
                    item.ErrorMessage = ex.Message;

                    // Log detailed error for tracking
                    _errorLogger.LogError(item.Index, item.FrontImagePath, item.BackImagePath, ex, modelToUse);
                }

                // Thread-safe increment of progress
                // Intentionally accessing backing field for Interlocked.Increment
#pragma warning disable MVVMTK0034
                Interlocked.Increment(ref _scanProgress);
#pragma warning restore MVVMTK0034
                OnPropertyChanged(nameof(ScanProgress));

                // Add delay ONLY for free models to avoid rate limiting
                // For paid models, the semaphore already limits concurrency
                if (isFreeModel && !_scanCts.Token.IsCancellationRequested)
                {
                    StatusMessage = "Waiting 4 seconds to avoid free tier rate limits...";
                    _logger.LogInformation("Waiting 4 seconds before releasing semaphore slot to avoid rate limits...");
                    await Task.Delay(4000, _scanCts.Token);
                }
            }
            finally
            {
                // Release semaphore slot
                semaphore.Release();
            }
#pragma warning restore CS8602
        }

        [RelayCommand]
        private void CancelScan()
        {
            _scanCts?.Cancel();
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

            foreach (var item in ready)
            {
                try
                {
                    var card = item.CardDetail!.ToCard();
                    card.ImagePathFront = item.FrontImagePath;
                    card.ImagePathBack = item.BackImagePath;
                    card.Status = CardStatus.Draft;
                    await _cardRepository.InsertCardAsync(card);

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
            SuccessMessage = $"Saved {saved} cards to My Cards!";
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
        Error
    }

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
