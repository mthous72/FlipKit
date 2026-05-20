# eBay API Integration

## The Reality: What You Can and Can't Do

### ❌ What You CAN'T Do (Anymore)

**Sold/Completed Listings Data** — The ability to programmatically access sold item data has been deprecated and restricted:

- The Finding API's `findCompletedItems` call has been deprecated and was decommissioned on February 5, 2025
- The Marketplace Insights API (which provides sold data) is only available to "high-end developers" like Terapeak — not regular developers
- The license agreement that developers sign forbids market research

**Bottom line:** You cannot build an automated price checker that pulls sold comps via eBay's public APIs.

### ✅ What You CAN Do

**1. Search Active Listings** — The Browse API lets you search currently listed items:

- Search by keyword, category, price range
- Filter by condition, buying format, location
- Get current asking prices (not sold prices)
- Up to 5,000 API calls/day, 100 items per call

**2. Get Item Details** — Retrieve full details for specific listings

**3. Search by Image** — Find similar items using an image (useful for card identification)

---

## Use Cases for Your Card Lister

### Use Case 1: Competitive Pricing Check

See what similar cards are **currently listed** for (not what they sold for):

```
"What are other sellers asking for 2023 Prizm Justin Jefferson Silver?"
```

**Helpful for:**
- Seeing if you're pricing too high/low vs active competition
- Finding gaps in the market
- Checking listing density (how many of this card are listed)

**Limitations:**
- Active listings ≠ sold prices
- Sellers often list cards at unrealistic prices
- Doesn't tell you what actually sells

### Use Case 2: Image-Based Card Lookup

Use the `search_by_image` endpoint to find matching cards:

```
Upload card image → Get matching eBay listings
```

**Helpful for:**
- Identifying unknown cards
- Finding exact matches to compare against
- Cross-referencing your scanned card data

### Use Case 3: Category/Aspect Research

Get category IDs and valid aspects for sports cards:

| Category | ID |
|----------|-----|
| Sports Trading Cards | 212 |
| Baseball Cards | 213 |
| Basketball Cards | 214 |
| Football Cards | 215 |

---

## Browse API Setup

### 1. Create eBay Developer Account

1. Go to https://developer.ebay.com/
2. Sign in with your eBay account (or create one)
3. Go to "My Account" → "Application Keys"
4. Create a new application (Production)
5. Note your **Client ID** (App ID) and **Client Secret** (Cert ID)

### 2. Environment Variables

```bash
export EBAY_CLIENT_ID="your-client-id"
export EBAY_CLIENT_SECRET="your-client-secret"
```

### 3. Get OAuth Token

```python
import requests
import base64

def get_ebay_token(client_id: str, client_secret: str) -> str:
    """Get OAuth token for eBay Browse API."""
    
    # Encode credentials
    credentials = f"{client_id}:{client_secret}"
    encoded = base64.b64encode(credentials.encode()).decode()
    
    response = requests.post(
        "https://api.ebay.com/identity/v1/oauth2/token",
        headers={
            "Content-Type": "application/x-www-form-urlencoded",
            "Authorization": f"Basic {encoded}"
        },
        data={
            "grant_type": "client_credentials",
            "scope": "https://api.ebay.com/oauth/api_scope"
        }
    )
    
    response.raise_for_status()
    return response.json()["access_token"]
```

### 4. Search Active Listings

```python
def search_ebay_active(query: str, category_id: str = "212", limit: int = 25) -> list:
    """
    Search eBay for currently active listings.
    
    Args:
        query: Search keywords
        category_id: eBay category (212 = Sports Trading Cards)
        limit: Max results (1-200)
    
    Returns:
        List of item summaries
    """
    token = get_ebay_token(EBAY_CLIENT_ID, EBAY_CLIENT_SECRET)
    
    response = requests.get(
        "https://api.ebay.com/buy/browse/v1/item_summary/search",
        headers={
            "Authorization": f"Bearer {token}",
            "X-EBAY-C-MARKETPLACE-ID": "EBAY_US"
        },
        params={
            "q": query,
            "category_ids": category_id,
            "limit": limit,
            "filter": "buyingOptions:{FIXED_PRICE}"  # BIN listings only
        }
    )
    
    response.raise_for_status()
    data = response.json()
    
    results = []
    for item in data.get("itemSummaries", []):
        results.append({
            "title": item.get("title"),
            "price": item.get("price", {}).get("value"),
            "currency": item.get("price", {}).get("currency"),
            "condition": item.get("condition"),
            "item_url": item.get("itemWebUrl"),
            "image_url": item.get("image", {}).get("imageUrl"),
            "seller": item.get("seller", {}).get("username"),
        })
    
    return results
```

### 5. Build Search Query from Card Data

