using System.ComponentModel.DataAnnotations;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Web.Models
{
    public class SettingsViewModel
    {
        // API Keys (masked for display, full value for saving)
        [Display(Name = "OpenRouter API Key")]
        public string? OpenRouterApiKey { get; set; }

        [Display(Name = "ImgBB API Key")]
        public string? ImgBBApiKey { get; set; }

        [Display(Name = "Ximilar API Key")]
        public string? XimilarApiKey { get; set; }

        // Fee Settings
        [Display(Name = "Whatnot Fee %")]
        [Range(0, 100)]
        public decimal WhatnotFeePercent { get; set; } = 11.0m;

        [Display(Name = "eBay Fee %")]
        [Range(0, 100)]
        public decimal EbayFeePercent { get; set; } = 13.25m;

        // Shipping Costs
        [Display(Name = "PWE Shipping Cost")]
        [Range(0, 100)]
        public decimal DefaultShippingCostPwe { get; set; } = 1.00m;

        [Display(Name = "BMWT Shipping Cost")]
        [Range(0, 100)]
        public decimal DefaultShippingCostBmwt { get; set; } = 4.50m;

        // AI Settings
        [Display(Name = "Default AI Model")]
        public string DefaultModel { get; set; } = "nvidia/nemotron-nano-12b-v2-vl:free";

        // Verification Settings
        [Display(Name = "Enable Variation Verification")]
        public bool EnableVariationVerification { get; set; } = true;

        [Display(Name = "Auto-Apply High Confidence Suggestions")]
        public bool AutoApplyHighConfidenceSuggestions { get; set; } = true;

        [Display(Name = "Enable Checklist Learning")]
        public bool EnableChecklistLearning { get; set; } = true;

        // Pricing Settings
        [Display(Name = "Price Staleness Threshold (Days)")]
        [Range(1, 365)]
        public int PriceStalenessThresholdDays { get; set; } = 30;

        // Export Settings
        [Display(Name = "Default Export Platform")]
        public ExportPlatform ActiveExportPlatform { get; set; } = ExportPlatform.Whatnot;

        // Status flags (read-only for display)
        public bool HasOpenRouterKey { get; set; }
        public bool HasImgBBKey { get; set; }
        public bool HasXimilarKey { get; set; }
        public bool IsDockerEnvironment { get; set; }
    }
}
