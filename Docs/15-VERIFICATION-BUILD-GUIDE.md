# Claude Code Build Guide — Variation Verification Pipeline

## Overview

Step-by-step prompts for Claude Code to implement the variation verification pipeline described in `14-VARIATION-VERIFICATION.md`. These should be run AFTER the base app is functional (Steps 1-12 in `07-CLAUDE-CODE-GUIDE.md`).

**Prerequisites:** The app already has a working scan flow (OpenRouter → Card → Save to DB).

---

## Step 13: New Models for Verification

**Prompt for Claude Code:**
```
Read 14-VARIATION-VERIFICATION.md for context. Now create the following new model files in FlipKit.Core/Models/:

1. ScanResult.cs:
   - Card property (Card type)
   - VisualCues property (VisualCues type)
   - AllVisibleText (List<string>)
   - Confidences (Dictionary<string, string>) — maps field names to "high"/"medium"/"low"

2. VisualCues.cs:
   - BorderColor (string?)
   - CardFinish (string?) — matte, glossy, holographic, refractor, shimmer, sparkle
   - HasFoil (bool)
   - HasRainbowEffect (bool)
   - SerialNumberVisible (bool)
   - SerialDenominator (string?) — the /XX number
   - BackgroundColor (string?)
   - HasAutographSticker (bool)
   - HasOnCardAuto (bool)
   - HasRelicWindow (bool)
   - RelicType (string?) — single_swatch, multi_swatch, patch, prime

3. VerificationResult.cs:
   - OverallConfidence (VerificationConfidence enum)
   - FieldConfidences (Dictionary<string, FieldConfidence>)
   - Warnings (List<string>)
   - Suggestions (List<string>)
   - ChecklistMatch (bool)
   - SuggestedVariation (string?)
   - SuggestedPlayerName (string?)
   - CardNumberVerified (bool)
   - PlayerVerified (bool)
   - VariationVerified (bool)

4. FieldConfidence.cs:
   - FieldName (string)
   - Value (string)
   - Confidence (VerificationConfidence)
   - Reason (string?)

5. VerificationConfidence.cs (enum):
   - High, Medium, Low, Conflict

6. SetChecklist.cs:
   - Id (int)
   - Manufacturer (string)
   - Brand (string)
   - Year (int)
   - Cards (List<ChecklistCard>) — will be JSON-serialized in DB
   - KnownVariations (List<string>) — will be JSON-serialized in DB
   - CachedAt (DateTime)

7. ChecklistCard.cs:
   - CardNumber (string)
   - PlayerName (string)
   - Team (string)
   - IsRookie (bool)

Make sure all classes are in the FlipKit.Core.Models namespace.
```

---

## Step 14: FuzzyMatcher Helper

**Prompt for Claude Code:**
```
Create FlipKit.Core/Helpers/FuzzyMatcher.cs — a static utility class for string matching used by the verification pipeline.

Methods:

1. Match(string a, string b) → double
   - Returns similarity score 0.0 to 1.0
   - Uses Levenshtein distance
   - Normalizes both strings first (lowercase, remove punctuation, collapse whitespace)
   - Returns 1.0 for exact matches, 0.0 for null/empty inputs

2. Normalize(string input) → string
   - ToLowerInvariant, trim
   - Remove non-alphanumeric except spaces
   - Collapse multiple spaces to single

3. NormalizeCardNumber(string number) → string
   - Remove leading zeros, remove "#" prefix
   - Trim whitespace
   - Handle formats like "RC-15", "127", "#001"

4. NormalizeParallelName(string name) → string
   - Normalize the string
   - Apply known aliases dictionary:
     - "refractors" → "refractor"
     - "xfractor" / "x fractor" → "x-fractor"
     - "superfractor" / "super fractor" → "superfractor"
     - "holo" → "holographic"
     - "rr" → "rated rookie"
     - "prizm" → "prizm" (not "prism")
     - "sp" → "short print"
     - "ssp" → "super short print"
   - Return normalized name

5. LevenshteinDistance(string s, string t) → int
   - Standard dynamic programming implementation
   - Private helper method

Include XML doc comments on all public methods. Namespace: FlipKit.Core.Helpers.
```

---

## Step 15: Update IScannerService Interface

**Prompt for Claude Code:**
```
Update FlipKit.Core/Services/IScannerService.cs:

Change the return type of ScanCardAsync from Card to ScanResult:

Before:
  Task<Card> ScanCardAsync(string imagePath, string? model = null);

After:
  Task<ScanResult> ScanCardAsync(string imagePath, string? model = null);

Make sure to add the using statement for ScanResult from FlipKit.Core.Models.
```

---

## Step 16: Update OpenRouterScannerService

