# FlipKit v3.1.0 Release Notes

**Release Date:** February 11, 2026
**Release Type:** Major Feature Release
**Codename:** Hub

---

## 🎉 Introducing FlipKit Hub

FlipKit v3.1.0 introduces a **unified package architecture** where the Desktop app manages embedded Web and API servers. No more downloading and managing three separate packages - everything is now in one place!

### What's New in v3.1.0

- **Unified Package** - Single download contains Desktop, Web, and API
- **Auto-Start Servers** - Web and API start automatically with Desktop app
- **Server Management UI** - Complete control from Settings → Servers
- **QR Code Access** - Instant mobile connection via QR code scan
- **Live Status Monitoring** - Real-time server status with health checks
- **Server Logs Viewer** - Capture and view stdout/stderr in UI
- **Port Configuration** - User-configurable ports with conflict resolution
- **Clean Shutdown** - Graceful process termination on app exit

---

## 🚀 Major Features

### 1. Unified FlipKit Hub Architecture

**Before (v3.0.0):**
```
- Download FlipKit-Desktop-Windows-x64-v3.0.0.zip (~43 MB)
- Download FlipKit-Web-Windows-x64-v3.0.0.zip (~64 MB)
- Download FlipKit-API-Windows-x64-v3.0.0.zip (~48 MB)
= Three separate packages, manual management, ~155 MB total
```

**After (v3.1.0):**
```
- Download FlipKit-Hub-Windows-x64-v3.1.0.zip (~150 MB)
= One package with everything, auto-managed, ~150 MB total
```

**Benefits:**
- ✅ Single download and extraction
- ✅ Automatic server management
- ✅ Smaller combined package size
- ✅ Better user experience

### 2. Server Management from Settings

**Location:** Settings → Servers

**Features:**
- **Auto-start Configuration**
  - Toggle Web server auto-start
  - Toggle API server auto-start
  - Auto-open browser option
  - Minimize to tray option (future)

- **Port Configuration**
  - Web Server: 5000-9999 (default: 5000)
  - API Server: 5000-9999 (default: 5001)
  - Automatic conflict resolution
  - Shows actual port if different

- **Server Controls**
  - Web Server: [Status] [Start] [Stop] [Open Browser]
  - API Server: [Status] [Start] [Stop]
  - Live status updates every 2 seconds

- **Mobile Access**
  - QR code for instant connection
  - Display all local network IPs
  - Connection instructions

- **Server Logs**
  - Expandable viewer with last 100 lines
  - Separate tabs for Web and API
  - Real-time updates
  - Clear logs buttons

### 3. QR Code for Mobile Connection

Scan and go! No more typing IP addresses.

**How it works:**
1. Launch Desktop app (servers auto-start)
2. Open Settings → Servers
3. Scan QR code with phone camera
4. Phone browser opens to Web UI automatically
5. Start scanning cards!

**Features:**
- Automatically updates with current IP and port
- Regenerates on network change
- Works with Wi-Fi and Tailscale
- 200x200 pixel QR code for easy scanning

### 4. Automatic Port Conflict Resolution

**Problem:** Port already in use by another app

**Solution:** Automatic fallback

**How it works:**
1. Try requested port (e.g., 5000)
2. If in use, try 5001
3. Continue up to 5009
4. UI shows actual port used
5. QR code updates automatically

**No more startup failures!**

### 5. Server Process Lifecycle Management

**ServerManagementService:**
- Spawns Web and API as child processes
- Captures stdout/stderr for logs
- Monitors health via `/health` endpoints
- Detects crashes and updates status
- Graceful shutdown with timeout

**Health Checks:**
- HTTP GET to `/health` every 5 seconds
- Timeout after 2 seconds
- Auto-recovery on process crash

**Clean Shutdown:**
1. Try graceful shutdown (5 seconds)
2. Force kill if needed
3. Dispose all resources
4. No orphaned processes

---

## 📦 Package Structure

### FlipKit Hub Package

