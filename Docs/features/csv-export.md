# Whatnot CSV Format

## Overview

Whatnot supports bulk listing upload via CSV. Each upload creates **new draft listings** — you cannot update existing listings via CSV.

**Source:** https://help.whatnot.com/hc/en-us/articles/7440530071821

---

## CSV Columns

| Column | Required | Type | Notes |
|--------|----------|------|-------|
| Category | ✅ | Text | Must match Whatnot's list exactly |
| Sub Category | ✅ | Text | Must match Whatnot's list exactly |
| Title | ✅ | Text | Listing title (max ~200 chars) |
| Description | ✅ | Text | Listing description |
| Quantity | ✅ | Integer | Minimum 1 |
| Type | ✅ | Text | `Buy It Now`, `Auction`, or `Giveaway` |
| Price | ✅ | Decimal | Starting bid (auction) or BIN price |
| Shipping Profile | ✅ | Text | Must match Whatnot's list or custom profile name |
| Offerable | Optional | Boolean | `TRUE` or `FALSE` — allows offers on BIN |
| Condition | Optional | Text | Item condition |
| Image URL 1 | Optional* | URL | Must be public https:// URL |
| Image URL 2-8 | Optional | URL | Additional images |

*Images required before publishing, but can upload as draft without them.

---

## Valid Values

### Category
For sports cards, always use:
```
Sports Cards
```

### Sub Category (for Sports Cards)
Must be one of:
```
Football Cards
Baseball Cards
Basketball Cards
Hockey Cards
Soccer Cards
Golf Cards
Racing Cards
UFC / MMA Cards
Wrestling Cards
Multi-Sport Cards
Other Sports Cards
```

### Type
```
Buy It Now
Auction
Giveaway
```

### Shipping Profile
Standard weight-based profiles:
```
4 oz
8 oz
1 lb
1 lb 8 oz
2 lb
3 lb
5 lb
10 lb
15 lb
20 lb
```

Or use a **custom shipping profile name** you've created in Whatnot Seller Hub.

### Condition
```
Brand New
Like New
Very Good
Good
Acceptable
Near Mint
Excellent
Poor
```

---

## Title Best Practices

Format:
```
{Year} {Manufacturer} {Brand} {Player} {Parallel} {Variation} #{Card Number}
```

Examples:
```
2023 Panini Prizm Justin Jefferson Silver #88
2024 Topps Chrome Shohei Ohtani Refractor #1
2023 Panini Donruss CJ Stroud Rated Rookie RC #301
2022 Panini Mosaic Patrick Mahomes Gold /10 #1
2023 Panini Prizm Victor Wembanyama RC Silver Auto #1
```

Tips:
- Keep under 200 characters
- Lead with year for easy sorting
- Include "RC" for rookie cards
- Include serial number if numbered (/25, /99, etc.)
- Include "Auto" or "Relic" if applicable

---

## Description Template

```
{Year} {Manufacturer} {Brand} {Player Name}
{Parallel Name} {Variation Type}
Card #{Card Number}

Team: {Team}
Condition: {Condition}
{Serial: /XX if numbered}
{Rookie Card | Autograph | Relic as applicable}

Ships within 2 business days in penny sleeve + top loader + bubble mailer.
```

Example:
```
2023 Panini Prizm Justin Jefferson
Silver Parallel
Card #88

Team: Minnesota Vikings
Condition: Near Mint

Ships within 2 business days in penny sleeve + top loader + bubble mailer.
```

---

## Image URL Requirements

1. Must be **publicly accessible** (no login required)
2. Must use **https://** protocol
3. Must be a **direct image URL** (ends in .jpg, .png, etc.)
4. Test by opening URL in incognito/private browser

**Recommended hosting:**
- ImgBB (free, API available) — https://imgbb.com/
- Imgur (free, API available)
- Cloudinary (free tier)

---

## CSV Generation Rules

