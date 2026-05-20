# FlipKit eBay & Whatnot Integration Roadmap

**Date:** 2026-05-06  
**Status:** Planning document — no code committed  
**Branch naming convention:** `feature/{kebab-case}` (confirmed from git log)

---

## Phase 1: Repo Investigation Findings

Before planning, the actual codebase was read. Corrections to the premise:

| Claim | Reality |
|---|---|
| Desktop is WPF-like | **Avalonia UI 11** (not WPF) |
| Sales tracking table | **No separate table** — sale fields (`SalePrice`, `SaleDate`, `SalePlatform`, `FeesPaid`, `ShippingCost`, `NetProfit`) live on the `Card` entity with `Status = Sold` |
| Sales populated manually | **Correct** — no automation exists |
| Surprise Set: "separate branch already in design" | **Not found** — no branch, no design doc, no code |
| eBay listing creation via Sell Inventory API | **Correct** — `EbayPublishingService` handles full publish flow |
| eBay CSV export | **Correct** — plus eBay Seller Hub CSV *import* already shipped (v3.3.5) |
| Whatnot: CSV only | **Correct** — no API calls, 21-column CSV via `WhatnotExporter` |
| Background jobs / webhooks | **None** — all operations are synchronous and user-initiated |

### Current eBay Integration State

**APIs called:** Sell Inventory v1 (PUT inventory_item, GET/POST/PUT offer, POST publish), Account v1 (GET fulfillment/payment/return policies)  
**OAuth flow:** Authorization Code Grant (user token). `EnsureValidTokenAsync()` checks expiry, calls `RefreshAccessTokenAsync()` when stale.  
**Scopes:** `sell.inventory`, `sell.account.readonly`  
**Token storage:** ASP.NET Core Data Protection API (DPAPI on Windows, file-based on Linux/Mac), stored in `%LOCALAPPDATA%\FlipKit\config.json`. `DataProtectionSecretEncryption` with `"protected:"` prefix.

### Current Whatnot Integration State

CSV export only. `WhatnotExporter` maps `Card` → 21-column CSV (Category, Sub Category, Title, Description, Qty, Type, Price, Shipping Profile, Offerable, Hazmat, Condition, Cost Per Item, SKU, Image URL 1–8). No Whatnot API calls.

### Key Structural Facts

- **No job scheduler** — Hangfire, Quartz, hosted services: none
- **No webhook receivers** — no signed-request endpoints in Desktop, Web, or Api
- **Secret storage** is adequate for current scope; multi-platform edge cases exist (see item 25)
- **`feature/ebay-browse-api`** branch existed but was deliberately removed — see decision log

---

## Phase 2: Roadmap Entries

---

### eBay Branches

---

### [EB-01] `feature/ebay-order-sync` — Fulfillment API Order Sync

**Summary:** Poll `getOrders`, match to inventory by SKU, mark cards Sold, write real fees to sale fields.

**Problem:** Sales are entered by hand. After every eBay sale, the user must manually look up the order, find the card, and type in sale price and fees. This is the highest-friction daily task.

**API:** eBay Fulfillment API v1 — `GET /sell/fulfillment/v1/order` (getOrders)

**Access status:** Requires new scope — `https://api.ebay.com/oauth/api_scope/sell.fulfillment.readonly`. Scope addition requires re-authorization by the user (new OAuth consent flow). No app approval gate beyond standard developer account.

**Dependencies:** EB-22 (background jobs — polling needs a timer/hosted service), EB-24 (unified sales model — to write consistent sale records)

**Estimated complexity:** Medium (1–2 weeks). Polling + deduplication logic is straightforward; edge cases (multi-item orders, orders created before FlipKit tracked SKUs) add time.

**New entities / schema changes:** `Card.EbayOrderId` (string, nullable) to track which order fulfilled each card. Idempotency key to prevent re-processing.

**Risk factors:**
- Cards listed before FlipKit won't have a `FK-{id}` SKU → unmatched orders need a "manual match" UI or are silently skipped
- Multi-item orders (one buyer, multiple cards) need per-line-item matching, not per-order
- Rate limits: getOrders allows reasonable polling (check eBay quotas per app)
- Scope re-auth interrupts the user on first run

**Acceptance criteria:**
- Orders fetched on configurable interval (default: 15 min)
- Matched cards automatically set to `Status = Sold` with eBay-reported sale price and fees
- Unmatched orders surface in a "needs attention" list in the UI
- Idempotent: re-running does not duplicate or overwrite already-matched sales
- User can trigger a manual sync in addition to the automatic poll

---

### [EB-02] `feature/ebay-shipping-tracking` — Fulfillment API Tracking Push

**Summary:** Push a tracking number from FlipKit to eBay after the user buys a label externally.

**Problem:** Sellers buy labels via Pirateship or USPS Click-N-Ship, then must separately log into eBay Seller Hub to enter tracking. Two trips to close one order.

**API:** eBay Fulfillment API v1 — `POST /sell/fulfillment/v1/order/{orderId}/shipping_fulfillment`

**Access status:** Requires scope `https://api.ebay.com/oauth/api_scope/sell.fulfillment` (write, not just readonly). Scope upgrade requires re-authorization.

**Dependencies:** EB-01 (order sync — need `EbayOrderId` stored on Card)

**Estimated complexity:** Small (1–3 days). One POST per shipment with carrier + tracking number.

**New entities / schema changes:** `Card.TrackingNumber` (string, nullable), `Card.ShippingCarrier` (string or enum, nullable).

**Risk factors:**
- Carrier name must match eBay's `shippingCarrierCode` enum — need a mapping table (USPS, UPS, FedEx, etc.)
- If the order is already marked shipped externally, the push may return a conflict error

