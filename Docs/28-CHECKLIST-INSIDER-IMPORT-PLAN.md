# Checklist Insider Excel Import — Future Feature Plan

**Status:** Planned (not yet started)
**Priority:** High (#1 on roadmap)
**Effort:** Medium-High (3-4 weeks for full surface set)
**Created:** 2026-05-01
**Last Updated:** 2026-05-02
**Related Docs:** [16-CHECKLIST-DATA-SPEC.md](16-CHECKLIST-DATA-SPEC.md), [17-FUTURE-ROADMAP.md](17-FUTURE-ROADMAP.md), [14-VARIATION-VERIFICATION.md](14-VARIATION-VERIFICATION.md)

---

## 1. Problem

Today FlipKit's `SetChecklist` table is seeded by hand and only covers a small set of products (see [16-CHECKLIST-DATA-SPEC.md](16-CHECKLIST-DATA-SPEC.md) Tier 1). The variation verification flow ([14-VARIATION-VERIFICATION.md](14-VARIATION-VERIFICATION.md)) silently no-ops whenever a user scans a card from a set we haven't pre-seeded — which is most modern releases.

We need a path to bulk-populate `SetChecklist` for any product the user actually owns, without:

- Scraping commercial sites (Beckett, TCDB) — both forbid commercial reproduction in their ToU.
- Licensing paid feeds (SportsCardsPro, CardHedger) until usage justifies the cost.
- Pinging any external service repeatedly from FlipKit clients (rate-limit / blocking risk).

## 2. Approach: User-driven Excel import

[Checklist Insider](https://www.checklistinsider.com/) publishes per-set Excel checklists for nearly every modern sports release, hosted on their AWS CloudFront CDN. Their ToU forbids commercial scraping or mirroring, but **grants individual users a personal-use license to download their files**.

The clean path: **FlipKit never touches their site.** The user downloads the .xlsx themselves under their personal license, then imports it into FlipKit via a file picker. FlipKit only ships an Excel parser and a UI — same legal posture as any app that opens a file the user already has.

### Why this works legally

- No automation against checklistinsider.com from FlipKit clients or build pipeline.
- User obtains the file under the personal-use license already granted by their ToU.
- FlipKit ships parsing code, not data — the `SetChecklist` rows are derived from the user's own legitimately-acquired file.
- No redistribution: imported checklists stay in the user's local SQLite DB.

## 3. UX overview — three surfaces

The checklist isn't just a Settings concern; it's a scan-accuracy tool. The same parser powers three entry points so users encounter it at the moment it's relevant:

- **Surface A — Settings → Checklists → Import** *(primary home)*. Canonical entry point. For "I just bought a stack of 2026 Bowman, let me prep before scanning."
- **Surface B — Contextual hint inside Scan / BulkScan results** *(highest-leverage win)*. When the AI returns a set we don't have a checklist for, a banner appears in the verification step with a one-click path to import. Catches users at the exact moment the gap matters.
- **Surface C — Pre-scan "set lock"** *(real accuracy lever)*. Optional dropdown on the Scan page: *"I'm scanning from [set]"*. If set, the AI prompt is constrained with that set's known card numbers + parallels, dramatically reducing hallucinations. If we don't have the checklist, the import banner surfaces *before* the scan.
- **Surface D — Settings → "Find missing checklists"** *(catch-up tool)*. Audits the existing inventory, lists every distinct (Year, Brand, Set) combo the user has cards in but no checklist for, with one-click "Get checklist" per row. Lets users retroactively bring their database up to spec without scanning again.

All four surfaces share the same Core import service and preview UI.

### 3a. Settings → Import flow (Surface A)

Reachable from Settings → Checklists → "Import from Excel..." in both Desktop and Web.

1. **"Browse Checklist Insider"** button — opens `https://www.checklistinsider.com/` in the user's default browser. Helper text: *"Find the set you scanned, click the Excel download link, save the file, then come back here."*
2. **"Import Excel File"** button — file picker (Desktop) or multipart upload (Web). Accepts `.xlsx`.
3. **Preview pane** — shows parsed result before commit:
   - Detected metadata (Year, Sport, Manufacturer, Brand, SetName) — editable
   - Total cards, subset count, subset names
   - First ~20 rows for sanity check
   - Warnings for unparsed rows
4. **Commit** button — writes `SetChecklist` + child `ChecklistCard` rows. Confirmation: *"Imported 1,285 cards into 2026 Bowman Baseball across 24 subsets."*
5. **Cancel** — discards parse, no DB changes.

### 3b. On-screen help (applies to every surface)

Discoverability is half the value of this feature — users will only import if they understand *why* and *where to get the file*. Every surface that prompts an import must show:

- A short *what + why* line: *"Import a Checklist Insider .xlsx to verify scans against the real set list and unlock parallel/insert detection."*
- A **"How does this work?"** info icon (`(?)` button) that opens a small panel explaining:
  1. Go to checklistinsider.com (free account, personal-use download)
  2. Search for the set, click the Excel download link
  3. Come back here, click "Import Excel File"
  4. Review the preview, fix any wrong metadata, commit
- An *open-in-browser* deeplink button that pre-fills a search for the relevant set when one is already known (Surfaces B/C/D pass a query string like `?s=2026+Bowman+Baseball`).
- A **"Don't have a file yet? Browse Checklist Insider"** button at the top of the import view — never let a user hit a dead end.

The same help content is also reachable from a permanent "Help: Checklists" link in Settings, so users can read it without entering an import flow.

## 4. Excel file structure (confirmed)

Sample analyzed: 2026 Bowman Baseball checklist xlsx (~45 KB, 3 sheets, only Sheet1 populated, ~1,285 rows).

- 1 active sheet, **no header row**.
- Columns: `A=Card #`, `B=Player Name`, `C=Team`, `D=optional flag` (e.g. `"Rookie"`).
- Subsets/parallels/inserts/autos are **NOT in separate sheets**. They are delimited inline by all-caps single-cell rows in column A:
  - `BASE CARDS`, `INSERT`, `CHROME PROSPECTS`, `BOWMAN SCOUTS TOP 100`, `AUTOGRAPH`, `CHROME PROSPECTS AUTOGRAPH VARIATION`, `CHROME ROOKIE AUTOGRAPH`, etc.
- Files are generated by Investintech's Able2Extract Engine (PDF→Excel converter), so structure is consistent-ish across releases but not bulletproof.
- **Print runs, parallel numbering (/199, /99, etc.), and autograph signers are NOT in the xlsx** — that data lives in the separate PDF "odds" sheet. Phase 2 territory.
- Coverage skews baseball-strong; basketball/football releases sometimes have PDF-only with no xlsx.

## 5. Architecture

```
FlipKit.Core/Services/
  Interfaces/
    IChecklistImportService.cs         ← parse-then-preview-then-commit API
    IChecklistAuditService.cs          ← missing-checklists query + re-verify pass (Surface D)
    IScanPromptAugmenter.cs            ← builds set-locked prompt prefix (Surface C)
  Implementations/
    ExcelChecklistImporter.cs          ← ClosedXML, subset-header parsing
    ChecklistFileMetadataExtractor.cs  ← guess year/brand/sport from filename + first rows
    ChecklistAuditService.cs           ← LEFT JOIN query, returns MissingChecklistRow list
    ChecklistVerificationMatcher.cs    ← fuzzy match scanned card → ChecklistCard (Surface B)
  ApiModels/
    ChecklistImportPreview.cs          ← DTO returned to UI before commit
    MissingChecklistRow.cs             ← (Year, Brand, SetName, CardCount, LastScanned)

FlipKit.Desktop/Views/
  ImportChecklistView.axaml            ← file picker + preview + commit (Surface A)
  MissingChecklistsView.axaml          ← audit table + per-row "Get Checklist" (Surface D)
FlipKit.Desktop/ViewModels/
  ImportChecklistViewModel.cs          ← [ObservableProperty] state, [RelayCommand] actions
  MissingChecklistsViewModel.cs        ← audit query results, re-verify command
  ScanViewModel.cs (modified)          ← LockedSetId property, set-lock dropdown binding (Surface C)
  BulkScanViewModel.cs (modified)      ← shared set-lock; aggregate "missing" banner (Surface B/D)

FlipKit.Web/Controllers/
  ChecklistImportController.cs         ← multipart upload, calls Core service
  ChecklistAuditController.cs          ← missing-checklists table + re-verify endpoint
FlipKit.Web/Views/Checklist/
  Import.cshtml                        ← Razor view, Bootstrap form (Surface A)
  Missing.cshtml                       ← Razor table view (Surface D)
  _ChecklistHelpPanel.cshtml           ← shared partial — same help content as Desktop §3b
```

### Library choice

**ClosedXML** — MIT-licensed, the de-facto .NET 8 OOXML reader. Safe for FlipKit's commercial distribution.
**Not EPPlus** — switched to a paid commercial license at v5+; would require a per-seat fee.
**Not NPOI** — Apache 2.0 and works, but the HSSF/XSSF API is clunky compared to ClosedXML's `IXLWorksheet.RowsUsed()`.

## 6. Parser algorithm

1. Open `.xlsx` with `ClosedXML.Excel.XLWorkbook`.
2. Take the first non-empty worksheet (Bowman sample has 3 sheets, only #1 populated).
3. Walk `worksheet.RowsUsed()` in order. Maintain `currentSubset = "BASE"` as state.
4. For each row:
   - Read cells A, B, C, D as strings (trim, null-safe).
   - **Section header detection:** if A is non-empty AND B/C/D are all empty AND A is uppercase letters/spaces (regex `^[A-Z][A-Z0-9 /\-]+$`), set `currentSubset = A`. Skip the row.
   - **Card row:** if A and B are both non-empty, append `ChecklistCard { CardNumber=A, PlayerName=B, Team=C, IsRookie = D contains "RC"|"Rookie", Subset = currentSubset }`.
   - **Skip:** rows where everything is empty (gap rows), or A starts with disclaimer text (e.g. *"Subject to change"*, *"Checklists provided by Topps"*).
5. Derive boolean flags from subset name:
   - `IsAutograph = currentSubset.Contains("AUTOGRAPH", IgnoreCase)`
   - `IsParallel = currentSubset.Contains("VARIATION") || currentSubset.Contains("PARALLEL")`
   - `IsInsert = !IsBase && !IsAutograph && !IsParallel`
6. Collect warnings for any row that didn't match the header or card patterns — surface in preview UI.

## 7. Schema additions

[16-CHECKLIST-DATA-SPEC.md](16-CHECKLIST-DATA-SPEC.md) defines `ChecklistCard` as a JSON sub-object with `card_number`, `player_name`, `team`, `is_rookie`, `subset`. The new fields needed for verification + variation flagging:

```csharp
public class ChecklistCard
{
    public string CardNumber { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string? Team { get; set; }
    public bool IsRookie { get; set; }
    public string Subset { get; set; } = "BASE";   // already in spec
    public bool IsAutograph { get; set; }          // NEW
    public bool IsParallel { get; set; }           // NEW
    public bool IsInsert { get; set; }             // NEW
}
```

`SetChecklist` gets one new field for provenance:

```csharp
public string? DataSource { get; set; }   // "checklist-insider" | "manual" | "manufacturer-pdf"
public DateTime? ImportedAt { get; set; }
```

If `Cards` is still stored as a JSON blob (per current spec), no migration needed beyond seeding new fields with defaults. If we move to a relational `ChecklistCard` table during this work, that's a separate migration.

## 8. Integration with scan flow (Surfaces B + C)

### 8a. Pre-scan "set lock" (Surface C — accuracy lever)

A new optional control at the top of the Scan page (and BulkScan page) labeled **"Scanning from a known set?"** with a searchable dropdown of imported `SetChecklist` rows.

- **Default:** unset. Single-card scans don't need it; the dropdown stays out of the way.
- **When set:** sticky for the remainder of the session. A small chip shows *"Locked: 2026 Bowman Baseball [×]"*.
- If the dropdown is opened and the user types a set name we don't have, an inline option appears: *"Don't see your set? [Import a checklist →]"* — opens Surface A pre-loaded with the typed set name.

When set lock is active, the AI prompt sent to OpenRouter is augmented:

```
This card is from 2026 Bowman Baseball.
Card numbers in this set: 1-150 (base), BCP1-BCP100 (Chrome Prospects), ...
Known parallels for this set: Refractor, X-Fractor, Gold /50, Orange /25, Red /5, SuperFractor 1/1
Known autograph subsets: Chrome Prospect Autographs, Chrome Rookie Autographs, ...
Choose a CardNumber strictly from the list above. If the visible card number does not appear in the list, return null and explain in the notes field.
```

This single change is the highest-impact accuracy win in the entire feature. Card-number hallucinations and parallel mis-IDs drop sharply when the model isn't free-associating from a generic prompt.

**Implementation note:** the prompt augmentation lives in `OpenRouterScannerService` and only fires when the caller passes a `lockedSetId`. Everything below the prompt layer (response parsing, MapToCard, etc.) is unchanged.

### 8b. Post-scan verification badge + import hint (Surface B — discoverability)

After [OpenRouterScannerService.cs:414](../FlipKit.Core/Services/Implementations/OpenRouterScannerService.cs#L414) — the `MapToCard` step — runs a follow-up:

1. Look up `SetChecklist` by `(Year, Sport, Manufacturer, Brand, SetName)`.
2. **If found:** fuzzy-match the scanned `CardNumber` + `PlayerName` against children. Three possible outcomes:
   - Match → `✓ Verified against checklist` green badge.
   - No card-number match → `⚠ Card #{n} not found in this set's checklist` amber badge, with a "Pick from checklist" picker for the user to correct.
   - Player-name mismatch on a found card # → `⚠ Player mismatch — checklist says {name}` amber badge.
3. **If not found:** surface a yellow banner inside the scan-result card:
   > **No checklist imported for 2026 Bowman Baseball** — accuracy will be limited.
   > [Get Checklist for this set →] [Why?]
   - **"Get Checklist for this set"** opens `https://www.checklistinsider.com/?s=2026+Bowman+Baseball` in the browser AND opens Surface A with metadata pre-filled, ready for the file picker the moment the user finishes downloading.
   - **"Why?"** opens the same shared help panel from §3b.

### 8c. BulkScan considerations

Surface B (the post-scan hint) is *especially* valuable in BulkScan: a single missing checklist hit by 30 cards in a row is exactly the wrong moment to interrupt the workflow. Two adjustments:

- **Aggregate banner above the scan grid:** *"3 of these 30 cards belong to sets without checklists — [Review missing →]"* — opens Surface D scoped to just this batch.
- **Per-card badge stays:** but the inline "Get Checklist" link is muted (link-style, not button) so it doesn't pull focus from cards that scanned cleanly.

Surface C (set lock) is even more useful in BulkScan: typical use case is "I'm scanning a stack of one set" — locking once saves 30 prompts' worth of accuracy.

## 9. Settings → "Find missing checklists" audit (Surface D)

A retrospective tool: most users will accumulate scanned cards from sets they never imported a checklist for. This surface lets them clean that up in one pass instead of one card at a time.

### 9a. UX

Settings → Checklists → **"Find missing checklists"** button. Opens a `MissingChecklistsView` showing a sortable list of every distinct `(Year, Brand, SetName)` combination present in `Cards` that has no matching `SetChecklist` row.

Columns:
- Year · Brand · Set Name
- # of cards in inventory from that set
- Last scanned date
- Action: **[Get Checklist]** button per row

Clicking **[Get Checklist]** for a row:
1. Opens `https://www.checklistinsider.com/?s={Year}+{Brand}+{SetName}` in the browser.
2. Opens Surface A (the import view) with metadata pre-filled from that row, so the moment the user finishes the download they can hit "Import Excel File" and commit.

A summary line at the top: *"You have 247 cards across 12 sets without imported checklists. Importing them unlocks variation verification for those cards."*

A **"Re-verify all cards"** button (after at least one import) re-runs the verification matcher against all `Cards` and updates each card's `ChecklistVerified` status. Without this, imported checklists only affect future scans — existing cards stay un-verified.

### 9b. Query

```sql
SELECT
  c.Year, c.Brand, c.SetName,
  COUNT(*) AS CardCount,
  MAX(c.CreatedAt) AS LastScanned
FROM Cards c
LEFT JOIN SetChecklists sc
  ON sc.Year = c.Year
 AND sc.Brand = c.Brand
 AND sc.SetName = c.SetName
WHERE sc.Id IS NULL
  AND c.Year IS NOT NULL
  AND c.Brand IS NOT NULL
  AND c.SetName IS NOT NULL
GROUP BY c.Year, c.Brand, c.SetName
ORDER BY CardCount DESC, c.Year DESC;
```

The implementation lives in a new `IChecklistAuditService` in `FlipKit.Core` so both Desktop and Web can call it. Same service powers the BulkScan "Review missing" link from §8c.

### 9c. Web parity

Surface D ships on Web at `/Settings/Checklists/Missing` with an identical table. The Web "Get Checklist" button opens checklistinsider.com in a new tab; the import flow is the existing Web upload form pre-filled via query string.

## 10. Build order & phasing

Phase the work so each phase is shippable on its own — stops half-done work from blocking releases, and lets users start benefiting from imports before the full surface set lands.

**Phase 1 — Foundation + Surface A (Settings home).** Core parser, schema migration, Settings import view. End of phase: power users can manually import any xlsx and benefit from variation verification on future scans.

1. **Schema** — add `IsAutograph` / `IsParallel` / `IsInsert` to `ChecklistCard`, `DataSource` / `ImportedAt` to `SetChecklist`. Migration via `dotnet ef migrations add ChecklistImportFields`.
2. **ClosedXML reference** — add NuGet package to `FlipKit.Core`.
3. **Core parser** — `ExcelChecklistImporter` + `ChecklistFileMetadataExtractor`. Unit tests against the sample 2026 Bowman xlsx (commit fixture to `tests/fixtures/checklists/`).
4. **Preview model** — `ChecklistImportPreview` DTO with editable metadata + parsed cards + warnings.
5. **Desktop UI** — `ImportChecklistView` + `ImportChecklistViewModel` with shared help panel from §3b. File picker via existing `IFileDialogService`.
6. **Web UI** — `ChecklistImportController` + Razor view, multipart upload, same help panel content.

**Phase 2 — Surface B (post-scan hint).** Adds the verification badge and import hint inside scan results. End of phase: users naturally discover the import feature as they hit gaps.

7. **Verification matcher** — fuzzy match scanned card → checklist children. Three-state badge (verified / not-found / mismatch).
8. **Scan-result banner** — "No checklist imported for X" yellow banner with deeplinked import button.
9. **BulkScan aggregate banner** — "3 of 30 cards belong to sets without checklists" link to Surface D.

**Phase 3 — Surface C (pre-scan set lock).** The accuracy multiplier. End of phase: power users unlock the prompt-constraining accuracy boost.

10. **Set-lock dropdown** in `ScanView` and `BulkScanView` (sticky session state).
11. **Prompt augmentation** in `OpenRouterScannerService` when `lockedSetId` is passed — emit known card numbers, parallels, autograph subsets.
12. **"Don't see your set?"** inline import path in the dropdown.

**Phase 4 — Surface D (missing-checklist audit).** Retroactive cleanup. End of phase: existing inventory can be brought up to date in one pass.

13. `IChecklistAuditService` in `FlipKit.Core` with the §9b query.
14. **Desktop:** Settings → Checklists → "Find missing checklists" view.
15. **Web:** `/Settings/Checklists/Missing` view.
16. **"Re-verify all cards"** action — bulk matcher pass over the `Cards` table to refresh verification status.

## 11. Risks & open questions

1. **Subset classification heuristic is fragile.** The all-caps header detection will misclassify edge cases (e.g. a card titled `"AARON JUDGE"` could look like a header). Mitigation: require B/C/D to all be empty for header classification, and surface unclassified rows in the preview for the user to review.
2. **Phase 1 doesn't capture parallels/print runs.** xlsx files lack /99, /50, etc. data. That lives in the PDF odds sheet. Phase 2 = add a PDF importer (`PdfPig`, MIT-licensed) using the same user-driven file-picker model — same legal posture.
3. **Some sets have no xlsx, only PDF.** Basketball/football releases skew PDF-only. Until phase 2 ships, those sets won't import via this feature. The "Get Checklist" deeplink should still work; users will just see "No xlsx available" on the post.
4. **Filename-based metadata guessing.** Filenames like `2026-Bowman-Baseball-Checklist-Downloads-Excel-spreadsheet.xlsx` parse cleanly, but `-SUBJECT-TO-CHANGE` suffix and casing variants (`Excel-spreadsheet` vs `Excel-Spreadsheet`) exist. The preview UI must let users correct any wrong guess before commit.
5. **Conflict with existing seeded data.** If a user imports a set we already shipped, what wins? Default = imported version replaces shipped version, with a confirmation dialog showing diff counts ("Replace shipped checklist with imported one? (1,285 → 1,290 cards)").
6. **Should imports sync across devices?** If FlipKit ever adds cloud sync ([17-FUTURE-ROADMAP.md](17-FUTURE-ROADMAP.md) §5), imported checklists should be part of the synced data — they're user content, not app content.

## 12. Future phases (post-v1)

- **PDF odds-sheet importer** to capture parallels, print runs, and autograph signer details. Same user-driven file picker, `PdfPig` for parsing.
- **Multi-file batch import** — drag-and-drop a folder of xlsx files; FlipKit parses all and shows a combined preview.
- **Re-import / update flow** — checklist files get updated when print runs are confirmed; allow non-destructive merge that preserves user-added scan associations.
- **Manufacturer PDF support** — Topps/Panini/Upper Deck dealer-kit PDFs are the upstream source for Checklist Insider anyway. If FlipKit gets contact-level access to manufacturer dealer portals, this becomes the most authoritative path with no third-party legal posture at all.

## 13. Out of scope for this feature

- Automated downloads from Checklist Insider — explicitly NOT doing this.
- Pricing data ingestion — Checklist Insider doesn't ship pricing in the xlsx; that's eBay Browse / 130point territory (see [09-EBAY-API.md](09-EBAY-API.md)).
- A bundled seed DB built from Checklist Insider — also out, for the same ToU reason. Bundled seeds remain manually-curated per [16-CHECKLIST-DATA-SPEC.md](16-CHECKLIST-DATA-SPEC.md).

---

**Decision log:**

- 2026-05-02 — Expanded scope from a single Settings import view to four surfaces: (A) Settings home, (B) post-scan contextual hint with verification badge, (C) optional pre-scan set-lock that constrains the AI prompt, (D) Settings → "Find missing checklists" audit over existing inventory. Phased the build so each surface ships independently. Added a shared on-screen help panel (§3b) reachable from every surface — discoverability is half the value of this feature. Effort estimate revised from 2-3 weeks to 3-4 weeks.
- 2026-05-01 — Researched Beckett (commercial scraping forbidden), TCDB (commercial scraping forbidden, no API), eBay Browse aspect refinements (viable but limited to active-listing aspects, not authoritative checklists), and Checklist Insider (xlsx files exist; user-driven import is the legally-clean path). Settled on user-driven Excel import as the v1 strategy.