```python
def build_ebay_query(card: dict) -> str:
    """Build optimized search query from card data."""
    parts = []
    
    if card.get("year"):
        parts.append(str(card["year"]))
    
    if card.get("brand"):
        parts.append(card["brand"])
    
    if card.get("player_name"):
        parts.append(card["player_name"])
    
    if card.get("parallel_name"):
        parts.append(card["parallel_name"])
    
    # Don't include card number - too specific, may miss matches
    
    return " ".join(parts)


def get_competitive_prices(card: dict) -> dict:
    """
    Get current competitive pricing for a card.
    
    Returns stats on active listings (NOT sold prices).
    """
    query = build_ebay_query(card)
    
    # Map sport to category
    category_map = {
        "football": "215",
        "baseball": "213", 
        "basketball": "214",
    }
    category = category_map.get(card.get("sport", "").lower(), "212")
    
    results = search_ebay_active(query, category, limit=50)
    
    if not results:
        return {"count": 0, "message": "No active listings found"}
    
    prices = [float(r["price"]) for r in results if r.get("price")]
    
    if not prices:
        return {"count": len(results), "message": "No prices available"}
    
    return {
        "count": len(prices),
        "lowest": min(prices),
        "highest": max(prices),
        "average": sum(prices) / len(prices),
        "median": sorted(prices)[len(prices) // 2],
        "query_used": query,
        "note": "These are ASKING prices, not sold prices"
    }
```

---

## API Limits

| Limit | Value |
|-------|-------|
| Calls per day | 5,000 |
| Items per call | 200 max |
| Total items per query | 10,000 max |

For 150 cards, you have plenty of headroom.

---

## Better Alternatives for Sold Prices

Since eBay's API won't give you sold data, here are your options:

### 1. Manual eBay Sold Search (Free)
Generate a URL, open in browser, review manually:
```
https://www.ebay.com/sch/i.html?_nkw={query}&_sacat=212&LH_Sold=1&LH_Complete=1
```

### 2. Terapeak via Seller Hub (Free for eBay sellers)
- Access at: https://www.ebay.com/sh/research
- Shows sold data with actual prices (including Best Offer accepted)
- Manual lookup, not API-accessible

### 3. 130point.com (Free)
- Website: https://130point.com/sales/
- Shows "Best Offer Accepted" prices, perfect for trading cards where Best Offer is the standard
- Manual lookup

### 4. SportsCardsPro API (Paid)
- $6-20/month for API access
- Has bulk pricing data across sets
- CSV downloads available

### 5. Ximilar Collectibles API (Paid)
- Returns eBay listing data including prices and direct links when you identify a card
- Combines card identification with pricing

---

## Recommended Approach for Your Project

### Hybrid Strategy

1. **Card Identification** → OpenRouter vision API (you're already planning this)

2. **Sold Prices (Primary)** → Generate eBay sold search URLs for manual lookup
   - Most accurate
   - Free
   - Shows actual transaction prices

3. **Competitive Check (Optional)** → eBay Browse API
   - See what competitors are asking
   - Sanity check your pricing
   - Automated, no manual lookup

4. **Price Suggestion** → Your algorithm
   - Input: Manual entry of estimated value from sold comps
   - Output: Suggested Whatnot listing price (accounting for fees)

### When to Use eBay API

| Task | Use API? | Why |
|------|----------|-----|
| Find sold prices | ❌ No | API doesn't provide this |
| Check competitor prices | ✅ Yes | See active listing prices |
| Verify card exists | ✅ Yes | Find matching listings |
| Identify by image | ✅ Yes | Image search available |
| Bulk price research | ❌ No | Use Terapeak manually or SportsCardsPro |

---

## Integration into Your Project

Add to your project structure:
```
whatnot-card-lister/
├── ...
├── ebay.py          # eBay Browse API client
└── ...
```

Add to config:
```python
# config.py
EBAY_CLIENT_ID = os.environ.get("EBAY_CLIENT_ID")
EBAY_CLIENT_SECRET = os.environ.get("EBAY_CLIENT_SECRET")
```

Add CLI command:
```bash
# Check competitive pricing for a card
python main.py competition 42

# Output:
# Card: 2023 Panini Prizm Justin Jefferson Silver
# Active eBay Listings: 23
# Price Range: $8.99 - $34.99
# Average Asking: $15.42
# Note: These are asking prices, not sold prices
```

---

## Summary

| What eBay API Gives You | What It Doesn't |
|-------------------------|-----------------|
| Active listing prices | Sold/completed prices |
| Current inventory levels | Historical sales data |
| Competitor asking prices | What actually sells |
| Image-based search | Market research data |

**For your card lister:** eBay API is a nice-to-have for competitive analysis, but **not essential**. Your core pricing workflow should use manually-researched sold comps (via eBay sold search URLs or Terapeak) rather than relying on API data.
