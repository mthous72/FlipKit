# FlipKit

AI-powered inventory management for sports card sellers. Scan cards with your phone, research pricing, and export to Whatnot.

## Deployment Options

### FlipKit Hub (Desktop + Embedded Servers)
Download from [Releases](https://github.com/mthous72/FlipKit/releases) - includes Desktop app with embedded Web and API servers.

| Platform | Download |
|----------|----------|
| Windows (x64) | `FlipKit-Hub-Windows-x64-v3.2.2.zip` |
| Linux (x64) | `FlipKit-Hub-Linux-x64-v3.2.2.zip` |

### Docker (Headless Server)
Run FlipKit as a headless web server on any Linux machine or NAS:

```bash
# Quick start
docker run -d --name flipkit \
  -p 5000:5000 -p 5001:5001 \
  -v flipkit-data:/data \
  flipkit:latest

# Or with docker-compose
docker-compose up -d
```

Access the web UI at `http://[server-ip]:5000` and configure API keys at `/Settings`.

## Features

- **AI Card Scanning** - Upload photos, AI extracts player, year, set, parallel, serial numbers (11 free vision models via OpenRouter)
- **Variation Verification** - Cross-references against 97 seeded checklists (2017-2024 Panini/Topps sets)
- **Pricing Research** - Smart Terapeak/eBay search URLs with customizable templates
- **Whatnot CSV Export** - SEO-optimized titles for different marketplaces
- **Sales Tracking** - Record sales, calculate profit, generate reports
- **Mobile Scanning** - Camera integration for phone browsers
- **Tailscale Support** - Access your inventory from anywhere on your private network

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Desktop | Avalonia UI 11, .NET 8 |
| Web | ASP.NET Core 8.0 MVC, Bootstrap 5 |
| API | .NET 9 Minimal API |
| Database | SQLite + Entity Framework Core |
| AI | OpenRouter API (free vision models) |

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

# Build release packages
.\build-release.ps1 -Version 3.2.2
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
