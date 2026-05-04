# FlipKit Future Roadmap

## Document Purpose

This document outlines planned future enhancements for FlipKit. As of May 2026, FlipKit Hub v3.3.6 is shipping — Desktop app with embedded Web and API servers, full end-to-end inventory + scanning + export workflow. This roadmap guides what comes next.

---

## Current Status Summary

**✅ Shipped (as of v3.3.6):**
- AI-powered card scanning with live OpenRouter model catalog and paid-model consent
- Bulk scanning workflow with front/back pairing, progress tracking, and rate-limit handling
- Variation verification with bundled checklists
- Inventory management with filtering, search, and editing
- Pricing research via browser deeplinks (Terapeak/eBay)
- Whatnot CSV export and eBay Bulk CSV export — both spec-compliant with template-based validation
- Sales tracking and financial reporting
- Graded card support (PSA, BGS, CGC, etc.)
- Setup wizard, settings, ImgBB image hosting
- 4-project architecture (Core / Desktop / Web / Api) with shared SQLite + WAL
- Tailscale-friendly remote access via Api server
- Inno Setup Windows installer + Hub zip portables

---

## High Priority (Next 3-6 Months)

### 1. User-Driven Checklist Excel Import (Checklist Insider)

**Status:** 📋 Planned
**Effort:** Medium (2-3 weeks)
**Plan Doc:** [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)

