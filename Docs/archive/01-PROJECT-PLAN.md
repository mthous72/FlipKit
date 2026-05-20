# Sports Card Lister for Whatnot — Avalonia MVVM Edition

## Current Status (February 2026)

**🎉 MVP Complete (~80-90%)** — FlipKit is a fully functional desktop application with end-to-end workflow from scanning to sale tracking.

**Latest Milestone:** Working on `feature/bulk-scan` branch to add batch scanning capabilities.

**What Works:**
- ✅ AI-powered card scanning (11 free vision models)
- ✅ Variation verification with checklist database
- ✅ Inventory management with advanced filtering
- ✅ Pricing research integration
- ✅ Whatnot CSV export with image hosting
- ✅ Sales and financial reporting
- ✅ Graded card support
- ✅ Checklist learning system

**What's Next:**
- Complete bulk scanning feature
- 3-project architecture refactor
- Unit and integration tests
- Automated price scraping
- Dark theme support

See "Development Phases" section below for detailed status.

---

## Project Goal

Build a cross-platform desktop application using **C# / .NET 8+ with Avalonia UI and the MVVM pattern** that:
1. Scans sports card images using AI vision (via OpenRouter)
2. Extracts card details (player, year, set, parallel, etc.)
3. Helps research pricing (eBay/Terapeak comps)
4. Stores inventory in a local SQLite database
5. Exports Whatnot-compatible CSV files for bulk listing
6. Tracks cost basis, profit, and price staleness for IRS compliance

**Target inventory size:** 50–150 football, baseball, and basketball singles

---

## Why Avalonia + MVVM

| Factor | Avalonia + MVVM | NiceGUI (Python + Browser) |
|--------|-----------------|---------------------------|
| Native desktop experience | ✅ True native window, no browser needed | ❌ Opens in browser tab |
| Cross-platform | ✅ Windows, macOS, Linux from one codebase | ✅ Same code everywhere |
| Performance | ✅ Compiled, fast startup | ⚠️ Python interpreter + browser overhead |
| Distribution | ✅ Single self-contained executable | ❌ Requires Python install |
| Offline capable | ✅ Full UI works offline | ✅ Similar (both need internet for APIs) |
| MVVM testability | ✅ ViewModels unit-testable without UI | ❌ No formal pattern |
| Type safety | ✅ C# compile-time checks | ❌ Python runtime errors |
| Modern UI | ✅ XAML with styles, animations, templates | ✅ Quasar/Vue components |
| Community/ecosystem | ✅ Large .NET ecosystem, NuGet packages | ⚠️ Smaller NiceGUI community |
| Claude Code compatible | ✅ Designed to be built iteratively | ✅ Same |

**Key advantages of this approach:**
- **No Python/browser dependency** — user double-clicks one .exe and it runs
- **MVVM separation** — ViewModels contain all logic, Views are pure XAML, Services handle I/O
- **Reactive UI** — CommunityToolkit.Mvvm for observable properties and commands
- **Local-first** — all data stays on the user's machine
- **Professional feel** — native window chrome, system tray potential, drag & drop

---

## High-Level Workflow

```
┌─────────────────┐
│  1. CAPTURE     │  Take photo of card front (+ optional back)
│     Card Image  │  Drag & drop or browse into app
└────────┬────────┘
         ▼
┌─────────────────┐
│  2. ANALYZE     │  Send image to OpenRouter (Claude/GPT-4o vision)
│     with AI     │  Extract structured card data as JSON
└────────┬────────┘
         ▼
┌─────────────────┐
│  3. REVIEW      │  Show extracted data in editable form
│     & Store     │  Save to SQLite database via EF Core
└────────┬────────┘
         ▼
┌─────────────────┐
│  4. PRICE       │  Open Terapeak/eBay sold searches in browser
│     Research    │  User enters estimated value → suggested list price
└────────┬────────┘
         ▼
┌─────────────────┐
│  5. UPLOAD      │  Upload card images to ImgBB (free, public URLs)
│     Images      │  Store URLs in database
└────────┬────────┘
         ▼
┌─────────────────┐
│  6. EXPORT      │  Generate Whatnot CSV with all required columns
│     CSV         │  Upload to Whatnot Seller Hub → publish
└─────────────────┘
```

