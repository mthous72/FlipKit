# FlipKit

AI-powered inventory management for sports card sellers. Scan cards with your phone, research pricing, and export to Whatnot or eBay.

## Deployment Options

### FlipKit Hub (Desktop + Embedded Servers)
Download from [Releases](https://github.com/mthous72/FlipKit/releases) - includes Desktop app with embedded Web and API servers.

| Platform | Download |
|----------|----------|
| Windows (Installer) | `FlipKit-Setup-v3.3.6.exe` |
| Windows (Portable Hub) | `FlipKit-Hub-Windows-x64-v3.3.6.zip` |
| Linux (Portable Hub) | `FlipKit-Hub-Linux-x64-v3.3.6.zip` |

## Features

- **AI Card Scanning** - Upload photos, AI extracts player, year, set, parallel, serial numbers via the live OpenRouter model catalog (free + paid models, with consent prompt before paid use)
- **Bulk Scanning** - Multi-card front/back batch workflow with progress tracking and rate-limit handling
- **Variation Verification** - Cross-references scans against bundled checklists; user-driven Excel import for additional sets is on the roadmap
- **Pricing Research** - Smart Terapeak/eBay search URLs with customizable templates
- **CSV Export** - Spec-compliant Whatnot and eBay Bulk Upload exports with template-based validation, ImgBB image hosting, and re-export support
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
| AI | OpenRouter API (live model catalog: free + paid vision models) |

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

## Configuration

- **OpenRouter** ([get key](https://openrouter.ai/keys)) - Required for AI scanning (free tier available)
- **ImgBB** ([get key](https://api.imgbb.com/)) - Optional, for image hosting

**Desktop:** Configure via setup wizard or Settings page
**Docker:** Configure at `http://[server-ip]:5000/Settings`

## Disclaimer

FlipKit is provided "as is" for educational purposes. AI-generated card data may contain errors. Verify all identifications and pricing independently.

## License

MIT License - see [LICENSE](LICENSE) for details.
