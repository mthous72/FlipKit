# Variation Verification Pipeline

## Overview

A single vision-model pass identifies most card variations, but it misses
obscure parallels, can hallucinate parallel names, and can't confirm whether a
variation actually exists in a given set's checklist.

FlipKit addresses this with **two cooperating mechanisms**:

1. **Checklist verification (shipped)** — after a scan, the
   `ChecklistVerificationMatcher` cross-references the scanned card against an
   imported `SetChecklist` and returns a tiered confidence outcome that drives
   the editor UI. See *Shipped: checklist verification matcher* below.
2. **Verified-fields LLM hint mode (shipped)** — when fields have already been
   confirmed (via OCR + the checklist directory), the scan call locks those
   fields so the LLM echoes them verbatim and spends its capacity on the visual
   pattern fields it actually sees. See *Shipped: verified-fields LLM hint mode*
   below.

The remainder of this document (from *The Problem* onward) is the original
multi-stage design spec. It predates the shipped implementation and is retained
for background; where it conflicts with the two "Shipped" sections, the shipped
sections are authoritative.

---

## Shipped: checklist verification matcher

`FlipKit.Core/Services/Implementations/ChecklistVerificationMatcher.cs`
(`IChecklistVerificationMatcher`) runs after the scan and produces a
`ChecklistMatchResult` with one of these tier outcomes (mirrored by the
`VerificationStatus` enum stored on `Card`):

| Tier | When | Editor surface |
|---|---|---|
| **Verified** (Tier 1) | Card number normalizes to an exact checklist match, player name fuzzy-matches ≥ 0.85, and parallel resolves cleanly (all field confidences ≥ 0.85). | Green "Verified" badge. |
| **BestGuess** (Tier 2) | Card # + player match, but at least one field (typically Parallel or Subset) is uncertain. | Amber "Best guess" badge. |
| **NoMatch** (Tier 3) | Card # missing from the set, or present with a wildly mismatched player. Result carries the top fuzzy candidates. | "Pick from checklist" picker. |

Key behaviors:

- **Checklist lookup** is keyed on `(Manufacturer, Brand, Year, Sport)`. When no
  `SetChecklist` exists for that tuple (or identity fields are missing), the
  result sets `ChecklistMissing = true` and the UI surfaces the
  "import this checklist" banner (Surface B).
- **Card-number matching** normalizes via `FuzzyMatcher.NormalizeCardNumber`
  before comparing.
- **Player-name scoring** combines Levenshtein similarity with a token-overlap
  bonus so reordered/multi-word names ("Roman Anthony" vs "Anthony, Roman")
  still score well. Match threshold 0.85; candidate threshold 0.55; up to 10
  candidates returned for the picker.
- **Parallel scoring** is a soft heuristic: high (0.95) when the matched
  ChecklistCard's subset contains the parallel name, otherwise mid (0.70) so the
  user confirms via the dropdown.
- **Re-find key** — `BuildMatchKey(setChecklistId, checklistCard)` produces the
  `"{id}:{normalizedNumber}:{subsetLower}"` string stored on
  `Card.MatchedChecklistKey`, so the matched row can be re-located later (e.g.
  by the missing-checklists audit when a new checklist is imported).

---

## Shipped: verified-fields LLM hint mode

When the caller has already validated identity fields (OCR extraction +
checklist-directory match), it passes an `OcrHint` (`FlipKit.Core/Models/OcrHint.cs`)
into the scan call. The hint has **two modes**, selected by whether
`VerifiedFieldNames` is non-empty:

- **Soft-hint mode (default)** — every supplied value is a suggestion. The LLM
  is told to treat them as hints and verify with its own vision; it may override
  anything. Built by `BuildSoftHintPreamble`.
- **Verified-fields mode** — when `VerifiedFieldNames` is populated, those fields
  are locked. The preamble (`BuildLockedHintPreamble`) lists each confirmed field
  with its JSON key and value under a *"CONFIRMED FIELDS — echo these EXACT values
  verbatim, do NOT re-derive"* header, lists any remaining unverified hints
  separately, and appends the raw OCR text. This focuses the model's effort on
  the visual-pattern fields (finish, border color, parallel) that OCR can't see.