---

## Tech Stack

| Component | Choice | Notes |
|-----------|--------|-------|
| Language | C# / .NET 8+ | Modern, cross-platform, strongly typed |
| UI Framework | Avalonia UI 11+ | Cross-platform XAML-based UI |
| Architecture | MVVM | CommunityToolkit.Mvvm for source generators |
| Database | SQLite via EF Core | Entity Framework Core with SQLite provider |
| AI Vision | OpenRouter API | Access to Claude, GPT-4o, Gemini via `HttpClient` |
| Image Hosting | ImgBB API | Free, returns public URLs |
| Price Research | Terapeak / eBay (manual) | Opens pre-filled URLs in system browser |
| Output | CSV | CsvHelper library for Whatnot bulk upload format |
| DI Container | Microsoft.Extensions.DependencyInjection | Standard .NET DI |
| Configuration | `appsettings.json` + user `config.json` | API keys stored locally |
| Logging | Microsoft.Extensions.Logging + Serilog | File-based logging |

---

## Solution Structure

```
FlipKit/
├── FlipKit.sln
│
├── src/
│   ├── FlipKit.App/                    # Avalonia application entry point
│   │   ├── App.axaml                      # Application resources & styles
│   │   ├── App.axaml.cs                   # Startup, DI container setup
│   │   ├── Program.cs                     # Main entry point
│   │   ├── ViewLocator.cs                 # Resolves Views from ViewModels
│   │   │
│   │   ├── Views/                         # XAML views (no code-behind logic)
│   │   │   ├── MainWindow.axaml           # Shell with navigation sidebar
│   │   │   ├── ScanView.axaml             # Card scanning page
│   │   │   ├── InventoryView.axaml        # Card list/grid with filtering
│   │   │   ├── PricingView.axaml          # Pricing research page
│   │   │   ├── ExportView.axaml           # CSV export page
│   │   │   ├── SettingsView.axaml         # API keys, preferences
│   │   │   ├── SetupWizardView.axaml      # First-time setup wizard
│   │   │   ├── CardDetailView.axaml       # Card edit form (used in scan & inventory)
│   │   │   └── RepriceView.axaml          # Stale price repricing workflow
│   │   │
│   │   ├── Styles/                        # Global styles and themes
│   │   │   ├── AppStyles.axaml            # Colors, fonts, shared styles
│   │   │   └── Controls.axaml             # Custom control templates
│   │   │
│   │   ├── Assets/                        # Icons, images, fonts
│   │   │   └── logo.png
│   │   │
│   │   └── Converters/                    # Value converters for XAML bindings
│   │       ├── PriceAgeToColorConverter.cs     # 🟢🟡🔴 logic
│   │       ├── StatusToBadgeConverter.cs
│   │       └── CurrencyFormatConverter.cs
│   │   # NOTE: this is the original planning doc. The actual converter set
│   │   # in FlipKit.Desktop/Converters/ has evolved — see that directory
│   │   # for the current list. BoolToVisibilityConverter was deleted in
│   │   # Phase 3 (no XAML reference found).
│   │
│   ├── FlipKit.Core/                   # ViewModels + business logic (no UI references)
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs     # Navigation, active page tracking
│   │   │   ├── ScanViewModel.cs           # Image upload + AI scan + save
│   │   │   ├── InventoryViewModel.cs      # Card list, filtering, search, bulk actions
│   │   │   ├── PricingViewModel.cs        # Price research, fee calc, save & next
│   │   │   ├── ExportViewModel.cs         # Image upload + CSV generation
│   │   │   ├── SettingsViewModel.cs       # API key management, preferences
│   │   │   ├── SetupWizardViewModel.cs    # First-run wizard logic
│   │   │   ├── CardDetailViewModel.cs     # Shared card edit form logic
│   │   │   └── RepriceViewModel.cs        # Stale card repricing
│   │   │
│   │   ├── Models/                        # Domain entities
│   │   │   ├── Card.cs                    # Card entity (maps to DB)
│   │   │   ├── PriceHistory.cs            # Price change tracking
│   │   │   ├── AppSettings.cs             # User configuration model
│   │   │   └── Enums/
│   │   │       ├── CardStatus.cs          # Draft, Priced, Ready, Listed, Sold
│   │   │       ├── Sport.cs               # Football, Baseball, Basketball
│   │   │       └── CostSource.cs          # LCS, Online, Break, Trade, Pack, etc.
│   │   │
│   │   ├── Services/                      # Abstractions (interfaces)
│   │   │   ├── ICardRepository.cs         # CRUD for cards
│   │   │   ├── IScannerService.cs         # AI vision scanning
│   │   │   ├── IPricerService.cs          # URL generation, fee calculation
│   │   │   ├── IImageUploadService.cs     # ImgBB upload
│   │   │   ├── IExportService.cs          # CSV generation
│   │   │   ├── ISettingsService.cs        # Load/save user config
│   │   │   ├── IBrowserService.cs         # Open URLs in system browser
│   │   │   └── IFileDialogService.cs      # Native file open/save dialogs
│   │   │
│   │   └── Helpers/
│   │       ├── PriceCalculator.cs         # Whatnot fee math, net profit
│   │       ├── TitleGenerator.cs          # Whatnot listing title format
│   │       └── PriceAgeHelper.cs          # Fresh/Aging/Stale logic
│   │
│   └── FlipKit.Infrastructure/         # Concrete service implementations
│       ├── Data/
│       │   ├── FlipKitDbContext.cs      # EF Core DbContext
│       │   ├── CardRepository.cs          # ICardRepository implementation
│       │   └── Migrations/               # EF Core migrations
│       │
│       ├── Services/
│       │   ├── OpenRouterScannerService.cs    # IScannerService → OpenRouter API
│       │   ├── ImgBBUploadService.cs          # IImageUploadService → ImgBB API
│       │   ├── PricerService.cs               # IPricerService → URL builders + math
│       │   ├── CsvExportService.cs            # IExportService → CsvHelper
│       │   ├── JsonSettingsService.cs         # ISettingsService → config.json
│       │   ├── SystemBrowserService.cs        # IBrowserService → Process.Start
│       │   └── AvaloniaFileDialogService.cs   # IFileDialogService → native dialogs
│       │
│       └── ApiModels/
│           ├── OpenRouterRequest.cs        # API request/response DTOs
│           ├── OpenRouterResponse.cs
│           ├── ImgBBRequest.cs
│           └── ImgBBResponse.cs
│
├── tests/
│   ├── FlipKit.Core.Tests/             # ViewModel + business logic unit tests
│   └── FlipKit.Infrastructure.Tests/   # Service integration tests
│
├── images/                                # Default local card image storage
│   ├── front/
│   └── back/
│
├── exports/                               # Generated CSV files
│
└── docs/                                  # Planning documents (this folder)
```

