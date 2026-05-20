# FlipKit Web Application - User Guide

**Version:** 1.0 (Web MVP)
**Last Updated:** February 7, 2026

---

## What is FlipKit Web?

FlipKit Web is a mobile-friendly companion to the FlipKit Desktop application. Access your card inventory, scan new cards, and manage listings from any device with a web browser - perfect for on-the-go card shopping at shows or quick price checks from your phone.

### Key Features

- 📱 **Mobile Camera Scanning** - Use your phone's camera to scan cards directly
- 🔄 **Shared Database** - Seamless sync with FlipKit Desktop app
- 💰 **Price Research** - Quick access to eBay and Terapeak pricing tools
- 📦 **Inventory Management** - Browse, search, edit, and delete cards
- 📤 **CSV Export** - Generate Whatnot-compatible bulk uploads
- 📊 **Reports** - Sales and financial analytics on any device

### Requirements

- FlipKit Desktop app running on your computer
- Web browser (Chrome, Edge, Firefox, Safari)
- Same local network (Wi-Fi) for mobile access

---

## Getting Started

### Accessing from Computer

1. Ensure FlipKit Desktop is running
2. Open your web browser
3. Navigate to: **http://localhost:5000**

### Accessing from Phone/Tablet

1. Make sure your mobile device is on the same Wi-Fi network as your computer
2. Find your computer's IP address (e.g., `192.168.1.100`)
   - Windows: Run `ipconfig` in Command Prompt
   - Mac: System Preferences → Network
3. Open your mobile browser
4. Navigate to: **http://YOUR-COMPUTER-IP:5000**
   - Example: `http://192.168.1.100:5000`

> **Tip:** Bookmark this URL on your phone for quick access!

---

## Dashboard

The dashboard provides a quick overview of your inventory and sales performance.

### What You'll See

**Card Status Counts:**
- Draft - Scanned but not priced
- Priced - Ready for listing
- Ready - Validated for export
- Listed - Currently on marketplace
- Sold - Sale completed

**Financial Metrics:**
- Active Inventory Value - Total listing price of unsold cards
- Total Revenue - Sales from sold cards
- Total Profit - Revenue minus costs

**Quick Actions:**
- Jump to pricing research
- Mark cards as ready for export
- Generate CSV files

---

## Scanning Cards

Perfect for scanning cards on-the-go at card shows or shops.

### Mobile Camera Workflow

1. **Open Scan Page**
   - Tap "Scan" in the top navigation

2. **Select AI Model** (Optional)
   - Default: GPT-4o Mini (fast, free)
   - For difficult cards: GPT-4o or Claude (more accurate)

3. **Take Front Photo**
   - Tap "Choose File" or camera icon
   - On mobile: Camera opens automatically
   - Position card to fill frame
   - Avoid glare and shadows
   - Tap to capture

4. **Add Back Photo** (Optional)
   - Useful for serial numbers or autographs
   - Skip for standard cards

5. **Scan Card**
   - Tap "Scan Card" button
   - Wait 30-60 seconds for AI processing
   - Loading spinner shows progress

6. **Review Results**
   - AI extracts player, year, brand, parallel, etc.
   - **Verification confidence:**
     - 🟢 High - Very confident, likely accurate
     - 🟡 Medium - Review suggestions shown
     - 🔴 Low - Manual verification recommended
   - Edit any incorrect fields

7. **Save to Inventory**
   - Tap "Save Card" to add to database
   - Card appears in Inventory immediately
   - Status set to "Draft" (needs pricing)

**Or discard:**
- Tap "Discard" to delete without saving

### Photo Tips

- Use good lighting (natural light best)
- Hold phone level/parallel to card
- Fill 70-90% of frame with card
- Ensure text is sharp and readable
- Plain background if possible

---

## Managing Inventory

View and manage all your cards.

### Browsing Cards

The inventory table shows:
- Player name with badges (Rookie, Auto, Graded)
- Sport, year, brand, set, card number
- Current price (if set)
- Action buttons

### Search & Filter

**Search by Player Name:**
- Type in search box
- Partial matches work ("Mah" finds "Mahomes")

**Filter by Sport:**
- Select: Baseball, Football, Basketball, Hockey, Soccer
- Shows only that sport

**Filter by Status:**
- Draft, Priced, Ready, Listed, Sold
- Focus on cards needing action

### Card Actions

**View Details:**
- Tap "Details" to see complete card information
- Shows all fields, images, grading, pricing

