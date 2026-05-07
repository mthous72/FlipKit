# Surprise Set Feature — Final Design

**Date:** 2026-05-06  
**Branch:** `claude/investigate-surprise-set-LsY0U`  
**Status:** Phase 2 complete — awaiting implementation approval

## Design Decisions (user-confirmed)

| Question | Decision |
|---|---|
| Lifecycle states | Full: `Draft → Exported → Live → Completed → Cancelled` |
| Show entity | Free-text `ShowName` string on `SurpriseSet`; promote to FK later if needed |
| Revenue allocation methods | Equal, CostWeighted, Manual (no ListingPriceWeighted) |
| Delete behavior for abandoned Draft sets | Hard delete — set AND all its cards permanently deleted |
| Default scan depth for Surprise Sets | Standard (full card details, same as individual scan) |
| Web UI parity | Full — bulk scan on Web too (not manage-only) |

---

## 1. Data Model

### 1.1 New entity: `SurpriseSet`

**File:** `FlipKit.Core/Models/SurpriseSet.cs`

```csharp
public class SurpriseSet
{
    public int Id { get; set; }

    // Identity
    public string Name { get; set; } = string.Empty;
    public string? ShowName { get; set; }
    public string? Notes { get; set; }

    // Lifecycle
    public SurpriseSetState State { get; set; } = SurpriseSetState.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExportedAt { get; set; }
    public DateTime? LiveAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Shared listing fields — stamped onto every CSV row at export.
    // Whatnot's consistency rule is enforced by construction, not post-hoc validation.
    public string Title { get; set; } = string.Empty;
    public string SharedListingType { get; set; } = "Buy it Now";
    public decimal SpotPrice { get; set; }
    public string SharedCondition { get; set; } = string.Empty;
    public string SharedShippingProfile { get; set; } = string.Empty;
    public string SharedWhatnotCategory { get; set; } = "Sports Trading Cards";
    public string? SharedWhatnotSubcategory { get; set; }
    public bool Offerable { get; set; } = false;
    public string? SharedImageUrl1 { get; set; }
    public string? SharedImageUrl2 { get; set; }
    public string? SharedImageUrl3 { get; set; }
    public string? SharedImageUrl4 { get; set; }
    public string? SharedImageUrl5 { get; set; }
    public string? SharedImageUrl6 { get; set; }
    public string? SharedImageUrl7 { get; set; }
    public string? SharedImageUrl8 { get; set; }

    // Economics
    public RevenueAllocationMethod AllocationMethod { get; set; } = RevenueAllocationMethod.EqualSplit;
    public decimal? LotCostBasis { get; set; }
    public int? SpotsSold { get; set; }
    public decimal? GrossRevenue { get; set; }
    public decimal? TotalFees { get; set; }
    public decimal? TotalShipping { get; set; }

    // Navigation
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}
```

### 1.2 New enums

**File:** `FlipKit.Core/Models/Enums/SurpriseSetState.cs`

```csharp
public enum SurpriseSetState { Draft, Exported, Live, Completed, Cancelled }
```

State machine (forward-only; Cancelled reachable from any state except Completed):
```
Draft → Exported → Live → Completed
  ↓         ↓       ↓
Cancelled  Cancelled  Cancelled
```

**File:** `FlipKit.Core/Models/Enums/RevenueAllocationMethod.cs`

```csharp
public enum RevenueAllocationMethod { EqualSplit, CostWeighted, Manual }
```

### 1.3 `CardStatus` additions

Extend `FlipKit.Core/Models/Enums/CardStatus.cs` (additive — stored as string, no integer reordering risk):

```csharp
public enum CardStatus
{
    Draft,
    Priced,
    Ready,
    Listed,
    Sold,
    ReservedForSet,  // card is locked into a SurpriseSet
    SoldInSet,       // card was sold as part of a completed SurpriseSet
}
```

### 1.4 `Card` entity additions

Add to `FlipKit.Core/Models/Card.cs`:

```csharp
// === SURPRISE SET ===
public int? SurpriseSetId { get; set; }
public int? SurpriseSetSlot { get; set; }  // 1-based position in checklist
public SurpriseSet? SurpriseSet { get; set; }
```

### 1.5 Schema changes — `SchemaUpdater` pattern

**File:** `FlipKit.Core/Data/SchemaUpdater.cs`

Add two new public async methods:

```csharp
public async Task EnsureSurpriseSetTablesAsync()
{
    await _db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS surprise_sets (
            id                        INTEGER PRIMARY KEY AUTOINCREMENT,
            name                      TEXT    NOT NULL DEFAULT '',
            show_name                 TEXT,
            notes                     TEXT,
            state                     TEXT    NOT NULL DEFAULT 'Draft',
            created_at                TEXT    NOT NULL,
            updated_at                TEXT    NOT NULL,
            exported_at               TEXT,
            live_at                   TEXT,
            completed_at              TEXT,
            cancelled_at              TEXT,
            title                     TEXT    NOT NULL DEFAULT '',
            shared_listing_type       TEXT    NOT NULL DEFAULT 'Buy it Now',
            spot_price                REAL    NOT NULL DEFAULT 0,
            shared_condition          TEXT    NOT NULL DEFAULT '',
            shared_shipping_profile   TEXT    NOT NULL DEFAULT '',
            shared_whatnot_category   TEXT    NOT NULL DEFAULT 'Sports Trading Cards',
            shared_whatnot_subcategory TEXT,
            offerable                 INTEGER NOT NULL DEFAULT 0,
            shared_image_url1         TEXT,
            shared_image_url2         TEXT,
            shared_image_url3         TEXT,
            shared_image_url4         TEXT,
            shared_image_url5         TEXT,
            shared_image_url6         TEXT,
            shared_image_url7         TEXT,
            shared_image_url8         TEXT,
            allocation_method         TEXT    NOT NULL DEFAULT 'EqualSplit',
            lot_cost_basis            REAL,
            spots_sold                INTEGER,
            gross_revenue             REAL,
            total_fees                REAL,
            total_shipping            REAL
        )
        """);
}

public async Task EnsureSurpriseSetCardColumnsAsync()
{
    await TryAddColumnAsync("cards", "surprise_set_id",   "INTEGER");
    await TryAddColumnAsync("cards", "surprise_set_slot", "INTEGER");
}
```

`TryAddColumnAsync` already handles the "duplicate column" error silently (pattern used for other migrations in `SchemaUpdater.cs`).

Wire both from `EnsureVerificationTablesAsync` (the last method in the existing bootstrap chain):

```csharp
await EnsureSurpriseSetTablesAsync();
await EnsureSurpriseSetCardColumnsAsync();
```

### 1.6 `FlipKitDbContext` additions

In `OnModelCreating`:

```csharp
// SurpriseSet
modelBuilder.Entity<SurpriseSet>(e =>
{
    e.ToTable("surprise_sets");
    e.Property(x => x.State).HasConversion<string>();
    e.Property(x => x.AllocationMethod).HasConversion<string>();
    e.Property(x => x.SpotPrice).HasColumnType("decimal(18,2)");
    e.Property(x => x.LotCostBasis).HasColumnType("decimal(18,2)");
    e.Property(x => x.GrossRevenue).HasColumnType("decimal(18,2)");
    e.Property(x => x.TotalFees).HasColumnType("decimal(18,2)");
    e.Property(x => x.TotalShipping).HasColumnType("decimal(18,2)");
});

// Card → SurpriseSet relationship
modelBuilder.Entity<Card>()
    .HasOne(c => c.SurpriseSet)
    .WithMany(s => s.Cards)
    .HasForeignKey(c => c.SurpriseSetId)
    .OnDelete(DeleteBehavior.Restrict); // hard delete handled explicitly in service layer
```

`DeleteBehavior.Restrict` is intentional — hard deletion of the set cascades through the service layer (not the DB), so we can confirm card count and log the action before deletion.

### 1.7 `CardStatusPredicates` helper

**File:** `FlipKit.Core/Helpers/CardStatusPredicates.cs` (new)

Centralises the "is this card available for individual listing" logic, which is currently duplicated across 5 call sites.

```csharp
public static class CardStatusPredicates
{
    /// Cards that appear in the inventory export grid and are eligible for CSV listing.
    public static readonly CardStatus[] IndividualListingStatuses =
        [CardStatus.Draft, CardStatus.Priced, CardStatus.Ready];

    /// Cards that count as "sold" for revenue report totals.
    public static readonly CardStatus[] SoldStatuses =
        [CardStatus.Sold, CardStatus.SoldInSet];

    public static bool IsAvailableForIndividualListing(Card card) =>
        Array.IndexOf(IndividualListingStatuses, card.Status) >= 0;

    public static bool IsSold(Card card) =>
        Array.IndexOf(SoldStatuses, card.Status) >= 0;
}
```

**Update the 5 existing call sites** to use `CardStatusPredicates`:

| File | Line (approx) | Change |
|---|---|---|
| `ExportViewModel.cs:157-161` | Status filter for export grid | Use `IndividualListingStatuses` |
| `CardRepository.cs:99-108` | `GetStaleCardsAsync` | Use `IndividualListingStatuses` |
| `InventoryViewModel.cs:583-585` | Active inventory filter | Use `IndividualListingStatuses` |
| `ReportsController.cs:121-127` | Sold totals query | Use `SoldStatuses` |
| `PricingController.cs:37` | Pricing query | Use `IndividualListingStatuses` |