```
FlipKit-Hub-Windows-x64-v3.1.0/
├── FlipKit.Desktop.exe          # Main application
├── [Desktop dependencies]
├── servers/
│   ├── FlipKit.Web.exe          # Web server (port 5000)
│   ├── FlipKit.Api.exe          # API server (port 5001)
│   └── [Server dependencies]
├── Docs/
│   ├── USER-GUIDE.md
│   ├── WEB-USER-GUIDE.md
│   ├── DEPLOYMENT-GUIDE.md
│   ├── HUB-ARCHITECTURE.md      # NEW!
│   └── README.md
├── LICENSE
└── README.txt                    # Quick start
```

**Total Size:**
- Windows: ~150 MB
- Linux: ~145 MB

---

## 💡 Use Cases

### Use Case 1: Quick Start

```
1. Download FlipKit-Hub-Windows-x64-v3.1.0.zip
2. Extract anywhere
3. Double-click FlipKit.Desktop.exe
4. Servers auto-start
5. Browser opens to http://localhost:5000
6. Ready to use!
```

### Use Case 2: Mobile Scanning

```
1. Desktop app running on PC
2. Phone on same Wi-Fi
3. Open Settings → Servers in Desktop
4. Scan QR code with phone
5. Start scanning cards from couch!
```

### Use Case 3: Desktop-Only Workflow

```
1. Settings → Servers
2. Uncheck "Start Web server automatically"
3. Uncheck "Start API server automatically"
4. Save
5. Servers won't start on next launch
   (reduced resource usage)
```

---

## 🔧 Technical Details

### New Components

**FlipKit.Core:**
- `IServerManagementService` interface
- `ServerStartResult` model
- `ServerStatus` model
- AppSettings extensions (6 new properties)

**FlipKit.Desktop:**
- `ServerManagementService` implementation
  - Process spawning and monitoring
  - Health check polling
  - Log aggregation (100 lines per server)
  - Port conflict detection
  - Graceful shutdown
- SettingsViewModel extensions
  - 18 new observable properties
  - 8 new commands (Start/Stop/Refresh/Clear)
  - QR code generation (QRCoder library)
  - Network IP detection
- SettingsView extensions
  - New "Local Servers" section
  - Server controls UI
  - QR code display
  - Server logs viewer

**FlipKit.Web:**
- `/health` endpoint

**FlipKit.Api:**
- `/health` endpoint (simple format)

### Build System

**New build-release.ps1:**
- Builds unified Hub packages only
- Windows and Linux targets
- macOS excluded (code signing issues)
- Structure: Desktop in root, Web/API in servers/
- Includes documentation
- Generates README.txt

**Commands:**
```powershell
.\build-release.ps1 -Version 3.1.0
```

**Output:**
- `releases/FlipKit-Hub-Windows-x64-v3.1.0.zip`
- `releases/FlipKit-Hub-Linux-x64-v3.1.0.tar.gz`

### Dependencies Added

- **QRCoder 1.6.0** - QR code generation

### Health Check Endpoints

**Web Server:**
```http
GET http://localhost:5000/health

Response:
{
  "status": "healthy",
  "service": "FlipKit.Web",
  "version": "3.1.0",
  "timestamp": "2026-02-11T..."
}
```

**API Server:**
```http
GET http://localhost:5001/health

Response:
{
  "status": "healthy",
  "service": "FlipKit.Api",
  "version": "3.1.0",
  "timestamp": "2026-02-11T..."
}
```

---

## ⚠️ Breaking Changes

### 1. Separate Packages Deprecated

**Before:** Three separate downloads
- FlipKit-Desktop-*.zip
- FlipKit-Web-*.zip
- FlipKit-API-*.zip

**After:** Single Hub package
- FlipKit-Hub-*.zip

**Migration:** Download new Hub package. Old packages will not receive updates.

### 2. macOS Builds Excluded

**Reason:** Code signing requirements for distributing unsigned apps

**Workaround:** Build from source if needed

**Affected:** macOS Intel and Apple Silicon users

### 3. Build Script Rewritten

**Before:** Builds separate Desktop/Web/API packages

**After:** Builds only unified Hub packages

**Impact:** If you're building from source, update your build scripts

---

## 📝 Migration Guide

