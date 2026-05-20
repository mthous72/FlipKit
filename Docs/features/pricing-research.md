# Pricing Research

## Overview

Accurate pricing is critical for Whatnot sales. This document covers:
1. Finding comparable sales (comps)
2. Understanding Whatnot fees
3. Setting competitive prices
4. Tools for price research

---

## Whatnot Fee Structure

Total fees are approximately **11%** of sale price:

| Fee | Amount |
|-----|--------|
| Commission | 8% of sale price |
| Payment Processing | 2.9% + $0.30 |
| **Total** | ~11% |

### Net Calculation
```
Net Payout = Sale Price × 0.89 - $0.30

Example: $10 sale
  Net = $10 × 0.89 - $0.30 = $8.60
```

### Break-Even Formula
```
Minimum Price = (Cost + $0.30) / 0.89

Example: $5 cost basis
  Min Price = ($5 + $0.30) / 0.89 = $5.96
```

---

## Finding Sold Comps

### Option 1: Terapeak (Best for eBay Sellers) ⭐

Terapeak is free for eBay sellers and shows:
- Actual sold prices (including Best Offer accepted)
- Up to 1 year of historical data
- More accurate than public eBay search

**Access:** https://www.ebay.com/sh/research (requires eBay seller account)

### Terapeak URL Generator

```python
import urllib.parse
import webbrowser

def build_terapeak_url(card: dict) -> str:
    """Generate Terapeak research URL for a card."""
    parts = []
    
    if card.get("year"):
        parts.append(str(card["year"]))
    if card.get("brand"):
        parts.append(card["brand"])
    if card.get("player_name"):
        parts.append(card["player_name"])
    if card.get("parallel_name"):
        parts.append(card["parallel_name"])
    
    query = " ".join(parts)
    encoded = urllib.parse.quote(query)
    
    return (
        f"https://www.ebay.com/sh/research"
        f"?marketplace=EBAY-US"
        f"&keywords={encoded}"
        f"&tabName=SOLD"
    )


def open_terapeak(card: dict):
    """Open Terapeak search in browser for pricing research."""
    url = build_terapeak_url(card)
    webbrowser.open(url)
    print(f"Opened Terapeak for: {card.get('player_name')}")
    print(f"URL: {url}")
```

**Usage:**
```python
card = {
    "year": 2023,
    "brand": "Prizm", 
    "player_name": "Justin Jefferson",
    "parallel_name": "Silver"
}
open_terapeak(card)
# Opens: https://www.ebay.com/sh/research?marketplace=EBAY-US&keywords=2023%20Prizm%20Justin%20Jefferson%20Silver&tabName=SOLD
```

---

### Option 2: eBay Sold Search (Public)

For non-sellers or quick lookups:

1. Go to eBay.com
2. Search: `{year} {brand} {player} {parallel} #{number}`
3. Click **"Sold Items"** filter (left sidebar)
4. Review last 90 days of sales

**Note:** Public eBay search shows strikethrough prices for Best Offer sales, not the actual accepted amount. Terapeak shows the real price.

### eBay Sold URL Generator

```
https://www.ebay.com/sch/i.html?_nkw={query}&_sacat=261328&LH_Sold=1&LH_Complete=1
```

Parameters:
- `_nkw` = search keywords (URL encoded)
- `_sacat=261328` = Sports Trading Cards category
- `LH_Sold=1` = Show sold items only
- `LH_Complete=1` = Show completed listings

### Example Function

```python
import urllib.parse

def build_ebay_url(card: dict) -> str:
    """Generate eBay sold search URL for a card."""
    parts = []
    
    if card.get("year"):
        parts.append(str(card["year"]))
    if card.get("brand"):
        parts.append(card["brand"])
    if card.get("player_name"):
        parts.append(card["player_name"])
    if card.get("parallel_name"):
        parts.append(card["parallel_name"])
    if card.get("card_number"):
        parts.append(f"#{card['card_number']}")
    
    query = " ".join(parts)
    encoded = urllib.parse.quote(query)
    
    return (
        f"https://www.ebay.com/sch/i.html"
        f"?_nkw={encoded}"
        f"&_sacat=261328"
        f"&LH_Sold=1"
        f"&LH_Complete=1"
    )
```

