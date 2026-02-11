# FlipKit User Guide

**Version:** 1.0
**Date:** February 2026
**Author:** FlipKit Project
**Website:** https://github.com/mthous72/FlipKit

---

## Table of Contents

1. [What is FlipKit?](#what-is-flipkit)
2. [Getting Started](#getting-started)
3. [Initial Setup](#initial-setup)
4. [Scanning Cards](#scanning-cards)
5. [Bulk Scanning](#bulk-scanning)
6. [Pricing Your Cards](#pricing-your-cards)
7. [Managing Your Inventory](#managing-your-inventory)
8. [Exporting to Whatnot](#exporting-to-whatnot)
9. [Reports & Analytics](#reports--analytics)
10. [Settings & Configuration](#settings--configuration)
11. [Tips & Best Practices](#tips--best-practices)
12. [Troubleshooting](#troubleshooting)

---

## What is FlipKit?

FlipKit is a free, open-source desktop application designed for sports card sellers who need to list cards on platforms like Whatnot, eBay, and COMC. It uses AI vision to automatically identify card details from photos, helps you research pricing, and generates bulk upload CSV files.

### Key Features

- **AI-Powered Scanning** - Photograph a card and AI extracts player name, year, brand, parallel, serial number, and more
- **Bulk Processing** - Scan 10, 50, or 100+ cards in one batch with front/back pairing
- **Automated Pricing** - Fetch current eBay active listing comps with statistics (median, range, confidence)
- **Inventory Management** - Track all your cards in a local database with search and filtering
- **Checklist Verification** - Cross-reference AI results against 97+ seeded set checklists to catch errors
- **CSV Export** - Generate platform-specific CSV files for Whatnot, eBay, or COMC bulk uploads
- **Sales Tracking** - Record sold cards with profit calculations and tax reports

### What You'll Need

1. **Windows, Mac, or Linux computer** (64-bit)
2. **Card photos** - Front image required, back image optional
3. **Free API Keys:**
   - OpenRouter API key (for AI vision) - Get at https://openrouter.ai/keys
   - ImgBB API key (for image hosting) - Optional, get at https://api.imgbb.com/
   - eBay Developer credentials (for automated pricing) - Optional, get at https://developer.ebay.com

---

## Getting Started

### Download and Install

**📸 SCREENSHOT PLACEHOLDER: `download-page.png`**
**Instructions:**
- Open https://github.com/mthous72/FlipKit/releases in your browser
- Show the latest release page with download links
- Capture the full browser window
- **Expected elements:** Windows/Mac/Linux download links, release notes

FlipKit is distributed as a self-contained executable - no .NET runtime installation required.

**Windows:**
1. Download `FlipKit-win-x64.zip`
2. Extract all files to a folder (right-click → Extract All)
3. Double-click `FlipKit.exe` to launch

**macOS:**
1. Download `FlipKit-osx-x64.zip` (Intel) or `FlipKit-osx-arm64.zip` (Apple Silicon)
2. Extract all files to the same folder
3. Open Terminal, navigate to the folder
4. Run `chmod +x FlipKit` to make it executable
5. Run `./FlipKit` to launch

**Linux:**
1. Download `FlipKit-linux-x64.zip`
2. Extract all files
3. Run `chmod +x FlipKit`
4. Run `./FlipKit`

### First Launch - Setup Wizard

On first launch, you'll see the Setup Wizard to configure your API keys.

**📸 SCREENSHOT PLACEHOLDER: `setup-wizard-welcome.png`**
**Instructions:**
- Launch FlipKit for the first time
- The Setup Wizard should appear automatically
- Capture the welcome screen
- **Expected elements:** "Welcome to FlipKit" heading, "Get Started" button

**📸 SCREENSHOT PLACEHOLDER: `setup-wizard-api-keys.png`**
**Instructions:**
- Click "Get Started" in the Setup Wizard
- You should see the API key configuration page
- Leave fields empty or enter dummy keys for screenshot
- **Expected elements:** OpenRouter API key field, ImgBB API key field, "Test Connection" buttons

The Setup Wizard will guide you through entering:
1. **OpenRouter API Key** (required for scanning)
2. **ImgBB API Key** (optional, for image hosting)
3. **eBay Developer Credentials** (optional, for automated pricing)

Click "Test Connection" to verify each API key works, then click "Finish" to start using FlipKit.

---

## Initial Setup

### Configuring API Keys

#### OpenRouter API Key (Required)

OpenRouter provides access to 11 free AI vision models with automatic fallback on rate limiting.

1. Go to https://openrouter.ai/keys
2. Sign up for a free account
3. Create a new API key
4. Copy the key (starts with `sk-or-v1-...`)
5. Paste into FlipKit Settings → OpenRouter API Key
6. Click "Test" to verify connection

**Cost:** $0 - FlipKit uses free models by default

#### ImgBB API Key (Optional)

ImgBB provides free image hosting for card photos that can be used in Whatnot listings.

1. Go to https://api.imgbb.com/
2. Sign up for a free account
3. Copy your API key
4. Paste into FlipKit Settings → ImgBB API Key
5. Click "Test" to verify connection

**Cost:** $0 - Free tier includes unlimited uploads

#### eBay Browse API (Optional)

eBay's official API provides automated active listing comps for pricing research.

1. Go to https://developer.ebay.com
2. Create a developer account (free)
3. Create a Production application
4. Copy the **Client ID** (App ID) and **Client Secret** (Cert ID)
5. Paste into FlipKit Settings → eBay Browse API section

**⚠️ IMPORTANT:** Before using eBay API, you MUST complete the "Marketplace Account Deletion Notification" opt-out process in your eBay Developer account:
- Log in to developer.ebay.com
- Go to Account Settings
- Find "Marketplace Account Deletion Notification"
- Select "Opt Out"
- Reason: "Application does not store eBay user data"
- Explain: "Only accesses public Browse API data with client credentials"

**Cost:** $0 - Free tier includes 5,000 API calls per day

**📸 SCREENSHOT PLACEHOLDER: `settings-api-keys.png`**
**Instructions:**
- Open FlipKit
- Click "Settings" in the left navigation
- Scroll to show all three API key sections (OpenRouter, ImgBB, eBay)
- Enter dummy keys or blur real keys for privacy
- **Expected elements:** Three API sections, test buttons, eBay compliance warning box (orange)

---

## Scanning Cards

The Scan page lets you photograph individual cards and have AI extract all the details automatically.

### How to Scan a Card

**📸 SCREENSHOT PLACEHOLDER: `scan-view-empty.png`**
**Instructions:**
- Click "Scan" in the left navigation
- Make sure no image is loaded (empty state)
- **Expected elements:**
  - AI Model dropdown at top showing selected model
  - "Browse for front image" button
  - Empty form fields on right side
  - "Browse for back image" (optional) button

1. **Select AI Model** - Choose from 11 free vision models (default: `nvidia/nemotron-nano-nano-12b-v2-vl:free`)

2. **Browse for Front Image** - Click the button and select your card photo
   - Supported formats: JPG, PNG, WEBP
   - Recommended: 800-2000px width for best results

3. **Optionally add Back Image** - For cards with important back details (serial numbers, autographs)

4. **Click "Scan Card"** - AI processes the images (~3-5 seconds)

**📸 SCREENSHOT PLACEHOLDER: `scan-view-processing.png`**
**Instructions:**
- Browse for any card image (front)
- Click "Scan Card" button
- Quickly capture while "Scanning..." spinner is visible
- **Expected elements:** Loading spinner, "Scanning card..." message

### Reviewing AI Results

**📸 SCREENSHOT PLACEHOLDER: `scan-view-results.png`**
**Instructions:**
- Wait for AI scan to complete
- Capture the view with populated form fields
- Use a recognizable card (e.g., rookie card, popular player)
- **Expected elements:**
  - Card image displayed on left
  - All form fields populated (player name, year, brand, etc.)
  - Verification status indicator
  - "Save Card" and "Scan Another" buttons

After scanning, the AI fills in:
- **Player Name** - Full player name
- **Year** - Card year
- **Sport** - Football, Baseball, or Basketball
- **Manufacturer** - Panini, Topps, Upper Deck, etc.
- **Brand/Set** - Prizm, Chrome, Optic, etc.
- **Card Number** - #301, #127, etc.
- **Parallel/Variation** - Silver Prizm, Refractor, etc.
- **Serial Number** - /499, /25, 1/1, etc.
- **Attributes** - Rookie, Auto, Relic checkboxes
- **Team** - Houston Texans, Los Angeles Dodgers, etc.
- **Grading Info** - If card is graded (PSA, BGS, CGC, etc.)

### Checklist Verification

If the AI detects a card from a set with a seeded checklist (97+ sets from 2017-2024), you'll see verification results:

**📸 SCREENSHOT PLACEHOLDER: `scan-view-verification-match.png`**
**Instructions:**
- Scan a card from a common set (2023 Prizm Football, 2024 Topps Chrome)
- Wait for verification to complete
- **Expected elements:**
  - Green checkmark or verification badge
  - "Verified against checklist" message
  - Possibly suggested corrections in yellow box

**Verification Outcomes:**

- **✅ Exact Match** - AI results match checklist perfectly, high confidence
- **⚠️ Suggestion** - Checklist suggests a correction (e.g., parallel name mismatch)
- **❓ No Checklist** - Set not in database (card still saves, no verification)

If verification suggests changes, you'll see them highlighted in yellow. Review and click "Apply Suggestion" or edit manually.

### Saving Your Card

**📸 SCREENSHOT PLACEHOLDER: `scan-view-save-button.png`**
**Instructions:**
- Scroll down to show bottom action buttons
- **Expected elements:** "Save Card" button (blue/accent), "Scan Another" button, "Clear Form" button

1. **Review all fields** - Fix any AI errors
2. **Click "Save Card"** - Saves to your local inventory database
3. **Status changes to "Draft"** - Card needs pricing before export

Your card is now in the Inventory view, ready for pricing.

---

## Bulk Scanning

For processing large quantities of cards efficiently, use the Bulk Scan feature.

### Setting Up Bulk Scan

**📸 SCREENSHOT PLACEHOLDER: `bulk-scan-empty.png`**
**Instructions:**
- Click "Bulk Scan" in left navigation
- No images loaded yet
- **Expected elements:**
  - AI Model dropdown
  - Concurrent Scans selector (1-8)
  - "Add Front Images" button
  - Empty card list
  - "Start Scanning" button (disabled)

1. **Select AI Model** - Choose your preferred model
2. **Set Concurrent Scans** - For paid API credits, use 3-4 for speed. Free models are limited to 1.
3. **Add Front Images** - Click "Add Front Images" and select multiple card photos

### Pairing Front and Back Images

**📸 SCREENSHOT PLACEHOLDER: `bulk-scan-pairing.png`**
**Instructions:**
- Click "Add Front Images", select 3-5 card images
- Images should appear in a list with "Pair Back" buttons
- **Expected elements:**
  - List of card items with front thumbnails
  - "Pair Back" button for each item
  - Status column showing "Ready"
  - "Start Scanning" button now enabled

If you have back images:
1. Click "Pair Back" next to a card
2. Select the corresponding back image
3. The card now shows both front and back thumbnails

**Tips for efficient pairing:**
- Name files consistently: `card001_front.jpg`, `card001_back.jpg`
- Sort by name before selecting
- Back images are optional - skip if cards don't need them

### Running Bulk Scan

**📸 SCREENSHOT PLACEHOLDER: `bulk-scan-processing.png`**
**Instructions:**
- Add 5-10 images to the bulk scan list
- Click "Start Scanning"
- Capture while scanning is in progress
- **Expected elements:**
  - Progress bar showing "Scanning 3 of 10"
  - Some cards marked "Completed" (green)
  - Some cards "Scanning..." (blue spinner)
  - "Cancel" button
  - Concurrent scans indicator if using >1

1. **Click "Start Scanning"** - Batch processing begins
2. **Watch progress** - Progress bar shows X of Y cards completed
3. **Wait for completion** - Free models: ~5-10 seconds per card. Paid models with concurrency: ~2-3 seconds per card.

You can cancel at any time - already-scanned cards will be saved.

### Reviewing Bulk Results

**📸 SCREENSHOT PLACEHOLDER: `bulk-scan-results.png`**
**Instructions:**
- Wait for all cards to complete
- **Expected elements:**
  - All cards showing "Completed" status (green checkmarks)
  - "Review & Save All" button
  - "Clear Completed" button
  - Success count: "10 cards scanned successfully"

After scanning completes:
- Each card shows extracted player name and status
- Click on a card to review/edit details
- Click "Review & Save All" to save the entire batch

All cards are saved to your inventory with "Draft" status, ready for pricing.

---

## Pricing Your Cards

The Pricing page helps you research market values and set listing prices.

### Pricing Workflow Overview

**📸 SCREENSHOT PLACEHOLDER: `pricing-view-overview.png`**
**Instructions:**
- Click "Pricing" in left navigation
- Should show a card from your "Draft" inventory
- **Expected elements:**
  - "Card 1 of X" counter at top
  - Card thumbnail and details
  - Pricing section with multiple options
  - "Save and Next" button at bottom

FlipKit guides you through unpriced cards one at a time:
1. View card details and photo
2. Research pricing (automated or manual)
3. Enter market value and listing price
4. Save and move to next card

### Option 1: Automated Active Listing Comps (eBay API)

If you have eBay API credentials configured, you can automatically fetch current eBay listings.

**📸 SCREENSHOT PLACEHOLDER: `pricing-active-comps-button.png`**
**Instructions:**
- Scroll to "Active Listing Comps (eBay)" section (blue/accent border)
- **Expected elements:**
  - Section header with eBay logo/text
  - Description of feature
  - "Get Active Comps" button (blue accent)
  - Help text about eBay API requirement

1. **Click "Get Active Comps"** - Searches eBay in real-time
2. **Wait 5-10 seconds** - Fetches listings and calculates statistics
3. **Review results** - Shows median price, range, sample size, confidence

**📸 SCREENSHOT PLACEHOLDER: `pricing-active-comps-results.png`**
**Instructions:**
- Click "Get Active Comps" and wait for results
- **Expected elements:**
  - Results box (light background)
  - Large median price displayed prominently
  - Range (low - high)
  - Average price
  - Sample size (X active listings)
  - Confidence level (High/Medium/Low)
  - Market Value field auto-filled with median price

The results show:
- **Median Price** - Middle value of comparable listings (auto-fills Market Value)
- **Range** - Lowest to highest comparable listing
- **Average Price** - Mean of all comparables
- **Sample Size** - Number of matching listings found
- **Confidence** - High (6+ matches, <7 days old), Medium (3-5 matches), Low (1-2 matches)

**How matching works:**
- Fuzzy matching on player name (85% similarity threshold)
- Exact year match
- Fuzzy brand match (80% threshold)
- Fuzzy parallel match (70% threshold)
- Graded vs raw separated
- Grade value exact match for graded cards

Results are cached locally for 7 days to minimize API usage.

### Option 2: Manual Research (Sold Prices)

For cards without automated matches, or to verify automated pricing, use manual research links.

**📸 SCREENSHOT PLACEHOLDER: `pricing-manual-research.png`**
**Instructions:**
- Scroll past the "OR" separator
- Show "Manual Research (Sold Prices)" section
- **Expected elements:**
  - "Open Terapeak" button
  - "Open eBay Sold" button
  - Help text explaining manual workflow

1. **Click "Open Terapeak"** or **"Open eBay Sold"** - Opens browser with pre-filled search
2. **Review sold listings** - Look at recent sales (last 30-90 days)
3. **Calculate median/average** - Mental math or spreadsheet
4. **Return to FlipKit** - Enter your researched value

Search queries are automatically built from card details using customizable templates (configured in Settings).

### Entering Prices

**📸 SCREENSHOT PLACEHOLDER: `pricing-fields.png`**
**Instructions:**
- Scroll to pricing input fields section
- Enter sample values (Market Value: $45.00, Listing Price: $54.99)
- **Expected elements:**
  - Market Value input field (pre-filled if using active comps)
  - Suggested Price (calculated automatically)
  - Listing Price input field
  - Net After Fees (calculated automatically, green text)
  - Cost Basis field (optional)
  - Cost Notes field (optional)

1. **Market Value** - What the card typically sells for (median of research)
2. **Suggested Price** - FlipKit calculates: `Market Value × 1.15` to cover fees
3. **Listing Price** - Your final asking price (edit as needed)
4. **Net After Fees** - Auto-calculated: `Listing Price - (Listing Price × Fee %)`

Fee percentages are configured in Settings (default: Whatnot 11%, eBay 13.25%).

**Optional Cost Tracking:**
- **Cost Basis** - What you paid for the card
- **Cost Notes** - Where you got it (receipt #, show name, etc.)

### Saving and Moving On

**📸 SCREENSHOT PLACEHOLDER: `pricing-save-button.png`**
**Instructions:**
- Scroll to bottom action buttons
- **Expected elements:**
  - "Previous" button
  - "Save and Next" button (large, accent color)
  - "Skip" button

1. **Click "Save and Next"** - Saves pricing, marks as "Priced", loads next Draft card
2. **Click "Skip"** - Skips this card, loads next Draft card (doesn't save)
3. **Click "Previous"** - Go back to previous card in queue

Price source is automatically tracked:
- `"eBay Active (X comps)"` if you used Get Active Comps
- `"Terapeak/eBay"` if you used manual research

---

## Managing Your Inventory

The Inventory page shows all your cards in a searchable, filterable grid.

### Inventory Overview

**📸 SCREENSHOT PLACEHOLDER: `inventory-view-grid.png`**
**Instructions:**
- Click "Inventory" in left navigation
- Make sure you have at least 5-10 cards in your database
- **Expected elements:**
  - "My Cards" heading with total count
  - Search bar and filter dropdowns (Sport, Status)
  - DataGrid with columns: checkbox, image thumbnail, player, year, brand, parallel, grade, team, price, age indicator, status
  - Row selection highlighting
  - Multiple cards visible
  - Action buttons at bottom

The grid displays:
- **Checkbox** - Select cards for bulk operations
- **Thumbnail** - Card front image preview
- **Player** - Player name
- **Year** - Card year
- **Brand** - Panini Prizm, Topps Chrome, etc.
- **Parallel** - Silver, Refractor, etc.
- **Grade** - PSA 10, BGS 9.5, etc.
- **Team** - Player's team
- **Price** - Current listing price
- **Age** - Price staleness indicator (green dot = fresh, yellow = aging, red = stale)
- **Status** - Draft, Priced, Ready, Listed, Sold

### Searching and Filtering

**📸 SCREENSHOT PLACEHOLDER: `inventory-filters.png`**
**Instructions:**
- Focus on the filter bar at top of grid
- Enter a search term (e.g., "Mahomes")
- Select a sport filter (e.g., "Football")
- **Expected elements:**
  - Search textbox with watermark "Search players, brands, teams..."
  - Sport dropdown (All, Football, Baseball, Basketball)
  - Status dropdown (All, Draft, Priced, Ready, Listed, Sold)
  - "Refresh" button
  - Separator line
  - "Select All" / "Deselect All" buttons
  - "X selected" count

**Search** - Type player name, brand, team, or manufacturer (searches as you type)

**Sport Filter** - Show only Football, Baseball, or Basketball cards

**Status Filter**:
- **Draft** - Scanned but not priced
- **Priced** - Has listing price
- **Ready** - Priced and ready for export
- **Listed** - Exported/uploaded to marketplace
- **Sold** - Marked as sold with sale tracking

### Bulk Selection

**📸 SCREENSHOT PLACEHOLDER: `inventory-bulk-select.png`**
**Instructions:**
- Click "Select All" button
- Multiple checkboxes should be checked
- **Expected elements:**
  - Multiple rows with checked checkboxes
  - "X selected" count updated
  - Action buttons at bottom enabled

1. **Check individual boxes** - Select specific cards
2. **Click "Select All"** - Check all visible (filtered) cards
3. **Click "Deselect All"** - Uncheck all

Selection count shows: "5 selected"

### Card Actions

**📸 SCREENSHOT PLACEHOLDER: `inventory-actions.png`**
**Instructions:**
- Select one card (click a row)
- Show action button row at bottom
- **Expected elements:**
  - Summary stats (Total, Priced, Needs Pricing, Value, Stale count)
  - "Edit Card" button
  - "Reprice Card" button
  - "Delete Selected" button
  - "Mark as Sold" button

**Single Card Actions:**
- **Edit Card** - Opens edit form to modify any field
- **Reprice Card** - Moves card back to Draft status for repricing
- **Delete Selected** - Removes card from inventory (with confirmation)
- **Mark as Sold** - Records sale with profit tracking

**Bulk Actions:**
- **Export Selected to CSV** - Generate Whatnot/eBay/COMC CSV
- **Upload Images for Selected** - Batch upload to ImgBB
- **Delete Selected** - Remove multiple cards

### Marking Cards as Sold

**📸 SCREENSHOT PLACEHOLDER: `inventory-sold-dialog.png`**
**Instructions:**
- Select a card with a price
- Click "Mark as Sold" button
- Dialog should appear
- **Expected elements:**
  - "Mark as Sold" heading
  - Player name displayed
  - Sale Price input
  - Platform dropdown (Whatnot/eBay/Other)
  - Fees input (auto-calculated)
  - Shipping Cost input
  - Net Profit display (green text)
  - "Cancel" and "Mark as Sold" buttons

1. **Select a sold card** - Click row or checkbox
2. **Click "Mark as Sold"** - Dialog opens
3. **Enter Sale Price** - What the buyer paid
4. **Select Platform** - Whatnot, eBay, or Other
5. **Fees auto-calculate** - Based on platform fee % in Settings
6. **Enter Shipping Cost** - What you paid to ship
7. **Review Net Profit** - Sale Price - Cost Basis - Fees - Shipping
8. **Click "Mark as Sold"** - Saves to Reports

Net profit is automatically calculated: `Sale Price - Cost Basis - Fees - Shipping Cost`

---

## Exporting to Whatnot

The Export page generates CSV files for bulk uploads to Whatnot, eBay, or COMC.

### Preparing for Export

**📸 SCREENSHOT PLACEHOLDER: `export-view-overview.png`**
**Instructions:**
- Click "Export" in left navigation
- **Expected elements:**
  - Export summary (X cards selected)
  - Platform selector dropdown (Whatnot/eBay/COMC/Generic)
  - "Select Cards to Export" button
  - Preview section (empty or showing selected cards)
  - Action buttons at bottom

Before exporting:
1. **Price your cards** - Only cards with listing prices can export
2. **Upload images** (optional) - For Whatnot, you need hosted image URLs

### Selecting Export Platform

**📸 SCREENSHOT PLACEHOLDER: `export-platform-selector.png`**
**Instructions:**
- Focus on platform selector dropdown
- **Expected elements:**
  - Dropdown showing "Whatnot" (or other platform)
  - Help text explaining platform-specific title templates

Choose your export platform:
- **Whatnot** - Uses Whatnot-optimized title template
- **eBay** - Uses eBay-optimized title template
- **COMC** - Uses COMC format
- **Generic** - Basic title format

Each platform uses a different title template optimized for that marketplace's search algorithm.

### Selecting Cards

**📸 SCREENSHOT PLACEHOLDER: `export-select-cards.png`**
**Instructions:**
- Click "Select Cards to Export" button
- **Expected elements:**
  - Card selection grid/list
  - Checkboxes for each card
  - Status indicators (only Ready/Priced cards shown)
  - "Export Selected" button

1. **Click "Select Cards to Export"** - Opens Inventory view
2. **Check cards to export** - Select using checkboxes
3. **Return to Export view** - Selected cards appear in preview

FlipKit validates each card before export:
- ✅ Has listing price
- ✅ Has player name
- ⚠️ Missing image URL (optional warning)

### Exporting CSV

**📸 SCREENSHOT PLACEHOLDER: `export-csv-button.png`**
**Instructions:**
- Cards selected and ready for export
- **Expected elements:**
  - Export summary showing count
  - "Export CSV" button (large, accent)
  - Success/error message area below

1. **Review selected cards** - Verify all are ready
2. **Click "Export CSV"** - Save dialog appears
3. **Choose filename** - Default: `whatnot-export-2026-02-07.csv`
4. **Click Save** - CSV file is generated

**📸 SCREENSHOT PLACEHOLDER: `export-success-message.png`**
**Instructions:**
- After export completes
- **Expected elements:**
  - Green success message: "Exported X cards to CSV"
  - File path shown

Success message: `"Exported 25 cards to CSV"`

### CSV Format

The exported CSV includes these columns (Whatnot format):
- **Title** - SEO-optimized, platform-specific
- **Description** - Auto-generated from card details
- **Price** - Your listing price
- **Quantity** - Default: 1
- **Category** - Sport category
- **Subcategory** - Player/team subcategory
- **Images** - Image URL (if uploaded to ImgBB)
- **Condition** - Near Mint, Excellent, etc.
- **Attributes** - Rookie, Auto, Relic, etc.

### Uploading to Marketplace

After exporting:
1. **Go to Whatnot Seller Hub** (or eBay, COMC)
2. **Find Bulk Upload tool**
3. **Upload your CSV file**
4. **Preview and publish**

Your cards are now live on the marketplace!

---

## Reports & Analytics

The Reports page shows sales history and profitability.

**📸 SCREENSHOT PLACEHOLDER: `reports-overview.png`**
**Instructions:**
- Click "Reports" in left navigation
- Must have at least one sold card in database
- **Expected elements:**
  - Date range selector (From/To dates)
  - "Generate Report" button
  - Summary cards showing totals
  - Monthly breakdown table
  - Top sellers list

### Generating Sales Reports

1. **Select date range** - From and To dates
2. **Click "Generate Report"** - Calculates sales for date range
3. **Review summary** - Total revenue, costs, fees, profit

**📸 SCREENSHOT PLACEHOLDER: `reports-summary.png`**
**Instructions:**
- After generating a report
- **Expected elements:**
  - Four summary cards in a row:
    - Total Revenue (sale prices sum)
    - Total Cost (cost basis sum)
    - Total Fees (fees paid sum)
    - Net Profit (green, large number)
  - Profit margin percentage

Summary shows:
- **Total Revenue** - Sum of all sale prices
- **Total Cost** - Sum of all cost basis
- **Total Fees** - Sum of marketplace fees
- **Net Profit** - Revenue - Cost - Fees
- **Profit Margin** - (Net Profit / Revenue) × 100%

### Monthly Breakdown

**📸 SCREENSHOT PLACEHOLDER: `reports-monthly.png`**
**Instructions:**
- Scroll to monthly breakdown table
- **Expected elements:**
  - Table with columns: Month, Cards Sold, Revenue, Fees, Net Profit
  - Multiple months listed
  - Totals row at bottom

Shows sales by month:
- Month (Jan 2026, Feb 2026, etc.)
- Cards Sold count
- Revenue for month
- Fees paid
- Net Profit

### Top Sellers

**📸 SCREENSHOT PLACEHOLDER: `reports-top-sellers.png`**
**Instructions:**
- Scroll to "Top Sellers" section
- **Expected elements:**
  - List of cards sorted by profit
  - Player name, sale price, profit for each
  - Top 10 or 20 cards

Lists your most profitable cards:
- Player name
- Sale price
- Net profit
- Sorted highest to lowest

### Tax Report Export

**📸 SCREENSHOT PLACEHOLDER: `reports-tax-export.png`**
**Instructions:**
- Show "Export Tax Report" button
- **Expected elements:**
  - "Export Tax Report CSV" button
  - Description of what's included
  - Date range selector

1. **Set date range** - Full year (Jan 1 - Dec 31)
2. **Click "Export Tax Report CSV"** - Generates tax-ready CSV
3. **Give to accountant** - Contains all sale transactions

Tax CSV includes:
- Sale Date
- Player Name
- Sale Price
- Cost Basis
- Fees Paid
- Shipping Cost
- Net Profit
- Platform

---

## Settings & Configuration

The Settings page controls all application preferences, API keys, and templates.

### General Settings

**📸 SCREENSHOT PLACEHOLDER: `settings-general.png`**
**Instructions:**
- Click "Settings" in left navigation
- Show the General/Preferences section
- **Expected elements:**
  - Default Model dropdown
  - eBay Seller checkbox
  - Default Shipping Profile dropdown
  - Default Condition dropdown

**Default AI Model** - Which OpenRouter model to use for scanning (default: nvidia/nemotron-nano-12b-v2-vl:free)

**eBay Seller** - Check if you're an eBay seller (affects title templates)

**Default Shipping Profile** - PWE (4 oz) or BMWT (bubble mailer)

**Default Condition** - Near Mint, Mint, Excellent, etc.

### API Configuration

We covered this in the [Initial Setup](#initial-setup) section. The Settings page lets you update API keys anytime:
- OpenRouter API Key
- ImgBB API Key
- eBay Browse API credentials

**📸 SCREENSHOT PLACEHOLDER: `settings-api-complete.png`**
**Instructions:**
- Scroll to show all three API sections
- Enter dummy/test keys or blur real keys
- Show the eBay compliance warning box
- **Expected elements:**
  - Three API key sections with test buttons
  - Orange compliance warning box for eBay
  - Status messages for each API

### Card Scanning Settings

**📸 SCREENSHOT PLACEHOLDER: `settings-scanning.png`**
**Instructions:**
- Scroll to "Card Scanning" section
- **Expected elements:**
  - Enable Variation Verification checkbox
  - Auto-Apply High Confidence Suggestions checkbox
  - Run Confirmation Pass checkbox
  - Enable Checklist Learning checkbox
  - Max Concurrent Scans number input (1-8)

**Enable Variation Verification** - Cross-check AI results against checklists

**Auto-Apply High Confidence Suggestions** - Automatically apply exact checklist matches

**Run Confirmation Pass** - Ask AI to double-check ambiguous results

**Enable Checklist Learning** - Learn from your saved cards to improve future verification

**Max Concurrent Scans** - How many cards to scan simultaneously (bulk scanning)
- Free models: Use 1 (rate limited)
- Paid models: Use 3-4 for optimal speed

### Financial Settings

**📸 SCREENSHOT PLACEHOLDER: `settings-financial.png`**
**Instructions:**
- Scroll to "Financial" section
- **Expected elements:**
  - Whatnot Fee % input (11.0 default)
  - eBay Fee % input (13.25 default)
  - Default Shipping Cost PWE (1.00 default)
  - Default Shipping Cost BMWT (4.50 default)
  - Price Staleness Threshold Days (30 default)

**Whatnot Fee Percent** - Marketplace fee for Whatnot (default 11%)

**eBay Fee Percent** - Marketplace fee for eBay (default 13.25%)

**Default Shipping Cost PWE** - Plain White Envelope (default $1.00)

**Default Shipping Cost BMWT** - Bubble Mailer With Tracking (default $4.50)

**Price Staleness Threshold Days** - How old before price is flagged as stale (default 30 days)

### Title Templates (Advanced)

**📸 SCREENSHOT PLACEHOLDER: `settings-title-templates.png`**
**Instructions:**
- Scroll to "Title Templates" section
- **Expected elements:**
  - Active Export Platform dropdown
  - Four template textboxes (Whatnot, eBay, COMC, Generic)
  - Placeholder help text
  - Template preview box
  - "Reset to Default" buttons

FlipKit uses platform-specific title templates optimized for each marketplace's search algorithm. Templates use placeholders like `{Year}`, `{Brand}`, `{Player}`, etc.

**Default templates:**
- **Whatnot:** `{Year} {Brand} {Player} {Parallel} {Attributes} {Grade}`
- **eBay:** `{Year} {Manufacturer} {Brand} {Player} {Team} {CardNumber} {Parallel} {Attributes} {Grade}`
- **COMC:** `{Year} {Brand} {Player} {CardNumber} {Parallel} {Grade}`

You can customize these to match your listing style. Changes apply to future exports.

### Search Query Templates (Advanced)

**📸 SCREENSHOT PLACEHOLDER: `settings-search-templates.png`**
**Instructions:**
- Scroll to "Search Query Templates" section
- **Expected elements:**
  - Terapeak Search Template textbox
  - eBay Search Template textbox
  - Help text
  - Preview box

These templates control how pricing research links are built:
- **Terapeak:** `{Year} {Brand} {Player} {Parallel} {Attributes} {Grade}`
- **eBay Sold:** `{Year} {Manufacturer} {Brand} {Player} {Team} {Parallel} {Attributes} {Grade}`

Note: Card Number and Serial Number are deliberately excluded to get broader comparable results.

### Saving Settings

**📸 SCREENSHOT PLACEHOLDER: `settings-save-button.png`**
**Instructions:**
- Scroll to very bottom of Settings page
- **Expected elements:**
  - "Save Settings" button (large, accent)
  - Success message area
  - Database location info

After making changes:
1. **Click "Save Settings"** - Saves to `config.json` in your app data folder
2. **Success message appears** - "Settings saved!"

Settings are stored locally: `%LocalAppData%\FlipKit\config.json` (Windows)

---

## Tips & Best Practices

### Photography Tips

**For Best AI Accuracy:**
- ✅ Use good lighting - natural light or bright LED
- ✅ Fill the frame - card should be 70-90% of image
- ✅ Keep camera/phone level - minimize perspective distortion
- ✅ Focus clearly - no blur
- ✅ Plain background - solid color, ideally white or black
- ❌ Avoid glare on card surface
- ❌ Avoid shadows or dark corners
- ❌ Don't crop too tight - leave small border

**Ideal resolution:** 800-2000px width. Higher isn't always better for AI.

### When to Scan Back Images

Only scan back images when they contain critical information:
- ✅ Serial numbers on back only
- ✅ Autograph on back
- ✅ Dual-sided cards (Topps Now, etc.)
- ✅ Memorabilia/relic on back
- ❌ Standard back (logo, player stats) - not needed

Front image alone is sufficient for 90% of cards.

### Bulk Scanning Workflow

**Recommended approach for 50-150 cards:**

1. **Take all photos first** - Dedicated photography session
2. **Organize files** - Name consistently, sort by sport or set
3. **Load 20-30 cards at a time** - Manageable batches
4. **Review results** - Fix obvious errors
5. **Save batch** - Repeat for next batch

Don't try to scan 150 cards in one session - break into smaller batches.

### Pricing Strategy

**Research thoroughness by card value:**
- **$1-5 cards** - Quick automated comps or bulk pricing
- **$5-25 cards** - Automated comps + quick Terapeak check
- **$25-100 cards** - Automated comps + thorough Terapeak/eBay sold research
- **$100+ cards** - Manual deep research, multiple sold listings, consider grading

Always price higher-value cards conservatively until you build sales history.

### Checklist Verification

**When verification suggests changes:**
- ✅ Apply if it's a parallel correction - checklists are usually right
- ✅ Apply if it's a player name spelling fix
- ⚠️ Review carefully if AI found an auto/relic and checklist doesn't list it
- ❌ Ignore if you're certain the AI is correct (e.g., you can see the parallel name on card)

Checklists are 95%+ accurate but not perfect. Use your judgment.

### Graded Cards

**For slabbed cards:**
- ✅ AI usually detects PSA, BGS, CGC, SGC correctly
- ✅ Grade value (10, 9.5, etc.) is usually extracted correctly
- ⚠️ Check cert number - sometimes misread
- ⚠️ Check auto grade (BGS) - sometimes missed

Pricing searches automatically look across ALL grading companies for better comps.

### Image Hosting for Whatnot

**ImgBB upload workflow:**
1. **Price all your cards first** - Don't upload until ready to list
2. **Export → Select cards** - Choose cards you're listing this week
3. **Click "Upload Images for Selected"** - Batch upload (~2-3 min for 20 cards)
4. **Export CSV** - Now includes image URLs

Don't upload images too early - you might reprice or decide not to list.

---

## Troubleshooting

### AI Scan Issues

**Problem:** AI returns empty/incorrect results

**Solutions:**
- Check OpenRouter API key in Settings
- Try a different AI model (some models work better for certain card types)
- Retake photo with better lighting
- Try cropping photo tighter around card
- Check photo isn't blurry or glare-obscured

**Problem:** "Rate limit exceeded" error

**Solutions:**
- Wait 4 seconds between scans (free models rate limited)
- FlipKit auto-rotates through 11 models on rate limits
- For faster scanning, use paid API credits and increase Max Concurrent Scans

### Verification Issues

**Problem:** "No checklist found for this set"

**This is normal** - FlipKit has 97 seeded checklists (2017-2024 major releases). Older sets and niche brands aren't covered yet.

**Solutions:**
- Proceed without verification (card still saves)
- Manually verify against online checklists (Cardboard Connection, Beckett)
- Enable Checklist Learning in Settings - app will learn from your cards

**Problem:** Verification suggests wrong parallel

**Solutions:**
- Double-check card against checklist manually
- If AI is correct, ignore suggestion
- If checklist is correct, apply suggestion
- Report discrepancy on GitHub if you believe checklist is wrong

### Pricing Issues

**Problem:** "eBay API credentials not configured"

**Solutions:**
- Go to Settings → eBay Browse API
- Enter Client ID and Client Secret
- Complete opt-out process in eBay Developer portal (see [Initial Setup](#initial-setup))
- Click Test to verify connection

**Problem:** "No comparable listings found"

**This is normal** for obscure cards, old cards, or extremely rare parallels.

**Solutions:**
- Use manual Terapeak/eBay sold research instead
- Try searching for base card (not parallel) to get ballpark value
- Check sold listings on other platforms (COMC, eBay sold auctions)

**Problem:** Automated comps show crazy high/low prices

**Rare but possible** if only a few listings exist with outliers.

**Solutions:**
- Check the Sample Size and Confidence level
- Low confidence (1-2 matches) → verify manually
- Look at Range (low-high) to spot outliers
- Use manual research for valuable cards

### Export Issues

**Problem:** CSV export validation errors

**Common validation errors:**
- Missing listing price → Go to Pricing, add price
- Missing player name → Edit card, add player name
- Missing image URL → Either upload to ImgBB or export without images

**Problem:** Whatnot bulk upload rejects CSV

**Solutions:**
- Check CSV encoding (should be UTF-8)
- Open CSV in Excel/Google Sheets - verify columns match Whatnot template
- Check for special characters in titles (some characters may cause issues)
- Verify image URLs are valid (test one URL in browser)

### Performance Issues

**Problem:** App feels slow with 1000+ cards

**Solutions:**
- Use Status filter in Inventory to limit displayed cards
- Use Search to narrow results
- Consider archiving sold cards to a separate database (export → delete)

**Problem:** Bulk scan freezing or crashing

**Solutions:**
- Reduce batch size (scan 20-30 cards at a time, not 150)
- Lower Max Concurrent Scans to 1-2
- Check available RAM (each scan uses ~100-200MB)
- Close other applications

### Database Issues

**Problem:** "Database locked" or "Database is read-only"

**Solutions:**
- Close all other FlipKit instances
- Check file permissions on `%LocalAppData%\FlipKit\cards.db`
- Restart FlipKit
- If persists, backup database and try deleting the `-wal` and `-shm` files

**Database location:**
- Windows: `C:\Users\[YourName]\AppData\Local\FlipKit\cards.db`
- macOS: `~/Library/Application Support/FlipKit/cards.db`
- Linux: `~/.local/share/FlipKit/cards.db`

### General Issues

**Problem:** App won't launch (Windows)

**Solutions:**
- Check Windows Defender / antivirus isn't blocking
- Verify all files extracted from ZIP (especially `.dll` files)
- Right-click FlipKit.exe → Properties → Unblock
- Try running as Administrator

**Problem:** App won't launch (macOS)

**Solutions:**
- Run `chmod +x FlipKit` in Terminal
- Right-click FlipKit → Open (bypasses Gatekeeper first time)
- Go to System Preferences → Security → Allow FlipKit
- For Apple Silicon: Make sure you downloaded `-arm64` version

**Problem:** Lost settings or data

**Solutions:**
- Check `%LocalAppData%\FlipKit\` folder - all data is there
- `config.json` - settings backup
- `cards.db` - database backup
- Copy these files to new computer to migrate

---

## Getting Help

### Resources

- **GitHub Repository:** https://github.com/mthous72/FlipKit
- **Issue Tracker:** https://github.com/mthous72/FlipKit/issues
- **Releases:** https://github.com/mthous72/FlipKit/releases

### Reporting Bugs

When reporting issues, please include:
1. **OS and version** (Windows 11, macOS Sonoma, etc.)
2. **FlipKit version** (from Settings → About)
3. **Steps to reproduce** the issue
4. **Error messages** (if any)
5. **Screenshots** (if applicable)

### Contributing

FlipKit is open source! Contributions welcome:
- **Checklist data** - Add missing sets from pre-2017 or niche brands
- **Bug fixes** - Fix issues you encounter
- **Feature requests** - Suggest improvements
- **Documentation** - Improve this guide

---

## Appendix: Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl/Cmd + S` | Save current card (Scan view) |
| `Ctrl/Cmd + N` | New scan (Scan view) |
| `Ctrl/Cmd + →` | Next card (Pricing view) |
| `Ctrl/Cmd + ←` | Previous card (Pricing view) |
| `Ctrl/Cmd + F` | Focus search box (Inventory) |
| `Ctrl/Cmd + A` | Select all (Inventory) |
| `Ctrl/Cmd + ,` | Open Settings |

---

## Appendix: Supported Card Checklists

FlipKit ships with 97 pre-seeded checklists:

**Football (48 sets):**
- Panini Prizm (2017-2024)
- Panini Donruss (2017-2024)
- Panini Donruss Optic (2017-2024)
- Panini Mosaic (2020-2024)
- Panini Phoenix (2023-2024)
- Panini Select (2017-2024)

**Basketball (21 sets):**
- Panini Prizm (2018-2024)
- Panini Donruss Optic (2019-2024)
- Panini Mosaic (2019-2024)

**Baseball (28 sets):**
- Topps Chrome (2018-2024)
- Bowman (2018-2024)
- Bowman Chrome (2018-2024)
- Topps (2018-2024)

For sets not in this list, AI scanning still works but verification won't catch hallucinated parallels.

---

**End of User Guide**

*This guide reflects FlipKit version 1.0 (February 2026). Features and UI may change in future updates.*
