# FlipKit Future Roadmap

## Document Purpose

This document outlines planned future enhancements for FlipKit. As of May 2026, FlipKit Hub v3.3.6 is shipping — Desktop app with embedded Web and API servers, full end-to-end inventory + scanning + export workflow. The Phase 1–6 refactor (see [29-REFACTORING-PLAN.md](29-REFACTORING-PLAN.md) and [30-REFACTOR-STATUS.md](30-REFACTOR-STATUS.md)) is complete: codebase is cleaned, 490 tests in place, several latent production bugs fixed. This roadmap re-baselines what comes next against that cleaned reality.

---

## Current Status Summary

**✅ Shipped (as of v3.3.6):**
- AI-powered card scanning with live OpenRouter model catalog (now with `IsFallback`-flagged static fallback when the live fetch fails) and paid-model consent
- Bulk scanning workflow with front/back pairing, semaphore-throttled concurrent scans, free-tier rate-limit handling, and per-session error logs
- Variation verification with bundled checklists + confirmation pass
- Inventory management with filtering, search, and editing
- Pricing research via browser deeplinks (Terapeak/eBay)
- Whatnot CSV export, eBay Bulk CSV export, COMC title template path — all spec-compliant with template-based validation
- Sales tracking and financial reporting
- Graded card support (PSA, BGS, CGC, etc.)
- Setup wizard, settings, ImgBB image hosting
- 4-project architecture (Core / Desktop / Web / Api) with shared SQLite + WAL
- Tailscale-friendly remote access via Api server
- Inno Setup Windows installer + Hub zip portables
- **490 unit + integration tests** (267 Core, 175 Desktop, 48 Web) wired into the build pipeline as a CI gate
- `NetworkAddressProvider` (Phase 5c.1) — IP/QR/URL logic split out of `SettingsViewModel` so it's testable without real adapters

---

## High Priority (Next 3-6 Months)

### 1. User-Driven Checklist Excel Import (Checklist Insider)

**Status:** 📋 Planned — **now actually buildable** (D3 fix landed in Phase 4.5)
**Effort:** High (3-4 weeks for full surface set + mobile parity + lookup wizard) — *was 4-5 weeks; cut after Phase 4.5*
**Plan Doc:** [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)

