using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class ScanViewModel : ViewModelBase
    {
        private readonly IScannerService _scannerService;
        private readonly IOcrService _ocrService;
        private readonly ICardRepository _cardRepository;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsService _settingsService;
        private readonly IVariationVerifier _variationVerifier;
        private readonly IChecklistLearningService _checklistLearningService;
        private readonly IChecklistVerificationMatcher _checklistMatcher;
        private readonly IOpenRouterModelCatalog _modelCatalog;
        private readonly IPaidModelConsentService _consentService;
        private readonly Services.IPaidScanGate _paidScanGate;
        private readonly Services.IAppNotificationService? _notificationService;
        private readonly IModelScoreboard? _scoreboard;
        private readonly IAiScanConsentService _aiScanConsentService;
        private readonly IImageUploadService _imageUploadService;
        private readonly IBrowserService _browserService;
        private readonly IWebcamCaptureDialogService _webcamCaptureDialog;
        private readonly IPlayerNameDirectory? _playerDirectory;
        private readonly ILogger<ScanViewModel> _logger;

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

        // Phase 2 tier-aware verification (Roadmap 1 §8d). When the matcher returns
        // ChecklistMissing the UI shows the Surface B banner; otherwise the tier badge
        // colour-codes the result. ChecklistMatchResult carries the candidate list for
        // the Pick-from-checklist picker (UI lands in a follow-up).
        [ObservableProperty] private ChecklistMatchResult? _checklistMatchResult;
        [ObservableProperty] private bool _hasMissingChecklist;
        [ObservableProperty] private string? _missingChecklistDeeplink;
        [ObservableProperty] private string? _missingChecklistLabel;
        [ObservableProperty] private string _tierBadgeText = string.Empty;
        [ObservableProperty] private string _tierBadgeColor = "#9E9E9E"; // grey when no tier

        // Webcam capture (Roadmap #2). Master toggle read from settings at
        // construction — bound to the 📷 buttons' IsVisible so users can hide
        // them on machines without a working camera.
        [ObservableProperty] private bool _isWebcamEnabled = true;

        // Scan mode
        [ObservableProperty] private ScanMode _scanMode = ScanMode.Ai;
        public bool IsOcrMode => ScanMode == ScanMode.Ocr;
        public bool IsAiMode => ScanMode == ScanMode.Ai;
        public bool IsOcrAvailable => _ocrService.IsAvailable;

        // Pre-save Enhance (OCR-only). Surfaces a button after an OCR scan
        // completes so the user can re-run the LLM with directory-validated
        // values locked, without first having to save the card.
        [ObservableProperty] private bool _isEnhancing;
        [ObservableProperty] private bool _lastScanWasOcr;
        [ObservableProperty] private string? _enhanceMessage;
        public bool CanEnhance => !IsScanning && !IsEnhancing && LastScanWasOcr && ScannedCard != null;

        partial void OnIsEnhancingChanged(bool value) => OnPropertyChanged(nameof(CanEnhance));
        partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(CanEnhance));
        partial void OnLastScanWasOcrChanged(bool value) => OnPropertyChanged(nameof(CanEnhance));
        partial void OnScannedCardChanged(CardDetailViewModel? value) => OnPropertyChanged(nameof(CanEnhance));

        partial void OnScanModeChanged(ScanMode value)
        {
            OnPropertyChanged(nameof(IsOcrMode));
            OnPropertyChanged(nameof(IsAiMode));
        }

        // Model selection
        [ObservableProperty] private ModelOption? _selectedModel;
        [ObservableProperty] private bool _isLoadingModels;
        [ObservableProperty] private string? _modelLoadError;

        public ObservableCollection<ModelOption> ModelOptions { get; } = new();

        // Additional photos (slots 3-8) — uploaded to ImgBB at export time but never sent to the LLM.
        // Cap matches the 6 remaining image columns on Card (front + back occupy slots 1 and 2).
        public ObservableCollection<PhotoSlot> AdditionalPhotos { get; } = new();
        public const int MaxAdditionalPhotos = 6;

        public ScanViewModel(
            IScannerService scannerService,
            IOcrService ocrService,
            ICardRepository cardRepository,
            IFileDialogService fileDialogService,
            ISettingsService settingsService,
            IVariationVerifier variationVerifier,
            IChecklistLearningService checklistLearningService,
            IChecklistVerificationMatcher checklistMatcher,
            IOpenRouterModelCatalog modelCatalog,
            IPaidModelConsentService consentService,
            Services.IPaidScanGate paidScanGate,
            IAiScanConsentService aiScanConsentService,
            IImageUploadService imageUploadService,
            IBrowserService browserService,
            IWebcamCaptureDialogService webcamCaptureDialog,
            ILogger<ScanViewModel> logger,
            IPlayerNameDirectory? playerDirectory = null,
            // Optional so existing test fixtures don't have to wire it up.
            // When unset, billing-error toasts are skipped — the inline
            // ErrorMessage path still surfaces the failure.
            Services.IAppNotificationService? notificationService = null,
            IModelScoreboard? scoreboard = null)
        {
            _scannerService = scannerService;
            _ocrService = ocrService;
            _cardRepository = cardRepository;
            _fileDialogService = fileDialogService;
            _settingsService = settingsService;
            _variationVerifier = variationVerifier;
            _checklistLearningService = checklistLearningService;
            _checklistMatcher = checklistMatcher;
            _modelCatalog = modelCatalog;
            _consentService = consentService;
            _paidScanGate = paidScanGate;
            _notificationService = notificationService;
            _scoreboard = scoreboard;
            _aiScanConsentService = aiScanConsentService;
            _imageUploadService = imageUploadService;
            _browserService = browserService;
            _webcamCaptureDialog = webcamCaptureDialog;
            _playerDirectory = playerDirectory;
            _logger = logger;

            // Honour the master toggle from Settings. Read once — users can re-open
            // the page after toggling to refresh.
            IsWebcamEnabled = settingsService.Load().WebcamCaptureEnabled;

            // Populate the dropdown asynchronously — first call hits OpenRouter, subsequent
            // ones use the cached catalog. Until it lands the dropdown shows a loading entry.
            _ = LoadModelsAsync();
        }

        partial void OnSelectedModelChanged(ModelOption? value)
        {
            if (value == null || value.IsAuto) return;
            var settings = _settingsService.Load();
            if (settings.DefaultModel == value.Value) return;
            settings.DefaultModel = value.Value;
            _settingsService.Save(settings);
        }

        private async Task LoadModelsAsync()
        {
            IsLoadingModels = true;
            ModelLoadError = null;
            try
            {
                var catalog = await _modelCatalog.GetAsync();

                // Attach scoreboard signal — the dropdown shows a per-model
                // quality pill and sorts higher-scoring models to the top.
                IReadOnlyDictionary<string, ModelQuality>? qualities = null;
                if (_scoreboard != null)
                {
                    try { qualities = await _scoreboard.GetQualitiesAsync(); }
                    catch { /* best-effort */ }
                }
                ModelOption WithQuality(OpenRouterModel m) =>
                    ModelOption.FromCatalog(m, qualities != null && qualities.TryGetValue(m.Id, out var q) ? q : null);

                ModelOptions.Clear();
                ModelOptions.Add(ModelOption.Auto());
                var sortedFree = catalog.FreeVisionModels.Select(WithQuality)
                    .OrderByDescending(o => o.QualitySortKey).ToList();
                var sortedPaid = catalog.PaidVisionModels.Select(WithQuality)
                    .OrderByDescending(o => o.QualitySortKey).ToList();
                foreach (var o in sortedFree) ModelOptions.Add(o);
                foreach (var o in sortedPaid) ModelOptions.Add(o);

                // Pick a sensible default: saved settings if it's still in the live catalog;
                // otherwise Auto. Never pre-select a stale/deprecated model — the user
                // would unknowingly scan with it every session until they noticed.
                var savedId = _settingsService.Load().DefaultModel;
                ModelOption? choice = null;
                if (!string.IsNullOrWhiteSpace(savedId) && savedId != ModelOption.AutoValue)
                    choice = ModelOptions.FirstOrDefault(o => o.Value == savedId);
                SelectedModel = choice ?? ModelOptions.First();   // First() = Auto

                if (catalog.IsEmpty)
                    ModelLoadError = "Couldn't reach OpenRouter for the live model list. Auto-rotation is disabled until the catalog loads.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load model catalog");
                ModelLoadError = $"Model catalog failed to load: {ex.Message}";
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
        private async Task CaptureFrontImageAsync()
        {
            var path = await _webcamCaptureDialog.CaptureAsync();
            if (!string.IsNullOrEmpty(path))
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
        private async Task CaptureBackImageAsync()
        {
            var path = await _webcamCaptureDialog.CaptureAsync();
            if (!string.IsNullOrEmpty(path))
                ImagePathBack = path;
        }

        [RelayCommand]
        private void RemoveBackImage()
        {
            ImagePathBack = null;
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
        private void RemoveAdditionalPhoto(PhotoSlot? slot)
        {
            if (slot != null)
                AdditionalPhotos.Remove(slot);
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
                // OCR mode: bypass AI consent and model selection entirely.
                if (ScanMode == ScanMode.Ocr)
                {
                    var ocrResult = await _ocrService.ScanCardAsync(ImagePath, ImagePathBack);
                    ocrResult.Card.ImagePathFront = ImagePath;
                    if (!string.IsNullOrEmpty(ImagePathBack))
                        ocrResult.Card.ImagePathBack = ImagePathBack;
                    ocrResult.Card.DataSource = CardDataSource.Ocr;
                    _lastScanResult = ocrResult;
                    ScannedCard = CardDetailViewModel.FromCard(ocrResult.Card);
                    MergeCustomGradingCompanies(ScannedCard);
                    ResetMatcherState();
                    LastScanWasOcr = true;
                    return;
                }

                var settings = _settingsService.Load();

                // First-run consent: if the user hasn't yet acknowledged that images
                // are sent to CardSight/OpenRouter, show the dialog before scanning.
                if (!settings.AiScanConsentGiven)
                {
                    var consent = await _aiScanConsentService.AskAsync();
                    if (!consent.Proceed)
                    {
                        IsScanning = false;
                        return;
                    }
                    if (consent.Remember)
                    {
                        settings.AiScanConsentGiven = true;
                        _settingsService.Save(settings);
                    }
                }

                // OCR pre-pass — runs Windows OCR silently before the LLM so the
                // model gets a rich OcrHint anchored on player / brand / manufacturer.
                // This is what feeds the parallel-candidate provider; without it
                // the LLM has no manufacturer signal and parallels are guesswork.
                // Best-effort: failure here just means scanning without a hint.
                OcrHint? ocrHint = null;
                if (_ocrService.IsAvailable)
                {
                    try
                    {
                        var ocr = await _ocrService.ScanCardAsync(ImagePath, ImagePathBack);
                        ocrHint = _playerDirectory?.IsReady == true
                            ? _playerDirectory.BuildHintFromCard(ocr.Card)
                            : new OcrHint
                            {
                                PlayerName = ocr.Card.PlayerName,
                                Year = ocr.Card.Year,
                                Manufacturer = ocr.Card.Manufacturer,
                                Brand = ocr.Card.Brand,
                                SetName = ocr.Card.SetName,
                            };
                        ocrHint.AllVisibleText = ocr.AllVisibleText ?? new List<string>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "OCR pre-pass failed; LLM will scan without hint.");
                    }
                }

                // Resolve the model to use:
                // - "Auto" (or no selection): rotate through free models, then ask consent for cheapest paid.
                // - Explicit free or paid pick: single attempt with that model, no rotation.
                ScanResult? scanResult;
                if (SelectedModel == null || SelectedModel.IsAuto)
                {
                    scanResult = await ScanWithAutoRotationAsync(ocrHint);
                    if (scanResult == null)
                    {
                        // Either user declined paid consent, or every model failed.
                        // Either way: clean exit, no error noise.
                        return;
                    }
                }
                else
                {
                    scanResult = await _scannerService.ScanCardAsync(ImagePath, ImagePathBack, SelectedModel.Value, ocrHint: ocrHint);
                }

                scanResult.Card.ImagePathFront = ImagePath;
                if (!string.IsNullOrEmpty(ImagePathBack))
                    scanResult.Card.ImagePathBack = ImagePathBack;
                // Stamp model id so post-save user edits attribute corrections
                // back to the model that produced this card.
                scanResult.Card.AiModelUsed = scanResult.UsedModelId;
                _lastScanResult = scanResult;
                ScannedCard = CardDetailViewModel.FromCard(scanResult.Card);
                MergeCustomGradingCompanies(ScannedCard);
                LastScanWasOcr = false;

                // Scoreboard: record the successful scan against the winning
                // model. CardId is null — single-scan cards aren't saved until
                // the user clicks Save Card; the user-correction hook attaches
                // by-card attribution if they later edit it.
                if (_scoreboard != null && !string.IsNullOrEmpty(scanResult.UsedModelId))
                {
                    try { await _scoreboard.RecordSuccessAsync(scanResult.UsedModelId, cardId: null, scanResult); }
                    catch (Exception sbEx) { _logger.LogDebug(sbEx, "Scoreboard RecordSuccess failed; continuing."); }
                }

                // Phase 2 tier-aware verification (Roadmap 1 §8d). Runs alongside the
                // existing variation verifier — produces a coloured tier badge + the
                // Surface B "no checklist imported" banner when applicable. Errors
                // here never block the scan; matcher failure just leaves tier blank.
                try
                {
                    await RunChecklistMatcherAsync(scanResult.Card, settings);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Checklist verification matcher failed for {ImagePath}", ImagePath);
                    ResetMatcherState();
                }

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
                        _logger.LogError(ex, "Verification failed for {ImagePath}", ImagePath);
                        VerificationStatus = $"Verification error: {ex.Message}";
                    }
                    finally
                    {
                        IsVerifying = false;
                    }
                }
            }
            catch (OpenRouterPaymentRequiredException pEx)
            {
                _logger.LogError(pEx, "Payment Required during scan of {ImagePath}", ImagePath);
                ErrorMessage = pEx.Message;
                _notificationService?.NotifyPaymentRequired(pEx.ModelId, pEx.ResponseBody);
            }
            catch (OpenRouterRateLimitException rlEx)
            {
                _logger.LogError(rlEx, "Rate limit during scan of {ImagePath}", ImagePath);
                ErrorMessage = rlEx.Message;
                _notificationService?.NotifyRateLimit(rlEx.ModelId, rlEx.Scope, rlEx.RetryAfterSeconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scan failed for {ImagePath}", ImagePath);
                ErrorMessage = $"Scan failed: {ex.Message}";

                // Scoreboard: when the explicit-pick path bubbles a non-billing
                // error (auto-rotation already records per-attempt internally),
                // attribute the failure to whichever model the user chose.
                if (_scoreboard != null && SelectedModel != null && !SelectedModel.IsAuto)
                {
                    var outcome = ex is System.Text.Json.JsonException
                        ? ScanOutcome.ParseFailure
                        : ScanOutcome.ModelError;
                    try { await _scoreboard.RecordFailureAsync(SelectedModel.Value, outcome); }
                    catch (Exception sbEx) { _logger.LogDebug(sbEx, "Scoreboard RecordFailure failed; continuing."); }
                }
            }
            finally
            {
                IsScanning = false;
            }
        }

        /// <summary>
        /// Pre-save Enhance for the Single Scan flow. Only available after an
        /// OCR scan — re-runs the LLM with directory-validated values locked
        /// (verified-fields hint) so the model spends its tokens on the
        /// visual-pattern fields the OCR pass couldn't see (parallel pattern,
        /// refractor, foil, border colour, visual cues). Result merges back
        /// into the in-memory ScannedCard form so the user can review before
        /// saving — same shape as Bulk Scan's per-item Enhance.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanEnhance))]
        private async Task EnhanceAsync()
        {
            if (ScannedCard == null || string.IsNullOrEmpty(ImagePath)) return;

            IsEnhancing = true;
            EnhanceMessage = null;
            ErrorMessage = null;

            try
            {
                // Build the verified-fields hint via the directory if available;
                // otherwise fall back to a soft hint so the LLM still has the
                // OCR's identity guess as context.
                OcrHint hint;
                if (_playerDirectory?.IsReady == true)
                {
                    hint = _playerDirectory.BuildHintFromCard(ScannedCard.ToCard());
                }
                else
                {
                    hint = new OcrHint
                    {
                        PlayerName = ScannedCard.PlayerName,
                        Year = ScannedCard.Year,
                        CardNumber = ScannedCard.CardNumber,
                        Manufacturer = ScannedCard.Manufacturer,
                        Brand = ScannedCard.Brand,
                        SetName = ScannedCard.SetName,
                    };
                }
                // Carry the raw OCR text from the pre-enhance scan so the LLM
                // sees peripheral text it might otherwise miss.
                if (_lastScanResult?.AllVisibleText != null)
                    hint.AllVisibleText = _lastScanResult.AllVisibleText.ToList();

                var settings = _settingsService.Load();
                // Either honor the user's explicit dropdown pick, or resolve a saved
                // settings value (folding the "auto" sentinel down to the free default
                // — see OpenRouterModelDefaults.ResolveModelId for the billing rationale).
                var resolved = SelectedModel?.IsAuto == false
                    ? SelectedModel.Value
                    : OpenRouterModelDefaults.ResolveModelId(settings.DefaultModel);
                // Gate: paid models always prompt the picker. Free passes through.
                var gated = await _paidScanGate.GateAsync(
                    resolved,
                    "About to enhance this card using a paid model. Pick which paid model to use, or cancel.");
                if (gated == null)
                {
                    SuccessMessage = "Enhance cancelled — no paid model used.";
                    return;
                }
                var model = gated;

                var result = await _scannerService.ScanCardAsync(
                    ImagePath,
                    ImagePathBack,
                    model,
                    scanDepth: ScanDepth.Standard,
                    ocrHint: hint);

                // Merge the LLM result back into the form. Verified fields are
                // already locked at the LLM level + restored by the drift guard,
                // so the assignments here mostly fill the visual / unknown
                // fields the OCR pass left blank. Use null-coalesce so an
                // empty LLM response doesn't wipe a field we already had.
                var e = result.Card;
                ScannedCard.PlayerName    = e.PlayerName    ?? ScannedCard.PlayerName;
                ScannedCard.Year          = e.Year          ?? ScannedCard.Year;
                ScannedCard.CardNumber    = e.CardNumber    ?? ScannedCard.CardNumber;
                ScannedCard.Manufacturer  = e.Manufacturer  ?? ScannedCard.Manufacturer;
                ScannedCard.Brand         = e.Brand         ?? ScannedCard.Brand;
                ScannedCard.SetName       = e.SetName       ?? ScannedCard.SetName;
                ScannedCard.Team          = e.Team          ?? ScannedCard.Team;
                ScannedCard.VariationType = e.VariationType ?? ScannedCard.VariationType;
                ScannedCard.ParallelName  = e.ParallelName  ?? ScannedCard.ParallelName;
                ScannedCard.SerialNumbered= e.SerialNumbered?? ScannedCard.SerialNumbered;
                ScannedCard.IsRookie      = e.IsRookie;
                ScannedCard.IsAuto        = e.IsAuto;
                ScannedCard.IsRelic       = e.IsRelic;
                ScannedCard.IsGraded      = e.IsGraded;
                ScannedCard.GradeCompany  = e.GradeCompany  ?? ScannedCard.GradeCompany;
                ScannedCard.GradeValue    = e.GradeValue    ?? ScannedCard.GradeValue;
                if (e.Sport.HasValue) ScannedCard.Sport = e.Sport;

                // Card has now effectively been AI-scanned — flag it so a
                // subsequent click of Enhance doesn't re-run uselessly.
                LastScanWasOcr = false;
                _lastScanResult = result;

                EnhanceMessage = "Enhanced with AI — review fields and save.";
                _logger.LogInformation("Enhanced single OCR scan with AI ({Player})", ScannedCard.PlayerName);
            }
            catch (OpenRouterPaymentRequiredException pEx)
            {
                _logger.LogError(pEx, "Payment Required during single-scan enhance");
                ErrorMessage = pEx.Message;
                _notificationService?.NotifyPaymentRequired(pEx.ModelId, pEx.ResponseBody);
            }
            catch (OpenRouterRateLimitException rlEx)
            {
                _logger.LogError(rlEx, "Rate limit during single-scan enhance");
                ErrorMessage = rlEx.Message;
                _notificationService?.NotifyRateLimit(rlEx.ModelId, rlEx.Scope, rlEx.RetryAfterSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Single-scan enhance failed");
                ErrorMessage = $"Enhance failed: {ex.Message}";
            }
            finally
            {
                IsEnhancing = false;
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
                ApplyAdditionalPhotosToCard(card);
                ApplyChecklistMatchToCard(card);

                // Auto-fill Whatnot category/subcategory from Sport if user left them
                // blank (e.g. Sport=Football → WhatnotSubcategory="Football Singles").
                // Whatnot rejects rows with a missing sub-category for categories that
                // require one, so this defaulter saves a manual round-trip through Edit.
                WhatnotCategoryDefaulter.ApplyDefaults(card);

                // Auto-upload any local images that don't have a hosted URL yet (no
                // separate Export-page step required). Failures here are non-fatal —
                // the card still saves with whatever URLs were obtained, status will
                // reflect the actual state.
                await TryUploadMissingUrlsAsync(card);

                // Auto-status: Ready if both images and price are present; Draft otherwise.
                card.Status = CardStatusEvaluator.Evaluate(card);

                await _cardRepository.InsertCardAsync(card);

                // Learn from saved card (fire-and-forget)
                _ = _checklistLearningService.LearnFromCardAsync(card);

                // Persist custom grading company if new
                if (card.IsGraded && !string.IsNullOrEmpty(card.GradeCompany))
                {
                    var presets = new[] { "PSA", "BGS", "CGC", "CCG", "SGC" };
                    var settings = _settingsService.Load();
                    if (!presets.Contains(card.GradeCompany, StringComparer.OrdinalIgnoreCase) &&
                        !settings.CustomGradingCompanies.Contains(card.GradeCompany, StringComparer.OrdinalIgnoreCase))
                    {
                        settings.CustomGradingCompanies.Add(card.GradeCompany);
                        _settingsService.Save(settings);
                    }
                }

                SuccessMessage = $"Saved {card.PlayerName} to My Cards!";
                Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save card failed");
                ErrorMessage = $"Save failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveDraftAsync()
        {
            if (string.IsNullOrEmpty(ImagePath))
                return;

            ErrorMessage = null;

            try
            {
                var draftName = await _cardRepository.GetNextDraftNameAsync();
                var card = new Card
                {
                    PlayerName = draftName,
                    ImagePathFront = ImagePath,
                    ImagePathBack = ImagePathBack,
                    Status = CardStatus.Draft
                };
                ApplyAdditionalPhotosToCard(card);
                await TryUploadMissingUrlsAsync(card);
                await _cardRepository.InsertCardAsync(card);

                SuccessMessage = $"Saved as '{draftName}' — scan again or clear for next card.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save draft failed");
                ErrorMessage = $"Save draft failed: {ex.Message}";
            }
        }

        private void MergeCustomGradingCompanies(CardDetailViewModel vm)
        {
            var settings = _settingsService.Load();
            foreach (var custom in settings.CustomGradingCompanies)
            {
                if (!vm.GradingCompanyOptions.Contains(custom, StringComparer.OrdinalIgnoreCase))
                    vm.GradingCompanyOptions.Add(custom);
            }
        }

        private void ApplyChecklistMatchToCard(Card card)
        {
            // Stamp the card with whichever tier outcome the matcher produced. The user
            // hasn't been given the picker yet (deferred UI), so Tier 1 stays Verified,
            // Tier 2 lands as BestGuess, Tier 3 as NoMatchFound. When no checklist exists
            // for the set we leave VerificationStatus at NotChecked.
            var match = ChecklistMatchResult;
            if (match == null || match.ChecklistMissing) return;

            card.MatchedChecklistKey = match.MatchKey;
            card.VerificationStatus = match.Tier switch
            {
                VerificationTier.Verified => Core.Models.Enums.VerificationStatus.Verified,
                VerificationTier.BestGuess => Core.Models.Enums.VerificationStatus.BestGuess,
                _ => Core.Models.Enums.VerificationStatus.NoMatchFound,
            };
        }

        private async Task RunChecklistMatcherAsync(Card scannedCard, AppSettings settings)
        {
            var match = await _checklistMatcher.MatchAsync(scannedCard);
            ChecklistMatchResult = match;

            if (match.ChecklistMissing)
            {
                HasMissingChecklist = true;
                MissingChecklistLabel = BuildMissingLabel(scannedCard);
                MissingChecklistDeeplink = BuildChecklistInsiderDeeplink(scannedCard);
                TierBadgeText = string.Empty;
                TierBadgeColor = "#9E9E9E";
                return;
            }

            HasMissingChecklist = false;
            MissingChecklistLabel = null;
            MissingChecklistDeeplink = null;

            switch (match.Tier)
            {
                case VerificationTier.Verified:
                    TierBadgeText = "✓ Verified against checklist";
                    TierBadgeColor = "#43A047"; // green
                    if (settings.AutoAcceptTier1Matches)
                    {
                        // Power-user shortcut: stamp the card's verification status
                        // immediately so SaveCard takes the auto-accept path.
                        scannedCard.VerificationStatus = Core.Models.Enums.VerificationStatus.Verified;
                        scannedCard.MatchedChecklistKey = match.MatchKey;
                    }
                    break;
                case VerificationTier.BestGuess:
                    TierBadgeText = "⚠ Best guess — review fields";
                    TierBadgeColor = "#FB8C00"; // amber
                    break;
                default:
                    TierBadgeText = "❓ No match — pick from checklist";
                    TierBadgeColor = "#E53935"; // red
                    break;
            }

            // Stash the proposed match key on the in-memory card. SaveCard will write
            // it to the row alongside whatever VerificationStatus the user accepts.
            scannedCard.MatchedChecklistKey = match.MatchKey;
        }

        private void ResetMatcherState()
        {
            ChecklistMatchResult = null;
            HasMissingChecklist = false;
            MissingChecklistLabel = null;
            MissingChecklistDeeplink = null;
            TierBadgeText = string.Empty;
            TierBadgeColor = "#9E9E9E";
        }

        private static string BuildMissingLabel(Card card)
        {
            var year = card.Year?.ToString() ?? "?";
            var brand = card.Brand ?? "?";
            var sport = card.Sport?.ToString();
            return string.IsNullOrWhiteSpace(sport)
                ? $"{year} {brand}"
                : $"{year} {brand} {sport}";
        }

        private static string BuildChecklistInsiderDeeplink(Card card)
        {
            var query = string.Join(" ", new[] { card.Year?.ToString(), card.Brand, card.Sport?.ToString() }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            var encoded = Uri.EscapeDataString(query);
            return $"https://www.checklistinsider.com/?s={encoded}";
        }

        [RelayCommand]
        private void OpenChecklistInsiderForMissingSet()
        {
            if (!string.IsNullOrWhiteSpace(MissingChecklistDeeplink))
                _browserService.OpenUrl(MissingChecklistDeeplink);
        }

        [RelayCommand]
        private void EnterManually()
        {
            ScannedCard = new CardDetailViewModel();
            MergeCustomGradingCompanies(ScannedCard);
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
            AdditionalPhotos.Clear();
            ScannedCard = null;
            ErrorMessage = null;
            VerificationResult = null;
            VerificationStatus = "";
            _lastScanResult = null;
            ResetMatcherState();
        }

        private void ApplyAdditionalPhotosToCard(Card card)
        {
            for (int i = 0; i < AdditionalPhotos.Count && i < MaxAdditionalPhotos; i++)
            {
                var path = AdditionalPhotos[i].Path;
                switch (i + 3)
                {
                    case 3: card.ImagePath3 = path; break;
                    case 4: card.ImagePath4 = path; break;
                    case 5: card.ImagePath5 = path; break;
                    case 6: card.ImagePath6 = path; break;
                    case 7: card.ImagePath7 = path; break;
                    case 8: card.ImagePath8 = path; break;
                }
            }
        }

        /// <summary>
        /// Uploads any local image paths that don't yet have a corresponding hosted URL.
        /// Updates the card's <c>ImageUrl{N}</c> fields in place. Swallows network errors
        /// — partial uploads are fine; the card saves with whatever it gets.
        /// </summary>
        private async Task TryUploadMissingUrlsAsync(Card card)
        {
            var paths = new[] { card.ImagePathFront, card.ImagePathBack,
                                card.ImagePath3, card.ImagePath4, card.ImagePath5,
                                card.ImagePath6, card.ImagePath7, card.ImagePath8 };
            var urls  = new[] { card.ImageUrl1, card.ImageUrl2,
                                card.ImageUrl3, card.ImageUrl4, card.ImageUrl5,
                                card.ImageUrl6, card.ImageUrl7, card.ImageUrl8 };

            // Only upload slots that have a path but no URL.
            var pathsToUpload = new List<string?>(8);
            for (int i = 0; i < 8; i++)
                pathsToUpload.Add(string.IsNullOrEmpty(urls[i]) ? paths[i] : null);

            if (!pathsToUpload.Any(p => !string.IsNullOrEmpty(p)))
                return;

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
                _logger.LogWarning(ex, "Image upload during save failed for {Player} — card saves without hosted URLs.", card.PlayerName);
            }
        }

        /// <summary>
        /// Auto-rotation: try every free model in catalog order; if all throw, surface
        /// the consent dialog for the cheapest paid model. Returns the successful
        /// ScanResult, or null when the user declines the paid prompt OR every model
        /// (free + the one approved paid) failed.
        /// </summary>
        private async Task<ScanResult?> ScanWithAutoRotationAsync(OcrHint? ocrHint = null)
        {
            var catalog = await _modelCatalog.GetAsync();
            if (catalog.IsEmpty)
            {
                ErrorMessage = "No OpenRouter models available — check your network connection or pick a model manually.";
                return null;
            }

            // Sort the free rotation by per-model accuracy score so the higher-
            // performing models are tried first. Untested models still get a
            // turn — they fall to the bottom but stay in the rotation.
            var freeModels = (IEnumerable<OpenRouterModel>)catalog.FreeVisionModels;
            if (_scoreboard != null)
            {
                try
                {
                    var qualities = await _scoreboard.GetQualitiesAsync();
                    freeModels = catalog.FreeVisionModels
                        .OrderByDescending(m => qualities.TryGetValue(m.Id, out var q) ? (q.Score ?? -1m) : -1m);
                }
                catch { /* fall back to catalog order */ }
            }

            Exception? lastError = null;
            foreach (var freeModel in freeModels)
            {
                try
                {
                    VerificationStatus = $"Trying {freeModel.DisplayName}...";
                    return await _scannerService.ScanCardAsync(ImagePath!, ImagePathBack, freeModel.Id, ocrHint: ocrHint);
                }
                catch (OpenRouterPaymentRequiredException) { throw; }
                catch (OpenRouterRateLimitException) { throw; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "Free model {Model} failed; trying next.", freeModel.Id);
                    // Scoreboard: each free-chain loser gets attributed.
                    if (_scoreboard != null)
                    {
                        var outcome = ex is System.Text.Json.JsonException
                            ? ScanOutcome.ParseFailure
                            : ScanOutcome.ModelError;
                        try { await _scoreboard.RecordFailureAsync(freeModel.Id, outcome); }
                        catch (Exception sbEx) { _logger.LogDebug(sbEx, "Scoreboard RecordFailure failed; continuing."); }
                    }
                }
            }

            VerificationStatus = string.Empty;

            if (catalog.PaidVisionModels.Count == 0)
            {
                ErrorMessage = "All free models failed and no paid models are available." +
                               (lastError != null ? $" Last error: {lastError.Message}" : "");
                return null;
            }

            var cheapest = catalog.PaidVisionModels[0];
            // Surface the full paid-model list so the user can pick a different
            // (more expensive but more accurate) model if they prefer — the
            // cheapest is just the suggested default.
            var chosenPaid = await _consentService.AskAsync(
                catalog.PaidVisionModels,
                cheapest,
                $"All {catalog.FreeVisionModels.Count} free OpenRouter vision models failed for this card. " +
                "Pick a paid model to continue with, or cancel.");

            if (chosenPaid == null)
            {
                SuccessMessage = "Scan canceled — no paid model was used.";
                return null;
            }

            try
            {
                VerificationStatus = $"Trying {chosenPaid.DisplayName}...";
                return await _scannerService.ScanCardAsync(ImagePath!, ImagePathBack, chosenPaid.Id, ocrHint: ocrHint);
            }
            catch (OpenRouterPaymentRequiredException) { throw; }
            catch (OpenRouterRateLimitException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paid model {Model} also failed.", chosenPaid.Id);
                ErrorMessage = $"Paid model {chosenPaid.DisplayName} failed: {ex.Message}";
                if (_scoreboard != null)
                {
                    var outcome = ex is System.Text.Json.JsonException
                        ? ScanOutcome.ParseFailure
                        : ScanOutcome.ModelError;
                    try { await _scoreboard.RecordFailureAsync(chosenPaid.Id, outcome); }
                    catch (Exception sbEx) { _logger.LogDebug(sbEx, "Scoreboard RecordFailure failed; continuing."); }
                }
                return null;
            }
        }
    }
}
