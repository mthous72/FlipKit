# ADR-005: Avalonia UI over MAUI / WPF

**Status:** Accepted
**Date:** 2026-05-04
**Related:** [10-GUI-ARCHITECTURE.md](../10-GUI-ARCHITECTURE.md), [`FlipKit.Desktop/FlipKit.Desktop.csproj`](../../FlipKit.Desktop/FlipKit.Desktop.csproj)

## Context

The Desktop app needed:
- A native window — not a browser tab, not a web view embedded in a thin shell.
- Cross-platform support (the user works on Windows primarily, but wanted Mac/Linux to be on the table).
- An MVVM pattern that supports unit-testable ViewModels independently of any UI runtime.
- Drag-and-drop, native file dialogs, a working DataGrid.
- A self-contained-publish story so users get a single executable, no runtime install.

The candidates evaluated:
- **WPF** — Windows only.
- **MAUI** — cross-platform, Microsoft-blessed, but in 2024 still rough on desktop targets and oriented toward mobile.
- **Avalonia 11.x** — XAML, cross-platform desktop-first, mature DataGrid + drag-drop + Fluent theme.
- **NiceGUI / Web tech** — would have made the Desktop app a browser, contradicting the "native window" requirement.

## Decision

Use **Avalonia UI 11** for the Desktop project. Pair it with **CommunityToolkit.Mvvm** for the MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`).

## Why

- **Cross-platform from day one.** WPF would have permanently locked us to Windows. Even though Windows is the only supported target today, the Linux release zip in `build-release.ps1` works because Avalonia made it a ~one-line investment.
- **XAML skills carry over.** Anyone who's written WPF or UWP can read and write Avalonia XAML with minor adjustments (compiled bindings on by default, slightly different style hierarchy).
- **MAUI's desktop story was immature.** MAUI's primary investment is mobile; desktop targets had (and still have) gaps in DataGrid behavior, drag-drop, and Window APIs.
- **Self-contained publish works cleanly.** `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` produces a single executable. No .NET runtime install needed by the user.
- **MVVM source generators are real.** `[ObservableProperty]` removes the boilerplate that made WPF MVVM painful. The Desktop tests rely on this — they construct ViewModels with mocked services, no Avalonia runtime needed.

## Consequences

**Positive:**
- Linux + Mac releases ship from the same codebase. `build-release.ps1` produces Windows + Linux Hub bundles in one run.
- ViewModels are unit-testable without `Avalonia.Headless` for the vast majority of cases. The 175 Desktop tests in `FlipKit.Desktop.Tests` use plain `xUnit + NSubstitute` with no Avalonia infrastructure.
- The compiled-bindings default catches binding errors at build time, not runtime.

**Negative:**
- **Avalonia.Bitmap requires `AppBuilder` initialization at runtime.** This bit us in Phase 5c.1: the `NetworkAddressProvider` tests can't assert on QR-code bitmap presence because tests run without an `AppBuilder`. The provider's `GenerateQrCodeBitmap` swallows the resulting exception and returns null. Documented in plan §7.4a; `Docs/07-CLAUDE-CODE-GUIDE.md` lists this as a common pitfall.
- **Smaller ecosystem than WPF.** Third-party controls (paid grids, charting libraries) have fewer Avalonia ports. So far we haven't needed any.
- **Some XAML differences from WPF that surprise newcomers.** `IsVisible` instead of `Visibility`, slightly different style selectors, `DataAnnotationsValidationPlugin` is disabled in `App.axaml.cs` to avoid conflicts with CommunityToolkit validation.

## Alternatives considered

- **Stay WPF + accept Windows-only.** Rejected because cross-platform support was a stated requirement.
- **Ship a web-only product (no Desktop).** Rejected because native drag-drop and the AI-vision UX want a real desktop window. The Web app exists but is a *companion* surface, not the primary one. See ADR-001.
- **Use Electron + .NET backend.** Rejected — Electron's resource footprint and update story are worse than Avalonia's, with no offsetting benefit for our use case.

## Status

Accepted. Plan to migrate to Avalonia 12 once it stabilizes (per [17-FUTURE-ROADMAP.md](../17-FUTURE-ROADMAP.md) "Dependency Hygiene").
