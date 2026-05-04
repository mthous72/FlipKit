# FlipKit Refactoring Plan — v1.0

**Target codebase:** FlipKit Hub v3.3.6 (`c:\Users\Matthew Houston\source\repos\FlipKit`)
**Goal:** Heavy cleanup with zero behavioral regressions, preserving roadmap-aligned code, ending in a roadmap revisit.
**Created:** 2026-05-04
**Status:** Approved — open questions resolved (see §10 Decisions Logged)

---

## 0. Prerequisite Gate (before Phase 1 starts)

This work is governed by Motz Engineering SOPs on this machine. Before any file is touched:

- Read `M:\Software Development\Docs\SOP\Development_Best_Practices.md` (covers all `.cs` work).
- Read `M:\Software Development\Docs\SOP\UI-Standards.md` (covers all XAML / Avalonia work).
- Re-read `CLAUDE.md` for repo conventions.

ViewModel decomposition pattern is **fixed** at "extract helper services to `FlipKit.Core/Services/`" per §10 Q8 — not subject to SOP override.

---

## 1. Test/Safety Strategy

The hard constraint is **zero tests in repo + heavy refactor desired**. Decision (per §10 Q7): **full test coverage first.** Roadmap item #4 (Tests) folds into this refactor as Phase 4 and gets removed from the future-work list.

Phase 5 only starts when coverage targets are met:
- ViewModels: 80%+
- Services: 70%+
- Helpers: 90%+

This adds 3-4 weeks to Phase 4 but makes Phase 5's invasive refactors (DI fixes, ViewModel splits) bulletproof. Full scope is in §6.

The manual regression checklist below remains in force as a layer above the automated tests — it's still run after each phase to catch end-to-end issues the unit tests can't.

Critical user-flow checklist (run after each phase):
1. Cold start Desktop → DB initializes → MainWindow loads.
2. Single scan: pick image → scan with free OpenRouter model → verify result populates → save card.
3. Bulk scan: 3 front/back pairs → results saved.
4. Inventory: load, filter by status/sport, edit a card, delete a card.
5. Pricing: open browser deeplinks for one card.
6. Whatnot CSV export: 5 cards → file generates → opens cleanly in Excel.
7. eBay Bulk CSV export: same 5 cards → matches template header.
8. Reports view: loads with at least one sold card.
9. Web app standalone (`dotnet run --project FlipKit.Web`) → mobile-style scan upload → save.
10. Settings → start/stop Web server, start/stop API server.

All ten flows must pass at the end of each phase before the next phase starts. If a phase is XAML-only (e.g., Phase 4 doc treatment), only flows touching changed surfaces need re-running.

---

## 2. Phasing Overview

| # | Phase | Risk | Effort | Goal |
|---|-------|------|--------|------|
| 1 | Inventory & Discovery | None | 1 day | Catalog every cleanup target with proof-of-deadness |
| 2 | Doc Archival & Root Tidy | Very Low | 0.5 day | Move historical docs, delete pure-rebrand artifacts |
| 3 | Trivial Code Cleanup | Low | 1 day | Dead converters, dead seeders, dead scripts, file rename |
| 4 | Full Test Coverage | Medium | 3–4 weeks | xUnit + Moq + Avalonia.Headless across Core / Desktop / Web; meets 80/70/90 targets |
| 5 | Targeted Code Refactors | High | 1–2 weeks | DI fixes, ViewModel splits, magic-string elimination |
| 6 | Roadmap Revamp | None (docs) | 0.5 day | Rewrite 17/26/27/28 against the cleaned tree |

Earlier phases are intentionally lowest-risk and produce permanent artifacts (manual checklist, helper tests) that gate the riskier work in Phase 5.

---

## 3. Phase 1 — Inventory & Discovery (no deletions yet)

**Goal:** Produce a single audit list — `Docs/AUDIT-2026-05.md` (working file) — with one row per cleanup candidate and one of three labels: `REMOVE` / `KEEP-ROADMAP` / `INVESTIGATE`. Nothing is deleted in this phase. The user explicitly asked for confirmation that "abandoned ≠ in-flight for a future feature," so this audit is the gate.