Field names match the scan JSON schema keys (`player_name`, `year`,
`card_number`, `manufacturer`, `brand`, `set_name`, `team`, `sport`,
`parallel_name`, `serial_numbered`, `is_rookie`, `is_auto`, `is_relic`,
`is_graded`, `grade_company`, `grade_value`).

**Drift guard:** after the response comes back,
`OpenRouterScannerService.ApplyVerifiedFieldOverrides` re-stamps every verified
field onto the resulting `Card` regardless of what the model returned, and counts
how many it had to correct. If the model disobeyed the "echo verbatim"
instruction, the confirmed values still win.

This flow is wired into the Bulk Scan **"Enhance with AI"** path on OCR-scanned
cards (commit `223cf95`). See [ai-scanning.md](ai-scanning.md) for the broader
scan pipeline (CardSight → OpenRouter).

---

## The Problem (original design spec)

> The sections below are the original pre-implementation design. They describe a
> 3-stage pipeline (extract → verify → confirm) and a bundled-checklist data
> strategy. The shipped system uses user-imported checklists (ADR-004) and the
> matcher described above rather than a bundled `checklists.db`. Read for
> background only.

A single vision model call gets you ~70-80% accuracy on variation identification:

---

## The Problem

A single vision model call gets you ~70-80% accuracy on variation identification:

- ✅ Correctly reads player name, card number, team most of the time
- ⚠️ Sometimes guesses wrong parallel name (calls a "Pink Ice" a "Pink")
- ⚠️ Can't distinguish unnumbered parallels that look similar in photos
- ❌ May hallucinate a parallel that doesn't exist in that set
- ❌ Doesn't know the full checklist for a given product

## The Solution

A 3-stage pipeline that runs after the initial scan:

```
┌─────────────────┐
│  Stage 1        │  Initial AI Vision Scan (existing)
│  EXTRACT        │  → Player, year, brand, visual cues, raw text
└────────┬────────┘
         ▼
┌─────────────────┐
│  Stage 2        │  Cross-reference against known set checklists
│  VERIFY         │  → Confirm card exists, validate variation name
└────────┬────────┘
         ▼
┌─────────────────┐
│  Stage 3        │  Targeted re-ask for low-confidence fields
│  CONFIRM        │  → "Is the border gold or orange?" type questions
└─────────────────┘
```

---

## Architecture (Fits Existing MVVM)

### New Service Interface

Add to `FlipKit.Core/Services/`:

```csharp
// IVariationVerifier.cs
public interface IVariationVerifier
{
    /// <summary>
    /// Run the full verification pipeline on a scanned card.
    /// Updates the card's variation fields and returns a confidence report.
    /// </summary>
    Task<VerificationResult> VerifyCardAsync(Card card, string imagePath);

    /// <summary>
    /// Look up a specific set's checklist from the local cache.
    /// Returns null if the set is not cached.
    /// </summary>
    Task<SetChecklist?> GetChecklistAsync(string manufacturer, string brand, int year);

    /// <summary>
    /// Refresh the local checklist cache for a specific set.
    /// Scrapes TCDB or loads from bundled data.
    /// </summary>
    Task<bool> RefreshChecklistAsync(string manufacturer, string brand, int year);
}
```

### New Models

Add to `FlipKit.Core/Models/`:

```csharp
// VerificationResult.cs
public class VerificationResult
{
    public VerificationConfidence OverallConfidence { get; set; }
    public Dictionary<string, FieldConfidence> FieldConfidences { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public bool ChecklistMatch { get; set; }
    public string? SuggestedVariation { get; set; }
}

public class FieldConfidence
{
    public string FieldName { get; set; } = "";
    public string Value { get; set; } = "";
    public VerificationConfidence Confidence { get; set; }
    public string? Reason { get; set; }
}

public enum VerificationConfidence
{
    High,       // Vision + checklist + visual cues all agree
    Medium,     // Vision + checklist agree, visual cues unclear
    Low,        // Vision only, no checklist confirmation
    Conflict    // Vision says one thing, checklist says another
}

// SetChecklist.cs
public class SetChecklist
{
    public int Id { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Brand { get; set; } = "";
    public int Year { get; set; }
    public List<ChecklistCard> Cards { get; set; } = new();
    public List<string> KnownVariations { get; set; } = new();
    public DateTime CachedAt { get; set; }
}

public class ChecklistCard
{
    public string CardNumber { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Team { get; set; } = "";
    public bool IsRookie { get; set; }
}
```

