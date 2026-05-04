# Phase 1 Testing Checklist - Desktop App Refactoring

## Overview
Phase 1 refactored the FlipKit desktop app from a single-project monolith to a 2-project architecture (Core + Desktop). This testing checklist ensures no functionality was broken during the refactoring.

## Build Status
✅ **Build: 0 Errors, 0 Warnings**
✅ **Core Library: Compiles independently**
✅ **Desktop App: Compiles with Core reference**

---

## Pre-Testing Setup

### 1. Database Backup
- [ ] Locate existing database: `%APPDATA%\FlipKit\cards.db`
- [ ] Create backup copy before testing
- [ ] Note: Database should work unchanged (schema is identical)

### 2. Settings Backup
- [ ] Locate settings file: `%APPDATA%\FlipKit\settings.json`
- [ ] Create backup copy
- [ ] Note: Settings format is unchanged

### 3. Launch Application
- [ ] Run: `dotnet run --project FlipKit.Desktop`
- [ ] OR: Double-click `FlipKit.Desktop.exe` after `dotnet build`
- [ ] Verify app window appears without crashes

---

## Critical Path Testing (30 minutes)

### Test 1: Application Startup ✅
**Expected:** App launches to Scan page (or Setup Wizard if first run)

- [ ] Application window opens
- [ ] No console errors visible
- [ ] Sidebar navigation visible
- [ ] Current page indicator shows "Scan"

**If First Run:**
- [ ] Setup Wizard appears
- [ ] Can enter API keys
- [ ] Can complete wizard
- [ ] Navigates to Scan page after completion

---

### Test 2: Navigation Between Pages ✅
**Goal:** Verify INavigationService works correctly

- [ ] Click "Scan" → Scan page loads
- [ ] Click "Bulk Scan" → Bulk Scan page loads
- [ ] Click "Inventory" → Inventory page loads
- [ ] Click "Reports" → Reports page loads
- [ ] Click "Checklists" → Checklist Manager page loads
- [ ] Click "Settings" → Settings page loads
- [ ] Click "Pricing" → Pricing page loads (may be empty if no cards)

**Expected:** All navigation transitions work smoothly, no errors

---

### Test 3: Single Card Scan Workflow ✅
**Goal:** Test AI scanning, verification, and saving

**Prerequisites:** OpenRouter API key configured in Settings

1. **Upload Image:**
   - [ ] Click "Upload Card Image" button
   - [ ] Select a card image from disk
   - [ ] Image preview displays

2. **AI Scan:**
   - [ ] Click "Scan Card" button
   - [ ] Loading indicator appears
   - [ ] Scan results populate form fields
   - [ ] Fields contain reasonable values (Player Name, Year, Sport, etc.)

3. **Verification (if enabled):**
   - [ ] Verification suggestions appear (if checklist match found)
   - [ ] Can accept/modify suggestions
   - [ ] Confidence indicators show (High/Medium/Low)

4. **Save Card:**
   - [ ] Click "Save Card" button
   - [ ] Success message appears
   - [ ] Navigates to Inventory page ✅ (INavigationService test)

5. **Verify in Inventory:**
   - [ ] Navigate to Inventory
   - [ ] Newly scanned card appears in list
   - [ ] Card details match what was scanned

---

### Test 4: Inventory Management ✅
**Goal:** Test CRUD operations and navigation from Inventory

1. **View Cards:**
   - [ ] Inventory page shows list of cards
   - [ ] Card count summary displays correctly
   - [ ] Can scroll through card list

2. **Search/Filter:**
   - [ ] Type in search box → filters cards by player name
   - [ ] Select Sport filter → filters by sport
   - [ ] Select Status filter → filters by status
   - [ ] Clear filters → all cards return

3. **Edit Card:**
   - [ ] Select a card from list
   - [ ] Click "Edit Selected" button
   - [ ] Navigates to Edit Card page ✅ (INavigationService test)
   - [ ] Card details load correctly
   - [ ] Change a field (e.g., Notes)
   - [ ] Click "Save"
   - [ ] Navigates back to Inventory ✅ (INavigationService test)
   - [ ] Changes are persisted (refresh inventory to verify)

4. **Cancel Edit:**
   - [ ] Edit a card
   - [ ] Click "Cancel" button
   - [ ] Navigates back to Inventory ✅ (INavigationService test)
   - [ ] No changes saved

5. **Delete Card:**
   - [ ] Select a card
   - [ ] Click "Delete Selected"
   - [ ] Confirmation dialog appears
   - [ ] Confirm deletion
   - [ ] Card removed from list

