# FlipKit Manual Regression Checklist

**Purpose:** End-to-end smoke pass after every refactor phase or before every release. Catches breakage that the automated test suite can't see (real browser rendering, real OpenRouter calls, real file system, real cross-process server orchestration).

**Test environment:** Clean Windows machine with `cards.db` containing 5+ test cards spanning Draft/Ready/Listed/Sold statuses. Run with valid OpenRouter + ImgBB API keys configured. Both Desktop and Web servers should be reachable.

**Cadence:** Run after every phase merge. If any flow fails, the merge is blocked until the failure is fixed or explicitly waived (with reason logged).

---

## Critical user flows (10)

Tick each box as the flow passes. Date the run at the bottom.

### Desktop

- [ ] **1. Cold start** — Launch FlipKit Desktop from `dotnet run --project FlipKit.Desktop`. Database initializes, MainWindow loads with sidebar visible (assumes valid config). Settings show `OpenRouter: Configured` / `ImgBB: Configured` if keys are set.

- [ ] **2. Single scan** — Pick a card image via Scan view → click "Scan" with the default free OpenRouter model → verification populates → save card. The card appears in Inventory with status Ready (if both image and price set) or Draft.

- [ ] **3. Bulk scan** — Pick 6 images from BulkScan view (3 front/back pairs with `ImagesArePairs` checked) → click "Scan All" → progress bar advances → all 3 cards complete → save all → cards appear in Inventory.

- [ ] **4. Inventory CRUD** — In Inventory view: filter by Status=Ready, change to Sport=Baseball. Edit a card via the side panel (change PlayerName), save, verify the change persists after a tab switch. Delete a card via the confirm dialog.

- [ ] **5. Pricing research** — In Pricing view: pick an unpriced card → click "Open Terapeak" → browser opens to a Terapeak research URL with the card's identifying fields. Repeat for "Open eBay Sold". Type a market value → suggested price + net-after-fees populate.

- [ ] **6. Whatnot CSV export** — In Export view: select 5 Ready cards → choose Whatnot platform → export. CSV file is created. Open in Excel and verify: 21 columns present, prices are positive integers, Hazmat = "Not Hazmat", Type column is "Buy it Now" (lowercase 'it').

- [ ] **7. eBay Bulk CSV export** — Same 5 cards, change platform to eBay → export. Open the CSV: header preserved verbatim from `ebay_template_header.csv`, data rows append with `*Action=Add`, `*Format=FixedPrice`, image URLs pipe-delimited and HTTPS.

- [ ] **8. Reports view** — At least one card in Sold status. Reports view loads showing CardsSold count, TotalRevenue, NetProfit, monthly breakdown chart, and top sellers list. Click "Export Tax CSV" → 8-column CSV is created with sale dates, costs, fees, net profit per row.

### Web

- [ ] **9. Web standalone** — `dotnet run --project FlipKit.Web --urls "http://0.0.0.0:5000"`. Open `http://localhost:5000` in a browser. Home redirects to `/Scan`. Upload a card image via the mobile-style scan upload → verification view appears → save card → `/Inventory` shows the new card.

### Hub server orchestration

- [ ] **10. Embedded servers** — In Desktop's Settings view, with both servers stopped, click "Start Web Server". Status changes from "Stopped" → "Starting..." → "Running on port 5000". `http://localhost:5000` is reachable from a browser. Click "Stop Web Server" → status returns to "Stopped". Repeat for API server (port 5001).

---

## Optional smoke layer (when added)

- [ ] **`test-web-app.ps1`** — pings 9 known web routes (Home, Inventory, Scan, Pricing, Export, Reports, Sales, Financial). All return 200 with expected content. Currently kept as a fallback bridge until Phase 4e's `WebApplicationFactory<Program>` integration tests cover the same surface — see AUDIT Q3.

---

## Run history

| Date | Run by | Phase | Result |
|---|---|---|---|
| _Add row when checklist passes/fails_ | | | |