---

## MVVM Architecture Explained

### The Pattern

```
┌──────────────────────────────────────────────────────────────┐
│                        VIEW (XAML)                            │
│  Avalonia XAML files — UI layout, data templates, styles      │
│  Binds to ViewModel properties and commands                  │
│  Zero business logic in code-behind                          │
└──────────────────────┬───────────────────────────────────────┘
                       │ Data Binding (OneWay, TwoWay, Commands)
                       ▼
┌──────────────────────────────────────────────────────────────┐
│                    VIEWMODEL (C#)                             │
│  Observable properties (CommunityToolkit.Mvvm)               │
│  RelayCommands for user actions                              │
│  Orchestrates service calls, manages UI state                │
│  No references to Avalonia or any View types                 │
└──────────────────────┬───────────────────────────────────────┘
                       │ Dependency Injection (interfaces)
                       ▼
┌──────────────────────────────────────────────────────────────┐
│                MODEL + SERVICES (C#)                          │
│  Card entity, PriceHistory, AppSettings                      │
│  ICardRepository, IScannerService, IPricerService, etc.      │
│  EF Core DbContext, HttpClient, CsvHelper                    │
└──────────────────────────────────────────────────────────────┘
```

### Key MVVM Rules for This Project

1. **Views** contain only XAML and minimal code-behind (event routing at most)
2. **ViewModels** use `[ObservableProperty]` and `[RelayCommand]` source generators
3. **Services** are injected via constructor injection (interfaces only in Core)
4. **Navigation** is managed by `MainWindowViewModel` swapping the active ViewModel
5. **No static state** — everything flows through DI
6. **Async commands** — all I/O operations (API calls, DB queries, file dialogs) use `async Task`

