# Local Desktop App with NiceGUI

## Overview

The app runs **locally on the user's computer** and opens in their web browser. Users provide their own API credentials, which are stored securely on their machine.

**Key Principles:**
- **Local-first:** All data stays on user's computer
- **User owns credentials:** They create their own API accounts
- **Simple setup:** Step-by-step wizard for non-technical users
- **KISS:** Keep It Simple, Stupid — one obvious way to do each task
- **No Norman Doors:** UI does what it looks like it does

---

## Why NiceGUI + Local Browser

| Factor | This Approach | Pure Desktop (Tkinter/PyQt) |
|--------|---------------|---------------------------|
| Modern UI | ✅ Clean, responsive | ❌ Dated look |
| Cross-platform | ✅ Same code everywhere | ❌ Separate builds |
| User credentials | ✅ Stored locally | ✅ Stored locally |
| Internet required | Only for API calls | Only for API calls |
| File size | ~50MB (with Python) | 100-200MB |
| Learning curve | Easier | Harder |

**How it works:**
1. User double-clicks `start.bat` (Windows) or `start.command` (Mac)
2. Python starts a local web server
3. Browser automatically opens to `http://localhost:8080`
4. Everything runs on their machine — browser is just the display

---

## Credential Storage

User API keys are stored locally in a config file:

```
CardLister/
├── app.py
├── config.json        ← User's API keys stored here
├── cards.db           ← Card inventory
└── images/            ← Local card photos
```

**config.json structure:**
```json
{
  "openrouter_api_key": "sk-or-v1-...",
  "imgbb_api_key": "...",
  "is_ebay_seller": true,
  "default_shipping_profile": "4 oz"
}
```

**Security:**
- Keys stored in plain text (acceptable for local desktop app)
- File is in user's private folder
- Never transmitted except to the respective API services
- User can delete config.json to reset

---

## GUI Framework Comparison

### Option 1: NiceGUI ⭐ Recommended

