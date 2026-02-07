# Working with CardLister in Claude Code

## Overview

This guide helps you work with the existing CardLister codebase using Claude Code. CardLister is ~80-90% complete MVP with full end-to-end functionality. Use this guide to understand the architecture, add features, and troubleshoot issues.

**Current State:** Fully functional single-project Avalonia MVVM application with 14 ViewModels, 12 Views, 11 services, SQLite database, and complete workflow from scanning to sales tracking.

---

## Prerequisites

1. **.NET 8 SDK** installed (`dotnet --version` should show 8.0+)
2. **Claude Code** installed (`npm install -g @anthropic-ai/claude-code`)
3. **API Keys:**
   - OpenRouter: https://openrouter.ai/keys
   - ImgBB: https://api.imgbb.com/

---

## Project Structure

### Current Architecture (Single Project)

```
CardLister/
├── CardLister.csproj          # Single project with all code
├── Program.cs                 # Entry point
├── App.axaml.cs               # DI setup, logging, database init
├── ViewLocator.cs             # ViewModel → View mapping
│
├── ViewModels/                # 14 ViewModels
│   ├── MainWindowViewModel.cs         # Navigation
│   ├── ScanViewModel.cs               # Single-card scanning
│   ├── BulkScanViewModel.cs           # Multi-card scanning (WIP)
│   ├── InventoryViewModel.cs          # Card management
│   ├── EditCardViewModel.cs           # Card editing
│   ├── PricingViewModel.cs            # Price research
│   ├── RepriceViewModel.cs            # Stale card repricing
│   ├── ExportViewModel.cs             # Whatnot export
│   ├── ReportsViewModel.cs            # Financial reports
│   ├── ChecklistManagerViewModel.cs   # Checklist editing
│   ├── SettingsViewModel.cs           # App settings
│   ├── SetupWizardViewModel.cs        # First-run setup
│   ├── CardDetailViewModel.cs         # Shared form logic
│   └── ViewModelBase.cs               # Base class
│
├── Views/                     # 12 XAML Views
│   ├── MainWindow.axaml              # Shell with sidebar
│   ├── ScanView.axaml                # Scanning page
│   ├── BulkScanView.axaml            # Bulk scanning
│   ├── InventoryView.axaml           # Card list
│   ├── EditCardView.axaml            # Edit form
│   ├── PricingView.axaml             # Pricing
│   ├── RepriceView.axaml             # Reprice
│   ├── ExportView.axaml              # Export
│   ├── ReportsView.axaml             # Reports
│   ├── ChecklistManagerView.axaml    # Checklists
│   ├── SettingsView.axaml            # Settings
│   └── SetupWizardView.axaml         # Setup
│
├── Models/                    # Domain entities
│   ├── Card.cs                       # 88 fields
│   ├── PriceHistory.cs               # Price tracking
│   ├── SetChecklist.cs               # Verification data
│   ├── MissingChecklist.cs           # Learning tracker
│   ├── AppSettings.cs                # User config
│   └── Enums/                        # CardStatus, Sport, CostSource, etc.
│
├── Services/                  # 11 services (interface + impl)
│   ├── ICardRepository.cs + CardRepository.cs
│   ├── IScannerService.cs + OpenRouterScannerService.cs + MockScannerService.cs
│   ├── IVariationVerifier.cs + VariationVerifierService.cs
│   ├── IChecklistLearningService.cs + ChecklistLearningService.cs
│   ├── IPricerService.cs + PricerService.cs
│   ├── IImageUploadService.cs + ImgBBUploadService.cs
│   ├── IExportService.cs + CsvExportService.cs
│   ├── ISettingsService.cs + JsonSettingsService.cs
│   ├── IBrowserService.cs + SystemBrowserService.cs
│   └── IFileDialogService.cs + AvaloniaFileDialogService.cs
│
├── Data/                      # EF Core & seeding
│   ├── CardListerDbContext.cs        # DbContext
│   ├── Migrations/                   # EF migrations
│   ├── SchemaUpdater.cs              # Column additions
│   └── ChecklistSeeder.cs            # Seed data loader
│
├── Converters/                # 8 XAML value converters
│   ├── BoolToVisibilityConverter.cs
│   ├── CardStatusToColorConverter.cs
│   ├── ConfidenceToColorConverter.cs
│   ├── CurrencyConverter.cs
│   ├── DateAgeConverter.cs
│   ├── InverseBoolConverter.cs
│   ├── NullToVisibilityConverter.cs
│   └── PriceAgeToColorConverter.cs
│
├── Helpers/
│   ├── FuzzyMatcher.cs               # String matching
│   ├── PriceCalculator.cs            # Fee calculations
│   └── ConfidenceScorer.cs           # Verification scoring
│
├── ApiModels/
│   ├── OpenRouterRequest.cs
│   ├── OpenRouterResponse.cs
│   ├── ScanResult.cs
│   ├── ImgBBRequest.cs
│   └── ImgBBResponse.cs
│
├── Styles/
│   └── AppStyles.axaml               # Global styles
│
└── Assets/
    ├── Icons/
    └── SeedData/                     # Embedded JSON checklists
        ├── football_checklists.json
        ├── baseball_checklists.json
        └── basketball_checklists.json
```

