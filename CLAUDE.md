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

**Current State:** v3.3.6 FlipKit Hub released. Unified package with Desktop app + embedded Web and API servers. Desktop and Web both feature-complete. Servers managed from Desktop Settings UI.

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
.\build-release.ps1 -Version 3.2.0
# Output: releases/FlipKit-Hub-Windows-x64-v3.2.0.zip
#         releases/FlipKit-Hub-Linux-x64-v3.2.0.zip

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

### Data Access Modes

Both Desktop and Web support two data access modes, detected automatically by `DataAccessModeDetector`:

- **Local Mode** (default) - Direct SQLite access via `FlipKitDbContext`
- **Remote Mode** (via Tailscale) - HTTP calls to the Api server using `ApiCardRepository`

### Desktop MVVM Pattern

```
View (XAML) → data binding → ViewModel (C#) → DI-injected services → Data/APIs
```

- **Views** are pure XAML with declarative bindings. No business logic in code-behind.
- **ViewModels** use CommunityToolkit.Mvvm source generators: `[ObservableProperty]` for reactive properties, `[RelayCommand]` for async commands.
- **Navigation** is ViewModel-first: `MainWindowViewModel.CurrentPage` holds the active ViewModel; `ViewLocator` resolves the matching View by replacing `"ViewModel"` with `"View"` in the type name.

### Web MVC Pattern

```
Browser → HTTP Request → Controller → Core Services → Database/APIs → View (Razor) → HTTP Response
```

- **Controllers** handle HTTP requests, call Core services, return views
- **ViewModels (DTOs)** are simple data transfer objects for Razor views (no ObservableObject)
- **DI Lifetimes:** Singleton for stateless services, Scoped for DbContext-dependent services

### API Server Endpoints

Minimal API design (no controllers, endpoint mapping in Program.cs):
- CRUD: `/api/cards`, `/api/cards/{id}`
- Queries: `/api/cards/unpriced`, `/api/cards/stale`, `/api/cards/stats`
- Price history: `/api/cards/{id}/price-history`
- Reports: `/api/reports/sold`
- Health: `/`, `/health`

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

Comprehensive specs are in `Docs/`. Key references:

| Doc | Content |
|-----|---------|
| `02-DATABASE-SCHEMA.md` | EF Core entities (Card, PriceHistory, SetChecklist), enums |
| `03-OPENROUTER-INTEGRATION.md` | AI vision API setup and prompts |
| `08-CARD-TERMINOLOGY.md` | Sports card domain reference |
| `10-GUI-ARCHITECTURE.md` | Avalonia MVVM patterns, DI setup |
| `14-VARIATION-VERIFICATION.md` | Checklist-based verification system |
| `17-FUTURE-ROADMAP.md` | Future feature planning |
| `HUB-ARCHITECTURE.md` | FlipKit Hub server management |

See README.md for full feature list, known limitations, and roadmap.
