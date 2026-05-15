# FlipKit

AI-powered inventory management for sports card sellers. Scan cards with your phone, research pricing, and export to Whatnot or eBay.

## Deployment Options

### FlipKit Hub (Desktop + Embedded Servers)
Download from [Releases](https://github.com/mthous72/FlipKit/releases) - includes Desktop app with embedded Web and API servers.

| Platform | Download |
|----------|----------|
| Windows (Installer) | `FlipKit-Setup-v3.6.0.exe` |
| Windows (Portable) | `FlipKit-Hub-Windows-x64-v3.6.0.zip` |
| macOS Apple Silicon (M1+) | `FlipKit-macOS-Apple-Silicon-v3.6.0.dmg` |
| macOS Intel | `FlipKit-macOS-Intel-v3.6.0.dmg` |
| Linux (Portable) | `FlipKit-Hub-Linux-x64-v3.6.0.zip` |

## Features

- **AI Card Scanning** - Upload photos, AI extracts player, year, set, parallel, serial numbers via the live OpenRouter model catalog (free + paid models, with consent prompt before paid use)
- **CardSight First-Pass Recognition (optional)** - If a CardSight API key is configured, FlipKit hits CardSight first (purpose-built sports-card recognition, 750 free identifications/month) and falls back to OpenRouter on miss / low confidence — preserving OpenRouter quota for cards CardSight can't identify
- **Bulk Scanning** - Multi-card front/back batch workflow with progress tracking and rate-limit handling
- **Variation Verification** - Cross-references scans against bundled checklists; user-driven Excel import for additional sets is on the roadmap
- **Pricing Research** - Smart Terapeak/eBay search URLs with customizable templates
- **CSV Export** - Spec-compliant Whatnot and eBay Bulk Upload exports with template-based validation, ImgBB image hosting, and re-export support
- **eBay Listings Import** - Import an eBay Seller Hub "All active listings" CSV export into the inventory; deterministic regex pass + LLM second pass enrich each title, eBay item number is the upsert key so re-imports stay clean
- **Sales Tracking** - Record sales, calculate profit, generate reports
- **Mobile Scanning** - Camera integration for phone browsers via the Web app
- **Webcam Capture** - Capture card images directly from a laptop webcam in both the Desktop app (OpenCvSharp4) and Web browser (`getUserMedia`); see [Docs/27-WEBCAM-CAPTURE-PLAN.md](Docs/27-WEBCAM-CAPTURE-PLAN.md). Browser capture requires HTTPS or `localhost` — Tailscale-over-HTTP falls back to file picker / phone upload.
- **Tailscale Support** - Access your inventory from anywhere on your private network (see [Docs/Tailscale-Sync-Architecture.md](Docs/Tailscale-Sync-Architecture.md))

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Desktop | Avalonia UI 11, .NET 8 |
| Web | ASP.NET Core 8.0 MVC, Bootstrap 5 |
| API | .NET 9 Minimal API |
| Database | SQLite + Entity Framework Core |
| AI | OpenRouter API (live model catalog: free + paid vision models), optional CardSight first-pass recognition |

## Building from Source

```bash
# Prerequisites: .NET 8 SDK, .NET 9 SDK (for API)

git clone https://github.com/mthous72/FlipKit.git
cd FlipKit

# Run desktop app
dotnet run --project FlipKit.Desktop

# Run web app standalone
dotnet run --project FlipKit.Web

# Build Docker image
docker build -t flipkit:latest .

# Build release packages (Hub zips for Windows + Linux)
.\build-release.ps1 -Version 3.3.6

# Build the Windows Inno Setup installer
.\build-hub-for-installer.ps1
```

## Getting Started

FlipKit's AI scanning requires a free OpenRouter account. Image hosting for CSV exports requires a free ImgBB account. eBay listing creation requires a free eBay Developer account.

**Step 1 — OpenRouter (required for AI scanning)**
1. Create a free account at [openrouter.ai](https://openrouter.ai)
2. Go to [openrouter.ai/keys](https://openrouter.ai/keys) and generate an API key
3. Paste it into FlipKit under **Settings → OpenRouter API Key**

Free-tier models (e.g. Gemini Flash) are available at no cost. Paid models offer higher accuracy and are opt-in — FlipKit will prompt for confirmation before using any paid model.

**Step 2 — CardSight (optional, but recommended to save OpenRouter quota)**
1. Create a free account at [cardsight.ai](https://cardsight.ai/)
2. Open the developer dashboard and copy your API key (see the [API docs](https://api.cardsight.ai/documentation) for reference)
3. Paste it into FlipKit under **Settings → CardSight API Key** and click **Test** to verify

When configured, FlipKit tries CardSight first on every scan and only falls back to OpenRouter on miss or low confidence. CardSight currently includes **750 free card identifications per month** per key, which is enough for most casual sellers. No key? FlipKit works fine — scans go straight to OpenRouter.

**Step 3 — ImgBB (optional, for image hosting in CSV exports)**
1. Create a free account at [imgbb.com](https://imgbb.com)
2. Go to [api.imgbb.com](https://api.imgbb.com/) and generate an API key
3. Paste it into FlipKit under **Settings → ImgBB API Key**

ImgBB is only needed if you want card image URLs embedded in your Whatnot or eBay CSV exports. Everything else works without it.

**Step 4 — eBay Developer credentials (optional, for direct listing creation)**
1. Create a free account at [developer.ebay.com](https://developer.ebay.com)
2. Go to [developer.ebay.com/my/keys](https://developer.ebay.com/my/keys)
3. Click **"Get a Production Keyset"** (or create an app if you don't have one)
4. Copy the **App ID (Client ID)** and **Client Secret** from your Production keyset
5. Paste both into FlipKit under **Settings → eBay API Credentials** and click **Test** to verify

> **Note:** eBay requires a one-time production access approval for the Sell Inventory API. Submit a support ticket at [developer.ebay.com](https://developer.ebay.com/support) explaining your use case (personal selling app) before attempting to publish live listings.

## Configuration

| Service | Where to get it | Required for |
|---------|----------------|-------------|
| **OpenRouter** | [openrouter.ai/keys](https://openrouter.ai/keys) | AI card scanning (free tier available) |
| **CardSight** | [cardsight.ai](https://cardsight.ai/) | First-pass sports-card recognition before OpenRouter (optional, 750 free identifications/month) |
| **ImgBB** | [api.imgbb.com](https://api.imgbb.com/) | Image URLs in CSV exports (optional) |
| **Ximilar** | [ximilar.com](https://www.ximilar.com/) | Legacy — no longer in the active scan chain (CardSight replaced it). Key field retained for manual use |
| **eBay Client ID + Secret** | [developer.ebay.com/my/keys](https://developer.ebay.com/my/keys) | Direct eBay listing creation (optional) |

**Desktop:** Configure via the Settings page (gear icon in the sidebar)
**Web:** Configure at `http://[server-ip]:5000/Settings` (Docker/remote mode only)

## Disclaimer

**FlipKit is provided "AS IS" without warranty of any kind, express or implied.**

**AI accuracy.** Card identification is performed by third-party AI services (OpenRouter, CardSight). AI output is probabilistic and will contain errors — wrong players, wrong sets, wrong parallels, wrong serial numbers. Every scan result must be verified by a human before being acted on. FlipKit makes no representation that AI identifications are correct, complete, or suitable for any purpose.

**Financial decisions.** Pricing data shown in FlipKit (eBay sold comps, Terapeak research) is pulled from public sources for reference only. It is not investment advice, and it is not a guarantee of what a card will sell for. Do not use FlipKit output as the sole basis for buying, selling, grading, or insuring cards. Card values fluctuate and individual sale prices vary widely based on condition, timing, platform, and buyer demand. You bear full responsibility for any financial decisions you make.

**No professional advice.** FlipKit is not a substitute for professional accounting, tax, legal, or financial advice. Consult a qualified professional for questions about taxes on card sales, business structuring, or insurance valuation.

**Use at your own risk.** By using this software you accept sole responsibility for verifying data accuracy, complying with applicable laws (including sales tax obligations, platform terms of service, and export controls), and all outcomes resulting from your use of FlipKit.

## License

MIT License - see [LICENSE](LICENSE) for details.
