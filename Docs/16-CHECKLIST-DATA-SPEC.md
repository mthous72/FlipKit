# Checklist Data Specification

## Overview

The verification pipeline depends on local set checklist data to confirm card numbers, player names, and variation names. This document defines the data format, sourcing strategy, and maintenance plan.

---

## Data Schema

### SetChecklist Table

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER PK | Auto-increment |
| Manufacturer | TEXT | Panini, Topps, Upper Deck, Leaf |
| Brand | TEXT | Prizm, Chrome, Donruss, etc. |
| Year | INTEGER | Release year (e.g., 2023) |
| Sport | TEXT | Football, Baseball, Basketball |
| Cards | TEXT (JSON) | JSON array of ChecklistCard objects |
| KnownVariations | TEXT (JSON) | JSON array of variation name strings |
| TotalBaseCards | INTEGER | Number of base cards in set |
| CachedAt | DATETIME | When this data was last updated |

**Unique constraint:** (Manufacturer, Brand, Year, Sport)

### ChecklistCard Object (JSON)

```json
{
  "card_number": "88",
  "player_name": "Justin Jefferson",
  "team": "Minnesota Vikings",
  "is_rookie": false,
  "subset": "Base"
}
```

### KnownVariations Array (JSON)

Stores the **canonical names** exactly as they appear in the product checklist. Includes numbering info where applicable.

```json
[
  "Base",
  "Silver",
  "Red White Blue",
  "Blue /199",
  "Carolina Blue /149",
  "Orange /99",
  "Purple /75",
  "Pink /50",
  "Gold /10",
  "Black 1/1",
  "Shimmer",
  "Neon Green"
]
```

**Naming convention:**
- Color name only for unnumbered: `"Silver"`
- Color + print run for numbered: `"Blue /199"`
- Color + `1/1` for one-of-ones: `"Black 1/1"`
- Descriptive for special: `"Red White Blue"`, `"Shimmer"`, `"Hyper"`

---

## Priority Sets to Include

### Tier 1 — Ship with v1.0 (must have)

These are the most commonly traded products on Whatnot. Full checklists + all known variations.

| Year | Manufacturer | Brand | Sport |
|------|-------------|-------|-------|
| 2024 | Panini | Prizm | Football |
| 2024 | Panini | Prizm | Basketball |
| 2024 | Topps | Chrome | Baseball |
| 2024 | Panini | Donruss | Football |
| 2024 | Panini | Donruss Optic | Football |
| 2024 | Panini | Donruss Optic | Basketball |
| 2024 | Panini | Mosaic | Football |
| 2024 | Panini | Mosaic | Basketball |
| 2024 | Panini | Select | Football |
| 2024 | Panini | Select | Basketball |
| 2023 | Panini | Prizm | Football |
| 2023 | Panini | Prizm | Basketball |
| 2023 | Topps | Chrome | Baseball |
| 2023 | Panini | Donruss | Football |
| 2023 | Panini | Donruss Optic | Football |

### Tier 2 — Ship with v1.1

| Year | Manufacturer | Brand | Sport |
|------|-------------|-------|-------|
| 2024 | Panini | Contenders | Football |
| 2024 | Panini | Phoenix | Football |
| 2024 | Topps | Bowman Chrome | Baseball |
| 2024 | Topps | Heritage | Baseball |
| 2024 | Topps | Stadium Club | Baseball |
| 2023 | Panini | Mosaic | Football |
| 2023 | Panini | Select | Football |
| 2022 | Panini | Prizm | Football |
| 2022 | Panini | Prizm | Basketball |
| 2022 | Topps | Chrome | Baseball |

### Tier 3 — Future updates

- Upper Deck Hockey series
- Panini National Treasures, Immaculate, Spectra (high-end)
- Topps Finest, Inception, Museum Collection (high-end)
- Leaf products
- Older years (2020-2021)

---

## Data Sourcing

