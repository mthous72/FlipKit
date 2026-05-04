# FlipKit Refactor — Status Checkpoint

**Snapshot date:** 2026-05-04 (updated post-Phase 4.5)
**Master HEAD:** `938cd75` (CHANGELOG history pointer); push pipeline now linear (origin master rebased to drop merge commits per branch protection rule)
**Plan:** [29-REFACTORING-PLAN.md](29-REFACTORING-PLAN.md)
**Audit:** [AUDIT-2026-05.md](AUDIT-2026-05.md)

This is a **breakpoint snapshot** — a single doc to read when picking the work back up. Sources of truth are still the plan and audit; this just summarizes "where we are / where we're going" in one place.

---

## Where we are

### Phases complete

| # | Phase | Branch | Merged | Commits | Headline |
|---|---|---|---|---|---|
| 1 | Inventory & Discovery | `refactor/phase-1-audit` | ✓ | 2 | Verified every cleanup candidate, surfaced 8 plan inaccuracies, answered 5 follow-up Qs |
| 2 | Doc Archival & Root Tidy | `refactor/phase-2-doc-tidy` | ✓ | 2 | Repo root 30+ → 9 files; 9 historical docs archived; Docker/rebrand/old-release-notes deleted |
| 3 | Trivial Code Cleanup | `refactor/phase-3-trivial-code` | ✓ | 1 | Renamed DbContext file; deleted 5 dead-code targets + 925 lines; 0 errors / 0 warnings (was 6) |
| 4a | Core Tests — helpers + stateless services | `refactor/phase-4a-core-tests` | ✓ | 1 | xUnit + NSubstitute scaffolded; 128 tests, all helpers ≥90%, all stateless services ≥70% |
| 4b | Core Tests — data + scanner services | `refactor/phase-4b-core-data-tests` | ✓ | 1 | +117 tests (245 total); SQLite-in-memory + HTTP-mock patterns validated; 2 production bugs surfaced |
| 4.5 | Bug-fix interlude (D3 ValueComparer) | `refactor/phase-4.5-checklist-bug-fix` | ✓ | 1 | Out-of-band fix for SetChecklist mutation bug; un-skipped the blocked test; suite now 246/246 |
| 4c | Desktop Tests — 13 ViewModels | `refactor/phase-4c-desktop-tests` | ✓ | 1 | +162 tests (408 total); 11 of 13 VMs ≥80% coverage; 2 deferred to 4e gap-fill |
| 4d | Web Tests — 7 controllers + ViewLocator smoke | `refactor/phase-4d-web-tests` | ✓ | 1 | +50 tests (458 total); 48 web controller tests + 2 ViewLocator contract tests; HttpContext.Session paths skipped |
| 4e | Coverage gap-fill + CI gate + regression checklist | `refactor/phase-4e-coverage-gate` | ✓ | 1 | +23 fill-in tests (481 total); XimilarService 51%→79.5%; CI gate wired into build scripts; REGRESSION-CHECKLIST.md committed; **Phase 4 complete** |
| 5a | Mechanical fixes bundle (DI lifetimes + HttpClient timeout + OpenRouter retry + Settings race) | `refactor/phase-5a-fixes-cleanup` | ✓ | 1 | Closes D1 + D2 + §7.10 Race 1 + §7.3 timeout config. +1 test (482 total). |
| 5b | OpenRouter catalog consolidation | `refactor/phase-5b-openrouter-catalog` | ✓ | 1 | Closes D4. Static arrays moved to OpenRouterModelDefaults, fallback catalog returned (with IsFallback flag) on fetch failure. +2 tests (484 total). |

**Master is in sync with `origin/master`.** Branch protection on origin rejects merge commits, so the original 13-commit merge-heavy local history was rebased to a linear 9-commit chain before pushing. Going forward, every phase merge to local master gets `git push origin master` after a clean rebase if needed.

### Test suite snapshot