**Acceptance criteria:**
- User can enter tracking number on a Sold card in FlipKit
- FlipKit pushes it to eBay and buyer receives notification
- Carrier code mapping covers USPS, UPS, FedEx, DHL at minimum
- Error surfaced gracefully if push fails

---

### [EB-03] `feature/ebay-promoted-listings` — Marketing API Promoted Listings Auto-Enrollment

**Summary:** Auto-enroll newly published listings in Promoted Listings Standard at a per-category ad rate.

**Problem:** Promoted Listings require manual setup per listing in Seller Hub. Users skip it and get fewer impressions.

**API:** eBay Marketing API v1 — `createAdCampaign`, `createAdByListingId`

**Access status:** Requires scope `https://api.ebay.com/oauth/api_scope/sell.marketing`. Standard developer account eligible; no extra program required.

**Dependencies:** None hard. Works best after EB-01 (so listing IDs are reliably stored), but can read existing `Card.EbayListingId`.

**Estimated complexity:** Medium (1–2 weeks). Campaign management logic + per-category rate configuration UI.

**New entities / schema changes:** `Card.EbayAdCampaignId`, `Card.EbayAdRate` (decimal, nullable); `AppSettings` entries for per-sport ad rate defaults.

**Risk factors:**
- Promoted Listings cost money (% of final sale) — misconfigured rates can erode margins silently
- Campaign setup has preconditions (listing must be active, account must be eligible)
- eBay Marketing API rate limits are more restrictive than Sell APIs

**Acceptance criteria:**
- Publishing a card optionally enrolls it in Promoted Listings at configured rate
- Per-sport/category default rates in Settings; per-card override available
- User can opt out globally or per card
- Campaign ID stored for auditing; can be viewed and cancelled from FlipKit

---

### [EB-04] `feature/ebay-markdown-discounts` — Marketing API Automated Markdowns

**Summary:** Apply a timed price markdown to listings that haven't sold after a configurable number of days.

**Problem:** Stale listings sit at full price indefinitely. User must manually find and discount them in Seller Hub.

**API:** eBay Marketing API v1 — `createItemPriceMarkdownPromotion`

**Access status:** Requires scope `sell.marketing`. Some accounts require a minimum seller level for markdown access — verify eligibility.

**Dependencies:** EB-05 (Analytics API helps identify stale listings, but time-since-listed from DB is sufficient without it). EB-22 (background jobs — schedule the nightly discount pass).

**Estimated complexity:** Medium (1–2 weeks). Promotion creation + validity date management; deactivation when card sells.

**New entities / schema changes:** `Card.ActiveMarkdownPromotionId` (string, nullable). Discount trigger threshold in `AppSettings`.

**Risk factors:**
- eBay markdown promotions have rules: minimum 5% off, minimum 3-day duration, some category exclusions
- Promotion must be deactivated when card sells, or a refund/correction triggers
- Creating too many promotions at once hits rate limits

**Acceptance criteria:**
- Nightly job marks listings stale after N days and creates a markdown promotion
- Markdown percentage and threshold configurable per Settings
- Promotions deactivated automatically when card is marked Sold
- User can override stale threshold per card

---

### [EB-05] `feature/ebay-listing-analytics` — Analytics API Traffic Reports

**Summary:** Pull per-listing views/impressions/CTR from eBay and surface them in the "stale listings" view.

**Problem:** Users have no visibility into whether a listing is getting eyeballs or is effectively invisible. Stale and low-impression listings need different interventions (reprice vs. better photos/title).

**API:** eBay Analytics API v1 — `getTrafficReport` (filtered by listing ID)

**Access status:** Requires scope `https://api.ebay.com/oauth/api_scope/sell.analytics.readonly`.

**Dependencies:** Listing IDs already stored from EbayPublishingService. EB-22 (background jobs — daily refresh).

**Estimated complexity:** Medium (1–2 weeks). API response parsing + storage; UI integration.

**New entities / schema changes:** JSON column or separate `ListingAnalytics` entity: `ImpressionCount`, `ClickCount`, `CTR`, `LastFetchedAt`. New entity preferred for queryability.

**Risk factors:**
- Analytics data lags 24–48h — cannot show real-time stats
- Response payload is large (all metrics for date range); need to scope query tightly by listing ID + date window
- Not all listing types return analytics data

**Acceptance criteria:**
- Stale listings view shows impressions, clicks, CTR per listing
- Data refreshed daily via background job
- Listings with zero impressions flagged distinctly from those with impressions but no sales
- Last-refreshed timestamp visible to user

---

### [EB-06] `feature/ebay-best-offer-automation` — Negotiation API Offer-to-Watchers

**Summary:** Auto-send Best Offer invitations to watchers on listings with no recent sale activity.

**Problem:** Watchers are warm leads; manually finding which listings have watchers and sending offers is tedious.

**API:** eBay Negotiation API v1 — `sendOfferToInterestedBuyers`

**Access status:** Requires scope `https://api.ebay.com/oauth/api_scope/sell.negotiation`. Standard account eligible.

**Dependencies:** EB-22 (background jobs). Listing must have Best Offer enabled — this needs to be wired into `EbayListingMapper`'s offer creation (currently unclear if it is).

**Estimated complexity:** Medium (1–2 weeks). Background job + offer rate rules; buyer deduplication (eBay limits one offer per buyer per listing).

**New entities / schema changes:** `Card.LastOfferSentAt` (DateTime?, nullable).

**Risk factors:**
- One offer per buyer per listing — eBay returns an error on duplicate sends; must track per-listing
- Offers expire; if buyer ignores, no follow-up allowed
- Listings must have Best Offer enabled at creation time — requires EbayListingMapper change

**Acceptance criteria:**
- Listings with watchers and no sale in N days trigger an offer at configured discount %
- Cooldown per listing (no repeated offers to same buyer)
- User can opt out globally or per listing
- Offer history visible on card detail

---