## Adding New Features

### Example: Adding a New Page

**Prompt for Claude Code:**
```
Add a new "Collection Stats" page to CardLister that shows:

1. Create ViewModel: ViewModels/CollectionStatsViewModel.cs
   - Inherit from ViewModelBase
   - Use [ObservableProperty] for stats properties
   - Inject ICardRepository
   - Load stats in constructor or LoadDataAsync command

2. Create View: Views/CollectionStatsView.axaml
   - Create XAML layout with stats display
   - Bind to ViewModel properties
   - Use existing converters for formatting

3. Register in DI: App.axaml.cs
   - Add services.AddTransient<CollectionStatsViewModel>();

4. Add navigation: MainWindowViewModel.cs
   - Add new navigation case
   - Update MainWindow sidebar

Example ViewModel:
```csharp
public partial class CollectionStatsViewModel : ViewModelBase
{
    private readonly ICardRepository _repository;

    [ObservableProperty]
    private int _totalCards;

    [ObservableProperty]
    private decimal _totalValue;

    public CollectionStatsViewModel(ICardRepository repository)
    {
        _repository = repository;
        LoadDataAsync();
    }

    private async void LoadDataAsync()
    {
        var cards = await _repository.GetAllCardsAsync();
        TotalCards = cards.Count;
        TotalValue = cards.Sum(c => c.ListingPrice ?? 0);
    }
}
```
```

### Example: Adding a New Service

**Prompt for Claude Code:**
```
Add a new backup service to CardLister that:

1. Create interface: Services/IBackupService.cs
2. Create implementation: Services/BackupService.cs
3. Register in DI: App.axaml.cs
   - services.AddSingleton<IBackupService, BackupService>();
4. Inject into ViewModel where needed

Example:
```csharp
public interface IBackupService
{
    Task<bool> BackupDatabaseAsync(string destinationPath);
    Task<bool> RestoreFromBackupAsync(string backupPath);
}

public class BackupService : IBackupService
{
    private readonly string _dbPath;

    public BackupService()
    {
        _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CardLister", "cards.db");
    }

