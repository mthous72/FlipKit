# Card Listings: Whatnot & eBay CSV Export Specification

> **Purpose.** This document specifies how to generate two CSV files from a card scanner's output (Ximilar / LLM identification, ImgBB-hosted images, pricing) so they can be imported in bulk into Whatnot's Seller Hub and eBay's Seller Hub Reports. It captures the exact column structures, validation rules, value constraints, and gotchas discovered through real upload errors. **Read this entire document before writing exporter code** — most of the rules look trivial but each one represents a previously-failed upload.

> **Companion file.** The full Whatnot reference data (37 categories, 264 sub-categories, 941 conditions across 153 keys, 23 shipping profiles, 3 hazmat values) lives in `whatnot_values.json` alongside this spec. Load it at runtime; do not hardcode the lists.

---

## 1. Source Data Model

Before writing exporters, define a single canonical card record that both serializers consume. Suggested structure (adapt field names to match the existing scanner output):

```python
@dataclass
class CardRecord:
    # Identity
    title: str                          # Final listing title, ≤80 chars
    sport_or_game: str                  # "Baseball", "Pokémon", "Magic", etc.
    set_name: str | None
    card_number: str | None
    year: int | None
    manufacturer: str | None            # "Topps", "Panini", "WOTC", etc.
    player_or_character: str | None

    # Condition
    condition_raw: str                  # As reported by scanner: "Used", "Mint", "PSA 9", etc.
    is_graded: bool
    grader: str | None                  # "PSA", "BGS", "BCCG", "BVG", "CGC"
    grade: str | None                   # "10", "9.5", "9", "8", etc.
    cert_number: str | None             # Grading slab cert #

    # Commerce
    price_usd: float                    # Whole or fractional dollars; serializers handle rounding
    quantity: int = 1
    sku: str | None = None

    # Media — ImgBB direct image URLs (https, publicly accessible, no auth)
    image_urls: list[str]               # First URL is the primary/gallery image

    # Optional / per-row overrides
    description_html: str | None = None # If None, build from other fields
```

The exporters take a `list[CardRecord]` plus per-export defaults (Whatnot category, eBay listing duration, etc.) and emit a CSV.

---

## 2. Output 1 — Whatnot CSV Bulk Import

### 2.1 File format

- UTF-8 encoded CSV (no BOM is fine; with BOM also works)
- Single header row, no `#INFO` rows, no preamble
- Standard CSV escaping (RFC 4180 — wrap fields containing commas/quotes/newlines in double quotes; double-up internal quotes)

### 2.2 Column structure — exact order, all 21 columns

```
Category, Sub Category, Title, Description, Quantity, Type, Price,
Shipping Profile, Offerable, Hazmat, Condition, Cost Per Item, SKU,
Image URL 1, Image URL 2, Image URL 3, Image URL 4,
Image URL 5, Image URL 6, Image URL 7, Image URL 8
```

### 2.3 Field specifications

| Column | Required | Type / Format | Rules |
|---|---|---|---|
| **Category** | Yes | Enum string | Must exactly match one of the 37 values in `whatnot_values.json → categories`. Case- and punctuation-sensitive. |
| **Sub Category** | Conditional | Enum string | If the chosen Category has sub-categories (`whatnot_values.json → subcategories[Category]`), pick one of those values. 6 of 37 categories have no sub-categories — leave blank for those. |
| **Title** | Yes | String, ≤ 80 chars | Truncate longer titles. Keep keywords near the front. |
| **Description** | Yes | String | Plain text or simple HTML. Build from card metadata if not supplied. |
| **Quantity** | Yes | Integer ≥ 1 | Whole number, no decimals (`1`, not `1.0`). |
| **Type** | Yes | Enum | Exactly one of: `Auction`, `Buy it Now`, `Giveaway`. **Note the lowercase `it`** — `Buy It Now` is rejected. |
| **Price** | Yes | **Positive integer** | **No decimals.** `65`, not `65.00`. Round to nearest dollar; clamp minimum to `1`. For auctions this is the starting bid. |
| **Shipping Profile** | Yes | Enum string | One of the 23 values in `whatnot_values.json → shipping_profiles`, OR the exact name of a custom shipping profile saved in the seller's Whatnot account. |
| **Offerable** | No | `TRUE` / `FALSE` | Only meaningful for `Buy it Now`. Leave blank for `Auction` and `Giveaway` rows. Uppercase string, not boolean. |
| **Hazmat** | Yes | Enum | `Not Hazmat`, `Hazmat - Standard`, or `Hazmat - Lithium Battery`. Cards are always `Not Hazmat`. |
| **Condition** | Conditional | Enum string | Look up valid values in `whatnot_values.json → conditions[Sub Category]`, falling back to `conditions[Category]` if no sub-category match. Some categories have no condition list — leave blank in that case. |
| **Cost Per Item** | No | Decimal (e.g. `9.59`) | The seller's cost basis. Decimals allowed here (unlike Price). Optional. |
| **SKU** | No | String | Internal tracking identifier. |
| **Image URL 1–8** | No | URL | Up to 8 publicly-accessible HTTPS URLs (ImgBB works). No auth, no `?` query params that require headers. Blank trailing columns are fine. |

