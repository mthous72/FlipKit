# ADR-002: net8.0 + net9.0 framework mix

**Status:** Accepted (transitional — see "Future")
**Date:** 2026-05-04
**Related:** [`FlipKit.Api.csproj`](../../FlipKit.Api/FlipKit.Api.csproj), [`FlipKit.Core.csproj`](../../FlipKit.Core/FlipKit.Core.csproj)

## Context

The solution targets two .NET versions:
- **net8.0:** `FlipKit.Core`, `FlipKit.Desktop`, `FlipKit.Web`, all three test projects
- **net9.0:** `FlipKit.Api`

This shows up to anyone running `dotnet --list-sdks` for the first time — they need both 8 and 9 installed.

## Decision

Keep the split. Api gets net9.0; everything else stays on net8.0.

## Why

- **Avalonia 11.3.x's officially supported floor is .NET 8.** Moving Desktop to net9 right now requires either pinning to Avalonia preview builds or accepting unofficial support.
- **Api uses minimal API features and middleware improvements that landed in .NET 9.** The Api server is small, has no UI dependencies, and benefits directly from the net9 ASP.NET Core improvements.
- **EF Core 8 is the current LTS line.** Core's database stack stays on net8 + EF Core 8 to match.
- **net8 → net9 cross-targeting "just works"** at the assembly-reference level: net8 assemblies load cleanly into a net9 host, and the only thing the developer notices is the SDK requirement.

## Consequences

**Positive:**
- Api gets new ASP.NET Core features without being held back by Avalonia's release cadence.
- Core stays on the LTS framework, which matters for SQLite/EF compatibility guarantees.
- No big-bang migration when Avalonia 12 lands — we already understand the cross-targeting pattern.

**Negative:**
- Two SDKs required for development. CI installs both.
- New contributors get tripped up by the version mismatch on first build. The repo [CLAUDE.md](../../CLAUDE.md) calls this out under Important Conventions.
- Slight risk of "works on Api, broken on Desktop" if someone uses a net9-only API in a Core type that Desktop also references. So far this hasn't happened — Core stays carefully net8-compatible.

## Future

Collapse to a single TFM **once Avalonia officially supports the same .NET version we want for the Api side.** Likely path: when Avalonia 12 lands and supports net10 LTS, move the whole solution to net10. Cited in [17-FUTURE-ROADMAP.md](../17-FUTURE-ROADMAP.md) under "Dependency Hygiene".

Until then, the cost of keeping two TFMs is small and the benefit is real. Don't try to unify prematurely by downgrading Api back to net8 — we'd lose the ASP.NET Core 9 improvements for no architectural gain.

## Status

Accepted, transitional. Re-evaluate when Avalonia 12 ships.
