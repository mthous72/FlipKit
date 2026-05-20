# Database Schema

## Overview

FlipKit uses a single SQLite database (`%LOCALAPPDATA%\FlipKit\cards.db`) in
**WAL mode** so Desktop, Web, and API can read concurrently. The schema is
defined by EF Core entities in `FlipKit.Core/Models/` and the
`FlipKitDbContext` in `FlipKit.Core/Data/`. Enums are stored as **strings**,
money fields as **decimal**, and dates as **DateTime** (UTC).

> Fresh installs are created from EF migrations; existing user databases are
> upgraded additively by `SchemaUpdater` on launch (see ADR-003). When you add a
> column, update both.

This document mirrors the C# entities. The authoritative source is always the
model classes — read them directly if in doubt.

---

## Entities

| Entity | File | Purpose |
|---|---|---|
| `Card` | `Models/Card.cs` | Core inventory row — identity, variation, pricing, sale, listing, verification, surprise-set linkage. |
| `PriceHistory` | `Models/PriceHistory.cs` | Per-card price-change log. |
| `SetChecklist` | `Models/SetChecklist.cs` | Imported set checklist (cards stored as a JSON blob). Drives verification. |
| `SurpriseSet` | `Models/SurpriseSet.cs` | Whatnot "surprise set" mystery-lot grouping. |
| `ModelScanRecord` | `Models/ModelScanRecord.cs` | Per-model scan accuracy scoreboard records. |

---

## Card

The central entity. Field groups (C# property names; columns are the
EF-generated snake/Pascal mapping):

### Identity
- `PlayerName` (string, required), `CardNumber`, `Year` (int?),
  `Sport` (`Sport` enum, stored as string).

### Manufacturer / set
- `Manufacturer`, `Brand`, `SetName`, `Team`.

### Variation / parallel
- `VariationType` (default `"Base"`), `ParallelName`, `SerialNumbered`
  (e.g. `"/99"`, `"1/1"`), `IsShortPrint`, `IsSSP`.

### Special attributes
- `IsRookie`, `IsAuto`, `IsRelic`.

### Condition / grading
- `Condition` (default `"Near Mint"`), `IsGraded`, `GradeCompany`,
  `GradeValue`, `CertNumber`, `AutoGrade`.

### Acquisition / cost basis
- `CostBasis` (decimal?), `CostSource` (`CostSource` enum?), `CostDate`,
  `CostNotes`.

### Pricing
- `EstimatedValue`, `PriceSource`, `PriceDate`, `ListingPrice`,
  `PriceCheckCount`.

### Sale information
- `SalePrice`, `SaleDate`, `SalePlatform`, `FeesPaid`, `ShippingCost`,
  `NetProfit`.

### Listing settings
- `Quantity` (default 1), `ListingType` (default `"Buy It Now"`),
  `Offerable` (default true), `ShippingProfile` (default `"4 oz"`).

### Images
- Eight front/back/extra slots: `ImagePathFront`, `ImagePathBack`,
  `ImagePath3`–`ImagePath8` (local paths) and `ImageUrl1`–`ImageUrl8`
  (public ImgBB URLs). Slots 1 (front) and 2 (back) are captured at scan time
  and sent to the AI for identification; slots 3–8 are user-attached extras
  (condition shots, slab close-ups) uploaded to ImgBB but never sent to the LLM.

### Export & marketplace linkage
- `Sku` (export identifier).
- `EbayItemId` — eBay listing ID for cards imported from a Seller Hub CSV; used
  as the upsert key so re-importing the same listing updates the existing row.
- `ListedAt` (DateTime?).

### Whatnot-specific
- `WhatnotCategory` (default `"Sports Cards"`), `WhatnotSubcategory`.

### Checklist verification (Phase 2 — Checklist Insider)
- `VerificationStatus` (`VerificationStatus` enum, default `NotChecked`) — the
  tier outcome the user accepted on save; drives the editor badge.
- `MatchedChecklistKey` — composite re-find key
  `"{setChecklistId}:{normalizedCardNumber}:{subsetLower}"`. (ChecklistCard rows
  live inside a JSON blob on `SetChecklist`, so a true FK isn't possible without
  a relational migration; this string is enough to look the matched row back up.)

### Surprise set linkage
- `SurpriseSetId` (int?), `SurpriseSetSlot` (int?, 1-based position in the set's
  checklist), `SurpriseSet` (navigation).

### Status / metadata
- `Status` (`CardStatus` enum, default `Draft`).
- `DataSource` (`CardDataSource` enum, default `None`).
- `AiModelUsed` (string?) — the scan provider/model id stamped at save time when
  `DataSource == Ai`. For LLM scans this is the OpenRouter model id; for a
  CardSight match it is `"cardsight"`. Drives user-correction attribution in the
  model-accuracy scoreboard.