### 2.4 Critical gotchas

These have all caused real upload failures:

1. **`Buy it Now`, not `Buy It Now`.** eBay's pre-export uses `FixedPrice` — map it to `Buy it Now` with lowercase `it`. Whatnot's data validation enum literally is `"Auction,Buy it Now,Giveaway"`.
2. **Price must be a positive integer.** Whatnot rejects `65.00` even though it's mathematically equal to `65`. Always emit `int(round(price))` clamped to `≥1`.
3. **Category and Sub Category are case-sensitive enum matches.** "Trading Cards" ≠ "Trading Card Games". "Pokemon Cards" ≠ "Pokémon Cards" (with the é).
4. **Conditions are looked up by sub-category, not category, for most groups.** 141 of 153 condition groups in the lookup are keyed by sub-category. Build a fallback chain: try sub-category first, then category, then leave blank.
5. **The Shipping Profile list is exhaustive.** Whatnot rejects custom weight strings like `"2 oz"` — must be one of the bucket names exactly: `0-1 oz`, `1-3 oz`, `4-7 oz`, `8-11 oz`, `12-15 oz`, `1 lb`, `1-2 lbs`, `2-3 lbs`, etc.
6. **Image URLs must be publicly fetchable.** Test each URL by opening in an Incognito window. ImgBB's direct image URLs (the `i.ibb.co/...` form) work; their viewer-page URLs do not.
7. **Title hard limit is 80 chars.** Truncate, don't fail.

### 2.5 Example row (Pokémon card)

```csv
Category,Sub Category,Title,Description,Quantity,Type,Price,Shipping Profile,Offerable,Hazmat,Condition,Cost Per Item,SKU,Image URL 1,Image URL 2,Image URL 3,Image URL 4,Image URL 5,Image URL 6,Image URL 7,Image URL 8
Trading Card Games,Pokémon Cards,1999 Pokemon Base Set Charizard Holo #4 PSA 8,1999 Pokemon Base Set Charizard Holo #4. Graded PSA 8 NM-MT.,1,Buy it Now,2500,0-1 oz,TRUE,Not Hazmat,Graded,,POK-CHAR-001,https://i.ibb.co/abc/charizard-front.jpg,https://i.ibb.co/abc/charizard-back.jpg,,,,,,
```

---

## 3. Output 2 — eBay CSV Bulk Listing

### 3.1 Choosing the template variant

eBay offers two paths from Seller Hub > Reports > Upload > Get template:

| Template | When to use | Required fields | Effect |
|---|---|---|---|
| **Create new drafts** | Programmatic exports where final review happens in eBay UI | `Action=Draft`, `Category ID` only | Creates draft listings; user finishes setup in Drafts folder |
| **Create new listings** | Listings should go live immediately, all data is complete | 13 required fields (see §3.4) | Creates active listings; insertion fees apply |

**Recommendation for a card scanner pipeline:** Use **Create new listings** because card data is well-structured and the scanner already produces all the required fields. Drafts can be useful for troubleshooting initial integration.

The user has provided a Sports Trading Cards "Create new listings" template (CategoryID `261328`). This spec describes that variant. For other categories, download the matching template and adapt the C: (Item Specifics) columns.

### 3.2 File structure

eBay templates are CSVs with metadata rows. Generated output must preserve this structure:

```
Info,Version=1.0.0,Template=fx_category_template_EBAY_US
*Action(SiteID=US|Country=US|Currency=USD|Version=1193|CC=UTF-8),CustomLabel,*Category,...



Info,>>> Get more details on how to complete listings...
Info,>>> For categoryId:  261328
Info,>>> The required aspects are Sport
Info,>>> The recommended value(s) for aspect Sport: ...
... (additional Info rows describing valid values)
Info,>>> Multiple Condition Descriptor values should be separated by the pipe (|) character.
Add,...,261328,...   ← first data row starts here
Add,...
```

**Rules:**
- Row 0: `Info,Version=1.0.0,Template=fx_category_template_EBAY_US` — copy verbatim from the downloaded template
- Row 1: Column headers (the long row starting with `*Action(SiteID=...|...)`)
- Rows 2–4: Empty rows (preserve them)
- Rows 5–N: `Info,>>>` rows describing valid values — copy verbatim
- After all `Info` rows: data rows begin
- **Do not strip the `#INFO` rows.** eBay's parser tolerates them; some report formats explicitly require them.
- The Action column header is the long form: `*Action(SiteID=US|Country=US|Currency=USD|Version=1193|CC=UTF-8)`. Treat the `*` and parenthesized parameters as part of the literal column name.

