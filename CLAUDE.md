# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FlipKit is a C# / .NET 8 application suite for sports card sellers, consisting of:

1. **FlipKit.Desktop** - Avalonia UI 11 desktop app (Windows/Mac/Linux) with full feature set
2. **FlipKit.Web** - ASP.NET Core 8.0 MVC web app for mobile access (phone/tablet browsers)
3. **FlipKit.Api** - Minimal API server (net9.0) for remote data access via Tailscale
4. **FlipKit.Core** - Shared business logic library (models, services, data access)

Desktop and Web share a single SQLite database with WAL mode for concurrent access. When using Tailscale for remote access, the Api server provides REST endpoints that Desktop and Web can consume instead of direct database access.

**Core Features:** AI vision scanning (OpenRouter API), inventory management, pricing research (eBay/Terapeak), Whatnot CSV export, sales tracking, financial reports.

**Current State:** v3.7.0 FlipKit Hub released. Unified package with Desktop app + embedded Web and API servers. Desktop and Web both feature-complete. Servers managed from Desktop Settings UI. Scan pipeline is CardSight (first pass) → OpenRouter (fallback); Ximilar was removed in v3.7.0.

## Build & Run Commands

```bash
# Build entire solution
dotnet build FlipKit.sln

# Run desktop app (servers auto-start if configured)
dotnet run --project FlipKit.Desktop

# Run web app standalone (development)
dotnet run --project FlipKit.Web --urls "http://0.0.0.0:5000"

# Run API server standalone
dotnet run --project FlipKit.Api

# Build release packages (Windows and Linux)
.\build-release.ps1 -Version 3.7.0
# Output: releases/FlipKit-Hub-Windows-x64-v3.7.0.zip
#         releases/FlipKit-Hub-Linux-x64-v3.7.0.zip

# Run tests (when test projects exist)
dotnet test
```

**EF Core Migrations:**
```bash
# Add new migration
dotnet ef migrations add <MigrationName> --project FlipKit.Core --startup-project FlipKit.Desktop

# Update database
dotnet ef database update --project FlipKit.Core --startup-project FlipKit.Desktop
```

**Environment Variables:**
- `FLIPKIT_DB_PATH` - Override database path for Api server (default: `%LocalAppData%/FlipKit/cards.db`)

## Architecture

**4-Project Structure:**

```
FlipKit.sln
├── FlipKit.Core/          # Shared business logic (net8.0 class library)
│   ├── Models/            # Domain entities, Enums/
│   ├── Services/          # Interfaces/ and Implementations/
│   ├── Data/              # FlipKitDbContext, migrations, seeders
│   └── Helpers/           # FuzzyMatcher, PriceCalculator, DataAccessModeDetector
│
├── FlipKit.Desktop/       # Avalonia UI app (net8.0 WinExe)
│   ├── Views/             # XAML views
│   ├── ViewModels/        # MVVM ViewModels with [ObservableProperty]
│   ├── Services/          # Platform-specific (file dialogs, server management)
│   └── Converters/        # XAML value converters
│
├── FlipKit.Web/           # ASP.NET Core MVC (net8.0)
│   ├── Controllers/       # MVC controllers
│   ├── Views/             # Razor views with Bootstrap 5
│   ├── Models/            # ViewModels/DTOs for Razor views
│   └── Services/          # Platform-specific (file upload, navigation)
│
└── FlipKit.Api/           # Minimal API server (net9.0)
    └── Program.cs         # REST endpoints, CORS, health checks
```

**Dependency Flow:**
```
FlipKit.Desktop ─┐
                 ├─→ FlipKit.Core ←─ Shared database (WAL mode)
FlipKit.Web ─────┤
                 │
FlipKit.Api ─────┘
```

Desktop, Web, and Api all reference Core, but **never reference each other**.