### New Database Table

Add to `FlipKitDbContext.cs`:

```csharp
public DbSet<SetChecklist> SetChecklists { get; set; }

// In OnModelCreating:
modelBuilder.Entity<SetChecklist>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.Manufacturer, e.Brand, e.Year }).IsUnique();
    entity.Property(e => e.Cards).HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<ChecklistCard>>(v, (JsonSerializerOptions?)null) ?? new()
    );
    entity.Property(e => e.KnownVariations).HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
    );
});
```

### Infrastructure Implementation

Add to `FlipKit.Infrastructure/Services/`:

```csharp
// VariationVerifierService.cs — implements IVariationVerifier
```

See the full implementation specification in the sections below.

---

## Stage 1: Enhanced Vision Extraction (Modify Existing)

### Changes to `OpenRouterScannerService`

The existing scan prompt returns a flat JSON object. Enhance it to also return **visual cues** and **raw text arrays** that Stage 2 uses for verification.

**Updated prompt** (replaces the one in `03-OPENROUTER-INTEGRATION.md`):

```
Analyze this sports card image and extract all identifying information.

Return ONLY a JSON object with these exact fields (use null for unknown values):

{
  "player_name": "Full player name",
  "card_number": "Card number without # symbol",
  "year": 2024,
  "sport": "Football|Baseball|Basketball",
  "manufacturer": "Panini|Topps|Upper Deck|Leaf",
  "brand": "Sub-brand (Prizm, Donruss, Chrome, etc.)",
  "set_name": "Full set name if visible",
  "team": "Team name",
  "variation_type": "Base|Parallel|Insert|Refractor|Auto|Relic",
  "parallel_name": "Color/pattern name (Silver, Blue, Gold, etc.) or null",
  "serial_numbered": "Print run as string (/99, /25, 1/1) or null",
  "is_rookie": true|false,
  "is_auto": true|false,
  "is_relic": true|false,
  "is_short_print": true|false,
  "condition_notes": "Any visible condition issues",

  "visual_cues": {
    "border_color": "Color of the card border (gold, silver, blue, standard, etc.)",
    "card_finish": "matte|glossy|holographic|refractor|shimmer|sparkle",
    "has_foil": true|false,
    "has_rainbow_effect": true|false,
    "serial_number_visible": true|false,
    "serial_denominator": "The /XX number if visible, e.g. 99, 25, 10",
    "background_color": "Dominant background color",
    "has_autograph_sticker": true|false,
    "has_on_card_auto": true|false,
    "has_relic_window": true|false,
    "relic_type": "single_swatch|multi_swatch|patch|prime or null"
  },
  "all_visible_text": ["Array", "of", "every", "text", "string", "visible", "on", "card"],
  "confidence": {
    "player_name": "high|medium|low",
    "year": "high|medium|low",
    "brand": "high|medium|low",
    "variation": "high|medium|low"
  }
}

Identification tips:
- "RC" or "Rated Rookie" logo = rookie card
- Serial numbers usually at bottom (e.g., 045/199)
- Panini brands: Prizm, Donruss, Mosaic, Select, Optic, Contenders, Phoenix
- Topps brands: Chrome, Heritage, Stadium Club, Finest, Bowman, Inception
- Rainbow/shimmer effects indicate parallels
- Actual ink/sticker signature = auto
- Jersey swatch or memorabilia piece = relic
- Report ALL text you can read in "all_visible_text" — even small print

Return ONLY the JSON, no other text or markdown.
```

