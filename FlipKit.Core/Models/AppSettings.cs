using System;
using System.Collections.Generic;
using FlipKit.Core.Models.Enums;
using FlipKit.Core.Services.ApiModels;
using FlipKit.Core.Services;

namespace FlipKit.Core.Models
{
    public class AppSettings
    {
        public string? OpenRouterApiKey { get; set; }
        public string? ImgBBApiKey { get; set; }
        public string? XimilarApiKey { get; set; }

        // eBay Browse API OAuth credentials (client_credentials grant).
        // Replaced stale Finding API path on 2026-05-05; Browse API returns
        // active-listing comps rather than sold history.
        // Get at developer.ebay.com — Production app, OAuth scopes.
        // Empty = competitive pricing disabled; falls back to manual Terapeak/eBay links.
        public string? EbayClientId { get; set; }
        public string? EbayClientSecret { get; set; }
        public bool IsEbaySeller { get; set; }
        public string DefaultShippingProfile { get; set; } = "4 oz";
        public string DefaultCondition { get; set; } = "Near Mint";
        public decimal WhatnotFeePercent { get; set; } = 11.0m;
        public decimal EbayFeePercent { get; set; } = 13.25m;
        public decimal DefaultShippingCostPwe { get; set; } = 1.00m;
        public decimal DefaultShippingCostBmwt { get; set; } = 4.50m;
        public int PriceStalenessThresholdDays { get; set; } = 30;
        public string DefaultModel { get; set; } = OpenRouterModelDefaults.DefaultFreeModelId;
        public bool EnableVariationVerification { get; set; } = true;
        public bool AutoApplyHighConfidenceSuggestions { get; set; } = true;
        public bool RunConfirmationPass { get; set; } = true;
        public bool EnableChecklistLearning { get; set; } = true;

        // When true, single-card and BulkScan flows save Tier 1 (Verified) matches
        // automatically without requiring the user to confirm. Off by default —
        // power-user shortcut once they've built trust on a given set. Tier 2/3
        // matches always require manual review regardless. (Roadmap 1 Phase 2 §8d.)
        public bool AutoAcceptTier1Matches { get; set; } = false;
        public List<string> CustomGradingCompanies { get; set; } = new();

        // Title Templates - SEO-optimized for each platform
        // Based on WTSCards research on platform search algorithms
        public string WhatnotTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Whatnot);
        public string EbayTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.eBay);
        public string ComcTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.COMC);
        public string GenericTitleTemplate { get; set; } = TitleTemplateService.GetDefaultTemplate(ExportPlatform.Generic);

        // Active export platform (used for exports)
        public ExportPlatform ActiveExportPlatform { get; set; } = ExportPlatform.Whatnot;

        // SKU auto-generation — assigned to cards on first export if blank.
        // Format: {SkuPrefix}{N:DSkuPadWidth} (e.g. "FK-000123" with prefix "FK-", pad 6).
        public string SkuPrefix { get; set; } = "FK-";
        public int SkuPadWidth { get; set; } = 6;

        // eBay export defaults — populate before exporting eBay listings.
        // SellerLocation is required for the *Location column (zip code or "City, ST").
        public string EbaySellerLocation { get; set; } = string.Empty;
        public int EbayDispatchTimeMax { get; set; } = 2;
        public bool EbayReturnsAccepted { get; set; } = true;
        // When true, the Action column on every row is "VerifyAdd" — eBay validates the
        // listing without creating it. Useful as a dry-run before real submission.
        public bool EbayUseVerifyAdd { get; set; } = false;

        // Search Query Templates - Optimized for pricing research
        // Exclude overly specific fields (CardNumber, Serial) to get broader results
        // Terapeak: Focus on key identifiers without team (already covered by player)
        public string TerapeakSearchTemplate { get; set; } = "{Year} {Brand} {Player} {Parallel} {Attributes} {Grade}";

        // eBay Sold: More comprehensive with manufacturer and team for better filtering
        public string EbaySearchTemplate { get; set; } = "{Year} {Manufacturer} {Brand} {Player} {Team} {Parallel} {Attributes} {Grade}";

        // Smart eBay Query Mode - Intelligently includes fields based on available data
        public bool UseSmartEbayQuery { get; set; } = true;

        // Bulk Scan Concurrency Settings
        // For free models (:free suffix), use 1 to avoid rate limits with the 4-second delay
        // For paid models (with credits), use 3-4 for optimal performance
        public int MaxConcurrentScans { get; set; } = 1;

        // Data Access Mode - Auto-detected based on API URL
        // If empty/localhost: Uses local SQLite database (fast, direct access)
        // If Tailscale IP: Uses remote API (network access via FlipKit.Api)
        public string? SyncServerUrl { get; set; }  // e.g., "http://100.64.1.5:5000"

        // FlipKit Hub - Server Management Settings
        // Controls auto-start behavior and port configuration for embedded Web and API servers
        public bool AutoStartWebServer { get; set; } = true;
        public bool AutoStartApiServer { get; set; } = true;
        public int WebServerPort { get; set; } = 5000;
        public int ApiServerPort { get; set; } = 5001;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoOpenBrowser { get; set; } = true;

        // Centralized HTTP timeouts (Phase 5.3 — was hardcoded in ServerManagementService).
        // Health-check pings against the embedded servers should fail fast — they're
        // localhost. 2 seconds covers slow startup without hanging the UI.
        public int ServerHealthCheckTimeoutSeconds { get; set; } = 2;

        // Webcam capture (Roadmap #2 — Docs/27-WEBCAM-CAPTURE-PLAN.md).
        // Master toggle for the 📷 Webcam buttons on Scan / Edit. Auto-flipped to
        // false the first time ListDevicesAsync returns no cameras so the buttons
        // hide on machines without a webcam.
        public bool WebcamCaptureEnabled { get; set; } = true;

        // Default device the capture dialog opens on. Index follows OpenCV's
        // ordering (probed 0..4). Null = pick the first device returned.
        public int? PreferredCameraIndex { get; set; }

        // Cross-session fallback for when the OS reorders devices (e.g. a USB cam
        // gets unplugged and the index shifts). When the saved index points at a
        // device whose label doesn't match this name, the dialog falls back to
        // matching on name.
        public string? PreferredCameraName { get; set; }
    }
}
