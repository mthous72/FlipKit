# Building in Claude Code — Avalonia MVVM Edition

## Overview

This guide walks through implementing the Card Lister using Claude Code. We build incrementally in C# with Avalonia UI + MVVM, testing each piece before moving on.

---

## Prerequisites

1. **.NET 8 SDK** installed (`dotnet --version` should show 8.0+)
2. **Claude Code** installed (`npm install -g @anthropic-ai/claude-code`)
3. **API Keys:**
   - OpenRouter: https://openrouter.ai/keys
   - ImgBB: https://api.imgbb.com/

---

## Build Order

### Step 1: Create Solution & Projects

**Prompt for Claude Code:**
```
Create a new .NET 8 solution called "CardLister" with three projects:

1. CardLister.App — Avalonia application (use `dotnet new avalonia.app`)
2. CardLister.Core — Class library for ViewModels, Models, and service interfaces
3. CardLister.Infrastructure — Class library for concrete service implementations

Set up project references:
- App references Core and Infrastructure
- Infrastructure references Core

Add these NuGet packages:
- CardLister.App: Avalonia (11.*), Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter
- CardLister.Core: CommunityToolkit.Mvvm (8.*)
- CardLister.Infrastructure: Microsoft.EntityFrameworkCore.Sqlite (8.*), CsvHelper (33.*), Microsoft.Extensions.DependencyInjection (8.*), Microsoft.Extensions.Http (8.*), Serilog.Extensions.Logging, Serilog.Sinks.File

Also install the Avalonia templates if not already:
dotnet new install Avalonia.Templates

Create the initial folder structure in each project:
- Core: Models/, ViewModels/, Services/, Helpers/
- Infrastructure: Data/, Services/, ApiModels/
- App: Views/, Styles/, Converters/, Assets/
```

### Step 2: Models & Enums

**Prompt:**
```
In CardLister.Core/Models/, create these files:

1. Card.cs — The main entity with these properties:
   - Id (int, primary key)
   - PlayerName, Year, Manufacturer, Brand, CardNumber, Team
   - Sport (enum: Football, Baseball, Basketball)
   - ParallelName, IsRookie, IsAutograph, IsRelic, IsNumbered, NumberedTo, Condition
   - ImagePathFront, ImagePathBack, ImageUrl1, ImageUrl2
   - EstimatedValue, ListingPrice, PriceDate (DateTime?), PriceCheckCount
   - CostBasis, CostSource (enum), CostDate, CostNotes
   - SalePrice, SaleDate, SalePlatform, FeesPaid, ShippingCost, NetProfit
   - Status (enum: Draft, Priced, Ready, Listed, Sold)
   - ShippingProfile, Notes, CreatedAt, UpdatedAt

2. PriceHistory.cs — Price tracking entity:
   - Id, CardId, EstimatedValue, ListingPrice, PriceSource, Notes, RecordedAt
   - Navigation property to Card

3. AppSettings.cs — User configuration:
   - OpenRouterApiKey, ImgbbApiKey, IsEbaySeller
   - DefaultShippingProfile, DefaultCondition
   - WhatnotFeePercent, EbayFeePercent
   - DefaultShippingCostPwe, DefaultShippingCostBmwt
   - PriceStalenessThresholdDays

4. Enums/CardStatus.cs — Draft, Priced, Ready, Listed, Sold
5. Enums/Sport.cs — Football, Baseball, Basketball
6. Enums/CostSource.cs — LCS, Online, Break, Trade, Pack, Gift, Other

Use decimal for all money fields. Use DateTime for all date fields.
```

### Step 3: Service Interfaces