**Prompt for Claude Code:**
```
Update FlipKit.Infrastructure/Services/OpenRouterScannerService.cs to:

1. Return ScanResult instead of Card

2. Use this updated vision prompt (replace the existing one):

"""
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
    "border_color": "Color of the card border or null",
    "card_finish": "matte|glossy|holographic|refractor|shimmer|sparkle or null",
    "has_foil": true|false,
    "has_rainbow_effect": true|false,
    "serial_number_visible": true|false,
    "serial_denominator": "The /XX number if visible or null",
    "background_color": "Dominant background color or null",
    "has_autograph_sticker": true|false,
    "has_on_card_auto": true|false,
    "has_relic_window": true|false,
    "relic_type": "single_swatch|multi_swatch|patch|prime or null"
  },
  "all_visible_text": ["every", "text", "string", "visible", "on", "card"],
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
- Report ALL text you can read in "all_visible_text"

Return ONLY the JSON, no other text or markdown.
"""

3. Parse the JSON response into a ScanResult object:
   - Map the main fields to Card properties (same as before)
   - Map "visual_cues" object to a new VisualCues instance
   - Map "all_visible_text" to List<string>
   - Map "confidence" to Dictionary<string, string>
   - Handle missing visual_cues gracefully (default to empty VisualCues if not present)

4. Keep the existing error handling (JSON parse errors, API errors, etc.)

5. Make sure the Card object in ScanResult is still fully populated like before — this is backwards compatible.
```

---

## Step 17: IVariationVerifier Interface

**Prompt for Claude Code:**
```
Create FlipKit.Core/Services/IVariationVerifier.cs with these methods:

1. VerifyCardAsync(Card card, string imagePath) → Task<VerificationResult>
   - Main entry point: runs the full verification pipeline
   - Takes the card from Stage 1 scan and the original image path

2. GetChecklistAsync(string manufacturer, string brand, int year) → Task<SetChecklist?>
   - Looks up a set checklist from the local database
   - Returns null if not found

3. NeedsConfirmationPass(VerificationResult result) → bool
   - Returns true if Stage 3 should run

4. RunConfirmationPassAsync(ScanResult scanResult, VerificationResult verification, string imagePath) → Task<VerificationResult>
   - Sends targeted questions to the vision model
   - Updates the verification result with answers

Namespace: FlipKit.Core.Services
```

---

## Step 18: VariationVerifierService Implementation

**Prompt for Claude Code:**
```
Create FlipKit.Infrastructure/Services/VariationVerifierService.cs implementing IVariationVerifier.

Constructor dependencies:
- FlipKitDbContext (for checklist queries)
- IScannerService (for confirmation pass — sending targeted questions)
- ISettingsService (for checking if verification is enabled)

Implement these methods following the logic in 14-VARIATION-VERIFICATION.md:

1. VerifyCardAsync:
   a. Look up checklist for the card's manufacturer/brand/year
   b. If no checklist found, return Low confidence result with note
   c. If checklist found:
      - Match card number (use FuzzyMatcher.NormalizeCardNumber)
      - Verify player name (use FuzzyMatcher.Match with 0.85 threshold)
      - Validate parallel name exists in KnownVariations (use FuzzyMatcher.NormalizeParallelName)
      - Cross-reference visual cues (border color vs parallel name, serial number vs numbered parallels)
   d. Build VerificationResult with per-field confidences

2. GetChecklistAsync:
   - Query SetChecklists table where Manufacturer, Brand, Year match
   - Use case-insensitive comparison
   - Return null if not found

3. NeedsConfirmationPass:
   - Return true if OverallConfidence is Low or Conflict
   - OR if there are any Suggestions
   - OR if any field has Conflict confidence

4. RunConfirmationPassAsync:
   - Build a targeted prompt with specific questions about ambiguous fields
   - Questions should be based on what the verification found:
     * If variation conflict: "AI said X, checklist has Y, which is correct?"
     * If border color unclear: "What color is the border?"
     * If serial number might be present: "Is there a serial number visible?"
   - Send to IScannerService (reuse the OpenRouter call with the targeted prompt)
   - Parse answers and update the VerificationResult
   - Update the Card's fields if the confirmation resolves the conflict

Private helper methods:
- ValidateVisualCues(ScanResult, SetChecklist, VerificationResult) — cross-reference visual cues
- BuildConfirmationPrompt(ScanResult, VerificationResult) → string
- CalculateOverallConfidence(VerificationResult) → VerificationConfidence

Use FuzzyMatcher from FlipKit.Core.Helpers for all string comparisons.
```

---

## Step 19: Database Migration for Checklists

