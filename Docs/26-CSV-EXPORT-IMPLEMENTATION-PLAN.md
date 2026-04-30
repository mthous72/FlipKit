# CSV Export Implementation Plan — Whatnot & eBay Bulk Listings

> **Status:** Planning — not yet implemented
> **Spec source:** [Docs/References/card_listings_export_spec.md](References/card_listings_export_spec.md)
> **Reference data:** [Docs/References/whatnot_values.json](References/whatnot_values.json), [Docs/References/eBay-category-listing-template-Apr-30-2026-16-17-11.csv](References/eBay-category-listing-template-Apr-30-2026-16-17-11.csv)

## 1. Goal

Replace the current single-format Whatnot CSV exporter with a per-platform exporter pair that:

- Emits a spec-compliant Whatnot bulk-import CSV (21 columns, integer prices, exact-match enums, sub-category-aware conditions).
- Emits a spec-compliant eBay Sports Trading Cards "Create new listings" CSV (preserving the template's Info rows verbatim, numeric ConditionIDs, pipe-delimited PicURLs).
- Validates each row pre-flight against `whatnot_values.json` and the eBay template's column constraints, surfacing row-level errors before any file is written.
- Slots into the existing Export page UI without changing the user flow.

## 2. Current state — what works, what's broken

`FlipKit.Core/Services/Implementations/CsvExportService.cs` already exists and is wired through `IExportService` to `ExportViewModel.ExportCsvAsync`. The dispatch path, settings integration, file-save dialog, and post-export `CardStatus.Listed` transition all work and stay.

The implementation itself, however, ships rows that Whatnot rejects:

| Bug | Location | Effect |
|---|---|---|
| Writes 17 columns instead of 21 (no `Hazmat`, `Cost Per Item`, `SKU`) | `CsvExportService.ExportCsvAsync` header section | Whatnot rejects the file |
| Hardcodes `"Sports Cards"` Category, ignoring `Card.WhatnotCategory` | Same method, data row loop | Wrong category for TCG cards |
| Emits `ListingPrice.ToString("F2")` → `"65.00"` | Same method | Whatnot rejects (must be integer) |
| Default `Card.ListingType = "Buy It Now"` (capital `I`) | `Card.cs:66` | Whatnot rejects (`Buy it Now` required) |
| Default `Card.ShippingProfile = "4 oz"` | `Card.cs:68` | Not a valid bucket (`4-7 oz` is) |
| `GetSubcategoryFromSport` returns `"Football Cards"` etc. | `CsvExportService.cs:195` | Valid Whatnot value is `Football Singles` |
| Always emits `"TRUE"`/`"FALSE"` for Offerable | Data row loop | Should be blank for non-`Buy it Now` rows |
| No eBay path — same Whatnot-shaped CSV regardless of `SelectedExportPlatform` | Whole method | eBay export silently broken |

These all get fixed inside the existing `IExportService` contract — no API breakage.

## 3. Architecture

### 3.1 Public contract (unchanged)

`IExportService.ExportCsvAsync(List<Card> cards, string outputPath, ExportPlatform platform)` stays as the single entry point. The ViewModel keeps calling it. What changes is what's behind the dispatcher.

### 3.2 Module layout

```
FlipKit.Core/
├── Services/
│   ├── Interfaces/
│   │   └── IExportService.cs                       [unchanged signature]
│   └── Implementations/
│       ├── CsvExportService.cs                     [becomes a thin dispatcher]
│       └── Export/
│           ├── WhatnotExporter.cs                  [new — writes 21-column CSV]
│           ├── EbayExporter.cs                     [new — preserves template header verbatim]
│           ├── WhatnotValuesProvider.cs            [new — loads + caches whatnot_values.json]
│           ├── EbayTemplateProvider.cs             [new — loads template header verbatim, parses column order]
│           ├── ConditionMapper.cs                  [new — Whatnot fallback chain + eBay ConditionID map]
│           ├── ShippingProfileNormalizer.cs        [new — Card.ShippingProfile → Whatnot bucket; weight-aware eBay shipping]
│           ├── SkuGenerator.cs                     [new — auto-increment, uniqueness-checked]
│           └── ExportValidator.cs                  [new — pre-flight rules from spec §5.4 / §7]
└── Resources/Export/
    ├── whatnot_values.json                          [embedded resource — copied from Docs/References]
    └── ebay_template_header.csv                     [embedded resource — first N lines of eBay template, verbatim]
```

`whatnot_values.json` and `ebay_template_header.csv` ship as embedded resources in `FlipKit.Core` (so Web, Desktop, and Api all get them transparently). They are checked-in copies of the files in `Docs/References/`. Bumping them is a manual step — copy + commit when Whatnot/eBay revs.

### 3.3 Data flow at export time

```
ExportViewModel.ExportCsvAsync (existing)
        │
        ▼
CsvExportService.ExportCsvAsync(cards, path, platform)
        │
        ├── ExportValidator.ValidateAll(cards, platform) ──► row-level errors back to UI
        │
        ▼ (only if validation passes)
   ┌────────────────────────┐
   │  platform switch       │
   └────────────────────────┘
        │                                  │
        ▼ Whatnot                          ▼ eBay
   WhatnotExporter.WriteAsync         EbayExporter.WriteAsync
        │                                  │
        ├─ uses WhatnotValuesProvider      ├─ uses EbayTemplateProvider
        ├─ uses ConditionMapper            ├─ uses ConditionMapper
        ├─ uses ShippingProfileNormalizer  ├─ uses ShippingProfileNormalizer
        └─ uses SkuGenerator               └─ uses SkuGenerator
```

## 4. Card model changes

### 4.1 Add `Sku` column (per user decision #1)

Add `public string? Sku { get; set; }` to `Card`. EF migration: `20260430_AddCardSku` creates the column nullable.

**Auto-increment logic** lives in `SkuGenerator`:
- Default format: `FK-000001`, zero-padded to 6 digits.
- On card creation (or first export if blank), generator queries `ICardRepository` for the highest existing numeric SKU matching `^FK-\d+$`, increments, assigns.
- User may override the SKU in the inventory grid; the generator must check uniqueness against the full `Sku` column before saving an override.
- The generator never reuses a previously-assigned SKU even if the original card was deleted (track via `MAX(numeric_part) + 1`, not `COUNT + 1`).

### 4.2 Extend image URLs to 8 (per user decision #2)

Add `ImageUrl3` … `ImageUrl8` columns to `Card` (matches the existing `ImageUrl1`/`ImageUrl2` pattern — denormalized rather than a child table to keep the model simple and EF migration cheap).

EF migration: `20260430_AddCardImageUrls3to8` adds 6 nullable string columns.

UI: the image upload service and Inventory grid extend their card-image handling to support up to 8. (Not in scope for this plan's first cut — exporters can ship reading 1–8 URLs from `Card`, with the upload UX update tracked as a follow-up.)

### 4.3 Default value normalization (per user decision #3)

**Do not** change `Card.ListingType` and `Card.ShippingProfile` defaults at the model level — that would require a DB migration and risk silently rewriting in-flight data. Instead, **normalize at export time** inside the per-platform exporter:

- `WhatnotExporter` maps:
  - `"Buy It Now"` → `"Buy it Now"`
  - `"4 oz"` → `"4-7 oz"` (and similar legacy weight strings to the closest bucket)
  - Anything still not matching `whatnot_values.json → shipping_profiles` is treated as a custom seller profile and passed through (with a validator warning, not an error).

- `EbayExporter` separately resolves the eBay shipping service from `Card.ShippingProfile` weight (per user decision #3 — "should be verified what the shipping should be on eBay upon export"):
  - Maps the Whatnot-style weight bucket onto an eBay `ShippingService-1:Option` (e.g. `USPSGroundAdvantage`, `USPSFirstClass`) plus a `ShippingService-1:Cost` value.
  - Mapping table lives in `ShippingProfileNormalizer` as a static dictionary; users can override via settings later if needed.
  - Uses `ShippingType=Calculated` only when weight is ambiguous; defaults to `Flat` with the resolved cost.

A separate one-time DB cleanup pass (`NormalizeLegacyDefaults`) can run on app startup to back-fill existing rows — but that's optional polish and not blocking for the export to work.

## 5. Exporter behavior — per-platform specifics

### 5.1 WhatnotExporter

- 21-column header in spec §2.2 order.
- Category from `Card.WhatnotCategory`; subcategory from `Card.WhatnotSubcategory`.
- Type normalized to lowercase-`it` `Buy it Now`.
- Price: `Math.Max(1, (int)Math.Round(card.ListingPrice.GetValueOrDefault()))` — emits as bare integer, no `.00`.
- Hazmat: always `"Not Hazmat"` for cards.
- Offerable: `"TRUE"` only when Type == `"Buy it Now"`; blank otherwise.
- Condition: resolved through `ConditionMapper.MapToWhatnot` (subcategory → category → blank fallback chain against `whatnot_values.json → conditions`). Graded cards prefer `"Graded"` if present in the allowed list.
- SKU: from `Card.Sku`; if blank, `SkuGenerator` assigns one and persists it.
- Image URLs: `ImageUrl1` … `ImageUrl8` placed in their respective columns; blank trailing columns allowed.
- UTF-8 with no BOM.

### 5.2 EbayExporter

- Loads `ebay_template_header.csv` (embedded resource) — copies its bytes verbatim into the output, including any BOM and the `Info,>>>` rows. This is the safest reading of spec gotcha §3.10.6 vs the template eBay actually shipped.
- Parses the column header line once, caches the column-name → index map.
- Every data row's Action is `"Add"` (configurable to `"VerifyAdd"` for testing — exposed via a settings flag).
- Numeric `*ConditionID` via `ConditionMapper.MapToEbay`:
  - Graded cards → `4000`.
  - Raw cards → `5000` (Ungraded) by default; `1000`/`7000` if `Card.Condition` text matches new/damaged tokens.
- For graded cards, also fills:
  - `CD:Professional Grader - (ID: 27501)` — human-readable label (`Professional Sports Authenticator (PSA)` etc.) derived from `Card.GradeCompany`.
  - `CD:Grade - (ID: 27502)` — `Card.GradeValue`.
  - `CDA:Certification Number - (ID: 27503)` — `Card.CertNumber`.
- `*StartPrice` as `card.ListingPrice.Value.ToString("F2", CultureInfo.InvariantCulture)` — decimals OK, no currency symbol.
- `*Quantity` as integer string.
- `*Format` = `"FixedPrice"` (Auctions deferred to future work).
- `*Duration` = `"GTC"` for fixed price.
- `*Location` from `AppSettings.SellerZipCode` (new setting; default empty → validation error).
- `*DispatchTimeMax` from settings (default `2`).
- `*ReturnsAcceptedOption` = `"ReturnsAccepted"`; with `"Days_30"` / `"MoneyBack"` / `"Buyer"` companions.
- `PicURL`: pipe-joined `ImageUrl1..8`, spaces encoded as `%20`, capped at 24.
- `CustomLabel` = `Card.Sku`.
- `C:*` Item Specifics filled from `Card` fields (Sport, Player/Athlete, Year Manufactured, Manufacturer, Set, Card Number, Team, League, Graded, Professional Grader, Grade, Autographed, Parallel/Variety, Features).
- Shipping: `ShippingType` + `ShippingService-1:Option` + `ShippingService-1:Cost` resolved from `ShippingProfileNormalizer` mapping.

## 6. Validation (`ExportValidator`)

Runs once before any file write; collects all errors across all rows; the dispatcher refuses to write if errors > 0.

### Whatnot rules
- Category ∈ `whatnot_values.json → categories`
- Sub Category (if non-empty) ∈ `whatnot_values.json → subcategories[Category]`
- Type ∈ `{Auction, Buy it Now, Giveaway}` (post-normalization)
- Price is integer ≥ 1, no decimal point in the emitted string
- Title length 1..80
- Hazmat ∈ `{Not Hazmat, Hazmat - Standard, Hazmat - Lithium Battery}`
- Condition (if non-empty) ∈ `conditions[subcategory or category]`
- All non-blank Image URL columns start with `https://`
- Shipping Profile ∈ `whatnot_values.json → shipping_profiles` OR flagged as warning (could be custom)

### eBay rules
- `*Title` length 1..80
- `*Format` ∈ `{Auction, FixedPrice}`
- `*Duration` ∈ `{Days_3, Days_5, Days_7, Days_10, Days_30, GTC}`
- `*StartPrice` parses as positive decimal
- `*Quantity` is positive integer
- `*Category` is numeric
- `*ConditionID` is numeric and in the allowed list for the category
- `PicURL` URLs are HTTPS, pipe-delimited, `≤ 24` count, no raw spaces
- `*Location` non-empty
- `*DispatchTimeMax` is positive integer

### Failure presentation
Validator returns `List<RowError> { CardId, PlayerName, Field, Message }`. ViewModel surfaces the first ~3 in `ErrorMessage`; full list optionally written to a sibling `.errors.txt` file alongside the would-be output path.

## 7. UI changes (minimal)

The Export page already has a platform dropdown. No UI restructure needed for the first cut.

Adds:
- A small "Auto-assigned SKU: FK-000123" hint next to cards missing a SKU when the user hovers Export.
- A pre-export error list panel that shows per-row validation errors (replaces the current single-line `ErrorMessage` truncation).

Defers:
- The 8-image-URL upload UX (keeps current 2-image upload; the extra columns just stay blank until that ships).
- Per-card SKU override editor (acceptable for v1: SKU is auto-assigned and visible; manual override comes later).

## 8. Settings additions

In `AppSettings`:

| Setting | Default | Purpose |
|---|---|---|
| `EbaySellerZipCode` | `""` | `*Location` for eBay rows; required at export time |
| `EbayDispatchTimeMax` | `2` | `*DispatchTimeMax` |
| `EbayReturnsAccepted` | `true` | drives `*ReturnsAcceptedOption` block |
| `EbayUseVerifyAdd` | `false` | switches Action column to `VerifyAdd` for test runs |
| `SkuPrefix` | `"FK-"` | prefix for `SkuGenerator` |
| `SkuPadWidth` | `6` | zero-pad width for the numeric portion |

## 9. Testing

- **Snapshot tests** per exporter: 3 fixture `Card` instances (raw sports card, graded sports card, sealed Pokémon). Byte-compare CSV output to checked-in expected fixtures.
- **Validator unit tests**: one positive + one negative case per rule.
- **Round-trip test**: parse generated CSV with `CsvHelper`, confirm field values survive escaping (especially `Pokémon Cards` with é).
- **`whatnot_values.json` shape test**: assert 37 categories, 23 shipping profiles, 153 condition keys load successfully — guards against silent corruption when the file is bumped.
- **eBay template integrity test**: assert the embedded template's first line matches `Info,Version=1.0.0,Template=fx_category_template_EBAY_US`.
- **Manual smoke**: 2-row CSV per platform.
  - Whatnot: upload as draft, inspect.
  - eBay: ship with `EbayUseVerifyAdd=true` for the first batch, switch to `Add` once clean.

## 10. Implementation order (when greenlit)

1. EF migrations — `Sku` column + `ImageUrl3..ImageUrl8` columns.
2. `SkuGenerator` + repository support for next-SKU lookup.
3. `WhatnotValuesProvider` + `EbayTemplateProvider` (+ embedded resources).
4. `ShippingProfileNormalizer` + `ConditionMapper`.
5. `ExportValidator` (whatnot rules first, ebay rules second).
6. `WhatnotExporter` + snapshot test.
7. Refactor `CsvExportService` into the dispatcher shape.
8. `EbayExporter` + snapshot test.
9. ViewModel: row-level error list panel.
10. Settings additions (eBay zip, dispatch time, returns, VerifyAdd toggle, SKU prefix/pad).
11. Manual smoke runs on both platforms.

Whatnot ships first (lower risk, all-or-nothing rejection model), eBay second (higher complexity, real-money insertion fees).

## 11. Open follow-ups (not in scope)

- 8-image-URL upload UX in the Inventory grid.
- Per-card SKU override editor in the Inventory grid.
- One-time `NormalizeLegacyDefaults` migration pass for existing `Card` rows with `"Buy It Now"` / `"4 oz"`.
- Auction format support (`Days_3..Days_30`) for time-boxed eBay listings.
- Auto-fetch latest eBay template on a periodic schedule rather than manual bumps.

---

*Plan locked-in pending approval. Once approved, current branch (`feature/native-distribution`) merges into `master`, then a new `feature/csv-export-overhaul` branch starts the work above.*