**Prompt:**
```
In CardLister.Core/Services/, create these interface files:

1. ICardRepository.cs:
   - InsertCardAsync(Card) → int
   - UpdateCardAsync(Card)
   - GetCardAsync(int id) → Card?
   - GetAllCardsAsync(CardStatus?, Sport?) → List<Card>
   - DeleteCardAsync(int id)
   - SearchCardsAsync(string query) → List<Card>
   - GetStaleCardsAsync(int thresholdDays) → List<Card>
   - AddPriceHistoryAsync(PriceHistory)
   - GetCardCountAsync() → int

2. IScannerService.cs:
   - ScanCardAsync(string imagePath, string model) → Card

3. IPricerService.cs:
   - BuildTerapeakUrl(Card) → string
   - BuildEbaySoldUrl(Card) → string
   - SuggestPrice(decimal estimatedValue, Card) → decimal
   - CalculateNet(decimal salePrice, decimal feePercent) → decimal

4. IImageUploadService.cs:
   - UploadImageAsync(string imagePath, string? name) → string (URL)
   - UploadCardImagesAsync(string frontPath, string? backPath) → (string? url1, string? url2)

5. IExportService.cs:
   - GenerateTitle(Card) → string
   - GenerateDescription(Card) → string
   - ExportCsvAsync(List<Card>, string outputPath)
   - ValidateCardForExport(Card) → List<string> (issues)
   - ExportTaxCsvAsync(List<Card> soldCards, string outputPath)

6. ISettingsService.cs:
   - Load() → AppSettings
   - Save(AppSettings)
   - HasValidConfig() → bool
   - TestOpenRouterConnectionAsync(string apiKey) → bool
   - TestImgBBConnectionAsync(string apiKey) → bool

7. IBrowserService.cs:
   - OpenUrl(string url)

8. IFileDialogService.cs:
   - OpenImageFileAsync() → string?
   - SaveCsvFileAsync(string defaultFileName) → string?

All methods that do I/O should be async Task.
```

### Step 4: EF Core Database

**Prompt:**
```
In CardLister.Infrastructure/Data/, create:

1. CardListerDbContext.cs — EF Core DbContext:
   - DbSet<Card> Cards
   - DbSet<PriceHistory> PriceHistories
   - Override OnModelCreating to configure:
     - Card table with proper column types (decimal precision, enum as string)
     - PriceHistory with foreign key to Card (cascade delete)
     - Index on Card.Status and Card.Sport for filtering

2. CardRepository.cs — Implements ICardRepository:
   - Constructor takes CardListerDbContext
   - All methods use async EF Core queries
   - GetStaleCardsAsync: filter where PriceDate is older than threshold and Status is not Sold
   - SearchCardsAsync: search PlayerName, Manufacturer, Brand, Team using LIKE
   - GetAllCardsAsync: optional filters on status and sport, ordered by UpdatedAt desc

After creating the DbContext, add an initial migration:
cd CardLister.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../CardLister.App

The database file should be created at the app's data directory (use Environment.GetFolderPath for AppData).
```

### Step 5: Settings Service

**Prompt:**
```
In CardLister.Infrastructure/Services/, create JsonSettingsService.cs implementing ISettingsService:

- Store config in {AppData}/CardLister/config.json
- Use System.Text.Json for serialization
- Load(): Read and deserialize, return defaults if file missing
- Save(): Serialize and write to file
- HasValidConfig(): Check if both API keys are non-empty
- TestOpenRouterConnectionAsync(): Make a minimal API call to OpenRouter to verify key
- TestImgBBConnectionAsync(): Make a minimal API call to ImgBB to verify key

Use HttpClient (injected via IHttpClientFactory) for the test calls.
Create the directory if it doesn't exist.
Handle file not found gracefully (return default AppSettings).
```

### Step 6: Main Window Shell + Navigation

**Prompt:**
```
Create the MainWindow with sidebar navigation for CardLister.App.

1. ViewLocator.cs — Maps ViewModels to Views by naming convention:
   - Replace "Core.ViewModels" with "App.Views"
   - Replace "ViewModel" with "View"
   - Implements IDataTemplate

2. MainWindowViewModel.cs (in Core/ViewModels/):
   - Has CurrentPage (ObservableObject) property
   - NavigateToCommand(string page) — switches CurrentPage to the correct ViewModel
   - Checks IsFirstRun on startup (from ISettingsService.HasValidConfig())
   - If first run, sets CurrentPage to SetupWizardViewModel

3. MainWindow.axaml:
   - DockPanel layout
   - Left sidebar (80px wide) with vertical StackPanel of nav buttons
   - Each button has an icon (PathIcon) + text label
   - Active button highlighted
   - Right side: ContentControl bound to CurrentPage
   - Settings gear icon in top-right

4. Register ViewLocator in App.axaml DataTemplates

5. Set up DI in App.axaml.cs:
   - Register all services and ViewModels
   - Create MainWindow with MainWindowViewModel as DataContext
   - Ensure EF Core migration runs on startup (context.Database.Migrate())

Use Avalonia.Themes.Fluent as the base theme.
Create placeholder views (just a TextBlock with the page name) for all pages so navigation works.
```

