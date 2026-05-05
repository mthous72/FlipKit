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

**Status:** 🟡 Partially shipped — Phase 1 + Phase 2 vertical slice done. Remaining Phase 2 items + Phases 3-4 indefinitely deferred (2026-05-04). Re-evaluate on real user friction.
**Effort:** Shipped portion ≈ 1.5 weeks of work. Deferred remainder estimated at 2-3 weeks if revived.
**Plan Doc:** [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md) (decision log entry 2026-05-04)

**What's live (master):**
- Surface A — Settings → Checklists → Import from Excel on both Desktop and Web; ClosedXML parser handles both Mosaic-style (column-A-subset) and Bowman-style (inline-header) layouts.
- Foundation: tier-aware `ChecklistVerificationMatcher`, bundled `ParallelFamilyCatalog.json` covering common modern releases, `Card.VerificationStatus` + `Card.MatchedChecklistKey` schema fields, `AppSettings.AutoAcceptTier1Matches`.
- First Phase 2 UI slice on Desktop: post-scan tier badge on `ScanView`, Surface B "no checklist imported" banner, tier badge on `EditCardView` for saved cards, Settings toggle for auto-accept Tier 1.

**Indefinitely deferred** (kept as cold backlog — schema is in place if any get revived):
- Card # typeahead, Parallel dropdown, Serial unlock, autograph auto-check inside `EditCardView`/`Edit.cshtml`
- `PickFromChecklistDialog` (Tier 3 picker)
- Web parity for the post-scan banner + tier-aware editor enhancements
- BulkScan tier-driven row collapsing + aggregate banner
- `checklist-roundtrip.js` localStorage stash for mobile import round-trips
- Phase 3 (Surface C: pre-scan set-lookup wizard + prompt augmentation)
- Phase 4 (Surface D: missing-checklists audit + re-verify pass)

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

**Status:** ✅ Shipped 2026-05-04 (`feature/webcam-capture`, 5 commits)
**Plan Doc:** [27-WEBCAM-CAPTURE-PLAN.md](27-WEBCAM-CAPTURE-PLAN.md) — see §12 "Outcome" for what landed, smoke-test findings, and follow-ups.

📷 Webcam buttons on Scan + Edit (Desktop, OpenCvSharp4) and Scan (Web, `getUserMedia`+canvas). Settings → Webcam Capture exposes a master toggle, device picker with max-resolution labels, and a Test capture button. Browser capture requires HTTPS or `localhost`; on HTTP-via-Tailscale the trigger buttons hide and a banner explains why.

**Deferred follow-ups:** ~~Inventory edit-card webcam wiring on Web~~ — shipped 2026-05-05 in `f3804db` (slots 3-8 only; front/back deferred to avoid the URL-clearing flow). Mac/Linux smoke pass and OpenCvSharp4 osx-arm64 verification — **blocked**, the maintainer has no Mac on hand to test against. Open issue for an external contributor or accept "Windows-only verified" until a Mac is available.

### 2.5 eBay Seller Hub Listings Import

**Status:** ✅ Shipped 2026-05-05 (4 PRs on `master`)
**Plan Doc:** None — designed inline; see PR commit messages on `master` (`5b46141` PR 1, `3d6411d` PR 2, `a2fe088` PR 3, `111198e` PR 4) for the per-PR scope.

Import an eBay Seller Hub "All active listings" CSV export into the inventory. EbayItemId is the upsert key, so re-importing updates existing rows instead of duplicating them.