**Pros:**
- Modern, clean UI built on Quasar (Vue.js components)
- Runs in browser — cross-platform automatically
- Built-in file upload, image display, data tables
- Multi-page apps with routing
- FastAPI backend — can add API endpoints easily
- Event-driven (doesn't re-run entire script like Streamlit)
- MIT license, free

**Cons:**
- Requires Python installed (but not complex setup)
- Smaller community than Streamlit
- Less documentation than Streamlit

**Sample code:**
```python
from nicegui import ui

# Card inventory table
columns = [
    {'name': 'player', 'label': 'Player', 'field': 'player'},
    {'name': 'year', 'label': 'Year', 'field': 'year'},
    {'name': 'brand', 'label': 'Brand', 'field': 'brand'},
    {'name': 'price', 'label': 'Price', 'field': 'price'},
]
rows = [
    {'player': 'Justin Jefferson', 'year': 2023, 'brand': 'Prizm', 'price': '$12.99'},
]

ui.table(columns=columns, rows=rows)
ui.run()
```

### Option 2: Streamlit

**Pros:**
- Most popular Python web app framework
- Huge community, tons of examples
- Great for dashboards and data display
- Built-in charts, tables, file uploads
- Free hosting via Streamlit Cloud

**Cons:**
- Re-runs entire script on every interaction (can be slow)
- Limited UI customization
- Less suitable for "app-like" workflows (more for dashboards)

**Sample code:**
```python
import streamlit as st

st.title("Card Lister")
uploaded = st.file_uploader("Upload card image", type=["jpg", "png"])
if uploaded:
    st.image(uploaded)
    if st.button("Scan Card"):
        st.write("Scanning...")
```

### Option 3: Gradio

**Pros:**
- Extremely simple for ML demos
- One-click sharing via Hugging Face
- Built-in image/audio/video components
- Great for AI-powered apps

**Cons:**
- Designed for single-page demos, not full apps
- Limited UI customization
- Share links expire after 72 hours (free tier)

**Best for:** Quick AI demos, not full inventory management apps.

### Option 4: Desktop App (Tkinter + PyInstaller)

**Pros:**
- No server needed
- Works offline
- Single .exe file to distribute

**Cons:**
- Dated appearance (Tkinter)
- Large file size (50-200MB for bundled Python)
- Antivirus false positives on .exe files
- Need separate builds for Windows/Mac
- Harder to update (users re-download)

**Best for:** Apps that must work offline with no browser.

### Option 5: Desktop App (PyQt/PySide)

**Pros:**
- Professional-looking native UI
- Highly customizable
- Good for complex desktop apps

**Cons:**
- Steeper learning curve
- Licensing complexity (PyQt vs PySide)
- Still need PyInstaller for distribution
- Large bundle size

---

## Recommended Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    NiceGUI Frontend                     │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐   │
│  │  Scan   │  │ Inventory│  │  Price  │  │ Export  │   │
│  │  Page   │  │  Page   │  │  Page   │  │  Page   │   │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘   │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                   Python Backend                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐              │
│  │ scanner  │  │   db     │  │ exporter │              │
│  │ (OpenRouter)│  │ (SQLite) │  │  (CSV)   │              │
│  └──────────┘  └──────────┘  └──────────┘              │
└─────────────────────────────────────────────────────────┘
```

---

## NiceGUI App Structure

```
whatnot-card-lister/
├── app.py               # Main NiceGUI app entry point
├── pages/
│   ├── __init__.py
│   ├── scan.py          # Card scanning page
│   ├── inventory.py     # Inventory list/edit page
│   ├── pricing.py       # Pricing research page
│   └── export.py        # CSV export page
├── components/
│   ├── __init__.py
│   ├── card_form.py     # Reusable card edit form
│   └── card_table.py    # Reusable inventory table
├── core/                # Backend modules (unchanged)
│   ├── db.py
│   ├── scanner.py
│   ├── pricer.py
│   ├── uploader.py
│   └── exporter.py
├── static/
│   └── logo.png
├── cards.db
└── requirements.txt
```

---

## Key Pages

### 1. Scan Page
- Upload card image (drag & drop)
- Preview image
- "Scan" button → calls OpenRouter
- Shows extracted data in editable form
- "Save to Inventory" button

### 2. Inventory Page
- Data table with all cards
- Filter by sport, status, search
- Click row to edit
- Bulk actions: delete, mark ready, upload images

### 3. Pricing Page
- Select card from inventory
- Shows card details
- "Open Terapeak" button → opens browser to Terapeak search
- "Open eBay Sold" button → opens browser to eBay search
- Input: estimated value
- Shows: suggested Whatnot price, net after fees
- "Save Price" button

### 4. Export Page
- Filter cards (by status, sport)
- Preview CSV data
- "Upload Images" button (batch ImgBB upload)
- "Export CSV" button → downloads file
- Instructions for Whatnot upload

---

## Distribution: Local Install Package

Users download a folder containing everything they need:

```
CardLister/
├── app.py                 # Main application
├── requirements.txt       # Python dependencies
├── install.bat           # Windows: double-click to install deps
├── install.command       # Mac: double-click to install deps
├── start.bat             # Windows: double-click to run
├── start.command         # Mac: double-click to run
├── core/                 # Backend modules
├── pages/                # UI pages
├── static/               # Icons, images
├── README.txt            # Quick start guide
└── INSTALL_GUIDE.pdf     # Detailed setup instructions
```

### Launcher Scripts

**start.bat (Windows):**
```batch
@echo off
echo Starting Card Lister...
echo.
echo The app will open in your web browser.
echo Keep this window open while using the app.
echo.
python app.py
pause
```

**start.command (Mac):**
```bash
#!/bin/bash
cd "$(dirname "$0")"
echo "Starting Card Lister..."
echo ""
echo "The app will open in your web browser."
echo "Keep this window open while using the app."
echo ""
python3 app.py
```

**install.bat (Windows):**
```batch
@echo off
echo Installing Card Lister requirements...
echo This may take a minute...
echo.
pip install -r requirements.txt
echo.
echo Installation complete!
echo You can now run start.bat to launch Card Lister.
pause
```

---

## Sample NiceGUI Implementation

```python
# app.py
from nicegui import ui
from core.db import get_all_cards, init_db
from pages import scan, inventory, pricing, export

# Initialize database
init_db()

# Navigation
@ui.page('/')
def home():
    with ui.header():
        ui.label('Card Lister').classes('text-h4')
        ui.link('Scan', '/scan')
        ui.link('Inventory', '/inventory')
        ui.link('Pricing', '/pricing')
        ui.link('Export', '/export')
    
    ui.label('Welcome to the Whatnot Card Lister')
    
    # Quick stats
    cards = get_all_cards()
    with ui.row():
        ui.label(f'Total Cards: {len(cards)}')
        ui.label(f'Ready to List: {len([c for c in cards if c["status"] == "ready"])}')

@ui.page('/scan')
def scan_page():
    scan.render()

@ui.page('/inventory')
def inventory_page():
    inventory.render()

@ui.page('/pricing/{card_id}')
def pricing_page(card_id: int):
    pricing.render(card_id)

@ui.page('/export')
def export_page():
    export.render()

ui.run(title='Whatnot Card Lister', port=8080)
```

```python
# pages/scan.py
from nicegui import ui
from core.scanner import scan_card
from core.db import insert_card
import tempfile
import os

def render():
    ui.label('Scan a Card').classes('text-h5')
    
    result_container = ui.column()
    
    async def handle_upload(e):
        # Save uploaded file temporarily
        content = e.content.read()
        with tempfile.NamedTemporaryFile(delete=False, suffix='.jpg') as f:
            f.write(content)
            temp_path = f.name
        
        # Show preview
        with result_container:
            result_container.clear()
            ui.image(temp_path).classes('w-64')
            ui.label('Scanning...').bind_visibility_from(scanning, 'value')
        
        # Call scanner
        scanning.value = True
        data = scan_card(temp_path)
        scanning.value = False
        
        # Show results
        with result_container:
            if 'error' in data:
                ui.label(f'Error: {data["error"]}').classes('text-negative')
            else:
                ui.label(f'Player: {data.get("player_name")}')
                ui.label(f'Year: {data.get("year")}')
                ui.label(f'Brand: {data.get("brand")}')
                ui.label(f'Parallel: {data.get("parallel_name", "Base")}')
                
                def save():
                    data['image_path_front'] = temp_path
                    card_id = insert_card(data)
                    ui.notify(f'Saved! Card ID: {card_id}')
                
                ui.button('Save to Inventory', on_click=save)
        
        # Cleanup
        os.unlink(temp_path)
    
    scanning = ui.state(False)
    ui.upload(on_upload=handle_upload, label='Drop card image here')
```

---

## Requirements

```
# requirements.txt
nicegui>=1.4.0
requests>=2.28.0
python-dotenv>=1.0.0
Pillow>=9.0.0
```

---

## Development Phases (Updated)

### Phase 1: Core Backend (CLI)
- [x] Database schema
- [x] OpenRouter scanner
- [x] Pricer with Terapeak URLs
- [x] ImgBB uploader
- [x] CSV exporter

### Phase 2: NiceGUI Frontend
- [ ] Basic app structure with routing
- [ ] Scan page with image upload
- [ ] Inventory table page
- [ ] Pricing page with browser links
- [ ] Export page with CSV download

### Phase 3: Polish
- [ ] Better styling/theming
- [ ] Error handling and validation
- [ ] Loading states
- [ ] Batch operations

### Phase 4: Distribution
- [ ] Test local install on clean machine
- [ ] Create installer (optional)
- [ ] Write user documentation
- [ ] Consider cloud hosting

---

## Next Steps

1. Build the CLI/backend first (Phase 1) — test all core functions
2. Add NiceGUI frontend (Phase 2) — one page at a time
3. Test with real cards
4. Package for distribution
