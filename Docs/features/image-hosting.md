# Image Hosting for Whatnot CSV

## Requirements

Whatnot CSV requires **publicly accessible image URLs**:
- Must use `https://` protocol
- Must not require login/authentication
- Must be direct image links (not a page containing an image)
- Test by opening URL in incognito/private browser

---

## Recommended: ImgBB

**Free, simple API, no account limits.**

### Setup

1. Go to https://api.imgbb.com/
2. Click "Get API Key"
3. Sign up / log in
4. Copy your API key

### Environment Variable
```bash
export IMGBB_API_KEY="your_api_key_here"
```

### API Usage

**Endpoint:** `POST https://api.imgbb.com/1/upload`

**Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| key | string | Your API key |
| image | string | Base64 encoded image data |
| name | string | (optional) Image name |

**Response:**
```json
{
  "data": {
    "url": "https://i.ibb.co/abc123/image.jpg",
    "display_url": "https://i.ibb.co/abc123/image.jpg",
    "delete_url": "https://ibb.co/delete/abc123"
  },
  "success": true
}
```

### Python Implementation

```python
import os
import base64
import requests

IMGBB_API_KEY = os.environ.get("IMGBB_API_KEY")
IMGBB_UPLOAD_URL = "https://api.imgbb.com/1/upload"

def upload_image(image_path: str, name: str = None) -> dict:
    """
    Upload an image to ImgBB.
    
    Returns:
        {"url": "https://...", "delete_url": "https://..."}
        or {"error": "message"}
    """
    if not IMGBB_API_KEY:
        return {"error": "IMGBB_API_KEY not set"}
    
    # Read and encode image
    with open(image_path, "rb") as f:
        image_b64 = base64.standard_b64encode(f.read()).decode("utf-8")
    
    # Upload
    response = requests.post(
        IMGBB_UPLOAD_URL,
        data={
            "key": IMGBB_API_KEY,
            "image": image_b64,
            "name": name or os.path.basename(image_path).split(".")[0]
        }
    )
    
    if response.status_code != 200:
        return {"error": f"Upload failed: {response.status_code}"}
    
    result = response.json()
    
    if not result.get("success"):
        return {"error": result.get("error", {}).get("message", "Unknown error")}
    
    return {
        "url": result["data"]["url"],
        "delete_url": result["data"]["delete_url"]
    }


def upload_card_images(front_path: str, back_path: str = None, card_id: int = None) -> dict:
    """
    Upload front (and optional back) images for a card.
    
    Returns:
        {"image_url_1": "...", "image_url_2": "..."}
    """
    result = {}
    
    # Upload front
    name = f"card_{card_id}_front" if card_id else None
    front = upload_image(front_path, name)
    
    if "error" in front:
        return front
    
    result["image_url_1"] = front["url"]
    
    # Upload back if provided
    if back_path and os.path.exists(back_path):
        name = f"card_{card_id}_back" if card_id else None
        back = upload_image(back_path, name)
        
        if "error" not in back:
            result["image_url_2"] = back["url"]
    
    return result
```

---

## Alternative: Imgur

Also free with API access.

### Setup
1. Register app at https://api.imgur.com/oauth2/addclient
2. Get Client ID

### Upload (Anonymous)
```python
import requests
import base64

def upload_to_imgur(image_path: str, client_id: str) -> dict:
    with open(image_path, "rb") as f:
        image_b64 = base64.standard_b64encode(f.read()).decode("utf-8")
    
    response = requests.post(
        "https://api.imgur.com/3/image",
        headers={"Authorization": f"Client-ID {client_id}"},
        data={"image": image_b64}
    )
    
    data = response.json()
    return {"url": data["data"]["link"]}
```

---

## Image Preparation

### Recommended Specs
- **Format:** JPEG (smaller files)
- **Resolution:** 1200-2000px on longest side
- **Quality:** 80-90%
- **Background:** Clean, solid color preferred
- **Lighting:** Even, no harsh shadows

### Resize Before Upload (Optional)

```python
from PIL import Image

def resize_for_upload(image_path: str, max_size: int = 1600) -> str:
    """
    Resize image if larger than max_size, save as JPEG.
    Returns path to resized image.
    """
    img = Image.open(image_path)
    
    # Calculate new size maintaining aspect ratio
    ratio = min(max_size / img.width, max_size / img.height)
    if ratio < 1:
        new_size = (int(img.width * ratio), int(img.height * ratio))
        img = img.resize(new_size, Image.LANCZOS)
    
    # Convert to RGB if necessary (for JPEG)
    if img.mode in ("RGBA", "P"):
        img = img.convert("RGB")
    
    # Save
    output_path = image_path.rsplit(".", 1)[0] + "_resized.jpg"
    img.save(output_path, "JPEG", quality=85)
    
    return output_path
```

---

## Batch Upload Workflow

```python
def upload_all_card_images(cards: list) -> list:
    """
    Upload images for all cards that have local paths but no URLs.
    
    Returns list of {"card_id": x, "image_url_1": "...", "image_url_2": "..."}
    """
    results = []
    
    for card in cards:
        # Skip if already has URLs
        if card.get("image_url_1"):
            continue
        
        # Skip if no local image
        if not card.get("image_path_front"):
            continue
        
        print(f"Uploading images for: {card['player_name']}...")
        
        urls = upload_card_images(
            front_path=card["image_path_front"],
            back_path=card.get("image_path_back"),
            card_id=card["id"]
        )
        
        if "error" in urls:
            print(f"  Error: {urls['error']}")
            continue
        
        urls["card_id"] = card["id"]
        results.append(urls)
        
        print(f"  → {urls['image_url_1']}")
    
    return results
```

---

## Local Image Organization

Suggested folder structure:

```
images/
├── raw/                    # Original photos from phone/camera
│   ├── batch_2024-01-15/
│   └── batch_2024-01-20/
├── processed/              # Cropped/edited images
│   ├── front/
│   │   ├── 001_jefferson.jpg
│   │   └── 002_ohtani.jpg
│   └── back/
│       ├── 001_jefferson.jpg
│       └── 002_ohtani.jpg
└── temp/                   # Resized for upload
```

---

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| "Invalid image" | File too large | Resize to < 32MB |
| "Rate limited" | Too many uploads | Add delays between uploads |
| Images not showing in Whatnot | URL not direct link | Use the `url` field, not `display_url` |
| Blurry in listing | Low resolution | Upload at least 1200px |