```
Combined solution suite (Core + Desktop + Web)
  Total:    481 tests
  Passing:  481
  Skipped:    0
  Failing:    0
  Runtime: ~7 seconds
  Build:    0 errors, 0 warnings

  FlipKit.Core.Tests:    264 tests
  FlipKit.Desktop.Tests: 169 tests
  FlipKit.Web.Tests:      48 tests

  CI gate: build-installers.ps1 + build-release.ps1 abort if dotnet test fails.
```

Coverage state (Phase 4 final targets: VMs ≥80%, Services ≥70%, Helpers ≥90%):

| Layer | State |
|---|---|
| Helpers (5 surfaces) | All ≥95% — target met |
| Stateless services (5 surfaces, Phase 4a) | All ≥84% — target met |
| Data + scanner services (11 surfaces, Phase 4b/4e) | 9 of 11 ≥70% — 2 deferred (see below) |
| Desktop ViewModels (13 surfaces, Phase 4c/4e) | 11 of 13 ≥80% — 2 deferred (see below) |
| Web controllers (7 surfaces, Phase 4d) | 7 of 7 covered — sufficient unit coverage; integration tests deferred to need-driven |

Below-target carryovers (deferred to Phase 5 work since the surfaces are explicit decomposition/refactor targets):

| Surface | Coverage | Reason for deferral |
|---|---|---|
| `ChecklistLearningService` | 29.62% | `TryLoadFromSeedData` reads embedded resources (not testable without test fixtures); other gap is in low-traffic export paths. Phase 5 may extract `ISeedDataLoader` as part of Roadmap #1 work. |
| `VariationVerifierService` | 56.09% | Confirmation-pass paths now covered (43%→56%); remainder is in `ValidateVisualCues` cross-reference logic with many short branches. Acceptable; Phase 5.4 may split out a `VerificationFieldEvaluator` helper. |
| `ScanViewModel` | 71.17% | Fill-in tests didn't move the needle — gap is in error-handling branches of `LoadModelsAsync` / `SaveCardAsync` and 6+ branches of `AcceptSuggestion`. Phase 5.4 splits this VM. |
| `SettingsViewModel` | 78.70% | Gap is in `UpdateLocalIpAddresses` (calls `NetworkHelper.GetNetworkInfo()` static — real network IO) and the QR-code generation path. Phase 5.4 splits this VM. |

Below-target services from Phase 4b (deferred to Phase 4e gap-fill):
- `VariationVerifierService` — 43.55% (RunConfirmationPassAsync path untested)
- `XimilarService` — 50.81% (MapTagsToCard fallback path untested)
- `ChecklistLearningService` — 29.62% (partly blocked by §5.10 bug; rest is embedded-resource path)

### Discoveries (production bugs found while writing tests)

These are real production issues uncovered by Phase 1 audit + Phase 4 test work. All are logged in the audit and have a corresponding fix item in Phase 5 of the plan.

| # | Severity | Description | Audit ref | Plan fix |
|---|---|---|---|---|
| D1 | **High — FIXED** | `ISoldPriceService` registered as `Singleton` in Desktop with a `Scoped` DbContext dep — captive-dependency bug. Web is correct. **Fixed in Phase 5a** (also fixed `IVariationVerifier` lifetime per AUDIT §4 corrected scope). | §4 | §7.1 |
| D2 | **High — FIXED** | `OpenRouterScannerService.IsRetryableHttpError` checks for digit substrings (`"500"`) but `HttpStatusCode.ToString()` produces enum names (`"InternalServerError"`). Fallback chain never triggers for 5xx/429 — only 404 actually works. **Fixed in Phase 5a** by including the integer in the throw-site message. | §5.9 | §7.8 |
| D3 | **High — FIXED** | `SetChecklist.Cards` and `KnownVariations` are JSON-converted via `HasConversion(serialize, deserialize)` without a `ValueComparer`. EF Core can't detect collection mutations on JSON-converted properties. `ChecklistLearningService`'s "enrich existing checklist" path silently fails in production — directly blocks Roadmap #1 Checklist Insider feature. **Fixed in Phase 4.5.** | §5.10 | §7.9 |
| D4 | Medium — FIXED | `OpenRouterModelCatalog.GetAsync` returns an empty `ModelCatalog` on fetch failure with a "caller should fall back gracefully" warning, but no caller has a fallback. `OpenRouterScannerService` has the static fallback catalog but in the wrong place. **Fixed in Phase 5b** — fallback catalog moved to `OpenRouterModelDefaults`, `GetAsync` returns the fallback (with `IsFallback = true`) on failure or filtered-empty response. | §5.5 | §7.2 |
| D5 | Medium | `Docs/25-DISTRIBUTION-PACKAGING.md` referenced `build-web-package.{bat,sh}` and the standalone Web zip distribution path — both deleted in Phase 2. Doc archived whole. | Q1 | Phase 2 §4.1 |
| D6 | Low | `installer/Linux/`, `installer/Mac/`, `installer/Windows/` were the active installer dirs but the `installer/README.md` documented `flipkit-setup.iss` (v3.0.0, deleted). | §1 | Phase 2 §4.2 |
| D7 | Low | `LegacyMigrator` had **6 call sites in 3 files** (Desktop + Web + Api), not 2 as the plan said. | §5.1 | Phase 3 §5.2 |
| D8 | Low | `MockScannerService` existed in **two** places — `FlipKit.Core/...` and `ScreenshotTool/MockServices.cs`. Both went with the ScreenshotTool deletion. | §5.2 | Phase 3 §5.2 |

