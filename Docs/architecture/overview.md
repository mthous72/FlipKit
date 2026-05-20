# Architecture Overview

FlipKit is a C# / .NET 8 application suite for sports-card sellers. It ships as
**FlipKit Hub** (v3.7.0): a single package containing the Avalonia desktop app
plus embedded Web and API servers, all managed from the Desktop UI.

This document covers two layers:

1. **The Hub package** — how Desktop, Web, and API fit together and how the
   Desktop app manages the embedded server processes.
2. **The Desktop MVVM app** — DI, navigation, and the View/ViewModel conventions.

For the data-access split (Local SQLite vs. Remote API over Tailscale) see
[data-access.md](data-access.md). For the schema see
[database-schema.md](database-schema.md).

---

## FlipKit Hub Package

Instead of three separate downloads, FlipKit Hub bundles everything into one
package and lets the Desktop app orchestrate the rest:

- **Single download** — one package contains Desktop + servers.
- **Unified management** — start/stop the Web and API servers from
  Settings → Servers in the Desktop app.
- **Auto-start** — servers can start automatically when Desktop launches.
- **QR-code access** — connect a phone to the Web UI by scanning a QR code.
- **Clean shutdown** — the Desktop app kills its server child processes on exit.

```
┌─────────────────────────────────────────────────────────────┐
│ FlipKit Hub Package                                          │
├─────────────────────────────────────────────────────────────┤
│  FlipKit.Desktop(.exe)  — Avalonia UI 11 (.NET 8)            │
│  ├─ ServerManagementService (spawns + monitors servers)      │
│  ├─ Settings → Servers UI (start/stop/logs/QR)               │
│  └─ Auto-start logic                                         │
│                                                              │
│  servers/                                                    │
│  ├─ FlipKit.Web(.exe)   — ASP.NET Core 8 MVC, default :5000  │
│  └─ FlipKit.Api(.exe)   — Minimal API (.NET 9), default :5001│
│                                                              │
│  Shared DB: %LOCALAPPDATA%\FlipKit\cards.db (SQLite, WAL)    │
└─────────────────────────────────────────────────────────────┘
```

The Desktop, Web, and API projects all reference **FlipKit.Core** and never
reference each other. The API targets **net9.0**; Core, Desktop, and Web target
**net8.0**.

### Components

| Component | Tech | Default port | Health |
|---|---|---|---|
| FlipKit.Desktop | Avalonia UI 11, .NET 8 | — | — |
| FlipKit.Web | ASP.NET Core 8 MVC, Bootstrap 5 | 5000 | `/health` |
| FlipKit.Api | Minimal API, .NET 9 | 5001 | `/health` |

The shared SQLite database runs in **Write-Ahead Logging (WAL)** mode so
Desktop, Web, and API can read concurrently without lock contention.

### Server lifecycle (`ServerManagementService`)

`FlipKit.Desktop/Services/ServerManagementService.cs` owns the server child
processes. Behavior reflects the shipping code, not the original v3.1 design:

- **Executable resolution** — looks for `FlipKit.Web`/`FlipKit.Api` (with or
  without `.exe`) first in a `servers/` subfolder next to the Desktop binary,
  then falls back to the Desktop directory for development runs.
- **Startup** — each server is launched with
  `--urls http://0.0.0.0:{port}`, with stdout/stderr captured into an in-memory
  ring buffer (last 100 lines, surfaced in Settings → Servers → Server Logs).
- **Port fallback** — if the requested port is taken, the service probes up to
  10 consecutive ports and uses the first free one; the actual port is reported
  back to the UI (and the QR code).
- **Health gating** — after launch the service polls `GET /health` (with a
  ~2 s warm-up) until success or a 10 s timeout, then reports the result.
- **Crash detection** — a 5 s timer checks whether either process has exited
  unexpectedly and clears its state so the UI shows it as stopped.
- **Shutdown** — servers are console apps, so they don't respond to
  `CloseMainWindow()`. The service calls `Process.Kill(entireProcessTree: true)`
  on each. `Dispose()` stops both servers synchronously so closing the Desktop
  app never orphans a server.

### Network binding & security

- The Web and API servers bind to `http://0.0.0.0:{port}` — reachable on every
  local interface. There is **no built-in authentication**; the trust model is
  "your local network."
- Recommendations: restrict with a firewall, connect phones over trusted Wi-Fi,
  use **Tailscale** for remote access, and never expose the servers to the
  public internet. See [data-access.md](data-access.md) and the Tailscale guides
  under `Docs/guides/`.