---

### Test 5: Pricing Research ✅
**Prerequisites:** Card(s) in inventory

1. **Navigate to Pricing:**
   - [ ] Go to Pricing page
   - [ ] Card selection dropdown appears
   - [ ] Select a card from dropdown

2. **Get Active Comps (if eBay configured):**
   - [ ] Click "Get Active Comps" button
   - [ ] API request executes
   - [ ] Results display (median price, range, confidence)
   - [ ] Market value field auto-fills

3. **External Research:**
   - [ ] Click "Open Terapeak" button
   - [ ] Browser opens to Terapeak URL ✅ (IBrowserService test)
   - [ ] Click "Open eBay Sold" button
   - [ ] Browser opens to eBay URL ✅ (IBrowserService test)

4. **Save Pricing:**
   - [ ] Enter market value manually
   - [ ] Enter listing price
   - [ ] Click "Save Pricing"
   - [ ] Success message appears
   - [ ] Card status updates to "Priced"

---

### Test 6: Export to Whatnot CSV ✅
**Prerequisites:** At least one card with status "Ready for Export"

1. **Navigate to Export:**
   - [ ] Go to Export page
   - [ ] Exportable cards list displays

2. **Mark Card as Ready:**
   - [ ] Go to Inventory
   - [ ] Select card(s)
   - [ ] Click "Mark as Ready for Export"
   - [ ] Card status changes to "Ready"

3. **Generate CSV:**
   - [ ] Go to Export page
   - [ ] Verify card appears in export list
   - [ ] Click "Generate Whatnot CSV" button
   - [ ] File save dialog appears ✅ (IFileDialogService test)
   - [ ] Save CSV to disk
   - [ ] Success message appears

4. **Verify CSV:**
   - [ ] Open generated CSV in Excel/text editor
   - [ ] Verify all required columns present
   - [ ] Verify card data is correct

---

### Test 7: Image Upload to ImgBB ✅
**Prerequisites:** ImgBB API key configured

1. **Upload from Inventory:**
   - [ ] Go to Inventory
   - [ ] Select card(s) without uploaded images
   - [ ] Click "Upload Images to ImgBB" button
   - [ ] Progress bar shows upload status
   - [ ] Success message appears
   - [ ] Card records update with image URLs

2. **Verify URLs:**
   - [ ] Edit an uploaded card
   - [ ] Image URLs populated in fields
   - [ ] URLs are valid ImgBB links

---

### Test 8: Bulk Scan Workflow ✅
**Goal:** Test batch scanning with front/back pairing

1. **Navigate to Bulk Scan:**
   - [ ] Click "Bulk Scan" button
   - [ ] Bulk Scan page loads

2. **Upload Multiple Images:**
   - [ ] Click "Add Front Image" multiple times
   - [ ] Select 3+ card front images
   - [ ] Images appear in front list
   - [ ] Click "Add Back Image" for each
   - [ ] Select corresponding back images
   - [ ] Pairs are correctly matched

3. **Scan All:**
   - [ ] Click "Scan All Cards" button
   - [ ] Progress indicator shows scanning
   - [ ] Each card scanned in sequence
   - [ ] Results populate for each card

4. **Review & Save:**
   - [ ] Review scanned results for each card
   - [ ] Click "Save All" button
   - [ ] Navigate to Inventory
   - [ ] All bulk-scanned cards appear in list

---

### Test 9: Settings Persistence ✅
**Goal:** Verify settings save and load correctly

1. **Modify Settings:**
   - [ ] Go to Settings page
   - [ ] Change a setting (e.g., Default Model)
   - [ ] Click "Save Settings"
   - [ ] Success message appears

2. **Verify Persistence:**
   - [ ] Close application completely
   - [ ] Relaunch application
   - [ ] Go to Settings page
   - [ ] Verify changed setting persisted

3. **API Key Security:**
   - [ ] Settings file at `%APPDATA%\FlipKit\settings.json`
   - [ ] Open in text editor
   - [ ] API keys stored in plain JSON (as expected)

---

### Test 10: Checklist Management ✅
**Goal:** Test checklist loading and management

1. **View Checklists:**
   - [ ] Go to Checklists page
   - [ ] Seed checklists display (if first run)
   - [ ] Can scroll through checklist list

2. **Import Checklist CSV:**
   - [ ] Click "Import Checklist" button
   - [ ] File dialog appears ✅ (IFileDialogService test)
   - [ ] Select a valid checklist CSV
   - [ ] Import succeeds
   - [ ] New checklist appears in list

