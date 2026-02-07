# CardLister

A desktop application for sports card sellers that uses AI vision to scan card images, manage inventory, research pricing, and export Whatnot-compatible CSV files for bulk listing.

Built with **C# / .NET 8**, **Avalonia UI 11**, and the **MVVM pattern**.

> **Note:** This is a **minimally viable product (MVP)**. The core workflow -- scan, save, price, export -- is functional, but many features are early-stage and actively being improved. Expect rough edges, and please report issues or contribute if you're interested.

## Download

Pre-built executables are available on the [Releases page](https://github.com/mthous72/CardLister/releases). No .NET runtime install needed -- these are fully self-contained.

| Platform | File |
|----------|------|
| Windows (x64) | `CardLister-win-x64.zip` -- extract and double-click `CardLister.exe` |
| macOS Intel | `CardLister-osx-x64.zip` -- extract all files to same folder, run `./CardLister` from terminal |
| macOS Apple Silicon (M1/M2/M3/M4) | `CardLister-osx-arm64.zip` -- extract all files to same folder, run `./CardLister` from terminal |

## What's Working (MVP)

These features are implemented and functional today:

- **AI Card Scanning** -- Browse for a photo of any sports card (front + optional back) and AI vision extracts player name, year, set, brand, parallel, serial numbering, and more. Uses free OpenRouter vision models with automatic fallback across 11 models on rate limiting.
- **Bulk Scan** -- Select multiple card images at once, optionally pair front/back images, and scan them all in one batch with progress tracking and cancellation support. Rate-limiting built in for free-tier models.
- **Variation Verification** -- Cross-references AI scan results against local set checklists to catch hallucinated parallels, correct player names, and validate card numbers. Includes a targeted confirmation pass for ambiguous results. **Currently ships with 97 seeded checklists** covering major sets from 2017-2024:
  - **Football (48 sets):** Panini Prizm, Donruss, Donruss Optic, Mosaic, Phoenix, Select (2017-2024)
  - **Basketball (21 sets):** Panini Prizm, Donruss Optic, Mosaic (2018-2024)
  - **Baseball (28 sets):** Topps Chrome, Bowman, Bowman Chrome, Topps (2018-2024)
- **Inventory Grid** -- Browse, search, filter, and sort your card collection in a DataGrid. Track card status (Draft, Priced, Listed, Sold) and price staleness indicators.
- **Pricing Workflow** -- Step through unpriced cards one at a time with intelligent Terapeak and eBay sold listing links that open in your browser. Search URLs use **customizable templates** to include relevant card details (year, manufacturer, brand, player, parallel, team, and grading info) while excluding overly specific fields like card number and serial number for broader results. For graded cards, searches include "graded [grade]" to find comparable sales across **all grading companies** (PSA, BGS, CGC, SGC). Enter market value, get a suggested list price, and save with cost basis tracking.
- **Reprice Feature** -- Send any priced card back to the pricing queue from the Inventory view to update market values or correct pricing mistakes.
- **Stale Price Repricing** -- Cards whose price is older than a configurable threshold (default 30 days) are flagged. Walk through them to keep or update prices.
- **Mark as Sold** -- Record sale price, platform (Whatnot/eBay), fees, and shipping cost. Net profit is auto-calculated.
- **Delete Cards** -- Remove cards from inventory with a confirmation dialog.
- **ImgBB Image Upload** -- Batch upload card images to ImgBB for free public URLs compatible with Whatnot.
- **Whatnot CSV Export** -- Generate properly formatted CSV files with SEO-optimized, platform-specific titles and descriptions. Choose export platform (Whatnot/eBay/COMC/Generic) to use the appropriate title template for that marketplace's search algorithm.
- **Tax Report Export** -- Export sold cards as a CSV with sale date, cost basis, fees, and net profit for record-keeping.
- **Reports** -- View sold card summary with total revenue, cost basis, fees, and net profit. Monthly breakdown and top sellers list.
- **Listing Title Optimization** -- SEO-optimized title templates for different platforms (Whatnot, eBay, COMC, Generic). Customize title format per platform using placeholders like `{Year} {Brand} {Player} {Parallel}`. Search query templates optimize pricing research by excluding overly specific fields. Export platform selector lets you choose which template to use per-export. Based on WTSCards research showing different platforms weight fields differently for search ranking.
- **Settings** -- Configure API keys, default AI model, fee percentages (Whatnot/eBay), shipping costs, price staleness threshold, and verification preferences. Includes full title and search template customization with validation, preview, and reset options.
- **Setup Wizard** -- First-run walkthrough for entering API keys and setting preferences.
- **Local-First Data** -- All data is stored on your machine in SQLite. API keys are stored in your local app data folder, never in the repo.

## Known Limitations

This is an MVP -- here's what's rough or missing:

- **Checklist data focused on recent years** -- 97 sets are seeded (2017-2024 for major Panini/Topps releases), but older sets and niche brands are not included. Cards from unseeded sets will scan fine but won't get checklist verification.
- **No drag-and-drop** -- Images must be added via the file browser. No clipboard paste either.
- **Batch scanning is new** -- The bulk scan feature was just added and may have rough edges with large batches (50+ cards).
- **No automated tests** -- The codebase has no unit or integration tests yet.
- **macOS builds are untested** -- The macOS executables cross-compile from Windows but haven't been tested on actual Mac hardware. They may need `chmod +x CardLister` and Gatekeeper approval.
- **No auto-update** -- You'll need to download new releases manually.
- **Basic UI polish** -- Uses Avalonia's Fluent theme defaults. No custom icons, splash screen, or loading animations.
- **AI accuracy varies** -- Free vision models are decent but not perfect (~70-80% accuracy on variations). The verification pipeline helps, but edge cases will slip through.
- **Single-window only** -- No multi-window support, no system tray.

## Roadmap

Planned improvements, roughly in priority order:

- [ ] **Expand checklist database** -- Add pre-2017 sets, niche brands (Leaf, Sage, Upper Deck), and international releases
- [ ] **Drag-and-drop image support** -- Drop card photos directly onto the scan area
- [ ] **Unit and integration tests** -- Cover ViewModels, services, and the verification pipeline
- [ ] **macOS .app bundle** -- Proper macOS application bundle with icon and Info.plist so it behaves like a native app
- [ ] **Linux support** -- Test and publish Linux builds
- [ ] **Improved AI prompts** -- Fine-tune prompts for better accuracy on parallels, inserts, and multi-sport sets
- [ ] **Per-field confidence indicators** -- Show colored dots next to each form field after scanning to indicate AI confidence
- [ ] **Drag-to-reorder columns** -- Persist user's preferred column order in the inventory grid
- [ ] **Dark mode toggle** -- Currently follows system theme; add an explicit toggle in settings
- [ ] **Auto-update** -- Check GitHub releases for new versions on startup
- [ ] **Clipboard paste** -- Paste images directly from clipboard or screenshots
- [ ] **Multi-card images** -- Detect and split photos containing multiple cards
- [ ] **Price history charts** -- Visualize price trends over time for individual cards
- [ ] **Bulk edit** -- Select multiple cards and update fields (status, price, etc.) in one action

## How It Works

```
1. CAPTURE      Take a photo of your card (front + optional back)
       |
2. AI SCAN      AI vision extracts structured card data (player, year, set, parallel, etc.)
       |
3. VERIFY       Cross-reference against set checklists to validate identification
       |
4. REVIEW       Edit extracted data in a form, save to local SQLite database
       |
5. PRICE        Research comps via Terapeak/eBay, set your listing price
       |
6. EXPORT       Generate Whatnot CSV, upload to Seller Hub, publish
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Language | C# / .NET 8 |
| UI Framework | Avalonia UI 11 (Fluent theme) |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Database | SQLite via Entity Framework Core |
| AI Vision | OpenRouter API (11 free vision models with automatic fallback) |
| Image Hosting | ImgBB API |
| CSV Export | CsvHelper |
| DI | Microsoft.Extensions.DependencyInjection |

## Project Structure

```
CardLister/
├── Models/           Domain entities (Card, PriceHistory, AppSettings, enums)
├── ViewModels/       MVVM ViewModels with observable properties and commands
├── Views/            Avalonia XAML views (no business logic in code-behind)
├── Services/         Service interfaces and implementations (scanning, export, pricing)
├── Data/             EF Core DbContext, seeders, schema management
├── Converters/       XAML value converters (currency, status badges, confidence colors)
├── Helpers/          Utility classes (FuzzyMatcher, PriceCalculator)
├── Styles/           Shared Avalonia styles
└── Docs/             Design specs and planning documents
```

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run

```bash
git clone https://github.com/mthous72/CardLister.git
cd CardLister
dotnet run --project CardLister
```

### Publish Self-Contained Executables

```bash
# Windows
dotnet publish CardLister -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Intel
dotnet publish CardLister -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Apple Silicon
dotnet publish CardLister -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Configuration

On first launch, a setup wizard walks you through entering your API keys:

- **OpenRouter API key** ([get one here](https://openrouter.ai/keys)) -- Free to sign up. The app defaults to free vision models, so scanning costs nothing.
- **ImgBB API key** ([get one here](https://api.imgbb.com/)) -- Optional. Only needed if you want to upload images for Whatnot listings.

All settings are stored locally in `%LocalAppData%\CardLister\config.json` (Windows) or `~/Library/Application Support/CardLister/config.json` (macOS). Your card database is stored alongside it as `cards.db`. Nothing is sent anywhere except the API calls you initiate.

## Supported Sports

- Football
- Baseball
- Basketball

## Contributing

This is an early-stage project and contributions are welcome. If you're a sports card seller, a .NET/Avalonia developer, or just interested in AI-powered tools, here are some ways to help:

- **Report bugs** -- [Open an issue](https://github.com/mthous72/CardLister/issues) with steps to reproduce
- **Test on macOS** -- The Mac builds haven't been verified on real hardware yet. If you have a Mac, try it out and let us know what happens
- **Add checklist data** -- The variation verification pipeline is only as good as its data. If you know a set well, help us build out the checklist database
- **Improve AI prompts** -- If the scanner consistently misidentifies a particular type of card, share examples so we can refine the prompts
- **Write tests** -- The codebase has zero tests. Pick a ViewModel or service and add coverage
- **UI/UX improvements** -- The interface is functional but plain. Designers and frontend developers welcome
- **Feature development** -- Check the roadmap above and pick something that interests you

## License

MIT
