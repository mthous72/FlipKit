# GitHub Copilot — FlipKit Instructions

FlipKit is a C# / .NET 8 application suite for sports-card sellers, shipped as
**FlipKit Hub** (Desktop + embedded Web and API servers). See the repo-root
[`CLAUDE.md`](../CLAUDE.md) and [`Docs/README.md`](../Docs/README.md) for the
authoritative architecture, conventions, and doc index.

## Key facts

- **Projects:** `FlipKit.Core` (shared, net8.0), `FlipKit.Desktop` (Avalonia UI 11,
  net8.0), `FlipKit.Web` (ASP.NET Core MVC, net8.0), `FlipKit.Api` (Minimal API,
  net9.0). Desktop/Web/Api reference Core but **never each other**.
- **Desktop = MVVM:** pure-XAML Views; ViewModels use CommunityToolkit.Mvvm
  (`[ObservableProperty]`, `[RelayCommand]` — the `Async` suffix is dropped).
- **Data:** shared SQLite (`FlipKitDbContext`) in WAL mode; `decimal` for money,
  `DateTime` for dates, enums stored as strings.
- **DI lifetime rule:** anything taking `FlipKitDbContext` must be **Scoped**.
- **Scanning:** `CompositeScannerService` tries CardSight first, then OpenRouter
  (Ximilar was removed in v3.7.0).
- **Nullable reference types are enabled** — annotate types explicitly.

## Conventions

- Never commit directly to `master`; use `feature/<name>` or `fix/<name>` branches.
- All I/O is `async Task`.
- Register a Core service in every consuming composition root (Desktop/Web/Api).
- Build check: `dotnet build FlipKit.sln`.
