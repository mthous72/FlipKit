using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FlipKit.Core.Data;
using FlipKit.Core.Helpers;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Interfaces;
using FlipKit.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IBrowserService _browserService;
        private readonly IServiceProvider _services;
        private readonly IServerManagementService _serverManagement;
        private readonly INetworkAddressProvider _networkAddresses;
        private readonly IEbayPublishingService? _ebayPublishingService;
        private Timer? _statusRefreshTimer;

        // Phase 5.10 fix — gate the 2-second Timer's UpdateServerStatus while an explicit
        // user Start/Stop command is in flight, so the Timer doesn't clobber the success
        // or failure message before the user sees it. See AUDIT-2026-05 §7.10.
        private volatile bool _explicitOperationInProgress;

        // API Keys
        [ObservableProperty] private string _openRouterApiKey = string.Empty;
        [ObservableProperty] private string _imgBBApiKey = string.Empty;
        [ObservableProperty] private string _cardsightApiKey = string.Empty;
        [ObservableProperty] private string _ebayClientId = string.Empty;
        [ObservableProperty] private string _ebayClientSecret = string.Empty;
        [ObservableProperty] private string _ebayRuName = string.Empty;
        [ObservableProperty] private string _openRouterStatus = "Not configured";
        [ObservableProperty] private string _imgBBStatus = "Not configured";
        [ObservableProperty] private string _cardsightStatus = "Not configured";
        [ObservableProperty] private string _ebayStatus = "Not configured";
        [ObservableProperty] private string _ebayConnectStatus = "Not connected";
        [ObservableProperty] private string _ebayPoliciesStatus = "Not fetched";
        [ObservableProperty] private bool _isTestingOpenRouter;
        [ObservableProperty] private bool _isTestingImgBB;
        [ObservableProperty] private bool _isTestingCardsight;
        [ObservableProperty] private bool _isTestingEbay;
        [ObservableProperty] private bool _isConnectingEbay;
        [ObservableProperty] private bool _isFetchingEbayPolicies;
        [ObservableProperty] private bool _isAwaitingEbayCode;
        [ObservableProperty] private string _ebayAuthCode = string.Empty;

        // Preferences
        [ObservableProperty] private bool _isEbaySeller;
        [ObservableProperty] private string _defaultShippingProfile = "4 oz";
        [ObservableProperty] private string _defaultCondition = "Near Mint";
        [ObservableProperty] private string _defaultModel = ModelOption.AutoValue;
        [ObservableProperty] private ModelOption? _selectedDefaultModel;
        [ObservableProperty] private bool _isLoadingModels;
        [ObservableProperty] private string? _modelLoadError;
        [ObservableProperty] private string? _modelLoadStatus;

        public ObservableCollection<ModelOption> ModelOptions { get; } = new();

        // === Model Performance Leaderboard ===
        // One row per model the scoreboard has seen. Rebuilt on Settings open
        // and after every ScanBatchCompleted event so the panel stays current
        // without the user needing to refresh manually.
        public ObservableCollection<ModelLeaderboardRow> LeaderboardRows { get; } = new();
        [ObservableProperty] private bool _isLoadingLeaderboard;
        [ObservableProperty] private string? _leaderboardError;

        // === OpenRouter Usage panel ===
        // Snapshot fetched from GET /api/v1/key. Null until the first successful
        // refresh. Each display string handles null gracefully (returns "—" or
        // similar) so XAML can bind without null guards.
        [ObservableProperty] private OpenRouterKeyInfo? _keyInfo;
        [ObservableProperty] private bool _isLoadingKeyInfo;
        [ObservableProperty] private string? _keyInfoError;

        // === CardSight Usage panel ===
        // Snapshot fetched from GET /v1/subscription. Null until the first
        // successful refresh. CardSight reports an aggregate call count but no
        // limit/plan, so we frame usage against the documented free-tier quota
        // of 750 identifications/month (clearly labelled as the free tier).
        [ObservableProperty] private CardsightSubscriptionStatus? _cardsightSubscription;
        [ObservableProperty] private bool _isLoadingCardsightSubscription;
        [ObservableProperty] private string? _cardsightSubscriptionError;

        // Card Scanning
        [ObservableProperty] private bool _enableVariationVerification = true;
        [ObservableProperty] private bool _autoApplyHighConfidenceSuggestions = true;
        [ObservableProperty] private bool _runConfirmationPass = true;
        [ObservableProperty] private bool _enableChecklistLearning = true;
        // Phase 2 of Checklist Insider — auto-save Tier 1 (Verified) matches without
        // user confirmation. Off by default; opt-in shortcut for trusted sets.
        [ObservableProperty] private bool _autoAcceptTier1Matches;
        [ObservableProperty] private int _maxConcurrentScans = 1;

        // Webcam capture (Roadmap #2 — Docs/27-WEBCAM-CAPTURE-PLAN.md). Device list
        // is populated lazily via RefreshWebcamDevicesCommand because probing all
        // OpenCV indices opens each camera in turn — we don't want that on app start.
        [ObservableProperty] private bool _webcamCaptureEnabled = true;
        [ObservableProperty] private CameraDevice? _selectedWebcamDevice;
        [ObservableProperty] private bool _isProbingWebcams;
        [ObservableProperty] private string? _webcamProbeStatus;
        public ObservableCollection<CameraDevice> WebcamDevices { get; } = new();

        // Financial
        [ObservableProperty] private decimal _whatnotFeePercent = 11.0m;
        [ObservableProperty] private decimal _ebayFeePercent = 13.25m;
        [ObservableProperty] private decimal _defaultShippingCostPwe = 1.00m;
        [ObservableProperty] private decimal _defaultShippingCostBmwt = 4.50m;
        [ObservableProperty] private int _priceStalenessThresholdDays = 30;

        // Data Info
        [ObservableProperty] private int _cardCount;
        [ObservableProperty] private string _dbPath = string.Empty;

        // Title Templates (SEO-optimized for each platform)
        [ObservableProperty] private string _whatnotTitleTemplate = string.Empty;
        [ObservableProperty] private string _ebayTitleTemplate = string.Empty;
        [ObservableProperty] private string _comcTitleTemplate = string.Empty;
        [ObservableProperty] private string _genericTitleTemplate = string.Empty;
        [ObservableProperty] private ExportPlatform _activeExportPlatform = ExportPlatform.Whatnot;
        [ObservableProperty] private string _templateValidationMessage = string.Empty;
        [ObservableProperty] private string _templatePreview = string.Empty;

        public List<ExportPlatform> ExportPlatformOptions { get; } = Enum.GetValues<ExportPlatform>().ToList();
        public string PlaceholderHelpText => TitleTemplateService.GetPlaceholderHelpText();

        // Search Query Templates (for pricing research)
        [ObservableProperty] private string _terapeakSearchTemplate = string.Empty;
        [ObservableProperty] private string _ebaySearchTemplate = string.Empty;
        [ObservableProperty] private string _searchTemplateValidationMessage = string.Empty;

        // eBay export defaults — populate before exporting eBay listings.
        [ObservableProperty] private string _ebaySellerLocation = string.Empty;
        [ObservableProperty] private int _ebayDispatchTimeMax = 2;
        [ObservableProperty] private bool _ebayReturnsAccepted = true;
        [ObservableProperty] private bool _ebayUseVerifyAdd;
        [ObservableProperty] private string _searchTemplatePreview = string.Empty;

        // Save feedback
        [ObservableProperty] private string _saveMessage = string.Empty;

        // Data Access Mode
        [ObservableProperty] private string? _syncServerUrl;
        [ObservableProperty] private string _dataAccessMode = "Local Database (Direct access)";
        [ObservableProperty] private string _dataAccessModeColor = "Green";

        // Server Management (FlipKit Hub)
        [ObservableProperty] private bool _autoStartWebServer = true;
        [ObservableProperty] private bool _autoStartApiServer = true;
        [ObservableProperty] private int _webServerPort = 5000;
        [ObservableProperty] private int _apiServerPort = 5001;
        [ObservableProperty] private bool _minimizeToTray = true;
        [ObservableProperty] private bool _autoOpenBrowser = true;

        [ObservableProperty] private bool _isWebRunning;
        [ObservableProperty] private bool _isApiRunning;
        [ObservableProperty] private int _actualWebPort;
        [ObservableProperty] private int _actualApiPort;
        [ObservableProperty] private string _webServerStatus = "Stopped";
        [ObservableProperty] private string _apiServerStatus = "Stopped";
        [ObservableProperty] private string _localIpAddresses = "No network connection";
        [ObservableProperty] private string _webServerLogs = string.Empty;
        [ObservableProperty] private string _apiServerLogs = string.Empty;
        [ObservableProperty] private Bitmap? _qrCodeBitmap;

        // Dual QR Code Support
        [ObservableProperty] private string? _localNetworkIp;
        [ObservableProperty] private string? _tailscaleIp;
        [ObservableProperty] private string _localNetworkUrl = string.Empty;
        [ObservableProperty] private string _tailscaleUrl = string.Empty;
        [ObservableProperty] private bool _isLocalNetworkAvailable;
        [ObservableProperty] private bool _isTailscaleAvailable;
        [ObservableProperty] private Bitmap? _localQrCodeBitmap;
        [ObservableProperty] private Bitmap? _tailscaleQrCodeBitmap;
        [ObservableProperty] private string _localNetworkStatus = "Checking...";
        [ObservableProperty] private string _tailscaleStatus = "Not configured";

        private readonly IOpenRouterModelCatalog? _modelCatalog;
        // Drives the OpenRouter Usage panel (credits remaining + daily/weekly/
        // monthly burn). Optional so Settings still loads if DI registration
        // ever changes — same pattern as _modelCatalog.
        private readonly IOpenRouterKeyInfoService? _keyInfoService;
        // Drives the CardSight Usage panel (calls used vs the free-tier 750/mo
        // quota). Optional so Settings still loads if DI registration changes —
        // same pattern as _keyInfoService.
        private readonly ICardsightSubscriptionService? _cardsightSubscriptionService;
        // Per-model accuracy scoreboard. Drives the leaderboard panel and the
        // sort-by-quality + score badge in the model dropdown. Optional so
        // Settings still loads even if DI ever changes.
        private readonly IModelScoreboard? _scoreboard;
        // Subscribed in ctor so post-batch auto-refresh fires while Settings
        // is the active page. Unsubscribed in Dispose so navigating away
        // doesn't leak the handler. Optional for the same reason as the others.
        private readonly Services.IAppNotificationService? _appNotifications;
        private readonly ICameraService? _cameraService;
        private readonly IWebcamCaptureDialogService? _webcamCaptureDialog;

        public SettingsViewModel(ISettingsService settingsService, IBrowserService browserService,
            IServiceProvider services, IServerManagementService serverManagement,
            INetworkAddressProvider networkAddresses)
        {
            _settingsService = settingsService;
            _browserService = browserService;
            _services = services;
            _serverManagement = serverManagement;
            _networkAddresses = networkAddresses;
            _ebayPublishingService = services.GetService(typeof(IEbayPublishingService)) as IEbayPublishingService;

            // Optional resolution — Settings page should still load even if catalog fails to register.
            _modelCatalog = services.GetService(typeof(IOpenRouterModelCatalog)) as IOpenRouterModelCatalog;
            _keyInfoService = services.GetService(typeof(IOpenRouterKeyInfoService)) as IOpenRouterKeyInfoService;
            _cardsightSubscriptionService = services.GetService(typeof(ICardsightSubscriptionService)) as ICardsightSubscriptionService;
            _scoreboard = services.GetService(typeof(IModelScoreboard)) as IModelScoreboard;
            _appNotifications = services.GetService(typeof(Services.IAppNotificationService)) as Services.IAppNotificationService;
            if (_appNotifications != null)
                _appNotifications.ScanBatchCompleted += OnScanBatchCompleted;
            _cameraService = services.GetService(typeof(ICameraService)) as ICameraService;
            _webcamCaptureDialog = services.GetService(typeof(IWebcamCaptureDialogService)) as IWebcamCaptureDialogService;

            LoadSettings();
            LoadCardCountAsync();
            UpdateDataAccessMode();
            UpdateServerStatus();
            UpdateLocalIpAddresses();

            _ = LoadModelsAsync();
            // Auto-fetch on Settings open per planning. Skipped silently when
            // no API key is configured — the panel renders the empty state.
            _ = RefreshKeyInfoAsync();
            _ = RefreshCardsightSubscriptionAsync();
            _ = LoadLeaderboardAsync();

            // Refresh server status every 2 seconds. This fires on a thread-pool thread,
            // so any exception here would be unhandled and crash the app — guard it.
            _statusRefreshTimer = new Timer(_ =>
            {
                try
                {
                    UpdateServerStatus();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Server status refresh failed: {ex}");
                }
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        private async Task LoadModelsAsync(bool forceRefresh = false)
        {
            if (_modelCatalog == null) return;
            IsLoadingModels = true;
            ModelLoadError = null;
            ModelLoadStatus = forceRefresh ? "Refreshing model list from OpenRouter..." : "Loading model catalog...";
            try
            {
                if (forceRefresh) _modelCatalog.InvalidateCache();
                var catalog = await _modelCatalog.GetAsync();

                // Attach scoreboard quality so the dropdown shows a per-model
                // pill and sorts higher-scoring models to the top of each group.
                IReadOnlyDictionary<string, ModelQuality>? qualities = null;
                if (_scoreboard != null)
                {
                    try { qualities = await _scoreboard.GetQualitiesAsync(); }
                    catch { /* best-effort: scoreboard miss = no pills, no penalty */ }
                }

                ModelOption WithQuality(OpenRouterModel m) =>
                    ModelOption.FromCatalog(m, qualities != null && qualities.TryGetValue(m.Id, out var q) ? q : null);

                ModelOptions.Clear();
                ModelOptions.Add(ModelOption.Auto());
                // Sort within each tier — better-performing models first, untested
                // last. Stable order across loads keeps the dropdown predictable.
                var sortedFree = catalog.FreeVisionModels.Select(WithQuality)
                    .OrderByDescending(o => o.QualitySortKey).ToList();
                var sortedPaid = catalog.PaidVisionModels.Select(WithQuality)
                    .OrderByDescending(o => o.QualitySortKey).ToList();
                foreach (var o in sortedFree) ModelOptions.Add(o);
                foreach (var o in sortedPaid) ModelOptions.Add(o);

                var savedId = string.IsNullOrWhiteSpace(DefaultModel) ? ModelOption.AutoValue : DefaultModel;
                ModelOption? choice = ModelOptions.FirstOrDefault(o => o.Value == savedId);
                if (choice == null && savedId != ModelOption.AutoValue)
                {
                    choice = ModelOption.Stale(savedId);
                    ModelOptions.Add(choice);
                }
                SelectedDefaultModel = choice ?? ModelOptions.First();

                ModelLoadStatus = catalog.IsEmpty
                    ? "Couldn't reach OpenRouter — using saved default only."
                    : $"Loaded {catalog.FreeVisionModels.Count} free + {catalog.PaidVisionModels.Count} paid vision models.";
            }
            catch (Exception ex)
            {
                ModelLoadError = ex.Message;
                ModelLoadStatus = null;
            }
            finally
            {
                IsLoadingModels = false;
            }
        }

        [RelayCommand]
        private async Task RefreshModelsAsync()
        {
            await LoadModelsAsync(forceRefresh: true);
        }

        // === Model Performance Leaderboard ===

        public async Task LoadLeaderboardAsync()
        {
            if (_scoreboard == null) return;
            IsLoadingLeaderboard = true;
            LeaderboardError = null;
            try
            {
                var qualities = await _scoreboard.GetQualitiesAsync();
                LeaderboardRows.Clear();
                // Sort by score desc; untested entries (null score) sort to
                // the bottom but stay in the table so the user can reset them.
                var ordered = qualities.Values
                    .OrderByDescending(q => q.Score ?? -1m)
                    .ThenByDescending(q => q.SampleCount);
                foreach (var q in ordered)
                    LeaderboardRows.Add(ModelLeaderboardRow.FromQuality(q));
            }
            catch (Exception ex)
            {
                LeaderboardError = $"Couldn't load leaderboard: {ex.Message}";
            }
            finally
            {
                IsLoadingLeaderboard = false;
            }
        }

        [RelayCommand]
        private async Task RefreshLeaderboardAsync() => await LoadLeaderboardAsync();

        [RelayCommand]
        private async Task ResetModelHistoryAsync(string? modelId)
        {
            if (_scoreboard == null || string.IsNullOrWhiteSpace(modelId)) return;
            try
            {
                await _scoreboard.ResetHistoryAsync(modelId);
                await LoadLeaderboardAsync();
                // Keep the dropdown's quality pills in sync so a reset model
                // immediately drops to the bottom of the picker.
                _ = LoadModelsAsync();
            }
            catch (Exception ex)
            {
                LeaderboardError = $"Reset failed: {ex.Message}";
            }
        }

        // === OpenRouter key-info / Usage panel ===

        /// <summary>
        /// Display strings for the four stat tiles. Computed so that null /
        /// no-limit cases render "—" or "(no limit)" instead of blank or "$0".
        /// </summary>
        public string CreditsRemainingDisplay
        {
            get
            {
                if (KeyInfo == null) return "—";
                var rem = KeyInfo.LimitRemaining;
                var lim = KeyInfo.Limit;
                if (rem == null && lim == null) return "(no credit limit)";
                if (rem != null && lim != null) return $"${rem:F2} of ${lim:F2}";
                if (rem != null) return $"${rem:F2}";
                return "(no limit set)";
            }
        }

        public string UsageDailyDisplay   => KeyInfo == null ? "—" : $"${KeyInfo.UsageDaily:F2}";
        public string UsageWeeklyDisplay  => KeyInfo == null ? "—" : $"${KeyInfo.UsageWeekly:F2}";
        public string UsageMonthlyDisplay => KeyInfo == null ? "—" : $"${KeyInfo.UsageMonthly:F2}";

        public string TierBadge => KeyInfo?.IsFreeTier == true ? "Free tier" : "Paid";

        public string LastRefreshedDisplay
        {
            get
            {
                if (KeyInfo == null) return string.Empty;
                var elapsed = DateTimeOffset.UtcNow - KeyInfo.FetchedAt;
                if (elapsed.TotalSeconds < 5) return "just now";
                if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds}s ago";
                if (elapsed.TotalHours < 1)   return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalDays < 1)    return $"{(int)elapsed.TotalHours}h ago";
                return KeyInfo.FetchedAt.LocalDateTime.ToString("g");
            }
        }

        public bool HasKeyInfo => KeyInfo != null;

        // Show the free-tier reminder when the user's still on the free RPM/RPD
        // bucket. Anchored to is_free_tier rather than a hard credit threshold
        // so OpenRouter changes its caps without breaking us.
        public bool ShowFreeTierLimitReminder => KeyInfo?.IsFreeTier == true;

        partial void OnKeyInfoChanged(OpenRouterKeyInfo? value)
        {
            // KeyInfo's display strings + computed flags need an explicit
            // PropertyChanged when the source record swaps — the source-generator
            // doesn't know about them.
            OnPropertyChanged(nameof(CreditsRemainingDisplay));
            OnPropertyChanged(nameof(UsageDailyDisplay));
            OnPropertyChanged(nameof(UsageWeeklyDisplay));
            OnPropertyChanged(nameof(UsageMonthlyDisplay));
            OnPropertyChanged(nameof(TierBadge));
            OnPropertyChanged(nameof(LastRefreshedDisplay));
            OnPropertyChanged(nameof(HasKeyInfo));
            OnPropertyChanged(nameof(ShowFreeTierLimitReminder));
        }

        /// <summary>
        /// Pulls a fresh snapshot from <c>GET /api/v1/key</c> via the registered
        /// service. Skips silently when the key isn't set yet (Settings just
        /// opened on a fresh install). Per-call exceptions populate
        /// <see cref="KeyInfoError"/> so the UI shows a graceful inline message.
        /// </summary>
        [RelayCommand]
        public async Task RefreshKeyInfoAsync()
        {
            if (_keyInfoService == null) return;
            if (string.IsNullOrWhiteSpace(OpenRouterApiKey))
            {
                // No key yet — clear stale state and skip the wire call. UI
                // will fall through to the "configure key first" affordance.
                KeyInfo = null;
                KeyInfoError = null;
                return;
            }

            IsLoadingKeyInfo = true;
            KeyInfoError = null;
            try
            {
                KeyInfo = await _keyInfoService.GetAsync();
            }
            catch (OpenRouterPaymentRequiredException pEx)
            {
                // Toast already fires from any scan path — here we just surface
                // the inline status so the user sees it on the Settings tab too.
                KeyInfoError = $"Payment required: {pEx.ResponseBody ?? "credit balance is negative."}";
            }
            catch (OpenRouterRateLimitException rlEx)
            {
                KeyInfoError = $"Rate limited (scope: {rlEx.Scope})";
            }
            catch (Exception ex)
            {
                KeyInfoError = $"Couldn't load usage: {ex.Message}";
            }
            finally
            {
                IsLoadingKeyInfo = false;
            }
        }

        // === CardSight Usage panel ===

        public bool HasCardsightSubscription => CardsightSubscription != null;

        /// <summary>
        /// Calls used this billing period (aggregate across the user's keys, as
        /// CardSight reports). "—" until the first refresh succeeds.
        /// </summary>
        public string CardsightCallsUsedDisplay =>
            CardsightSubscription == null ? "—" : CardsightSubscription.CallsUsed.ToString();

        /// <summary>
        /// Free-tier framing. CardSight doesn't report the user's actual plan
        /// limit, so we explicitly anchor this to the documented free-tier
        /// allowance (750/mo) and label it as such — paid users see "Free tier:"
        /// and understand it's the free allowance, not their own cap.
        /// </summary>
        public string CardsightFreeTierUsageDisplay
        {
            get
            {
                if (CardsightSubscription == null) return "—";
                var s = CardsightSubscription;
                return $"Free tier: {s.CallsUsed} / {s.FreeTierMonthlyQuota} used · {s.CallsRemaining} remaining";
            }
        }

        public int CardsightCallsUsed => CardsightSubscription?.CallsUsed ?? 0;
        public int CardsightFreeTierQuota =>
            CardsightSubscription?.FreeTierMonthlyQuota ?? ICardsightSubscriptionService.DefaultFreeTierMonthlyQuota;

        public string CardsightLastRefreshedDisplay
        {
            get
            {
                if (CardsightSubscription == null) return string.Empty;
                var elapsed = DateTimeOffset.UtcNow - CardsightSubscription.FetchedAt;
                if (elapsed.TotalSeconds < 5) return "just now";
                if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds}s ago";
                if (elapsed.TotalHours < 1)   return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalDays < 1)    return $"{(int)elapsed.TotalHours}h ago";
                return CardsightSubscription.FetchedAt.LocalDateTime.ToString("g");
            }
        }

        partial void OnCardsightSubscriptionChanged(CardsightSubscriptionStatus? value)
        {
            OnPropertyChanged(nameof(HasCardsightSubscription));
            OnPropertyChanged(nameof(CardsightCallsUsedDisplay));
            OnPropertyChanged(nameof(CardsightFreeTierUsageDisplay));
            OnPropertyChanged(nameof(CardsightCallsUsed));
            OnPropertyChanged(nameof(CardsightFreeTierQuota));
            OnPropertyChanged(nameof(CardsightLastRefreshedDisplay));
        }

        /// <summary>
        /// Pulls a fresh snapshot from <c>GET /v1/subscription</c>. Skips silently
        /// when the CardSight key isn't set yet (the panel renders its empty
        /// state). Typed <see cref="CardsightException"/> reasons are translated
        /// into a friendly inline message in <see cref="CardsightSubscriptionError"/>.
        /// </summary>
        [RelayCommand]
        public async Task RefreshCardsightSubscriptionAsync()
        {
            if (_cardsightSubscriptionService == null) return;
            if (string.IsNullOrWhiteSpace(CardsightApiKey))
            {
                CardsightSubscription = null;
                CardsightSubscriptionError = null;
                return;
            }

            IsLoadingCardsightSubscription = true;
            CardsightSubscriptionError = null;
            try
            {
                CardsightSubscription = await _cardsightSubscriptionService.GetAsync();
            }
            catch (CardsightException cex)
            {
                CardsightSubscriptionError = cex.Reason switch
                {
                    CardsightFailureReason.NotConfigured => "Enter your CardSight key above and click Refresh.",
                    CardsightFailureReason.InvalidKey => "CardSight rejected the key — double-check it in Settings.",
                    CardsightFailureReason.QuotaExceeded => "CardSight quota exceeded for this billing period.",
                    CardsightFailureReason.RateLimited => "CardSight is rate limiting requests — try again in a minute.",
                    _ => $"Couldn't load CardSight usage: {cex.Message}"
                };
            }
            catch (Exception ex)
            {
                CardsightSubscriptionError = $"Couldn't load CardSight usage: {ex.Message}";
            }
            finally
            {
                IsLoadingCardsightSubscription = false;
            }
        }

        partial void OnSelectedDefaultModelChanged(ModelOption? value)
        {
            // By design: DefaultModel string field stays in sync with SelectedDefaultModel
            // ModelOption. LoadModelsAsync resolves saved DefaultModel → ModelOption, the
            // assignment fires this partial, and DefaultModel gets written back to the
            // resolved value. If saved value matches an existing option no change; if it
            // doesn't, we get the Stale stub's value, which is what we want.
            if (value != null) DefaultModel = value.Value;
        }

        private void LoadSettings()
        {
            var s = _settingsService.Load();

            OpenRouterApiKey = s.OpenRouterApiKey ?? string.Empty;
            ImgBBApiKey = s.ImgBBApiKey ?? string.Empty;
            CardsightApiKey = s.CardsightApiKey ?? string.Empty;
            EbayClientId = s.EbayClientId ?? string.Empty;
            EbayClientSecret = s.EbayClientSecret ?? string.Empty;
            EbayRuName = s.EbayRuName ?? string.Empty;
            IsEbaySeller = s.IsEbaySeller;
            DefaultShippingProfile = s.DefaultShippingProfile;
            DefaultCondition = s.DefaultCondition;
            DefaultModel = s.DefaultModel;
            EnableVariationVerification = s.EnableVariationVerification;
            AutoApplyHighConfidenceSuggestions = s.AutoApplyHighConfidenceSuggestions;
            RunConfirmationPass = s.RunConfirmationPass;
            EnableChecklistLearning = s.EnableChecklistLearning;
            AutoAcceptTier1Matches = s.AutoAcceptTier1Matches;
            MaxConcurrentScans = s.MaxConcurrentScans;

            // Webcam capture — device list stays empty until the user clicks
            // "Detect cameras" (probing is slow and disturbs other camera apps).
            // The combo's selection placeholder shows the saved name when available.
            WebcamCaptureEnabled = s.WebcamCaptureEnabled;
            WhatnotFeePercent = s.WhatnotFeePercent;
            EbayFeePercent = s.EbayFeePercent;
            DefaultShippingCostPwe = s.DefaultShippingCostPwe;
            DefaultShippingCostBmwt = s.DefaultShippingCostBmwt;
            PriceStalenessThresholdDays = s.PriceStalenessThresholdDays;

            // Title Templates
            WhatnotTitleTemplate = s.WhatnotTitleTemplate;
            EbayTitleTemplate = s.EbayTitleTemplate;
            ComcTitleTemplate = s.ComcTitleTemplate;
            GenericTitleTemplate = s.GenericTitleTemplate;
            ActiveExportPlatform = s.ActiveExportPlatform;

            // Search Query Templates
            TerapeakSearchTemplate = s.TerapeakSearchTemplate;
            EbaySearchTemplate = s.EbaySearchTemplate;

            // eBay export defaults
            EbaySellerLocation = s.EbaySellerLocation;
            EbayDispatchTimeMax = s.EbayDispatchTimeMax;
            EbayReturnsAccepted = s.EbayReturnsAccepted;
            EbayUseVerifyAdd = s.EbayUseVerifyAdd;

            // API Server URL
            SyncServerUrl = s.SyncServerUrl;

            // Server Management (FlipKit Hub)
            AutoStartWebServer = s.AutoStartWebServer;
            AutoStartApiServer = s.AutoStartApiServer;
            WebServerPort = s.WebServerPort;
            ApiServerPort = s.ApiServerPort;
            MinimizeToTray = s.MinimizeToTray;
            AutoOpenBrowser = s.AutoOpenBrowser;

            OpenRouterStatus = string.IsNullOrWhiteSpace(OpenRouterApiKey) ? "Not configured" : "Configured (not tested)";
            ImgBBStatus = string.IsNullOrWhiteSpace(ImgBBApiKey) ? "Not configured" : "Configured (not tested)";
            CardsightStatus = string.IsNullOrWhiteSpace(CardsightApiKey) ? "Not configured" : "Configured (not tested)";
            EbayStatus = (string.IsNullOrWhiteSpace(EbayClientId) || string.IsNullOrWhiteSpace(EbayClientSecret))
                ? "Not configured"
                : "Configured (not tested)";
            EbayConnectStatus = !string.IsNullOrEmpty(s.EbayAccessToken) ? "Connected ✓" : "Not connected";
            EbayPoliciesStatus = !string.IsNullOrEmpty(s.EbayFulfillmentPolicyId) ? "Policies loaded ✓" : "Not fetched";

            DbPath = FlipKitDbContext.GetDbPath();
        }

        private async void LoadCardCountAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                CardCount = await db.Cards.CountAsync();
            }
            catch
            {
                CardCount = 0;
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            // Validate all templates before saving
            var templates = new[]
            {
                ("Whatnot Title", WhatnotTitleTemplate),
                ("eBay Title", EbayTitleTemplate),
                ("COMC Title", ComcTitleTemplate),
                ("Generic Title", GenericTitleTemplate),
                ("Terapeak Search", TerapeakSearchTemplate),
                ("eBay Search", EbaySearchTemplate)
            };

            foreach (var (name, template) in templates)
            {
                var (isValid, errorMessage) = TitleTemplateService.ValidateTemplate(template);
                if (!isValid)
                {
                    SaveMessage = $"{name} template error: {errorMessage}";
                    TemplateValidationMessage = SaveMessage;
                    SearchTemplateValidationMessage = SaveMessage;
                    return;
                }
            }

            // Preserve OAuth tokens and policy IDs set by the Connect flow — they aren't
            // exposed in the UI and must not be overwritten with empty defaults on save.
            var current = _settingsService.Load();

            var s = new AppSettings
            {
                OpenRouterApiKey = OpenRouterApiKey,
                ImgBBApiKey = ImgBBApiKey,
                CardsightApiKey = CardsightApiKey,
                MinCardsightConfidence = current.MinCardsightConfidence,
                EbayClientId = EbayClientId,
                EbayClientSecret = EbayClientSecret,
                EbayRuName = EbayRuName,
                EbayAccessToken = current.EbayAccessToken,
                EbayRefreshToken = current.EbayRefreshToken,
                EbayTokenExpiry = current.EbayTokenExpiry,
                EbayFulfillmentPolicyId = current.EbayFulfillmentPolicyId,
                EbayPaymentPolicyId = current.EbayPaymentPolicyId,
                EbayReturnPolicyId = current.EbayReturnPolicyId,
                IsEbaySeller = IsEbaySeller,
                DefaultShippingProfile = DefaultShippingProfile,
                DefaultCondition = DefaultCondition,
                DefaultModel = DefaultModel,
                EnableVariationVerification = EnableVariationVerification,
                AutoApplyHighConfidenceSuggestions = AutoApplyHighConfidenceSuggestions,
                RunConfirmationPass = RunConfirmationPass,
                EnableChecklistLearning = EnableChecklistLearning,
                AutoAcceptTier1Matches = AutoAcceptTier1Matches,
                MaxConcurrentScans = MaxConcurrentScans,
                WhatnotFeePercent = WhatnotFeePercent,
                EbayFeePercent = EbayFeePercent,
                DefaultShippingCostPwe = DefaultShippingCostPwe,
                DefaultShippingCostBmwt = DefaultShippingCostBmwt,
                PriceStalenessThresholdDays = PriceStalenessThresholdDays,
                WhatnotTitleTemplate = WhatnotTitleTemplate,
                EbayTitleTemplate = EbayTitleTemplate,
                ComcTitleTemplate = ComcTitleTemplate,
                GenericTitleTemplate = GenericTitleTemplate,
                ActiveExportPlatform = ActiveExportPlatform,
                TerapeakSearchTemplate = TerapeakSearchTemplate,
                EbaySearchTemplate = EbaySearchTemplate,
                EbaySellerLocation = EbaySellerLocation,
                EbayDispatchTimeMax = EbayDispatchTimeMax,
                EbayReturnsAccepted = EbayReturnsAccepted,
                EbayUseVerifyAdd = EbayUseVerifyAdd,
                SyncServerUrl = SyncServerUrl,
                AutoStartWebServer = AutoStartWebServer,
                AutoStartApiServer = AutoStartApiServer,
                WebServerPort = WebServerPort,
                ApiServerPort = ApiServerPort,
                MinimizeToTray = MinimizeToTray,
                AutoOpenBrowser = AutoOpenBrowser,
                WebcamCaptureEnabled = WebcamCaptureEnabled,
                // Preserve whatever the dialog last persisted unless the user
                // explicitly picked something in this Settings session. Reading
                // the saved values back keeps "Detect cameras" + select-but-not-save
                // safe — switching device only sticks once the user hits Save.
                PreferredCameraIndex = SelectedWebcamDevice?.Index ?? _settingsService.Load().PreferredCameraIndex,
                PreferredCameraName = SelectedWebcamDevice?.Name ?? _settingsService.Load().PreferredCameraName,
            };

            _settingsService.Save(s);
            UpdateDataAccessMode();
            SaveMessage = "Settings saved!";
            TemplateValidationMessage = string.Empty;
        }

        [RelayCommand]
        private async Task TestOpenRouterAsync()
        {
            IsTestingOpenRouter = true;
            OpenRouterStatus = "Testing...";

            var success = await _settingsService.TestOpenRouterConnectionAsync(OpenRouterApiKey);
            OpenRouterStatus = success ? "Connected!" : "Connection failed";

            IsTestingOpenRouter = false;
        }

        [RelayCommand]
        private async Task TestImgBBAsync()
        {
            IsTestingImgBB = true;
            ImgBBStatus = "Testing...";

            var success = await _settingsService.TestImgBBConnectionAsync(ImgBBApiKey);
            ImgBBStatus = success ? "Connected!" : "Connection failed";

            IsTestingImgBB = false;
        }

        [RelayCommand]
        private async Task TestCardsightAsync()
        {
            IsTestingCardsight = true;
            CardsightStatus = "Testing...";

            var success = await _settingsService.TestCardsightConnectionAsync(CardsightApiKey);
            CardsightStatus = success ? "Connected!" : "Connection failed";

            IsTestingCardsight = false;
        }

        [RelayCommand]
        private async Task TestEbayAsync()
        {
            IsTestingEbay = true;
            EbayStatus = "Testing...";

            var success = await _settingsService.TestEbayConnectionAsync(EbayClientId, EbayClientSecret);
            EbayStatus = success ? "Connected!" : "Connection failed";

            IsTestingEbay = false;
        }

        [RelayCommand]
        private void ConnectEbayAccount()
        {
            if (_ebayPublishingService == null)
            {
                EbayConnectStatus = "Service not available.";
                return;
            }

            // Persist Client ID, Secret, and RuName before building the URL
            SaveSettings();

            string authUrl;
            try { authUrl = _ebayPublishingService.BuildAuthorizationUrl(); }
            catch (Exception ex) { EbayConnectStatus = $"Config error: {ex.Message}"; return; }

            EbayAuthCode = string.Empty;
            IsAwaitingEbayCode = true;
            EbayConnectStatus = "Browser opened — authorize FlipKit, then paste the code below.";
            _browserService.OpenUrl(authUrl);
        }

        [RelayCommand]
        private async Task SubmitEbayAuthCodeAsync()
        {
            if (_ebayPublishingService == null) return;
            if (string.IsNullOrWhiteSpace(EbayAuthCode))
            {
                EbayConnectStatus = "Paste the authorization code from eBay first.";
                return;
            }

            var code = ExtractAuthCode(EbayAuthCode.Trim());
            if (string.IsNullOrEmpty(code))
            {
                EbayConnectStatus = "Couldn't find an authorization code in that text.";
                return;
            }

            IsConnectingEbay = true;
            EbayConnectStatus = "Exchanging code for tokens…";
            try
            {
                await _ebayPublishingService.ExchangeCodeForTokensAsync(code);
                EbayConnectStatus = "Connected ✓";
                IsAwaitingEbayCode = false;
                EbayAuthCode = string.Empty;
            }
            catch (Exception ex)
            {
                EbayConnectStatus = $"Failed: {ex.Message}";
            }
            finally
            {
                IsConnectingEbay = false;
            }
        }

        // Accepts either a raw code or a full redirect URL like
        // https://auth.ebay.com/...?code=v%5E1.1%23i...&expires_in=299
        // and returns the URL-decoded code value.
        private static string ExtractAuthCode(string input)
        {
            if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var c = query["code"];
                    if (!string.IsNullOrEmpty(c)) return c;
                }
            }
            // Raw paste — eBay codes are URL-encoded in the redirect, so decode any escapes.
            return System.Web.HttpUtility.UrlDecode(input);
        }

        [RelayCommand]
        private void CancelEbayAuth()
        {
            IsAwaitingEbayCode = false;
            EbayAuthCode = string.Empty;
            EbayConnectStatus = "Authorization cancelled.";
        }

        [RelayCommand]
        private async Task FetchEbayPoliciesAsync()
        {
            if (_ebayPublishingService == null)
            {
                EbayPoliciesStatus = "Service not available.";
                return;
            }

            IsFetchingEbayPolicies = true;
            EbayPoliciesStatus = "Fetching policies…";
            try
            {
                var ok = await _ebayPublishingService.FetchAndStorePoliciesAsync();
                EbayPoliciesStatus = ok ? "Policies loaded ✓" : "Failed — check connection and retry.";
            }
            finally
            {
                IsFetchingEbayPolicies = false;
            }
        }

        [RelayCommand]
        private void OpenDataFolder()
        {
            var folder = Path.GetDirectoryName(DbPath);
            if (folder != null && Directory.Exists(folder))
            {
                _browserService.OpenUrl(folder);
            }
        }

        [RelayCommand]
        private void OpenLogFolder()
        {
            // Match the Serilog paths from App.axaml.cs — dev build first, then published.
            var devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "Docs", "debug"));
            var publishedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipKit", "logs");

            var folder = Directory.Exists(devPath) && Directory.GetFiles(devPath, "*.log").Length > 0
                ? devPath
                : publishedPath;

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _browserService.OpenUrl(folder);
        }

        [RelayCommand]
        private void OpenScanLogsFolder()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipKit", "BulkScanLogs");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _browserService.OpenUrl(folder);
        }

        [RelayCommand]
        private void ResetTitleTemplates()
        {
            WhatnotTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Whatnot);
            EbayTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.eBay);
            ComcTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.COMC);
            GenericTitleTemplate = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Generic);
            TemplateValidationMessage = "Templates reset to defaults";
        }

        [RelayCommand]
        private void ValidateCurrentTemplate()
        {
            var template = ActiveExportPlatform switch
            {
                ExportPlatform.Whatnot => WhatnotTitleTemplate,
                ExportPlatform.eBay => EbayTitleTemplate,
                ExportPlatform.COMC => ComcTitleTemplate,
                _ => GenericTitleTemplate
            };

            var (isValid, errorMessage) = TitleTemplateService.ValidateTemplate(template);
            TemplateValidationMessage = isValid ? "✓ Template is valid" : $"✗ {errorMessage}";
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            // Get a sample card from the database for preview
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                var sampleCard = await db.Cards.FirstOrDefaultAsync();

                if (sampleCard == null)
                {
                    TemplatePreview = "No cards in database. Add a card to see preview.";
                    return;
                }

                var template = ActiveExportPlatform switch
                {
                    ExportPlatform.Whatnot => WhatnotTitleTemplate,
                    ExportPlatform.eBay => EbayTitleTemplate,
                    ExportPlatform.COMC => ComcTitleTemplate,
                    _ => GenericTitleTemplate
                };

                var titleService = new TitleTemplateService();
                TemplatePreview = $"Preview: {titleService.GenerateTitle(sampleCard, template)}";
            }
            catch (Exception ex)
            {
                TemplatePreview = $"Error generating preview: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ResetSearchTemplates()
        {
            TerapeakSearchTemplate = "{Year} {Brand} {Player} {Parallel} {Attributes} {Grade}";
            EbaySearchTemplate = "{Year} {Manufacturer} {Brand} {Player} {Team} {Parallel} {Attributes} {Grade}";
            SearchTemplateValidationMessage = "Search templates reset to defaults";
        }

        [RelayCommand]
        private void ValidateSearchTemplates()
        {
            var templates = new[]
            {
                ("Terapeak", TerapeakSearchTemplate),
                ("eBay", EbaySearchTemplate)
            };

            foreach (var (name, template) in templates)
            {
                var (isValid, errorMessage) = TitleTemplateService.ValidateTemplate(template);
                if (!isValid)
                {
                    SearchTemplateValidationMessage = $"✗ {name}: {errorMessage}";
                    return;
                }
            }

            SearchTemplateValidationMessage = "✓ All search templates are valid";
        }

        [RelayCommand]
        private async Task GenerateSearchPreviewAsync()
        {
            // Get a sample card from the database for preview
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlipKitDbContext>();
                var sampleCard = await db.Cards.FirstOrDefaultAsync();

                if (sampleCard == null)
                {
                    SearchTemplatePreview = "No cards in database. Add a card to see preview.";
                    return;
                }

                var titleService = new TitleTemplateService();
                var terapeakQuery = titleService.GenerateTitle(sampleCard, TerapeakSearchTemplate);
                var ebayQuery = titleService.GenerateTitle(sampleCard, EbaySearchTemplate);

                SearchTemplatePreview = $"Terapeak: {terapeakQuery}\neBay: {ebayQuery}";
            }
            catch (Exception ex)
            {
                SearchTemplatePreview = $"Error generating preview: {ex.Message}";
            }
        }

        private void UpdateDataAccessMode()
        {
            var settings = _settingsService.Load();
            var mode = DataAccessModeDetector.DetectMode(settings);

            DataAccessMode = DataAccessModeDetector.GetModeDescription(mode);
            DataAccessModeColor = mode == Core.Helpers.DataAccessMode.Local ? "Green" : "Blue";
        }

        // Server Management Commands

        [RelayCommand]
        private async Task StartWebServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                WebServerStatus = "Starting...";
                var result = await _serverManagement.StartWebServerAsync(WebServerPort);

                if (result.Success)
                {
                    ActualWebPort = result.ActualPort;
                    WebServerStatus = $"Running on port {result.ActualPort}";
                    UpdateLocalIpAddresses();

                    if (AutoOpenBrowser && result.ActualPort > 0)
                    {
                        _browserService.OpenUrl($"http://localhost:{result.ActualPort}");
                    }
                }
                else
                {
                    WebServerStatus = $"Failed: {result.ErrorMessage}";
                }
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task StopWebServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                WebServerStatus = "Stopping...";
                await _serverManagement.StopWebServerAsync();
                WebServerStatus = "Stopped";
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task StartApiServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                ApiServerStatus = "Starting...";
                var result = await _serverManagement.StartApiServerAsync(ApiServerPort);

                if (result.Success)
                {
                    ActualApiPort = result.ActualPort;
                    ApiServerStatus = $"Running on port {result.ActualPort}";
                }
                else
                {
                    ApiServerStatus = $"Failed: {result.ErrorMessage}";
                }
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task StopApiServerAsync()
        {
            _explicitOperationInProgress = true;
            try
            {
                ApiServerStatus = "Stopping...";
                await _serverManagement.StopApiServerAsync();
                ApiServerStatus = "Stopped";
            }
            finally
            {
                _explicitOperationInProgress = false;
            }
        }

        [RelayCommand]
        private void OpenWebBrowser()
        {
            var port = IsWebRunning ? ActualWebPort : WebServerPort;
            _browserService.OpenUrl($"http://localhost:{port}");
        }

        [RelayCommand]
        private void RefreshServerLogs()
        {
            var webLogs = _serverManagement.GetWebServerLogs();
            var apiLogs = _serverManagement.GetApiServerLogs();

            WebServerLogs = string.Join(Environment.NewLine, webLogs);
            ApiServerLogs = string.Join(Environment.NewLine, apiLogs);
        }

        [RelayCommand]
        private void ClearWebLogs()
        {
            _serverManagement.ClearWebServerLogs();
            WebServerLogs = string.Empty;
        }

        [RelayCommand]
        private void ClearApiLogs()
        {
            _serverManagement.ClearApiServerLogs();
            ApiServerLogs = string.Empty;
        }

        private void UpdateServerStatus()
        {
            var status = _serverManagement.GetServerStatus();

            IsWebRunning = status.IsWebRunning;
            IsApiRunning = status.IsApiRunning;

            // Phase 5.10 fix — when an explicit user Start/Stop command is mid-flight,
            // skip the status-message overwrite. The command sets its own success/failure
            // message; the periodic Timer must not clobber it. Without this gate,
            // IsWebRunning=false (lagging) caused "Failed: port in use" to be replaced
            // by "Stopped" within ~2 seconds.
            if (!_explicitOperationInProgress)
            {
                if (status.IsWebRunning)
                {
                    ActualWebPort = status.WebPort;
                    WebServerStatus = $"Running on port {status.WebPort}";
                }
                else if (WebServerStatus != "Starting..." && WebServerStatus != "Stopping...")
                {
                    WebServerStatus = "Stopped";
                }

                if (status.IsApiRunning)
                {
                    ActualApiPort = status.ApiPort;
                    ApiServerStatus = $"Running on port {status.ApiPort}";
                }
                else if (ApiServerStatus != "Starting..." && ApiServerStatus != "Stopping...")
                {
                    ApiServerStatus = "Stopped";
                }
            }

            // Refresh logs if servers are running
            if (IsWebRunning || IsApiRunning)
            {
                RefreshServerLogs();
            }
        }

        private void UpdateLocalIpAddresses()
        {
            // Phase 5c — delegated to INetworkAddressProvider so the testable parts of
            // the network/QR logic live outside the VM. The VM's only remaining job is
            // to apply the provider's snapshot to its bindable [ObservableProperty]
            // fields (XAML/test contract preserved).
            var info = _networkAddresses.GetCurrent(ActualWebPort, IsWebRunning);

            LocalNetworkIp = info.LocalNetworkIp;
            TailscaleIp = info.TailscaleIp;
            IsLocalNetworkAvailable = info.IsLocalNetworkAvailable;
            IsTailscaleAvailable = info.IsTailscaleAvailable;
            LocalNetworkStatus = info.LocalNetworkStatus;
            TailscaleStatus = info.TailscaleStatus;
            LocalNetworkUrl = info.LocalNetworkUrl;
            TailscaleUrl = info.TailscaleUrl;
            LocalQrCodeBitmap = info.LocalQrCodeBitmap;
            TailscaleQrCodeBitmap = info.TailscaleQrCodeBitmap;
            LocalIpAddresses = info.LegacyLocalIpAddresses;
            QrCodeBitmap = info.LegacyQrCodeBitmap;
        }

        [RelayCommand]
        private void OpenTailscaleGuide()
        {
            // Detect OS and open appropriate guide
            var guidePath = OperatingSystem.IsWindows() ? "Tailscale-Setup-Windows.md"
                : OperatingSystem.IsMacOS() ? "Tailscale-Setup-Mac.md"
                : "Tailscale-Setup-Linux.md";

            // Try to open local documentation first
            var docsPath = Path.Combine(AppContext.BaseDirectory, "Docs", guidePath);
            if (File.Exists(docsPath))
            {
                _browserService.OpenUrl(docsPath);
            }
            else
            {
                // Fall back to Tailscale download page
                _browserService.OpenUrl("https://tailscale.com/download");
            }
        }

        [RelayCommand]
        private void RefreshNetworkStatus()
        {
            UpdateLocalIpAddresses();
        }

        [RelayCommand]
        private async Task RefreshWebcamDevicesAsync()
        {
            if (_cameraService is null)
            {
                WebcamProbeStatus = "Camera service not available.";
                return;
            }

            IsProbingWebcams = true;
            WebcamProbeStatus = "Probing cameras…";
            try
            {
                var found = await _cameraService.ListDevicesAsync();
                WebcamDevices.Clear();
                foreach (var d in found)
                    WebcamDevices.Add(d);

                // Restore the saved selection if still present so Save doesn't
                // accidentally null it out by reading SelectedWebcamDevice.
                var saved = _settingsService.Load();
                CameraDevice? choice = null;
                if (saved.PreferredCameraIndex.HasValue)
                    choice = WebcamDevices.FirstOrDefault(d => d.Index == saved.PreferredCameraIndex.Value);
                if (choice is null && !string.IsNullOrEmpty(saved.PreferredCameraName))
                    choice = WebcamDevices.FirstOrDefault(d => string.Equals(d.Name, saved.PreferredCameraName, StringComparison.OrdinalIgnoreCase));
                SelectedWebcamDevice = choice ?? WebcamDevices.FirstOrDefault();

                WebcamProbeStatus = WebcamDevices.Count == 0
                    ? "No cameras detected. Connect a webcam and try again."
                    : $"Found {WebcamDevices.Count} camera(s).";
            }
            catch (Exception ex)
            {
                WebcamProbeStatus = $"Probe failed: {ex.Message}";
            }
            finally
            {
                IsProbingWebcams = false;
            }
        }

        [RelayCommand]
        private async Task TestWebcamCaptureAsync()
        {
            if (_webcamCaptureDialog is null)
            {
                WebcamProbeStatus = "Webcam dialog service not available.";
                return;
            }

            // Persist the current pick before opening so the dialog defaults to it.
            // (Avoids confusing UX where the test ignores the in-flight selection.)
            if (SelectedWebcamDevice is { } d)
            {
                var s = _settingsService.Load();
                s.PreferredCameraIndex = d.Index;
                s.PreferredCameraName = d.Name;
                _settingsService.Save(s);
            }

            var path = await _webcamCaptureDialog.CaptureAsync();
            WebcamProbeStatus = string.IsNullOrEmpty(path)
                ? "Test capture cancelled."
                : $"Test capture saved: {path}";
        }

        // Handler for IAppNotificationService.ScanBatchCompleted. Re-fetches
        // the OpenRouter usage tiles whenever a Bulk Scan / Inventory Enhance
        // / Surprise Set Enhance batch finishes. Fire-and-forget — the refresh
        // method is idempotent and handles its own error states.
        private void OnScanBatchCompleted(object? sender, EventArgs e)
        {
            _ = RefreshKeyInfoAsync();
            _ = RefreshCardsightSubscriptionAsync();
            _ = LoadLeaderboardAsync();
        }

        public void Dispose()
        {
            _statusRefreshTimer?.Dispose();
            if (_appNotifications != null)
                _appNotifications.ScanBatchCompleted -= OnScanBatchCompleted;
        }
    }
}
