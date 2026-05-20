# OpenRouter Integration for Card Scanning

## Overview

Use OpenRouter.ai to access vision-capable models (Claude, GPT-4o, Gemini) for analyzing sports card images and extracting structured data.

---

## OpenRouter Setup

### 1. Get API Key
1. Go to https://openrouter.ai/
2. Create account / sign in
3. Go to https://openrouter.ai/keys
4. Create new API key
5. Add credits (pay-as-you-go)

### 2. Environment Variable
```bash
export OPENROUTER_API_KEY="sk-or-v1-your-key-here"
```

Or in a `.env` file:
```
OPENROUTER_API_KEY=sk-or-v1-your-key-here
```

---

## Recommended Models for Vision

| Model | ID | Cost (input/output per 1M tokens) | Notes |
|-------|----|------------------------------------|-------|
| Claude Sonnet 4 | `anthropic/claude-sonnet-4` | $3 / $15 | Best accuracy |
| GPT-4o | `openai/gpt-4o` | $2.50 / $10 | Good balance |
| GPT-4o mini | `openai/gpt-4o-mini` | $0.15 / $0.60 | Budget option |
| Gemini Flash | `google/gemini-flash-1.5` | $0.075 / $0.30 | Cheapest |

**Recommendation:** Start with `openai/gpt-4o-mini` for testing, use `anthropic/claude-sonnet-4` for production accuracy.

---

## API Request Format

### Endpoint
```
POST https://openrouter.ai/api/v1/chat/completions
```

### Headers
```
Authorization: Bearer {OPENROUTER_API_KEY}
Content-Type: application/json
HTTP-Referer: https://your-app.com  (optional but recommended)
X-Title: Card Scanner  (optional, shows in OpenRouter dashboard)
```

### Request Body (with image)
```json
{
  "model": "anthropic/claude-sonnet-4",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "image_url",
          "image_url": {
            "url": "data:image/jpeg;base64,{BASE64_ENCODED_IMAGE}"
          }
        },
        {
          "type": "text",
          "text": "Your prompt here..."
        }
      ]
    }
  ],
  "max_tokens": 1024
}
```

---

## Vision Prompt for Card Extraction

```
Analyze this sports card image and extract all identifying information.

Return ONLY a JSON object with these exact fields (use null for unknown values):

{
  "player_name": "Full player name",
  "card_number": "Card number without # symbol",
  "year": 2024,
  "sport": "Football|Baseball|Basketball",
  "manufacturer": "Panini|Topps|Upper Deck|Leaf",
  "brand": "Sub-brand (Prizm, Donruss, Chrome, etc.)",
  "set_name": "Full set name if visible",
  "team": "Team name",
  "variation_type": "Base|Parallel|Insert|Refractor|Auto|Relic",
  "parallel_name": "Color/pattern name (Silver, Blue, Gold, etc.) or null",
  "serial_numbered": "Print run as string (/99, /25, 1/1) or null",
  "is_rookie": true|false,
  "is_auto": true|false,
  "is_relic": true|false,
  "is_short_print": true|false,
  "condition_notes": "Any visible condition issues"
}

Identification tips:
- "RC" or "Rated Rookie" logo = rookie card
- Serial numbers are usually printed at bottom (e.g., 045/199)
- Panini brands: Prizm, Donruss, Mosaic, Select, Optic, Contenders, Phoenix
- Topps brands: Chrome, Heritage, Stadium Club, Finest, Bowman, Inception
- Look for rainbow/shimmer effects to identify parallels
- Actual ink/sticker signature = auto
- Jersey swatch or memorabilia piece = relic

Return ONLY the JSON, no other text or markdown.
```

---

## Python Implementation Skeleton

```python
import os
import base64
import requests
import json

OPENROUTER_API_KEY = os.environ.get("OPENROUTER_API_KEY")
OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"

def encode_image(image_path: str) -> str:
    """Read image file and return base64 string."""
    with open(image_path, "rb") as f:
        return base64.standard_b64encode(f.read()).decode("utf-8")

def get_media_type(image_path: str) -> str:
    """Get MIME type from file extension."""
    ext = image_path.lower().split(".")[-1]
    return {
        "jpg": "image/jpeg",
        "jpeg": "image/jpeg",
        "png": "image/png",
        "webp": "image/webp",
    }.get(ext, "image/jpeg")

def scan_card(image_path: str, model: str = "openai/gpt-4o-mini") -> dict:
    """
    Send card image to OpenRouter and extract structured data.
    
    Returns dict with card fields or {"error": "message"}.
    """
    if not OPENROUTER_API_KEY:
        return {"error": "OPENROUTER_API_KEY not set"}
    
    # Encode image
    image_b64 = encode_image(image_path)
    media_type = get_media_type(image_path)
    data_url = f"data:{media_type};base64,{image_b64}"
    
    # Build prompt (use the prompt from above)
    prompt = """Analyze this sports card image..."""  # Full prompt here
    
    # API request
    response = requests.post(
        OPENROUTER_URL,
        headers={
            "Authorization": f"Bearer {OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
        },
        json={
            "model": model,
            "messages": [
                {
                    "role": "user",
                    "content": [
                        {"type": "image_url", "image_url": {"url": data_url}},
                        {"type": "text", "text": prompt},
                    ]
                }
            ],
            "max_tokens": 1024,
        }
    )
    
    if response.status_code != 200:
        return {"error": f"API error: {response.status_code} {response.text}"}
    
    # Parse response
    data = response.json()
    content = data["choices"][0]["message"]["content"]
    
    # Extract JSON from response
    try:
        # Handle potential markdown code blocks
        if "```json" in content:
            content = content.split("```json")[1].split("```")[0]
        elif "```" in content:
            content = content.split("```")[1].split("```")[0]
        
        return json.loads(content.strip())
    except json.JSONDecodeError:
        return {"error": f"Failed to parse JSON: {content}"}
```

---

## Cost Estimation

For 150 cards at ~0.5 MB average image size:

| Model | Est. Cost |
|-------|-----------|
| Claude Sonnet 4 | ~$2-3 |
| GPT-4o | ~$1.50-2 |
| GPT-4o mini | ~$0.15-0.25 |
| Gemini Flash | ~$0.05-0.10 |

**Tip:** Use GPT-4o-mini for initial testing, then re-scan problem cards with Claude Sonnet for accuracy.

---

## Error Handling

Common issues:
1. **Invalid API key** → Check OPENROUTER_API_KEY
2. **Model not available** → Check model ID spelling
3. **Image too large** → Resize to max 2048px on longest side
4. **Rate limited** → Add delay between requests
5. **JSON parse error** → Model didn't follow format; retry or use different model

---

## Testing

Create a test script to verify your setup:

```python
# test_scanner.py
import os
from scanner import scan_card

# Test with a sample card image
result = scan_card("images/test_card.jpg")

if "error" in result:
    print(f"Error: {result['error']}")
else:
    print(f"Player: {result.get('player_name')}")
    print(f"Year: {result.get('year')}")
    print(f"Brand: {result.get('brand')}")
    print(f"Parallel: {result.get('parallel_name')}")
```
