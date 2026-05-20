using Microsoft.AspNetCore.Mvc;
using FlipKit.Core.Services;
using FlipKit.Web.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Web.Controllers
{
    /// <summary>
    /// Settings controller for configuring API keys and preferences.
    /// In Docker mode, this provides the only way to configure the application.
    /// In Desktop mode, redirects to use the Desktop app for settings.
    /// </summary>
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly IOpenRouterModelCatalog _modelCatalog;
        private readonly IOpenRouterKeyInfoService _keyInfoService;
        private readonly ICardsightSubscriptionService _cardsightSubscriptionService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            ISettingsService settingsService,
            IOpenRouterModelCatalog modelCatalog,
            IOpenRouterKeyInfoService keyInfoService,
            ICardsightSubscriptionService cardsightSubscriptionService,
            ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _modelCatalog = modelCatalog;
            _keyInfoService = keyInfoService;
            _cardsightSubscriptionService = cardsightSubscriptionService;
            _logger = logger;
        }

        private bool IsDockerEnvironment()
        {
            // Check if running in Docker (FLIPKIT_DB_PATH is set to /data/*)
            var dbPath = Environment.GetEnvironmentVariable("FLIPKIT_DB_PATH");
            return !string.IsNullOrEmpty(dbPath) && dbPath.StartsWith("/data");
        }

        public async Task<IActionResult> Index()
        {
            // In non-Docker mode, redirect to Desktop app
            if (!IsDockerEnvironment())
            {
                TempData["InfoMessage"] = "Settings are managed in the FlipKit Desktop application when running locally.";
                return RedirectToAction("Index", "Scan");
            }

            var settings = _settingsService.Load();

            var viewModel = new SettingsViewModel
            {
                // Don't expose full API keys, just show if they're configured
                OpenRouterApiKey = string.IsNullOrEmpty(settings.OpenRouterApiKey) ? "" : "••••••••" + settings.OpenRouterApiKey[^4..],
                ImgBBApiKey = string.IsNullOrEmpty(settings.ImgBBApiKey) ? "" : "••••••••" + settings.ImgBBApiKey[^4..],
                CardsightApiKey = string.IsNullOrEmpty(settings.CardsightApiKey) ? "" : "••••••••" + settings.CardsightApiKey[^4..],
                EbayClientId = string.IsNullOrEmpty(settings.EbayClientId) ? "" : "••••••••" + settings.EbayClientId[^4..],
                EbayClientSecret = string.IsNullOrEmpty(settings.EbayClientSecret) ? "" : "••••••••" + settings.EbayClientSecret[^4..],
                HasOpenRouterKey = !string.IsNullOrEmpty(settings.OpenRouterApiKey),
                HasImgBBKey = !string.IsNullOrEmpty(settings.ImgBBApiKey),
                HasCardsightKey = !string.IsNullOrEmpty(settings.CardsightApiKey),
                HasEbayCredentials = !string.IsNullOrEmpty(settings.EbayClientId) && !string.IsNullOrEmpty(settings.EbayClientSecret),
                WhatnotFeePercent = settings.WhatnotFeePercent,
                EbayFeePercent = settings.EbayFeePercent,
                DefaultShippingCostPwe = settings.DefaultShippingCostPwe,
                DefaultShippingCostBmwt = settings.DefaultShippingCostBmwt,
                DefaultModel = settings.DefaultModel,
                EnableVariationVerification = settings.EnableVariationVerification,
                AutoApplyHighConfidenceSuggestions = settings.AutoApplyHighConfidenceSuggestions,
                EnableChecklistLearning = settings.EnableChecklistLearning,
                PriceStalenessThresholdDays = settings.PriceStalenessThresholdDays,
                ActiveExportPlatform = settings.ActiveExportPlatform,
                IsDockerEnvironment = true
            };

            try
            {
                var catalog = await _modelCatalog.GetAsync();
                viewModel.FreeModels = catalog.FreeVisionModels;
                viewModel.PaidModels = catalog.PaidVisionModels;
                if (catalog.IsFallback)
                    viewModel.CatalogError = "Couldn't reach OpenRouter for the live model list — showing cached fallback models.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog fetch failed on Settings/Index GET");
                viewModel.CatalogError = $"Model catalog failed to load: {ex.Message}";
            }

            // OpenRouter key-info / Usage panel — fetches credits remaining +
            // daily/weekly/monthly burn so the Settings page surfaces billing
            // visibility before the user kicks off a scan. Best-effort: any
            // failure (no key, 402, 429, network) populates OpenRouterUsageError
            // so the Razor view renders a graceful inline message instead of
            // crashing the page.
            if (viewModel.HasOpenRouterKey)
            {
                try
                {
                    viewModel.OpenRouterUsage = await _keyInfoService.GetAsync();
                }
                catch (OpenRouterPaymentRequiredException pEx)
                {
                    viewModel.OpenRouterUsageError =
                        $"Payment required — {pEx.ResponseBody ?? "credit balance is negative."}";
                }
                catch (OpenRouterRateLimitException rlEx)
                {
                    viewModel.OpenRouterUsageError =
                        $"Rate limited (scope: {rlEx.Scope}). Try again in a minute.";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Key-info fetch failed on Settings/Index GET");
                    viewModel.OpenRouterUsageError = $"Couldn't load usage: {ex.Message}";
                }
            }

            // CardSight Usage card — fetches calls-used this period so the
            // Settings page surfaces how much of the free-tier allowance has
            // been consumed. Best-effort: any failure populates
            // CardsightUsageError so the Razor view renders a graceful inline
            // message instead of crashing the page.
            if (viewModel.HasCardsightKey)
            {
                try
                {
                    viewModel.CardsightUsage = await _cardsightSubscriptionService.GetAsync();
                }
                catch (CardsightException cex)
                {
                    viewModel.CardsightUsageError = cex.Reason switch
                    {
                        CardsightFailureReason.NotConfigured => "Enter your CardSight key to see usage.",
                        CardsightFailureReason.InvalidKey => "CardSight rejected the key — double-check it.",
                        CardsightFailureReason.QuotaExceeded => "CardSight quota exceeded for this billing period.",
                        CardsightFailureReason.RateLimited => "CardSight is rate limiting requests. Try again in a minute.",
                        _ => $"Couldn't load CardSight usage: {cex.Message}"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CardSight subscription fetch failed on Settings/Index GET");
                    viewModel.CardsightUsageError = $"Couldn't load CardSight usage: {ex.Message}";
                }
            }

            return View(viewModel);
        }

        /// <summary>
        /// POST endpoint for the Refresh button in the Usage card. Just bounces
        /// back to <see cref="Index"/> which re-fetches.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RefreshUsage()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SettingsViewModel model)
        {
            if (!IsDockerEnvironment())
            {
                return RedirectToAction("Index", "Scan");
            }

            if (!ModelState.IsValid)
            {
                model.IsDockerEnvironment = true;
                try
                {
                    var catalog = await _modelCatalog.GetAsync();
                    model.FreeModels = catalog.FreeVisionModels;
                    model.PaidModels = catalog.PaidVisionModels;
                }
                catch { /* catalog failure is non-fatal on validation error re-render */ }
                return View("Index", model);
            }

            try
            {
                var settings = _settingsService.Load();

                // Only update API keys if a new value was provided (not the masked placeholder)
                if (!string.IsNullOrEmpty(model.OpenRouterApiKey) && !model.OpenRouterApiKey.StartsWith("••••"))
                {
                    settings.OpenRouterApiKey = model.OpenRouterApiKey.Trim();
                }

                if (!string.IsNullOrEmpty(model.ImgBBApiKey) && !model.ImgBBApiKey.StartsWith("••••"))
                {
                    settings.ImgBBApiKey = model.ImgBBApiKey.Trim();
                }

                if (!string.IsNullOrEmpty(model.CardsightApiKey) && !model.CardsightApiKey.StartsWith("••••"))
                {
                    settings.CardsightApiKey = model.CardsightApiKey.Trim();
                }

                if (!string.IsNullOrEmpty(model.EbayClientId) && !model.EbayClientId.StartsWith("••••"))
                {
                    settings.EbayClientId = model.EbayClientId.Trim();
                }

                if (!string.IsNullOrEmpty(model.EbayClientSecret) && !model.EbayClientSecret.StartsWith("••••"))
                {
                    settings.EbayClientSecret = model.EbayClientSecret.Trim();
                }

                // Update other settings
                settings.WhatnotFeePercent = model.WhatnotFeePercent;
                settings.EbayFeePercent = model.EbayFeePercent;
                settings.DefaultShippingCostPwe = model.DefaultShippingCostPwe;
                settings.DefaultShippingCostBmwt = model.DefaultShippingCostBmwt;
                settings.DefaultModel = model.DefaultModel;
                settings.EnableVariationVerification = model.EnableVariationVerification;
                settings.AutoApplyHighConfidenceSuggestions = model.AutoApplyHighConfidenceSuggestions;
                settings.EnableChecklistLearning = model.EnableChecklistLearning;
                settings.PriceStalenessThresholdDays = model.PriceStalenessThresholdDays;
                settings.ActiveExportPlatform = model.ActiveExportPlatform;

                _settingsService.Save(settings);

                _logger.LogInformation("Settings saved successfully");
                TempData["SuccessMessage"] = "Settings saved successfully!";

                // Test OpenRouter connection if key was updated
                if (!string.IsNullOrEmpty(model.OpenRouterApiKey) && !model.OpenRouterApiKey.StartsWith("••••"))
                {
                    var isValid = await _settingsService.TestOpenRouterConnectionAsync(settings.OpenRouterApiKey!);
                    if (!isValid)
                    {
                        TempData["WarningMessage"] = "Settings saved, but OpenRouter API key could not be validated. Please check the key.";
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                TempData["ErrorMessage"] = $"Failed to save settings: {ex.Message}";
                model.IsDockerEnvironment = true;
                return View("Index", model);
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken] // Test connection is read-only, no CSRF risk
        public async Task<IActionResult> TestConnection(string service)
        {
            var settings = _settingsService.Load();

            if (service == "openrouter")
            {
                if (string.IsNullOrEmpty(settings.OpenRouterApiKey))
                {
                    return Json(new { success = false, message = "No API key configured" });
                }

                var isValid = await _settingsService.TestOpenRouterConnectionAsync(settings.OpenRouterApiKey);
                return Json(new { success = isValid, message = isValid ? "Connection successful!" : "Connection failed" });
            }
            else if (service == "imgbb")
            {
                if (string.IsNullOrEmpty(settings.ImgBBApiKey))
                {
                    return Json(new { success = false, message = "No API key configured" });
                }

                var isValid = await _settingsService.TestImgBBConnectionAsync(settings.ImgBBApiKey);
                return Json(new { success = isValid, message = isValid ? "Connection successful!" : "Connection failed" });
            }
            else if (service == "cardsight")
            {
                if (string.IsNullOrEmpty(settings.CardsightApiKey))
                {
                    return Json(new { success = false, message = "No API key configured" });
                }

                var isValid = await _settingsService.TestCardsightConnectionAsync(settings.CardsightApiKey);
                return Json(new { success = isValid, message = isValid ? "Connection successful!" : "Connection failed" });
            }

            return Json(new { success = false, message = "Unknown service" });
        }
    }
}
