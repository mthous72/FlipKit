# FlipKit Hub Architecture

**Document Version:** 1.0
**FlipKit Version:** 3.1.0
**Date:** February 2026

## Overview

FlipKit Hub is a unified application package that combines the Desktop app with embedded Web and API servers. All components are managed from a single Desktop application, providing seamless integration between desktop power features and mobile convenience.

## What is FlipKit Hub?

Instead of downloading and managing three separate packages (Desktop, Web, API), FlipKit Hub provides:

- **Single Download** - One ~150 MB package contains everything
- **Unified Management** - Control all servers from Desktop app Settings
- **Auto-Start** - Servers start automatically when Desktop launches
- **QR Code Access** - Instant mobile connection via QR code scan
- **Clean Shutdown** - All processes stop gracefully on exit

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│ FlipKit Hub Package                                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  FlipKit.Desktop.exe (Main Application)                    │
│  ├─ Avalonia UI 11 (Cross-platform desktop)               │
│  ├─ ServerManagementService (Process lifecycle)            │
│  ├─ Settings UI (Server controls)                          │
│  └─ Auto-start logic                                       │
│                                                             │
│  servers/                                                   │
│  ├─ FlipKit.Web.exe (Web Server)                          │
│  │  ├─ ASP.NET Core MVC                                    │
│  │  ├─ Bootstrap 5 UI                                      │
│  │  ├─ Mobile camera integration                           │
│  │  └─ Port: 5000 (default)                               │
│  │                                                          │
│  └─ FlipKit.Api.exe (API Server)                          │
│     ├─ ASP.NET Core Web API                                │
│     ├─ REST endpoints                                      │
│     ├─ CORS enabled                                        │
│     └─ Port: 5001 (default)                               │
│                                                             │
│  Shared Database: %LOCALAPPDATA%\FlipKit\cards.db         │
│  └─ SQLite with WAL mode (concurrent access)              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. FlipKit.Desktop (Main Application)

**Purpose:** Primary user interface and server orchestrator

**Responsibilities:**
- Provide full desktop UI for all features
- Manage Web and API server lifecycle
- Display server status and logs
- Generate QR codes for mobile access
- Handle graceful shutdown of all processes

**Technology:**
- Avalonia UI 11 (cross-platform)
- .NET 8
- CommunityToolkit.Mvvm

**Key Services:**
- `ServerManagementService` - Process spawning and monitoring
- `ISettingsService` - Configuration persistence
- `INavigationService` - Page navigation

### 2. FlipKit.Web (Web Server)

**Purpose:** Mobile-optimized web interface

**Responsibilities:**
- Serve responsive web UI
- Handle camera uploads from mobile devices
- Provide read/write access to database
- Generate CSV exports
- Display reports and analytics

**Technology:**
- ASP.NET Core 8.0 MVC
- Bootstrap 5
- Razor views
- jQuery for interactivity

**Default Port:** 5000
**Health Check:** `http://localhost:5000/health`

**Routes:**
- `/` - Dashboard/Home
- `/Inventory` - Card management
- `/Scan` - Mobile scanning
- `/Pricing` - Price research
- `/Export` - CSV generation
- `/Reports` - Analytics

### 3. FlipKit.Api (API Server)

**Purpose:** RESTful API for remote access

**Responsibilities:**
- Provide data access via HTTP REST
- Enable Tailscale remote workflows
- Support future mobile apps
- CORS-enabled for web access

**Technology:**
- ASP.NET Core 8.0 Web API
- Minimal APIs
- JSON serialization

**Default Port:** 5001
**Health Check:** `http://localhost:5001/health`

**Endpoints:**
- `/api/cards` - CRUD operations
- `/api/cards/unpriced` - Get unpriced cards
- `/api/cards/stale` - Get stale prices
- `/api/reports/sold` - Sales analytics
- `/api/sync/*` - Sync operations

### 4. Shared Database

**Path:** `%LOCALAPPDATA%\FlipKit\cards.db`

**Technology:** SQLite with Write-Ahead Logging (WAL)

**Why WAL Mode?**
- Allows concurrent reads from Desktop, Web, and API
- No lock contention during simultaneous access
- Automatic checkpoint management
- Better performance under concurrent load

**Schema:** See `02-DATABASE-SCHEMA.md` for complete schema

## Server Management

### Settings UI

Location: **Settings → Servers** in Desktop app

**Auto-start Configuration:**
- ☑ Start Web server automatically on launch
- ☑ Start API server automatically on launch
- ☑ Open browser when Web server starts
- ☑ Minimize to system tray *(future feature)*

**Port Configuration:**
- Web Server Port: 5000-9999 (default: 5000)
- API Server Port: 5000-9999 (default: 5001)
- Changes take effect after server restart

**Server Controls:**
- Web Server: [Status] [Start] [Stop] [Open Browser]
- API Server: [Status] [Start] [Stop]
- Live status updates every 2 seconds

**Mobile Access:**
- Display of all local network IP addresses
- QR code for instant mobile connection
- Instructions for connecting phone

