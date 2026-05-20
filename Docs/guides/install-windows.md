# Windows Installation Guide (For Non-Technical Users)

**Applies to:** FlipKit v3.7.0 (Windows x64)

## What You're Installing

**FlipKit** is a desktop program that helps you:
1. Take photos of sports cards
2. Automatically read the card details (player, year, brand, etc.)
3. Research prices
4. Create listings for Whatnot and eBay

It runs directly on your computer — just double-click to open. No browser, no
extra software needed. FlipKit Hub also bundles a built-in Web server you can
reach from your phone (managed from Settings → Servers); that's optional.

---

## Before You Start

You'll need a couple of free accounts to get the most out of FlipKit:

### 1. OpenRouter Account (Card Scanning — required)
This service reads your card photos and extracts the details when CardSight
can't.

- **Cost:** Pay-as-you-go, about $0.01-0.02 per card scanned
- **To start:** Add $5 credit (enough for ~250-500 cards)

### 2. ImgBB Account (Image Hosting — required for export)
Whatnot and eBay need your card images hosted online. ImgBB does this for free.

- **Cost:** Free

### 3. CardSight Account (optional, recommended)
CardSight is a sports-card-specific recognition service. When configured,
FlipKit tries it **first** on every scan and only falls back to OpenRouter when
CardSight isn't confident. The free tier covers 750 identifications/month.

- **Cost:** Free tier (750 IDs/month); paid tiers above that
- **Where:** enter the API key in Settings → Scanning after first run

---

## Installation Steps

### Step 1: Download FlipKit

1. Download the Windows package: **`FlipKit-Hub-Windows-x64-v3.7.0.zip`**
   from the [Releases page](https://github.com/mthous72/FlipKit/releases).
2. Unzip/extract the folder.
3. Move the folder somewhere permanent, like `C:\Users\YourName\FlipKit`.

**That's it for installation. No Python, no extra downloads, no command prompt.**

---

### Step 2: Create Your API Accounts

#### OpenRouter (Required)

1. Go to: https://openrouter.ai/
2. Click "Sign Up" (top right) and create an account
3. After signing in, click your profile → "Keys"
4. Click "Create Key"
5. Name it: "FlipKit"
6. Copy the key (starts with `sk-or-v1-...`) and save it somewhere safe
7. Click "Credits" → Add $5 to start

#### ImgBB (Required)

1. Go to: https://api.imgbb.com/
2. Click "Get API Key" and sign up for a free account
3. Copy the key and save it somewhere safe

---

### Step 3: Start FlipKit

1. Open the FlipKit folder
2. Double-click `FlipKit.Desktop.exe`
3. The app window opens directly — no browser needed

If Windows shows **"Windows protected your PC"**, click **More info → Run anyway**.
This happens because the app isn't code-signed (it's safe).

---

### Step 4: First-Time Setup

When FlipKit opens for the first time, a setup wizard walks you through:

1. **OpenRouter** — paste your key, click "Test Connection" (should show ✅), Next
2. **ImgBB** — paste your key, click "Test Connection", Next
3. **Preferences** — check "I sell on eBay" if you do, then Finish

You're ready to scan. To enable CardSight later, go to **Settings → Scanning**
and paste your CardSight API key; the CardSight usage panel shows your monthly
quota.

---

## Using FlipKit

### Scanning a Card

1. Click the **Scan** tab in the sidebar
2. Drag a card photo into the drop area (or click to browse), or use the webcam
3. Click **Scan Card** (FlipKit tries CardSight first, then OpenRouter)
4. Review the details — fix anything wrong
5. Click **Save to My Cards**

**Tips for good photos:** good lighting, card fills the frame, flat (not angled),
clear (not blurry).

### Pricing Your Cards

1. Click the **Price** tab
2. For each card, click **Open Terapeak** (eBay sellers) or **Open eBay Sold**
3. Enter the market value you find; FlipKit suggests a price (accounting for fees)
4. Click **Save & Next**

### Exporting to Whatnot / eBay

1. Click the **Export** tab
2. Click **Upload Images** (uploads your photos to ImgBB)
3. Click **Download CSV**
4. Import the CSV into Whatnot Seller Hub (or use the eBay listing flow)

---

## Troubleshooting

### Windows says "Windows protected your PC"
Click "More info" → "Run anyway". The app isn't signed with a certificate.

### "API key invalid"
Double-check you copied the entire key with no extra spaces; try a new key.

### Card scan isn't accurate
Try a clearer, well-lit photo that fills the frame. You can always edit details
manually after scanning.

### App won't start
Make sure you downloaded the Windows x64 package. Check the
`%LOCALAPPDATA%\FlipKit\logs\` folder for error details.

---

## Closing FlipKit

Just close the window like any other app. Your cards save automatically, and the
embedded servers (if running) stop when the Desktop app closes.

---

## Updating FlipKit

1. Download the new version
2. Replace your FlipKit folder with the new one
3. Your card database and settings are preserved (stored separately in your user
   data folder)
4. Start FlipKit as usual

---

## Keeping Your Data Safe

Your card data lives in your user data folder:
`C:\Users\YourName\AppData\Local\FlipKit\`

**To back up:** Open FlipKit → Settings → "Backup Data", or manually copy
`cards.db` from that folder.

**To restore:** copy your backed-up `cards.db` back into that folder.

---

See also: [install-mac.md](install-mac.md), [install-linux.md](install-linux.md),
and [user-guide.md](user-guide.md).
