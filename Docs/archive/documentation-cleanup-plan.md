# FlipKit Documentation Cleanup — Full Restructure

> **✅ Completed 2026-05-20 (`fix/docs-cleanup`, PR #30).** This plan is fully executed and is kept here in `archive/` as a historical record (the pre-execution rescan/delta below stays useful). What shipped, in 4 content commits on top of the re-baselined plan:
> - **Commit 1 (move-only):** `Docs/` restructured into `architecture/ features/ guides/ development/ planning/ archive/`; `00-PROGRAM-OVERVIEW.md` deleted; `01-PROJECT-PLAN.md`, `11-UX-DESIGN.md`, `26-CSV-EXPORT-IMPLEMENTATION-PLAN.md`, `27-WEBCAM-CAPTURE-PLAN.md`, and `card_listings_export_spec.md` moved to `archive/`.
> - **Commit 2 (refresh):** version bumps to v3.7.0, brand-drift fixes ("Card Lister" → FlipKit), dead-ref removal, screenshot-placeholder cleanup; HUB + GUI merged into `architecture/overview.md`.
> - **Commit 3 (add):** CardSight feature docs + subscription/quota panel, `Card.AiModelUsed` schema, verified-fields LLM hint mode, `guides/install-linux.md`, and the top-level `Docs/README.md` index.
> - **Commit 4 (cross-cutting):** root `README.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, and link integrity across the tree.
> - **Ximilar** was fully removed in v3.7.0 (PR #31) and scrubbed from active docs; living planning docs annotate it as "removed in v3.7.0" rather than deleting the historical context.
>
> See [planning/roadmap.md](../planning/roadmap.md) Roadmap #0 for the shipped summary.

## Context

The FlipKit `Docs/` folder has accumulated 30+ markdown files over the v1 → v3.6.1 lifecycle, mixing live reference docs, completed planning artifacts, pre-rebrand drafts, and historical phase summaries. Concrete problems found in the inventory pass:

- **Brand drift**: `00-PROGRAM-OVERVIEW.md` (658 lines) and `References/card_listings_export_spec.md` still call the app "Card Lister" — the rebrand to FlipKit landed Feb 2026.
- **Version drift**: `README.md` lists v3.6.0 downloads; `HUB-ARCHITECTURE.md` self-versions as v3.1.0; `CLAUDE.md` build example shows v3.2.0. Current shipping is v3.6.1.
- **Schema/feature gaps**: `02-DATABASE-SCHEMA.md` predates SurpriseSet entity, `RevenueAllocationMethod`, and verified-fields LLM hint mode (commit `223cf95`). `14-VARIATION-VERIFICATION.md` doesn't document the LLM hint mode either.
- **Dead references**: `07-CLAUDE-CODE-GUIDE.md` references `MockScannerService.cs` (deleted in Phase 3 audit). `README.md` promotes Docker (Dockerfile flagged for deletion in `AUDIT-2026-05.md`).
- **Flat numbering hides intent**: Files numbered 00–31 with no folder grouping; readers can't tell architecture from feature spec from completed planning doc.
- **Overlap**: `CLAUDE.md` § Architecture duplicates ~30% of `10-GUI-ARCHITECTURE.md`; `07-CLAUDE-CODE-GUIDE.md` and `CLAUDE.md` both claim "how to work with FlipKit in Claude" with different scope.
- **Screenshot placeholders never filled**: `USER-GUIDE.md` (1250 lines) and `WEB-USER-GUIDE.md` are littered with `📸 SCREENSHOT PLACEHOLDER` markers.
- **Stale `.github/copilot-instructions.md`**: Contains only Azure boilerplate, no FlipKit context.

Goal: a `Docs/` tree that a new contributor (or Claude Code in a future session) can navigate by topic in under a minute, with every active doc reflecting v3.6.1 state and pre-rebrand or completed-phase content moved out of the active set.

> **Branch-state caveat.** This plan was drafted from a snapshot of `feature/surprise-sets-secondary-inventory` on 2026-05-07. Other in-flight branches will land before the doc cleanup runs, and they will change the inventory: new docs may appear, features that this plan flags as "missing from docs" may already be documented elsewhere, and code references that this plan calls out as dead may have been resurrected. **The plan must be re-baselined immediately before execution** (see "Pre-execution rescan" below) — the file-by-file action list in this document is a starting template, not the final spec.

---

## Target Structure

```
Docs/
├── README.md                       (NEW — top-level index, by topic)
├── architecture/
│   ├── overview.md                 (consolidated from HUB-ARCHITECTURE + 10-GUI-ARCHITECTURE)
│   ├── data-access.md              (consolidated from Tailscale-Sync-Architecture)
│   ├── database-schema.md          (refreshed 02-DATABASE-SCHEMA, adds SurpriseSet + verified-fields)
│   ├── checklist-data.md           (16-CHECKLIST-DATA-SPEC, light update)
│   └── adr/                        (5 existing ADRs + README, unchanged)
├── features/
│   ├── ai-scanning.md              (03-OPENROUTER-INTEGRATION, refreshed)
│   ├── verification.md             (14-VARIATION-VERIFICATION + LLM hint mode section)
│   ├── inventory.md                (13-INVENTORY-TRACKING, light update)
│   ├── csv-export.md               (04-WHATNOT-CSV-FORMAT + condensed 26-CSV-EXPORT-PLAN)
│   ├── ebay-integration.md         (09-EBAY-API, light update)
│   ├── image-hosting.md            (06-IMAGE-HOSTING, light update)
│   ├── surprise-sets.md            (31-SURPRISE-SET-DESIGN, current)
│   └── card-terminology.md         (08-CARD-TERMINOLOGY, reference)
├── guides/
│   ├── install-windows.md          (rewrite from 12-INSTALL-GUIDE)
│   ├── install-mac.md              (Mac-Installation-Guide, fix broken `your-repo` URL)
│   ├── install-linux.md            (NEW — slim guide; currently absent)
│   ├── user-guide.md               (USER-GUIDE rewrite — drop screenshot placeholders)
│   ├── web-guide.md                (WEB-USER-GUIDE light update)
│   ├── tailscale-windows.md        (existing, light update)
│   ├── tailscale-mac.md            (existing, light update)
│   ├── tailscale-linux.md          (existing, light update)
│   └── deployment.md               (DEPLOYMENT-GUIDE light update — drop dead Docker section)
├── development/
│   ├── claude-code-guide.md        (07-CLAUDE-CODE-GUIDE rewrite — fix MockScanner ref, dedupe with CLAUDE.md)
│   ├── verification-build.md       (15-VERIFICATION-BUILD-GUIDE, light update)
│   └── regression-checklist.md     (REGRESSION-CHECKLIST, keep as-is)
├── planning/                       (LIVING DOCS — actively maintained)
│   ├── roadmap.md                  (17-FUTURE-ROADMAP)
│   ├── refactor-plan.md            (29-REFACTORING-PLAN)
│   ├── refactor-status.md          (30-REFACTOR-STATUS)
│   ├── audit-2026-05.md            (AUDIT-2026-05)
│   └── integration-roadmap.md      (existing)
└── archive/                        (frozen historical content)
    ├── README.md                   (existing — explicitly "not maintained")
    ├── 01-PROJECT-PLAN.md          (MOVED from active — Feb 2026 build plan, phases shipped)
    ├── 11-UX-DESIGN.md             (MOVED from active — pre-implementation design philosophy)
    ├── 26-CSV-EXPORT-IMPLEMENTATION-PLAN.md  (MOVED — design shipped; useful bits folded into features/csv-export.md)
    ├── card_listings_export_spec.md (MOVED from Docs/References/ — pre-rebrand)
    └── (existing 11 archived files unchanged)
```

**Deletions:**
- `Docs/00-PROGRAM-OVERVIEW.md` — 658 lines of pre-rebrand "Card Lister" content; `README.md` + `guides/user-guide.md` cover current state.
- `Docs/References/` directory once `card_listings_export_spec.md` is moved; the eBay category CSVs stay where the code reads them or move into `Docs/References/` if still referenced (verify during execution).

---

## File-by-File Actions

### Active docs → restructure (move + light update unless noted)

| Current path | New path | Action |
|---|---|---|
| `Docs/02-DATABASE-SCHEMA.md` | `architecture/database-schema.md` | **Rewrite section**: add SurpriseSet, RevenueAllocationMethod, CardStatus, VerificationStatus from `FlipKit.Core/Models/` |
| `Docs/03-OPENROUTER-INTEGRATION.md` | `features/ai-scanning.md` | Light update: confirm consent flow + verified-fields hint mode noted |
| `Docs/04-WHATNOT-CSV-FORMAT.md` | `features/csv-export.md` (merge) | Merge with condensed 26-CSV-EXPORT-PLAN |
| `Docs/06-IMAGE-HOSTING.md` | `features/image-hosting.md` | Light update |
| `Docs/07-CLAUDE-CODE-GUIDE.md` | `development/claude-code-guide.md` | **Rewrite**: remove MockScannerService ref (line 72); dedupe overlap with `CLAUDE.md` |
| `Docs/08-CARD-TERMINOLOGY.md` | `features/card-terminology.md` | Move only |
| `Docs/09-EBAY-API.md` | `features/ebay-integration.md` | Light update |
| `Docs/10-GUI-ARCHITECTURE.md` | `architecture/overview.md` (merge) | **Merge with HUB-ARCHITECTURE.md** into single architecture overview |
| `Docs/12-INSTALL-GUIDE.md` | `guides/install-windows.md` | Rewrite for v3.6.1 |
| `Docs/13-INVENTORY-TRACKING.md` | `features/inventory.md` | Light update |
| `Docs/14-VARIATION-VERIFICATION.md` | `features/verification.md` | **Rewrite section**: add verified-fields LLM hint mode (commit `223cf95`) |
| `Docs/15-VERIFICATION-BUILD-GUIDE.md` | `development/verification-build.md` | Light update |
| `Docs/16-CHECKLIST-DATA-SPEC.md` | `architecture/checklist-data.md` | Light update — note Excel import schema |
| `Docs/17-FUTURE-ROADMAP.md` | `planning/roadmap.md` | Move only (living doc) |
| `Docs/29-REFACTORING-PLAN.md` | `planning/refactor-plan.md` | Move only (living doc) |
| `Docs/30-REFACTOR-STATUS.md` | `planning/refactor-status.md` | Move only (living doc) |
| `Docs/31-SURPRISE-SET-DESIGN.md` | `features/surprise-sets.md` | Move only (current) |
| `Docs/HUB-ARCHITECTURE.md` | merged into `architecture/overview.md` | **Heavy rewrite to v3.6.1**; bump self-version, audit "planned for v3.2/4.0" features against shipped state |
| `Docs/DEPLOYMENT-GUIDE.md` | `guides/deployment.md` | Light update — drop dead Docker section per `AUDIT-2026-05.md` |
| `Docs/USER-GUIDE.md` | `guides/user-guide.md` | **Heavy rewrite**: remove all `📸 SCREENSHOT PLACEHOLDER` markers (either fill or drop), refresh against v3.6.1 features (Surprise Sets, eBay listing, OAuth) |
| `Docs/WEB-USER-GUIDE.md` | `guides/web-guide.md` | Light update — note Settings/bulk-scan parity status |
| `Docs/Mac-Installation-Guide.md` | `guides/install-mac.md` | Light update — fix `your-repo` GitHub placeholder URL |
| `Docs/Tailscale-Setup-Windows.md` | `guides/tailscale-windows.md` | Move only |
| `Docs/Tailscale-Setup-Mac.md` | `guides/tailscale-mac.md` | Move only |
| `Docs/Tailscale-Setup-Linux.md` | `guides/tailscale-linux.md` | Move only |
| `Docs/Tailscale-Sync-Architecture.md` | `architecture/data-access.md` | Move + add Local-vs-Remote mode summary |
| `Docs/REGRESSION-CHECKLIST.md` | `development/regression-checklist.md` | Move only |
| `Docs/AUDIT-2026-05.md` | `planning/audit-2026-05.md` | Move only (living doc) |
| `Docs/integration-roadmap.md` | `planning/integration-roadmap.md` | Move only |
| `Docs/ADR/*` | `architecture/adr/*` | Move folder |

### Active docs → archive (move into `Docs/archive/`)

- `01-PROJECT-PLAN.md` — Feb 2026 build plan; phases shipped.
- `11-UX-DESIGN.md` — Pre-implementation philosophy doc; superseded by live code.
- `26-CSV-EXPORT-IMPLEMENTATION-PLAN.md` — Design shipped; salvage useful spec into `features/csv-export.md`.
- `References/card_listings_export_spec.md` — Pre-rebrand "Card Lister"; useful as reference but isolated.

### Deletions

- `Docs/00-PROGRAM-OVERVIEW.md` — per user direction, delete entirely.

### NEW files

- `Docs/README.md` — top-level index. Topic-based table linking into each subfolder. ~50 lines.
- `Docs/guides/install-linux.md` — slim install guide; currently absent.
- During the cleanup itself, this plan file (`Docs/32-DOCUMENTATION-CLEANUP-PLAN.md`) should be moved to `Docs/planning/documentation-cleanup-plan.md` to match the new structure (or archived once the cleanup ships, depending on whether the rescan/delta material is still useful as a record).

### Root + cross-cutting updates

| File | Action |
|---|---|
| `README.md` (root) | Bump download table from v3.6.0 → v3.6.1; remove Docker mention if AUDIT confirms removal; update Docs/ links to new paths |
| `CLAUDE.md` (root) | Fix v3.2.0 build example → v3.6.1; trim § Architecture overlap with `architecture/overview.md` (link out instead); update `## Planning Documents` table to new paths |
| `CHANGELOG.md` (root) | No structural change; verify no stale entries |
| `.github/copilot-instructions.md` | Replace Azure boilerplate with FlipKit-specific instructions or delete if Copilot isn't in use (verify with user during execution) |

---

## Critical files to read during execution

Reference these to make rewrites accurate (not just rename-and-pray):

- `FlipKit.Core/Models/SurpriseSet.cs`, `Card.cs`, `PriceHistory.cs`, `SetChecklist.cs` — for `architecture/database-schema.md`
- `FlipKit.Core/Services/Implementations/ChecklistVerificationMatcher.cs` — for `features/verification.md` LLM hint section
- `FlipKit.Core/Services/Implementations/OpenRouterScanService.cs` — for `features/ai-scanning.md` (verified-fields hint mode)
- `FlipKit.Desktop/Services/ServerManagementService.cs` — for `architecture/overview.md` (Hub server lifecycle)
- `FlipKit.Core/Helpers/DataAccessModeDetector.cs` — for `architecture/data-access.md`
- `Docs/AUDIT-2026-05.md` — authoritative list of dead code/files to stop documenting
- Recent commits `223cf95` (OCR + verified-fields), `f302074` (icon/single-instance), `b7a7391` (encrypted secrets) — features that may need doc additions

---

## Pre-execution rescan (MANDATORY — run immediately before commit 1)

The plan above is a snapshot. Other side branches will merge between drafting and execution and will invalidate parts of the file-by-file action list. **Do not start the restructure until this rescan completes and the action list has been reconfirmed.**

### Step 1 — Refresh git state

```
git fetch --all --prune
git log --all --since="2026-05-07" --oneline -- Docs/ README.md CLAUDE.md CHANGELOG.md .github/
git branch -a --merged master
git branch -a --no-merged master
```

Goal: identify every commit that touched docs since the inventory date (2026-05-07), and every branch still in flight.

### Step 2 — Diff each in-flight branch against this branch

For every branch listed by `git branch -a --no-merged master` other than the cleanup branch itself:

```
git diff master...<branch> -- Docs/ README.md CLAUDE.md CHANGELOG.md .github/
git diff master...<branch> -- FlipKit.Core/Models/ FlipKit.Core/Services/ FlipKit.Desktop/ViewModels/
```

For each branch, capture:
- **Doc files added/modified/deleted** — these alter the inventory.
- **Code changes that affect documented features** — schema additions, new ViewModels, new services. These may close "missing from docs" gaps or open new ones.
- **Likely merge order and ETA** — if a branch will land before doc cleanup, treat its changes as the new baseline; if it lands after, coordinate to avoid conflicts.

### Step 3 — Re-run the inventory pass

Re-execute the same survey that produced this plan, against current `master` (after expected merges) plus the doc-cleanup branch:

- List every `*.md` under repo root, `Docs/`, `Docs/archive/`, `Docs/ADR/`, `Docs/References/`, `.github/`, `installer/`.
- For each, classify: keep / light-update / heavy-rewrite / move / archive / delete.
- Specifically re-check the freshness signals this plan currently flags:
  - "Card Lister" mentions (pre-rebrand brand)
  - Version strings (`v3\.[0-5]\.`, `v3\.6\.0`)
  - Dead refs (`MockScannerService`, `Dockerfile`, `flipkit-setup.iss`)
  - Schema gaps (SurpriseSet, RevenueAllocationMethod, verified-fields LLM hint) — confirm whether other branches already addressed these
  - Screenshot placeholders in user guides

### Step 4 — Reconfile the final plan

Produce a **delta document** that shows how the file-by-file action list changes vs. the snapshot in this plan. Cover at minimum:

- New active docs added by other branches → slot into the target structure (architecture/features/guides/development/planning).
- Docs that were planned for "heavy rewrite" but have been refreshed by another branch → downgrade to "light update" or "move only."
- Docs that were planned for "move only" but have been deleted by another branch → drop from action list.
- New stale signals that other branches introduced (e.g., a branch that bumps to v3.7.0 might leave older version strings in older docs).
- Any new ADRs that need to land in `architecture/adr/`.

Update this plan file in place with the delta, then proceed to execution. **Do not skip this step** — the cost of re-baselining is one focused inventory pass; the cost of skipping it is merge-conflict hell across 30+ files.

### Step 5 — Coordinate timing

Before the move-commit lands, confirm with the user:
- All doc-touching side branches have either merged or will defer their doc edits until after the cleanup.
- No side branch will rebase onto the cleanup branch's restructure (massive path-rename diffs are painful to merge across).
- Pick a quiet window — the move commit is intentionally git-mv-only so reviewers can verify nothing was lost, but it still rewrites every path under `Docs/`.

---

## Execution order

After the pre-execution rescan reconfirms the action list, restructure as a single feature branch (`fix/docs-cleanup`) in 4 commits to keep diffs reviewable:

1. **Move + delete** — git-mv all files into the new tree, delete `00-PROGRAM-OVERVIEW.md`, archive the four planning docs. No content edits. This commit is purely structural so the diff is clean and reviewers can verify path-only changes via `git log --follow` and `git diff -M`.
2. **Refresh stale content** — version bumps, dead-ref removal, brand checks, screenshot-placeholder cleanup. Touches the heavy-rewrite docs (HUB+GUI merge, USER-GUIDE, schema, verification, claude-code-guide).
3. **Add missing content** — SurpriseSet schema, LLM hint mode, install-linux, new `Docs/README.md` index.
4. **Cross-cutting updates** — root `README.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, broken cross-references.

---

## Verification

End-to-end check after restructure:

1. **Link integrity**: grep the repo for any `Docs/` path references in source, scripts, READMEs, and `.md` files; ensure no broken links.
   ```
   grep -rn "Docs/" --include="*.md" --include="*.cs" --include="*.csproj" --include="*.ps1"
   ```
2. **No stale brand**: grep for "Card Lister" (case-insensitive) — should only appear in `Docs/archive/`.
3. **No stale version**: grep for `v3\.[0-5]\.` and `v3\.6\.0` — only in `CHANGELOG.md` history entries.
4. **No dead refs**: grep for `MockScannerService`, `Dockerfile`, `flipkit-setup.iss` — should not appear in active docs (only `archive/` or `AUDIT-2026-05.md`).
5. **Docs index resolves**: open `Docs/README.md`, click each link, confirm target exists.
6. **CLAUDE.md works**: skim `CLAUDE.md` Planning Documents section — every linked path should exist.
7. **Build still passes**: `dotnet build FlipKit.sln` — moves shouldn't affect build, but confirm no `.csproj` references doc paths (unlikely).
8. **Spot-check rewrites**: open `architecture/database-schema.md` and verify it lists every entity present in `FlipKit.Core/Models/`. Open `features/verification.md` and confirm LLM hint mode is described.

---

## Pre-execution rescan delta — 2026-05-20 (re-baselined against `master` @ v3.7.0)

This section is the mandatory rescan output (Steps 1–4 above), run against `master` after the v3.7.0 release. It supersedes the snapshot wherever they conflict.

### Branch / timing state (Steps 1–2, 5)

- **In-flight branches:** only `fix/docs-cleanup` (this branch, now merged up to `master`) and the stale auto-branch `origin/claude/investigate-surprise-set-LsY0U`. **No pending feature branches.** The plan's central worry — many side branches invalidating the inventory — is effectively resolved.
- **Recommendation:** confirm `claude/investigate-surprise-set-LsY0U` is abandoned and delete it (no doc impact either way). Timing window is clear; safe to proceed to execution after this delta.
- **Doc-affecting merges since the 2026-05-07 snapshot:** `#31` (CardSight + subscription panel + **Ximilar removal**), `#32` (installer build guide + `build-hub-for-installer.ps1 -Version`), `#25` (OCR + verified-fields hint).

### Baseline shift: v3.6.1 → **v3.7.0**

- Current shipping version is **v3.7.0**. Version-drift checks must now flag `v3.6.0` **and** `v3.6.1`/`v3.6.x`, not just `v3.6.0`.
- **Root `README.md`**: the entire download table is `v3.6.0` → bump to `v3.7.0` (note: macOS `.dmg` assets were not built for 3.7.0, so either omit or mark "build on Mac"). The `build-release.ps1` example shows `3.3.6`.
- **Root `CLAUDE.md`**: "Current State: **v3.3.6**" and build examples `v3.2.0` — worse drift than the snapshot noted (it only flagged v3.2.0). Bump to v3.7.0.
- **`HUB-ARCHITECTURE.md`**: 15 version references → heavy rewrite confirmed.

### NEW — Ximilar fully removed; scrub from docs

Ximilar was deleted from all code in v3.7.0 but still appears in **7 docs**:

- **Active (must edit):** `09-EBAY-API.md` (1), `integration-roadmap.md` (1) — remove Ximilar references.
- **Living (note removal):** `29-REFACTORING-PLAN.md` (5), `30-REFACTOR-STATUS.md` (3), `AUDIT-2026-05.md` (3) — annotate as "Ximilar removed in v3.7.0" rather than deleting the historical context.
- **Archive-bound (no action):** `26-CSV-EXPORT-IMPLEMENTATION-PLAN.md`, `References/card_listings_export_spec.md`.
- `features/ai-scanning.md` (from `03-OPENROUTER-INTEGRATION`) must describe **CardSight → OpenRouter** and **not** mention Ximilar.
- **Add `Ximilar` to the dead-ref verification grep** (Verification §4) — should appear only in `archive/` and living planning docs after cleanup.

### NEW — CardSight is undocumented in `Docs/`

Zero CardSight mentions exist anywhere under `Docs/` (only root `README.md` + `CHANGELOG.md`). The restructure must **add** CardSight content (commit 3):

- `features/ai-scanning.md`: CardSight first-pass recognition, confidence tiers, fallthrough to OpenRouter, and the **subscription/quota panel** (`ICardsightSubscriptionService`, `GET /v1/subscription`, 750/mo free tier).
- `architecture/database-schema.md`: add `Card.AiModelUsed` (shipped in `#29`, undocumented) alongside the SurpriseSet/verified-fields additions.
- Consider a new ADR for the CardSight-replaces-Ximilar provider swap (optional).
- Read for accuracy: `FlipKit.Core/Services/Implementations/CardsightScannerService.cs`, `CardsightSubscriptionService.cs`, `Services/ApiModels/CardsightModels.cs`.

### NEW — docs the original action list MISSED (now classified)

| Doc | Disposition |
|---|---|
| `Docs/05-PRICING-RESEARCH.md` | → `features/pricing-research.md` (active feature doc; light update). **Omitted entirely from the snapshot's file-by-file list.** |
| `Docs/27-WEBCAM-CAPTURE-PLAN.md` | → `archive/` — header says **"✅ Shipped 2026-05-04."** Salvage a brief webcam-capture blurb into `guides/user-guide.md` if useful. |
| `Docs/28-CHECKLIST-INSIDER-IMPORT-PLAN.md` | → `planning/checklist-insider-import-plan.md` — **"Planned (not yet started)"**, a living plan. |
| `installer/README.md` | **Leave in place** — now a maintained Windows-installer build guide (from `#32`). Link it from the new `Docs/README.md` index; do not move. |
| `AGENTS.md` (repo root, untracked) | **Out of scope** for the `Docs/` restructure. Separate decision needed: track it as agent guidance (sibling to `CLAUDE.md`) or delete. Flag to user. |

### Brand drift is wider than the snapshot documented

"Card Lister" still appears in active/living docs the snapshot did **not** flag (it only named `00-PROGRAM-OVERVIEW` + `card_listings_export_spec`). Add to commit 2 (refresh) scope:

- `09-EBAY-API.md` (2), **`12-INSTALL-GUIDE.md` (12)**, `14-VARIATION-VERIFICATION.md` (3), `16-CHECKLIST-DATA-SPEC.md` (1), `17-FUTURE-ROADMAP.md` (2), `29/30` (living). `12-INSTALL-GUIDE` is being rewritten anyway; the rest move from "move only" → "move + fix brand".

### Screenshot placeholders

- Only **`USER-GUIDE.md`** still has them (**41** markers) → heavy cleanup confirmed.
- **`WEB-USER-GUIDE.md` is now clean (0)** → downgrade from "littered with placeholders" to plain light update.

### Corrections to the snapshot

- The "Critical files to read" list cites `OpenRouterScanService.cs`; the actual file is **`OpenRouterScannerService.cs`**.
- `02-DATABASE-SCHEMA` rewrite must also add `Card.AiModelUsed`; verify the full entity set against `FlipKit.Core/Models/` (SurpriseSet is shipped).
- This plan file (`32-…`) → move to **`planning/documentation-cleanup-plan.md`** (keep — the rescan/delta record stays useful), not archived.

### Verdict

Inventory is re-baselined. The 4-commit execution order still holds; the deltas above are folded into each commit (esp. commit 2 = wider brand/version/Ximilar scrub, commit 3 = add CardSight + the three newly-classified docs). **Cleared to proceed to execution.**