Let users populate `SetChecklist` by downloading per-set Excel files from [checklistinsider.com](https://www.checklistinsider.com/) themselves and importing the .xlsx into FlipKit via a file picker. Closes the gap where most modern releases aren't pre-seeded.

**Why user-driven (not automated):** Checklist Insider's ToU forbids commercial scraping/mirroring but grants individual users a personal-use download license. FlipKit ships only a parser (ClosedXML) and UI — never touches their site. Same legal posture as any app that opens a user-supplied file. TCDB and Beckett are off the table for the same reason. ADR-004 captures this in detail.

**Why "now actually buildable":** prior to Phase 4.5, `SetChecklist.Cards` and `KnownVariations` were JSON-converted properties without a `ValueComparer`. `ChecklistLearningService`'s enrichment path silently no-op'd in production — every "learn from this scan" call was lost. The D3 fix added `ValueComparer<List<T>>` to both columns, so checklist mutations now persist. Without that fix, this entire roadmap item would have shipped broken on day one.

**What it adds:**
- "Import Checklist" view (Desktop + Web) with file picker, parse-preview, edit metadata, commit
- ClosedXML-based `ExcelChecklistImporter` in FlipKit.Core
- New fields on `ChecklistCard`: `IsAutograph`, `IsParallel`, `IsInsert`
- New fields on `SetChecklist`: `DataSource`, `ImportedAt`
- "Get Checklist for this set" deeplink in scan results when no checklist is imported yet

**Phase 2 follow-ups:** PDF odds-sheet importer (PdfPig) for parallels/print-runs/signers, batch folder import, manufacturer dealer-kit PDF support.

### 2. Webcam Capture for Scanning

**Status:** 📋 Planned
**Effort:** Medium (2-3 weeks)
**Plan Doc:** [27-WEBCAM-CAPTURE-PLAN.md](27-WEBCAM-CAPTURE-PLAN.md) — re-validate against current Avalonia version before starting

Allow scanning directly from a connected webcam instead of requiring file uploads, enabling a true "stream of cards" workflow on Desktop. Avalonia 11.3 doesn't ship a webcam control; expect to either bind a platform-specific MediaFoundation/AVFoundation/V4L2 wrapper or use a third-party library (e.g. `LibVLCSharp`). Reconfirm the chosen approach in the plan doc when work starts.

### 3. Automated Price Scraping

**Status:** 📋 Planned — see also "Decision required" below
**Effort:** High (4-6 weeks)

Today PricerService only builds Terapeak/eBay search URLs and opens them in a browser. Target: pull median sold prices automatically. `Point130SoldPriceService` exists, is registered in DI, but is **shelved at the only call site** (`PricingViewModel.cs:19` — `// SHELVED: ISoldPriceService _soldPriceService (kept for potential future use)`).

**Decision required before this item starts:**
- **Revive Point130** — finish wiring `PricingViewModel` to call it, decide on caching/throttling, accept the legal-gray scraping posture.
- **Replace with eBay Finding API** — delete `Point130SoldPriceService`, build a new `EbayFindingApiService` (Option A below). Cleaner long-term but requires eBay developer approval + key management.
- **Delete the shelf** — drop `ISoldPriceService` and `Point130SoldPriceService` entirely, keep manual pricing as the only path. Simplest, but blocks #9 (Price Alerts).

**Approach Options (when this is greenlit):**

**Option A: eBay Finding API (Recommended)**
- Official eBay developer API — sold listings via `findCompletedItems`
- Free developer account, ~5,000 calls/day
- Pros: official, reliable, no scraping risk
- Cons: requires approval + key management

**Option B: Web scraping (HtmlAgilityPack or revived Point130)**
- Pros: no API key
- Cons: fragile (eBay/130point HTML changes), legal gray area

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

### ~~4. Unit and Integration Tests~~ — ✅ DELIVERED in Phase 4

**Status:** ✅ Done (Phase 4a–4e of the refactor)

Originally a roadmap item assuming zero tests. Delivered as Phase 4 of the refactor: **490 tests** (267 Core, 175 Desktop, 48 Web), real-SQLite-in-memory + NSubstitute HTTP-mock patterns, CI gate wired into `build-installers.ps1` and `build-release.ps1`. Coverage targets met: helpers ≥95%, stateless services ≥84%, ViewModels ≥80% (with documented carryovers in [30-REFACTOR-STATUS.md](30-REFACTOR-STATUS.md)).

Two latent production bugs surfaced and fixed during test writing — see audit D2 (OpenRouter retry filter) and D3 (SetChecklist ValueComparer).

---

## Medium Priority (6-12 Months)

### 5. Finish COMC Exporter

**Status:** 🟡 Partial — *more partial than the previous roadmap implied*
**Effort:** Small (1 week) — *unchanged*

`ExportPlatform` enum has a `COMC` entry, `AppSettings` has a `ComcTitleTemplate`, `TitleTemplateService` resolves it, and `CsvExportService` falls through to the Whatnot writer for COMC export today (see `CsvExportService.cs:69, 158`). What's still missing: a dedicated `ICOMCExporter` (or `COMCWriter`) that emits COMC's actual column set rather than reusing Whatnot's, plus a COMC-specific `ExportValidator` and a consignment category mapping.

If no concrete signal of demand exists from end users, **consider downgrading or dropping** — the dead `ExportPlatform.COMC` enum value + half-wired settings are themselves a Phase 6+ cleanup target.

### 6. Inventory Performance — Virtualization & Image Cache

**Status:** 🟡 Partial
**Effort:** Medium (3-4 weeks)

DB indexes are in place. Remaining gaps:
- DataGrid virtualization in InventoryView (slows down past ~500 cards — re-measure after Phase 6 if user reports drift)
- Lazy / cached thumbnails (images currently loaded eagerly)
- Frequently-accessed checklist cache (reduce DB round-trips on every scan)

### 7. Dark Theme Toggle

**Status:** 🟡 Partial
**Effort:** Low (1-2 weeks)

`App.axaml` already follows the system theme, but there's no in-app toggle and no audited dark variant. Add Settings → Theme (System / Light / Dark), persist preference, ensure WCAG AA contrast across all views. The Phase 5c.1 NetworkAddressProvider extraction did not introduce any new theme-coupling, so this estimate is unchanged.

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

### Code Quality (post-Phase 5 reality check)

ViewModel sizes after Phase 5c.1 (was 803 → 662 for Settings):

| ViewModel | Lines | Phase 5 fate |
|---|---|---|
| `SettingsViewModel` | 662 | NetworkAddressProvider extracted in 5c.1; connection-tester split deferred (low ROI vs XAML/test churn) |
| `BulkScanViewModel` | 506 | 5d skipped — named extractions (`BulkScanQueueService`, `RateLimitTracker`) didn't survive code re-read |
| `ScanViewModel` | 483 | Untouched; ≥71% test coverage; revisit only if Roadmap #1 work makes it bigger |
| `InventoryViewModel` | 471 | Untouched; ≥80% test coverage; current shape is fine |
| `ExportViewModel` | 256 | Untouched; under threshold |

Standing cleanup items:
- Magic strings for OpenRouter model IDs are now consolidated in `OpenRouterModelDefaults` (Phase 5b). What remains: any leftover hardcoded model IDs in tests or ViewModels — sweep when convenient.
- Hardcoded HttpClient timeouts: `ServerManagementService.cs:42` is now wired through a typed setting (Phase 5a). New code should use the same pattern.
- The two `#pragma warning disable` blocks in `BulkScanViewModel.ProcessItemAsync` (`CS8602` around `_scanCts`, `MVVMTK0034` around `Interlocked.Increment(ref _scanProgress)`) could be cleaned up in a 30-minute targeted edit if they ever bother future-us.

### Documentation

- ADRs for non-obvious choices live in [ADR/](ADR/). Five of them landed in Phase 6: Hub-vs-separate-apps, net8/net9 mix, EnsureCreated+SchemaUpdater vs migrations, user-driven Checklist Insider, Avalonia choice.
- `Docs/07-CLAUDE-CODE-GUIDE.md` was rewritten in Phase 6 to reflect the 4-project architecture (was a single-project guide).
- Inline XML comments on public Core APIs — still pending, low priority.
- End-user help (Desktop F1, screenshots) — `M:\Software Development\Releases\Help\` per Motz SOP.

### Dependency Hygiene

Current floor: Avalonia 11.3.11, EF Core 8.0.11, .NET 8/9 mix.
- Plan Avalonia 12 migration when it stabilizes
- Plan unified .NET 9 (or 10) once Avalonia supports it cleanly — would eliminate the Core/Api framework split (see ADR-002)

---

## Decision Framework

When deciding what to build next:

1. **User Impact:** Does it solve a real pain point in the daily reseller workflow?
2. **Effort vs ROI:** How long, and what does it unlock?
3. **Risk:** Could it break existing flows? Use the [REGRESSION-CHECKLIST.md](REGRESSION-CHECKLIST.md) gate before merge.
4. **Dependencies:** Does it block higher-priority work?
5. **Maintenance:** Ongoing support burden?

---

**Last Updated:** 2026-05-04 (Phase 6 re-baseline against cleaned codebase)
**Next Review:** August 2026

**Recent changes:**
- 2026-05-04 — **Phase 6 re-baseline.** Roadmap #4 (Tests) marked Done, delivered by refactor Phase 4. Roadmap #1 effort cut from 4-5 wk → 3-4 wk after Phase 4.5 D3 fix unblocked it. Roadmap #3 (Price Scraping) gained an explicit "Decision required" gate covering the shelved `Point130SoldPriceService`. Roadmap #5 (COMC) re-read found more wiring than previously implied — flagged for downgrade or drop pending demand signal. Tech-debt section rewritten against actual post-Phase-5 ViewModel sizes. Pointer added to new `Docs/ADR/` directory.
- 2026-05-02 — Promoted Webcam Capture from Medium #4 to High #2; pushed Price Scraping → #3, Tests → #4.
- 2026-05-02 — Audit pass: removed completed items (Bulk Scan, Architecture Refactor, eBay Bulk CSV) and dropped items no longer in scope (Cloud Sync/Backup, MySlabs, TCGPlayer, Barcode/QR Scanning, Multi-User/Team). Renumbered.
- 2026-05-01 — Added "User-Driven Checklist Excel Import" — see [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)