### Step 7: Scan Feature (End-to-End)

**Prompt:**
```
Build the complete Scan feature for CardLister:

1. CardDetailViewModel.cs (Core/ViewModels/):
   - Observable properties for every card field (player, year, brand, etc.)
   - ToCard() method that creates a Card entity from the VM properties
   - FromCard(Card) static method that populates VM from a Card entity
   - Used by both ScanView and InventoryView for editing

2. ScanViewModel.cs (Core/ViewModels/):
   - ImagePath (string?) — bound to image preview
   - ScannedCard (CardDetailViewModel?) — bound to form
   - IsScanning (bool) — shows spinner, disables buttons
   - ErrorMessage (string?) — shown when scan fails
   - BrowseImageCommand — opens file dialog via IFileDialogService
   - ScanCardCommand — calls IScannerService.ScanCardAsync, populates ScannedCard
   - SaveCardCommand — calls ICardRepository.InsertCardAsync, resets form
   - CanScan — only when ImagePath is set and not currently scanning

3. OpenRouterScannerService.cs (Infrastructure/Services/):
   - Implements IScannerService
   - Reads image file, base64 encodes it
   - Sends to OpenRouter API with vision prompt (refer to 03-OPENROUTER-INTEGRATION.md)
   - Parses JSON response into Card entity
   - Handles markdown code blocks in response (strip ```json```)
   - Default model: "openai/gpt-4o-mini"

4. ScanView.axaml:
   - Two-column Grid layout
   - Left: Border with drag-drop zone (DragDrop.Drop event), shows image preview when loaded
   - Right: Card detail form (TextBoxes bound to ScannedCard properties)
   - Bottom: "Scan Card" and "Save to My Cards" buttons
   - Spinner overlay when IsScanning is true
   - Error banner when ErrorMessage is set

5. AvaloniaFileDialogService.cs (Infrastructure/Services/):
   - Uses Avalonia's StorageProvider for native file dialogs
   - OpenImageFileAsync: filter for jpg, png, webp

Test by running the app, dropping a card image, clicking Scan.
```

### Step 8: Inventory View

**Prompt:**
```
Build the Inventory (My Cards) page:

1. InventoryViewModel.cs:
   - Cards (ObservableCollection<Card>) — loaded from repository
   - FilteredCards — filtered view based on search/status/sport
   - SearchText, SelectedStatus, SelectedSport — filter properties
   - SelectedCards (for bulk actions)
   - LoadCardsCommand — async, loads from DB
   - DeleteSelectedCommand — confirms, then deletes
   - MarkReadyCommand — sets status to Ready for selected cards
   - EditCardCommand(Card) — opens card detail
   - RefreshCommand — reloads from DB
   - StaleCardCount — computed property for "Reprice Stale (N)" button

2. InventoryView.axaml:
   - Top bar: TextBox (search), ComboBox (sport filter), ComboBox (status filter)
   - DataGrid with columns:
     - CheckBox (selection)
     - Image thumbnail (small)
     - Player, Year, Brand, Price (formatted as currency)
     - Price Age (colored circle: green/yellow/red based on days since PriceDate)
     - Status badge
   - Bottom bar: Delete Selected, Mark Ready buttons
   - "Reprice Stale Cards (N)" button (visible when N > 0)
   - Double-click row to edit

Use PriceAgeToColorConverter for the colored indicators.
Load cards when the page is navigated to (OnActivated pattern or Loaded event).
```

### Step 9: Pricing View

**Prompt:**
```
Build the Pricing page:

1. PricerService.cs (Infrastructure/Services/):
   - BuildTerapeakUrl: Constructs Terapeak search URL from card details
   - BuildEbaySoldUrl: Constructs eBay sold listings URL
   - SuggestPrice: Takes market value, applies inverse fee calc for target net
   - CalculateNet: salePrice * (1 - feePercent/100)

2. SystemBrowserService.cs (Infrastructure/Services/):
   - OpenUrl: Uses Process.Start with UseShellExecute = true

3. PricingViewModel.cs:
   - UnpricedCards list (status = Draft)
   - CurrentCard (Card being priced)
   - CurrentIndex / TotalCount for "Card 3 of 15" display
   - MarketValue input (decimal)
   - SuggestedPrice (computed from MarketValue)
   - ListingPrice (user can override)
   - Cost basis fields
   - OpenTerapeakCommand → opens URL in browser
   - OpenEbaySoldCommand → opens URL in browser
   - SaveAndNextCommand → saves price + cost basis, moves to next card
   - SkipCommand → moves to next without saving

4. PricingView.axaml:
   - Card display (thumbnail + details text)
   - "Open Terapeak" and "Open eBay Sold" buttons
   - Market Value TextBox → auto-updates Suggested Price
   - Listing Price TextBox (editable)
   - Cost basis section (collapsible): Cost, Source dropdown, Date picker, Notes
   - "Save & Next →" button, "Skip" button
   - "Card 3 of 15" progress indicator
```

