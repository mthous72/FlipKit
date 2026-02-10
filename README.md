# CardLister

A dual-platform application for sports card sellers that uses AI vision to scan card images, manage inventory, research pricing, and export Whatnot-compatible CSV files for bulk listing.

**Two ways to use CardLister:**
- **Desktop App** - Full-featured Windows/macOS application built with Avalonia UI (inventory, export, reports, settings)
- **Web App** - Mobile-optimized interface for on-the-go card scanning and price research only

Built with **C# / .NET 8**, using **Avalonia UI 11** for desktop and **ASP.NET Core MVC** for web.

> **Note:** This is a **production-ready application** with complete features for the core workflow. The desktop app is mature and stable; the web app is newly released for mobile access.

## Download

Pre-built executables are available on the [Releases page](https://github.com/mthous72/CardLister/releases). No .NET runtime install needed -- these are fully self-contained.

### Desktop Application

For power users who want the full desktop experience with bulk scanning and advanced features:

| Platform | File |
|----------|------|
| Windows (x64) | `CardLister-win-x64.zip` -- extract and double-click `CardLister.exe` |
| macOS Intel | `CardLister-osx-x64.zip` -- extract all files to same folder, run `./CardLister` from terminal |
| macOS Apple Silicon (M1/M2/M3/M4) | `CardLister-osx-arm64.zip` -- extract all files to same folder, run `./CardLister` from terminal |

### Web Application

For mobile access - scan cards with your phone's camera and quick price research:

| Platform | File | Usage |
|----------|------|-------|
| Windows (x64) | `CardLister-Web-v2.1.0.zip` | Extract and double-click `StartWeb.bat` |
| macOS Intel | `CardLister-Web-macOS-Intel-v2.1.0.zip` | Extract and run `./start-web.sh` |
| macOS Apple Silicon | `CardLister-Web-macOS-ARM-v2.1.0.zip` | Extract and run `./start-web.sh` |
| Linux (x64) | `CardLister-Web-Linux-v2.1.0.tar.gz` | Extract and run `./start-web.sh` |

**Web App Quick Start:**
1. Download the package for your computer's OS (not your phone!)
2. Extract and run the launcher script
3. Server starts at `http://localhost:5000` (browser opens automatically)
4. On your phone: Connect to same Wi-Fi → Open browser → Go to `http://YOUR-COMPUTER-IP:5000`
5. Use your phone's camera to scan cards and research prices on-the-go!

**Shared Database:** Desktop and Web apps share the same SQLite database, so scanned cards are immediately available in the desktop app for full inventory management.

## What's Working

These features are implemented and functional today:

### Desktop Application Features

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
- **Tailscale Sync** -- Access your card inventory from multiple computers on your private Tailscale network. Run a simple sync server on your main computer, and remote computers automatically sync their local database via secure, encrypted connection. Features timestamp-based conflict resolution (newest wins), auto-sync on startup/exit, and manual sync button. Zero cost, no cloud hosting needed - your data stays on your private network. Perfect for accessing cards from a laptop while traveling or managing inventory from multiple locations.
- **Settings** -- Configure API keys, default AI model, fee percentages (Whatnot/eBay), shipping costs, price staleness threshold, verification preferences, and Tailscale sync. Includes full title and search template customization with validation, preview, and reset options.
- **Setup Wizard** -- First-run walkthrough for entering API keys and setting preferences.
- **Local-First Data** -- All data is stored on your machine in SQLite. API keys are stored in your local app data folder, never in the repo. Optional Tailscale sync keeps data synchronized across your computers via private network.

### Web Application Features (v2.1.0)

Mobile-optimized web interface focused on core on-the-go features:

- **Mobile Camera Scanning** -- Use your phone's camera to scan cards directly from the web browser. Touch-optimized upload interface with instant camera access.
- **Single Card Scanning** -- Scan one card at a time with AI vision (front + optional back photo). Same AI models and verification as desktop app.
- **Pricing Research** -- Quick access to eBay and Terapeak pricing tools. Real-time profit calculator shows fees, revenue, and margin as you type.
- **Shared Database** -- Uses the same SQLite database as the desktop app with Write-Ahead Logging (WAL) for concurrent access. Cards scanned on mobile immediately appear in desktop app for full management.
- **No Installation on Mobile** -- Just open your phone's browser and navigate to your computer's IP address. No app store, no downloads on your phone!
- **Network Access** -- Run the server on your computer, access from any device on your local Wi-Fi network. Perfect for scanning at card shows or quick price checks on-the-go.

**Simplified Architecture (v2.1.0):** The web app focuses exclusively on **scanning and pricing** to provide the best mobile experience. For full inventory management, CSV export, reports, and settings, use the desktop application.

**Use Cases:**
- Scan cards at card shows using your phone's camera
- Quick price checks while shopping for cards
- Research comps from your phone while browsing eBay
- Scan cards away from your desk, manage them later on desktop

## Known Limitations

### Desktop App

Here's what's rough or missing in the desktop application:

- **Checklist data focused on recent years** -- 97 sets are seeded (2017-2024 for major Panini/Topps releases), but older sets and niche brands are not included. Cards from unseeded sets will scan fine but won't get checklist verification.
- **No drag-and-drop** -- Images must be added via the file browser. No clipboard paste either.
- **Batch scanning is new** -- The bulk scan feature was just added and may have rough edges with large batches (50+ cards).
- **No automated tests** -- The codebase has no unit or integration tests yet.
- **macOS builds are untested** -- The macOS executables cross-compile from Windows but haven't been tested on actual Mac hardware. They may need `chmod +x CardLister` and Gatekeeper approval.
- **No auto-update** -- You'll need to download new releases manually.
- **Basic UI polish** -- Uses Avalonia's Fluent theme defaults. No custom icons, splash screen, or loading animations.
- **AI accuracy varies** -- Free vision models are decent but not perfect (~70-80% accuracy on variations). The verification pipeline helps, but edge cases will slip through.
- **Single-window only** -- No multi-window support, no system tray.

### Web App

Limitations specific to the web application:

- **Limited Feature Set (v2.1.0)** -- Web app provides **scanning and pricing only**. For inventory management, CSV export, reports, and settings, use the desktop application. This simplification provides a better mobile experience focused on on-the-go workflows.
- **No Bulk Scanning** -- Web app only supports single-card scanning. Use desktop app for batch scanning (10+ cards).
- **No Settings Configuration** -- API keys and preferences must be configured in the desktop app. Web app reads from the shared `settings.json` file.
- **No Authentication** -- Web app has no login system. Only use on trusted Wi-Fi networks (home/office). Anyone on your network can access it.
- **HTTP Only** -- No HTTPS support yet. Data is not encrypted in transit on your local network.
- **Local Network Only** -- Designed for local Wi-Fi access. Not suitable for internet deployment without authentication and HTTPS.
- **Manual IP Entry** -- You need to find your computer's IP address and type it on your phone. No auto-discovery yet.

## Roadmap

Planned improvements, roughly in priority order:

### Desktop App

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

### Web App

Future enhancements for the web application:

- [ ] **Progressive Web App (PWA)** -- Install web app to phone home screen like a native app, offline support
- [ ] **Authentication** -- Add login system for multi-user support and secure access
- [ ] **HTTPS Support** -- SSL/TLS certificates for encrypted communication
- [ ] **Auto-Discovery** -- Automatic detection of server on local network (no manual IP entry)
- [ ] **Dark Mode** -- Toggle between light and dark themes
- [ ] **Real-Time Sync** -- Live updates using SignalR when changes are made in desktop app

**Note:** Inventory management, export, reports, and settings will remain desktop-only to maintain a focused mobile experience.

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
| Desktop UI | Avalonia UI 11 (Fluent theme) |
| Web UI | ASP.NET Core 8.0 MVC with Razor Views |
| Frontend | Bootstrap 5 (responsive, mobile-first) |
| Architecture | MVVM (Desktop), MVC (Web), 3-Project Structure |
| Database | SQLite via Entity Framework Core with WAL mode |
| AI Vision | OpenRouter API (11 free vision models with automatic fallback) |
| Image Hosting | ImgBB API |
| CSV Export | CsvHelper |
| DI | Microsoft.Extensions.DependencyInjection |

## Project Structure

The codebase uses a **3-project architecture** for maximum code reuse and separation of concerns:

```
CardLister.sln
│
├── CardLister.Core/              # Shared business logic (net8.0 library)
│   ├── Models/                   # Domain entities (Card, PriceHistory, enums)
│   ├── Services/                 # Service interfaces + implementations
│   ├── Data/                     # EF Core DbContext, migrations, seeders
│   └── Helpers/                  # Shared utilities (FuzzyMatcher, etc.)
│
├── CardLister.Desktop/           # Desktop application (Avalonia)
│   ├── ViewModels/               # MVVM ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/                    # Avalonia XAML views
│   ├── Converters/               # XAML value converters
│   ├── Services/                 # Desktop-specific services (file dialogs, etc.)
│   └── Styles/                   # Avalonia styles
│
├── CardLister.Web/               # Web application (ASP.NET Core MVC)
│   ├── Controllers/              # MVC controllers (Home, Scan, Inventory, etc.)
│   ├── Views/                    # Razor views with Bootstrap 5
│   ├── ViewModels/               # DTOs for views
│   ├── Services/                 # Web-specific services (file upload, etc.)
│   └── wwwroot/                  # Static assets (CSS, JS, images)
│
└── Docs/                         # Design specs and planning documents
```

**Code Reuse:** ~55% of the codebase is shared via `CardLister.Core`, including all business logic, database access, AI scanning, pricing, export, and validation.

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run

**Desktop App:**
```bash
git clone https://github.com/mthous72/CardLister.git
cd CardLister
dotnet run --project CardLister
```

**Web App:**
```bash
cd CardLister
dotnet run --project CardLister.Web
# Open browser to http://localhost:5000
```

### Publish Self-Contained Executables

**Desktop App:**
```bash
# Windows
dotnet publish CardLister -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Intel
dotnet publish CardLister -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Apple Silicon
dotnet publish CardLister -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**Web App (Use build scripts for complete packages with launcher scripts and documentation):**
```bash
# Windows - Creates ZIP with StartWeb.bat launcher
./build-web-package.bat

# macOS/Linux - Creates all 3 packages (macOS Intel, macOS ARM, Linux)
bash build-web-package.sh
```

**Or manually publish Web App:**
```bash
# Windows
dotnet publish CardLister.Web/CardLister.Web.csproj -c Release -r win-x64 --self-contained -o publish/web-win

# macOS Intel
dotnet publish CardLister.Web/CardLister.Web.csproj -c Release -r osx-x64 --self-contained -o publish/web-macos-intel

# macOS ARM
dotnet publish CardLister.Web/CardLister.Web.csproj -c Release -r osx-arm64 --self-contained -o publish/web-macos-arm

# Linux
dotnet publish CardLister.Web/CardLister.Web.csproj -c Release -r linux-x64 --self-contained -o publish/web-linux
```

## Configuration

**Desktop App:** On first launch, a setup wizard walks you through entering your API keys.

**Web App:** Configuration must be done via the Desktop app. The Web app reads from the shared `settings.json` file.

**API Keys needed:**
- **OpenRouter API key** ([get one here](https://openrouter.ai/keys)) -- Free to sign up. The app defaults to free vision models, so scanning costs nothing.
- **ImgBB API key** ([get one here](https://api.imgbb.com/)) -- Optional. Only needed if you want to upload images for Whatnot listings.

**File Locations:**
- **Settings:** `%APPDATA%\CardLister\settings.json` (Windows) or `~/Library/Application Support/CardLister/settings.json` (macOS/Linux)
- **Database:** `%APPDATA%\CardLister\cards.db` (Windows) or `~/Library/Application Support/CardLister/cards.db` (macOS) or `~/.local/share/CardLister/cards.db` (Linux)

**Shared Between Apps:** Desktop and Web apps use the same settings.json and cards.db files, so your API keys and inventory are automatically synced. Nothing is sent anywhere except the API calls you initiate.

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

## Disclaimer

**EDUCATIONAL AND INFORMATIONAL USE ONLY**

CardLister is provided for **educational and informational purposes only**. This software is not intended to provide professional accounting, financial, tax, or business advice.

**NO WARRANTIES**

This software is provided "AS IS" without warranty of any kind, either express or implied, including but not limited to:
- Accuracy of AI card identification or pricing data
- Accuracy of financial calculations, profit/loss reporting, or tax information
- Accuracy of data exported to third-party platforms
- Fitness for any particular purpose
- Merchantability or non-infringement

**USE AT YOUR OWN RISK**

By using CardLister, you acknowledge and agree that:
- You are solely responsible for verifying all card identifications, prices, and financial calculations
- You should independently verify all data before making business decisions or filing taxes
- The developers and contributors assume no liability for any losses, damages, or errors resulting from use of this software
- AI-generated card data may contain errors or inaccuracies
- Pricing data from third-party sources may be outdated or incomplete
- You are responsible for complying with all applicable laws, regulations, and marketplace terms of service

**THIRD-PARTY SERVICES**

CardLister integrates with third-party APIs (OpenRouter, ImgBB, eBay) that have their own terms of service. Users are responsible for:
- Obtaining and maintaining valid API keys
- Complying with each service provider's terms and conditions
- Any costs or rate limits imposed by these services
- Ensuring proper use of AI-generated content in accordance with provider policies

**FINANCIAL AND TAX REPORTING**

CardLister's financial reports and profit calculations are **estimates only** and should not be used as the sole basis for:
- Tax filing or reporting
- Business financial statements
- Legal or regulatory compliance
- Investment or business decisions

Consult with qualified accountants, tax professionals, or financial advisors for professional advice.

**NO GUARANTEES**

The developers make no guarantees regarding:
- Uptime, availability, or continued operation of the software
- Compatibility with future versions of dependencies or operating systems
- Data integrity, backup, or recovery
- Support, updates, or bug fixes

Use this software at your own risk. Always maintain backups of your data.

---

## License

MIT

Copyright (c) 2026 CardLister Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