### Updated `ScanCardAsync` Return

The `IScannerService.ScanCardAsync` method should now return a `ScanResult` instead of just a `Card`:

```csharp
// Add to Core/Models/
public class ScanResult
{
    public Card Card { get; set; } = new();
    public VisualCues VisualCues { get; set; } = new();
    public List<string> AllVisibleText { get; set; } = new();
    public Dictionary<string, string> Confidences { get; set; } = new();
}

public class VisualCues
{
    public string? BorderColor { get; set; }
    public string? CardFinish { get; set; }
    public bool HasFoil { get; set; }
    public bool HasRainbowEffect { get; set; }
    public bool SerialNumberVisible { get; set; }
    public string? SerialDenominator { get; set; }
    public string? BackgroundColor { get; set; }
    public bool HasAutographSticker { get; set; }
    public bool HasOnCardAuto { get; set; }
    public bool HasRelicWindow { get; set; }
    public string? RelicType { get; set; }
}
```

**Update the interface:**

```csharp
// IScannerService.cs — updated
public interface IScannerService
{
    Task<ScanResult> ScanCardAsync(string imagePath, string? model = null);
}
```

---

## Stage 2: Checklist Verification

### Bundled Checklist Data

The app ships with a **bundled SQLite database of common set checklists** covering the most popular products. This avoids needing live web access for verification.

**Data source:** Pre-scraped from TCDB.com for the most common sets.

**Coverage priority (bundle these first):**

| Priority | Sets | Why |
|----------|------|-----|
| P0 | Panini Prizm (Football, Basketball) 2020-2024 | Most traded product |
| P0 | Topps Chrome (Baseball) 2020-2024 | Most traded baseball |
| P0 | Panini Donruss/Optic (Football, Basketball) 2020-2024 | Very common |
| P1 | Panini Mosaic, Select 2020-2024 | Popular mid-tier |
| P1 | Topps Bowman Chrome 2020-2024 | Prospect focused |
| P2 | Panini Contenders, Phoenix 2022-2024 | Less common |
| P2 | Upper Deck Hockey 2022-2024 | Hockey sellers |

**Bundled data file:** `FlipKit.App/Assets/checklists.db` — a separate read-only SQLite DB embedded as a resource.

### Checklist Matching Algorithm

```csharp
public async Task<ChecklistMatchResult> MatchAgainstChecklist(ScanResult scan)
{
    var result = new ChecklistMatchResult();

    // Step 1: Find the set checklist
    var checklist = await GetChecklistAsync(
        scan.Card.Manufacturer,
        scan.Card.Brand,
        scan.Card.Year ?? 0
    );

    if (checklist == null)
    {
        result.ChecklistFound = false;
        result.Notes.Add("Set checklist not in database. Variation not verified.");
        return result;
    }

    result.ChecklistFound = true;

    // Step 2: Match card number
    var cardMatch = checklist.Cards.FirstOrDefault(c =>
        NormalizeCardNumber(c.CardNumber) == NormalizeCardNumber(scan.Card.CardNumber));

    if (cardMatch != null)
    {
        result.CardNumberVerified = true;

        // Verify player name matches
        if (FuzzyMatch(cardMatch.PlayerName, scan.Card.PlayerName) > 0.85)
        {
            result.PlayerVerified = true;
        }
        else
        {
            result.Warnings.Add(
                $"Card #{scan.Card.CardNumber} is '{cardMatch.PlayerName}' in checklist, " +
                $"but scan read '{scan.Card.PlayerName}'");
            result.SuggestedPlayerName = cardMatch.PlayerName;
        }

        // Check rookie status
        if (cardMatch.IsRookie && !scan.Card.IsRookie)
        {
            result.Suggestions.Add("Checklist shows this is a rookie card. Marking as RC.");
            scan.Card.IsRookie = true;
        }
    }
    else
    {
        result.Warnings.Add(
            $"Card #{scan.Card.CardNumber} not found in {scan.Card.Year} {scan.Card.Brand} checklist.");
    }

    // Step 3: Validate variation exists in this set
    if (!string.IsNullOrEmpty(scan.Card.ParallelName))
    {
        var normalizedParallel = NormalizeParallelName(scan.Card.ParallelName);
        var knownMatch = checklist.KnownVariations
            .FirstOrDefault(v => NormalizeParallelName(v) == normalizedParallel);

        if (knownMatch != null)
        {
            result.VariationVerified = true;
            // Use the canonical name from the checklist
            scan.Card.ParallelName = knownMatch;
        }
        else
        {
            // Try fuzzy matching
            var fuzzyMatch = checklist.KnownVariations
                .Select(v => new { Name = v, Score = FuzzyMatch(v, scan.Card.ParallelName) })
                .Where(x => x.Score > 0.7)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (fuzzyMatch != null)
            {
                result.Suggestions.Add(
                    $"Did you mean '{fuzzyMatch.Name}'? (AI read '{scan.Card.ParallelName}')");
                result.SuggestedVariation = fuzzyMatch.Name;
            }
            else
            {
                result.Warnings.Add(
                    $"'{scan.Card.ParallelName}' is not a known variation for " +
                    $"{scan.Card.Year} {scan.Card.Brand}. Please verify manually.");
            }
        }
    }

    // Step 4: Cross-reference visual cues with variation rules
    ValidateVisualCues(scan, checklist, result);

    return result;
}
```