### Step 10: Export View

**Prompt:**
```
Build the Export page:

1. ImgBBUploadService.cs (Infrastructure/Services/):
   - UploadImageAsync: POST to ImgBB API with base64 image, return URL
   - UploadCardImagesAsync: Upload front (and optional back), return both URLs
   - Use IHttpClientFactory for HttpClient

2. CsvExportService.cs (Infrastructure/Services/):
   - GenerateTitle: "{Year} {Manufacturer} {Brand} {PlayerName} {ParallelName} #{CardNumber}"
   - GenerateDescription: Card details, team, condition, shipping note
   - ExportCsvAsync: Map Card fields to Whatnot CSV columns using CsvHelper
   - ValidateCardForExport: Check required fields (title, price, image URLs)
   - ExportTaxCsvAsync: Generate tax-ready CSV for sold cards
   - Refer to 04-WHATNOT-CSV-FORMAT.md for exact column mapping

3. ExportViewModel.cs:
   - ReadyCards (filtered to Status = Ready)
   - UploadProgress (0-100)
   - IsUploading, IsExporting booleans
   - UploadImagesCommand: Batch upload all card images to ImgBB with progress
   - ExportCsvCommand: Validate → generate → save via file dialog
   - ValidationIssues list (shown if any cards have problems)
   - CardsWithoutImages count

4. ExportView.axaml:
   - Step 1: Card count + preview table
   - Step 2: "Upload Images" button + ProgressBar
   - Step 3: "Download CSV" button → save file dialog
   - Step 4: Instructions text + "Open Whatnot Seller Hub" link button
   - Validation warnings shown if any cards have issues
```

### Step 11: Setup Wizard

**Prompt:**
```
Build the first-run Setup Wizard:

1. SetupWizardViewModel.cs:
   - CurrentStep (1, 2, or 3)
   - OpenRouterKey, ImgbbKey (string inputs)
   - IsEbaySeller (bool)
   - DefaultShippingProfile (string)
   - IsTestingOpenRouter, IsTestingImgbb (bools for spinner)
   - OpenRouterStatus, ImgbbStatus (Connected/Failed/NotTested)
   - TestOpenRouterCommand → calls ISettingsService.TestOpenRouterConnectionAsync
   - TestImgbbCommand → calls ISettingsService.TestImgBBConnectionAsync
   - NextStepCommand → validates current step, advances
   - PreviousStepCommand → goes back
   - FinishCommand → saves all settings, triggers navigation to Scan page
   - OpenSignUpCommand(string url) → opens browser

2. SetupWizardView.axaml:
   - Centered card/panel layout
   - Step indicator ("Step 1 of 3")
   - Step 1: OpenRouter explanation + sign up link + key input + Test Connection button
   - Step 2: ImgBB explanation + sign up link + key input + Test Connection button
   - Step 3: Preferences (eBay toggle, shipping profile dropdown)
   - Back/Next/Finish buttons based on step
   - Status indicators: ✅ Connected, ❌ Failed, ⏳ Testing...

The wizard should appear as the main content (not a dialog) when HasValidConfig() returns false.
After Finish, save settings and navigate to Scan page.
```

### Step 12: Settings View

**Prompt:**
```
Build the Settings page:

1. SettingsViewModel.cs:
   - All AppSettings properties as observable (loaded on init)
   - MaskedOpenRouterKey, MaskedImgbbKey (show last 4 chars only)
   - ChangeOpenRouterKeyCommand → shows input, tests, saves
   - ChangeImgbbKeyCommand → same
   - TestConnectionsCommand → tests both
   - OpenDataFolderCommand → opens file explorer to app data dir
   - BackupDataCommand → copies cards.db to user-chosen location
   - ClearAllDataCommand → confirms, then deletes DB + images
   - SavePreferencesCommand → saves non-key settings
   - CardCount (loaded from repository)

2. SettingsView.axaml:
   - API Connections section: masked keys + status + Change buttons
   - Preferences section: eBay toggle, shipping profile, default condition
   - Financial Settings: fee percentages, shipping costs, staleness threshold
   - Your Data section: card count, DB path, Open Folder / Backup / Clear buttons
   - Clear All Data has a confirmation dialog
```

