# FlipKit

AI-powered inventory management for sports card sellers. Scan cards with your phone, research pricing, and export to Whatnot.

**FlipKit Hub** is a unified package containing:
- **Desktop App** - Full-featured Avalonia UI application (Windows/Linux)
- **Web Server** - Mobile-optimized interface for on-the-go scanning
- **API Server** - Remote access for multi-device workflows

Built with C# / .NET 8, Avalonia UI 11, and ASP.NET Core MVC.

## Quick Start

1. Download from [Releases](https://github.com/mthous72/FlipKit/releases)
2. Extract and run `FlipKit.Desktop.exe`
3. Complete the setup wizard (enter your free [OpenRouter API key](https://openrouter.ai/keys))
4. On your phone, scan the QR code from Settings → Servers

| Platform | Download |
|----------|----------|
| Windows (x64) | `FlipKit-Hub-Windows-x64-v3.2.0.zip` |
| Linux (x64) | `FlipKit-Hub-Linux-x64-v3.2.0.zip` |

## Features

### Core Workflow
- **AI Card Scanning** - Upload photos, AI extracts player, year, set, parallel, serial numbers, and more (11 free vision models)
- **Variation Verification** - Cross-references against 97 seeded checklists (2017-2024 Panini/Topps sets)
- **Pricing Research** - Smart Terapeak/eBay search URLs with customizable templates
- **Whatnot CSV Export** - SEO-optimized titles for different marketplaces
- **Sales Tracking** - Record sales, calculate profit, generate tax reports

### Mobile (Web App)
- **Camera Scanning** - Scan cards directly from your phone's browser
- **Buying Mode** - Quick comp research without saving to inventory
- **Selling Mode** - Full catalog building workflow
- **Shared Database** - SQLite with WAL mode for concurrent Desktop + Mobile access

### Multi-Device Access
- **Tailscale Support** - Access your inventory from anywhere on your private network
- **Auto-Detection** - Switches between local database and API mode automatically
- **QR Code Connection** - Instant mobile setup from Desktop settings

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Desktop | Avalonia UI 11, CommunityToolkit.Mvvm |
| Web | ASP.NET Core 8.0 MVC, Bootstrap 5 |
| Database | SQLite + Entity Framework Core |
| AI | OpenRouter API (free vision models) |
| Image Hosting | ImgBB API |

## Building from Source

```bash
# Prerequisites: .NET 8 SDK

git clone https://github.com/mthous72/FlipKit.git
cd FlipKit

# Run desktop app
dotnet run --project FlipKit.Desktop

# Run web app standalone
dotnet run --project FlipKit.Web

# Build release packages
.\build-release.ps1 -Version 3.2.0
```

## Configuration

API keys are configured in the Desktop app's setup wizard or Settings page:

- **OpenRouter** ([get key](https://openrouter.ai/keys)) - Required for AI scanning (free tier available)
- **ImgBB** ([get key](https://api.imgbb.com/)) - Optional, for image hosting

Data is stored locally:
- Windows: `%APPDATA%\FlipKit\`
- Linux: `~/.local/share/FlipKit/`

## Known Limitations

- Checklist data covers 2017-2024 major sets only
- No drag-and-drop or clipboard paste for images
- Web app has no authentication (use on trusted networks)
- macOS builds untested

## Contributing

Contributions welcome! See [issues](https://github.com/mthous72/FlipKit/issues) for ideas:
- Report bugs with reproduction steps
- Add checklist data for more sets
- Write tests (currently none)
- Test on macOS

## Disclaimer

FlipKit is provided "as is" for educational purposes. AI-generated card data may contain errors. Verify all identifications and pricing independently. Not intended for professional accounting or tax advice. See [LICENSE](LICENSE) for full terms.

## License

MIT License - see [LICENSE](LICENSE) for details.