**Pipeline:** `EbayListingsCsvReader` (CsvHelper, tolerant of BOM + Apr-29-26 dates + CD:* grading columns) → `EbayTitleParser` rule pass (regex for year/manufacturer/card #/serial/auto/relic/rookie/SP/SSP) → `OpenRouterEbayTitleEnricher` LLM pass (batches of 10 titles per request, JSON-array prompt for player/brand/set/parallel/team) → `EbayListingImportService` mapper with field-preservation rules (LLM nulls don't overwrite existing values, boolean flags only flip true) → `ICardRepository` upsert.

**UI:** Desktop "Import eBay…" toolbar button on Inventory → `ImportEbayListingsDialog` (file picker → preview DataGrid with Skip checkbox → Import). Web "Import eBay…" button on Inventory header → `/Inventory/ImportEbay` form (parses + commits in one shot — no review step on Web to avoid doubling LLM cost).

**Tests:** 25 in `FlipKit.Core.Tests` covering CSV reader, import service orchestration, and OpenRouter response parsing. Plus the original 37 from PR 1 over the parser regex against real eBay title fixtures.

**Deferred follow-ups:**
- ~~2-step preview-then-commit Web flow~~ — shipped 2026-05-05 in `689d64e`. IMemoryCache stashes the parsed preview by GUID token (30 min sliding TTL); review page lets users untick rows before commit. Avoids the second LLM call.
- ~~Bulk-edit on the Desktop preview grid~~ — shipped 2026-05-05 in `dc64fc5`. Player/Year/Brand/Parallel/#/Price are now editable in-place via two-way DataGrid binding.
- ~~Add `/api/cards/by-ebay-item-id/{id}` to the API server~~ — shipped 2026-05-05 in `31936b2`.
- ~~Map eBay title → `Sport` enum~~ — shipped 2026-05-05 in `31936b2` (regex over league acronyms + brand fallbacks; leaves null on genuinely ambiguous titles).

### 3. Automated Pricing — Active-Listing Comps via Browse API

> **Status (2026-05-05):** Stale recommendation rewritten. The previous version of this entry recommended building against eBay's Finding API `findCompletedItems` for sold-comp data. That API was **decommissioned 2026-02-05** (eBay [Traditional APIs deprecation thread](https://community.ebay.com/t5/Traditional-APIs-Search/Alert-Finding-API-and-Shopping-API-to-be-decommissioned-in-2025/td-p/34222062) + the [API Deprecation Status](https://developer.ebay.com/develop/get-started/api-deprecation-status) page). The replacement Marketplace Insights API is gated to "select developers approved by business units" (eBay [Marketplace Insights Overview](https://developer.ebay.com/api-docs/buy/marketplace-insights/static/overview.html)) — **not realistically attainable for FlipKit**. eBay's License Agreement also prohibits "deriving aggregated seller or buyer data" without express written permission, which constrains what we could ship even with Insights access. See [Docs/09-EBAY-API.md](09-EBAY-API.md) for the full posture analysis.

**Conclusion:** automated **sold-price** lookup via official eBay APIs is not viable for a regular developer account in 2026. Build automated **active-listing comps** via the Browse API instead, and keep sold-price research manual via the existing Terapeak/eBay deeplink workflow.

**Status:** 📋 In progress (PR A scaffold shipped 2026-05-05 in `a255afe` — service shell + `AppSettings.EbayFindingApiAppId`. Both need renaming to match the chosen Browse API path; see "Required correction" below).
**Effort:** Medium (~2 weeks for the Browse API path; the prior 4-6 week estimate assumed a sold-comp pipeline + statistical analysis).

**Chosen path: Browse API for active competitive comps.**

The [Browse API](https://developer.ebay.com/api-docs/buy/browse/overview.html) is the sanctioned surface for keyword/category search. 5,000 calls/day default for free tier ([API Call Limits](https://developer.ebay.com/develop/get-started/api-call-limits)) — comfortable headroom for a 200-card inventory. Returns currently active listings only (not sold prices), so we surface results as **"asking prices, not sold"** throughout the UI to avoid misleading users into pricing low based on what someone is *trying* to charge but not getting.

```csharp
// Endpoint: GET https://api.ebay.com/buy/browse/v1/item_summary/search
// Auth: OAuth2 client_credentials grant → Bearer token (cached 2 hr)
// Scope: https://api.ebay.com/oauth/api_scope
// Rate: 5000 calls/day (default), 100 items/call
```

**Configuration:** Settings → `EbayClientId` + `EbayClientSecret` (OAuth pair, replaces the single `EbayFindingApiAppId` shipped in PR A). Free developer account at https://developer.ebay.com/.

**UI:** "Get Competitive Pricing" button on PricingView (renamed from "Get Market Price" to be honest about what it returns). Shows median ask, low/high, listing count, and a prominent "ASKING prices — actual sales may differ" disclaimer. 24-hour cache so repeated views don't burn quota.

**Required correction (PR A.1, before PR B):** The PR A scaffold landed `EbayFindingApiSoldPriceService` and `AppSettings.EbayFindingApiAppId` based on this stale roadmap entry. Both need to rename to `EbayBrowseApiActiveListingService` / `EbayClientId`+`EbayClientSecret` before any HTTP code lands. See open work below.

**Sold-price research stays manual** — Terapeak Seller Hub (free for any eBay seller) or the LH_Sold=1&LH_Complete=1 deeplink workflow already wired into `PricingViewModel.OpenEbaySoldCommand`. Both surfaces accept browser-based research; FlipKit just generates the URL.

**Future opt-in: paid third-party sold-data adapter.** SportsCardsPro ($6-20/month) has sold-price data via API, sports-card-specific, no eBay-developer-agreement constraints. Worth wiring as a second `ISoldPriceService` impl behind a paid-toggle if users ask for true automated sold-comps. [Docs/09-EBAY-API.md](09-EBAY-API.md) §"Better Alternatives for Sold Prices" lists this and Ximilar's Collectibles API as the realistic options. Out of scope for the current Browse-API-only plan.

**Open work:**
- PR A.1 — rename service + AppSettings field + Settings UI to match the Browse API path. Pure mechanical rename, no functional change.
- PR B — `EbayBrowseApiClient` (OAuth token cache + GET /item_summary/search), response mapping to a new `ActiveListingRecord` (or repurpose `SoldPriceRecord` with a `IsSold` flag), median + outlier-trimmed analysis. Tests with mocked HTTP.
- PR C — `PricingView` "Get Competitive Pricing" button, ASKING-prices disclaimer, 24-hour cache, error states (no listings found / quota exceeded / OAuth failed).

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
- ~~Magic strings for OpenRouter model IDs~~ — swept 2026-05-05 in `f3804db`. `AppSettings.DefaultModel` and Web `SettingsViewModel.DefaultModel` now reference `OpenRouterModelDefaults.DefaultFreeModelId`; new `OpenRouterModelDefaults.AutoModelValue` const aliases the "auto" sentinel from both `ModelOption.AutoValue` and `WebModelOption.AutoValue`. Test fixtures with specific model IDs intentionally left alone (they exercise the catalog parser).
- Hardcoded HttpClient timeouts: `ServerManagementService.cs:42` is now wired through a typed setting (Phase 5a). New code should use the same pattern.
- ~~The two `#pragma warning disable` blocks in `BulkScanViewModel.ProcessItemAsync`~~ — cleaned up 2026-05-05 in `31936b2`. CS8602 by passing the CTS as a method parameter; MVVMTK0034 by introducing a separate `_completedCount` int field for `Interlocked.Increment` and publishing through the source-generator-managed `ScanProgress` setter.

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

**Last Updated:** 2026-05-04 (Roadmap 1 partial-ship + deferral)
**Next Review:** August 2026

**Recent changes:**
- 2026-05-04 — **Roadmap 1 partial ship.** Phase 1 (Surface A) + Phase 2 foundation + first Phase 2 UI slice landed (commits `1053f11`, `4b9009f`, `d036bfd`). Remaining Phase 2 polish items (typeahead, parallel dropdown, picker, BulkScan tier collapsing, Web parity, round-trip JS) and Phases 3-4 deferred indefinitely; re-evaluate on real friction or when an adjacent feature needs them. Schema fields kept regardless of UI state.
- 2026-05-04 — **Phase 6 re-baseline.** Roadmap #4 (Tests) marked Done, delivered by refactor Phase 4. Roadmap #1 effort cut from 4-5 wk → 3-4 wk after Phase 4.5 D3 fix unblocked it. Roadmap #3 (Price Scraping) gained an explicit "Decision required" gate covering the shelved `Point130SoldPriceService`. Roadmap #5 (COMC) re-read found more wiring than previously implied — flagged for downgrade or drop pending demand signal. Tech-debt section rewritten against actual post-Phase-5 ViewModel sizes. Pointer added to new `Docs/ADR/` directory.
- 2026-05-02 — Promoted Webcam Capture from Medium #4 to High #2; pushed Price Scraping → #3, Tests → #4.
- 2026-05-02 — Audit pass: removed completed items (Bulk Scan, Architecture Refactor, eBay Bulk CSV) and dropped items no longer in scope (Cloud Sync/Backup, MySlabs, TCGPlayer, Barcode/QR Scanning, Multi-User/Team). Renumbered.
- 2026-05-01 — Added "User-Driven Checklist Excel Import" — see [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)