### [EB-07] `feature/ebay-inventory-reconciliation` — Feed API Active Inventory Sync

**Summary:** Nightly reconciliation of FlipKit DB against eBay active listings to detect drift (ended externally, sold outside FlipKit, etc.).

**API:** eBay Inventory API v1 — `getInventoryItems` (paginated); or Feed API if volume warrants.

**Access status:** `sell.inventory` scope already granted. Feed API may require separate program enrollment for high-volume feeds — check before building.

**Dependencies:** EB-22 (background jobs).

**Estimated complexity:** Large (2–4 weeks). Reconciliation logic requires handling multiple mismatch types (in DB but ended on eBay; on eBay but not in DB; price mismatch; quantity mismatch) and a resolution UI.

**New entities / schema changes:** `ReconciliationRun` log table (timestamp, mismatches found, resolved).

**Risk factors:**
- Feed API access may require an additional eBay program application
- Conflict resolution UX is the hard part — automated resolution of mismatches risks data loss
- Paginated inventory fetch can be slow at scale

**Acceptance criteria:**
- Nightly job fetches all active eBay listings and compares to FlipKit inventory
- Discrepancies (ended externally, sold externally, price drift) shown in a reconciliation review view
- User approves resolution per discrepancy (auto-resolve only for unambiguous cases like "ended + no FlipKit record")
- Reconciliation log kept for audit

---

### [EB-08] `feature/ebay-policies-ui` — Account API Business Policies Browser

**Summary:** Replace manual policy-ID-copy workflow in Settings with a dropdown that shows policy names.

**Problem:** Users currently paste policy IDs from eBay into Settings. There is no way to see policy names without switching to Seller Hub.

**API:** eBay Account API v1 — `getFulfillmentPolicies`, `getPaymentPolicies`, `getReturnPolicies` (already partially integrated in `EbayPublishingService`).

**Access status:** `sell.account.readonly` scope already granted. No additional access needed.

**Dependencies:** None — can ship standalone.

**Estimated complexity:** Small (1–3 days). The API calls already exist; this is UI work.

**New entities / schema changes:** None — policy IDs stored in `AppSettings` unchanged.

**Risk factors:** Minimal. API already partially called.

**Acceptance criteria:**
- Settings → eBay shows dropdowns of available policies by name for fulfillment, payment, and return
- Selecting a policy stores its ID (not name) in AppSettings
- Handles accounts with no policies gracefully (shows "none configured")

---

### [EB-09] `feature/ebay-taxonomy-aspects` — Taxonomy API Auto-Fill Required Item Aspects

**Summary:** Fetch required item aspects for the sports card category, validate before publish, pre-fill from AI scan data.

**Problem:** eBay listings can fail or be demoted for missing required aspects. Currently `EbayListingMapper` maps a fixed set; required aspects vary by sub-category and eBay updates them.

**API:** eBay Taxonomy API v1 — `getItemAspectsForCategory`

**Access status:** Taxonomy API uses application token (client credentials) — no user token needed. No special approval.

**Dependencies:** None hard. Depends on AI scan data being populated on `Card` (already standard flow).

**Estimated complexity:** Medium (1–2 weeks). Fetching + caching aspect requirements; mapping AI output to eBay-defined value lists; validation UI.

**New entities / schema changes:** `CategoryAspects` cache table (category ID, required aspects JSON, fetched date) to avoid re-fetching every publish.

**Risk factors:**
- eBay aspect value lists are specific (e.g., "Year" must be 4-digit string, not integer). Mismatch causes listing rejection.
- Taxonomy changes when eBay updates categories — cache must expire and refresh
- AI scan values may not match eBay's allowed value lists exactly (e.g., grader name format)

**Acceptance criteria:**
- Pre-publish validation checks all required aspects against Taxonomy API data
- Missing or invalid aspects shown to user before publish (not as a post-publish error)
- AI-derived values pre-filled where they match eBay's allowed value list
- Aspect cache refreshed weekly or on cache miss

---

### [EB-10] `feature/ebay-browse-comps` — Browse API Active Comp Pricing

**Summary:** Search active eBay listings from within FlipKit for competitive price reference.

**Note:** This branch (`feature/ebay-browse-api`) was previously built and **deliberately removed** (see `Docs/planning/roadmap.md` item 3, decision date 2026-05-05). The core objection was that active asking prices add noise, not signal — users set prices based on sold comps (Terapeak), not what competitors are asking.

**This item is explicitly deferred.** See Phase 4 decision log. Re-evaluate only if there's user demand for "how many of this card are listed and at what asking price" as a market-depth signal (distinct from pricing).

---

### [EB-11] `feature/ebay-finances-reconciliation` — Finances API Payout Reconciliation

**Summary:** Pull real transaction data from eBay Managed Payments to populate accurate fee breakdowns and net profit per card.

**Problem:** `FeesPaid` and `NetProfit` on `Card` are user-entered guesses. eBay's final value fee, shipping label cost, promoted listing fee, and processing fee vary by category, price, and account tier. Net profit is wrong today.

**API:** eBay Finances API v1 — `getTransactions`, `getPayouts`

**Access status:** Requires scope `https://api.ebay.com/oauth/api_scope/sell.finances`. Must be an eBay Managed Payments seller (standard for US sellers since 2021).

**Dependencies:** EB-01 (order sync — need `EbayOrderId` to match transactions to cards). EB-24 (unified sales model — fee breakdown fields need to exist).

**Estimated complexity:** Medium (1–2 weeks). Transaction types are varied; matching payout → transaction → order → item → SKU → card is a multi-hop join.

**New entities / schema changes:** Fee breakdown fields on `SaleRecord` (from EB-24): `FinalValueFee`, `PromotedListingFee`, `ShippingLabelCost`, `ProcessingFee`, `TotalFees`, `NetPayout`.