### Visual Cue Cross-Reference

```csharp
private void ValidateVisualCues(ScanResult scan, SetChecklist checklist, ChecklistMatchResult result)
{
    var cues = scan.VisualCues;
    var parallel = scan.Card.ParallelName?.ToLowerInvariant() ?? "";

    // Serial number validation
    if (cues.SerialNumberVisible && !string.IsNullOrEmpty(cues.SerialDenominator))
    {
        var denom = cues.SerialDenominator;

        // Check if this matches a known numbered parallel
        var numberedParallels = GetNumberedParallels(checklist);
        var match = numberedParallels
            .FirstOrDefault(p => p.PrintRun.ToString() == denom);

        if (match != null)
        {
            if (parallel != NormalizeParallelName(match.Name).ToLowerInvariant())
            {
                result.Suggestions.Add(
                    $"Serial /{denom} matches '{match.Name}' parallel. " +
                    $"AI identified as '{scan.Card.ParallelName}'.");
                result.SuggestedVariation = match.Name;
            }
            // Also set the serial_numbered field
            scan.Card.SerialNumbered = $"/{denom}";
        }
    }

    // Foil/refractor detection
    if (cues.HasRainbowEffect || cues.HasFoil)
    {
        if (parallel == "base")
        {
            result.Suggestions.Add(
                "Card appears to have foil/rainbow effect but was identified as Base. " +
                "This may be a parallel — check manually.");
        }
    }

    // Color border validation
    if (!string.IsNullOrEmpty(cues.BorderColor) && cues.BorderColor != "standard")
    {
        var borderColor = cues.BorderColor.ToLowerInvariant();
        if (!parallel.Contains(borderColor) && parallel == "base")
        {
            result.Suggestions.Add(
                $"Card has a {cues.BorderColor} border but was identified as Base. " +
                "This may be a color parallel.");
        }
    }
}
```

---

## Stage 3: Targeted Re-Ask (Confirmation Pass)

When Stage 2 produces `Low` or `Conflict` confidence, send the image back to the vision model with **specific, targeted questions** rather than a general "identify this card" prompt.

### When to Trigger

```csharp
public bool NeedsConfirmationPass(VerificationResult result)
{
    return result.OverallConfidence == VerificationConfidence.Low
        || result.OverallConfidence == VerificationConfidence.Conflict
        || result.Suggestions.Count > 0
        || result.FieldConfidences.Any(f =>
            f.Value.Confidence == VerificationConfidence.Conflict);
}
```

### Targeted Prompts

