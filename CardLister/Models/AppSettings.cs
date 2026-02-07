using System.Collections.Generic;
using CardLister.Models.Enums;
using CardLister.Services;

namespace CardLister.Models
{
    public class AppSettings
    {
        public string? OpenRouterApiKey { get; set; }
        public string? ImgBBApiKey { get; set; }
        public bool IsEbaySeller { get; set; }
        public string DefaultShippingProfile { get; set; } = "4 oz";
        public string DefaultCondition { get; set; } = "Near Mint";
        public decimal WhatnotFeePercent { get; set; } = 11.0m;
        public decimal EbayFeePercent { get; set; } = 13.25m;
        public decimal DefaultShippingCostPwe { get; set; } = 1.00m;
        public decimal DefaultShippingCostBmwt { get; set; } = 4.50m;
        public int PriceStalenessThresholdDays { get; set; } = 30;
        public string DefaultModel { get; set; } = "nvidia/nemotron-nano-12b-v2-vl:free";
        public bool EnableVariationVerification { get; set; } = true;
        public bool AutoApplyHighConfidenceSuggestions { get; set; } = true;
        public bool RunConfirmationPass { get; set; } = true;
        public bool EnableChecklistLearning { get; set; } = true;
        public List<string> CustomGradingCompanies { get; set; } = new();

        // Title Templates - SEO-optimized for each platform
        // Based on WTSCards research on platform search algorithms
        public string WhatnotTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Whatnot);
        public string EbayTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.eBay);
        public string ComcTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.COMC);
        public string GenericTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Generic);

        // Active export platform (used for exports)
        public ExportPlatform ActiveExportPlatform { get; set; } = ExportPlatform.Whatnot;

        // Search Query Templates - Optimized for pricing research
        // Exclude overly specific fields (CardNumber, Serial) to get broader results
        // Terapeak: Focus on key identifiers without team (already covered by player)
        public string TerapeakSearchTemplate { get; set; } = "{Year} {Brand} {Player} {Parallel} {Attributes} {Grade}";

        // eBay Sold: More comprehensive with manufacturer and team for better filtering
        public string EbaySearchTemplate { get; set; } = "{Year} {Manufacturer} {Brand} {Player} {Team} {Parallel} {Attributes} {Grade}";
    }
}
