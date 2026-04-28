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
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        private bool IsDockerEnvironment()
        {
            // Check if running in Docker (FLIPKIT_DB_PATH is set to /data/*)
            var dbPath = Environment.GetEnvironmentVariable("FLIPKIT_DB_PATH");
            return !string.IsNullOrEmpty(dbPath) && dbPath.StartsWith("/data");
        }

        public IActionResult Index()
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
                HasOpenRouterKey = !string.IsNullOrEmpty(settings.OpenRouterApiKey),
                HasImgBBKey = !string.IsNullOrEmpty(settings.ImgBBApiKey),
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

            return View(viewModel);
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
        [ValidateAntiForgeryToken]
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

            return Json(new { success = false, message = "Unknown service" });
        }
    }
}