### 3.3 Action column values

For the Create new listings template, every data row's Action is one of:
- `Add` — create the listing
- `Revise` — modify an existing listing (requires Item number)
- `End` — end an existing listing
- `Relist` — relist an ended listing
- `VerifyAdd` — dry-run validation without creating the listing

For programmatic exports, use `Add`. To validate without committing, change Action to `VerifyAdd` for a test batch.

### 3.4 Required columns (Sports Trading Cards, CategoryID 261328)

Columns marked `*` in the template header are required:

| Column (template header) | Description | Format |
|---|---|---|
| `*Action(SiteID=US\|Country=US\|Currency=USD\|Version=1193\|CC=UTF-8)` | Listing action | `Add` / `Revise` / `End` / `Relist` / `VerifyAdd` |
| `*Category` | Numeric eBay category leaf node ID | `261328` for Sports Trading Cards. Find others at https://pages.ebay.com/sellerinformation/news/categorychanges.html |
| `*Title` | Listing title | ≤ 80 chars |
| `*ConditionID` | Condition (numeric ID) | See §3.5 |
| `*C:Sport` | Sport item specific | Free text, but eBay recommends matching their suggested list (see Info rows in template) — `Baseball`, `Basketball`, `Football`, `Hockey`, `Soccer`, etc. |
| `*Description` | Listing description | Plain text or HTML. Use `<p>` and `<br>` for breaks. |
| `*Format` | Listing format | `Auction` or `FixedPrice` (no space, exact case) |
| `*Duration` | Listing duration | `Days_3`, `Days_5`, `Days_7`, `Days_10`, `Days_30`, `GTC` (Good Til Cancelled — fixed-price only) |
| `*StartPrice` | Price | Decimal dollars, **no currency symbol**. Decimals OK here (`5.99`). For auctions, this is the starting bid. |
| `*Quantity` | Number of items | Integer ≥ 1 |
| `*Location` | Where the item ships from | ZIP code (e.g. `45202`) or city (e.g. `Cincinnati, OH`) |
| `*DispatchTimeMax` | Handling time in days | Integer (`1`, `2`, `3`) |
| `*ReturnsAcceptedOption` | Whether you accept returns | `ReturnsAccepted` or `ReturnsNotAccepted` |

When `ReturnsAcceptedOption = ReturnsAccepted`, also fill in:
- `ReturnsWithinOption` — `Days_30`, `Days_60`
- `RefundOption` — `MoneyBack`
- `ShippingCostPaidByOption` — `Buyer` or `Seller`

### 3.5 ConditionID — numeric IDs by category family

Condition IDs differ per category. For Sports Trading Cards:

| ID | Meaning |
|---|---|
| `1000` | New |
| `2750` | Like New |
| `3000` | Used |
| `4000` | Graded (use this for slabbed cards) |
| `5000` | Ungraded (use this for raw cards) |
| `7000` | For parts or not working |

Full list: https://developer.ebay.com/DevZone/finding/CallRef/Enums/conditionIdList.html

**For the Drafts template only**, ConditionID accepts the words `NEW` or `USED` (all caps) instead of numbers. This spec assumes the live listings template, so use numbers.

### 3.6 Condition Descriptors (CD: / CDA: prefix)

For graded cards, fill in the Condition Descriptor columns (these are sport-card specific):

- **CD:Professional Grader (ID 27501)** — pick from: `Professional Sports Authenticator (PSA)` (ID 275010), `Beckett Grading Services (BGS)` (275013), `Beckett Vintage Grading (BVG)` (275012), `Beckett Collectors Club Grading (BCCG)` (275011), and others.
- **CD:Grade (ID 27502)** — pick from: `10` (275020), `9.5` (275021), `9` (275022), `8.5` (275023), `8` (275024), and so on down to `1`.
- **CDA:Certification Number (ID 27503)** — free text (the cert number printed on the slab).
- **CD:Card Condition (ID 40001)** — for ungraded cards: `Near mint or better` (400010), `Excellent` (400011), `Very good` (400012), `Poor` (400013).

Multiple Condition Descriptor values in one cell are separated by `|` (pipe).

The CD/CDA columns expect either the human-readable label OR the numeric ID. Pick one approach and stick with it.

### 3.7 Item Specifics (C: prefix)

Columns prefixed `C:` are eBay Item Specifics — searchable structured attributes. The Sports Cards template includes 35+ of them. For a card scanner, populate at least:

- `*C:Sport` (required) — `Baseball`, `Basketball`, `Football`, etc.
- `C:Player/Athlete` — single name
- `C:Year Manufactured` — 4-digit year
- `C:Manufacturer` — `Topps`, `Panini`, `Upper Deck`, `Bowman`, `Fleer`, `Donruss`, etc.
- `C:Set` — set name
- `C:Card Number` — alphanumeric (`#4`, `#250`, `RC-12`)
- `C:Team` — team name
- `C:League` — `MLB`, `NBA`, `NFL`, `NHL`, etc.
- `C:Graded` — `Yes` or `No`
- `C:Professional Grader` — same options as CD:Professional Grader if `Graded=Yes`
- `C:Grade` — numeric grade if graded
- `C:Autographed` — `Yes` or `No`
- `C:Parallel/Variety` — `Holo`, `Refractor`, `Prizm`, `Base`, etc.
- `C:Features` — `Rookie Card`, `Insert`, `Serial Numbered`, etc. (semicolon-delimited for multiple)

eBay's Info rows in the template list the recommended values for each — match these strings exactly when possible to maximize search visibility.

### 3.8 Images

- Column: **`PicURL`** (not `Item photo URL` — that's the Drafts template name)
- Multiple URLs in one cell, separated by `|` (pipe)
- ≤ 24 URLs per listing
- HTTPS, publicly accessible, no auth
- Spaces in URLs must be encoded as `%20`
- ≥ 1000px on the longest side recommended
- `.jpg` preferred

Example: `https://i.ibb.co/abc/charizard-front.jpg|https://i.ibb.co/abc/charizard-back.jpg`

### 3.9 Example row (graded sports card)

Skipping ~30 columns of empty Item Specifics for brevity:

```csv
Add,SPRT-MJ-001,261328,,1986 Fleer Michael Jordan Rookie Card #57 PSA 8,,,,,,4000,Professional Sports Authenticator (PSA)|275013,8|275024,12345678,,Basketball,Michael Jordan,1986,1986,Fleer,,Base,Rookie Card,1986 Fleer Basketball,Chicago Bulls,NBA,No,Jordan,57,...,https://i.ibb.co/abc/jordan-front.jpg|https://i.ibb.co/abc/jordan-back.jpg,Gallery,,<p>1986 Fleer Michael Jordan Rookie #57. PSA 8 Near Mint-Mint.</p>,FixedPrice,GTC,5000,,FALSE,,,1,,45202,Calculated,,,,2,,,ReturnsAccepted,Days_30,MoneyBack,Buyer,...
```

### 3.10 eBay-specific gotchas

1. **ConditionID must be numeric for live listings** but a word (`NEW`/`USED`) for drafts. Don't mix them up.
2. **Format is `FixedPrice`** — one word, no space.
3. **Duration uses `Days_N` format**, not bare numbers.
4. **`StartPrice` accepts decimals** unlike Whatnot's Price. Don't round here.
5. **Don't strip the `#INFO` rows** at the top of the file.
6. **Some MacOS Excel installs add a BOM (ï»¿) to the first cell.** If generating from macOS, ensure the first character of the file is `I` (of `Info`), not the BOM.
7. **The Action column must be column 0** — even though other columns can be reordered, Action must be first.
8. **Item ID columns are 12-digit numbers** that Excel will mangle as scientific notation. When generating CSV programmatically this isn't an issue; just be aware that hand-edits via Excel can corrupt them.
9. **Condition Descriptor pipe delimiter for multi-value cells: `|`**, not comma.

---

## 4. Field Mapping Matrix — Source → Whatnot → eBay

Use this as the canonical translation table when implementing the exporters:

| `CardRecord` field | Whatnot column | eBay column |
|---|---|---|
| `title` | `Title` (truncate to 80) | `*Title` (truncate to 80) |
| `sport_or_game` | maps to `Category` / `Sub Category` (see §4.1) | `*C:Sport` |
| `set_name` | inline in Description | `C:Set` |
| `card_number` | inline in Title | `C:Card Number` |
| `year` | inline in Title | `C:Year Manufactured` |
| `manufacturer` | inline in Title | `C:Manufacturer` |
| `player_or_character` | inline in Title | `C:Player/Athlete` |
| `condition_raw` | mapped via §4.2 → `Condition` | `*ConditionID` (mapped via §3.5) |
| `is_graded` | influences `Condition` choice | `C:Graded` (Yes/No), drives ConditionID 4000 |
| `grader` | (descriptive only) | `CD:Professional Grader` + `C:Professional Grader` |
| `grade` | (descriptive only) | `CD:Grade` + `C:Grade` |
| `cert_number` | (descriptive only) | `CDA:Certification Number` |
| `price_usd` | `Price` (= `max(1, round(price))`) | `*StartPrice` (= `f"{price:.2f}"`) |
| `quantity` | `Quantity` | `*Quantity` |
| `sku` | `SKU` | `CustomLabel` |
| `image_urls[0..7]` | `Image URL 1..8` (separate columns) | `PicURL` (single column, pipe-joined) |
| `description_html` | `Description` (HTML allowed) | `*Description` (HTML allowed) |

### 4.1 Whatnot category mapping for cards

| Card type | Whatnot Category | Whatnot Sub Category |
|---|---|---|
| Pokémon | `Trading Card Games` | `Pokémon Cards` |
| Magic: The Gathering | `Trading Card Games` | `Magic: The Gathering` |
| Yu-Gi-Oh! | `Trading Card Games` | `Yu-Gi-Oh! Cards` |
| Lorcana | `Trading Card Games` | `Lorcana` |
| One Piece TCG | `Trading Card Games` | `One Piece Cards` |
| Sports cards (any) | `Sports Cards` | `Baseball Singles` / `Basketball Singles` / `Football Singles` / etc. |
| Sealed sports boxes | `Sports Cards` | `Sealed Boxes` (verify in JSON) |
| Comic / TV / movie cards | `Entertainment Cards` | (varies) |

For the full sub-category list per category, query `whatnot_values.json → subcategories[Category]`.

### 4.2 Condition mapping (heuristic, source → Whatnot)

The Whatnot Condition list varies per sub-category. Build a tiered fallback:

```python
EBAY_TO_WHATNOT_CONDITION_PREFS = {
    # eBay raw text (lowercased) → ordered Whatnot preferences
    r'\bbrand new\b|\bnew with tags?\b|\bnwt\b|\bsealed\b|^new$': ['New', 'Sealed', 'Brand New', 'Mint'],
    r'\bnew without\b|\bnwot\b|\bopen box\b':                    ['New', 'Mint', 'Near Mint'],
    r'\blike new\b':                                              ['Mint', 'Near Mint', 'Excellent', 'New'],
    r'\bmint\b':                                                  ['Mint', 'Near Mint'],
    r'\bnear mint\b':                                             ['Near Mint', 'Mint', 'Excellent'],
    r'\bexcellent\b|\bvery good\b':                               ['Excellent', 'Near Mint', 'Very Good', 'Good', 'Light Played'],
    r'\bgood\b':                                                  ['Good', 'Light Played', 'Used', 'Lightly Used'],
    r'\bused\b|\bpre[- ]?owned\b':                                ['Used', 'Good', 'Light Played', 'Pre-Owned'],
    r'\bacceptable\b|\bfair\b':                                   ['Fair', 'Moderately Played', 'Played', 'Used', 'Heavily Played'],
    r'\bfor parts\b|\bnot working\b|\bpoor\b|\bdamaged\b':        ['Damaged', 'Heavily Played', 'Poor', 'For Parts', 'Played'],
}

def map_to_whatnot_condition(raw: str, allowed: list[str]) -> str | None:
    if not raw or not allowed: return None
    raw_l = raw.lower()
    # Exact match first
    for c in allowed:
        if c.lower() == raw_l: return c
    # Heuristic ladder
    for pattern, prefs in EBAY_TO_WHATNOT_CONDITION_PREFS.items():
        if re.search(pattern, raw_l):
            for pref in prefs:
                hit = next((c for c in allowed if c.lower() == pref.lower()), None)
                if hit: return hit
    return None  # caller falls back to a default
```

For graded cards: `is_graded=True` should always map Whatnot Condition to `Graded` when that value exists in the allowed list (most card sub-categories include it).

### 4.3 ConditionID mapping (source → eBay)

```python
def to_ebay_condition_id(record: CardRecord) -> int:
    if record.is_graded:
        return 4000  # Graded
    raw = (record.condition_raw or "").lower()
    if any(t in raw for t in ['new', 'mint', 'sealed', 'brand new']):
        return 1000  # New
    if any(t in raw for t in ['for parts', 'damaged', 'not working']):
        return 7000  # For parts or not working
    return 3000      # Used (sensible default for raw cards)
```

---

## 5. Implementation Guidance

### 5.1 Suggested module structure

```
exporters/
├── __init__.py
├── models.py              # CardRecord dataclass
├── whatnot_values.json    # Reference data (this folder)
├── whatnot.py             # WhatnotExporter
├── ebay.py                # EbayExporter
├── ebay_template.csv      # The template's header + Info rows (preserved as-is)
└── tests/
    ├── test_whatnot.py
    └── test_ebay.py
```

### 5.2 Whatnot exporter sketch

```python
import csv
import json
from pathlib import Path

WHATNOT_COLUMNS = [
    "Category", "Sub Category", "Title", "Description", "Quantity", "Type", "Price",
    "Shipping Profile", "Offerable", "Hazmat", "Condition", "Cost Per Item", "SKU",
    "Image URL 1", "Image URL 2", "Image URL 3", "Image URL 4",
    "Image URL 5", "Image URL 6", "Image URL 7", "Image URL 8",
]
VALID_TYPES = {"Auction", "Buy it Now", "Giveaway"}  # Note lowercase 'it'

class WhatnotExporter:
    def __init__(self, values_json_path: Path):
        self.values = json.loads(values_json_path.read_text(encoding='utf-8'))

    def conditions_for(self, category: str, subcategory: str | None) -> list[str]:
        if subcategory and subcategory in self.values["conditions"]:
            return self.values["conditions"][subcategory]
        if category in self.values["conditions"]:
            return self.values["conditions"][category]
        return []

    def serialize_row(self, card: CardRecord, *, category: str, subcategory: str,
                      shipping_profile: str, listing_type: str = "Buy it Now") -> dict:
        # Validate enum inputs
        assert category in self.values["categories"], f"Invalid category: {category}"
        assert listing_type in VALID_TYPES, f"Invalid type: {listing_type}"
        assert shipping_profile in self.values["shipping_profiles"] or shipping_profile, \
            f"Invalid shipping profile: {shipping_profile}"

        # Map condition with fallback
        allowed = self.conditions_for(category, subcategory)
        condition = "Graded" if card.is_graded and "Graded" in allowed \
                    else map_to_whatnot_condition(card.condition_raw, allowed) \
                    or (allowed[0] if allowed else "")

        # Whole-dollar integer price — non-negotiable
        price_int = max(1, round(card.price_usd))

        # Up to 8 image URLs across separate columns
        images = (card.image_urls + [""] * 8)[:8]

        row = {
            "Category":         category,
            "Sub Category":     subcategory or "",
            "Title":            card.title[:80],
            "Description":      card.description_html or self._build_description(card),
            "Quantity":         card.quantity,
            "Type":             listing_type,
            "Price":            price_int,
            "Shipping Profile": shipping_profile,
            "Offerable":        "TRUE" if listing_type == "Buy it Now" else "",
            "Hazmat":           "Not Hazmat",
            "Condition":        condition,
            "Cost Per Item":    "",
            "SKU":              card.sku or "",
        }
        for i, url in enumerate(images, start=1):
            row[f"Image URL {i}"] = url
        return row

    def write(self, cards: list[CardRecord], out_path: Path, **defaults) -> None:
        with out_path.open("w", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=WHATNOT_COLUMNS)
            writer.writeheader()
            for card in cards:
                writer.writerow(self.serialize_row(card, **defaults))

    def _build_description(self, card: CardRecord) -> str:
        parts = [card.title]
        if card.is_graded and card.grader and card.grade:
            parts.append(f"Graded {card.grader} {card.grade}.")
        if card.condition_raw:
            parts.append(f"Condition: {card.condition_raw}.")
        return " ".join(parts)
```

### 5.3 eBay exporter sketch

```python
class EbayExporter:
    """
    Generates the Sports Trading Cards 'Create new listings' CSV.
    Requires the original template file's first 6 lines (Info + header + blanks)
    to be saved as `ebay_template_header.csv` — the exporter prepends them verbatim.
    """

    def __init__(self, template_header_path: Path, *, location: str,
                 dispatch_time_max: int = 2,
                 returns_accepted: bool = True):
        self.template_header = template_header_path.read_text(encoding='utf-8')
        # Parse the column order from the second line of the template
        lines = self.template_header.splitlines()
        self.columns = next(csv.reader([lines[1]]))
        self.location = location
        self.dispatch_time_max = dispatch_time_max
        self.returns_accepted = returns_accepted

    def serialize_row(self, card: CardRecord, *, category_id: str = "261328",
                      listing_format: str = "FixedPrice",
                      duration: str = "GTC") -> dict:
        cond_id = to_ebay_condition_id(card)

        row = {col: "" for col in self.columns}
        action_col = next(c for c in self.columns if c.startswith("*Action"))

        row[action_col]              = "Add"
        row["CustomLabel"]           = card.sku or ""
        row["*Category"]             = category_id
        row["*Title"]                = card.title[:80]
        row["*ConditionID"]          = str(cond_id)
        row["*C:Sport"]              = card.sport_or_game or ""
        row["C:Player/Athlete"]      = card.player_or_character or ""
        row["C:Year Manufactured"]   = str(card.year) if card.year else ""
        row["C:Manufacturer"]        = card.manufacturer or ""
        row["C:Set"]                 = card.set_name or ""
        row["C:Card Number"]         = card.card_number or ""
        row["C:Graded"]              = "Yes" if card.is_graded else "No"

        if card.is_graded:
            if card.grader:     row["C:Professional Grader"] = card.grader
            if card.grade:      row["C:Grade"] = card.grade
            if card.cert_number: row["C:Certification Number"] = card.cert_number
            if card.grader:     row["CD:Professional Grader - (ID: 27501)"] = card.grader
            if card.grade:      row["CD:Grade - (ID: 27502)"] = card.grade
            if card.cert_number: row["CDA:Certification Number - (ID: 27503)"] = card.cert_number

        row["PicURL"]                = "|".join(self._encode_url(u) for u in card.image_urls[:24])
        row["GalleryType"]           = "Gallery"
        row["*Description"]          = card.description_html or self._build_description(card)
        row["*Format"]               = listing_format
        row["*Duration"]             = duration
        row["*StartPrice"]           = f"{card.price_usd:.2f}"
        row["*Quantity"]             = str(card.quantity)
        row["*Location"]             = self.location
        row["*DispatchTimeMax"]      = str(self.dispatch_time_max)
        row["*ReturnsAcceptedOption"] = "ReturnsAccepted" if self.returns_accepted else "ReturnsNotAccepted"
        if self.returns_accepted:
            row["ReturnsWithinOption"]      = "Days_30"
            row["RefundOption"]             = "MoneyBack"
            row["ShippingCostPaidByOption"] = "Buyer"
        return row

    @staticmethod
    def _encode_url(url: str) -> str:
        return url.replace(" ", "%20")

    def write(self, cards: list[CardRecord], out_path: Path, **defaults) -> None:
        with out_path.open("w", encoding="utf-8", newline="") as f:
            f.write(self.template_header)
            if not self.template_header.endswith("\n"):
                f.write("\n")
            writer = csv.DictWriter(f, fieldnames=self.columns)
            for card in cards:
                writer.writerow(self.serialize_row(card, **defaults))

    def _build_description(self, card: CardRecord) -> str:
        # Same pattern as Whatnot — but wrap in <p>...</p>
        ...
```

### 5.4 Validation patterns

Add a pre-flight check before writing each file. Cheap to do, saves a round-trip through eBay's error report.

```python
def validate_whatnot_row(row: dict, values: dict) -> list[str]:
    errors = []
    if row["Category"] not in values["categories"]:
        errors.append(f"Invalid Category: {row['Category']!r}")
    if row["Sub Category"] and row["Category"] in values["subcategories"]:
        if row["Sub Category"] not in values["subcategories"][row["Category"]]:
            errors.append(f"Invalid Sub Category for {row['Category']}: {row['Sub Category']!r}")
    if row["Type"] not in {"Auction", "Buy it Now", "Giveaway"}:
        errors.append(f"Invalid Type: {row['Type']!r}")
    try:
        p = int(row["Price"])
        if p < 1: errors.append(f"Price must be >= 1, got {p}")
        if str(row["Price"]) != str(p):
            errors.append(f"Price must be integer (no decimals): {row['Price']!r}")
    except (ValueError, TypeError):
        errors.append(f"Price not a valid integer: {row['Price']!r}")
    if not row["Title"] or len(row["Title"]) > 80:
        errors.append(f"Title length out of range (1..80): {len(row['Title'])}")
    if row["Shipping Profile"] not in values["shipping_profiles"]:
        # Could be a custom profile; warn rather than error
        pass
    return errors

def validate_ebay_row(row: dict) -> list[str]:
    errors = []
    if not row.get("*Title") or len(row["*Title"]) > 80:
        errors.append("Title missing or > 80 chars")
    if row.get("*Format") not in {"Auction", "FixedPrice"}:
        errors.append(f"Invalid Format: {row.get('*Format')!r}")
    if row.get("*Duration") not in {"Days_3", "Days_5", "Days_7", "Days_10", "Days_30", "GTC"}:
        errors.append(f"Invalid Duration: {row.get('*Duration')!r}")
    try:
        float(row.get("*StartPrice", ""))
    except (ValueError, TypeError):
        errors.append(f"Invalid StartPrice: {row.get('*StartPrice')!r}")
    return errors
```

### 5.5 Testing strategy

1. **Unit tests on the serializer** — feed in known `CardRecord` instances, assert exact CSV output bytes.
2. **Round-trip test** — generate a CSV, parse it back with `csv.DictReader`, confirm field values survive escaping.
3. **Manual smoke test** — generate a 2-row CSV (one graded card, one raw card). Upload to Whatnot/eBay test/staging or upload as a draft. Inspect the result.
4. **For eBay specifically:** start with `Action=VerifyAdd` instead of `Add` for the first batch — this validates the listing without creating it. Switch to `Add` once clean.

---

## 6. Operational Notes

- **Whatnot draft listings:** every uploaded CSV creates *draft* listings on Whatnot. Sellers review and publish them in the Inventory page. Re-uploading the same CSV will create duplicate drafts.
- **eBay live listings:** the `Add` action creates active listings immediately and incurs insertion fees per listing. Use `Draft` template or `VerifyAdd` action during integration testing to avoid surprises.
- **eBay drafts:** eBay's Drafts folder holds up to 1,000 drafts; they expire in 120 days. Don't accumulate more than the cap.
- **Image URL freshness:** ImgBB URLs are persistent if the seller doesn't delete them. Both Whatnot and eBay re-fetch images, so the URLs need to remain accessible at least until the listing publishes.
- **Title overlap is fine:** the same `CardRecord` can produce identical titles on both platforms; both systems accept duplicates within an account.
- **Pricing parity:** Whatnot rounds to whole dollars, eBay uses decimals. If platform-pricing parity matters, drive both from a `price_usd` field rounded to whole dollars at the source.

---

## 7. Validation Checklist

Before shipping the exporters, verify each item:

**Whatnot CSV**
- [ ] Header row has exactly 21 columns in the specified order
- [ ] Every `Category` is in `whatnot_values.json → categories`
- [ ] Every non-empty `Sub Category` is valid for its `Category`
- [ ] Every `Type` is one of `Auction`, `Buy it Now`, `Giveaway` (with lowercase `it`)
- [ ] Every `Price` is an integer ≥ 1 (no decimals, no `.00` suffix)
- [ ] Every `Shipping Profile` matches a Whatnot bucket name OR a known custom profile
- [ ] Every `Hazmat` is one of the 3 valid values
- [ ] `Offerable` is blank for non-`Buy it Now` rows
- [ ] All Image URLs use HTTPS and load in an Incognito browser

**eBay CSV**
- [ ] First line: `Info,Version=...,Template=fx_category_template_EBAY_US`
- [ ] Second line: column header beginning with `*Action(SiteID=...)`
- [ ] `Info,>>>` rows preserved between header and data
- [ ] Every data row's Action is `Add` (or `VerifyAdd` during testing)
- [ ] Every `*Category` is a numeric leaf node ID (e.g. `261328`)
- [ ] Every `*ConditionID` is a numeric ID matching the category's allowed conditions
- [ ] Every `*Format` is `Auction` or `FixedPrice`
- [ ] Every `*Duration` is `Days_3` / `Days_5` / `Days_7` / `Days_10` / `Days_30` / `GTC`
- [ ] Every `*StartPrice` parses as a float
- [ ] Every `*Quantity` is a positive integer
- [ ] All `PicURL` values use HTTPS, are pipe-delimited, with spaces encoded as `%20`
- [ ] Title length ≤ 80 chars on every row
- [ ] No BOM (ï»¿) at the start of the file

---

## 8. Reference Data

### 8.1 Whatnot

Load from companion file `whatnot_values.json`:

```json
{
  "categories":        [ /* 37 strings */ ],
  "shipping_profiles": [ /* 23 strings */ ],
  "hazmat":            [ "Not Hazmat", "Hazmat - Standard", "Hazmat - Lithium Battery" ],
  "subcategories":     { "Trading Card Games": ["Pokémon Cards", "Magic: The Gathering", ...], ... },
  "conditions":        { "Pokémon Cards": ["Graded", "New", "Mint", "Near Mint", "Light Played", ...], ... }
}
```

Key facts:
- 37 categories; 31 of them have sub-categories (the other 6 leave Sub Category blank)
- 264 sub-categories total; largest group is `Toys & Hobbies` with 37
- 941 condition entries across 153 keys; 141 keyed by sub-category, 12 by category
- 23 shipping profiles spanning oz, lb, gram, and KG buckets
- "Type" enum: `Auction`, `Buy it Now`, `Giveaway`

### 8.2 eBay

**Common Trading Card category IDs:**

| Category | ID |
|---|---|
| Sports Trading Cards | `261328` |
| Trading Card Games > CCG Individual Cards | `183454` |
| Trading Card Games > CCG Sealed Booster Boxes | `261339` |
| Non-Sport Trading Cards | `870` |

Find others at: https://pages.ebay.com/sellerinformation/news/categorychanges.html

**ConditionID quick reference (Sports Trading Cards):**

| Code | Meaning |
|---|---|
| `1000` | New |
| `2750` | Like New |
| `3000` | Used |
| `4000` | Graded |
| `5000` | Ungraded |
| `7000` | For parts or not working |

Full list: https://developer.ebay.com/DevZone/finding/CallRef/Enums/conditionIdList.html

**Duration enum:**

```
Days_3, Days_5, Days_7, Days_10, Days_30, GTC
```

(`GTC` = Good Til Cancelled, fixed-price only)

**Format enum:**

```
Auction, FixedPrice
```

(no space, exact case)

---

*End of specification. The implementation should target this document; deviations from any rule above are likely upload failures waiting to happen.*