**Risk factors:**
- eBay transaction types include: SALE, CREDIT, REFUND, DISPUTE, FEE, ADJUSTMENT — each needs handling
- Transaction-to-order matching requires eBay order ID, which must be stored in EB-01 first
- Cost basis (what you paid for the card) is not tracked today — without it, gross margin is computable but true profit is not. **Open question: is `CostBasis` tracked on `Card`?** (Not seen in `Card.cs`.)

**Acceptance criteria:**
- Nightly job matches payouts to sold cards via order ID
- Fee breakdown (FVF, promoted listing fee, shipping label, processing fee) shown on sold card detail
- Net payout (after all fees) shown and compared to `SalePrice`
- Unmatched transactions flagged for review

---

### [EB-12] `feature/ebay-post-order` — Returns and Cancellation Handling

**Summary:** Handle eBay returns and cancellation requests so inventory state in FlipKit stays accurate.

**Problem:** When a buyer cancels or returns, the card in FlipKit stays `Sold` forever. The user has to manually edit the record.

**API:** eBay Post-Order API — `getCancellationRequests`; eBay Fulfillment API — order state changes; eBay Sell Account API — return policies.

**Access status:** Post-Order API may require separate scope — `https://api.ebay.com/oauth/api_scope/sell.fulfillment`. Verify scope coverage.

**Dependencies:** EB-01 (order sync — need order IDs). EB-22 (background jobs — poll for return/cancel events).

**Estimated complexity:** Medium (1–2 weeks). Multi-state flow (requested → approved → refunded → inventory restored) needs a small state machine.

**New entities / schema changes:** New `CardStatus` enum values: `ReturnRequested`, `Returned`, `CancellationRequested`, `Cancelled`. Possibly a `ReturnRecord` entity to log return reason and refund amount.

**Risk factors:**
- Return flow has many states and both sides can act — keeping FlipKit in sync requires polling or webhooks (webhooks preferred, but see EB-23)
- A "returned" card needs careful inventory handling: create a new card entry (the item may be damaged) vs. revert original card to `Priced`

**Acceptance criteria:**
- Cancellations flip card status to `Cancelled` and revert to `Priced` or `Listed` as appropriate
- Return requests surface as notifications; accepted returns restore card to inventory (new record if condition unknown)
- Refund amount recorded on the original sale record
- State transitions logged for audit

---

### Whatnot Branches

---

### [WN-13] Surprise Set Feature

**Note:** The user's brief described this as "a planned Surprise Set feature (separate branch already in design)." **Investigation found no design doc, no branch, and no code.** This item cannot be sequenced as a dependency without a spec. **Treat as a pre-roadmap gap: define and doc the feature before assigning a branch.** When defined, it will likely depend on WN-15 (show prep workflow) and WN-14 (sales import). Do not block other items on it.

---

### [WN-14] `feature/whatnot-sales-import` — Sold Show CSV Reconciliation Importer

**Summary:** Upload a Whatnot Live Stream Report CSV, fuzzy-match sold lots to inventory cards, and write sale records.

**Problem:** After a show, reconciling sold lots to inventory is done by hand. With 50+ lots per show, this takes 30+ minutes.

**Data source:** Whatnot "Live Stream Report" CSV, downloaded from seller dashboard. No API needed.

**Access status:** No API — file upload only.

**Dependencies:** EB-24 (unified sales model — consistent write path for Whatnot sales). No other hard dependencies.

**Estimated complexity:** Small–Medium (3–5 days). CSV parsing is easy; fuzzy matching on lot titles is the hard part. `FuzzyMatcher` already exists in `FlipKit.Core/Helpers/`.

**New entities / schema changes:** `Card.WhatnotOrderId` (string, nullable) to prevent duplicate imports from the same show.

**Risk factors:**
- Lot titles set during a show may not match card names exactly ("Mahomes RC" vs "2017 Panini Prizm Patrick Mahomes Rookie")
- Unmatched lots need a review UI — can't silently skip them
- Duplicate import detection requires the Whatnot order ID as a deduplication key (check if Live Stream Report includes one)

**Acceptance criteria:**
- User uploads show report CSV on Desktop or Web
- FuzzyMatcher proposes matches; user confirms or overrides
- Unmatched lots shown for manual assignment
- Matched cards updated to `Status = Sold` with Whatnot sale price and date
- Re-importing the same report does not create duplicate sales

---

### [WN-15] `feature/whatnot-show-prep` — Show Prep Workflow

**Summary:** Multi-card selection UI → assign to auction/BIN/giveaway buckets with rule-based starting prices → export grouped Whatnot CSV.

**Problem:** Manually selecting cards for a show, deciding types and starting prices, and building the upload CSV takes significant time each show.

**Data source:** Extends existing `WhatnotExporter`. No new API.

**Access status:** N/A — CSV only.

**Dependencies:** None hard. Works with existing WhatnotExporter.

**Estimated complexity:** Small–Medium (3–5 days). UI for selection + bucketing; extend CSV writer with Type and Price overrides.

**New entities / schema changes:** Possibly `Card.PlannedShowType` and `Card.PlannedStartingPrice` (transient, cleared after export). Or keep entirely in-memory and don't persist.

**Risk factors:** Minimal — extends existing working code path.

**Acceptance criteria:**
- User can multi-select cards from inventory and assign to Auction/BIN/Giveaway
- Starting price suggested by rule (e.g., 50% of `SalePrice` for auctions, `Price` for BIN)
- Rule thresholds configurable in Settings
- Export produces valid Whatnot CSV grouped by bucket
- Cards marked "In Show Prep" status during planning, reverted if show is cancelled

---

### [WN-16] `feature/whatnot-sold-comps-scraping` — Whatnot Scraping for Sold Comps

**⚠️ UNOFFICIAL / BRITTLE — flag prominently in any future discussion.**