### Use cases

- **Mobile scanning at home** — launch Desktop (servers auto-start), open
  Settings → Servers, scan the QR code with a phone, and scan cards from the Web
  UI. Cards sync through the shared database.
- **Card-show scanning** — with Desktop running at home and the phone on
  Tailscale, browse to the Tailscale IP to scan purchases on the go.
- **Desktop-only** — uncheck the auto-start options in Settings → Servers to run
  the desktop app alone with no servers.

---

## Desktop MVVM App

The Desktop app is a native cross-platform desktop application built with
Avalonia UI 11 and the MVVM pattern. Users double-click an executable — no
Python, no browser, no runtime dependency on the target machine
(self-contained publish).

**Key principles:**

- **Local-first** — data stays on the user's computer.
- **User owns credentials** — users create their own API accounts.
- **Simple setup** — a first-run wizard guides API-key configuration.
- **MVVM** — ViewModels are unit-testable; Views are declarative XAML.

### Dependency injection

Services and ViewModels are registered in `App.axaml.cs`. The scan service is
the `CompositeScannerService` (CardSight first, OpenRouter fallback — see
[../features/ai-scanning.md](../features/ai-scanning.md)).

DI lifetime rule: any service that takes `FlipKitDbContext` (directly or
transitively) must be **Scoped**; ViewModels are **Transient**; pure helpers
with no DB dependency can be **Singleton**.

### ViewLocator pattern

ViewModels are mapped to Views by naming convention — the type name's
`ViewModel` suffix is replaced with `View` and the namespace is remapped to the
Views namespace. So `ScanViewModel` resolves to `ScanView`. The `ViewLocator` is
registered as an application-level `DataTemplate` in `App.axaml`.

### Navigation

Navigation is ViewModel-first. `MainWindowViewModel.CurrentPage` holds the active
ViewModel and a `NavigateTo` relay command swaps it for the requested page
(Scan, Inventory, Pricing, Export, Settings, etc.). `MainWindow.axaml` hosts a
sidebar plus a `ContentControl` bound to `CurrentPage`; the `ViewLocator`
resolves the matching View automatically.

### Where things live

```
FlipKit.Desktop/
├── App.axaml(.cs)        # Theme, resources, ViewLocator, DI container
├── ViewLocator.cs        # ViewModel → View resolver
├── Views/                # Pure XAML (MainWindow, Scan, Inventory, …)
├── ViewModels/           # [ObservableProperty] / [RelayCommand] VMs
├── Services/             # Platform-coupled services (file dialogs, ServerManagementService)
├── Converters/           # XAML value converters
└── Assets/               # Icons, logo
```

Views are pure XAML with declarative bindings — no business logic in code-behind.
ViewModels use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`,
`[RelayCommand]`); note the generator drops the `Async` suffix from command names
(`LoadAsync` → `LoadCommand`).

### Credential & data storage

User configuration and data live under the platform app-data folder:

```
%LOCALAPPDATA%\FlipKit\        (Windows; ~/.local/share/FlipKit on Linux,
├── config.json                 ~/Library/Application Support/FlipKit on macOS)
├── cards.db                    ← SQLite inventory (WAL)
├── images/                     ← local card photos
├── exports/                    ← generated CSV files
└── logs/                       ← app logs
```

API keys and OAuth tokens (OpenRouter, ImgBB, CardSight, eBay) are stored in
`config.json`. Secrets are encrypted at rest via
`DataProtectionSecretEncryption` (ASP.NET Core Data Protection — DPAPI on
Windows, file-based key ring on Linux/macOS) using a `protected:` prefix.

---

## Service interfaces

Business logic is accessed through interfaces defined in `FlipKit.Core`. Key
ones include `ICardRepository`, `IScannerService` (implemented by
`CompositeScannerService`), `ICardsightSubscriptionService`, `IPricerService`,
`IImageUploadService`, `IExportService`, `ISettingsService`, and
`IServerManagementService`. See `FlipKit.Core/Services/Interfaces/` for the
authoritative list.

## Architecture decision records

Significant decisions are recorded under [adr/](adr/):

- ADR-001 — Hub architecture (embedded servers).
- ADR-002 — net8/net9 project mix.
- ADR-003 — `EnsureCreated`/`SchemaUpdater` vs. migrations.
- ADR-004 — User-driven checklist import.
- ADR-005 — Avalonia over MAUI/WPF.