For the full architecture — Hub embedded-server lifecycle, Desktop MVVM /
ViewLocator / navigation, Web MVC, the API endpoint surface, DI lifetimes, and
the Local-vs-Remote data-access modes — see
**[Docs/architecture/overview.md](Docs/architecture/overview.md)** and
**[Docs/architecture/data-access.md](Docs/architecture/data-access.md)**. Brief
reminders:

- **Desktop (MVVM):** Views are pure XAML; ViewModels use CommunityToolkit.Mvvm
  source generators (`[ObservableProperty]`, `[RelayCommand]` — the `Async`
  suffix is dropped). Navigation is ViewModel-first via
  `MainWindowViewModel.CurrentPage` + `ViewLocator`.
- **Web (MVC):** Controllers → Core services → DbContext → Razor views. Singleton
  for stateless services, Scoped for anything taking `FlipKitDbContext`.
- **Api:** Minimal API, no controllers — endpoints mapped in `Program.cs`
  (`/api/cards`, `/api/cards/unpriced|stale|stats`, `/api/cards/{id}/price-history`,
  `/api/reports/sold`, `/health`).
- **Data access:** `DataAccessModeDetector` auto-selects Local (direct SQLite via
  `FlipKitDbContext`) or Remote (HTTP to the Api via `ApiCardRepository`, over
  Tailscale).

## Important Conventions

- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`) — all types must be explicitly nullable or non-nullable.
- **Compiled bindings by default** in Desktop (`<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`).
- Use `decimal` for all money fields, `DateTime` for all date fields.
- Enums stored as strings in the database.
- All I/O operations must be `async Task`.
- Avalonia `DataAnnotationsValidationPlugin` is disabled in `App.axaml.cs` to avoid conflicts with CommunityToolkit validation.
- **DbContext class** is `FlipKitDbContext` in `FlipKit.Core/Data/FlipKitDbContext.cs`.
- **Api targets net9.0** while Core, Desktop, and Web target net8.0.

## Git Branching Workflow

- **Never commit directly to `master`.** All work must be done on feature/fix branches.
- **Branch naming:** `feature/<short-name>` for new features, `fix/<short-name>` for bug fixes.
- **Create the branch before making changes:** `git checkout -b feature/<name>` from an up-to-date `master`.
- **Merge to master only after verification:** `dotnet build` passes with 0 errors, and the feature has been manually tested.
- **Delete the branch after merging** to keep the repo clean.

## Common Troubleshooting

- **View not found at runtime:** Check that the ViewModel class name matches the View name via the ViewLocator convention.
- **Binding not working:** Ensure properties use `[ObservableProperty]` (generates public property from `_camelCase` field) or manually raise `PropertyChanged`.
- **Command not firing:** Check `CanExecute` logic and ensure dependent properties call `OnPropertyChanged` when they change.
- **OpenRouter JSON parse fails:** Strip markdown code blocks (` ```json `) from API response before deserializing.
- **Data access mode issues:** Check `DataAccessModeDetector` — it auto-detects Local vs Remote mode based on Tailscale availability.
- **Server management:** Web and API servers are managed as child processes by `ServerManagementService` in Desktop. Check server health via `/health` endpoints.

## Planning Documents

Comprehensive specs are in `Docs/` — start at the [Docs index](Docs/README.md).
Key references:

| Doc | Content |
|-----|---------|
| `Docs/architecture/overview.md` | Hub server management + Avalonia MVVM patterns, DI setup |
| `Docs/architecture/database-schema.md` | EF Core entities (Card, PriceHistory, SurpriseSet, SetChecklist), enums |
| `Docs/features/ai-scanning.md` | Scan pipeline: CardSight → OpenRouter, prompts, quota panel |
| `Docs/features/verification.md` | Checklist-based verification + verified-fields LLM hint mode |
| `Docs/features/card-terminology.md` | Sports card domain reference |
| `Docs/planning/roadmap.md` | Future feature planning |

See README.md for full feature list, known limitations, and roadmap.