3. **Search Checklists:**
   - [ ] Type in search box
   - [ ] Checklists filter by manufacturer/brand/year

---

### Test 11: Reports ✅
**Goal:** Verify reporting functionality

1. **Sales Report:**
   - [ ] Go to Reports page
   - [ ] View total cards, sold cards, revenue
   - [ ] Date range filter works (if implemented)

2. **Financial Report:**
   - [ ] Profit/loss calculations display
   - [ ] Charts render correctly (if present)

---

## Service Layer Testing (Automated/Manual)

### Core Services (via Desktop UI)

**ICardRepository:** ✅
- [ ] CRUD operations work (covered in Tests 3-4)
- [ ] Search/filter works (covered in Test 4)

**IScannerService:** ✅
- [ ] AI scanning works (covered in Test 3)
- [ ] Multiple models supported (change in Settings)

**IPricerService:** ✅
- [ ] Pricing calculations work (covered in Test 5)

**IExportService:** ✅
- [ ] CSV export works (covered in Test 6)

**IImageUploadService:** ✅
- [ ] ImgBB upload works (covered in Test 7)

**IVariationVerifier:** ✅
- [ ] Verification suggestions appear (covered in Test 3)

**IChecklistLearningService:** ✅
- [ ] Checklist matching works (covered in Test 3, 10)

**INavigationService:** ✅
- [ ] Page navigation works (covered in Tests 2, 4)
- [ ] Parameterized navigation works (Edit Card - Test 4)

**ISettingsService:** ✅
- [ ] Settings load/save works (covered in Test 9)

**IFileDialogService:** ✅
- [ ] File dialogs work (covered in Tests 6, 10)

**IBrowserService:** ✅
- [ ] External URLs open (covered in Test 5)

---

## Regression Testing (1 hour)

### Edge Cases

1. **Empty Database:**
   - [ ] Delete database file
   - [ ] Launch app
   - [ ] Database recreates
   - [ ] Seed checklists load
   - [ ] App functions normally

2. **Missing Settings:**
   - [ ] Delete settings.json
   - [ ] Launch app
   - [ ] Setup Wizard appears (if configured)
   - [ ] OR: Default settings created

3. **Invalid API Keys:**
   - [ ] Enter invalid OpenRouter API key
   - [ ] Try to scan a card
   - [ ] Error message displays (not crash)
   - [ ] Can recover by fixing API key

4. **Network Failures:**
   - [ ] Disconnect from internet
   - [ ] Try eBay active comps
   - [ ] Error message displays gracefully
   - [ ] Try ImgBB upload
   - [ ] Error message displays gracefully

5. **Large Inventory (Performance):**
   - [ ] If database has 100+ cards
   - [ ] Inventory page loads in <2 seconds
   - [ ] Search/filter is responsive
   - [ ] No UI freezes

---

## Known Issues (Non-Blocking)

### Expected Behaviors
1. **First-run seed data:** Checklists may take 5-10 seconds to seed on first run (normal)
2. **AI scan latency:** Free-tier models may be rate-limited (30-60s wait time)
3. **eBay API limits:** Active comps may fail if rate limit exceeded (temporary)

### Not a Bug
1. **Static Services property removed:** This is intentional - we refactored to instance field
2. **Namespace changes:** All namespaces now FlipKit.Desktop.* or FlipKit.Core.* (intentional)
3. **ViewModelBase location:** Still in Desktop project (not moved to Core) - by design

---

## Test Results Summary

### Pass Criteria
- [ ] All 11 critical path tests pass
- [ ] No crashes or unhandled exceptions
- [ ] Navigation works in all scenarios
- [ ] Database operations succeed
- [ ] Settings persist correctly
- [ ] External integrations work (API calls, file dialogs, browser)

### Test Date: _______________
### Tester: _______________
### Build: `feature/web-app-migration` (Phase 1 complete)

### Issues Found:
| Issue # | Description | Severity | Status |
|---------|-------------|----------|--------|
| 1 | | | |
| 2 | | | |
| 3 | | | |

### Notes:
```
[Add any observations or notes here]
```

---

## Sign-Off

### Phase 1 Refactoring Complete: ☐ YES / ☐ NO

**If YES:**
- All tests passed
- No critical issues found
- Desktop app fully functional
- Ready to proceed to Phase 2 (Web app)

**If NO:**
- Critical issues documented above
- Requires fixes before Phase 2

---

**Tested By:** _______________
**Date:** _______________
**Signature:** _______________