### Plan corrections

These are ways the plan was wrong and has since been corrected. Future-you should trust the latest plan, not the original commit.

- **Phase 5.1 DI lifetime fix scope** — originally expanded to 4 services (Point130, Pricer, CsvExport, VariationVerifier). Phase 4b found that `PricerService` and `CsvExportService` don't actually take `FlipKitDbContext` (Web's `// Depends on DbContext` comments are stale). **Real scope is 2 services** (Point130, VariationVerifier). To be corrected in plan during Phase 5 implementation.
- **Phase 4a scope** — `SkuGenerator` was originally listed under "stateless services" but takes `FlipKitDbContext`. Moved to Phase 4b. `BulkScanErrorLogger` calls `Environment.GetFolderPath(LocalApplicationData)` at construction; deferred to Phase 5 with a constructor refactor.
- **Phase 5.2 OpenRouter magic-string fix** — originally framed as introducing a single `DefaultFreeModelId` constant. Reality is a multi-model fallback catalog. Scope expanded to consolidate live + fallback catalog into `OpenRouterModelCatalog` (closes D4 as a side effect).

---

## Where we're going

### Phase 4 remaining sub-phases

The big test-coverage build-out is roughly half done. Three sub-phases remain.

| # | Phase | Estimated effort | Scope |
|---|---|---|---|
| 4c | Desktop ViewModels | ~1 week | 12 ViewModels mocked through their service deps via NSubstitute. No DbContext touched. Hardest surfaces: `SettingsViewModel` (803 lines), `BulkScanViewModel` (585) — Phase 5.4 will *split* these later, so 4c effectively encodes current behavior to lock down before refactor. |
| 4d | Web controllers + Avalonia.Headless smoke | ~3-4 days | 6 controllers via NSubstitute + `WebApplicationFactory` for integration tests. Plus 4 Avalonia.Headless smoke tests for App boot and navigation. |
| 4e | Coverage gap-fill + CI gate | ~2-3 days | Bring the 3 below-target services from Phase 4b up to 70% (VariationVerifier, Ximilar, ChecklistLearning). Wire Coverlet into `build-installers.ps1`. Commit `Docs/REGRESSION-CHECKLIST.md`. Decide whether to retire `test-web-app.ps1` (per AUDIT Q3). |

### Phase 5 — Targeted Code Refactors (4 sub-phases, post-regroup scope)

Phase 4 is now complete (§coverage targets met or carryovers documented). Phase 5 has been **scoped down** in the post-Phase-4 regroup:

