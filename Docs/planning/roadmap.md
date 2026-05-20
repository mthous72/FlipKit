# FlipKit Future Roadmap

## Document Purpose

This document outlines planned future enhancements for FlipKit. As of May 2026, FlipKit Hub v3.7.0 is shipping — Desktop app with embedded Web and API servers, full end-to-end inventory + scanning + export workflow. The Phase 1–6 refactor (see [refactor-plan.md](refactor-plan.md) and [refactor-status.md](refactor-status.md)) is complete: codebase is cleaned, 919 tests in place, several latent production bugs fixed. This roadmap re-baselines what comes next against that cleaned reality.

---

## Current Status Summary

**✅ Shipped (as of v3.7.0):**
- AI-powered card scanning with live OpenRouter model catalog (now with `IsFallback`-flagged static fallback when the live fetch fails) and paid-model consent
- **CardSight first-pass recognition** — optional purpose-built sports-card recognition tried before OpenRouter (750 free identifications/month), with a CardSight subscription/quota panel in Desktop + Web Settings; falls through to OpenRouter on miss / low confidence / quota exhaustion. **Ximilar was fully removed in v3.7.0** — the scan pipeline is now CardSight → OpenRouter
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
- **919 unit + integration tests** (634 Core, 224 Desktop, 61 Web) wired into the build pipeline as a CI gate
- `NetworkAddressProvider` (Phase 5c.1) — IP/QR/URL logic split out of `SettingsViewModel` so it's testable without real adapters
- **Documentation restructure** — `Docs/` reorganized into topic folders (`architecture/`, `features/`, `guides/`, `development/`, `planning/`, `archive/`), all active docs refreshed to v3.7.0, CardSight documented, Ximilar scrubbed from active docs (see Roadmap #0 below)

---

## High Priority (Next 3-6 Months)

### 0. Documentation Cleanup — Full Restructure

**Status:** ✅ Shipped 2026-05-20 (`fix/docs-cleanup`, PR #30 — 4 content commits on top of the re-baselined plan)
**Effort:** Medium (delivered across 4 commits)
**Plan Doc:** [documentation-cleanup-plan.md](../archive/documentation-cleanup-plan.md) (now archived — completed plan, kept as a historical record with its rescan/delta)

Restructured `Docs/` into topic folders (`architecture/`, `features/`, `guides/`, `development/`, `planning/`, `archive/`), refreshed stale content, added missing documentation, and consolidated overlap.

**What shipped:** topic-folder restructure (move-only commit 1), stale-content refresh to v3.7.0 (commit 2), new content — CardSight feature docs + `AiModelUsed` schema, Linux install guide, top-level `Docs/README.md` index (commit 3), and cross-cutting updates to root `README.md` / `CLAUDE.md` / `.github/copilot-instructions.md` + link integrity (commit 4). Ximilar references scrubbed from active docs (annotated as "removed in v3.7.0" in living planning docs). Original scope below for reference.

**Scope:**
- **Move/restructure** ~30 files from flat `Docs/00-…/31-…` numbering into topic folders.
- **Heavy rewrite:** `HUB-ARCHITECTURE.md` + `10-GUI-ARCHITECTURE.md` merged into `architecture/overview.md`; `USER-GUIDE.md` (1250 lines) refreshed to v3.7.0 with screenshot placeholders resolved; `02-DATABASE-SCHEMA.md` extended with SurpriseSet/RevenueAllocationMethod/CardStatus/VerificationStatus/AiModelUsed; `14-VARIATION-VERIFICATION.md` extended with verified-fields LLM hint mode (commit `223cf95`); `03-OPENROUTER-INTEGRATION.md` extended with CardSight; `07-CLAUDE-CODE-GUIDE.md` rewritten to drop the `MockScannerService` dead reference and dedupe overlap with root `CLAUDE.md`.
- **Archive:** `01-PROJECT-PLAN.md`, `11-UX-DESIGN.md`, `26-CSV-EXPORT-IMPLEMENTATION-PLAN.md`, `References/card_listings_export_spec.md`.
- **Delete:** `00-PROGRAM-OVERVIEW.md` (658 lines of pre-rebrand content under the old product name; current state covered by `README.md` + `guides/user-guide.md`).
- **Cross-cutting:** root `README.md` (v3.6.0 → v3.7.0 download bump, drop dead Docker mention), root `CLAUDE.md` (fix build example + v3.3.6 current-state, trim § Architecture overlap), `.github/copilot-instructions.md` (replace Azure boilerplate or delete), and a new `Docs/README.md` topic index.

**Why now:** Brand drift (the Feb 2026 rebrand to FlipKit), version drift (older `v3.x` strings scattered across active docs vs current v3.7.0), missing schema docs, and dead references have accumulated to the point where new contributors and Claude Code in future sessions waste time figuring out which docs are current. Cleanup is cheaper now than after another round of feature work compounds the drift.

**Pre-execution gate:** plan starts with a mandatory rescan (refresh git state, diff each in-flight branch against the cleanup branch, re-run inventory, produce a delta) so the file-by-file action list is reconfirmed against current `master` before commit 1. Move-commit timing must be coordinated with any open doc-touching branches to avoid massive rename diffs across merges.

### 1. User-Driven Checklist Excel Import (Checklist Insider)

**Status:** 🟡 Partially shipped — Phase 1 + Phase 2 vertical slice done. Remaining Phase 2 items + Phases 3-4 indefinitely deferred (2026-05-04). Re-evaluate on real user friction.
**Effort:** Shipped portion ≈ 1.5 weeks of work. Deferred remainder estimated at 2-3 weeks if revived.
**Plan Doc:** [checklist-insider-import-plan.md](checklist-insider-import-plan.md) (decision log entry 2026-05-04)

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
**Plan Doc:** [27-WEBCAM-CAPTURE-PLAN.md](../archive/27-WEBCAM-CAPTURE-PLAN.md) — see §12 "Outcome" for what landed, smoke-test findings, and follow-ups.

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

### ~~3. Automated Pricing~~ — ❌ NOT BUILDING

**Decision (2026-05-05):** All automated pricing via the eBay API was built and then deliberately removed. The Browse API only returns active asking prices (not sold prices), which adds ambiguity rather than value — users could price too low chasing what someone is *asking* rather than what cards actually sell for. The manual workflow (Terapeak Seller Hub + eBay Sold deeplinks already wired into `PricingViewModel`) is more accurate and requires no developer credentials.

**What was removed:** `ISoldPriceService`, `IEbayBrowseApiClient`, `EbayBrowseApiClient`, `EbayBrowseApiActiveListingService`, `ListingRecord` model, the DB table plumbing, and the Settings UI credential section. The Terapeak and eBay Sold deeplink buttons remain untouched.

**Manual pricing workflow (stays as-is):**
- Terapeak Seller Hub — free for any eBay seller, shows actual sold prices including Best Offer accepted
- eBay Sold deeplink — `LH_Sold=1&LH_Complete=1` query, opens in browser, manually researched
- Both are generated from card data by `PricingViewModel.OpenTerapeakCommand` / `OpenEbaySoldCommand`

**Future opt-in (if demand arises):** SportsCardsPro or CardLadder APIs provide real sold-price data for cards and are not subject to eBay's license constraints. Either could be wired as a toggled service if a user with paid API access requests it. Do not begin this work speculatively.

### ~~4. Unit and Integration Tests~~ — ✅ DELIVERED in Phase 4

**Status:** ✅ Done (Phase 4a–4e of the refactor)

Originally a roadmap item assuming zero tests. Delivered as Phase 4 of the refactor: the suite started at **490 tests** (267 Core, 175 Desktop, 48 Web) at refactor close-out and has since grown to **919 tests** (634 Core, 224 Desktop, 61 Web) as later features landed. Real-SQLite-in-memory + NSubstitute HTTP-mock patterns, CI gate wired into `build-installers.ps1` and `build-release.ps1`. Coverage targets met: helpers ≥95%, stateless services ≥84%, ViewModels ≥80% (with documented carryovers in [refactor-status.md](refactor-status.md)).

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

- ADRs for non-obvious choices live in [ADR/](../architecture/adr/). Five of them landed in Phase 6: Hub-vs-separate-apps, net8/net9 mix, EnsureCreated+SchemaUpdater vs migrations, user-driven Checklist Insider, Avalonia choice.
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
3. **Risk:** Could it break existing flows? Use the [REGRESSION-CHECKLIST.md](../development/regression-checklist.md) gate before merge.
4. **Dependencies:** Does it block higher-priority work?
5. **Maintenance:** Ongoing support burden?

---

**Last Updated:** 2026-05-20 (v3.7.0 + documentation cleanup shipped)
**Next Review:** August 2026

**Recent changes:**
- 2026-05-20 — **v3.7.0 + documentation cleanup shipped.** Roadmap #0 (Documentation Cleanup — Full Restructure) marked ✅ Shipped (`fix/docs-cleanup`, PR #30). v3.7.0 also shipped **CardSight first-pass recognition + subscription/quota panel** and the **full removal of Ximilar** (scan pipeline is now CardSight → OpenRouter); both folded into the Shipped summary above. Test count updated 490 → 919; framing version v3.3.6 → v3.7.0. The completed cleanup plan was archived to [archive/documentation-cleanup-plan.md](../archive/documentation-cleanup-plan.md).
- 2026-05-08 — **Roadmap #0 added: Documentation Cleanup — Full Restructure** queued as next up. Plan checked in as `32-DOCUMENTATION-CLEANUP-PLAN.md` on `fix/docs-cleanup`. Mandatory pre-execution rescan must run before any restructure commits to re-baseline the file-by-file action list against current `master` and any in-flight branches.
- 2026-05-04 — **Roadmap 1 partial ship.** Phase 1 (Surface A) + Phase 2 foundation + first Phase 2 UI slice landed (commits `1053f11`, `4b9009f`, `d036bfd`). Remaining Phase 2 polish items (typeahead, parallel dropdown, picker, BulkScan tier collapsing, Web parity, round-trip JS) and Phases 3-4 deferred indefinitely; re-evaluate on real friction or when an adjacent feature needs them. Schema fields kept regardless of UI state.
- 2026-05-04 — **Phase 6 re-baseline.** Roadmap #4 (Tests) marked Done, delivered by refactor Phase 4. Roadmap #1 effort cut from 4-5 wk → 3-4 wk after Phase 4.5 D3 fix unblocked it. Roadmap #3 (Price Scraping) gained an explicit "Decision required" gate covering a shelved sold-price service (since removed in 2026-05-05). Roadmap #5 (COMC) re-read found more wiring than previously implied — flagged for downgrade or drop pending demand signal. Tech-debt section rewritten against actual post-Phase-5 ViewModel sizes. Pointer added to new `Docs/ADR/` directory.
- 2026-05-02 — Promoted Webcam Capture from Medium #4 to High #2; pushed Price Scraping → #3, Tests → #4.
- 2026-05-02 — Audit pass: removed completed items (Bulk Scan, Architecture Refactor, eBay Bulk CSV) and dropped items no longer in scope (Cloud Sync/Backup, MySlabs, TCGPlayer, Barcode/QR Scanning, Multi-User/Team). Renumbered.
- 2026-05-01 — Added "User-Driven Checklist Excel Import" — see [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](checklist-insider-import-plan.md)
