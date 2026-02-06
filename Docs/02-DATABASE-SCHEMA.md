# Database Schema

## Overview

SQLite database with a single `cards` table. Designed to capture everything needed for:
- Card identification
- Variation/parallel tracking
- Pricing
- Whatnot listing generation

---

## Cards Table

```sql
CREATE TABLE cards (
    -- Primary key
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,

    -- === CARD IDENTITY ===
    player_name         TEXT NOT NULL,
    card_number         TEXT,           -- e.g., "127", "RC-15"
    year                INTEGER,        -- e.g., 2023
    sport               TEXT,           -- Football, Baseball, Basketball

    -- === MANUFACTURER / SET ===
    manufacturer        TEXT,           -- Panini, Topps, Upper Deck
    brand               TEXT,           -- Prizm, Donruss, Chrome, Bowman
    set_name            TEXT,           -- Full set name if different from brand
    team                TEXT,           -- Team name

    -- === VARIATION / PARALLEL ===
    variation_type      TEXT DEFAULT 'Base',  
                                        -- Base, Parallel, Insert, Refractor, etc.
    parallel_name       TEXT,           -- Silver, Blue, Gold, Holo, etc.
    serial_numbered     TEXT,           -- "/99", "/25", "1/1", or NULL
    is_short_print      INTEGER DEFAULT 0,  -- SP indicator
    is_ssp              INTEGER DEFAULT 0,  -- SSP indicator

    -- === SPECIAL ATTRIBUTES ===
    is_rookie           INTEGER DEFAULT 0,
    is_auto             INTEGER DEFAULT 0,  -- Has autograph
    is_relic            INTEGER DEFAULT 0,  -- Has jersey/memorabilia piece

    -- === CONDITION / GRADING ===
    condition           TEXT DEFAULT 'Near Mint',
    is_graded           INTEGER DEFAULT 0,
    grade_company       TEXT,           -- PSA, BGS, SGC, CGC
    grade_value         TEXT,           -- "10", "9.5", "9"
    cert_number         TEXT,           -- Grading cert # for lookup

    -- === ACQUISITION / COST BASIS (for tax tracking) ===
    cost_basis          REAL,           -- What you paid for the card
    cost_source         TEXT,           -- LCS, Online, Break, Trade, Gift, etc.
    cost_date           TEXT,           -- When you acquired it (YYYY-MM-DD)
    cost_notes          TEXT,           -- Receipt #, seller name, etc.

    -- === PRICING ===
    estimated_value     REAL,           -- Market value from comps
    price_source        TEXT,           -- "Terapeak", "eBay comps", etc.
    price_date          TEXT,           -- When price was last researched (YYYY-MM-DD)
    listing_price       REAL,           -- Your asking price
    price_check_count   INTEGER DEFAULT 0,  -- How many times repriced

    -- === SALE INFORMATION (when sold) ===
    sale_price          REAL,           -- What it sold for
    sale_date           TEXT,           -- When it sold (YYYY-MM-DD)
    sale_platform       TEXT,           -- Whatnot, eBay, etc.
    fees_paid           REAL,           -- Platform + payment processing fees
    shipping_cost       REAL,           -- Your actual shipping cost
    net_profit          REAL,           -- Auto-calculated: sale - cost - fees - shipping

    -- === LISTING SETTINGS ===
    quantity            INTEGER DEFAULT 1,
    listing_type        TEXT DEFAULT 'Buy It Now',  
                                        -- "Buy It Now", "Auction"
    offerable           INTEGER DEFAULT 1,  -- Accept offers on BIN
    shipping_profile    TEXT DEFAULT '4 oz',

    -- === IMAGES ===
    image_path_front    TEXT,           -- Local path to front image
    image_path_back     TEXT,           -- Local path to back image
    image_url_1         TEXT,           -- Public URL (ImgBB) for front
    image_url_2         TEXT,           -- Public URL for back

    -- === WHATNOT-SPECIFIC ===
    whatnot_category    TEXT DEFAULT 'Sports Cards',
    whatnot_subcategory TEXT,           -- Football Cards, Baseball Cards, etc.

    -- === STATUS / METADATA ===
    status              TEXT DEFAULT 'draft',  
                                        -- draft, priced, ready, listed, sold
    notes               TEXT,           -- Free-form notes
    created_at          TEXT DEFAULT (datetime('now')),
    updated_at          TEXT DEFAULT (datetime('now'))
);

-- Price history for tracking changes over time
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

## Indexes

```sql
CREATE INDEX idx_cards_sport ON cards(sport);
CREATE INDEX idx_cards_player ON cards(player_name);
CREATE INDEX idx_cards_status ON cards(status);
CREATE INDEX idx_cards_year ON cards(year);
```

---

## Key Field Notes

### variation_type
Common values:
- `Base` — Standard base card
- `Parallel` — Color/pattern variant of base
- `Insert` — Special subset card
- `Refractor` — Chrome/Prizm refractor parallel
- `Auto` — Autograph card (also set `is_auto = 1`)
- `Relic` — Memorabilia card (also set `is_relic = 1`)

### parallel_name
Examples by manufacturer:

**Panini Prizm:**
- Silver, Red, Blue, Green, Gold, Orange, Purple
- Hyper, Shimmer, Neon Green, Pink Ice
- Black (1/1)

**Topps Chrome:**
- Refractor, Sepia, Pink, Purple, Gold, Red
- X-Fractor, Prism, Atomic, SuperFractor (1/1)

**Panini Donruss:**
- Rated Rookie, Press Proof, Holo variants
- Optic parallels (various colors)

### shipping_profile
Must match Whatnot's exact values:
- `4 oz` — Single raw card in toploader + bubble mailer
- `8 oz` — Multiple cards or graded slab
- `1 lb` — Several slabs or small box

### whatnot_subcategory
Must be one of:
- `Football Cards`
- `Baseball Cards`
- `Basketball Cards`
- `Hockey Cards`
- `Soccer Cards`
- `Other Sports Cards`

---

## Status Flow

```
draft → priced → ready → listed → sold
  ↑       ↑        │        │
  └───────┴────────┘        │
     (can revert)           │
                            ▼
                   (archived in reports)
```

- **draft**: Just scanned, missing price or other data
- **priced**: Has listing price, may need images uploaded
- **ready**: Has price + images uploaded, ready for CSV export
- **listed**: Exported to Whatnot CSV and uploaded
- **sold**: Marked as sold (for financial tracking/reports)

### Price Staleness

Cards in `priced`, `ready`, or `listed` status can become "stale" if `price_date` is more than 30 days ago. The app will flag these for re-pricing.

---

## Example Queries

### Get all cards ready for export
```sql
SELECT * FROM cards 
WHERE status = 'ready' 
  AND listing_price > 0 
  AND image_url_1 IS NOT NULL;
```

### Get cards by sport
```sql
SELECT * FROM cards WHERE sport = 'Football' ORDER BY player_name;
```

### Search by player
```sql
SELECT * FROM cards 
WHERE player_name LIKE '%Jefferson%' 
ORDER BY year DESC;
```

### Calculate total inventory value
```sql
SELECT 
    sport,
    COUNT(*) as card_count,
    SUM(listing_price) as total_value
FROM cards
GROUP BY sport;
```

### Find cards missing prices
```sql
SELECT id, player_name, year, brand 
FROM cards 
WHERE listing_price IS NULL OR listing_price = 0;
```