**Summary:** Scrape Whatnot show history to find sold prices for comparable cards.

**Data source:** Unofficial scraping of Whatnot's website — no public API for sold show data.

**Access status:** Unofficial. Whatnot's Terms of Service likely prohibit automated scraping. This would require Playwright or similar, and is subject to login walls, DOM changes, rate limiting, and blocking at any time.

**Dependencies:** None technical, but see risk.

**Estimated complexity:** Medium, but with unbounded maintenance cost.

**Risk factors:**
- **ToS risk** — Whatnot could consider this a violation; accounts could be terminated
- **Fragility** — DOM structure changes break scrapers without notice
- **Auth** — show history may require login; session management adds complexity
- **Maintenance burden** — every Whatnot site update potentially breaks the scraper

**Recommendation:** Do not build. If sold comp data from Whatnot is needed, wait for Whatnot Seller API (WN-17+) to expose it officially, or document a manual lookup workflow. See Phase 4 decision log.

---

### [WN-17] `feature/whatnot-api-listing` — Whatnot Seller API: productCreate + listingPublish *(GATED)*

**Summary:** Create Whatnot product listings directly from FlipKit without CSV upload.

**API:** Whatnot GraphQL Seller API — `productCreate`, `listingPublish` mutations

**Access status:** **GATED — Developer Preview (private access required).** Apply at whatnot.com/developer. No guaranteed timeline for general availability. All WN-1x items share this gate.

**Dependencies:** EB-25 (secret storage — new Whatnot API tokens). EB-22 (background jobs for async operations).

**Estimated complexity:** Medium–Large (1–3 weeks once access granted). GraphQL client setup, image URL requirements, product taxonomy mapping.

**New entities / schema changes:** `Card.WhatnotListingId` (string, nullable).

**Risk factors:** Developer Preview = breaking changes expected. Image hosting requirements unknown (may require public HTTPS URLs — ImgBB integration may cover this).

**Acceptance criteria:**
- Card published to Whatnot directly from FlipKit
- Listing appears in seller account with correct title, price, images
- `WhatnotListingId` stored for future reference and WN-18/19

---

### [WN-18] `feature/whatnot-api-orders` — Whatnot Seller API: Orders + addTrackingCode *(GATED)*

**Summary:** Fetch Whatnot orders and push tracking numbers via Seller API.

**API:** Whatnot GraphQL Seller API — `orders` query + `addTrackingCode` mutation

**Access status:** GATED — Developer Preview.

**Dependencies:** WN-17 (listing integration). EB-24 (unified sales model — write Whatnot order data consistently).

**Estimated complexity:** Small (1–3 days once access + WN-17 in place).

**Risk factors:** Developer Preview stability. See WN-17.

**Acceptance criteria:** Whatnot orders sync to FlipKit; tracking push works from FlipKit sold-card view.

---

### [WN-19] `feature/whatnot-api-show-planner` — Whatnot Seller API: listingAssignToLivestream *(GATED)*

**Summary:** Drag-and-drop show queue planning in FlipKit, synced to Whatnot via API.

**API:** Whatnot GraphQL Seller API — `listingAssignToLivestream` mutation

**Access status:** GATED — Developer Preview.

**Dependencies:** WN-17 (listing integration). WN-15 (show prep workflow — the UI layer for this exists here).

**Estimated complexity:** Medium (1–2 weeks once access granted) — ordering/drag UI is the complexity.

**Acceptance criteria:** Show queue in FlipKit maps 1:1 to Whatnot livestream queue. Reordering in FlipKit syncs to Whatnot.

---

### [WN-20] `feature/whatnot-api-webhooks` — Whatnot Seller API: Real-Time Sold Events *(GATED)*

**Summary:** Receive Whatnot sold events via webhook to update inventory in real time during a show.

**API:** Whatnot GraphQL Seller API — webhook subscriptions

**Access status:** GATED — Developer Preview.

**Dependencies:** EB-23 (webhook receiver infrastructure). WN-17 (listing integration).

**Estimated complexity:** Small–Medium (1 week once access + EB-23 in place).

**Risk factors:** Webhook delivery guarantees in Developer Preview unknown. Must handle missed events gracefully (fallback to polling).

**Acceptance criteria:** Card marked Sold within seconds of Whatnot sale event; no manual reconciliation needed for shows.

---

### [WN-21] `feature/whatnot-api-bulk-ops` — Whatnot Seller API: bulkOperationRunMutation *(GATED)*

**Summary:** Replace current CSV bulk upload path with the native Whatnot bulk operation API.

**API:** Whatnot GraphQL Seller API — `bulkOperationRunMutation`

**Access status:** GATED — Developer Preview.

**Dependencies:** WN-17 (listing integration).

**Estimated complexity:** Medium (1–2 weeks once access). Async pattern: submit → poll for completion → fetch results.

**Risk factors:** Async bulk operations require polling job; result format may differ from synchronous listing creation. Developer Preview stability.

**Acceptance criteria:** Large show preps (50+ cards) submitted in one batch; results reconciled with FlipKit DB; replaces manual CSV upload.

---

### Cross-Cutting Branches

---

### [CC-22] `feature/background-jobs` — Background Job Infrastructure

**Summary:** Add a lightweight timer/hosted-service layer to support polling-based integrations (order sync, reconciliation, analytics refresh).

**Problem:** All current operations are synchronous and user-initiated. Polling eBay APIs on a schedule requires at minimum a hosted background service.

**Approach:** `IHostedService` + `System.Threading.Timer` in `FlipKit.Desktop` (runs while Desktop is open) and `FlipKit.Web` (runs while Web server is active). No Hangfire — adds a heavy dependency for modest needs. Jobs configured with interval settings in `AppSettings`.

