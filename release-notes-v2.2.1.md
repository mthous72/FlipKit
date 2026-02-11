# 🐛 Bug Fix Release: v2.2.1

## Critical Fix

**Fixed Database Path Mismatch** - The web app was looking for the database in `AppData\Roaming` instead of `AppData\Local`, causing it to create a separate empty database. This prevented users from seeing their existing inventory when accessing the web app.

### What Changed

- Web app now uses the same database path as Desktop app (`%LOCALAPPDATA%\FlipKit\cards.db`)
- Both apps now properly share the same SQLite database
- Your inventory is now accessible from both Desktop and Web/Mobile interfaces

### Who Should Update

**If you downloaded v2.2.0** and found that your inventory was missing when using the web app, update to v2.2.1 immediately.

If you haven't installed v2.2.0 yet, skip it and install v2.2.1 directly.

---

## Full v2.2.x Feature Set

All features from v2.2.0 are included:

**Smart Hybrid Data Access**
- Automatic detection of local vs remote mode
- Fast local SQLite access when on same computer
- API-based access when remote via Tailscale

**Full Mobile Inventory Management**
- Browse, search, filter cards from phone
- Edit card details and pricing from mobile
- Delete cards with confirmation
- CSV export directly from phone
- Sales reports and analytics

**Tailscale Network Support**
- Access your cards from anywhere on private Tailscale network
- Secure, encrypted connections
- Works with Desktop, Web, and API

**API Server**
- RESTful data access API
- 11+ endpoints for complete CRUD operations
- Health check endpoint for easy testing

---

# 📥 Downloads

## Desktop Application

For full-featured desktop experience with bulk scanning:

- **Windows (x64)** - Extract and double-click `FlipKit.exe`
- **macOS Intel** - Extract and run `./FlipKit` from terminal
- **macOS Apple Silicon** - Extract and run `./FlipKit` from terminal

## Web Application

For mobile access - run on your computer, access from phone:

- **Windows (x64)** - Extract and double-click `StartWeb.bat`
- **macOS Intel** - Extract and run `./start-web.sh`
- **macOS Apple Silicon** - Extract and run `./start-web.sh`
- **Linux (x64)** - Extract and run `./start-web.sh`

## API Server

For remote Desktop/Web app access via Tailscale:

- **Windows (x64)** - Extract and double-click `StartAPI.bat`
- **macOS Intel** - Extract and run `./start-api.sh`
- **macOS Apple Silicon** - Extract and run `./start-api.sh`
- **Linux (x64)** - Extract and run `./start-api.sh`

---

# 🆕 First Time Setup

1. **Desktop/Web:** Extract and run - includes launcher scripts
2. **API (Optional):** Only needed for remote access via Tailscale
3. **Configure:** Enter OpenRouter and ImgBB API keys in Settings
4. **Tailscale (Optional):** Install for remote access features

See [README](https://github.com/mthous72/FlipKit#readme) for detailed setup instructions.

---

# 📚 Documentation

- [README](https://github.com/mthous72/FlipKit#readme) - Full feature list and setup
- [TAILSCALE-SYNC-GUIDE.md](https://github.com/mthous72/FlipKit/blob/master/TAILSCALE-SYNC-GUIDE.md) - Remote access setup
- [WEB-USER-GUIDE.md](https://github.com/mthous72/FlipKit/blob/master/Docs/WEB-USER-GUIDE.md) - Mobile app usage

---

# 🙏 Feedback & Issues

Found a bug or have a suggestion? [Open an issue](https://github.com/mthous72/FlipKit/issues)!