Let users populate `SetChecklist` by downloading per-set Excel files from [checklistinsider.com](https://www.checklistinsider.com/) themselves and importing the .xlsx into FlipKit via a file picker. Closes the gap where most modern releases aren't pre-seeded.

**Why user-driven (not automated):** Checklist Insider's ToU forbids commercial scraping/mirroring but grants individual users a personal-use download license. FlipKit ships only a parser (ClosedXML) and UI — never touches their site. Same legal posture as any app that opens a user-supplied file. TCDB and Beckett are off the table for the same reason.

**What it adds:**
- "Import Checklist" view (Desktop + Web) with file picker, parse-preview, edit metadata, commit
- ClosedXML-based `ExcelChecklistImporter` in FlipKit.Core
- New fields on `ChecklistCard`: `IsAutograph`, `IsParallel`, `IsInsert`
- New fields on `SetChecklist`: `DataSource`, `ImportedAt`
- "Get Checklist for this set" deeplink in scan results when no checklist is imported yet

**Phase 2 follow-ups:** PDF odds-sheet importer (PdfPig) for parallels/print-runs/signers, batch folder import, manufacturer dealer-kit PDF support.

### 2. Webcam Capture for Scanning

**Status:** 📋 Planned
**Plan Doc:** [27-WEBCAM-CAPTURE-PLAN.md](27-WEBCAM-CAPTURE-PLAN.md)

Allow scanning directly from a connected webcam instead of requiring file uploads, enabling a true "stream of cards" workflow on Desktop.

### 3. Automated Price Scraping

**Status:** 📋 Planned
**Effort:** High (4-6 weeks)

Today PricerService only builds Terapeak/eBay search URLs and opens them in a browser. Target: pull median sold prices automatically.

**Approach Options:**

**Option A: eBay Finding API (Recommended)**
- Official eBay developer API — sold listings via `findCompletedItems`
- Free developer account, ~5,000 calls/day
- Pros: official, reliable, no scraping risk
- Cons: requires approval + key management

**Option B: Web scraping (HtmlAgilityPack)**
- Pros: no API key
- Cons: fragile (eBay HTML changes), legal gray area

**Option C: Terapeak Research API**
- Best data quality but requires eBay Store subscription ($30/month)

**Recommended Implementation:**
```csharp
public interface IPriceScraperService
{
    Task<PriceDataResult> GetMarketPriceAsync(Card card);
}

public class PriceDataResult
{
    public decimal MedianSoldPrice { get; set; }
    public decimal AverageSoldPrice { get; set; }
    public int SoldCount { get; set; }
    public DateTime DataAsOf { get; set; }
    public List<RecentSale> RecentSales { get; set; }
}
```

**UI Changes:** "Get Market Price" button on PricingView, auto-populate `EstimatedValue`, show confidence interval ("$12-18 based on 15 sales"), 24-hour cache.

**Configuration:** Settings → eBay API key, toggle auto-price vs manual.

### 4. Unit and Integration Tests

**Status:** 📋 Planned
**Effort:** Medium (3-4 weeks)

Currently zero tests in the repo. Without them, every refactor is high-risk and regressions ship invisibly.

**Test Projects:**
```
FlipKit.Core.Tests/
├── ViewModels/         # ScanViewModel, BulkScanViewModel, ExportViewModel, etc.
├── Helpers/            # FuzzyMatcher, WhatnotCategoryDefaulter, CardStatusEvaluator
├── Services/           # CsvExportService, EbayExporter, WhatnotExporter, ExportValidator
└── Data/               # In-memory SQLite repository tests
```

**Strategy:**
- Unit tests: ViewModels with mocked services (xUnit + Moq)
- Integration tests: Database operations with in-memory SQLite
- API/scanner tests: recorded responses (VCR pattern) — avoids hitting OpenRouter
- UI smoke tests: Avalonia.Headless for critical flows

**Coverage goals:** ViewModels 80%+, Services 70%+, Helpers 90%+.

---

## Medium Priority (6-12 Months)

### 5. Finish COMC Exporter

**Status:** 🟡 Partial
**Effort:** Small (1 week)

`ExportPlatform` enum has a `COMC` entry and `CsvExportService` has a title template, but no dedicated `COMCExporter` class exists. Build it out alongside an export validator and consignment-specific category mapping.

### 6. Inventory Performance — Virtualization & Image Cache

**Status:** 🟡 Partial
**Effort:** Medium (3-4 weeks)

DB indexes are in place. Remaining gaps:
- DataGrid virtualization in InventoryView (slows down past ~500 cards)
- Lazy / cached thumbnails (images currently loaded eagerly)
- Frequently-accessed checklist cache (reduce DB round-trips on every scan)

### 7. Dark Theme Toggle

**Status:** 🟡 Partial
**Effort:** Low (1-2 weeks)

`App.axaml` already follows the system theme, but there's no in-app toggle and no audited dark variant. Add Settings → Theme (System / Light / Dark), persist preference, ensure WCAG AA contrast across all views.

---

## Low Priority (Future Considerations)

### 8. Mobile Companion App (PWA)

**Status:** 💭 Considering
**Effort:** Medium (4-6 weeks for PWA)

The Web app is already mobile-responsive and accessible via Tailscale, which covers most of this. Only worth pursuing if/when offline-mobile-camera scanning is a real need. **Recommended path: PWA, not native** — reuses existing FlipKit.Web, no app store, works on any device. Skip MAUI/React Native unless there's a specific reason.

### 9. Price Alerts and Notifications

**Status:** 💭 Considering
**Effort:** Medium (2-3 weeks)
**Depends on:** Item 3 (Automated Price Scraping)

Once we have automated pricing, alerting on significant value changes or stale prices becomes useful. Not worth building until #3 ships.

---

## Technical Debt and Maintenance

### Code Quality

- A few ViewModels are pushing 500+ lines (ScanViewModel, BulkScanViewModel, ExportViewModel) — consider splitting into smaller pieces or extracting helpers as they grow
- Magic strings for OpenRouter model IDs and API endpoints — move to typed configuration
- Hardcoded timeouts in HttpClient calls — make configurable

### Documentation

- Inline XML comments on public Core APIs
- Architecture decision records (ADRs) for non-obvious choices (e.g., why Hub vs separate apps, why net8 + net9 mix)
- End-user help (Desktop F1, screenshots) — `M:\Software Development\Releases\Help\` per Motz SOP

### Dependency Hygiene

Current floor: Avalonia 11.3.11, EF Core 8.0.11, .NET 8/9 mix.
- Plan Avalonia 12 migration when it stabilizes
- Plan unified .NET 9 (or 10) once Avalonia supports it cleanly — would eliminate the Core/Api framework split

---

## Decision Framework

When deciding what to build next:

1. **User Impact:** Does it solve a real pain point in the daily reseller workflow?
2. **Effort vs ROI:** How long, and what does it unlock?
3. **Risk:** Could it break existing flows?
4. **Dependencies:** Does it block higher-priority work?
5. **Maintenance:** Ongoing support burden?

---

**Last Updated:** 2026-05-02
**Next Review:** August 2026

**Recent changes:**
- 2026-05-02 — Promoted Webcam Capture from Medium #4 to High #2; pushed Price Scraping → #3, Tests → #4.
- 2026-05-02 — Audit pass: removed completed items (Bulk Scan, Architecture Refactor, eBay Bulk CSV) and dropped items no longer in scope (Cloud Sync/Backup, MySlabs, TCGPlayer, Barcode/QR Scanning, Multi-User/Team). Renumbered.
- 2026-05-01 — Added "User-Driven Checklist Excel Import" — see [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)