### Example ViewModel Pattern

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ScanViewModel : ObservableObject
{
    private readonly IScannerService _scanner;
    private readonly ICardRepository _cardRepo;
    private readonly IFileDialogService _fileDialog;

    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private CardDetailViewModel? _scannedCard;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string? _errorMessage;

    public ScanViewModel(IScannerService scanner, ICardRepository cardRepo, IFileDialogService fileDialog)
    {
        _scanner = scanner;
        _cardRepo = cardRepo;
        _fileDialog = fileDialog;
    }

    [RelayCommand]
    private async Task BrowseImageAsync()
    {
        var path = await _fileDialog.OpenImageFileAsync();
        if (path != null) ImagePath = path;
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanCardAsync()
    {
        IsScanning = true;
        ErrorMessage = null;
        try
        {
            var result = await _scanner.ScanCardAsync(ImagePath!);
            ScannedCard = new CardDetailViewModel(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't scan this card: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool CanScan => !string.IsNullOrEmpty(ImagePath) && !IsScanning;

    [RelayCommand]
    private async Task SaveCardAsync()
    {
        if (ScannedCard == null) return;
        var card = ScannedCard.ToCard();
        card.ImagePathFront = ImagePath;
        await _cardRepo.InsertCardAsync(card);
        // Reset for next scan
        ImagePath = null;
        ScannedCard = null;
    }
}
```

---

## Development Phases

### Phase 1: Foundation & Skeleton ✅ COMPLETE
- [x] Create solution structure (single project - refactor to 3 projects planned)
- [x] Set up dependency injection in `App.axaml.cs`
- [x] Create `MainWindow` with sidebar navigation
- [x] Implement `ViewLocator` for ViewModel-first navigation
- [x] Create EF Core DbContext with Card entity + SQLite
- [x] Run initial migration to create `cards.db`
- [x] Implement `JsonSettingsService` for settings persistence

### Phase 2: Card Scanning ✅ COMPLETE
- [x] Build `ScanView` — image drop zone + preview + editable form + save button
- [x] Implement `OpenRouterScannerService` — 11 free vision models supported
- [x] Create `CardDetailViewModel` — shared form for card fields
- [x] Parse JSON response → map to `Card` entity (with markdown stripping)
- [x] Wire up `ScanViewModel` — browse image → scan → review → save flow
- [x] Add variation verification against checklist database
- [x] Implement fuzzy matching for player names and parallels
- [x] Add confidence scoring and conflict resolution

### Phase 3: Inventory Management ✅ COMPLETE
- [x] Build `InventoryView` — DataGrid with card list
- [x] Implement filters: sport, status, search text, price age
- [x] Click row → open `EditCardView` for editing
- [x] Bulk actions: select multiple → delete, mark ready, mark sold
- [x] Price age indicators (🟢🟡🔴) via `PriceAgeToColorConverter`
- [x] CSV export from inventory
- [x] Image upload status tracking

### Phase 4: Pricing ✅ COMPLETE
- [x] Build `PricingView` — card selector + research links + price input
- [x] Implement `PricerService` — Terapeak URL builder, eBay sold URL builder
- [x] Open URLs in system browser via `SystemBrowserService`
- [x] Fee calculator: market value → suggested Whatnot price
- [x] Save & Next workflow for batch pricing
- [x] Cost basis fields: acquisition cost, source, date, notes
- [x] Market value and listing price fields

### Phase 5: Image Upload & CSV Export ✅ COMPLETE
- [x] Implement `ImgBBUploadService` — upload images, store public URLs
- [x] Build `ExportView` — filter ready cards, preview, batch upload, download CSV
- [x] Implement `CsvExportService` — map Card fields to Whatnot CSV columns
- [x] Validate required fields before export
- [x] Progress bar for batch image upload
- [x] Whatnot category/subcategory mapping

### Phase 6: Setup Wizard & Settings ✅ COMPLETE
- [x] Build `SetupWizardView` — 3-step guided setup
- [x] "Test Connection" buttons that validate API keys
- [x] Build `SettingsView` — change keys, preferences, data management
- [x] Auto-detect first run → show wizard
- [x] Support for custom grading companies

### Phase 7: Price Re-checking & Financial Tracking ✅ COMPLETE
- [x] Build `RepriceView` — stale card queue with skip/keep/update options
- [x] Price history table + `PriceHistory` entity
- [x] "Mark as Sold" workflow — sale price, fees, shipping → net profit calculation
- [x] Build `ReportsView` — revenue, costs, profit by date range
- [x] Financial summary with monthly breakdown
- [x] Top sellers report
- [x] Sales tracking with date filtering

### Phase 8: Advanced Features ✅ COMPLETE
- [x] Graded card support (PSA, BGS, CGC, CCG, SGC)
- [x] Auto-grade detection from AI scanning
- [x] Checklist learning system (learns from saved cards)
- [x] Checklist CSV import
- [x] `ChecklistManagerView` for viewing and editing checklists
- [x] Missing checklist tracking
- [x] Seed data system with embedded JSON

### Phase 9: Bulk Scanning 🚧 IN PROGRESS (feature/bulk-scan)
- [x] Build `BulkScanView` — multi-card grid layout
- [x] Front/back image pairing
- [x] Progress tracking for batch operations
- [x] Rate limiting for free-tier models (4-second delays)
- [ ] Finalize UI polish and error handling
- [ ] Complete testing and merge to master

### Phase 10: Polish & Distribution ⏳ PLANNED
- [ ] Theming — dark mode support
- [ ] Enhanced error handling and retry logic
- [ ] Loading states optimization
- [ ] Keyboard shortcuts (Ctrl+N, Ctrl+S, Ctrl+F, arrow keys)
- [ ] Publish as self-contained executable (Windows x64, macOS arm64/x64, Linux x64)
- [ ] Create installer or single-file deploy
- [ ] Write end-user documentation

### Phase 11: Architecture Refactor ⏳ PLANNED
- [ ] Split into 3 projects: App, Core, Infrastructure
- [ ] Move ViewModels to Core
- [ ] Move service interfaces to Core
- [ ] Move implementations to Infrastructure
- [ ] Update ViewLocator for new namespace structure
- [ ] Add unit test projects

### Phase 12: Testing ⏳ PLANNED
- [ ] Unit tests for ViewModels
- [ ] Unit tests for services
- [ ] Integration tests for database
- [ ] Integration tests for API calls
- [ ] UI automation tests

---

## NuGet Packages

```xml
<!-- FlipKit.App -->
<PackageReference Include="Avalonia" Version="11.*" />
<PackageReference Include="Avalonia.Desktop" Version="11.*" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="11.*" />
<PackageReference Include="Avalonia.Diagnostics" Version="11.*" Condition="'$(Configuration)' == 'Debug'" />

<!-- FlipKit.Core -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />

<!-- FlipKit.Infrastructure -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
<PackageReference Include="CsvHelper" Version="33.*" />
<PackageReference Include="Serilog.Extensions.Logging" Version="8.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.*" />
<PackageReference Include="System.Text.Json" Version="8.*" />
```

---

## Database (EF Core + SQLite)

### Card Entity

```csharp
public class Card
{
    public int Id { get; set; }

    // Card Identity
    public string? PlayerName { get; set; }
    public int? Year { get; set; }
    public string? Manufacturer { get; set; }     // Panini, Topps, etc.
    public string? Brand { get; set; }             // Prizm, Select, Chrome, etc.
    public string? CardNumber { get; set; }
    public string? Team { get; set; }
    public Sport Sport { get; set; }

    // Card Attributes
    public string? ParallelName { get; set; }      // Silver, Gold, etc.
    public bool IsRookie { get; set; }
    public bool IsAutograph { get; set; }
    public bool IsRelic { get; set; }
    public bool IsNumbered { get; set; }
    public string? NumberedTo { get; set; }         // "/99", "/25"
    public string? Condition { get; set; }          // Near Mint, etc.

    // Images
    public string? ImagePathFront { get; set; }    // Local file path
    public string? ImagePathBack { get; set; }
    public string? ImageUrl1 { get; set; }         // ImgBB public URL
    public string? ImageUrl2 { get; set; }

    // Pricing
    public decimal? EstimatedValue { get; set; }   // Market value from comps
    public decimal? ListingPrice { get; set; }     // Your asking price
    public DateTime? PriceDate { get; set; }       // When price was last researched
    public int PriceCheckCount { get; set; }

    // Acquisition / Cost Basis
    public decimal? CostBasis { get; set; }
    public CostSource? CostSource { get; set; }
    public DateTime? CostDate { get; set; }
    public string? CostNotes { get; set; }

    // Sale Information
    public decimal? SalePrice { get; set; }
    public DateTime? SaleDate { get; set; }
    public string? SalePlatform { get; set; }
    public decimal? FeesPaid { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? NetProfit { get; set; }

    // Status & Metadata
    public CardStatus Status { get; set; } = CardStatus.Draft;
    public string? ShippingProfile { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}
```

### Supporting Entities

```csharp
public class PriceHistory
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public decimal? EstimatedValue { get; set; }
    public decimal? ListingPrice { get; set; }
    public string? PriceSource { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Card Card { get; set; } = null!;
}

public enum CardStatus { Draft, Priced, Ready, Listed, Sold }
public enum Sport { Football, Baseball, Basketball }
public enum CostSource { LCS, Online, Break, Trade, Pack, Gift, Other }
```

---

## User Interface Design

### Navigation

The app uses a **sidebar navigation** pattern — always visible, collapsible to icons on smaller windows:

```
┌──────┬────────────────────────────────────────────────────────────┐
│      │  🃏 Card Lister                              [⚙ Settings] │
│ 📸   ├────────────────────────────────────────────────────────────┤
│ Scan │                                                            │
│      │                                                            │
│ 📋   │                    (Active Page Content)                   │
│ Cards│                                                            │
│ (47) │                                                            │
│      │                                                            │
│ 💰   │                                                            │
│ Price│                                                            │
│      │                                                            │
│ 📤   │                                                            │
│Export│                                                            │
│ (12) │                                                            │
│      │                                                            │
└──────┴────────────────────────────────────────────────────────────┘
```

### Design Philosophy

- **KISS** — Keep It Simple, Stupid. One obvious way to do each task.
- **No Norman Doors** — Buttons do what they look like they do.
- **Progressive disclosure** — Show basics first, details on demand.
- **User owns their data** — Everything stored locally, user provides own API keys.
- **Fluent theme** — Avalonia's built-in Fluent theme for modern look.

### Pages

1. **Scan** — Drop/browse card image → AI extracts details → Review form → Save
2. **My Cards** — DataGrid with all cards, filter by status/sport/search, click to edit
3. **Price** — Select card → open Terapeak/eBay in browser → enter value → suggested price
4. **Export** — Filter ready cards → batch upload images → download Whatnot CSV
5. **Settings** — API keys, preferences, data management, financial settings

### First-Time Setup Wizard

On first launch (no `config.json` found), a modal wizard walks through:
1. **OpenRouter API Key** — link to sign up, paste field, Test Connection button
2. **ImgBB API Key** — link to sign up, paste field, Test Connection button
3. **Preferences** — eBay seller toggle, default shipping, default condition

---

## Distribution: Self-Contained Executable

No Python. No browser. No runtime install. Just one file (or folder).

### Publish Commands

```bash
# Windows (single file, self-contained)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

### What the user gets

```
FlipKit/
├── FlipKit.exe          ← Double-click to run (Windows)
├── cards.db                ← Created on first run
├── config.json             ← Created by setup wizard
├── images/                 ← Local card photos
├── exports/                ← Generated CSVs
└── logs/                   ← App logs for troubleshooting
```

**No install step. No command prompt. No browser.**

---

## API Keys Needed

| Service | Purpose | Cost | Required? |
|---------|---------|------|-----------|
| **OpenRouter** | Card image analysis | Pay-as-you-go (~$0.01-0.02/card) | ✅ Yes |
| **ImgBB** | Image hosting for Whatnot | Free | ✅ Yes |
| **eBay Seller Account** | Terapeak access for pricing | Free (if selling on eBay) | ✅ Recommended |
| **eBay Developer** | Competitive price check API | Free | ❌ Optional |

---

## Documents in This Plan

| # | Document | Purpose |
|---|----------|---------|
| 00 | PROGRAM-OVERVIEW.md | Visual preview of the app |
| 01 | PROJECT-PLAN.md | This file — overview and roadmap |
| 02 | DATABASE-SCHEMA.md | EF Core entities and SQLite schema |
| 03 | OPENROUTER-INTEGRATION.md | AI vision for card scanning |
| 04 | WHATNOT-CSV-FORMAT.md | Export format for Whatnot |
| 05 | PRICING-RESEARCH.md | Terapeak + eBay comp research |
| 06 | IMAGE-HOSTING.md | ImgBB for public image URLs |
| 07 | CLAUDE-CODE-GUIDE.md | Step-by-step build instructions for Claude Code |
| 08 | CARD-TERMINOLOGY.md | Sports card reference guide |
| 09 | EBAY-API.md | Optional competitive pricing check |
| 10 | GUI-ARCHITECTURE.md | Avalonia MVVM app architecture & views |
| 11 | UX-DESIGN.md | User experience, screens, KISS principles |
| 12 | INSTALL-GUIDE.md | Non-technical user installation guide |
| 13 | INVENTORY-TRACKING.md | Price re-checking & profit/tax tracking |

---

## Next Steps

1. Review this plan for overall architecture and scope
2. Review `02-DATABASE-SCHEMA.md` for the EF Core data model
3. Review `03-OPENROUTER-INTEGRATION.md` for the vision API setup
4. Review `04-WHATNOT-CSV-FORMAT.md` for export requirements
5. Review `07-CLAUDE-CODE-GUIDE.md` for step-by-step Claude Code prompts
6. Review `10-GUI-ARCHITECTURE.md` for detailed Avalonia MVVM structure
7. Start building in Claude Code: `dotnet new sln`, create projects, wire up DI
