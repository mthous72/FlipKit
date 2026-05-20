# Inventory Management & Financial Tracking

## Overview

Two critical features for serious card sellers:

1. **Price Re-checking** — Cards sit unsold, market changes, need to reprice
2. **Cost Basis Tracking** — What you paid vs. what you sold for (IRS needs this)

---

## Feature 1: Price Staleness & Re-checking

### The Problem

You priced a card at $15 three months ago. It hasn't sold. Meanwhile:
- The player got injured (price dropped)
- Or the player made the Pro Bowl (price went up)
- Or the market just shifted

You need to know which cards have stale pricing.

### Solution: Price Age Tracking

Every card tracks:
- `price_date` — When you last researched the price
- `listing_price` — Your current asking price
- `estimated_value` — Market value at time of pricing

**Visual indicators in My Cards:**

```
┌──────────────────────────────────────────────────────────────────────────┐
│  📋 My Cards (47 total)                                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  🔍 [Search...            ]  Sport [All ▼]  Status [All ▼]  Price Age ▼ │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │    │       │                 │      │        │        │ Price    │ │ │
│  │ ☐  │ Image │ Player          │ Year │ Brand  │ Price  │ Age      │ │ │
│  ├────┼───────┼─────────────────┼──────┼────────┼────────┼──────────┤ │ │
│  │ ☐  │ [IMG] │ Justin Jefferson│ 2023 │ Prizm  │ $12.99 │ 🟢 5 days │ │ │
│  │ ☐  │ [IMG] │ CJ Stroud       │ 2023 │ Donruss│ $18.99 │ 🟡 32 days│ │ │
│  │ ☐  │ [IMG] │ Brock Purdy     │ 2023 │ Prizm  │ $24.99 │ 🔴 67 days│ │ │
│  │ ☐  │ [IMG] │ Trevor Lawrence │ 2023 │ Select │ $8.99  │ 🔴 91 days│ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  Legend: 🟢 < 14 days   🟡 14-30 days   🔴 > 30 days                     │
│                                                                          │
│  [ 🔄 Reprice Stale Cards (8) ]                                          │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Price Age Rules

| Age | Status | Color | Action |
|-----|--------|-------|--------|
| 0-14 days | Fresh | 🟢 Green | No action needed |
| 14-30 days | Aging | 🟡 Yellow | Consider rechecking |
| 30+ days | Stale | 🔴 Red | Should reprice |
| 60+ days | Very stale | 🔴 Red + badge | Strongly recommend reprice |

### Reprice Workflow

New button: **"Reprice Stale Cards"**

Shows cards with prices older than 30 days, same interface as initial pricing:

```
┌──────────────────────────────────────────────────────────────────────────┐
│   Reprice Stale Cards                                                    │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   8 cards have prices older than 30 days                                │
│                                                                          │
│   ┌──────────────────────────────────────────────────────────────────┐  │
│   │                                                                  │  │
│   │   Card 1 of 8                                    [ ⏭️ Skip ]     │  │
│   │                                                                  │  │
│   │   ┌─────────┐                                                    │  │
│   │   │  [IMG]  │   2023 Panini Prizm                               │  │
│   │   │         │   Brock Purdy                                     │  │
│   │   └─────────┘   Silver Parallel #341                            │  │
│   │                                                                  │  │
│   │   ─────────────────────────────────────────────────────────────│  │
│   │                                                                  │  │
│   │   📅 Last priced: 67 days ago (Dec 1, 2024)                     │  │
│   │   💵 Current price: $24.99                                      │  │
│   │   📊 Original market value: $28.00                              │  │
│   │                                                                  │  │
│   │   ─────────────────────────────────────────────────────────────│  │
│   │                                                                  │  │
│   │   [ 🔍 Open Terapeak ]    [ 🔍 Open eBay Sold ]                  │  │
│   │                                                                  │  │
│   │   New market value:  $ [                    ]                   │  │
│   │                                                                  │  │
│   │   Suggested price: —                                            │  │
│   │   New listing price: $ [                    ]                   │  │
│   │                                                                  │  │
│   │   ─────────────────────────────────────────────────────────────│  │
│   │                                                                  │  │
│   │   [ ✓ Price unchanged, keep current ]                           │  │
│   │                                                                  │  │
│   │   [ 💾 Save New Price & Next → ]                                 │  │
│   │                                                                  │  │
│   └──────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

**Options:**
1. **Update price** — Enter new market value, save new listing price
2. **Keep current** — Mark as "rechecked" without changing price (resets the clock)
3. **Skip** — Come back to it later

### Price History (Optional Enhancement)

Track all price changes over time:

```sql
CREATE TABLE price_history (
    id              INTEGER PRIMARY KEY,
    card_id         INTEGER NOT NULL,
    estimated_value REAL,
    listing_price   REAL,
    price_source    TEXT,
    recorded_at     TEXT DEFAULT (datetime('now')),
    FOREIGN KEY (card_id) REFERENCES cards(id)
);
```

Benefits:
- See how a card's value changed over time
- Identify cards that keep dropping (maybe just sell them)
- Spot market trends

---

## Feature 2: Cost Basis & Profit Tracking

### The Problem

The IRS considers card selling as income (hobby or business). You need:
- What you paid for each card (cost basis)
- What you sold it for (revenue)
- Your profit (revenue - cost - fees)

Without tracking: Tax nightmare. With tracking: Easy Schedule C or hobby income.

### Solution: Financial Fields

Every card tracks:

| Field | Purpose | Example |
|-------|---------|---------|
| `cost_basis` | What you paid | $5.00 |
| `cost_source` | Where you got it | "LCS purchase", "Break", "Trade" |
| `cost_date` | When you acquired it | 2024-01-15 |
| `sale_price` | What it sold for | $12.99 |
| `sale_date` | When it sold | 2024-02-20 |
| `sale_platform` | Where it sold | "Whatnot" |
| `fees_paid` | Platform + payment fees | $1.69 |
| `shipping_cost` | Your actual shipping cost | $0.75 |
| `net_profit` | Auto-calculated | $5.55 |

### Updated Card Entry Form

Add cost basis during scanning or editing:

```
┌──────────────────────────────────────────────────────────────────────────┐
│   Card Details                         Acquisition                       │
│   ────────────                         ───────────                       │
│                                                                          │
│   Player    [Justin Jefferson  ]       Cost      $ [    5.00    ]       │
│   Year      [2023              ]                                         │
│   Brand     [Prizm             ]       Source    [ LCS Purchase   ▼ ]   │
│   ...                                                                    │
│                                        Date      [ 2024-01-15     ]     │
│                                                                          │
│                                        Notes     [                 ]     │
│                                                  (e.g., "From Joe's     │
│                                                   Cards, receipt #123") │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

**Cost Source dropdown:**
- LCS Purchase (Local Card Shop)
- Online Purchase (eBay, etc.)
- Card Show
- Break/Box
- Trade
- Gift/Free
- Personal Collection
- Unknown

### Marking Cards as Sold

When a card sells, you mark it in the app:

```
┌──────────────────────────────────────────────────────────────────────────┐
│   Mark as Sold                                                           │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   2023 Panini Prizm Justin Jefferson Silver #88                         │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   Cost Basis:        $5.00                                              │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   Sale price:        $ [    12.99    ]                                  │
│                                                                          │
│   Platform:          [ Whatnot              ▼ ]                         │
│                                                                          │
│   Sale date:         [ 2024-02-20           ]                           │
│                                                                          │
│   Platform fees:     $ [     1.43    ]  (auto-calculated: 11%)          │
│                                                                          │
│   Shipping cost:     $ [     0.75    ]  (what you actually paid)        │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   💰 Net Profit:     $5.81                                               │
│      (Sale $12.99 - Cost $5.00 - Fees $1.43 - Shipping $0.75)           │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   [ Cancel ]                              [ ✓ Mark as Sold ]            │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Financial Reports

New **Reports** tab (or section in Export):