```csharp
public string BuildConfirmationPrompt(ScanResult scan, VerificationResult verification)
{
    var questions = new List<string>();

    // Variation conflict
    if (verification.SuggestedVariation != null
        && verification.SuggestedVariation != scan.Card.ParallelName)
    {
        questions.Add(
            $"The card was initially identified as '{scan.Card.ParallelName}' parallel. " +
            $"The checklist suggests it might be '{verification.SuggestedVariation}'. " +
            "Looking closely at the card's color, border, and finish, " +
            "which parallel name is correct?");
    }

    // Border color unclear
    if (scan.VisualCues.BorderColor == null || scan.VisualCues.BorderColor == "standard")
    {
        questions.Add(
            "What color is the border of this card? " +
            "Options: standard/white, silver, gold, blue, red, green, orange, purple, pink, black.");
    }

    // Serial number unclear
    if (!scan.VisualCues.SerialNumberVisible
        && (scan.Card.ParallelName?.ToLowerInvariant().Contains("gold") == true
            || scan.Card.ParallelName?.ToLowerInvariant().Contains("green") == true))
    {
        questions.Add(
            "Is there a serial number (like /25 or /99) visible anywhere on the card, " +
            "especially near the bottom?");
    }

    // Surface finish unclear
    if (scan.VisualCues.CardFinish == null)
    {
        questions.Add(
            "What is the surface finish of this card? " +
            "Options: matte, glossy, holographic/refractor, shimmer, sparkle, chrome-like.");
    }

    var prompt = "Look carefully at this sports card image and answer these specific questions.\n\n" +
        "Return ONLY a JSON object with your answers:\n\n{\n";

    for (int i = 0; i < questions.Count; i++)
    {
        prompt += $"  \"question_{i + 1}\": \"Your answer here\",\n";
        prompt += $"  // Q{i + 1}: {questions[i]}\n";
    }

    prompt += "  \"final_parallel_name\": \"Your best determination of the parallel/variation name\",\n";
    prompt += "  \"confidence\": \"high|medium|low\"\n}\n\n";
    prompt += "Answer ONLY based on what you can see in the image.";

    return prompt;
}
```

---

## Integration into ScanViewModel

### Updated Scan Flow

The `ScanViewModel.ScanCardCommand` currently does:

```
1. Send image to OpenRouter → get Card
2. Display in form
3. User reviews and saves
```

**Updated flow:**

```
1. Send image to OpenRouter → get ScanResult (Card + VisualCues + Confidences)
2. Run VariationVerifier.VerifyCardAsync(card, imagePath)
3. If suggestions exist, show them inline in the form
4. If confirmation pass needed, run it automatically (with spinner)
5. Display final result in form with confidence indicators
6. User reviews, accepts/rejects suggestions, and saves
```

### ViewModel Changes

Add to `ScanViewModel.cs`:

```csharp
[ObservableProperty]
private VerificationResult? _verificationResult;

[ObservableProperty]
private bool _isVerifying;

[ObservableProperty]
private string _verificationStatus = "";

[RelayCommand]
private async Task ScanCard()
{
    if (string.IsNullOrEmpty(SelectedImagePath)) return;

    try
    {
        IsScanning = true;
        ScanStatus = "Scanning card with AI...";

        // Stage 1: Initial scan
        var scanResult = await _scannerService.ScanCardAsync(SelectedImagePath);
        CurrentCard = scanResult.Card;

        // Stage 2: Verification
        IsScanning = false;
        IsVerifying = true;
        VerificationStatus = "Verifying against checklist...";

        var verification = await _variationVerifier.VerifyCardAsync(
            scanResult.Card, SelectedImagePath);
        VerificationResult = verification;

        // Stage 3: Confirmation pass (if needed)
        if (_variationVerifier.NeedsConfirmationPass(verification))
        {
            VerificationStatus = "Running confirmation check...";
            // This sends a second vision request with targeted questions
            await _variationVerifier.RunConfirmationPassAsync(
                scanResult, verification, SelectedImagePath);
        }

        // Apply high-confidence suggestions automatically
        ApplyAutoSuggestions(verification);

        VerificationStatus = GetStatusSummary(verification);
    }
    catch (Exception ex)
    {
        ScanStatus = $"Error: {ex.Message}";
    }
    finally
    {
        IsScanning = false;
        IsVerifying = false;
    }
}

private void ApplyAutoSuggestions(VerificationResult result)
{
    // Auto-apply suggestions where checklist definitively corrects the AI
    if (result.SuggestedPlayerName != null && result.PlayerVerified == false)
    {
        // Don't auto-apply player name changes — show as suggestion
    }

    if (result.SuggestedVariation != null
        && result.OverallConfidence == VerificationConfidence.High)
    {
        // Auto-apply if high confidence from checklist + visual cues
        CurrentCard.ParallelName = result.SuggestedVariation;
    }
}

private string GetStatusSummary(VerificationResult result)
{
    return result.OverallConfidence switch
    {
        VerificationConfidence.High =>
            "✅ Verified — card and variation confirmed against checklist",
        VerificationConfidence.Medium =>
            "⚠️ Partially verified — review suggestions below",
        VerificationConfidence.Low =>
            "⚠️ Not verified — checklist not available, review carefully",
        VerificationConfidence.Conflict =>
            "❌ Conflict detected — AI and checklist disagree, review needed",
        _ => ""
    };
}
```