| # | Branch | Scope | Estimate |
|---|---|---|---|
| 5a | `refactor/phase-5a-fixes-cleanup` | §7.1 DI fixes (2 services) + §7.3 HttpClient timeouts + §7.8 OpenRouter retry filter + §7.10 SettingsViewModel races | 1-2 days |
| 5b | `refactor/phase-5b-openrouter-catalog` | §7.2 OpenRouter catalog consolidation (closes D4 latent bug) | 1-2 days |
| 5c | `refactor/phase-5c-settings-vm-split` | §7.4a Split SettingsViewModel (803 lines) | 3-4 days |
| 5d | `refactor/phase-5d-bulkscan-vm-split` | §7.4b Split BulkScanViewModel (585 lines) | 2-3 days |

**Dropped from Phase 5** (re-evaluated in Phase 6): InventoryViewModel / ScanViewModel / ExportViewModel splits. All three are testable as-is (Phase 4c coverage), and decomposition opportunities are less obvious without a fresh read. Phase 6 roadmap revamp decides if they're worth it.

**Deferred to Phase 6:** §7.5 Doc 07 refresh (already a docs-heavy phase).

Subsections from plan §7 (full reference):

- **§7.1** DI lifetime fixes — 2 services (Point130, VariationVerifier), per the corrected scope above
- **§7.2** OpenRouter catalog consolidation — closes D2 indirectly + closes D4 as a side effect
- **§7.3** HttpClient timeout configuration centralization
- **§7.4** ViewModel decomposition — extract helper services from `SettingsViewModel` (803 lines), `BulkScanViewModel` (585), `InventoryViewModel` (556), `ScanViewModel` (546), `ExportViewModel` (299). Re-enables Phase 4c tests against the new shape.
- **§7.5** Refresh `Docs/07-CLAUDE-CODE-GUIDE.md`
- **§7.6** Optional: `SchemaUpdater` → real EF migrations (probably out of scope — flagged for "Phase 6+")
- **§7.7** *(reserved)*
- **§7.8** OpenRouter retry filter — fixes D2 (5xx/429 fallback)
- **§7.9** SetChecklist JSON `ValueComparer` — fixes D3 (Checklist Insider blocker)

After Phase 5: re-run the full test suite + manual regression checklist. Anything that doesn't pass becomes a Phase 5 follow-up before Phase 6.

### Phase 6 — Roadmap Revamp

With the codebase clean and tested, re-cost every roadmap item against the new reality. The Phase 5 §7.9 fix is the bigger lever here than it looks — Roadmap #1 (Checklist Insider) was estimated at 4-5 weeks assuming `ChecklistLearningService` actually worked; it does not, and the fix is a precondition.

ADRs to write in §8.3 (5 of them — see plan).

---

## Resume notes

When picking back up:

1. **Read this doc + the most recent plan + audit** in that order.
2. **Verify state**: `git log --oneline -5` should show `Merge refactor/phase-4b-core-data-tests`. `dotnet test FlipKit.Core.Tests` should be 245 passed / 1 skipped / 0 failed.
3. **Decide what to do next** — most likely Phase 4c, but two valid alternatives:
   - Push current 11 unpushed commits to `origin/master` first if anyone else needs visibility
   - Jump to Phase 5.9 (SetChecklist ValueComparer fix) since it unblocks Roadmap #1; this would deviate from the "tests before refactors" sequencing rule but is defensible if Checklist Insider work is queued
4. **If continuing Phase 4c**: branch `refactor/phase-4c-desktop-tests`. New `FlipKit.Desktop.Tests` project. Different scaffolding from 4a/4b — needs Avalonia test runner config and CommunityToolkit.Mvvm understanding. Per the cadence pattern, validate with one VM (probably `MainWindowViewModel` — smallest and pure navigation) before batching.

### Open questions — all resolved

The three open questions raised at the original status checkpoint have been answered and acted on:

- **D3 fix queue** — Yes, jumped via Phase 4.5. Roadmap #1 unblocked. Suite now 246/246 with no skips.
- **Push to origin** — Yes, pushed. Branch protection rejected merge commits; rebased to linear history (9 commits ahead of original origin) and pushed cleanly. Going forward: rebase + push after every phase.
- **CHANGELOG backfill** — Skipped per recommendation. Replaced the punch-list note with a git-archaeology pointer (commit `b540c67^` recovers the original `release-notes-v3.x.md` files).