**Server Logs:**
- Expandable section showing last 100 log lines
- Separate tabs for Web and API logs
- Clear logs buttons
- Real-time updates

### Process Lifecycle

**Startup Sequence:**
1. Desktop app launches
2. Load settings from JSON
3. If auto-start enabled:
   - Start Web server on configured port
   - Wait for health check (max 10 seconds)
   - Open browser if configured
   - Start API server on configured port
   - Wait for health check (max 10 seconds)
4. Display main window
5. Start status monitoring timer (2 second interval)

**Health Checks:**
- HTTP GET to `/health` endpoint every 5 seconds
- Response must be 200 OK with JSON body
- If check fails, mark server as crashed
- Desktop UI updates status automatically

**Shutdown Sequence:**
1. User closes Desktop app or clicks Exit
2. Stop status monitoring timer
3. Send graceful shutdown to Web server
   - Try CloseMainWindow() first
   - Wait 5 seconds
   - Force kill if not exited
4. Send graceful shutdown to API server
   - Try CloseMainWindow() first
   - Wait 5 seconds
   - Force kill if not exited
5. Dispose ServerManagementService
6. Close Desktop app

### Port Conflict Resolution

**Problem:** Requested port may already be in use by another application.

**Solution:** Automatic fallback to next available port

**Algorithm:**
1. Try requested port (e.g., 5000)
2. If in use, try 5001
3. If in use, try 5002
4. Continue up to 10 attempts
5. If all fail, show error message

**User Notification:**
- Settings UI shows actual port used
- Log message: "Web port 5000 was in use, using 5001 instead"
- QR code updates to show actual port

### Security Considerations

**Network Binding:**
- Web and API servers bind to `http://0.0.0.0:{port}`
- Accessible on all network interfaces
- No built-in authentication (trust your local network)

**Recommendations:**
- Use firewall to restrict access
- Connect phone via trusted Wi-Fi only
- Consider Tailscale for remote access
- Do not expose to public internet

**Future Enhancements:**
- Authentication/authorization
- HTTPS support
- API key management
- Rate limiting

## Use Cases

### Use Case 1: Mobile Scanning at Home

**Scenario:** User wants to scan cards from couch using phone

**Flow:**
1. Launch Desktop app on PC (servers auto-start)
2. Open Settings → Servers on Desktop
3. Scan QR code with phone camera
4. Phone browser opens to Web UI
5. Tap camera button, scan cards
6. Cards appear in Desktop inventory immediately

**Benefits:**
- No manual server management
- Instant connection via QR code
- Real-time sync via shared database

### Use Case 2: Card Show Scanning

**Scenario:** User at card show wants to scan purchases

**Flow:**
1. Desktop app running at home with servers enabled
2. Phone connected to Tailscale
3. Navigate to Tailscale IP on phone (saved bookmark)
4. Scan cards at show
5. Cards sync to home Desktop

**Benefits:**
- Access from anywhere via Tailscale
- No need to bring laptop
- Data stays private (no cloud)

### Use Case 3: Desktop-Only Workflow

**Scenario:** User doesn't need mobile access

**Flow:**
1. Launch Desktop app
2. Go to Settings → Servers
3. Uncheck "Start Web server automatically"
4. Uncheck "Start API server automatically"
5. Save settings
6. Servers won't start on next launch

**Benefits:**
- Reduced resource usage
- Faster startup
- Simpler workflow

## Troubleshooting

### Web Server Won't Start

**Symptoms:**
- Status shows "Failed: ..."
- Error message in logs

**Causes:**
1. Port 5000-5009 all in use
2. Missing server files
3. Firewall blocking

**Solutions:**
1. Check ports: `netstat -ano | findstr :5000` (Windows)
2. Verify `servers/FlipKit.Web.exe` exists
3. Check firewall rules
4. View logs in Settings → Servers → Server Logs

### API Server Won't Start

**Symptoms:**
- Status shows "Failed: ..."
- Error message in logs

**Causes:**
1. Port 5001-5010 all in use
2. Missing server files
3. Permission issues

**Solutions:**
1. Check ports: `netstat -ano | findstr :5001` (Windows)
2. Verify `servers/FlipKit.Api.exe` exists
3. Run as administrator (if needed)
4. View logs in Settings → Servers → Server Logs

### QR Code Not Displaying

**Symptoms:**
- QR code section is blank

**Causes:**
1. Web server not running
2. No network connection
3. QR code generation failed

**Solutions:**
1. Start Web server manually from Settings
2. Check network connection (Wi-Fi/Ethernet)
3. Restart Desktop app

### Phone Can't Connect

**Symptoms:**
- Browser times out or shows "Cannot connect"

**Causes:**
1. Phone not on same Wi-Fi network
2. Firewall blocking connections
3. Wrong IP address
4. Server not running

**Solutions:**
1. Verify same Wi-Fi: Settings → Wi-Fi (both devices)
2. Disable firewall temporarily to test
3. Check IP in Settings → Servers → Mobile Access
4. Verify Web server status shows "Running"

### Servers Don't Stop on Exit