### UI Changes to ScanView

Add a verification status panel below the scan results form:

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Verification                                                            │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ✅ Verified — card and variation confirmed against checklist            │
│                                                                          │
│  ┌─ Confidence ────────────────────────────────────────────────────┐    │
│  │  Player:    🟢 High   (matches checklist)                       │    │
│  │  Year:      🟢 High                                             │    │
│  │  Brand:     🟢 High                                             │    │
│  │  Variation: 🟡 Medium (AI said "Pink", checklist has "Pink Ice")│    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  💡 Suggestion: Did you mean "Pink Ice"? [Accept] [Ignore]              │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

In XAML, this would be a collapsible `Expander` or a `StackPanel` that is visible when `VerificationResult != null`.

---

## Checklist Data Management

### Bundled Data Strategy

Ship a `checklists.db` file as an embedded resource. On first run, copy it to the app data directory alongside `cards.db`.

```
{AppData}/FlipKit/
├── cards.db              ← User's card inventory
├── checklists.db         ← Bundled set checklists (read-only copy)
├── config.json           ← User settings
└── images/               ← Card photos
```

### Building the Checklist Database

Create a standalone tool (not part of the main app) for building `checklists.db`:

```csharp
// Tools/ChecklistBuilder/ — separate console project
// Scrapes TCDB.com and builds the checklist SQLite file
// Run manually before each app release to update bundled data
```

**TCDB scraping approach:**

1. For each set, hit: `https://www.tcdb.com/ViewAll.cfm/sid/{set_id}/`
2. Parse the HTML table for card numbers, player names, team
3. Hit the set's variation page for known parallels
4. Store in `checklists.db`

**Important:** This tool runs offline during development. The main FlipKit app does NOT scrape TCDB at runtime. Users get pre-built data.

### Updating Checklists

When a new FlipKit version is released:
1. Developer runs ChecklistBuilder tool to scrape latest sets
2. Updated `checklists.db` is bundled with the new release
3. On update, the new `checklists.db` replaces the old one

Users can also manually trigger a checklist refresh for a specific set (future feature — downloads updated data for one set from a hosted endpoint).

---

## Fuzzy Matching Utilities

### String Matching

Add to `FlipKit.Core/Helpers/`:

```csharp
// FuzzyMatcher.cs
public static class FuzzyMatcher
{
    /// <summary>
    /// Returns similarity score 0.0-1.0 using Levenshtein distance.
    /// </summary>
    public static double Match(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;

        a = Normalize(a);
        b = Normalize(b);

        if (a == b) return 1.0;

        int distance = LevenshteinDistance(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        return 1.0 - (double)distance / maxLen;
    }

    /// <summary>
    /// Normalize a string for comparison: lowercase, remove punctuation,
    /// collapse whitespace.
    /// </summary>
    public static string Normalize(string input)
    {
        return Regex.Replace(input.ToLowerInvariant().Trim(), @"[^a-z0-9\s]", "")
            .Replace("  ", " ");
    }

    /// <summary>
    /// Normalize parallel/variation names specifically.
    /// Handles common aliases.
    /// </summary>
    public static string NormalizeParallelName(string name)
    {
        var normalized = Normalize(name);

        // Common aliases
        var aliases = new Dictionary<string, string>
        {
            { "refractor", "refractor" },
            { "refractors", "refractor" },
            { "xfractor", "x-fractor" },
            { "x fractor", "x-fractor" },
            { "superfractor", "superfractor" },
            { "super fractor", "superfractor" },
            { "holo", "holographic" },
            { "pink ice", "pink ice" },
            { "neon green", "neon green" },
            { "rated rookie", "rated rookie" },
            { "rr", "rated rookie" },
        };

        return aliases.GetValueOrDefault(normalized, normalized);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        // Standard Levenshtein implementation
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));

        return d[n, m];
    }
}
```

---

## Cost Impact

The verification pipeline adds **zero to one** additional API call per card:

| Scenario | Extra API Calls | Extra Cost |
|----------|----------------|------------|
| Checklist match, high confidence | 0 | $0.00 |
| Checklist match, needs confirmation | 1 | ~$0.005-0.01 |
| No checklist available | 0 | $0.00 |

The checklist lookup is entirely local (SQLite query). Only the confirmation pass (Stage 3) costs anything, and it only fires when there's ambiguity.

**Estimated cost increase:** ~$0.003 per card average (most cards won't need Stage 3).

---

## DI Registration

Add to `App.axaml.cs`:

```csharp
// In ConfigureServices:
services.AddTransient<IVariationVerifier, VariationVerifierService>();
```

---

## Settings Addition

Add to `AppSettings.cs`:

```csharp
// Verification settings
public bool EnableVariationVerification { get; set; } = true;
public bool AutoApplyHighConfidenceSuggestions { get; set; } = true;
public bool RunConfirmationPass { get; set; } = true;  // Stage 3
```

Add to Settings UI:

```
┌─ Scanning Settings ─────────────────────────────────────────────────────┐
│                                                                          │
│  ☑ Verify variations against set checklists                             │
│  ☑ Auto-apply high-confidence corrections                               │
│  ☑ Run confirmation pass for ambiguous cards (uses 1 extra API call)    │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## File Placement Summary

```
FlipKit/
├── src/
│   ├── FlipKit.Core/
│   │   ├── Models/
│   │   │   ├── ScanResult.cs              ← NEW
│   │   │   ├── VisualCues.cs              ← NEW
│   │   │   ├── VerificationResult.cs      ← NEW
│   │   │   ├── SetChecklist.cs            ← NEW
│   │   │   └── ... (existing)
│   │   ├── Services/
│   │   │   ├── IVariationVerifier.cs      ← NEW
│   │   │   ├── IScannerService.cs         ← MODIFIED (returns ScanResult)
│   │   │   └── ... (existing)
│   │   └── Helpers/
│   │       ├── FuzzyMatcher.cs            ← NEW
│   │       └── ... (existing)
│   │
│   ├── FlipKit.Infrastructure/
│   │   ├── Services/
│   │   │   ├── VariationVerifierService.cs    ← NEW
│   │   │   ├── OpenRouterScannerService.cs    ← MODIFIED (new prompt, returns ScanResult)
│   │   │   └── ... (existing)
│   │   └── Data/
│   │       ├── FlipKitDbContext.cs          ← MODIFIED (add SetChecklist entity)
│   │       └── Migrations/                    ← NEW migration
│   │
│   └── FlipKit.App/
│       ├── Assets/
│       │   └── checklists.db                  ← NEW (bundled data)
│       └── Views/
│           └── ScanView.axaml                 ← MODIFIED (verification panel)
│
└── tools/
    └── ChecklistBuilder/                      ← NEW (standalone tool, not part of main app)
        ├── ChecklistBuilder.csproj
        └── Program.cs
```