> **Update 2026-05-01:** TCDB scraping is no longer the planned path. TCDB's Terms of Use explicitly prohibit both data mining/screen scraping AND commercial reproduction of their compiled checklist data, which conflicts with FlipKit's commercial distribution. Beckett has the same posture. The new strategy has two prongs:
>
> 1. **Bundled seed DB** — small, manually-curated set of Tier 1 checklists shipped with FlipKit (this document's original scope, still valid for Tier 1).
> 2. **User-driven Excel import** — users download per-set .xlsx files from [checklistinsider.com](https://www.checklistinsider.com/) under their own personal-use license and import them into FlipKit via a file picker. FlipKit never touches the site. See **[28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md)** for the full plan.
>
> The TCDB-scraping `ChecklistBuilder` console tool described below is **deprecated as a planned implementation** — keep this section for historical context only.

### Primary Source: TCDB.com

The Trading Card Database (tcdb.com) has the most comprehensive free checklists.

**Base URL patterns:**

```
# Set search
https://www.tcdb.com/SearchSets.cfm?SetName=2023+Panini+Prizm

# Set checklist (once you have the set ID)
https://www.tcdb.com/ViewAll.cfm/sid/{SET_ID}

# Set variations page
https://www.tcdb.com/Checklist.cfm/sid/{SET_ID}
```

**What to scrape from each set page:**
1. Full card list (card number, player name, team)
2. Subset names (Base, Rated Rookie, Inserts, etc.)
3. Parallel/variation names with numbering

### Secondary Source: CardboardConnection.com

Good for variation lists with images. Useful for confirming visual descriptions of parallels.

```
https://www.cardboardconnection.com/{year}-panini-prizm-football/
```

### Scraping Tool Specification

Build a standalone console app:

```
tools/ChecklistBuilder/
├── ChecklistBuilder.csproj
├── Program.cs
├── Scrapers/
│   ├── TcdbScraper.cs
│   └── CardboardConnectionScraper.cs
├── Models/
│   └── ScrapedSet.cs
└── Output/
    └── checklists.db
```

**ChecklistBuilder usage:**

```bash
# Scrape a specific set
dotnet run -- scrape --manufacturer "Panini" --brand "Prizm" --year 2023 --sport "Football"

# Scrape all Tier 1 sets
dotnet run -- scrape-all --tier 1

# Export to SQLite
dotnet run -- export --output checklists.db

# Export to JSON (for seed data)
dotnet run -- export --output checklist_seed.json --format json
```

**Important:** This tool is NOT part of the main Card Lister app. It's a developer tool run before each release. The main app only reads the bundled `checklists.db`.

---

## Variation Reference Data

### Panini Prizm Standard Parallels (All Sports)

```json
{
  "variations": [
    { "name": "Base", "numbered": false },
    { "name": "Silver", "numbered": false },
    { "name": "Red White Blue", "numbered": false, "retail_exclusive": "Target" },
    { "name": "Red", "numbered": false },
    { "name": "Blue", "numbered": true, "print_run": 199 },
    { "name": "Carolina Blue", "numbered": true, "print_run": 149 },
    { "name": "Orange", "numbered": true, "print_run": 99 },
    { "name": "Purple", "numbered": true, "print_run": 75 },
    { "name": "Pink", "numbered": true, "print_run": 50 },
    { "name": "Light Blue", "numbered": true, "print_run": 35 },
    { "name": "Green", "numbered": true, "print_run": 25 },
    { "name": "Gold", "numbered": true, "print_run": 10 },
    { "name": "Black", "numbered": true, "print_run": 1 },
    { "name": "Gold Vinyl", "numbered": true, "print_run": 1 },
    { "name": "Shimmer", "numbered": false },
    { "name": "Neon Green", "numbered": false },
    { "name": "Neon Orange", "numbered": false },
    { "name": "Neon Pink", "numbered": false },
    { "name": "Hyper", "numbered": false },
    { "name": "White Sparkle", "numbered": false },
    { "name": "Snakeskin", "numbered": false }
  ]
}
```

### Topps Chrome Standard Parallels

```json
{
  "variations": [
    { "name": "Base", "numbered": false },
    { "name": "Refractor", "numbered": false },
    { "name": "Sepia", "numbered": true, "print_run": 100 },
    { "name": "Pink", "numbered": true, "print_run": 50 },
    { "name": "Purple", "numbered": true, "print_run": 75 },
    { "name": "Green", "numbered": true, "print_run": 25 },
    { "name": "Orange", "numbered": true, "print_run": 10 },
    { "name": "Red", "numbered": true, "print_run": 5 },
    { "name": "Gold", "numbered": true, "print_run": 1 },
    { "name": "SuperFractor", "numbered": true, "print_run": 1 },
    { "name": "X-Fractor", "numbered": false },
    { "name": "Prism Refractor", "numbered": false },
    { "name": "Atomic Refractor", "numbered": false },
    { "name": "Negative Refractor", "numbered": false },
    { "name": "Black Refractor", "numbered": true, "print_run": 75 }
  ]
}
```

### Panini Donruss Standard Parallels

```json
{
  "variations": [
    { "name": "Base", "numbered": false },
    { "name": "Rated Rookie", "numbered": false, "note": "Rookie subset only" },
    { "name": "Press Proof Silver", "numbered": false },
    { "name": "Press Proof Blue", "numbered": true, "print_run": 75 },
    { "name": "Press Proof Gold", "numbered": true, "print_run": 25 },
    { "name": "Press Proof Red", "numbered": true, "print_run": 10 },
    { "name": "Press Proof Black", "numbered": true, "print_run": 1 },
    { "name": "Holo Red", "numbered": false },
    { "name": "Holo Blue", "numbered": false },
    { "name": "Holo Gold", "numbered": false },
    { "name": "Holo Purple", "numbered": false }
  ]
}
```

### Panini Donruss Optic Standard Parallels

```json
{
  "variations": [
    { "name": "Base", "numbered": false },
    { "name": "Rated Rookie", "numbered": false },
    { "name": "Holo", "numbered": false },
    { "name": "Red", "numbered": true, "print_run": 99 },
    { "name": "Blue", "numbered": true, "print_run": 75 },
    { "name": "Pink", "numbered": true, "print_run": 50 },
    { "name": "Purple", "numbered": true, "print_run": 25 },
    { "name": "Gold", "numbered": true, "print_run": 10 },
    { "name": "Black", "numbered": true, "print_run": 1 },
    { "name": "Orange", "numbered": true, "print_run": 199 },
    { "name": "Lime Green", "numbered": true, "print_run": 149 },
    { "name": "Pink Velocity", "numbered": false },
    { "name": "Blue Velocity", "numbered": false },
    { "name": "Red Velocity", "numbered": false }
  ]
}
```

---

## Parallel Identification Rules

These rules help the verifier cross-reference visual cues with known parallels. Embed these as logic in `VariationVerifierService`:

### Rule 1: Serial Number → Numbered Parallel

```
IF serial_denominator is visible:
  MATCH against known numbered parallels for this set
  The denominator uniquely identifies the parallel in most cases
  Example: /199 in Prizm = Blue, /99 = Orange, /75 = Purple
```

### Rule 2: Border Color → Color Parallel

```
IF border_color is non-standard:
  CHECK if a parallel with that color name exists
  Example: gold border + Prizm = "Gold /10"
  Example: blue border + Chrome = could be "Blue" or several others (needs serial)
```

### Rule 3: Surface Finish → Refractor/Foil Type

```
IF card_finish is "holographic" or "refractor":
  AND manufacturer is Topps: likely a Refractor variant
  AND manufacturer is Panini: likely Silver Prizm or Shimmer
IF card_finish is "shimmer" or "sparkle":
  AND brand is Prizm: likely "Shimmer" or "White Sparkle"
```

### Rule 4: Retail Exclusive Detection

```
IF all_visible_text contains "TARGET EXCLUSIVE" or red-white-blue color scheme:
  Likely "Red White Blue" retail exclusive
IF all_visible_text contains "WALMART" or similar:
  Check for retail-specific parallels
```

---

## Handling Missing Checklists

When a card's set is not in the checklist database:

1. **Don't block the workflow** — the card still saves normally
2. **Show informational message:** "Set checklist not available. Variation not verified — please confirm manually."
3. **Log the miss** — track which sets users scan that aren't in the DB, so you know what to add next
4. **Variation name is preserved** — whatever the AI said is kept as-is

### Missing Set Logging

Add to the Card entity or a separate tracking table:

```csharp
// Track which sets users scan that aren't in the checklist DB
public class MissingChecklist
{
    public int Id { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Brand { get; set; } = "";
    public int Year { get; set; }
    public string Sport { get; set; } = "";
    public int HitCount { get; set; } = 1;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}
```

This data helps prioritize which sets to add in future updates.

---

## Update Cadence

| Timing | Action |
|--------|--------|
| Before each release | Run ChecklistBuilder for new Tier 1 sets |
| Annually (January) | Add previous year's Tier 1 sets |
| Annually (September) | Add current year's football sets (season start) |
| On request | Add specific sets users report as missing |

---

## File Size Estimates

| Component | Estimated Size |
|-----------|---------------|
| Full Tier 1 checklist DB | ~2-5 MB |
| Full Tier 1+2 | ~5-10 MB |
| Full all tiers | ~15-25 MB |
| Seed data (JSON, abbreviated) | ~50-100 KB |

These sizes are reasonable for bundling in a desktop app.