---

## Other Price Research Tools

### 130point.com (Free)
Aggregates eBay sold data with easier filtering.

```
https://130point.com/sales/?search={query}
```

### SportsCardsPro.com ($6-20/mo)
- Price guides by set
- API access (Legendary tier)
- CSV downloads

### Card Ladder ($)
- Historical price trends
- Player indexes
- Population reports

### eBay App Price Guide (Free)
- Scan cards with phone camera
- Shows recent comps
- Limited to graded cards currently

---

## Pricing Strategy for Whatnot

### Base Card Pricing
```
Raw base cards: Price at 75-85% of eBay comps
  - Whatnot buyers expect deals
  - Faster sales = better for your metrics
```

### Parallel/Insert Pricing
```
Numbered parallels: Price at 85-95% of comps
  - Scarcity holds value
  - Collectors actively searching
```

### Rookie Cards
```
Hot rookies: Price at 90-100% of comps
  - High demand on Whatnot
  - Can price competitively and sell quickly
```

### Graded Cards
```
PSA 10 / BGS 9.5+: Price at 95-100% of comps
  - Graded cards have defined value
  - Buyers expect fair pricing
```

---

## Price Suggestion Algorithm

```python
def suggest_price(estimated_value: float, card: dict) -> float:
    """
    Suggest a listing price based on market value and card attributes.
    Accounts for Whatnot fees and competitive positioning.
    """
    
    # Start with market value
    price = estimated_value
    
    # Adjust for card type
    variation = card.get("variation_type", "Base").lower()
    
    if variation == "base":
        price *= 0.80  # Price base cards competitively
    elif card.get("serial_numbered"):
        # Numbered cards hold value better
        serial = card["serial_numbered"].replace("/", "")
        if serial.isdigit():
            num = int(serial)
            if num <= 10:
                price *= 0.95
            elif num <= 25:
                price *= 0.92
            else:
                price *= 0.88
    else:
        price *= 0.85  # Standard parallel
    
    # Boost for special attributes
    if card.get("is_rookie"):
        price *= 1.05
    if card.get("is_auto"):
        price *= 1.02
    
    # Round to nice price points
    if price >= 100:
        price = round(price / 5) * 5      # Nearest $5
    elif price >= 20:
        price = round(price)              # Nearest $1
    elif price >= 5:
        price = round(price * 2) / 2      # Nearest $0.50
    else:
        price = round(price, 2)           # Keep cents
    
    # Minimum viable price
    return max(price, 0.99)
```

---

## Pricing Workflow

### For Each Card:

1. **Open Terapeak** (or eBay Sold if not an eBay seller)
   ```python
   open_terapeak(card)  # Opens browser automatically
   ```

2. **Review 5-10 recent sold listings in Terapeak**
   - Note the price range
   - Look for "Avg Sold Price" stat
   - Consider card condition differences
   - Ignore outliers (damaged cards, rare variants)

3. **Enter estimated market value**
   ```python
   estimated_value = float(input("Estimated value: $"))
   ```

4. **Get suggested price**
   ```python
   suggested = suggest_price(estimated_value, card)
   print(f"Suggested listing price: ${suggested:.2f}")
   ```

5. **Confirm or adjust**
   ```python
   final_price = float(input(f"Listing price (Enter for ${suggested}): ") or suggested)
   ```

6. **Save to database**
   ```python
   update_card(card_id, {
       "estimated_value": estimated_value,
       "listing_price": final_price,
       "price_source": "Terapeak",
       "price_date": datetime.now().strftime("%Y-%m-%d")
   })
   ```

---

## Batch Pricing Tips

For efficiency when pricing 50+ cards:

1. **Sort by set** — Similar cards have similar values
2. **Price hot players first** — Easier to find comps
3. **Use price bands** — "$1-3", "$5-10", "$10-25", "$25+"
4. **Skip deep research on cheap cards** — Time vs. value tradeoff
5. **Flag unknowns** — Mark cards needing more research

---

## Price Update Triggers

Consider repricing when:
- Card listed for 30+ days without sale
- Player has big game / major news
- New set releases (older parallels may drop)
- Season changes (football cards dip in spring)