**Edit Card:**
- Tap "Edit" button
- Update any field
- Tap "Save" to update database

**Delete Card:**
- Tap "Delete" button
- Confirm in popup
- ⚠️ **Warning:** Permanent and cannot be undone

### Pagination

- 20 cards per page
- Use page numbers to navigate
- Previous/Next buttons for quick browsing

---

## Pricing Cards

Research prices and set listing values.

### Research Workflow

1. **Navigate to Pricing**
   - Tap "Pricing" in top menu
   - Shows cards with Status = Draft or Priced

2. **Select Card**
   - Tap "Research Price" for a card
   - Opens research page with card details

3. **External Research**
   - **Terapeak Link** - Historical sold prices and trends
   - **eBay Sold Link** - Recent completed sales
   - Opens in new tab for comparison

4. **Set Prices**
   - **Market Value** - What card typically sells for
   - **Listing Price** - Your asking price
   - Profit calculator updates automatically

5. **Review Profit Calculator**
   - Shows breakdown in real-time:
     - Listing price: $100.00
     - Fees (11%): -$11.00
     - Net revenue: $89.00
     - Cost basis: -$25.00
     - **Net profit: $64.00 (256%)**

6. **Save Pricing**
   - Tap "Save Pricing"
   - Status changes to "Priced"
   - Returns to pricing list

### Profit Calculator

Automatically calculates:
- Platform fees (11% for Whatnot by default)
- Net revenue after fees
- Profit margin percentage
- Break-even point

Updates as you type!

---

## Exporting to CSV

Generate bulk upload files for Whatnot.

### Export Workflow

1. **Navigate to Export**
   - Tap "Export" in top menu

2. **Two Sections:**
   - **Ready for Export** - Cards with Status = Ready
   - **Priced Cards** - Cards with prices but not marked ready

3. **Mark as Ready**
   - Find card in "Priced Cards" section
   - Tap "Mark as Ready"
   - Card moves to "Ready for Export"

4. **Preview Export Data** (Optional)
   - Tap "Preview" next to any card
   - Review generated title and description
   - Check for validation errors
   - Fix issues before marking ready

5. **Generate CSV**
   - Tap "Generate Whatnot CSV" button
   - File downloads automatically
   - Filename includes timestamp

6. **Upload to Whatnot**
   - Go to Whatnot Seller Dashboard
   - Navigate to Inventory → Bulk Upload
   - Select downloaded CSV file
   - Review and publish listings

### CSV Contents

Includes:
- Title (optimized for search)
- Description (auto-generated from card details)
- Price (your listing price)
- Quantity
- Images (if uploaded to ImgBB)
- Category and attributes

---

## Reports & Analytics

Track sales and profitability.

### Reports Dashboard

**Inventory Statistics:**
- Total cards by status
- Breakdown by sport with percentages

**Financial Overview:**
- Active inventory value and cost
- Total revenue from sold cards
- Total profit

**Recent Sales:**
- Last 10 sales in past 30 days
- Shows player, price, profit

### Sales Report

1. **Navigate to Reports → Sales**
2. **Set Date Range**
   - From date
   - To date
   - Default: Last 30 days
3. **Tap Filter**

**Metrics:**
- Total sales count
- Total revenue
- Total profit
- Average profit per card

**Detailed Table:**
- Sale date, player, card details
- Sale price, cost, profit
- Profit margin percentage

### Financial Report

**Overall Stats:**
- Inventory value vs. cost
- Realized profit from sales
- Overall profit margin
- Inventory turnover rate

**Profitability by Sport:**
- Revenue and profit per sport
- Average profit per card
- Profit margin by sport
- Visual distribution chart

**Use For:**
- Identify most profitable sports
- Optimize inventory allocation
- Business planning

---

## Tips & Best Practices

### Mobile Scanning

**For Best Results:**
- Clean camera lens first
- Use well-lit area (natural light)
- Avoid shadows and glare
- Hold phone steady
- Retake if blurry

**When to Use:**
- At card shows (quick scan while shopping)
- On the go (scan pulls from breaks)
- When away from computer

### Pricing Strategy

**Research Depth:**
- $1-5 cards: Quick check
- $5-25 cards: Compare 3-5 recent sales
- $25-100 cards: Thorough research, multiple comps
- $100+ cards: Deep research, consider condition carefully

**Set Realistic Prices:**
- Check at least 5-10 recent sold comps
- Consider card condition
- Account for current player performance
- Factor in seasonal trends

