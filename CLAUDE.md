# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FlipKit is a C# / .NET 8 application suite for sports card sellers, consisting of:

1. **FlipKit.Desktop** - Avalonia UI 11 desktop app (Windows/Mac/Linux) with full feature set
2. **FlipKit.Web** - ASP.NET Core 8.0 MVC web app for mobile access (phone/tablet browsers)
3. **FlipKit.Core** - Shared business logic library (models, services, data access)

Both apps share a single SQLite database with WAL mode for concurrent access, enabling seamless workflow between desktop power features and mobile convenience.

**Core Features:** AI vision scanning (OpenRouter API), inventory management, pricing research (eBay/Terapeak), Whatnot CSV export, sales tracking, financial reports.

**Current State:** v3.1.0 FlipKit Hub released. Unified package with Desktop app + embedded Web and API servers. Desktop and Web both feature-complete. Servers managed from Desktop Settings UI.

## Build & Run Commands

**Desktop App (Development):**
```bash
# Restore and build
dotnet restore
dotnet build

# Run desktop app (servers auto-start if configured)
dotnet run --project FlipKit.Desktop

# Build for release
dotnet build -c Release
```

**Web App (Development - Standalone):**
```bash
# Run web app standalone (development)
cd FlipKit.Web
dotnet run

# Run with specific URLs (for local network access)
dotnet run --urls "http://0.0.0.0:5000"
```

**Build FlipKit Hub Packages (Release):**
```bash
# Build unified packages for Windows and Linux
.\build-release.ps1 -Version 3.1.0

# Output: releases/FlipKit-Hub-Windows-x64-v3.1.0.zip
#         releases/FlipKit-Hub-Linux-x64-v3.1.0.tar.gz
```

**All Projects:**
```bash
# Run tests (when test projects exist)
dotnet test

# Build entire solution
dotnet build FlipKit.sln
```

## Architecture

**Current: 3-Project Structure** (as of Phase 1 refactor):

```
FlipKit.sln
│
├── FlipKit.Core/          # Shared business logic (net8.0 class library)
│   ├── Models/                # Domain entities (Card, PriceHistory, SetChecklist, enums)
│   ├── Services/
│   │   ├── Interfaces/        # All service contracts
│   │   └── Implementations/   # UI-agnostic implementations (9 services)
│   ├── Data/                  # EF Core DbContext, migrations, seeders
│   └── Helpers/               # FuzzyMatcher, PriceCalculator, etc.
│
├── FlipKit.Desktop/       # Avalonia UI app (net8.0 WinExe)
│   ├── Views/                 # 12 XAML views
│   ├── ViewModels/            # 14 ViewModels with [ObservableProperty]
│   ├── Services/              # Platform-specific (3 services)
│   │   ├── AvaloniaFileDialogService.cs
│   │   ├── SystemBrowserService.cs
│   │   ├── JsonSettingsService.cs
│   │   └── AvaloniaNavigationService.cs
│   ├── Converters/            # 8 XAML value converters
│   ├── Styles/                # AppStyles.axaml
│   ├── Assets/                # Icons, images, seed data JSON
│   ├── ViewLocator.cs
│   ├── App.axaml.cs
│   └── Program.cs
│
└── FlipKit.Web/           # ASP.NET Core MVC (net8.0 web app)
    ├── Controllers/           # 6 controllers (Home, Inventory, Scan, Pricing, Export, Reports)
    ├── Models/                # ViewModels/DTOs for Razor views (12 files)
    ├── Views/
    │   ├── Shared/            # _Layout.cshtml navigation
    │   ├── Home/              # Dashboard
    │   ├── Inventory/         # CRUD operations
    │   ├── Scan/              # Mobile camera upload
    │   ├── Pricing/           # Research and pricing
    │   ├── Export/            # CSV generation
    │   └── Reports/           # Analytics
    ├── Services/              # Platform-specific (4 services)
    │   ├── WebFileUploadService.cs
    │   ├── JavaScriptBrowserService.cs
    │   ├── MvcNavigationService.cs
    │   └── JsonSettingsService.cs
    ├── wwwroot/               # Static files, CSS, JS
    └── Program.cs             # DI, middleware, WAL mode setup
```

### Dependency Flow

```
FlipKit.Desktop ─┐
                    ├─→ FlipKit.Core ←─ Shared database (WAL mode)
FlipKit.Web ─────┘
```

Both Desktop and Web reference Core, but **never reference each other**.

### Web App Architecture (ASP.NET Core MVC)

**Data Flow:**
```
Browser → HTTP Request → Controller → Core Services → Database/APIs → View (Razor) → HTTP Response
```

**Key Patterns:**
- **Controllers** - Handle HTTP requests, call Core services, return views
- **ViewModels (DTOs)** - Simple data transfer objects for Razor views (no ObservableObject)
- **Views (Razor)** - Server-rendered HTML with Bootstrap 5, client-side JavaScript for interactivity
- **Shared Database** - SQLite with WAL mode enables concurrent Desktop + Web access without locking
- **Platform Services** - Web-specific implementations (file upload via IFormFile, browser navigation via response headers)