**Estimated complexity:** Small–Medium (3–5 days). `IHostedService` registration, job dispatch, graceful cancellation on shutdown.

**New entities / schema changes:** `JobRun` log table optional but useful: job name, started, completed, outcome.

**Risk factors:**
- Desktop and Web may both be running simultaneously — avoid duplicate polling. Solve with a DB-based "last run" timestamp check (don't poll if another instance ran within the interval).
- Desktop is not always running — critical polling (order sync) should also work via Web server if Desktop is off.

**Acceptance criteria:**
- Configurable polling intervals per job type (order sync, analytics, reconciliation)
- Jobs fire reliably while the app is running
- Last-run time and outcome visible in Settings
- Graceful shutdown — no orphaned polls on app exit

---

### [CC-23] `feature/webhook-receiver` — Webhook Receiver Infrastructure

**Summary:** Add signed-request webhook endpoints to `FlipKit.Web` and `FlipKit.Api` for eBay platform notifications and future Whatnot webhooks.

**Problem:** Polling is inefficient for event-driven data (order sold, return opened, tracking needed). Webhooks require a public HTTPS endpoint — the Web server running via Tailscale satisfies this.

**Approach:** Webhook endpoints in `FlipKit.Web/Controllers/WebhookController.cs`. Signature verification per platform (eBay uses challenge/response + HMAC; Whatnot to be determined). Event dispatch to appropriate handler services.

**Note:** eBay requires a public HTTPS endpoint registration for Marketplace Account Deletion notifications (GDPR compliance). This is a **compliance requirement**, not just a feature — any eBay developer account must handle it.

**Estimated complexity:** Medium (1–2 weeks). Endpoint scaffolding is fast; HMAC signature verification and event routing add time.

**New entities / schema changes:** `WebhookEvent` log table: platform, event type, received at, payload hash, processing status.

**Risk factors:**
- Local dev testing requires a public URL — ngrok or localtunnel needed for development
- Whatnot webhook format is unknown until Developer Preview access granted
- eBay Account Deletion endpoint is a compliance requirement with a hard deadline from eBay

**Acceptance criteria:**
- eBay Marketplace Account Deletion endpoint live and passing eBay's challenge/response verification
- Incoming webhooks logged with signature status before processing
- Failed events retried or queued for manual review
- Endpoints work via Tailscale HTTPS (public IP not required if Tailscale covers it)

---

### [CC-24] `feature/unified-sales-model` — Unified Sales Tracking Model

**Summary:** Extract sale data from `Card` entity into a separate `SaleRecord` entity that supports eBay-attributed sales, Whatnot per-listing sales, and Whatnot Surprise Set allocated sales with consistent fee breakdowns.

**Problem:** Sales fields on `Card` are flat and platform-agnostic. They can't represent a Whatnot show where one order contains multiple lots, or a Surprise Set where revenue must be allocated across mystery cards, or the eBay fee breakdown needed for EB-11.

**Approach:** New `SaleRecord` entity linked to `Card` (1:1 for single-item sales, 1:many for Surprise Sets or bundle sales). `Card` keeps `Status = Sold` and `SaleDate` for quick filtering; detailed sale data moves to `SaleRecord`.

**Estimated complexity:** Medium (1–2 weeks). EF Core migration + data migration for existing `Sold` cards + update all queries that read sale fields.

**New entities / schema changes:**

```
SaleRecord:
  Id, CardId (FK), Platform (eBay|Whatnot|Manual), SalePrice, SaleDate,
  OrderId (platform order ID), FinalValueFee, PromotedListingFee,
  ShippingLabelCost, ProcessingFee, TotalFees, NetPayout,
  CostBasis (if tracked), Notes
```

**Risk factors:**
- Breaking migration — all existing queries against `Card.SalePrice` etc. must be updated
- Existing sold card data must be migrated into `SaleRecord` without loss
- Reports (`/api/reports/sold`, financial views) need rewriting

**Acceptance criteria:**
- Existing `Sold` cards migrated into `SaleRecord` with data intact
- All reports and exports continue to work after migration
- eBay and Whatnot sales stored with platform-appropriate fee fields
- Net profit computed from `SalePrice - TotalFees - CostBasis` where data is available

---

### [CC-25] `feature/secret-storage-hardening` — Token/Secret Storage Hardening

**Summary:** Audit current DPAPI approach and address cross-platform gaps before adding Whatnot + additional eBay scopes.

**Problem:** Current approach works well on Windows. On Linux (Web server on a headless box), key storage relies on file permissions only. With more platforms and more tokens (eBay refresh token, Whatnot API token, OpenRouter key, ImgBB key, CardSight key), the config.json grows and any plaintext leak of the file is serious.

**Current state:** `DataProtectionSecretEncryption` with ASP.NET Core Data Protection — good on Windows (DPAPI), acceptable on Linux with file permissions, no cross-machine portability.

**Estimated complexity:** Small–Medium (3–5 days) — mostly audit + documentation; small implementation changes.

**Risk factors:**
- Migration of existing encrypted values if changing key ring location
- Cross-platform key ring differences (Desktop runs on Windows, Web may run on Linux)
- Token revocation is not possible today — if a token leaks, there's no per-token kill switch

**Acceptance criteria:**
- Audit report: current key ring location on Windows and Linux documented
- Tokens survive app reinstall on Windows (key ring persisted, not ephemeral)
- Individual tokens can be cleared from Settings without affecting others
- Plaintext values never written to disk unprotected

---

### [CC-26] `feature/fee-calculation-engine` — Per-Platform Fee Calculation Engine

**Summary:** Compute net profit automatically from platform-specific fee rules rather than user entry.

**Problem:** `NetProfit` is either blank or manually entered. eBay final value fees vary by category (10.35%–15% for sports cards), Whatnot charges 8% + payment processing (~3%), and shipping costs vary. Without a fee engine, all P&L data is unreliable.

**Approach:** `IFeeCalculator` service in `FlipKit.Core` with implementations per platform. eBay rates loaded from a configuration table (not hardcoded — rates change). Applied when a sale record is written.

**Dependencies:** CC-24 (unified sales model — fee breakdown fields must exist).

**Estimated complexity:** Small–Medium (3–5 days for rule engine; up to Medium if pulling live rate schedules).

**New entities / schema changes:** `PlatformFeeSchedule` config table (platform, category, fee percentage, effective date) — avoids hardcoding.

**Risk factors:**
- eBay fee schedules change and vary by category, seller tier, and promotion participation
- Hardcoded rates drift over time — must be user-updatable or fetched from eBay Account API
- Without `CostBasis` on `Card`, gross margin is computable but true net profit is not

**Acceptance criteria:**
- New sale records auto-populated with estimated fees on creation
- Fee schedule configurable in Settings per platform
- Sold inventory report shows gross margin and estimated net
- User can override calculated fees on any individual sale

---

## Phase 3: Sequencing

### Constraints Applied

1. CC-22 (background jobs) and CC-24 (unified sales model) are prerequisites for most integrations — they go first.
2. Whatnot Seller API items (WN-17 through WN-21) are gated on Developer Preview access — they form a parallel track activated only when access is granted.
3. Revenue-closing features (order sync, sales import) come before optimization features (promoted listings, analytics).
4. CC-23 (webhook receiver) is required for eBay compliance (account deletion), so it lands early — but full webhook-driven integrations can come later.
5. EB-10 (Browse API comps) is deferred — documented decision, do not schedule.
6. WN-16 (scraping) is deferred — ToS risk, do not schedule.

---

### M0: Foundations *(~2.5 weeks)*

**Branches:** CC-22, CC-24, CC-25, CC-23 (partial — compliance endpoint only)

**Total estimated complexity:** ~2.5 weeks

**What changes for the user:** Nothing visible yet — but order sync and reconciliation features become buildable, and eBay's Account Deletion compliance requirement is met.

**Do not do yet:** Any API polling (no jobs exist to run it). Fee engine (no sale model to attach it to). Any eBay Sell API work beyond what already exists.

| Branch | Why now |
|---|---|
| CC-24 unified sales model | All sale-writing features need consistent schema |
| CC-22 background jobs | All polling features need a scheduler |
| CC-23 webhook receiver | eBay Account Deletion compliance — must have a live endpoint |
| CC-25 secret storage audit | Before adding more tokens, understand what's there |

---

### M1: Close the Sales Loop *(~3 weeks)*

**Branches:** EB-01, WN-14, CC-26

**Total estimated complexity:** ~3 weeks

**What changes for the user:** After a show or eBay sale, FlipKit knows what sold and at what net profit — without manual entry. This is the highest user-visible value per week of work on the roadmap.

**Do not do yet:** Fulfillment API write access (tracking push — needs M1 complete first for order IDs). Finances API (needs order sync running first). Any Whatnot API items (gated).

| Branch | Why here |
|---|---|
| EB-01 eBay order sync | Closes the eBay sales loop; highest friction today |
| WN-14 Whatnot sales import | Closes the Whatnot loop via CSV; no API gate |
| CC-26 fee calculation engine | Makes net profit numbers in M0+M1 actually accurate |

---

### M2: Fulfillment & Show Prep *(~2 weeks)*

**Branches:** EB-02, WN-15, EB-12

**Total estimated complexity:** ~2 weeks

**What changes for the user:** Tracking numbers pushed to eBay from FlipKit; show prep CSV workflow faster and more structured; returns/cancellations handled without manual card status edits.

**Do not do yet:** Marketing API features (need to understand sales data trends first). eBay Analytics (low value without volume). Taxonomy aspects (useful but not urgent).

| Branch | Why here |
|---|---|
| EB-02 shipping tracking | Short work; M1 provides the order IDs needed |
| WN-15 show prep workflow | Short work; extends existing CSV path |
| EB-12 post-order | Prevents stale Sold status after returns/cancellations |

---

### M3: Listing Quality *(~2 weeks)*

**Branches:** EB-08, EB-09

**Total estimated complexity:** ~1.5 weeks

**What changes for the user:** No more copying policy IDs by hand; listings have all required aspects before publishing (fewer eBay rejections).

**Do not do yet:** Marketing API (want clean sales data in for a few weeks before automating discounts). Feed API reconciliation (needs volume of order sync data first).

| Branch | Why here |
|---|---|
| EB-08 policies UI | Small; unblocks cleaner listing experience; scope already granted |
| EB-09 taxonomy aspects | Prevents listing rejections; low risk |

---

### M4: Financial Clarity *(~3 weeks)*

**Branches:** EB-11, EB-07

**Total estimated complexity:** ~3 weeks

**What changes for the user:** Accurate payout reconciliation — FlipKit tells you exactly what eBay paid you per card after all fees. Inventory stays in sync with eBay even when things happen outside FlipKit.

**Do not do yet:** Promoted listings (still no baseline performance data). Negotiation API (want comps data first).

| Branch | Why here |
|---|---|
| EB-11 Finances API | Requires M1 order IDs; now has enough data to be useful |
| EB-07 inventory reconciliation | After several weeks of order sync, drift will have accumulated — now worth reconciling |

---

### M5: Optimization Layer *(~5–6 weeks)*

**Branches:** EB-05, EB-06, EB-04, EB-03

**Total estimated complexity:** ~5–6 weeks (parallel work possible on EB-05 + EB-06)

**What changes for the user:** Data-driven pricing decisions; automated discount and offer tools; promoted listings enrollment without leaving FlipKit.

**Do not do yet:** WN-16 scraping (deferred indefinitely). EB-10 Browse API (deferred — see decision log).

| Branch | Why here |
|---|---|
| EB-05 listing analytics | Provides the data that makes EB-04 and EB-06 decisions intelligent |
| EB-06 best offer automation | Needs listing data + background jobs (both exist by now) |
| EB-04 markdown discounts | Needs analytics to identify stale listings well |
| EB-03 promoted listings | Last because it costs money — want clean sales data before automating |

---

### M6: Whatnot Native *(activate when Developer Preview access granted)*

**Branches:** WN-17, WN-18, WN-19, WN-20, WN-21

**Total estimated complexity:** ~5–7 weeks once access granted

**What changes for the user:** No CSV uploads to Whatnot — listings, show planner, order sync, and tracking all work from FlipKit directly. Real-time sold events during shows.

**Do not do yet:** WN-21 bulk ops — build WN-17 through WN-20 first and understand the API before optimizing with bulk operations.

**Activation condition:** Apply for Whatnot Developer Preview at whatnot.com/developer. Start M6 planning when access is confirmed — Developer Preview docs may change the estimates significantly.

---

### Sequencing Summary

```
M0 (Foundations)
  └─ M1 (Close the Sales Loop)
       └─ M2 (Fulfillment & Show Prep)
            └─ M3 (Listing Quality)         ← short; can overlap with M2 tail
                 └─ M4 (Financial Clarity)
                      └─ M5 (Optimization Layer)

M6 (Whatnot Native) ── parallel track, activates on Developer Preview access
  └─ does not block M0–M5
```

---

## Phase 4: Risk and Decision Log

### Open Questions (Need Your Input)

1. **Cost basis tracking** — `Card` has no `CostBasis` or `PurchasePrice` field visible in the investigation. True net profit (sale price minus what you paid for the card) is impossible without it. Is this tracked somewhere else, or intentionally omitted? If not tracked, CC-24 should add it.

2. **Surprise Set design** — The user brief described this as "a planned feature with a branch already in design." Investigation found nothing. Does this exist in an external doc, a private branch, or a conversation? Cannot sequence it as a dependency without a spec.

3. **Multi-user / multi-machine scenario** — Desktop and Web both read from the same SQLite DB. With background jobs in both, concurrent polling from two machines would be a problem. Is there a dedicated server machine, or is it typically one Desktop + one Web on the same machine?

4. **eBay seller tier** — Some features (markdown promotions, Promoted Listings) have seller eligibility requirements. Is the account Top Rated, Above Standard, or other? This affects M5 scope.

5. **Whatnot Developer Preview** — Has an application been submitted? Timelines vary widely. M6 is the highest-value Whatnot unlock and should be applied for now even if M0–M5 are the near-term focus.

6. **Image hosting for Whatnot API** — WN-17 will likely require public HTTPS image URLs for product photos. ImgBB is already integrated for eBay. Does Whatnot's API accept ImgBB URLs, or does it require their own CDN upload endpoint? Unknown until Developer Preview docs are available.

---

### Known Risks

1. **eBay API scope re-authorization** — Each new scope (sell.fulfillment, sell.marketing, sell.finances) requires the user to go through OAuth consent again. This is a UX interruption; consider batching scope additions across milestones to minimize how often this happens.

2. **eBay fee schedule drift** — CC-26 fee engine will have hardcoded or table-driven rates that go stale. eBay adjusts sports card FVF rates periodically. Build a "fee schedule last updated" warning into the UI.

3. **Whatnot policy churn** — Whatnot is a fast-moving platform. API contracts in Developer Preview are explicitly unstable. WN-17 through WN-21 carry non-trivial maintenance risk if Whatnot changes the GraphQL schema between API versions.

4. **eBay Platform Notifications compliance** — The Account Deletion notification endpoint (CC-23) is a hard requirement from eBay for any live app. If this is not in place, the eBay developer account is at risk. **Prioritize the compliance-only slice of CC-23 in M0.**

5. **SQLite concurrent write contention** — WAL mode handles concurrent reads well, but multiple background jobs from Desktop + Web writing simultaneously could cause lock contention. Profile under concurrent load before deploying M1.

6. **Marketplace Insights API** — Item EB-10 noted that sold-price comps via eBay API require Marketplace Insights, which is restricted to Terapeak-tier partners ("high-end developers"). This is confirmed unavailable via standard developer registration. The `Docs/features/ebay-integration.md` and the roadmap both document this. Do not apply speculatively.

---

### Explicitly Deferred Items (Documented Decisions)

| Item | Decision | Reasoning |
|---|---|---|
| **EB-10 Browse API comps** | ❌ Do not build | Already built and deliberately removed (2026-05-05). Active asking prices add noise, not signal — users price from sold comps (Terapeak/deeplinks). Revisit only if demand for market-depth data (listing volume, asking range) is distinct from pricing. |
| **WN-16 Whatnot scraping** | ❌ Do not build | ToS risk, fragility, unbounded maintenance cost. Wait for Whatnot Seller API to expose sold data officially. |
| **WN-13 Surprise Set** | 🟡 Needs spec | No design doc or branch found. Cannot plan without a spec. Define it first, then it slots into M2–M3 based on complexity. |
| **EB-10 Marketplace Insights** | ❌ Not accessible | Requires eBay partner-tier approval not available to standard developers. Confirmed in `Docs/features/ebay-integration.md`. |
| **Price alerts / notifications** | ❌ Deferred | Depends on automated pricing data, which is deferred (Browse API decision above). No foundation to build on. |
| **COMC exporter** | ⬇️ Downgrade or drop | `Docs/planning/roadmap.md` item 5 flags this as "consider downgrading or dropping" — no demand signal, wiring is half-present and misleading. Not included in this roadmap. |

---

*Last updated: 2026-05-06*  
*Next review: When M0 is complete or Developer Preview access status changes.*
