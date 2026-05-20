# FlipKit Documentation

FlipKit is a C# / .NET 8 application suite for sports-card sellers — desktop app
(Avalonia), web app (ASP.NET Core MVC), API server, and a shared Core library,
shipped together as **FlipKit Hub** (current version **v3.7.0**).

This is the documentation index. Start with the [user guide](guides/user-guide.md)
if you're a seller, or the [architecture overview](architecture/overview.md) if
you're a contributor. The repo-root [`CLAUDE.md`](../CLAUDE.md) is the quickstart
for working in the codebase.

## Architecture

| Doc | What it covers |
|---|---|
| [overview.md](architecture/overview.md) | Hub package, embedded server lifecycle, Desktop MVVM / DI / navigation |
| [data-access.md](architecture/data-access.md) | Local SQLite vs. Remote API (Tailscale) data-access modes |
| [database-schema.md](architecture/database-schema.md) | EF Core entities (Card, PriceHistory, SurpriseSet, SetChecklist) and enums |
| [checklist-data.md](architecture/checklist-data.md) | Checklist data format and import |
| [adr/](architecture/adr/README.md) | Architecture decision records |

## Features

| Doc | What it covers |
|---|---|
| [ai-scanning.md](features/ai-scanning.md) | Scan pipeline: CardSight (first pass) → OpenRouter (fallback), quota panel |
| [verification.md](features/verification.md) | Checklist verification matcher + verified-fields LLM hint mode |
| [pricing-research.md](features/pricing-research.md) | Pricing comps and fee-aware price suggestions |
| [inventory.md](features/inventory.md) | Inventory tracking, status, search/filter |
| [csv-export.md](features/csv-export.md) | Whatnot / eBay / COMC CSV export |
| [ebay-integration.md](features/ebay-integration.md) | eBay APIs — what's possible, listing, pricing comps |
| [image-hosting.md](features/image-hosting.md) | ImgBB image hosting for listings |
| [surprise-sets.md](features/surprise-sets.md) | Whatnot mystery-lot surprise sets + revenue allocation |
| [card-terminology.md](features/card-terminology.md) | Sports-card domain reference |

## Guides

| Doc | What it covers |
|---|---|
| [user-guide.md](guides/user-guide.md) | Full end-user walkthrough |
| [web-guide.md](guides/web-guide.md) | Mobile/web interface guide |
| [install-windows.md](guides/install-windows.md) | Windows install |
| [install-mac.md](guides/install-mac.md) | macOS install |
| [install-linux.md](guides/install-linux.md) | Linux install |
| [deployment.md](guides/deployment.md) | Standalone Web server deployment |
| [tailscale-windows.md](guides/tailscale-windows.md) / [mac](guides/tailscale-mac.md) / [linux](guides/tailscale-linux.md) | Tailscale remote-access setup |

## Development

| Doc | What it covers |
|---|---|
| [claude-code-guide.md](development/claude-code-guide.md) | Working in the codebase with Claude Code / LLM agents |
| [verification-build.md](development/verification-build.md) | Building/testing the verification system |
| [regression-checklist.md](development/regression-checklist.md) | Manual smoke flows before merging |

## Planning (living docs)

| Doc | What it covers |
|---|---|
| [roadmap.md](planning/roadmap.md) | Feature roadmap |
| [integration-roadmap.md](planning/integration-roadmap.md) | eBay / Whatnot integration roadmap |
| [refactor-plan.md](planning/refactor-plan.md) / [refactor-status.md](planning/refactor-status.md) | Refactor plan and live status |
| [audit-2026-05.md](planning/audit-2026-05.md) | Code/doc audit and discovery log |
| [checklist-insider-import-plan.md](planning/checklist-insider-import-plan.md) | Checklist Insider import plan |
| [documentation-cleanup-plan.md](planning/documentation-cleanup-plan.md) | This docs restructure plan + rescan delta |

## Other

- [Windows installer build guide](../installer/README.md) — building the Windows installer.
- [archive/](archive/README.md) — frozen historical content (not maintained).