**Discovery process per candidate:**
1. Grep for all references (code + XAML + docs + ps1).
2. Map references back to either (a) a roadmap item in `Docs/17-FUTURE-ROADMAP.md`, (b) a still-running build script, or (c) nothing.
3. Label accordingly. Anything matching (a) is `KEEP-ROADMAP`, (c) is `REMOVE`, (b) is `KEEP`, ambiguous is `INVESTIGATE` and gets escalated to the user.

**Concrete candidates already verified during planning** (these go into the audit pre-labeled):

### REMOVE (high confidence, references confirmed)

| Item | Path | Why |
|---|---|---|
| ScreenshotTool subproject | `ScreenshotTool/` | csproj references `..\FlipKit\FlipKit.csproj` (pre-rebrand path that no longer exists) and uses old `FlipKit.Services.*` namespaces. Not in `FlipKit.sln`. Cannot build. Confirmed kill (§10 Q2). |
| `LegacyMigrator` | `FlipKit.Core/Helpers/LegacyMigrator.cs` | One-shot CardLister→FlipKit folder migration. Confirmed kill (§10 Q1). |
| Rebrand rename scripts | `rename-folders.ps1`, `rename-flipkit-content.ps1`, `rename-to-flipkit.ps1` | One-shot CardLister→FlipKit rebrand done Feb 2026. All target paths no longer exist. |
| GitHub rename instructions | `GITHUB-RENAME-INSTRUCTIONS.md` | Manual steps for the rebrand. Already executed. |
| Rebrand completion summary | `REBRAND-COMPLETION-SUMMARY.md` | Historical artifact. Delete fully (consistent with §10 Q6). |
| Stale Inno Setup script | `installer/flipkit-setup.iss` | v3.0.0, hardcoded version. Real installer is `installer/Windows/FlipKit.iss` (v3.3.6) per `build-installers.ps1` and `build-hub-for-installer.ps1`. |
| Stale installer README | `installer/README.md` | Documents the dead `flipkit-setup.iss`. |
| Old release notes | `release-notes-v2.2.0.md`, `release-notes-v2.2.1.md`, `release-notes-v3.0.0.md`, `release-notes-v3.1.0.md` | Pre-v3.3.6. Confirmed delete (§10 Q6). `git log` retains history; CHANGELOG.md is sole source of truth going forward. |
| Old changelogs | `CHANGELOG-v2.0.3.md`, `CHANGELOG-v2.0.4.md` | Web-only changelogs from before unification. Confirmed delete (§10 Q6). |
| Docker deployment files | `Dockerfile`, `docker-compose.yml`, `docker-entrypoint.sh`, `build-web-package.bat`, `build-web-package.sh` | Predate Hub unification; no longer an active deployment target. Confirmed kill (§10 Q4). |
| `convert-to-word.bat` | repo root | Pandoc Word-doc generator. Confirmed kill (§10 Q5). |
| Build logs | `build-3.3.6.log`, `build-installer-3.3.6.log` | Build output committed to repo. Should be `.gitignore`d. |
| Debug log files | `Docs/debug/flipkit-20260430.log`, `flipkit-20260501.log` | Serilog runtime logs accidentally checked in (the path is intentional per `App.axaml.cs` but the `.log` files shouldn't be in source). Add to `.gitignore`. |
| `DatabaseSeeder` | `FlipKit.Core/Data/DatabaseSeeder.cs` | Only call site is **commented out** in `App.axaml.cs:182–184` with note "users don't want auto-generated cards." Web's `Program.cs` doesn't call it either. Unambiguously dead. |
| `MockScannerService` | `FlipKit.Core/Services/Implementations/MockScannerService.cs` | Only consumer was `ScreenshotTool` (which itself is dead). Not registered in DI by Desktop or Web. |
| `BoolToVisibilityConverter` | `FlipKit.Desktop/Converters/BoolToVisibilityConverter.cs` | No `StaticResource` reference in any `.axaml`. (Other 9 converters all verified bound — keep them.) |

### KEEP-ROADMAP (do NOT remove, reserved for planned work)

| Item | Path | Roadmap reason |
|---|---|---|
| `Point130SoldPriceService` + `ISoldPriceService` | `FlipKit.Core/Services/Implementations/Point130SoldPriceService.cs` | Comment in `PricingViewModel.cs:19` says "SHELVED ... kept for potential future use" — directly maps to roadmap #3 Automated Price Scraping. |
| `HtmlAgilityPack` package ref | `FlipKit.Core.csproj`, `FlipKit.Desktop.csproj` | Only consumer is `Point130SoldPriceService` (above). Roadmap #3. |
| `ChecklistLearningService`, `MissingChecklist`, `IChecklistLearningService` | `FlipKit.Core/...` | Roadmap #1 Checklist Insider import — these are the learning-from-scans surface. |
| `XimilarService` + all `XimilarScanMode` plumbing | Core + Web + Desktop | Active in `CompositeScannerService` — used in production scan path with the `XimilarScanMode.Standard/Magic/Disabled` switch in the UI. NOT roadmap-only, currently shipping. |
| COMC enum value | `ExportPlatform.cs` | Roadmap #5 Finish COMC Exporter. |
| Dark theme groundwork (`App.axaml` `RequestedThemeVariant="Default"`, `Styles/AppStyles.axaml`) | Desktop | Roadmap #7. |

### INVESTIGATE (remaining items — resolve during Phase 1)

| Item | Path | Question |
|---|---|---|
| `test-web-app.ps1` | repo root | Hardcoded `localhost:5000` smoke test. Outdated relative to current Web routes? Either update + KEEP (becomes part of regression checklist) or delete. |
| `TAILSCALE-SYNC-GUIDE.md` (root) vs `Docs/Tailscale-Setup-*.md` | root + Docs | Two parallel sets of Tailscale docs. Pick one home. |
| `installers/FlipKit-Windows-x64-v3.3.0.zip` | `installers/` | Old build artifact in source. Probably should be in `releases/` or deleted entirely; release artifacts shouldn't be in git. |

### Discovery commands the audit phase must run

These are the verification commands every candidate gets before being moved to REMOVE:

```
# Code references
Grep -r "<TypeName>"                    # all .cs, .axaml, .cshtml
Grep -r "<filename-no-ext>"             # filename references in scripts/docs

# Build references
Grep -rn "<filename>" *.ps1 *.sh *.bat *.csproj *.sln Dockerfile

# DI registration check (per service)
Grep -n "AddSingleton\|AddTransient\|AddScoped" FlipKit.Desktop/App.axaml.cs FlipKit.Web/Program.cs FlipKit.Api/Program.cs

# Roadmap mapping
Grep -n "<TypeName>" Docs/17-FUTURE-ROADMAP.md Docs/26-CSV-EXPORT-IMPLEMENTATION-PLAN.md \
                     Docs/27-WEBCAM-CAPTURE-PLAN.md Docs/28-CHECKLIST-INSIDER-IMPORT-PLAN.md
```

**Exit criteria:** The audit doc exists with every candidate labeled, INVESTIGATE rows resolved with the user, and a final "go list" of REMOVE items signed off. No code changes yet.

---

## 4. Phase 2 — Doc Archival & Root Tidy

**Goal:** Get the repo's *file tree* legible before touching code. Pure document/file moves — easy to review, easy to revert.

### 4.1 Docs/ folder treatment

Create `Docs/archive/` and move:
- `18-PHASE1-COMPLETION-SUMMARY.md`
- `19-TESTING-CHECKLIST-PHASE1.md`
- `20-PHASE2-COMPLETION-SUMMARY.md`
- `21-PHASE3-TESTING-PLAN.md`
- `22-PHASE3-PROGRESS-SUMMARY.md`
- `23-FUNCTIONAL-TEST-RESULTS.md`
- `24-PHASE3-COMPLETION-SUMMARY.md`

These are all `Phase N completion / Phase N progress` write-ups from prior refactors. They have historical value (audit trail of what was decided when) but should not be in the main `Docs/` folder where they crowd out load-bearing docs.

Resolve duplicate numbering:
- `10-GUI-ARCHITECTURE.md` is referenced from `CLAUDE.md` — KEEP as `10-GUI-ARCHITECTURE.md`.
- `10-GUI-OPTIONS.md` is the older "which UI framework should we use" deliberation — move to `Docs/archive/10-GUI-OPTIONS.md` (decision was made: Avalonia).

Load-bearing docs (referenced by README.md or CLAUDE.md or active plan docs) — KEEP in main `Docs/`:
- `00-PROGRAM-OVERVIEW.md`, `01-PROJECT-PLAN.md` (if still used as orientation)
- `02-DATABASE-SCHEMA.md`
- `03-OPENROUTER-INTEGRATION.md`
- `04-WHATNOT-CSV-FORMAT.md`
- `05-PRICING-RESEARCH.md`
- `06-IMAGE-HOSTING.md`
- `07-CLAUDE-CODE-GUIDE.md` (NB: contains stale references to old structure — flag for Phase 6 update)
- `08-CARD-TERMINOLOGY.md`
- `09-EBAY-API.md`
- `10-GUI-ARCHITECTURE.md`
- `11-UX-DESIGN.md`
- `12-INSTALL-GUIDE.md`
- `13-INVENTORY-TRACKING.md`
- `14-VARIATION-VERIFICATION.md`
- `15-VERIFICATION-BUILD-GUIDE.md`
- `16-CHECKLIST-DATA-SPEC.md`
- `17-FUTURE-ROADMAP.md`
- `25-DISTRIBUTION-PACKAGING.md`
- `26-CSV-EXPORT-IMPLEMENTATION-PLAN.md`
- `27-WEBCAM-CAPTURE-PLAN.md`
- `28-CHECKLIST-INSIDER-IMPORT-PLAN.md`
- `HUB-ARCHITECTURE.md`
- `USER-GUIDE.md`, `WEB-USER-GUIDE.md`, `Mac-Installation-Guide.md`, `DEPLOYMENT-GUIDE.md`, `Tailscale-Setup-*.md`

Add `Docs/archive/README.md` — one paragraph explaining "these are historical decision records, kept for context, not maintained."

### 4.2 Repo root tidy

Move to `Docs/archive/`:
- `REBRAND-COMPLETION-SUMMARY.md`
- `GITHUB-RENAME-INSTRUCTIONS.md`

Delete:
- `rename-folders.ps1`, `rename-flipkit-content.ps1`, `rename-to-flipkit.ps1`
- `installer/flipkit-setup.iss`, `installer/README.md` (then re-add a fresh README pointing at `installer/Windows/FlipKit.iss`)
- `CHANGELOG-v2.0.3.md`, `CHANGELOG-v2.0.4.md`
- `release-notes-v2.2.0.md`, `release-notes-v2.2.1.md`, `release-notes-v3.0.0.md`, `release-notes-v3.1.0.md`
- `build-3.3.6.log`, `build-installer-3.3.6.log`
- `installers/FlipKit-Windows-x64-v3.3.0.zip` (binary in source)
- `Dockerfile`, `docker-compose.yml`, `docker-entrypoint.sh`, `build-web-package.bat`, `build-web-package.sh` (per §10 Q4)
- `convert-to-word.bat` (per §10 Q5)

Add to `.gitignore`:
- `*.log`
- `Docs/debug/*.log`
- `releases/temp/`
- `installers/*.zip`, `installers/*.exe` (build outputs only)

Update `CHANGELOG.md`:
- Move "Tailscale Sync" out of `[Unreleased]` (it shipped a long time ago).
- Backfill 3.x entries up through v3.3.6.

Update `CLAUDE.md`:
- Bump version line from "v3.2.0" to "v3.3.6" (drift the user explicitly flagged).
- Update the troubleshooting note about `CardListerDbContext.cs` once Phase 3 renames the file.

**Exit criteria:** Repo root has < 12 files visible. `Docs/` main directory has only 25-ish numbered docs + key references. Manual checklist passes (only flow #10 needed since this is doc-only).

---

## 5. Phase 3 — Trivial Code Cleanup

**Goal:** Apply REMOVE-labeled code changes that have one-line blast radius. Strictly safer-than-safe.

### 5.1 File rename

- Rename `FlipKit.Core/Data/CardListerDbContext.cs` → `FlipKit.Core/Data/FlipKitDbContext.cs`. Class name (`FlipKitDbContext`) does not change. Update the `CLAUDE.md` troubleshooting note. **Sequencing constraint:** must happen *before* anyone adds a new EF migration or new schema-update method, so a future contributor doesn't grep for the old name and find nothing.

### 5.2 Delete confirmed-dead code

- `FlipKit.Core/Data/DatabaseSeeder.cs` — delete. Remove the commented-out call in `App.axaml.cs:182–184`.
- `FlipKit.Core/Services/Implementations/MockScannerService.cs` — delete (only consumer was the dead ScreenshotTool).
- `FlipKit.Desktop/Converters/BoolToVisibilityConverter.cs` — delete (no XAML binding).
- `ScreenshotTool/` — delete the entire directory (§10 Q2).
- `FlipKit.Core/Helpers/LegacyMigrator.cs` — delete (§10 Q1). Remove its call site from `App.axaml.cs` and `FlipKit.Web/Program.cs` if present.

### 5.3 Single-tool sanity sweeps

- Run `dotnet format` on the solution to fix unused `using` directives the IDE-side linter has been ignoring.
- Run `dotnet build FlipKit.sln` — must be 0 errors, 0 *new* warnings.
- Resolve any *existing* warnings introduced visible after the format pass (don't suppress).

### 5.4 ViewModel comment cleanup

In `App.axaml.cs`, remove the now-stale commented-out blocks:
- `// Disabled sample card seeding - users don't want auto-generated cards`
- `// Log.Debug("Running database seeder");`
- `// DatabaseSeeder.SeedIfEmptyAsync(db).GetAwaiter().GetResult();`

These become dead history once `DatabaseSeeder` itself is gone.

**Exit criteria:** Solution builds clean, all 10 manual flows pass, `git diff` shows only deletions + the rename.

---

## 6. Phase 4 — Full Test Coverage

**Goal:** Build the test suite that makes Phase 5 bulletproof. Folds in roadmap item #4 (Tests) entirely — that item gets struck from the future-work list at end of Phase 6. Coverage targets: **ViewModels 80%+, Services 70%+, Helpers 90%+**.

Effort: 3-4 weeks. Phase 5 does not start until coverage targets are green.

### 6.1 `FlipKit.Core.Tests` (xUnit)

**Helpers** (target 90%+):
- `FuzzyMatcher` — ratio thresholds, null handling, case insensitivity
- `WhatnotCategoryDefaulter` — every Sport → category mapping (Wrestling/Golf/Tennis/Racing/MMA/Soccer/Hockey)
- `CardStatusEvaluator` — every CardStatus transition path
- `PriceCalculator` — fee math, profit math, edge cases (zero fees, negative profit)
- `ConditionMapper` — every condition string → eBay/Whatnot output
- `ShippingProfileNormalizer` — every input variant
- `SkuGenerator` — uniqueness contract, format validation
- `DataAccessModeDetector` — Local vs Remote detection logic

**Services** (target 70%+):
- `WhatnotExporter` — given N `Card` records, output CSV matches expected fixture
- `EbayExporter` — same, against `ebay_template_header.csv`
- `ExportValidator` — known-bad rows produce expected errors
- `CsvExportService` — title template generation per platform
- `OpenRouterScannerService` — recorded responses (VCR pattern); JSON parse, markdown stripping, error handling
- `XimilarService` — recorded responses; mode switching (Standard/Magic/Disabled)
- `CompositeScannerService` — composition logic with mocked dependencies
- `VariationVerifierService` — fuzzy match against seeded `SetChecklist`
- `PricerService` — deeplink URL generation
- `TitleTemplateService` — template substitution
- `BulkScanErrorLogger` — append + retrieval contracts
- `ChecklistLearningService` — learn-from-scan flow
- `CardRepository` — in-memory SQLite, full CRUD + queries (unpriced, stale, stats)
- `ApiCardRepository` — mocked HttpClient (recorded responses)

### 6.2 `FlipKit.Desktop.Tests` (xUnit + Moq + Avalonia.Headless)

**ViewModels** (target 80%+) — all services mocked via Moq:
- `MainWindowViewModel` — navigation, page resolution
- `ScanViewModel` — image picking, scan flow, result enrichment
- `BulkScanViewModel` — queue, cancellation, rate-limit handling
- `InventoryViewModel` — filter/sort/edit/delete
- `PricingViewModel` — research deeplinks, value updates
- `RepriceViewModel` — bulk reprice flow
- `ExportViewModel` — preview, generate, validation
- `ReportsViewModel` — sales, financial summaries
- `ChecklistManagerViewModel` — view + edit flows
- `CardDetailViewModel` — load, edit, save, delete
- `SettingsViewModel` — settings persistence, connection tests (each tester mocked)
- `SetupWizardViewModel` — first-run wizard

**Avalonia.Headless smoke tests** for critical UI flows:
- App boot → MainWindow rendered with correct DataContext
- Navigate Scan → Inventory → Pricing → Export → Reports
- Open EditCardView dialog and confirm bindings populate
- SettingsView server start/stop UI commands wire to `IServerManagementService`

### 6.3 `FlipKit.Web.Tests` (xUnit + Moq)

**Controllers** with mocked Core services + WebApplicationFactory for integration tests:
- `HomeController` — dashboard render
- `PricingController` — list, research, save
- `ExportController` — preview, generate
- `ReportsController` — sales, financials

### 6.4 Test infrastructure

- Embedded JSON fixtures for input cards (no DB needed for unit-level tests)
- Recorded HTTP responses for OpenRouter / Ximilar / ImgBB / eBay (VCR-style under `Tests/Fixtures/Http/`)
- `WebApplicationFactory<Program>` for Web integration tests
- Coverlet for coverage measurement; gate at 80/70/90 in CI

### 6.5 Wire into build script

- `build-release.ps1` and `build-installers.ps1` run `dotnet test` before publishing
- Block release on any test failure or coverage drop below targets
- `Docs/REGRESSION-CHECKLIST.md` committed (the 10 manual flows from §1) — kept as a layer above automated tests for end-to-end sanity after each phase

**Exit criteria:** `dotnet test` green across all three test projects; coverage targets met (80% VMs / 70% Services / 90% Helpers); release build script blocks on failures; manual regression checklist committed.

---

## 7. Phase 5 — Targeted Code Refactors

**Goal:** Address the structural debt called out in the roadmap's "Technical Debt" section and the architectural issues found during exploration. This is the riskiest phase and the test foundation from Phase 4 gates it.

Each subsection is independent — pick them off one at a time on separate feature branches, run the manual checklist + smoke tests after each merge.

### 7.1 DI lifetime fix (BUG, surfaces under load)

`App.axaml.cs:135` registers `Point130SoldPriceService` as `Singleton`, but its constructor takes `FlipKitDbContext` (Scoped). This is a captive-dependency bug — the singleton holds the first-resolved DbContext forever and will throw `ObjectDisposedException` after the first request scope ends.

The Web project (`FlipKit.Web/Program.cs:82`) already correctly uses `AddScoped`. Per `Docs/22-PHASE3-PROGRESS-SUMMARY.md` this was identified before but never fixed in Desktop.

**Fix:** Change `AddSingleton<ISoldPriceService, Point130SoldPriceService>()` → `AddScoped<…>()` in `App.axaml.cs`. Audit every other registration in the same file for the same pattern (specifically anything taking `FlipKitDbContext`).

**Risk note:** This is a real behavioral change. Even though `ISoldPriceService` is currently not injected anywhere active in Desktop (per the "SHELVED" comment), fixing the registration is preventive and maintains parity with Web.

### 7.2 Magic-string elimination for OpenRouter model IDs

The model ID `"nvidia/nemotron-nano-12b-v2-vl:free"` is hardcoded as a default parameter in 4 files (`IScannerService.cs`, `OpenRouterScannerService.cs`, `MockScannerService.cs` (now deleted), `CompositeScannerService.cs`). When this model is rotated, all four sites must be updated in lockstep.

**Fix:** Introduce `FlipKit.Core/Configuration/ScannerDefaults.cs`:
```csharp
public static class ScannerDefaults
{
    public const string DefaultFreeModelId = "nvidia/nemotron-nano-12b-v2-vl:free";
}
```
Replace every occurrence. SOP-aligned: this is a typed-config introduction, not a sprawling refactor.

### 7.3 HttpClient timeout configuration

Only one hardcoded timeout was found: `ServerManagementService.cs:42` → `_httpClient.Timeout = TimeSpan.FromSeconds(2)`. Roadmap calls out "hardcoded timeouts" plural, so audit any `Task.Delay(N)` and `CancellationTokenSource(...timeout)` while we're here. Centralize in `AppSettings` if more than one timeout exists.

### 7.4 ViewModel decomposition (the big one)

Sizes confirmed by `wc -l`:
| ViewModel | Lines |
|---|---|
| `SettingsViewModel` | **803** |
| `BulkScanViewModel` | 585 |
| `InventoryViewModel` | 556 |
| `ScanViewModel` | 546 |
| `ExportViewModel` | 299 |

Note: `SettingsViewModel` was *not* on the user's flagged list but is the worst offender. Add it.

**Decomposition pattern (fixed per §10 Q8):** extract *helper services* (in `FlipKit.Core/Services/`) over partial-class or region splits. Helper services are testable in isolation — partials and regions aren't, and Phase 4's coverage targets demand testable units. Suggested splits:

- `ScanViewModel` → extract `ScanResultEnrichmentService` (the post-scan checklist-learning + verification glue) and `ImageRotationService`.
- `BulkScanViewModel` → extract `BulkScanQueueService` (queue management + cancellation) and `RateLimitTracker`.
- `InventoryViewModel` → extract `InventoryFilterService` (filter/sort logic) and `InventoryColumnConfig`.
- `ExportViewModel` → extract `ExportPreviewBuilder` (already partially in `ExportableCard`).
- `SettingsViewModel` → extract `SettingsValidationService`, `XimilarConnectionTester`, `OpenRouterConnectionTester`, `ImgBBConnectionTester` — most of the bulk is connection-test helpers that don't belong in a ViewModel.

Do these one at a time, on separate branches, each followed by the full manual regression checklist + the new helper unit tests.

### 7.5 Stale `Docs/07-CLAUDE-CODE-GUIDE.md`

This doc still references `MockScannerService` and `BoolToVisibilityConverter` as live files, and uses the old folder layout. Refresh it to match the cleaned tree. (Defer to Phase 6 if scope is tight.)

### 7.6 Optional: SchemaUpdater → real EF migrations

`FlipKit.Core/Data/SchemaUpdater.cs` has 175 lines of raw `ALTER TABLE IF NOT EXISTS` SQL because the project uses `EnsureCreated()` instead of EF migrations. Every new column accretes here forever. Long-term this should become real migrations, but converting from `EnsureCreated` to migrations on a live SQLite database is a Phase 6+ project — not in scope for this refactor sweep.

**Exit criteria for Phase 5:** Each subsection passes the manual regression checklist *and* the smoke tests on its own branch before merge. End-of-phase: total LOC down, no ViewModel above ~400 lines.

---

## 8. Phase 6 — Roadmap & Plan Doc Revamp

**Goal:** With the codebase cleaned, take a fresh pass at the planning docs. The user's stated final goal.

### 8.1 Re-cost every roadmap item

Open `Docs/17-FUTURE-ROADMAP.md` and, for each item, ask the same questions against the *cleaned* codebase:

| Roadmap item | Re-evaluate |
|---|---|
| #1 Checklist Insider Import | Does Phase 5.4's `ChecklistManagerViewModel` extraction make the import view easier to add? Update effort estimate. |
| #2 Webcam Capture | Avalonia 11.3 webcam APIs — confirm latest support; reconfirm or drop. |
| #3 Automated Price Scraping | `Point130SoldPriceService` is still shelved. Decision time: revive it (eBay Finding API per §17 Option A) or delete it. |
| #4 Tests | **Strike from roadmap** — fully delivered by Phase 4 per §10 Q7. |
| #5 COMC Exporter | If no concrete signal of demand, consider dropping this and removing the `COMC` enum value. |
| #6 Inventory virtualization | Inventory is now smaller after Phase 5.4 split — re-measure threshold (still 500 cards?). |
| #7 Dark theme | Phase 5 may have surfaced more theme leaks; re-assess effort. |
| #8 PWA, #9 Price alerts | Probably stay where they are. |

### 8.2 Update plan docs against new code

- `Docs/26-CSV-EXPORT-IMPLEMENTATION-PLAN.md` — mark sections complete vs in-progress against actual code.
- `Docs/27-WEBCAM-CAPTURE-PLAN.md` — re-validate against current Avalonia version.
- `Docs/28-CHECKLIST-INSIDER-IMPORT-PLAN.md` — verify file paths, type names referenced still exist post-rename.

### 8.3 New ADRs

Create `Docs/ADR/` and write short ADRs for the non-obvious choices the user has made:
- ADR-001: Why Hub (Desktop + embedded servers) over separate apps.
- ADR-002: Why net8.0 + net9.0 mix (Api on net9, others on net8).
- ADR-003: Why `EnsureCreated` + `SchemaUpdater` over EF migrations.
- ADR-004: Why user-driven Checklist Insider import (legal posture).
- ADR-005: Why Avalonia over MAUI/WPF.

These were decisions made earlier that no doc captures.

**Exit criteria:** `17-FUTURE-ROADMAP.md` re-baselined with realistic post-cleanup estimates. `CLAUDE.md` reflects current state. ADRs cover the top 5 architectural decisions.

---

## 9. Sequencing Constraints (what blocks what)

```
Phase 0 (SOP read) ─→ Phase 1 (audit) ─→ Phase 2 (docs)
                                       └→ Phase 3 (trivial code)
                                          ├→ rename DbContext file BEFORE adding any new schema work
                                          └→ delete DatabaseSeeder BEFORE removing the commented call site
Phase 3 done ─→ Phase 4 (tests) ─→ Phase 5 (refactors)
                                    ├→ DI lifetime fix BEFORE any new feature touches scanning
                                    ├→ Magic-string elim BEFORE next OpenRouter model rotation
                                    └→ ViewModel splits ONLY AFTER smoke tests green
Phase 5 done ─→ Phase 6 (roadmap revamp)
```

Hard rules:
- Never start Phase 5 until Phase 4 coverage targets (80/70/90) are green.
- Never delete files that show up only in Docs/ (might be roadmap-referenced from a doc you haven't read).
- Branch per subsection — keep PRs small and revertable.

---

## 10. Decisions Logged (2026-05-04)

| # | Question | Decision | Affects |
|---|---|---|---|
| Q1 | `LegacyMigrator` — keep or kill? | **Kill** | Phase 3 §5.2 |
| Q2 | `ScreenshotTool/` — kill, repair, or keep? | **Kill** | Phase 3 §5.2 (also clears `MockScannerService`) |
| Q3 | COMC exporter — keep or drop? | **Keep** (roadmap #5 stays) | Phase 1 KEEP-ROADMAP |
| Q4 | Docker files — active deployment target? | **Kill** all 5 files | Phase 2 §4.2 |
| Q5 | `convert-to-word.bat` — used for SOP help bundle? | **Kill** | Phase 2 §4.2 |
| Q6 | Old release notes / changelogs — archive or delete? | **Delete fully** (`git log` retains history) | Phase 2 §4.2 |
| Q7 | Test scope for Phase 4 — hybrid or full coverage? | **Full coverage** (80% VMs / 70% Services / 90% Helpers); roadmap #4 folds in and is struck | Phase 4 §6, Phase 6 §8.1 |
| Q8 | ViewModel split pattern — helper services or partials? | **Helper services** to `FlipKit.Core/Services/`, no SOP gate | Phase 5 §7.4 |

### Remaining INVESTIGATE items (resolve during Phase 1, no hard gate)

- `test-web-app.ps1` — outdated against current Web routes? Update + keep, or delete.
- `TAILSCALE-SYNC-GUIDE.md` (root) vs `Docs/Tailscale-Setup-*.md` — pick one home.
- `installers/FlipKit-Windows-x64-v3.3.0.zip` — old build artifact in source; delete (binaries don't belong in git).