### 1.8 Repository

**Interface:** `FlipKit.Core/Services/Interfaces/ISurpriseSetRepository.cs`

```csharp
public interface ISurpriseSetRepository
{
    Task<SurpriseSet?> GetByIdAsync(int id);
    Task<SurpriseSet?> GetByIdWithCardsAsync(int id);
    Task<List<SurpriseSet>> GetAllAsync();
    Task<List<SurpriseSet>> GetDraftSetsAsync();
    Task<int> InsertAsync(SurpriseSet set);
    Task UpdateAsync(SurpriseSet set);
    Task DeleteAsync(int id); // hard-deletes set AND all its ReservedForSet cards
    Task AddCardAsync(int setId, Card card);
    Task RemoveCardAsync(int setId, int cardId); // detaches card, renumbers slots
    Task<bool> IsLockedAsync(int id); // true if State >= Exported
}
```

**Implementation:** `FlipKit.Core/Services/Implementations/SurpriseSetRepository.cs`

Key implementation notes:
- `DeleteAsync`: within a transaction, delete all `cards WHERE surprise_set_id = @id`, then delete the `surprise_sets` row.
- `RemoveCardAsync`: null out `SurpriseSetId`/`SurpriseSetSlot`, re-evaluate status via `CardStatusEvaluator.Evaluate`, renumber remaining card slots in ascending order.
- `IsLockedAsync`: returns true when `State` is `Exported`, `Live`, `Completed`, or `Cancelled`. Used to gate card add/remove and metadata edits.
- `AddCardAsync`: rejected if set is locked. Assigns next slot number.

**DI registration** — add to both:
- `FlipKit.Desktop/App.axaml.cs` (as Transient, consistent with other DB-dependent services)
- `FlipKit.Web/Program.cs` (as Scoped)

---

## 2. Bulk Scan Flow + Rate-Limit Handling

### 2.1 New enums

```csharp
public enum BulkScanDestination { Inventory, SurpriseSet }
public enum ScanDepth { Quick, Standard }
```

### 2.2 `BulkScanViewModel` additions

New observable properties on `BulkScanViewModel`:

```csharp
[ObservableProperty] BulkScanDestination _destination = BulkScanDestination.Inventory;
[ObservableProperty] int? _destinationSurpriseSetId;
[ObservableProperty] SurpriseSet? _destinationSet;
[ObservableProperty] ScanDepth _scanDepth = ScanDepth.Standard;
[ObservableProperty] ObservableCollection<SurpriseSetIssue> _liveIssues = new();
[ObservableProperty] bool _showFreeModelWarning; // >40 items + free model
```

New commands:
- `ResumeBulkScanCommand` — re-runs `ScanAllAsync` over `Items.Where(i => i.Status == BulkScanItemStatus.Pending)`. Used after `AccountPerDay` cancellation.

New setting in `AppSettings`:
```csharp
public string? PreferredBulkScanModel { get; set; } // null = use catalog's first free vision model
```

### 2.3 `NewSurpriseSetDialog` (Desktop)

Collects required-up-front fields before navigating to `BulkScanView`:
- Set Name (required)
- Show Name (optional, free text)
- Listing Type (Auction / Buy it Now / Giveaway — dropdown)
- Spot Price (decimal)
- Shared Condition (dropdown matching Whatnot values)
- Shared Shipping Profile (text)
- Whatnot Category / Subcategory
- Lot Cost Basis (optional — if entered, even-split begins from card 1)

On confirm: inserts a `SurpriseSet` with `State = Draft`, navigates to `BulkScanView` with `Destination = SurpriseSet`, `DestinationSurpriseSetId` set, `ScanDepth = Standard`.

### 2.4 Save-flow branch in `BulkScanViewModel.SaveAllAsync`

When `Destination == SurpriseSet`:

```csharp
// For each card being saved into a set:
card.Status = CardStatus.ReservedForSet;
card.SurpriseSetId = DestinationSurpriseSetId;
card.SurpriseSetSlot = nextSlot++;

// Stamp shared fields from set
card.Condition = destinationSet.SharedCondition;
// (other shared fields are stored on the set, not duplicated on Card)

// Re-balance lot cost basis across all cards in the set
if (destinationSet.LotCostBasis.HasValue)
{
    var allSetCards = await _surpriseSetRepository.GetByIdWithCardsAsync(destinationSet.Id);
    decimal perCard = destinationSet.LotCostBasis.Value / allSetCards.Cards.Count;
    foreach (var c in allSetCards.Cards)
    {
        c.CostBasis = perCard;
        c.CostSource = "LotSplit";
        await _cardRepository.UpdateCardAsync(c);
    }
}
```

