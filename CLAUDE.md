# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CardLister is a C# / .NET 8 desktop application built with **Avalonia UI 11** and the **MVVM pattern** (CommunityToolkit.Mvvm). It helps sports card sellers scan card photos with AI vision (OpenRouter API), manage inventory in local SQLite, research pricing via Terapeak/eBay, and export Whatnot-compatible CSV files for bulk listing.

**Current State:** ~80-90% MVP Complete — Fully functional end-to-end workflow with AI scanning, database management, pricing, export, and financial tracking. Currently working on the `feature/bulk-scan` branch to add batch scanning capabilities.

## Build & Run Commands

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run the app
dotnet run --project CardLister

# Build for release
dotnet build -c Release

# Publish self-contained Windows executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Run tests (when test projects exist)
dotnet test
```

## Architecture

**Current: Single-project structure** (works well, 3-project refactor is planned but optional):

```
CardLister/          # Avalonia desktop app (net8.0)
├── Views/           # 12 XAML views — no business logic in code-behind
├── ViewModels/      # 14 ViewModels using [ObservableProperty] and [RelayCommand]
├── Models/          # Domain entities (Card, PriceHistory, SetChecklist, enums)
├── Services/        # 11 service interfaces + implementations
├── Data/            # EF Core DbContext, repositories, migrations, seeders
├── Converters/      # 8 value converters for XAML bindings
├── Helpers/         # FuzzyMatcher, PriceCalculator, etc.
├── Assets/          # Icons, images, seed data JSON
├── Styles/          # AppStyles.axaml with consistent theming
├── ViewLocator.cs   # Maps ViewModels to Views by naming convention
├── App.axaml.cs     # DI container, logging, database initialization
└── Program.cs       # Entry point
```

**Future 3-project layout** (planned refactor for better testability):
- `CardLister.App` — Avalonia views, converters, styles, DI wiring
- `CardLister.Core` — ViewModels, models, service interfaces (no UI references)
- `CardLister.Infrastructure` — EF Core DbContext, API clients, service implementations

### MVVM Data Flow

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

**Technical Implementation:**
- ✅ 14 ViewModels with MVVM pattern
- ✅ 12 Views with Avalonia UI
- ✅ 11 services with interface-based DI
- ✅ SQLite database with EF Core (88 fields per card)
- ✅ Seed data system for checklists
- ✅ 8 custom XAML value converters
- ✅ Fuzzy matching for verification (0.85/0.7 thresholds)
- ✅ Rate limiting for free-tier AI models

### 🚧 Current Work

- 🚧 **Bulk Scan feature** (feature/bulk-scan branch) — Multi-card batch scanning with front/back pairing

### 📋 Future Roadmap

**High Priority:**
- ⏳ 3-project architecture refactor (App/Core/Infrastructure split)
- ⏳ Unit and integration tests
- ⏳ Automated price scraping (replace manual browser lookup)

**Medium Priority:**
- ⏳ Cloud sync / backup
- ⏳ Additional export formats (eBay, COMC)
- ⏳ Performance optimizations for large inventories (1000+ cards)
- ⏳ Dark theme support

**Low Priority:**
- ⏳ Mobile companion app
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