1. **Use Google Sheets** to edit the template, then File > Download > CSV
2. **Don't use Excel** to save — can corrupt formatting
3. **UTF-8 encoding** — handle special characters properly
4. **No extra columns** — only include the defined columns
5. **No empty required fields** — will fail validation
6. **Prices as decimals** — use `12.99` not `$12.99`

---

## Sample CSV

```csv
Category,Sub Category,Title,Description,Quantity,Type,Price,Shipping Profile,Offerable,Condition,Image URL 1,Image URL 2
Sports Cards,Football Cards,2023 Panini Prizm Justin Jefferson Silver #88,"2023 Panini Prizm Justin Jefferson Silver Parallel Card #88. Team: Minnesota Vikings. Condition: Near Mint. Ships in penny sleeve + top loader + bubble mailer.",1,Buy It Now,12.99,4 oz,TRUE,Near Mint,https://i.ibb.co/abc123/jefferson-front.jpg,https://i.ibb.co/abc124/jefferson-back.jpg
Sports Cards,Baseball Cards,2024 Topps Chrome Shohei Ohtani Refractor #1,"2024 Topps Chrome Shohei Ohtani Refractor Card #1. Team: Los Angeles Dodgers. Condition: Near Mint. Ships in penny sleeve + top loader + bubble mailer.",1,Buy It Now,8.99,4 oz,TRUE,Near Mint,https://i.ibb.co/def456/ohtani-front.jpg,
Sports Cards,Basketball Cards,2023 Panini Prizm Victor Wembanyama RC Silver #280,"2023 Panini Prizm Victor Wembanyama Rookie Card Silver Parallel #280. Team: San Antonio Spurs. Condition: Near Mint. Rookie Card! Ships in penny sleeve + top loader + bubble mailer.",1,Buy It Now,24.99,4 oz,TRUE,Near Mint,https://i.ibb.co/ghi789/wemby-front.jpg,https://i.ibb.co/ghi790/wemby-back.jpg
```

---

## Export Function Pseudocode

```python
def export_whatnot_csv(cards: list, output_path: str):
    """
    Export cards to Whatnot-compatible CSV.
    
    Args:
        cards: List of card dicts from database
        output_path: Where to save the CSV file
    """
    
    COLUMNS = [
        "Category", "Sub Category", "Title", "Description",
        "Quantity", "Type", "Price", "Shipping Profile",
        "Offerable", "Condition", 
        "Image URL 1", "Image URL 2", "Image URL 3", "Image URL 4",
        "Image URL 5", "Image URL 6", "Image URL 7", "Image URL 8"
    ]
    
    rows = []
    for card in cards:
        row = {
            "Category": "Sports Cards",
            "Sub Category": map_sport_to_subcategory(card["sport"]),
            "Title": generate_title(card),
            "Description": generate_description(card),
            "Quantity": card.get("quantity", 1),
            "Type": card.get("listing_type", "Buy It Now"),
            "Price": f"{card['listing_price']:.2f}",
            "Shipping Profile": card.get("shipping_profile", "4 oz"),
            "Offerable": "TRUE" if card.get("offerable", True) else "FALSE",
            "Condition": card.get("condition", "Near Mint"),
            "Image URL 1": card.get("image_url_1", ""),
            "Image URL 2": card.get("image_url_2", ""),
            # ... etc
        }
        rows.append(row)
    
    # Write CSV
    with open(output_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=COLUMNS)
        writer.writeheader()
        writer.writerows(rows)
```

---

## Upload Process

1. Export CSV from your app
2. Log into Whatnot Seller Hub (web)
3. Go to Inventory → Import Products
4. Upload the CSV file
5. Review imported drafts
6. Add any missing images directly in Whatnot
7. Publish listings

---

## Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| "Invalid category" | Typo in category/subcategory | Use exact values from lists above |
| "Invalid shipping profile" | Typo or non-existent profile | Use standard weights or create custom profile first |
| Images not showing | URL not public or uses http:// | Use https:// and test in incognito |
| Price format error | Using $ symbol or commas | Use plain decimal: `12.99` |
| Upload fails silently | Excel corrupted the CSV | Use Google Sheets → Download CSV |