- `Notes`, `CreatedAt`, `UpdatedAt`.

### Navigation
- `PriceHistories` (`ICollection<PriceHistory>`).

---

## PriceHistory

Tracks price changes over time.

- `Id`, `CardId` (FK → Card, cascade delete), `EstimatedValue`, `ListingPrice`,
  `PriceSource`, `Notes`, `RecordedAt`.

---

## SurpriseSet

A Whatnot mystery-lot grouping: multiple cards sold under one shared listing,
with revenue allocated back to the constituent cards on completion.

### Identity
- `Name` (required), `ShowName`, `Notes`.

### Lifecycle
- `State` (`SurpriseSetState` enum, default `Draft`).
- Timestamps: `CreatedAt`, `UpdatedAt`, `ExportedAt`, `LiveAt`, `CompletedAt`,
  `CancelledAt`.

### Shared listing fields
Stamped onto every CSV row at export time (Whatnot's consistency rule is enforced
by construction): `Title`, `SharedListingType` (default `"Buy it Now"`),
`SpotPrice`, `SharedCondition`, `SharedShippingProfile`, `SharedWhatnotCategory`
(default `"Sports Trading Cards"`), `SharedWhatnotSubcategory`, `Offerable`
(default false), and gallery images `SharedImageUrl1`–`SharedImageUrl8`.

### Economics
- `AllocationMethod` (`RevenueAllocationMethod` enum, default `EqualSplit`).
- `LotCostBasis` (decimal?) — optional total cost paid for all cards as a lot,
  auto-split evenly (CostSource = LotSplit); per-card overrides survive re-balance.
- Completion fields: `SpotsSold`, `GrossRevenue`, `TotalFees`, `TotalShipping`.

### Navigation
- `Cards` (`ICollection<Card>`).

---

## SetChecklist

Imported set checklist used by the verification matcher. The `Cards` list and
`KnownVariations` list are stored as JSON-converted columns (with a
`ValueComparer` so EF detects collection mutations — see the claude-code-guide
gotcha). Keyed on `(Manufacturer, Brand, Year, Sport)`.

---

## Enums

All stored as strings.

### CardStatus (`Models/Enums/CardStatus.cs`)
`Draft`, `Priced`, `Ready`, `Listed`, `Sold`, `ReservedForSet` (locked into a
SurpriseSet; excluded from individual listing flows), `SoldInSet` (sold as part
of a completed SurpriseSet; included in revenue reports).

### VerificationStatus (`Models/Enums/VerificationStatus.cs`)
`NotChecked` (default; never run against a checklist), `Verified` (Tier 1 —
exact match, all field confidences high, saved as-is), `BestGuess` (Tier 2 —
card # + player matched but a field was uncertain), `UserCorrected` (user picked
a different ChecklistCard than the matcher's top candidate), `NoMatchFound`
(Tier 3 — saved with the AI guess only).

### RevenueAllocationMethod (`Models/Enums/RevenueAllocationMethod.cs`)
`EqualSplit`, `CostWeighted`, `Manual`.

### CardDataSource (`Models/Enums/CardDataSource.cs`)
How the card's data was produced (`None`, `Ai`, manual, etc.) — pairs with
`Card.AiModelUsed` for scoreboard attribution.

### SurpriseSetState (`Models/Enums/SurpriseSetState.cs`)
Lifecycle states for a SurpriseSet (`Draft`, … `Completed`, `Cancelled`).

### Other enums
`Sport`, `CostSource`, `ExportPlatform`, `ScanDepth`, `ScanMode`, `ScanOutcome`,
`RateLimitScope`, `CardsightConfidenceTier`, `VerificationConfidence` — see
`FlipKit.Core/Models/Enums/`.

---

## Status flow

```
Draft → Priced → Ready → Listed → Sold
  ↑        ↑       │        │
  └────────┴───────┘        └──→ (archived in reports)
       (can revert)

ReservedForSet ──→ SoldInSet   (SurpriseSet path)
```

- **Draft** — just scanned, missing price/other data.
- **Priced** — has a listing price, may still need images uploaded.
- **Ready** — priced + images uploaded, ready for CSV export.
- **Listed** — exported and uploaded to a marketplace.
- **Sold** — marked sold (financial reporting).
- **ReservedForSet / SoldInSet** — card belongs to a SurpriseSet (locked, then
  sold as part of the completed set).

### Price staleness
Cards in `Priced`, `Ready`, or `Listed` become "stale" when `PriceDate` is older
than the configured threshold (default 30 days); the app flags them for repricing.