Live compliance validation fires after each card save (non-blocking; issues populate `LiveIssues` observable).

### 2.5 Web bulk scan for Surprise Sets

The Web `ScanController` currently handles single-image scans. Extend with:

**New routes:**
- `GET /SurpriseSet/New` — form to create a set + start bulk scan session
- `POST /SurpriseSet/New` — inserts Draft set, redirects to `/SurpriseSet/{id}/Scan`
- `GET /SurpriseSet/{id}/Scan` — multi-file upload page
- `POST /SurpriseSet/{id}/ScanBatch` — accepts the full file batch, processes sequentially, streams results
- `POST /SurpriseSet/{id}/SaveAll` — saves all scanned cards into the set

**Upload UX — batch photo picker (mirrors Desktop folder selection):**

The scan page uses `<input type="file" multiple accept="image/*">`. On mobile (iOS/Android), this opens the native photo picker with multi-select, which is the phone equivalent of selecting a folder of images on Desktop. The user selects all their card photos at once and submits.

```html
<input type="file" id="cardImages" name="files" multiple accept="image/*" />
```

On submit, all selected files upload together in a single `multipart/form-data` POST to `/SurpriseSet/{id}/ScanBatch`. The controller processes images sequentially (one at a time, matching the Desktop's free-model concurrency of 1) and streams results back via Server-Sent Events (SSE) so the page updates per-card as each scan completes — the same progressive feedback the Desktop provides.

**Pair mode on Web:** if the user is shooting front + back, they select all images together (naming convention: alphabetical order, same as Desktop). The controller applies the same strict alphabetical pairing logic from `BulkScanViewModel` — files sorted by name, odd indices are fronts, even indices are backs. A note on the scan page explains this: "Name your photos so fronts sort before backs (e.g., card01-front.jpg, card01-back.jpg)."

**Single-image mode:** if pair mode is off (set-level toggle), every uploaded file is treated as a front-only card.

**SSE streaming format:**
```
data: {"index":0,"status":"scanning","filename":"card01-front.jpg"}
data: {"index":0,"status":"complete","cardDetail":{...}}
data: {"index":1,"status":"error","error":"Rate limited — daily limit reached"}
data: {"type":"done","totalScanned":12,"totalErrors":1}
```

The page JS updates a results table row-by-row as events arrive. A "Resume" button appears if the session ends with pending items (analogous to `ResumeBulkScanCommand` on Desktop) — it re-submits only the files that didn't complete.

Rate-limit handling on Web: `AccountPerDay` emits an SSE error event with the reset timestamp and a link to `https://openrouter.ai/credits`. Processing halts; the Resume button re-queues remaining images for when the limit resets.

### 2.6 Rate-limit infrastructure

**New exception:** `FlipKit.Core/Services/Implementations/Scanning/OpenRouterRateLimitException.cs`

```csharp
public enum RateLimitScope { ProviderUpstream, AccountPerMinute, AccountPerDay, Unknown }

public class OpenRouterRateLimitException : Exception
{
    public RateLimitScope Scope { get; }
    public int? RetryAfterSeconds { get; }
    public DateTime? ResetAt { get; }
    public string ModelId { get; }

    public OpenRouterRateLimitException(
        RateLimitScope scope, string modelId,
        int? retryAfterSeconds = null, DateTime? resetAt = null)
        : base($"Rate limited ({scope}) on model {modelId}")
    {
        Scope = scope;
        ModelId = modelId;
        RetryAfterSeconds = retryAfterSeconds;
        ResetAt = resetAt;
    }
}
```

**429 parsing in `OpenRouterScannerService.SendSingleRequestAsync`:**

```csharp
if (response.StatusCode == HttpStatusCode.TooManyRequests)
{
    var retryAfter = response.Headers.RetryAfter?.Delta is { } d ? (int)d.TotalSeconds : (int?)null;
    var body = await response.Content.ReadAsStringAsync();
    var scope = Classify429(body);
    // Parse X-RateLimit-Reset header for AccountPerDay reset time if present
    var resetAt = TryParseResetHeader(response.Headers);
    throw new OpenRouterRateLimitException(scope, modelId, retryAfter, resetAt);
}

private static RateLimitScope Classify429(string responseBody)
{
    using var doc = JsonDocument.Parse(responseBody);
    var root = doc.RootElement;

    if (root.TryGetProperty("metadata", out var meta) &&
        meta.TryGetProperty("provider_name", out _))
        return RateLimitScope.ProviderUpstream;

    var message = root.TryGetProperty("error", out var err) &&
                  err.TryGetProperty("message", out var msg)
        ? msg.GetString() ?? string.Empty
        : string.Empty;

    if (message.Contains("free-models-per-day", StringComparison.OrdinalIgnoreCase))
        return RateLimitScope.AccountPerDay;

    if (message.Contains("rate-limited", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        return RateLimitScope.AccountPerMinute;

    return RateLimitScope.Unknown;
}
```

**Chain walker routing (in `ScanCardAsync`):**

| Scope | Behavior |
|---|---|
| `ProviderUpstream` | Walk to next model in chain |
| `AccountPerMinute` | Wait `RetryAfterSeconds` (default 60s if null), retry same model once, then walk |
| `AccountPerDay` | Re-throw — all free models share the bucket, walking is pointless |
| `Unknown` | Wait `RetryAfterSeconds` (default 30s if null), walk chain |

**Transient-error retry (5xx, JSON parse error, network):**

5 retries on the same model with exponential backoff: 2s, 4s, 8s, 16s, 32s. After all retries exhausted, walk chain.

**Three user-facing banners:**

1. **BulkScanView — pre-scan warning:** shown when item count > 40 and `PreferredBulkScanModel` resolves to a `:free` model. Text: "Scanning {n} cards with a free model may hit the daily API limit (~50 requests/day without credit). Consider adding OpenRouter credit or selecting a paid model in Settings."

2. **SettingsView — API key section note:** "Free-tier models share a daily quota (~50 requests/day without credit, ~1,000/day with $10+ lifetime credit). For bulk scans, a paid model or OpenRouter credit is recommended."

3. **BulkScanView — AccountPerDay error panel:** shown when `AccountPerDay` is thrown. Text: "OpenRouter free daily limit reached. Resets at {localTime}. [Add Credit ↗](https://openrouter.ai/credits)" with a "Resume Scan" button that triggers `ResumeBulkScanCommand`.

---

## 3. `SurpriseSetValidator`

### 3.1 Structure

**Interface:** `FlipKit.Core/Services/Interfaces/ISurpriseSetValidator.cs`

```csharp
public interface ISurpriseSetValidator
{
    IList<SurpriseSetIssue> Validate(SurpriseSet set, IList<Card> cards);
}

public record SurpriseSetIssue(
    string Code,
    string Message,
    IssueSeverity Severity,
    int? CardId = null,
    string? Field = null);

public enum IssueSeverity { Warning, Error }
```

**Implementation:** `FlipKit.Core/Services/Implementations/SurpriseSet/SurpriseSetValidator.cs`

Composes a list of `ISurpriseSetRule` instances, runs each, aggregates issues.

```csharp
public interface ISurpriseSetRule
{
    IEnumerable<SurpriseSetIssue> Check(SurpriseSet set, IList<Card> cards);
}
```

### 3.2 Rules

Files live in `FlipKit.Core/Services/Implementations/SurpriseSet/Rules/`:

| Class | Code | Severity | Condition |
|---|---|---|---|
| `MinCardsRule` | `MIN_CARDS` | Error | `cards.Count < 1` |
| `MaxCardsRule` | `MAX_CARDS` | Error | `cards.Count > 500` |
| `MixedSportRule` | `MIXED_SPORT` | Warning | more than one distinct non-null `card.Sport` |
| `MixedProductTypeRule` | `MIXED_PRODUCT` | Error | mix of graded and raw cards, or any sealed/repack type |
| `InconsistentConditionRule` | `INCONSISTENT_CONDITION` | Error | any `card.Condition` differs from `set.SharedCondition` |
| `MissingGalleryRule` | `MISSING_GALLERY` | Error | `set.SharedImageUrl1` is null or empty |
| `ProhibitedValueLanguageRule` | `PROHIBITED_VALUE_LANG` | Error | see regex below |
| `ProhibitedPrizeLanguageRule` | `PROHIBITED_PRIZE_LANG` | Error | see regex below |
| `CompletionDataRule` | `COMPLETION_DATA` | Error | only when `State >= Completed`: `GrossRevenue` or `SpotsSold` is null |
| `ManualAllocationRule` | `MANUAL_ALLOC_MISMATCH` | Error | `AllocationMethod == Manual` and per-card SalePrice sum ≠ NetGross |

**Prohibited value language regex** (case-insensitive, applied to `set.Title + " " + set.Notes`):
```
\b(floor|ceiling|average\s+value|book\s+value|estimated\s+value|worth\s+at\s+least|valued\s+at|guaranteed\s+value|guaranteed\s+minimum|min\s+value)\b
```

**Prohibited prize language regex:**
```
\b(guaranteed\s+hit|big\s+hit|chase\s+card|chase|holy\s+grail|grail\s+card|whale\s+hit|prize\s+card)\b
```

### 3.3 Tests

`FlipKit.Core.Tests/Services/SurpriseSet/Rules/` — one test class per rule. Pattern:

```csharp
public class MinCardsRuleTests
{
    [Fact]
    public void Should_ReturnError_When_NoCCards()
    {
        var rule = new MinCardsRule();
        var issues = rule.Check(new SurpriseSet(), Array.Empty<Card>());
        Assert.Contains(issues, i => i.Code == "MIN_CARDS" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Should_ReturnNoIssues_When_OneCard()
    {
        var rule = new MinCardsRule();
        var issues = rule.Check(new SurpriseSet(), [new Card()]);
        Assert.Empty(issues);
    }
}
```

---

## 4. CSV Exporter Extension

### 4.1 `SurpriseSetDescriptionGenerator`

**File:** `FlipKit.Core/Services/Implementations/Export/SurpriseSetDescriptionGenerator.cs`

```csharp
// CRITICAL: This generator is intentionally template-based, not LLM-based.
// Surprise set descriptions are subject to Whatnot's anti-prize-language and
// anti-value-language policies. LLM rewrites have produced policy-violating
// text in the past (e.g., "guaranteed hit", "minimum value $X"). Never wire
// an LLM into this path, even for "polish" or "tone adjustment".
public class SurpriseSetDescriptionGenerator
{
    public string Generate(SurpriseSet set, IList<Card> cards)
    {
        var sport = InferSport(cards);
        var checklist = BuildChecklist(cards);

        return $"""
            Surprise Set — {cards.Count} {sport} Card{(cards.Count == 1 ? "" : "s")}
            Each spot wins one randomly assigned card from the checklist below.
            Condition: {set.SharedCondition}
            Ships in penny sleeve + top loader + bubble mailer.

            CHECKLIST (in order of slot assignment):
            {checklist}

            All cards are individual sports cards. No mixed product types.
            """;
    }

    private static string BuildChecklist(IList<Card> cards)
    {
        var ordered = cards.OrderBy(c => c.SurpriseSetSlot ?? int.MaxValue);
        var lines = ordered.Select((c, i) =>
        {
            var year    = c.Year?.ToString() ?? "????";
            var set     = string.IsNullOrWhiteSpace(c.SetName) ? "Unknown Set" : c.SetName;
            var number  = string.IsNullOrWhiteSpace(c.CardNumber) ? "" : $" #{c.CardNumber}";
            var player  = string.IsNullOrWhiteSpace(c.PlayerName) ? "Unknown Player" : c.PlayerName;
            var rc      = c.IsRookie ? " (RC)" : "";
            return $"{c.SurpriseSetSlot ?? (i + 1)}. {year} {set}{number} — {player}{rc}";
        });
        return string.Join("\n", lines);
    }

    private static string InferSport(IList<Card> cards)
    {
        var sport = cards
            .Where(c => c.Sport != null)
            .GroupBy(c => c.Sport)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key?.ToString())
            .FirstOrDefault();
        return sport ?? "Sports";
    }
}
```

### 4.2 `SurpriseSetCsvExporter`

**File:** `FlipKit.Core/Services/Implementations/Export/SurpriseSetCsvExporter.cs`

Composes `WhatnotExporter` — does not duplicate row-serialization logic.

Key per-row construction:

| Column | Source |
|---|---|
| Title | `set.Title` (identical every row) |
| Description | `descriptionGenerator.Generate(set, cards)` (identical every row) |
| Type | `set.SharedListingType` |
| Quantity | `"1"` |
| Price | `set.SpotPrice.ToString("F2")` (identical every row) |
| Condition | `set.SharedCondition` |
| Shipping Profile | `set.SharedShippingProfile` |
| Category | `set.SharedWhatnotCategory` |
| Sub Category | `set.SharedWhatnotSubcategory ?? ""` |
| Offerable | `set.Offerable ? "yes" : "no"` |
| Image URL 1..8 | `set.SharedImageUrl1..8` (NOT per-card photos) |
| SKU | `$"FK-SET-{set.Id:D5}-{card.SurpriseSetSlot:D3}"` |
| Cost Per Item | `card.CostBasis?.ToString("F2") ?? ""` |

**State transition on successful export:**
```csharp
set.State = SurpriseSetState.Exported;
set.ExportedAt = DateTime.UtcNow;
await _surpriseSetRepository.UpdateAsync(set);
```

Cards remain `ReservedForSet`. After this point, `ISurpriseSetRepository.IsLockedAsync` returns true, blocking card add/remove.

**Validation gate:** `SurpriseSetValidator.Validate` is called before exporting. If any `Error`-severity issue exists, export is rejected and issues are returned to the caller. Warnings are shown but do not block export.

### 4.3 Tests

`FlipKit.Core.Tests/Services/SurpriseSet/SurpriseSetCsvExporterTests.cs`:
- Row uniformity: all rows have identical Title, Description, Price, images
- SKU format: `FK-SET-00001-001` through `FK-SET-00001-{n:D3}`
- Gallery stamping: set images, not per-card images
- Checklist ordering: slots respected
- Rookie tag: `(RC)` appears for `IsRookie = true`
- Edge cases: missing card number (`????`), missing year, single card

`FlipKit.Core.Tests/Services/SurpriseSet/SurpriseSetDescriptionGeneratorTests.cs`:
- Count line grammar (singular vs plural)
- Condition line
- Checklist ordering by slot
- Rookie tag
- Missing card number handled gracefully
- Missing year handled gracefully

---

## 5. Sales Reconciliation

### 5.1 Mark Completed flow

**Trigger:** "Mark Completed" command on set detail view (available in `Exported` or `Live` state).

**Step 1 — Input form:**
- SpotsSold (int, required, 1 ≤ n ≤ cards.Count)
- GrossRevenue (decimal, required)
- TotalFees (decimal, default 0)
- TotalShipping (decimal, default 0)
- AllocationMethod (override for this completion, defaults to set's configured method)

**Step 2 — Partial sell-through handling** (shown only if `SpotsSold < cards.Count`):
- List of `ReservedForSet` cards with a "Sold?" toggle (all toggled on by default)
- User unticks unsold cards
- Disposition for unticked cards (radio):
  - "Return to inventory" — re-evaluate status via `CardStatusEvaluator.Evaluate`
  - "Roll into a new Draft set" — create a new `SurpriseSet` in Draft state and move cards there

**Step 3 — Manual allocation** (shown only if `AllocationMethod == Manual`):
- Per-card decimal input
- Running total vs NetGross shown; submit blocked if sum ≠ NetGross (±$0.01 tolerance)

**Step 4 — Commit:**

```csharp
decimal netGross = GrossRevenue - TotalFees - TotalShipping;
var soldCards = cards.Where(c => soldCardIds.Contains(c.Id)).ToList();
var perCardAllocations = Allocate(netGross, soldCards, allocationMethod);

foreach (var (card, allocated) in soldCards.Zip(perCardAllocations))
{
    card.SalePrice    = allocated;
    card.FeesPaid     = allocated == 0 ? 0 : TotalFees * (allocated / netGross);
    card.ShippingCost = allocated == 0 ? 0 : TotalShipping * (allocated / netGross);
    card.NetProfit    = allocated - (card.CostBasis ?? 0) - card.FeesPaid - card.ShippingCost;
    card.SalePlatform = "Whatnot";
    card.SaleDate     = DateTime.UtcNow;
    card.Status       = CardStatus.SoldInSet;
    await _cardRepository.UpdateCardAsync(card);
}

set.SpotsSold    = SpotsSold;
set.GrossRevenue = GrossRevenue;
set.TotalFees    = TotalFees;
set.TotalShipping = TotalShipping;
set.State        = SurpriseSetState.Completed;
set.CompletedAt  = DateTime.UtcNow;
await _surpriseSetRepository.UpdateAsync(set);
```

### 5.2 Allocation math

**EqualSplit:**
```csharp
decimal each = netGross / soldCount;
return soldCards.Select(_ => each).ToList();
```

**CostWeighted:**
```csharp
decimal totalCost = soldCards.Sum(c => c.CostBasis ?? 0);
if (totalCost == 0)
    // Fall back to EqualSplit with a Warning issued to the caller
    return EqualSplit(netGross, soldCards);
return soldCards.Select(c => netGross * ((c.CostBasis ?? 0) / totalCost)).ToList();
```

**Manual:**
Per-card amounts from user input. `ManualAllocationRule` validates sum ≡ NetGross (±$0.01).

### 5.3 Reports impact

- `SoldInSet` rolls into revenue totals wherever `SoldStatuses` is used (via the updated `CardStatusPredicates`).
- Reports that group by platform will show "Whatnot" for `SoldInSet` cards.
- Existing `/api/reports/sold` endpoint: update status filter to include `SoldInSet`.

### 5.4 Tests

`FlipKit.Core.Tests/Services/SurpriseSet/RevenueAllocatorTests.cs`:
- EqualSplit distributes evenly
- CostWeighted weights proportionally
- CostWeighted falls back to EqualSplit when all costs are 0
- Manual sums validated correctly
- Partial sell-through: only sold cards receive allocation

Integration test: `SurpriseSetLifecycleTests.cs` — full `Draft → Exported → Completed` path using `TestDbContext.Create()`.

---

## 6. Edge Cases

### 6.1 Move card from inventory → set

Available via "Add to Surprise Set…" command in inventory. Only Draft sets shown in picker. Card must be `Draft`, `Priced`, or `Ready` (not already `Listed`, `ReservedForSet`, or `Sold*`).

If card is `Listed`: block with message "This card is marked as Listed on another platform. Remove that listing before adding to a Surprise Set."

On successful add: `Status → ReservedForSet`, `SurpriseSetId` set, slot assigned, lot cost re-balanced.

### 6.2 Remove card from Draft set

Allowed only while `State == Draft` (enforced by `ISurpriseSetRepository.IsLockedAsync`). On remove: `SurpriseSetId → null`, `SurpriseSetSlot → null`, status re-evaluated via `CardStatusEvaluator.Evaluate`, remaining card slots renumbered.

### 6.3 Hard delete of abandoned Draft set

Available via "Delete Set" on Draft set detail. Blocked if `State != Draft`.

Confirmation dialog: "Delete '{set.Name}'? This will permanently delete this set and all {n} of its cards. This cannot be undone."

On confirm: `SurpriseSetRepository.DeleteAsync(id)` — within a transaction:
1. `DELETE FROM cards WHERE surprise_set_id = @id`
2. `DELETE FROM surprise_sets WHERE id = @id`

### 6.4 State transitions

| From | Allowed transitions |
|---|---|
| Draft | → Exported (via export), → Cancelled |
| Exported | → Live (manual toggle), → Completed, → Cancelled |
| Live | → Completed, → Cancelled |
| Completed | (terminal — no transitions) |
| Cancelled | (terminal — no transitions) |

State changes are validated in the repository — invalid transitions throw `InvalidOperationException`.

### 6.5 Slot numbering

Slots are 1-based. On add, slot = `Max(existing slots) + 1`. On remove + renumber, remaining cards are assigned `1..N` in their current slot order. Renumbering is performed in a single transaction to avoid partial states.

### 6.6 LotCostBasis re-balance

Triggered on every card save into a set. If `LotCostBasis` is null, cards get `CostBasis = null`. If set, `perCard = LotCostBasis / N` where N = total cards in set after this save. All cards in the set (including previously saved ones) are updated.

User can override per-card `CostBasis` after the fact; the re-balance only applies when `CostSource == "LotSplit"`. Manual overrides (`CostSource != "LotSplit"`) are preserved.

---

## Phase 3 — Implementation Order

> Stop after each checkpoint and wait for user review.

1. **EF entity changes + SchemaUpdater + DI** — `SurpriseSet` model, enum extensions, `Card` columns, `CardStatusPredicates`, `ISurpriseSetRepository` + impl. Update 5 status-filter call sites. Register DI in Desktop + Web. `dotnet build` clean. **Checkpoint.**

2. **`SurpriseSetValidator` + unit tests** — all 10 rules, all tests, no UI. **Checkpoint.**

3. **Bulk scan service changes** — `ScanDepth` enum, `OpenRouterRateLimitException` + 429 parser + chain walker + exponential backoff retry. `BulkScanViewModel` additions (`Destination`, `DestinationSurpriseSetId`, `ScanDepth`, save-flow branch, `ResumeBulkScanCommand`, `PreferredBulkScanModel`, three banners). Rate-limit parser unit tests. `AppSettings` additions. **Checkpoint.**

4. **`SurpriseSetDescriptionGenerator`** — deterministic templates, no-LLM class comment, full unit tests. **Checkpoint.**

5. **`SurpriseSetCsvExporter`** — composes `WhatnotExporter`, state transition on export, per-card SKU, validator gate. Unit tests for row uniformity, SKU format, gallery stamping, description embedding. **Checkpoint.**

6. **Desktop UI** — `NewSurpriseSetDialog`, nav entry in `MainWindow`, `SurpriseSetListView`, `SurpriseSetDetailView`, live compliance panel in `BulkScanView`. Manual golden-path test (scan → review → export). **Checkpoint.**

7. **Web UI** — `SurpriseSet` controller + views (new set, bulk scan, list, detail, mark completed, CSV download). Full parity with Desktop flow. **Checkpoint.**

8. **Sales reconciliation** — mark-completed dialog, allocation math service, partial sell-through handling, status transitions, reports impact. Unit tests per allocation method. Full lifecycle integration test. **Checkpoint.**