**Prompt for Claude Code:**
```
Update FlipKit.Infrastructure/Data/FlipKitDbContext.cs:

1. Add: public DbSet<SetChecklist> SetChecklists { get; set; }

2. In OnModelCreating, configure the SetChecklist entity:
   - HasKey(e => e.Id)
   - HasIndex on (Manufacturer, Brand, Year) as unique
   - Cards property: use HasConversion with JsonSerializer to store as JSON text
   - KnownVariations property: use HasConversion with JsonSerializer to store as JSON text

3. Create a new EF Core migration:
   cd FlipKit.Infrastructure
   dotnet ef migrations add AddSetChecklists --startup-project ../FlipKit.App

This table will store cached set checklist data. The Cards and KnownVariations columns are JSON strings in SQLite.
```

---

## Step 20: Update ScanViewModel for Verification

**Prompt for Claude Code:**
```
Update FlipKit.Core/ViewModels/ScanViewModel.cs to integrate the verification pipeline:

1. Add new constructor dependency: IVariationVerifier

2. Add new observable properties:
   - VerificationResult? VerificationResult
   - bool IsVerifying
   - string VerificationStatus
   - bool ShowVerificationPanel (true when VerificationResult != null)
   - bool HasSuggestions (true when there are suggestions to show)
   - ObservableCollection<string> VerificationWarnings
   - ObservableCollection<string> VerificationSuggestions

3. Update the ScanCard command/method to add verification after scanning:

   async Task ScanCard():
     a. IsScanning = true, ScanStatus = "Scanning card with AI..."
     b. var scanResult = await _scannerService.ScanCardAsync(imagePath)
     c. CurrentCard = scanResult.Card  (populate the form as before)
     d. IsScanning = false

     e. Check if verification is enabled in settings
     f. If enabled:
        - IsVerifying = true, VerificationStatus = "Verifying against checklist..."
        - var result = await _variationVerifier.VerifyCardAsync(scanResult.Card, imagePath)
        - If NeedsConfirmationPass(result):
            VerificationStatus = "Running confirmation check..."
            result = await _variationVerifier.RunConfirmationPassAsync(scanResult, result, imagePath)
        - VerificationResult = result
        - Populate VerificationWarnings and VerificationSuggestions from result
        - Auto-apply high-confidence corrections if setting is enabled
        - IsVerifying = false

4. Add AcceptSuggestionCommand(string suggestion):
   - When user clicks "Accept" on a suggestion (e.g., changing parallel name)
   - Apply the suggested value to CurrentCard
   - Remove the suggestion from the list

5. Add IgnoreSuggestionCommand(string suggestion):
   - Remove the suggestion from the list without applying

Keep the existing save flow unchanged — verification happens between scan and save.
```

---

## Step 21: Update ScanView XAML for Verification Panel

**Prompt for Claude Code:**
```
Update FlipKit.App/Views/ScanView.axaml to add a verification results panel.

Add a new section below the card details form (above the Save button):

1. Verification Status Panel (visible when IsVerifying or VerificationResult != null):
   - When IsVerifying: show a ProgressBar (IsIndeterminate=True) + VerificationStatus text
   - When VerificationResult is set:
     a. Overall confidence badge:
        - High: green background, "✅ Verified" text
        - Medium: yellow background, "⚠️ Partially Verified" text
        - Low: gray background, "ℹ️ Unverified" text
        - Conflict: red background, "❌ Conflict Detected" text
     b. Field confidence list (optional, collapsible Expander):
        - Show each field with its confidence level (colored dot)
     c. Warnings list:
        - ItemsControl bound to VerificationWarnings
        - Each warning in orange text
     d. Suggestions list:
        - ItemsControl bound to VerificationSuggestions
        - Each suggestion has the text + [Accept] [Ignore] buttons
        - Accept calls AcceptSuggestionCommand
        - Ignore calls IgnoreSuggestionCommand

2. Keep the existing layout — this panel fits between the card detail form and the Save/Next buttons.

3. If verification is disabled in settings, this panel simply doesn't show.

Use Avalonia Fluent theme styles. Keep it clean and non-overwhelming — the panel should be helpful, not noisy.
```

---

## Step 22: Register New Services in DI

**Prompt for Claude Code:**
```
Update FlipKit.App/App.axaml.cs to register the new verification service:

In the ConfigureServices section, add:
  services.AddTransient<IVariationVerifier, VariationVerifierService>();

Make sure the VariationVerifierService gets its dependencies:
  - FlipKitDbContext (already registered)
  - IScannerService (already registered)
  - ISettingsService (already registered)

Also update ScanViewModel registration to include IVariationVerifier in its constructor.
```

---

## Step 23: Settings UI for Verification