**DI Service Lifetimes:**
- **Singleton** - Stateless services (ISettingsService, IScannerService, IImageUploadService)
- **Scoped** - Services that depend on DbContext (ICardRepository, IPricerService, IExportService)
- **Transient** - Not used in web app

**Mobile Optimization:**
- Camera integration via `<input accept="image/*" capture="environment">`
- Bootstrap 5 responsive design (mobile-first)
- Touch-friendly UI elements
- JavaScript real-time calculators (profit calculator in pricing)

### Desktop MVVM Data Flow

```
View (XAML) → data binding → ViewModel (C#) → DI-injected services → Data/APIs
```

- **Views** are pure XAML with declarative bindings. No business logic in code-behind.
- **ViewModels** use CommunityToolkit.Mvvm source generators: `[ObservableProperty]` for reactive properties, `[RelayCommand]` for async commands.
- **Services** are accessed via interfaces injected through constructors.
- **Navigation** is ViewModel-first: `MainWindowViewModel.CurrentPage` holds the active ViewModel; `ViewLocator` resolves the matching View.

### ViewLocator Convention

The `ViewLocator` maps ViewModel types to View types by replacing `"ViewModel"` with `"View"` in the fully qualified type name. When the planned 3-project structure is adopted, this changes to replacing `"Core.ViewModels"` with `"App.Views"`.

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.11 | Cross-platform UI framework |
| Avalonia.Themes.Fluent | 11.3.11 | Modern Fluent theme |
| CommunityToolkit.Mvvm | 8.2.1 | MVVM source generators |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.11 | SQLite database with EF Core |
| CsvHelper | 33.0.1 | CSV export for Whatnot |
| Serilog | 8.0.0 | Structured logging to file |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | DI container |
| Microsoft.Extensions.Http | 8.0.1 | HttpClient factory |
| System.Text.Json | 8.0.5 | JSON serialization |

## Important Conventions

- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`) — all types must be explicitly nullable or non-nullable.
- **Compiled bindings by default** (`<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`).
- Use `decimal` for all money fields, `DateTime` for all date fields.
- Enums stored as strings in the database.
- All I/O operations must be `async Task`.
- Avalonia `DataAnnotationsValidationPlugin` is disabled in `App.axaml.cs` to avoid conflicts with CommunityToolkit validation.

## Implementation Status

### ✅ Completed Features (Production Ready)

**Core Workflow:**
- ✅ AI vision scanning via OpenRouter (11 free models supported)
- ✅ Variation verification against checklist database with fuzzy matching
- ✅ Single-card and bulk scanning workflows
- ✅ Inventory management (CRUD, filtering, bulk operations, search)
- ✅ Pricing research with browser integration (Terapeak/eBay)
- ✅ Whatnot CSV export with validation
- ✅ ImgBB image hosting integration
- ✅ Sales tracking and profitability reports
- ✅ Financial reporting by date range

**Advanced Features:**
- ✅ Graded card support (PSA, BGS, CGC, CCG, SGC)
- ✅ Checklist learning system (improves from saved cards)
- ✅ Checklist CSV import and editing
- ✅ Price staleness tracking with visual indicators
- ✅ "Mark as Sold" workflow with profit calculation
- ✅ Setup wizard for first-run configuration
- ✅ Settings persistence (JSON)
- ✅ Logging (Serilog to file)

**Technical Implementation (Desktop):**
- ✅ 14 ViewModels with MVVM pattern
- ✅ 12 Views with Avalonia UI
- ✅ 11 services with interface-based DI
- ✅ SQLite database with EF Core (88 fields per card)
- ✅ Seed data system for checklists
- ✅ 8 custom XAML value converters
- ✅ Fuzzy matching for verification (0.85/0.7 thresholds)
- ✅ Rate limiting for free-tier AI models

**Web Application (feature/web-app-migration branch):**
- ✅ 3-project architecture (Core, Desktop, Web)
- ✅ ASP.NET Core 8.0 MVC with Bootstrap 5
- ✅ 6 controllers (Home, Inventory, Scan, Pricing, Export, Reports)
- ✅ 13 Razor views with responsive design
- ✅ Mobile camera integration for card scanning
- ✅ Shared SQLite database with WAL mode (concurrent access)
- ✅ Platform-specific service implementations
- ✅ Real-time JavaScript calculators (profit calculator)
- ✅ Full CRUD inventory management
- ✅ CSV export for Whatnot
- ✅ Sales and financial analytics

### 🚧 Current Work

- 🚧 **Web App Testing** (feature/web-app-migration branch) — Phase 3 polish, mobile testing, documentation
- 🚧 **Bulk Scan feature** (feature/bulk-scan branch) — Multi-card batch scanning with front/back pairing

### 📋 Future Roadmap

**High Priority:**
- ⏳ Web app authentication (multi-user support)
- ⏳ Unit and integration tests
- ⏳ Progressive Web App (PWA) - install web app on phone home screen
- ⏳ Real-time sync between Desktop and Web (SignalR)

**Medium Priority:**
- ⏳ Bulk scan from web interface
- ⏳ Additional export formats (eBay, COMC)
- ⏳ Performance optimizations for large inventories (1000+ cards)
- ⏳ Dark theme support (Desktop and Web)
- ⏳ Cloud sync / backup

**Low Priority:**
- ⏳ Automated price scraping (replace manual browser lookup)
- ⏳ Barcode scanning
- ⏳ Price alerts/notifications

See `Docs/17-FUTURE-ROADMAP.md` for detailed planning.

## Planning Documents

Comprehensive specs are in `Docs/`. Most are now implemented, use as reference for modifications:

| Doc | Status | Content |
|-----|--------|---------|
| `01-PROJECT-PLAN.md` | 📝 Updated | Architecture, tech stack, development phases |
| `02-DATABASE-SCHEMA.md` | ✅ Implemented | EF Core entities (Card, PriceHistory, SetChecklist), enums |
| `03-OPENROUTER-INTEGRATION.md` | ✅ Implemented | AI vision API setup and prompts (11 models) |
| `04-WHATNOT-CSV-FORMAT.md` | ✅ Implemented | Export CSV column mapping |
| `05-PRICING-RESEARCH.md` | ⚠️ Partial | Terapeak/eBay URL construction (browser links, no scraping) |
| `06-IMAGE-HOSTING.md` | ✅ Implemented | ImgBB API integration |
| `07-CLAUDE-CODE-GUIDE.md` | 📝 Updated | Working with existing codebase guide |
| `08-CARD-TERMINOLOGY.md` | 📖 Reference | Sports card domain reference |
| `10-GUI-ARCHITECTURE.md` | ✅ Implemented | Detailed Avalonia MVVM patterns, DI setup, view specs |
| `13-INVENTORY-TRACKING.md` | ✅ Implemented | Price staleness and financial tracking |
| `14-VARIATION-VERIFICATION.md` | ✅ Implemented | Checklist-based verification system |
| `16-CHECKLIST-DATA-SPEC.md` | ✅ Implemented | Checklist data structure and seeding |
| `17-FUTURE-ROADMAP.md` | 🆕 New | Future feature planning and priorities |
| `18-PHASE1-COMPLETION-SUMMARY.md` | ✅ Complete | Core library extraction and refactor summary |
| `19-TESTING-CHECKLIST-PHASE1.md` | ✅ Complete | Comprehensive testing checklist for Phase 1 |
| `20-PHASE2-COMPLETION-SUMMARY.md` | ✅ Complete | Web app foundation and feature implementation |
| `21-PHASE3-TESTING-PLAN.md` | 📝 In Progress | Functional testing, mobile, performance, security |
| `22-PHASE3-PROGRESS-SUMMARY.md` | 📝 In Progress | Current status and progress tracking |
| `23-FUNCTIONAL-TEST-RESULTS.md` | ✅ Complete | All page load tests passed (8/8) |
| `WEB-USER-GUIDE.md` | 📖 Reference | Web app user guide for mobile access |
| `DEPLOYMENT-GUIDE.md` | 📖 Reference | Web app deployment and network setup |
| `USER-GUIDE.md` | 📖 Reference | Desktop app user guide with screenshots |
| `HUB-ARCHITECTURE.md` | 📖 Reference | FlipKit Hub architecture and server management |

## Git Branching Workflow

- **Never commit directly to `master`.** All work must be done on feature/fix branches.
- **Branch naming:** `feature/<short-name>` for new features, `fix/<short-name>` for bug fixes (e.g., `feature/graded-cards`, `fix/date-picker-type`).
- **Create the branch before making changes:** `git checkout -b feature/<name>` from an up-to-date `master`.
- **Merge to master only after verification:** `dotnet build` passes with 0 errors, and the feature has been manually tested.
- **Delete the branch after merging** to keep the repo clean.

## Common Troubleshooting

- **View not found at runtime:** Check that the ViewModel class name matches the View name via the ViewLocator convention.
- **Binding not working:** Ensure properties use `[ObservableProperty]` (generates public property from `_camelCase` field) or manually raise `PropertyChanged`.
- **Command not firing:** Check `CanExecute` logic and ensure dependent properties call `OnPropertyChanged` when they change.
- **EF Core "no migrations":** Run `dotnet ef migrations add <Name>` from the Infrastructure project with `--startup-project` pointing to the App project.
- **OpenRouter JSON parse fails:** Strip markdown code blocks (` ```json `) from API response before deserializing.