```
┌──────────────────────────────────────────────────────────────────────────┐
│   📊 Financial Reports                                                   │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   Date Range: [ 2024-01-01 ] to [ 2024-12-31 ]    [ Apply ]             │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   Summary                                                                │
│   ───────                                                                │
│                                                                          │
│   Cards Sold:           127                                             │
│   Total Revenue:        $2,847.23                                       │
│   Total Cost Basis:     $1,234.56                                       │
│   Total Fees:           $312.87                                         │
│   Total Shipping:       $95.25                                          │
│   ─────────────────────────────────                                     │
│   Net Profit:           $1,204.55                                       │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   Breakdown by Month                                                     │
│   ──────────────────                                                     │
│                                                                          │
│   January 2024:    12 sold    $234.56 revenue    $89.23 profit          │
│   February 2024:   15 sold    $312.99 revenue    $124.55 profit         │
│   March 2024:      8 sold     $189.00 revenue    $67.80 profit          │
│   ...                                                                    │
│                                                                          │
│   ─────────────────────────────────────────────────────────────────────  │
│                                                                          │
│   [ ⬇️ Export for Tax Purposes (CSV) ]                                   │
│                                                                          │
│   Includes: Date, Item Description, Cost Basis, Sale Price,             │
│             Fees, Shipping, Net Profit                                  │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Tax Export CSV

Generates a CSV suitable for tax preparation:

```csv
Sale Date,Item Description,Cost Basis,Sale Price,Platform,Fees,Shipping,Net Profit
2024-01-15,"2023 Prizm Justin Jefferson Silver #88",5.00,12.99,Whatnot,1.43,0.75,5.81
2024-01-18,"2023 Donruss CJ Stroud Rated Rookie #301",3.50,18.99,Whatnot,2.09,0.75,12.65
2024-01-22,"2024 Topps Chrome Shohei Ohtani Refractor #1",8.00,24.99,Whatnot,2.75,0.75,13.49
...
```

**Columns explained:**
- **Item Description** — Auto-generated from card details
- **Cost Basis** — What you paid
- **Sale Price** — What buyer paid
- **Fees** — Platform + payment processing
- **Shipping** — What you spent on shipping
- **Net Profit** — Sale - Cost - Fees - Shipping

---

## Updated Database Schema

Add these fields to the `cards` table:

```sql
-- Acquisition / Cost Basis
cost_basis          REAL,               -- What you paid for the card
cost_source         TEXT,               -- LCS, Online, Break, Trade, etc.
cost_date           TEXT,               -- When you acquired it
cost_notes          TEXT,               -- Receipt #, seller name, etc.

-- Sale Information
sale_price          REAL,               -- What it sold for
sale_date           TEXT,               -- When it sold
sale_platform       TEXT,               -- Whatnot, eBay, etc.
fees_paid           REAL,               -- Platform + payment fees
shipping_cost       REAL,               -- Your actual shipping cost
net_profit          REAL,               -- Auto-calculated

-- Pricing Metadata
price_date          TEXT,               -- When price was last researched
price_check_count   INTEGER DEFAULT 0,  -- How many times repriced
```

Add new table for price history:

```sql
CREATE TABLE price_history (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    card_id         INTEGER NOT NULL,
    estimated_value REAL,
    listing_price   REAL,
    price_source    TEXT,
    notes           TEXT,
    recorded_at     TEXT DEFAULT (datetime('now')),
    FOREIGN KEY (card_id) REFERENCES cards(id) ON DELETE CASCADE
);
```

---

## Card Status Flow (Updated)

```
                    ┌─────────┐
                    │  draft  │  Just scanned, no price
                    └────┬────┘
                         │ Price added
                         ▼
                    ┌─────────┐
            ┌──────│  priced │  Has price, needs images
            │       └────┬────┘
            │            │ Images uploaded
   Price    │            ▼
   expires  │       ┌─────────┐
   (30+ days)│       │  ready  │  Ready for CSV export
            │       └────┬────┘
            │            │ Exported to Whatnot
            │            ▼
            │       ┌─────────┐
            └──────▶│ listed  │◀─── Reprice ───┐
                    └────┬────┘                │
                         │ Sold                │
                         ▼                     │
                    ┌─────────┐                │
                    │  sold   │  Archived, in reports
                    └─────────┘
```

---

## Settings: Financial Preferences

```
┌──────────────────────────────────────────────────────────────────────────┐
│   Financial Settings                                                     │
│   ──────────────────                                                     │
│                                                                          │
│   Default Platform Fees                                                  │
│                                                                          │
│   Whatnot:     [ 11   ] %  (8% + 2.9% + $0.30)                          │
│   eBay:        [ 13.25] %  (varies by category)                         │
│                                                                          │
│   Default Shipping Cost                                                  │
│                                                                          │
│   PWE (plain white envelope):  $ [ 1.00 ]                               │
│   BMWT (bubble mailer):        $ [ 4.50 ]                               │
│                                                                          │
│   Price Staleness Threshold                                              │
│                                                                          │
│   Flag cards for repricing after: [ 30 ] days                           │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Summary of New Features

### Price Re-checking
- ✅ Track when each card was priced
- ✅ Visual indicators (🟢🟡🔴) for price age
- ✅ Filter to show stale prices
- ✅ "Reprice Stale Cards" workflow
- ✅ Option to keep current price (resets clock)
- ✅ Price history log (optional)

### Cost Basis & Profit Tracking
- ✅ Cost basis field (what you paid)
- ✅ Cost source (where you got it)
- ✅ Acquisition date
- ✅ Mark as Sold workflow
- ✅ Auto-calculate fees and profit
- ✅ Financial reports by date range
- ✅ Tax export CSV

### IRS Compliance
- ✅ All data needed for Schedule C (business) or hobby income
- ✅ Clear paper trail: cost → sale → fees → profit
- ✅ Exportable reports for tax prep
- ✅ Notes field for receipt numbers, seller info
