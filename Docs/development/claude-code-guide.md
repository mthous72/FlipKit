# Working with FlipKit in Claude Code

## Overview

This guide helps you work with the **current FlipKit Hub v3.3.6 codebase** using Claude Code or any LLM agent. It assumes you're modifying or extending an existing, working app — not building from scratch. For the from-scratch story, read `Docs/01-PROJECT-PLAN.md` (historical) and the architecture doc [10-GUI-ARCHITECTURE.md](10-GUI-ARCHITECTURE.md).

**Current State:** Production. 4-project solution (Core / Desktop / Web / Api), shared SQLite via WAL mode, embedded Web + API servers managed from Desktop. 490 unit/integration tests with a CI gate. Refactor Phases 1–6 complete (see [29-REFACTORING-PLAN.md](29-REFACTORING-PLAN.md) and [30-REFACTOR-STATUS.md](30-REFACTOR-STATUS.md)).

The repo's [CLAUDE.md](../CLAUDE.md) at the project root is the authoritative quickstart for an agent — this doc is the longer-form companion: when to touch which project, how to add features in each layer, common pitfalls.

---

## Prerequisites

1. **.NET 8 SDK** for Core / Desktop / Web; **.NET 9 SDK** for Api (`dotnet --list-sdks` should show both 8.0+ and 9.0+).
2. **Claude Code** (or your LLM agent of choice) — `npm install -g @anthropic-ai/claude-code` for the CLI.
3. **API Keys** (only needed if you're running the app, not for refactoring/tests):
   - OpenRouter: https://openrouter.ai/keys
   - ImgBB: https://api.imgbb.com/

---

## Solution Structure (4 projects)

See [CLAUDE.md](../CLAUDE.md) for the canonical layout. Quick reminder of where to put things:

| You're adding... | Goes in... |
|---|---|
| Domain entity, enum, business rule | `FlipKit.Core/Models/`, `FlipKit.Core/Models/Enums/` |
| Pure helper (no I/O) | `FlipKit.Core/Helpers/` |
| Service interface | `FlipKit.Core/Services/Interfaces/` |
| Service implementation | `FlipKit.Core/Services/Implementations/` (or `FlipKit.Desktop/Services/` if it's UI-coupled, like `NetworkAddressProvider`) |
| EF migration | `FlipKit.Core/Data/Migrations/` (`dotnet ef migrations add ...` from the Desktop project as startup) |
| Desktop ViewModel + View | `FlipKit.Desktop/ViewModels/` + `FlipKit.Desktop/Views/` (paired by name — `FooView` ↔ `FooViewModel`) |
| XAML value converter | `FlipKit.Desktop/Converters/` |
| Web controller + Razor view | `FlipKit.Web/Controllers/` + `FlipKit.Web/Views/` |
| Web view-model / DTO | `FlipKit.Web/Models/` |
| New API endpoint | Add a `MapGet`/`MapPost` to `FlipKit.Api/Program.cs` (minimal API, no controllers) |

**Hard rule:** `FlipKit.Desktop`, `FlipKit.Web`, and `FlipKit.Api` all reference `FlipKit.Core` but **never reference each other**. If you find yourself wanting a Desktop service inside Web, the abstraction belongs in Core.

---

## Adding a New Feature — by example

### Add a new Desktop ViewModel + View

1. **ViewModel:** `FlipKit.Desktop/ViewModels/FooViewModel.cs`
   - Inherit from `ViewModelBase`
   - `[ObservableProperty] private string? _bar;` — the source generator gives you `Bar` + `INotifyPropertyChanged`
   - `[RelayCommand] private async Task LoadAsync()` — gives you `LoadCommand` (note: drops the `Async` suffix)
   - Constructor-inject every dependency. Don't `new` services up.
2. **View:** `FlipKit.Desktop/Views/FooView.axaml` — `ViewLocator` maps by name. `FooView` ↔ `FooViewModel`.
3. **DI:** register the VM in `FlipKit.Desktop/App.axaml.cs`. ViewModels are `Transient`. (See "DI lifetime gotcha" below.)
4. **Navigation:** add a case in `MainWindowViewModel.NavigateToCommand` and a sidebar button in `MainWindow.axaml`.
5. **Tests:** `FlipKit.Desktop.Tests/ViewModels/FooViewModelTests.cs`. Use `Substitute.For<IFoo>()` for every dep.

### Add a new Core service

1. **Interface:** `FlipKit.Core/Services/Interfaces/IFooService.cs`
2. **Implementation:** `FlipKit.Core/Services/Implementations/FooService.cs`
3. **DI:** register in **all** consuming surfaces — `FlipKit.Desktop/App.axaml.cs`, `FlipKit.Web/Program.cs`, `FlipKit.Api/Program.cs` (only the ones that consume it). Pick lifetime by dep — see below.
4. **Tests:** `FlipKit.Core.Tests/Services/FooServiceTests.cs`. If it touches `FlipKitDbContext`, use the real-SQLite-in-memory pattern (see `TestDbContext` in the test project).

### Add a new database column

Two paths exist — pick by whether you also need EF to know about the column at query time.

- **Reversible, EF-tracked:** add a property to the model, then `dotnet ef migrations add Add<Column>To<Table> --project FlipKit.Core --startup-project FlipKit.Desktop` and `dotnet ef database update`. This is the right path for almost all cases.
- **Best-effort additive:** add the column to `SchemaUpdater` (`FlipKit.Core/Data/SchemaUpdater.cs`) so existing user databases get the column on next launch. Do this **in addition to** the migration — the migration covers fresh installs, `SchemaUpdater` covers users who upgrade past a version that didn't yet have the column. (This duality is captured in ADR-003.)

### Add a new API endpoint

Edit `FlipKit.Api/Program.cs`. There are no controllers — endpoints live as `app.MapGet("/api/foo/{id}", ...)` etc. Pattern:

```csharp
app.MapGet("/api/cards/foo", async (FlipKitDbContext db, int id) =>
{
    var card = await db.Cards.FindAsync(id);
    return card is null ? Results.NotFound() : Results.Ok(card);
});
```

---

## Critical patterns to follow

### MVVM (Desktop)

```
View (.axaml)  ── compiled bindings ──→  ViewModel (.cs)  ── DI ──→  Service interfaces  ── ──→  Core / external APIs
```

- Views are **dumb XAML**. No code-behind business logic. If you find a `.axaml.cs` doing more than wiring up a control event, move it to the VM.
- `[ObservableProperty]` and `[RelayCommand]` come from `CommunityToolkit.Mvvm` source generators — always prefer these to manual `INotifyPropertyChanged`.
- The `Async` suffix is dropped by `[RelayCommand]`: a method called `LoadAsync` produces `LoadCommand`, not `LoadAsyncCommand`. The Desktop tests assume this.

### MVC (Web)

```
Browser → Request → Controller (.cs) → Core service interfaces → DbContext → View (.cshtml) → Response
```

- DI lifetimes: **Singleton** for stateless services, **Scoped** for any service that takes `FlipKitDbContext`. Mismatching these creates the captive-dependency bug — see "DI lifetime gotcha" below.

### Minimal API (Api)

Endpoint mapping in `Program.cs`. No controllers, no MVC. Each endpoint receives its dependencies via parameter injection.

### Testing

Three test projects mirror the production projects:
- `FlipKit.Core.Tests` — helpers, services, repositories. Real SQLite in-memory; NSubstitute for HTTP.
- `FlipKit.Desktop.Tests` — ViewModels with NSubstitute mocks for every Core service.
- `FlipKit.Web.Tests` — controllers, also via NSubstitute.

The CI gate (in `build-installers.ps1` and `build-release.ps1`) aborts the build if `dotnet test` fails. Don't add a test that depends on machine-specific state (real network adapters, real OpenRouter key, etc.) — wrap external systems behind an interface and stub them.

---

## DI lifetime gotcha (read this once)

`FlipKitDbContext` is **Scoped**. If a Singleton service captures a Scoped dep, EF Core will throw at startup or — worse — silently reuse a stale context. The audit caught this with `ISoldPriceService` registered as Singleton with a Scoped DbContext (D1 in [AUDIT-2026-05.md](AUDIT-2026-05.md)) and again with `IVariationVerifier`. Both fixed in Phase 5a.

**Rule:** any service that takes `FlipKitDbContext` (directly or transitively) must be **Scoped**. ViewModels are Transient (Avalonia spins them up per-navigation). Pure helpers that don't touch the DB can be Singleton.

---

## Data Access Modes (Local vs Remote)

`DataAccessModeDetector` picks one of two modes at startup:

- **Local:** direct SQLite via `FlipKitDbContext`. Default when not on Tailscale or when the API server isn't reachable.
- **Remote:** HTTP calls to the FlipKit.Api server through `ApiCardRepository`. Used when Desktop or Web is consuming a remote machine's data via Tailscale.

If you're adding a repository operation, **add it to both `CardRepository` (DB) and `ApiCardRepository` (HTTP) and a matching API endpoint**. Tests live alongside the DB version.

---

## Working with the LLM agent

### What to paste into prompts

For most tasks the agent will read the relevant files itself. For the rare cases when you want to seed it:

- The repo root [CLAUDE.md](../CLAUDE.md) (auto-loaded by Claude Code; included in agent context).
- The relevant subsection of [10-GUI-ARCHITECTURE.md](10-GUI-ARCHITECTURE.md) for new VM/View work.
- This doc's "Critical patterns" section for new contributors.
- For checklist or learning work: [16-CHECKLIST-DATA-SPEC.md](16-CHECKLIST-DATA-SPEC.md) and (when implemented) [28-CHECKLIST-INSIDER-IMPORT-PLAN.md](28-CHECKLIST-INSIDER-IMPORT-PLAN.md).

### Common pitfalls the agent may hit

- **Adding services to only one DI surface.** A Core service consumed by Desktop, Web, **and** Api needs registration in all three composition roots.
- **Forgetting `Async` is stripped from RelayCommand names.** `ConfirmDeleteAsync` becomes `ConfirmDeleteCommand`, not `ConfirmDeleteAsyncCommand`.
- **JSON-converted EF columns without a `ValueComparer`.** EF Core can't detect collection mutations otherwise — `list.Add(x)` followed by `SaveChangesAsync()` is a no-op. The pattern is set on `SetChecklist.Cards` and `SetChecklist.KnownVariations` (D3 fix, Phase 4.5). Copy that pattern when adding new JSON columns.
- **Testing UI code that needs `Avalonia.AppBuilder`.** `new Bitmap(stream)` returns null in tests — there's no app builder. The `NetworkAddressProvider` tests deal with this by asserting `null` for bitmaps; if you need richer Avalonia coverage, plan a headless test layer.

---

## Common Tasks (cheat sheet)

| Task | Command |
|---|---|
| Build everything | `dotnet build FlipKit.sln` |
| Run Desktop (auto-starts servers if configured) | `dotnet run --project FlipKit.Desktop` |
| Run Web standalone | `dotnet run --project FlipKit.Web --urls "http://0.0.0.0:5000"` |
| Run Api standalone | `dotnet run --project FlipKit.Api` |
| Run all tests | `dotnet test` |
| Run one test project | `dotnet test FlipKit.Core.Tests` |
| Add EF migration | `dotnet ef migrations add <Name> --project FlipKit.Core --startup-project FlipKit.Desktop` |
| Apply migrations | `dotnet ef database update --project FlipKit.Core --startup-project FlipKit.Desktop` |
| Build release Hub bundles | `.\build-release.ps1 -Version 3.3.6` |

Database location: `%LOCALAPPDATA%\FlipKit\cards.db`
Logs: `%LOCALAPPDATA%\FlipKit\logs\log-YYYYMMDD.txt`
Settings: `%LOCALAPPDATA%\FlipKit\config.json`
Override Api DB path: `FLIPKIT_DB_PATH` env var.

---

## Troubleshooting

### View not found at runtime
ViewLocator can't map the ViewModel to a View. Either the View name doesn't match (must be `FooView` for `FooViewModel`) or the View isn't in `FlipKit.Desktop/Views/`.

### Binding doesn't update
Property doesn't raise `PropertyChanged`. Use `[ObservableProperty]` on the backing field — the generator wires up notification automatically.

### Command doesn't fire
Either the binding name is wrong (remember the `Async`-stripping rule) or `CanExecute` returns false. If you have a command that depends on a property, the property setter needs to call `OnPropertyChanged` *and* the command needs to know via `[RelayCommand(CanExecute = nameof(...))]`.

### Database is locked
Multiple processes hold a write lock. Close all FlipKit instances. If `cards.db-wal` and `cards.db-shm` linger after closing, delete them. WAL mode normally lets multiple readers + one writer coexist — if you're seeing locks, something's holding a transaction open longer than it should.

### OpenRouter scan fails on first model, fallback doesn't kick in
Pre-Phase 5a this was a real bug (D2): `IsRetryableHttpError` checked for the digit `"500"` in the error message but `HttpStatusCode.ToString()` produces `"InternalServerError"`. The throw site now includes the integer status code. If you see this regress, check the test `Should_FallBackOn5xx_When_FirstModelReturnsServerError` and the throw-site format string in `OpenRouterScannerService`.

### Mode detection wrong (Local vs Remote)
`DataAccessModeDetector` picks Remote when it can reach the Api server's `/health` endpoint over Tailscale. If you're on a flaky network it may flap. Settings → Data Access lets you pin the mode.

### Server start/stop message vanishes after ~2 seconds (Phase 5a fix)
This was the §7.10 race in `SettingsViewModel`: the 2-second status-poll Timer was overwriting user-visible Start/Stop success messages. Phase 5a gated the message-overwrite branch on an `_explicitOperationInProgress` volatile flag. If you reintroduce the race when refactoring, the existing test will catch it.

---

## See also

- [29-REFACTORING-PLAN.md](29-REFACTORING-PLAN.md) — full refactor plan (history + future)
- [30-REFACTOR-STATUS.md](30-REFACTOR-STATUS.md) — live snapshot of where the refactor is
- [AUDIT-2026-05.md](AUDIT-2026-05.md) — original audit + ongoing discovery log
- [REGRESSION-CHECKLIST.md](REGRESSION-CHECKLIST.md) — manual smoke flows to run before merging
- [HUB-ARCHITECTURE.md](HUB-ARCHITECTURE.md) — embedded server model
- [10-GUI-ARCHITECTURE.md](10-GUI-ARCHITECTURE.md) — MVVM details, DI setup
- `Docs/ADR/` — architecture decision records (added in Phase 6)

---

**Last Updated:** 2026-05-04 (Phase 6 rewrite — replaced single-project guide with 4-project orientation)
