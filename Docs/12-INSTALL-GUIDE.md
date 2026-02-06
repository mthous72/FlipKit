# Installation Guide (For Non-Technical Users)

## What You're Installing

**Card Lister** is a desktop program that helps you:
1. Take photos of sports cards
2. Automatically read the card details (player, year, brand, etc.)
3. Research prices
4. Create listings for Whatnot

It runs directly on your computer — just double-click to open. No browser, no extra software needed.

---

## Before You Start

You'll need to create **two free accounts** to use Card Lister:

### 1. OpenRouter Account (For Card Scanning)
This service reads your card photos and extracts the details.

- **Cost:** Pay-as-you-go, about $0.01-0.02 per card scanned
- **To start:** Add $5 credit (enough for ~250-500 cards)

### 2. ImgBB Account (For Image Hosting)
Whatnot needs your card images hosted online. ImgBB does this for free.

- **Cost:** Free
- **Limit:** More than enough for your needs

---

## Installation Steps

### Step 1: Download Card Lister

1. Download the Card Lister file for your computer:
   - **Windows:** `CardLister-win-x64.zip`
   - **Mac (Apple Silicon):** `CardLister-osx-arm64.zip`
   - **Mac (Intel):** `CardLister-osx-x64.zip`
2. Unzip/extract the folder
3. Move the folder somewhere permanent, like:
   - Windows: `C:\Users\YourName\CardLister`
   - Mac: `/Users/YourName/Applications/CardLister`

**That's it for installation. No Python, no extra downloads, no command prompt.**

---

### Step 2: Create Your API Accounts

#### OpenRouter (Required)

1. Go to: https://openrouter.ai/
2. Click "Sign Up" (top right)
3. Create account with email or Google
4. After signing in, click your profile → "Keys"
5. Click "Create Key"
6. Name it: "Card Lister"
7. Copy the key (starts with `sk-or-v1-...`)
8. **Save this key somewhere safe!** You'll need it in a moment.
9. Click "Credits" → Add $5 to start

#### ImgBB (Required)

1. Go to: https://api.imgbb.com/
2. Click "Get API Key"
3. Sign up for a free account
4. After signing in, you'll see your API key
5. Copy the key
6. **Save this key somewhere safe!**

---

### Step 3: Start Card Lister

**For Windows:**
1. Open the CardLister folder
2. Double-click `CardLister.exe`
3. The app window opens directly — no browser needed!

**For Mac:**
1. Open the CardLister folder
2. Double-click `CardLister`
3. If Mac blocks it: Right-click → Open → click "Open" in the dialog
4. The app window opens directly

---

### Step 4: First-Time Setup

When Card Lister opens for the first time:

1. **Welcome screen appears**
2. **Step 1: OpenRouter**
   - Paste your OpenRouter API key
   - Click "Test Connection"
   - Should show ✅ Connected
   - Click "Next"
3. **Step 2: ImgBB**
   - Paste your ImgBB API key
   - Click "Test Connection"
   - Should show ✅ Connected
   - Click "Next"
4. **Step 3: Preferences**
   - Check "I sell on eBay" if you do (enables Terapeak)
   - Click "Finish"
5. **Done!** You're ready to scan cards.

---

## Using Card Lister

### Scanning a Card

1. Click the **Scan** tab in the sidebar
2. Drag a card photo into the drop area (or click to browse)
3. Click **Scan Card**
4. Wait 5-10 seconds
5. Review the details — fix anything wrong
6. Click **Save to My Cards**

**Tips for good photos:**
- Good lighting (no harsh shadows)
- Card fills most of the frame
- Flat, not at an angle
- Clear, not blurry

### Pricing Your Cards

1. Click the **Price** tab
2. For each card:
   - Click **Open Terapeak** (if you're an eBay seller) or **Open eBay Sold**
   - Look at what similar cards sold for recently
   - Enter that value in the "Market Value" box
   - The app suggests a Whatnot price (accounting for fees)
   - Adjust if you want
   - Click **Save & Next**

### Exporting to Whatnot

1. Click the **Export** tab
2. Click **Upload Images** (uploads your photos to ImgBB)
3. Wait for upload to complete
4. Click **Download CSV**
5. Go to Whatnot Seller Hub → Inventory → Import Products
6. Upload the CSV file
7. Review and publish your listings!

---

## Troubleshooting

### Windows says "Windows protected your PC"
- Click "More info"
- Click "Run anyway"
- This happens because the app isn't signed with a certificate (it's safe)

### Mac says "CardLister can't be opened"
- Right-click the app → "Open" → click "Open" in the dialog
- Or: System Settings → Privacy & Security → scroll down → click "Open Anyway"

### "API key invalid"
- Double-check you copied the entire key
- Make sure there are no extra spaces
- Try generating a new key

### Card scan isn't accurate
- Try a clearer photo
- Make sure the card is well-lit and fills the frame
- You can always edit the details manually after scanning

### App won't start
- Make sure you downloaded the right version for your computer (Windows vs Mac, Intel vs Apple Silicon)
- Try restarting your computer
- Check the `logs/` folder for error details

### Need more help?
Contact [your support info here]

---

## Closing Card Lister

Just close the window like any other app. Your cards are saved automatically — nothing will be lost.

---

## Updating Card Lister

When a new version is available:

1. Download the new version
2. Replace your CardLister folder with the new one
3. Your card database and settings are preserved (stored separately in your user data folder)
4. Start Card Lister as usual

---

## Keeping Your Data Safe

Your card data is stored in your user data folder:
- **Windows:** `C:\Users\YourName\AppData\Local\CardLister\`
- **Mac:** `/Users/YourName/Library/Application Support/CardLister/`

**To back up your data:**
1. Open Card Lister → Settings → Click "Backup Data"
2. Choose where to save the backup
3. Or manually copy the `cards.db` file from the data folder

**To restore from backup:**
1. Copy your backed-up `cards.db` into the data folder
2. Replace the existing file if asked