### Inventory Management

**Stay Organized:**
- Set status immediately after pricing
- Use Location field for storage tracking
- Add notes for special handling
- Update sold cards promptly

**Batch Operations:**
- Price similar cards together
- Mark multiple as ready at once
- Export in batches by sport/set

### Export Best Practices

**Before Exporting:**
- Use Preview to validate all cards
- Fix any errors before marking ready
- Ensure images are uploaded (if using)
- Review titles for accuracy

**After Exporting:**
- Keep CSV file for records
- Track which cards were exported
- Monitor Whatnot import for errors
- Update status to Listed after publishing

---

## Troubleshooting

### Common Issues

**"Card not found" error**
- Card may have been deleted in Desktop app
- Refresh the page
- Check Inventory to confirm

**Images not displaying**
- ImgBB service may be temporarily down
- Check internet connection
- Re-upload images if necessary

**Scan taking too long (> 2 minutes)**
- OpenRouter may be rate limiting
- Try different AI model
- Wait and retry
- Check API key balance in Desktop Settings

**CSV download fails**
- No cards marked as Ready
- Validation errors prevent export
- Check browser download settings
- Try different browser

**Profit calculator not updating**
- Enter listing price first
- Ensure JavaScript is enabled
- Refresh page and try again

**Page not loading**
- Ensure Desktop app is running on computer
- Check correct URL (localhost or IP address)
- Verify firewall allows port 5000
- Restart Desktop app

**Can't access from phone**
- Verify same Wi-Fi network
- Check computer's firewall settings
- Try IP address instead of computer name
- Ensure port 5000 is allowed

### Performance Tips

**Slow Page Loads:**
- Use filters to limit displayed cards
- Search for specific cards instead of browsing all
- Close unused browser tabs
- Clear browser cache

**Database Conflicts:**
- Avoid editing same card in Desktop and Web simultaneously
- Refresh page if seeing stale data
- Last save wins in concurrent edits

---

## Mobile Quick Reference Card

### Fast Scan Workflow
1. Open browser → http://IP:5000
2. Tap Scan
3. Take front photo
4. (Optional) Take back photo
5. Tap Scan Card
6. Wait 30-60 seconds
7. Review → Edit if needed
8. Tap Save Card

### Quick Price Check
1. Tap Pricing
2. Find card
3. Tap Research Price
4. Open Terapeak/eBay links
5. Enter market value and listing price
6. Review profit calculator
7. Tap Save Pricing

### Fast Export
1. Tap Export
2. Tap "Mark as Ready" for each priced card
3. Tap "Generate Whatnot CSV"
4. Download → Upload to Whatnot

---

## Keyboard Shortcuts (Desktop Browser)

- **Tab** - Navigate form fields
- **Enter** - Submit active form
- **Esc** - Close modals
- **Alt+Left** - Browser back button
- **Ctrl+F** - Find on page

---

## Getting Help

### Resources

- **Desktop App Guide:** See USER-GUIDE.md for full feature documentation
- **GitHub Issues:** Report bugs or request features
- **Settings:** Configure API keys in Desktop app

### Support

For issues or questions:
1. Check this guide first
2. Try Desktop app USER-GUIDE.md
3. Check GitHub Issues
4. Report new issues with:
   - Browser and OS version
   - Steps to reproduce
   - Screenshots if applicable

---

## Differences from Desktop App

**Web App Has:**
- ✅ Mobile camera support
- ✅ Touch-optimized interface
- ✅ Quick access from any device

**Web App Doesn't Have:**
- ❌ Settings configuration (use Desktop)
- ❌ Bulk scan feature (single cards only)
- ❌ Advanced checklist management
- ❌ Detailed setup wizard

**Use Desktop App For:**
- Initial setup and API key configuration
- Bulk scanning (10+ cards at once)
- Detailed settings customization
- Advanced features and reports

**Use Web App For:**
- Mobile scanning at card shows
- Quick inventory checks
- On-the-go pricing research
- Fast CSV exports

---

## What's Next?

**Coming Soon:**
- Authentication (multi-user support)
- Progressive Web App (install on phone home screen)
- Real-time sync between devices
- Bulk scan from web interface
- Dark mode

**Feedback Welcome:**
- Report bugs on GitHub
- Suggest features
- Share your workflow tips

---

**Happy selling from anywhere!** 🎉📱

*This guide covers FlipKit Web v1.0 (February 2026). Features may change in future updates.*
