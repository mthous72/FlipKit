# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CardLister is a C# / .NET 8 desktop application built with **Avalonia UI 11** and the **MVVM pattern** (CommunityToolkit.Mvvm). It helps sports card sellers scan card photos with AI vision (OpenRouter API), manage inventory in local SQLite, research pricing via Terapeak/eBay, and export Whatnot-compatible CSV files for bulk listing.

**Current state:** Skeleton — basic Avalonia app structure with placeholder MainWindow. No database, services, or domain models implemented yet.

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

**Single-project structure** (planned to expand to 3 projects: App, Core, Infrastructure):

```
CardLister/          # Avalonia desktop app (net8.0)
├── Views/           # XAML views — no business logic in code-behind
├── ViewModels/      # ObservableObject subclasses with [ObservableProperty] and [RelayCommand]
├── Models/          # Domain entities (Card, PriceHistory, enums) — currently empty
├── Assets/          # Icons and images
├── ViewLocator.cs   # Maps ViewModels to Views by naming convention (replace "ViewModel" → "View")
├── App.axaml.cs     # Application startup (DI container setup goes here)
└── Program.cs       # Entry point
```

**Planned 3-project layout** (per `Docs/01-PROJECT-PLAN.md`):
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

**Planned additions:** EF Core SQLite, CsvHelper, Serilog, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Http

## Important Conventions

- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`) — all types must be explicitly nullable or non-nullable.
- **Compiled bindings by default** (`<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`).
- Use `decimal` for all money fields, `DateTime` for all date fields.
- Enums stored as strings in the database.
- All I/O operations must be `async Task`.
- Avalonia `DataAnnotationsValidationPlugin` is disabled in `App.axaml.cs` to avoid conflicts with CommunityToolkit validation.

## Planning Documents

Comprehensive specs are in `Docs/`. Reference these when implementing features:

| Doc | Content |
|-----|---------|
| `01-PROJECT-PLAN.md` | Architecture, tech stack, development phases |
| `02-DATABASE-SCHEMA.md` | EF Core entities (Card, PriceHistory), enums |
| `03-OPENROUTER-INTEGRATION.md` | AI vision API setup and prompts |
| `04-WHATNOT-CSV-FORMAT.md` | Export CSV column mapping |
| `05-PRICING-RESEARCH.md` | Terapeak/eBay URL construction |
| `06-IMAGE-HOSTING.md` | ImgBB API integration |
| `07-CLAUDE-CODE-GUIDE.md` | Step-by-step build prompts and troubleshooting |
| `08-CARD-TERMINOLOGY.md` | Sports card domain reference |
| `10-GUI-ARCHITECTURE.md` | Detailed Avalonia MVVM patterns, DI setup, view specs |
| `13-INVENTORY-TRACKING.md` | Price staleness and financial tracking |

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