    public async Task<bool> BackupDatabaseAsync(string destinationPath)
    {
        try
        {
            await Task.Run(() => File.Copy(_dbPath, destinationPath, overwrite: true));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```
```

### Example: Adding a Database Column

**Prompt for Claude Code:**
```
Add a "Notes" field to the Card entity in CardLister:
1. Update Model: Models/Card.cs
   - Card already has a Notes field, but if adding a new one:
   - Add the property: `public string? NewField { get; set; }`

2. Add Migration:
   ```bash
   # If using EF Core migrations (future refactor)
   dotnet ef migrations add AddNewFieldToCard

   # Current approach: Use SchemaUpdater
   ```

3. Update SchemaUpdater: Data/SchemaUpdater.cs
   - Add ALTER TABLE command if column doesn't exist
   - Called automatically on app startup

4. Update ViewModels:
   - Add [ObservableProperty] to CardDetailViewModel
   - Update ToCard() and FromCard() methods

5. Update Views:
   - Add TextBox or control to CardDetailView.axaml
   - Bind to new property

Example SchemaUpdater addition:
```csharp
public static async Task EnsureColumnExistsAsync(CardListerDbContext context, string tableName, string columnName, string columnType)
{
    var sql = $"PRAGMA table_info({tableName})";
    var columns = await context.Database.SqlQueryRaw<TableInfo>(sql).ToListAsync();

    if (!columns.Any(c => c.name == columnName))
    {
        await context.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}");
    }
}
```
```

## Understanding the Current Codebase

### Key Architecture Patterns

**MVVM Pattern:**
- Views (XAML) bind to ViewModels (C#)
- ViewModels use CommunityToolkit.Mvvm source generators
- `[ObservableProperty]` generates INotifyPropertyChanged
- `[RelayCommand]` generates ICommand implementations

**Dependency Injection:**
- All services registered in App.axaml.cs
- ViewModels receive dependencies via constructor injection
- Services use interface-based design for testability

**Navigation:**
- MainWindowViewModel.CurrentPage holds active ViewModel
- ViewLocator resolves View from ViewModel by naming convention
- Sidebar buttons call NavigateToPage() command

### Data Flow Examples

**Scanning a Card:**
1. User drops image → ScanView updates ImagePath binding
2. User clicks "Scan Card" → ScanCardCommand executes
3. ScanViewModel calls OpenRouterScannerService.ScanCardAsync()
4. Service sends image to OpenRouter API
5. Response parsed into ScannedCardData
6. If variation verification enabled, calls VariationVerifierService
7. Results populate CardDetailViewModel properties
8. User reviews → clicks "Save" → SaveCardCommand
9. CardRepository.InsertCardAsync() saves to database
10. ChecklistLearningService updates checklist if needed

**Exporting to Whatnot:**
1. ExportViewModel loads Ready cards from repository
2. User clicks "Upload Images" → UploadImagesCommand
3. ImgBBUploadService uploads each image with progress tracking
4. Image URLs stored in database
5. User clicks "Export CSV" → ExportCsvCommand
6. CsvExportService validates cards, generates Whatnot CSV
7. File dialog saves CSV to user location

### Database Location

- **Development:** `%LOCALAPPDATA%\CardLister\cards.db`
- **Logs:** `%LOCALAPPDATA%\CardLister\logs\`
- **Settings:** `%LOCALAPPDATA%\CardLister\config.json`

### Common Tasks

**Running the app:**
```bash
dotnet run --project CardLister
```

**Building:**
```bash
dotnet build
```

**Publishing (Windows):**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**Checking current branch:**
```bash
git status
# Currently on: feature/bulk-scan
```

## Troubleshooting Guide

### Common Issues

**1. View Not Found Error**
- **Symptom:** "Could not find view for ViewModel"
- **Cause:** ViewLocator can't map ViewModel to View
- **Fix:** Ensure View name matches ViewModel name (replace "ViewModel" with "View")
  - ✅ ScanViewModel → ScanView
  - ❌ ScanViewModel → ScanningView

**2. Binding Not Updating**
- **Symptom:** UI doesn't reflect property changes
- **Cause:** Property doesn't raise PropertyChanged
- **Fix:** Use `[ObservableProperty]` on private field:
  ```csharp
  [ObservableProperty]
  private string? _playerName;  // Generates PlayerName property
  ```

**3. Command Not Firing**
- **Symptom:** Button click does nothing
- **Cause:** CanExecute returns false or command not bound
- **Fix:**
  - Check CanExecute method logic
  - Ensure dependent properties call OnPropertyChanged
  - Verify XAML binding: `Command="{Binding ScanCardCommand}"`

**4. OpenRouter API Fails**
- **Symptom:** "Invalid API key" or timeout
- **Cause:** Wrong key, rate limit, or network issue
- **Fix:**
  - Check Settings → API key is correct
  - Try different model (some have rate limits)
  - Check OpenRouter dashboard for credits
  - Review logs in `%LOCALAPPDATA%\CardLister\logs\`

**5. Database Locked**
- **Symptom:** "Database is locked" error
- **Cause:** Multiple connections or unfinished transaction
- **Fix:**
  - Close app completely and restart
  - Check for lingering processes in Task Manager
  - Delete `cards.db-wal` and `cards.db-shm` files if app not running

**6. CSV Export Validation Fails**
- **Symptom:** "Card missing required fields"
- **Cause:** Cards don't have all required Whatnot fields
- **Fix:**
  - Check ExportViewModel.ValidationIssues
  - Ensure cards have: Title, Price, At least 1 image URL
  - Update card in Inventory → Edit

**7. Fuzzy Match False Positives**
- **Symptom:** Wrong player or variation matched
- **Cause:** Similarity threshold too low
- **Fix:**
  - Adjust thresholds in VariationVerifierService
  - Player name: 0.85 (higher = stricter)
  - Parallel: 0.7
  - Or disable auto-apply and manually review

**8. Slow Performance with Many Cards**
- **Symptom:** UI freezes or slow loading
- **Cause:** Loading too much data at once
- **Fix:**
  - Add pagination to InventoryView (future enhancement)
  - Use status/sport filters to reduce dataset
  - Check for missing database indexes

### Debugging Tips

**Enable Verbose Logging:**
- Logs are in `%LOCALAPPDATA%\CardLister\logs\log-YYYYMMDD.txt`
- Serilog configured in App.axaml.cs
- Check logs for exceptions and API responses

**Check DI Registration:**
- All services must be registered in App.axaml.cs
- Missing registration = NullReferenceException at runtime

**Test Services Independently:**
- Services are interface-based, easy to test
- Can create console app to test service without UI

**Database Inspection:**
- Use DB Browser for SQLite to view cards.db
- Check schema, data, and indexes

## Reference: Service Responsibilities

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
