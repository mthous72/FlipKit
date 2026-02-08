using Microsoft.AspNetCore.Mvc;
using CardLister.Core.Services;
using CardLister.Web.Models;

namespace CardLister.Web.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        // GET: Settings
        public IActionResult Index()
        {
            try
            {
                var settings = _settingsService.Load();
                var viewModel = new SettingsViewModel
                {
                    HasOpenRouterKey = !string.IsNullOrWhiteSpace(settings.OpenRouterApiKey),
                    HasImgBBKey = !string.IsNullOrWhiteSpace(settings.ImgBBApiKey),
                    DefaultModel = settings.DefaultModel,
                    WhatnotFeePercent = settings.WhatnotFeePercent,
                    EbayFeePercent = settings.EbayFeePercent,
                    DefaultShippingCostPwe = settings.DefaultShippingCostPwe,
                    DefaultShippingCostBmwt = settings.DefaultShippingCostBmwt,
                    PriceStalenessThresholdDays = settings.PriceStalenessThresholdDays
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
                return View(new SettingsViewModel());
            }
        }
    }
}