**Symptoms:**
- Web/API processes still running after Desktop closes
- Task Manager shows orphaned processes

**Causes:**
1. Crash during shutdown
2. Process kill failed
3. Timeout exceeded

**Solutions:**
1. Kill manually: Task Manager → End Task
2. Restart Desktop app (cleanup on startup)
3. Report bug with logs from Docs/debug/

## Performance

### Resource Usage

**Desktop App:**
- Memory: ~80-120 MB idle
- CPU: <1% idle, 5-15% during scanning
- Disk: Minimal (database writes only)

**Web Server:**
- Memory: ~60-80 MB idle
- CPU: <1% idle, 3-8% during requests
- Disk: Minimal

**API Server:**
- Memory: ~40-60 MB idle
- CPU: <1% idle
- Disk: Minimal

**Total Combined:**
- Memory: ~200-260 MB
- CPU: <3% idle
- Startup time: <5 seconds

### Network Traffic

**Local Access:**
- Mobile camera upload: ~2-4 MB per card image
- Page loads: ~50-200 KB
- API calls: ~5-50 KB per request

**Bandwidth Requirements:**
- Minimum: 1 Mbps (basic usage)
- Recommended: 10 Mbps (smooth experience)

## Future Enhancements

### Planned for v3.2.0

- **System Tray Integration**
  - Minimize to tray instead of taskbar
  - Quick menu: Start/Stop servers, Open Web UI, Exit
  - Status indicator (green/red dot)

- **Advanced Logging**
  - Export logs to file
  - Log level configuration
  - Search/filter logs

- **Process Monitoring**
  - Auto-restart crashed servers
  - Crash notifications
  - Process health metrics

### Planned for v4.0.0

- **Authentication**
  - User login for Web interface
  - API key management
  - Multi-user support

- **HTTPS Support**
  - Self-signed certificates
  - Let's Encrypt integration
  - Secure connections

- **Docker Support**
  - Containerized deployment
  - Docker Compose orchestration
  - Easy cloud hosting

## Migration from v3.0.0

### For Desktop-Only Users

**Before (v3.0.0):**
- Download FlipKit-Desktop-Windows-x64-v3.0.0.zip
- Extract and run FlipKit.exe

**After (v3.1.0):**
- Download FlipKit-Hub-Windows-x64-v3.1.0.zip
- Extract and run FlipKit.Desktop.exe
- Servers auto-start (disable in Settings if not needed)

**Data Migration:**
- Automatic - database location unchanged
- No manual steps required

### For Web App Users

**Before (v3.0.0):**
- Download FlipKit-Web-Windows-x64-v3.0.0.zip
- Extract and run StartWeb.bat
- Manually manage Web server

**After (v3.1.0):**
- Download FlipKit-Hub-Windows-x64-v3.1.0.zip
- Extract and run FlipKit.Desktop.exe
- Web server managed from Settings
- Same database location (works side-by-side)

### For API Users

**Before (v3.0.0):**
- Download FlipKit-API-Windows-x64-v3.0.0.zip
- Extract and run StartAPI.bat
- Manually manage API server

**After (v3.1.0):**
- Download FlipKit-Hub-Windows-x64-v3.1.0.zip
- Extract and run FlipKit.Desktop.exe
- API server managed from Settings
- Same database location (works side-by-side)

## FAQ

**Q: Can I still use the old separate packages?**

A: Separate packages are no longer built as of v3.1.0. Use FlipKit Hub instead.

**Q: Can I disable the servers if I don't need them?**

A: Yes! Go to Settings → Servers and uncheck "Start Web server automatically" and "Start API server automatically".

**Q: Do the servers run when Desktop app is closed?**

A: No. Servers automatically stop when you close the Desktop app.

**Q: Can I run multiple instances of FlipKit Hub?**

A: No. Only one instance should run at a time to avoid database conflicts.

**Q: Does this work on macOS?**

A: Not officially. macOS builds are excluded due to code signing requirements. You can build from source if needed.

**Q: Can I change the ports?**

A: Yes! Settings → Servers → Port Configuration. Changes take effect after restarting servers.

**Q: Is my data secure?**

A: Data is stored locally on your machine. Network access is unencrypted HTTP on your local network. Use Tailscale for remote access, and don't expose to public internet.

**Q: How do I access from my phone?**

A: Two options: (1) Scan QR code in Settings → Servers, or (2) Navigate to `http://YOUR-COMPUTER-IP:5000` in phone browser.

**Q: What if I get a port conflict error?**

A: FlipKit automatically tries the next 10 ports. Check Settings → Servers to see which port was actually used, or manually configure different ports.

## Support

**Documentation:**
- See `Docs/` folder for complete guides
- USER-GUIDE.md - Desktop app features
- WEB-USER-GUIDE.md - Web interface
- DEPLOYMENT-GUIDE.md - Advanced setup

**Issues:**
- GitHub: https://github.com/mthous72/FlipKit/issues
- Include logs from `%LOCALAPPDATA%\FlipKit\logs\`
- Specify Windows or Linux
- Describe steps to reproduce

---

**Document End**