---

## Testing Each Module

### Test Settings
```bash
dotnet run --project CardLister.App
# Should create config.json directory, show setup wizard
```

### Test Database
```bash
dotnet ef database update --project CardLister.Infrastructure --startup-project CardLister.App
# Should create cards.db
```

### Test Scanner
```csharp
// In a test project or scratch console:
var scanner = new OpenRouterScannerService(httpClient, settings);
var card = await scanner.ScanCardAsync("test_card.jpg");
Console.WriteLine($"{card.PlayerName} - {card.Year} {card.Brand}");
```

### Test Pricer
```csharp
var pricer = new PricerService(settings);
var card = new Card { Year = 2023, Brand = "Prizm", PlayerName = "Justin Jefferson" };
Console.WriteLine(pricer.BuildEbaySoldUrl(card));
Console.WriteLine(pricer.SuggestPrice(15.00m, card));
```

### Test Exporter
```csharp
var exporter = new CsvExportService();
var card = new Card
{
    Year = 2023, Manufacturer = "Panini", Brand = "Prizm",
    PlayerName = "Justin Jefferson", ParallelName = "Silver", CardNumber = "88"
};
Console.WriteLine(exporter.GenerateTitle(card));
// Expected: "2023 Panini Prizm Justin Jefferson Silver #88"
```

---

## Full Workflow Test

1. Launch app → Setup Wizard appears
2. Enter OpenRouter key → Test Connection → ✅
3. Enter ImgBB key → Test Connection → ✅
4. Set preferences → Finish
5. Scan tab → drop card image → Scan Card → review → Save
6. My Cards tab → see card in list
7. Price tab → Open Terapeak → enter value → Save & Next
8. Export tab → Upload Images → Download CSV
9. Open CSV → verify Whatnot format

---

## Iteration Tips for Claude Code

1. **Build one feature at a time** — get ScanView working before touching InventoryView
2. **ViewModel first, View second** — write the VM logic, then the XAML bindings
3. **Test ViewModels without UI** — they don't depend on Avalonia
4. **Keep Views dumb** — if you're writing C# in .axaml.cs, move it to the ViewModel
5. **Use source generators** — `[ObservableProperty]` and `[RelayCommand]` save tons of boilerplate
6. **Reference the planning docs** — paste relevant sections from this project into prompts
7. **Run `dotnet build` frequently** — catch type errors early
8. **One prompt per feature** — focused prompts get better results

---

## Common Issues

### Avalonia
- **View not found:** Check ViewLocator namespace mapping matches project structure
- **Binding not working:** Ensure property uses `[ObservableProperty]` or raises PropertyChanged
- **Command not firing:** Check `CanExecute` logic, ensure it's re-evaluated (call `OnPropertyChanged`)
- **Drag & drop not working:** Need `DragDrop.AllowDrop="True"` on the target control

### OpenRouter API
- "Invalid API key" → Check settings service is loading the key correctly
- Model not responding → Try different model ID (e.g., "anthropic/claude-3.5-sonnet")
- JSON parse fails → Strip markdown code blocks from response before deserializing

### EF Core
- "No migrations" → Run `dotnet ef migrations add InitialCreate` from Infrastructure project
- "Table not found" → Ensure `context.Database.Migrate()` runs on startup
- "Decimal precision" → Configure in OnModelCreating with `.HasColumnType("decimal(18,2)")`

### CSV Export
- Category/subcategory must match Whatnot's exact values
- Price must be decimal without $ symbol
- Images must be uploaded first (URLs populated)

---

## Architecture Quick Reference

```
User clicks button in View (XAML)
    → Command fires on ViewModel (C#, CommunityToolkit.Mvvm)
        → ViewModel calls Service (injected interface)
            → Service does work (API call, DB query, file I/O)
        → ViewModel updates ObservableProperty
    → View automatically updates via data binding
```

**The golden rule:** ViewModels never reference Views. Views never contain business logic. Services never reference either.