### From v3.0.0 Desktop-Only

**Steps:**
1. Download `FlipKit-Hub-Windows-x64-v3.1.0.zip`
2. Extract anywhere
3. Run `FlipKit.Desktop.exe`
4. Servers auto-start (disable in Settings if not needed)

**Data:**
- No migration needed
- Database location unchanged
- Settings preserved

### From v3.0.0 Web + Desktop

**Steps:**
1. Close old Web server (if running)
2. Download `FlipKit-Hub-Windows-x64-v3.1.0.zip`
3. Extract anywhere
4. Run `FlipKit.Desktop.exe`
5. Web server now managed from Settings

**Data:**
- Same database used
- Can run old and new side-by-side
- Delete old packages when ready

### From v3.0.0 API + Desktop

**Steps:**
1. Close old API server (if running)
2. Download `FlipKit-Hub-Windows-x64-v3.1.0.zip`
3. Extract anywhere
4. Run `FlipKit.Desktop.exe`
5. API server now managed from Settings

**Data:**
- Same database used
- Can run old and new side-by-side
- Update Tailscale sync URL if needed

---

## 🐛 Known Issues

None at this time. Report issues at https://github.com/mthous72/FlipKit/issues

---

## 🔮 Future Enhancements

### Planned for v3.2.0

- **System Tray Integration**
  - Minimize to tray
  - Quick menu for server controls
  - Status indicator

- **Advanced Logging**
  - Export logs to file
  - Log level configuration
  - Search/filter logs

### Planned for v4.0.0

- **Authentication**
  - User login for Web
  - API key management
  - Multi-user support

- **HTTPS Support**
  - Self-signed certificates
  - Secure connections

---

## 📚 Documentation

### New Documents

- **HUB-ARCHITECTURE.md** - Complete Hub architecture guide
  - Architecture diagram
  - Component details
  - Server management
  - Process lifecycle
  - Troubleshooting
  - FAQ

### Updated Documents

- **README.md** - Updated for Hub architecture
- **CLAUDE.md** - Updated build commands
- **USER-GUIDE.md** - Added server management section (future)

---

## 💻 System Requirements

### Minimum

- **OS:** Windows 10/11 (x64) or Linux (x64)
- **RAM:** 512 MB available
- **Disk:** 500 MB free space
- **Network:** For mobile access only

### Recommended

- **OS:** Windows 11 (x64) or Ubuntu 22.04+ (x64)
- **RAM:** 1 GB available
- **Disk:** 1 GB free space
- **Network:** 10 Mbps for smooth mobile experience

### Excluded Platforms

- ❌ macOS (code signing requirements)
- ❌ 32-bit systems
- ❌ ARM Windows

---

## 🙏 Credits

**Developed by:** [@mthous72](https://github.com/mthous72)

**With assistance from:** Claude Sonnet 4.5 (AI pair programming)

**Built with:**
- C# / .NET 8
- Avalonia UI 11
- ASP.NET Core 8
- SQLite with EF Core
- QRCoder
- CommunityToolkit.Mvvm
- Bootstrap 5

---

## 📥 Download

**GitHub Release:** https://github.com/mthous72/FlipKit/releases/tag/v3.1.0

**Packages:**
- `FlipKit-Hub-Windows-x64-v3.1.0.zip` (~150 MB)
- `FlipKit-Hub-Linux-x64-v3.1.0.tar.gz` (~145 MB)

**Checksums:** See release assets

---

## 📞 Support

**Documentation:** See `Docs/` folder in package

**Issues:** https://github.com/mthous72/FlipKit/issues

**Discussions:** https://github.com/mthous72/FlipKit/discussions

---

## 📜 License

MIT License - See LICENSE file

---

## 🎯 Summary

FlipKit v3.1.0 "Hub" is a major architectural improvement that unifies all components into a single, easy-to-use package. The new server management UI makes mobile access effortless with QR code scanning, while automatic startup and shutdown eliminate manual server management.

**Upgrade from v3.0.0 and experience the difference!**

---

**Thank you for using FlipKit!** 🎴

---

*Generated on February 11, 2026*