**Prompt for Claude Code:**
```
Update the Settings page to include verification toggles.

1. Add to AppSettings.cs (if not already):
   - EnableVariationVerification (bool, default true)
   - AutoApplyHighConfidenceSuggestions (bool, default true)
   - RunConfirmationPass (bool, default true)

2. Add to SettingsViewModel.cs:
   - Observable properties for the three new settings
   - They save/load with the rest of AppSettings

3. Add to SettingsView.axaml, in a new "Scanning" section:
   - Header: "Card Scanning"
   - CheckBox: "Verify variations against set checklists"
     Bound to EnableVariationVerification
   - CheckBox: "Auto-apply high-confidence corrections"
     Bound to AutoApplyHighConfidenceSuggestions
     Enabled only when EnableVariationVerification is true
   - CheckBox: "Run confirmation pass for ambiguous cards (uses 1 extra API call)"
     Bound to RunConfirmationPass
     Enabled only when EnableVariationVerification is true

Place this section after the API Connections section but before Preferences.
```

---

## Step 24: Seed Checklist Data

**Prompt for Claude Code:**
```
Create a data seeding mechanism for the bundled checklist data.

1. Create FlipKit.Infrastructure/Data/ChecklistSeeder.cs:
   - Static method: SeedFromBundledData(FlipKitDbContext context)
   - Checks if SetChecklists table is empty
   - If empty, loads data from embedded JSON resource and inserts

2. Create the bundled data file FlipKit.App/Assets/checklist_seed.json:
   - JSON array of SetChecklist objects
   - Start with these high-priority sets (use realistic but abbreviated data):

   a. 2023 Panini Prizm Football:
      - KnownVariations: ["Base", "Silver", "Red White Blue", "Red", "Blue /199", "Carolina Blue /149", "Orange /99", "Purple /75", "Pink /50", "Light Blue /35", "Green /25", "Gold /10", "Black 1/1", "Gold Vinyl 1/1", "Shimmer", "Neon Green", "Hyper", "White Sparkle", "Snakeskin"]
      - Cards: first 10 entries with card number, player name, team, isRookie

   b. 2023 Panini Prizm Basketball:
      - Same variation structure as football
      - Cards: first 10 entries

   c. 2024 Topps Chrome Baseball:
      - KnownVariations: ["Base", "Refractor", "Sepia /100", "Pink /50", "Green /25", "Orange /10", "Red /5", "Gold 1/1", "SuperFractor 1/1", "X-Fractor", "Prism Refractor", "Atomic Refractor", "Negative Refractor"]
      - Cards: first 10 entries

   d. 2023 Panini Donruss Football:
      - KnownVariations: ["Base", "Rated Rookie", "Press Proof Silver", "Press Proof Blue /75", "Press Proof Gold /25", "Press Proof Red /10", "Press Proof Black 1/1", "Holo Red", "Holo Blue", "Holo Gold", "Holo Purple"]
      - Cards: first 10 entries

3. In App.axaml.cs startup (after Database.Migrate()), call:
   ChecklistSeeder.SeedFromBundledData(context);

4. Mark the JSON file as an EmbeddedResource in the .csproj.

NOTE: This is seed data for development and initial testing. The full checklist database will be built with a separate tool before release. The seed data just needs to be realistic enough to test the verification pipeline.
```

---

## Testing the Verification Pipeline

### Manual Test Flow

```
1. Launch app
2. Go to Scan tab
3. Drop a card image (e.g., 2023 Prizm Silver parallel)
4. Click "Scan Card"
5. Watch for:
   a. "Scanning card with AI..." → fills in form fields
   b. "Verifying against checklist..." → brief pause
   c. Verification panel appears:
      - Should show "✅ Verified" if it's a 2023 Prizm card (seeded data)
      - Should show field confidences
      - Should show suggestions if AI identified wrong parallel
6. If suggestions appear, click "Accept" or "Ignore"
7. Click "Save to My Cards"
```

### Test Cases to Try

```
Test 1: Known card in seeded checklist
- Use a 2023 Prizm football card image
- Expected: High confidence, all fields verified

Test 2: Card NOT in checklist
- Use a 2022 Mosaic card (not in seed data)
- Expected: Low confidence, "checklist not available" warning

Test 3: Wrong parallel name
- If AI says "Pink" but checklist has "Pink /50"
- Expected: Suggestion to use "Pink /50"

Test 4: Verification disabled
- Turn off verification in Settings
- Expected: No verification panel appears, scan works as before

Test 5: Confirmation pass
- Card with ambiguous parallel (looks Silver but could be base in good lighting)
- Expected: Confirmation questions asked, result improves
```

---

## Iteration Notes

- **Build Steps 13-16 first** (models, matcher, updated scanner interface) — these are foundation
- **Then Steps 17-19** (verifier interface, implementation, DB migration)
- **Then Steps 20-22** (ViewModel integration, UI, DI)
- **Steps 23-24 last** (settings, seed data)
- **Test after each step** — `dotnet build` should pass
- **Test the full pipeline** only after all steps are complete
