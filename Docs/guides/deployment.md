# FlipKit Web - Deployment Guide

**Version:** 1.0
**Last Updated:** February 7, 2026

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Local Development Setup](#local-development-setup)
4. [Running on Local Network](#running-on-local-network)
5. [Production Deployment Options](#production-deployment-options)
6. [Configuration](#configuration)
7. [Firewall Configuration](#firewall-configuration)
8. [HTTPS Setup (Optional)](#https-setup-optional)
9. [Troubleshooting](#troubleshooting)

---

## Overview

FlipKit Web is an ASP.NET Core 8.0 MVC application designed to run on your local computer and be accessed from mobile devices on the same Wi-Fi network. It shares a SQLite database with the FlipKit Desktop (Avalonia) application.

### Architecture

```
┌─────────────────┐
│  Desktop App    │──┐
│  (Avalonia)     │  │
└─────────────────┘  │
                     ├──→ SQLite Database (WAL mode)
┌─────────────────┐  │    %APPDATA%\FlipKit\cards.db
│   Web App       │──┘
│  (ASP.NET MVC)  │
└─────────────────┘
       │
       ↓
  Port 5000 (HTTP)
       │
    ┌──┴───┬───────┬────────┐
    │      │       │        │
  Phone Tablet Desktop   Other
```

### Key Features

- Shared database with concurrent access (WAL mode)
- No authentication (local network only)
- Self-contained deployment
- Minimal configuration required

---

## Requirements

### System Requirements

**Operating System:**
- Windows 10/11 (x64)
- macOS 10.15+ (x64 or ARM64)
- Linux (x64, ARM64, ARM)

**Software:**
- .NET 8.0 SDK (for building from source)
- .NET 8.0 Runtime (for running published app)
- Web browser (Chrome, Edge, Firefox, Safari)

**Recommended:**
- 4GB RAM minimum
- 500MB free disk space
- SSD for better database performance

### Network Requirements

**For Local Access Only:**
- No special requirements

**For Mobile Device Access:**
- Computer and mobile device on same Wi-Fi network
- Firewall configured to allow port 5000 (TCP)
- Static IP address recommended (or DHCP reservation)

---

## Local Development Setup

### Option 1: Run from Source

**Prerequisites:**
- .NET 8.0 SDK installed
- Git (to clone repository)

**Steps:**

1. **Clone Repository**
   ```bash
   git clone https://github.com/mthous72/FlipKit.git
   cd FlipKit
   ```

2. **Build Project**
   ```bash
   dotnet restore
   dotnet build
   ```

3. **Run Web App**
   ```bash
   cd FlipKit.Web
   dotnet run
   ```

4. **Access Application**
   - Open browser to: http://localhost:5000

5. **Stop Server**
   - Press `Ctrl+C` in terminal

### Option 2: Run Published Release

**Prerequisites:**
- .NET 8.0 Runtime installed

**Steps:**

1. **Download Release**
   - Go to https://github.com/mthous72/FlipKit/releases
   - Download `FlipKit.Web-vX.X.X.zip`

2. **Extract Files**
   - Extract to folder (e.g., `C:\FlipKit\Web`)

3. **Run Application**
   ```bash
   cd C:\FlipKit\Web
   dotnet FlipKit.Web.dll
   ```

4. **Access Application**
   - Open browser to: http://localhost:5000

---

## Running on Local Network

To access the web app from your phone or tablet on the same Wi-Fi network.

### Step 1: Find Your Computer's IP Address

**Windows:**
```powershell
ipconfig
```
Look for "IPv4 Address" under your Wi-Fi adapter (e.g., `192.168.1.100`)

**macOS/Linux:**
```bash
ifconfig
# or
ip addr show
```
Look for `inet` address on your Wi-Fi interface (e.g., `192.168.1.100`)

### Step 2: Configure Firewall

**Windows Firewall:**

1. Open "Windows Defender Firewall with Advanced Security"
2. Click "Inbound Rules" → "New Rule"
3. Rule Type: Port
4. Protocol: TCP, Specific local ports: `5000`
5. Action: Allow the connection
6. Profile: Private, Domain (do NOT check Public for security)
7. Name: "FlipKit Web"
8. Click Finish

**Or via PowerShell (Run as Administrator):**
```powershell
New-NetFirewallRule -DisplayName "FlipKit Web" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow -Profile Private
```

**macOS:**
```bash
# macOS firewall typically allows localhost access by default
# If firewall is enabled, add FlipKit.Web to allowed apps in System Preferences → Security & Privacy → Firewall
```

**Linux (ufw):**
```bash
sudo ufw allow 5000/tcp
```

### Step 3: Start Web App Listening on All Interfaces

**Option A: Use Launch Settings (Development)**

Edit `FlipKit.Web/Properties/launchSettings.json`:
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://0.0.0.0:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Then run:
```bash
dotnet run
```

**Option B: Set URLs via Command Line**

```bash
dotnet run --urls "http://0.0.0.0:5000"
```

**Option C: Set Environment Variable**

Windows:
```powershell
$env:ASPNETCORE_URLS="http://0.0.0.0:5000"
dotnet run
```

macOS/Linux:
```bash
export ASPNETCORE_URLS="http://0.0.0.0:5000"
dotnet run
```

### Step 4: Access from Mobile Device

1. **Ensure mobile device is on same Wi-Fi network**
2. **Open mobile browser**
3. **Navigate to:** `http://YOUR-COMPUTER-IP:5000`
   - Example: `http://192.168.1.100:5000`
4. **Bookmark for future access**

### Troubleshooting Network Access

**Can't connect from phone:**
- ✅ Verify same Wi-Fi network
- ✅ Check firewall rule exists
- ✅ Confirm app is listening on `0.0.0.0` (not `localhost`)
- ✅ Ping computer from phone to test connectivity
- ✅ Try different browser on phone
- ✅ Restart web app with correct URL binding

**Connection refused:**
- Check web app is actually running
- Verify port 5000 is not in use by another application
- Check antivirus software isn't blocking connection

---

## Production Deployment Options

### Option 1: Windows Service (Recommended for Always-On)

Use `sc.exe` or NSSM (Non-Sucking Service Manager) to run as Windows service.

**Using NSSM:**

1. **Download NSSM:** https://nssm.cc/download
2. **Extract to folder** (e.g., `C:\Tools\nssm`)
3. **Install Service:**
   ```powershell
   cd C:\Tools\nssm\win64
   .\nssm.exe install FlipKitWeb "C:\Program Files\dotnet\dotnet.exe" "C:\FlipKit\Web\FlipKit.Web.dll"
   .\nssm.exe set FlipKitWeb AppDirectory "C:\FlipKit\Web"
   .\nssm.exe set FlipKitWeb AppEnvironmentExtra ASPNETCORE_URLS=http://0.0.0.0:5000
   .\nssm.exe start FlipKitWeb
   ```

4. **Verify Service:**
   ```powershell
   Get-Service FlipKitWeb
   ```

**Remove Service:**
```powershell
.\nssm.exe stop FlipKitWeb
.\nssm.exe remove FlipKitWeb confirm
```

### Option 2: IIS (Internet Information Services)

For Windows Server or advanced scenarios.

**Prerequisites:**
- IIS installed with ASP.NET Core Hosting Bundle

**Steps:**

1. **Install .NET 8.0 Hosting Bundle:**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Install on server

2. **Publish Application:**
   ```bash
   dotnet publish -c Release -o C:\inetpub\FlipKitWeb
   ```

3. **Create Application Pool:**
   - Open IIS Manager
   - Create new Application Pool: "FlipKitWeb"
   - .NET CLR Version: No Managed Code
   - Managed Pipeline Mode: Integrated

4. **Create Website:**
   - Add Website: "FlipKit Web"
   - Physical path: `C:\inetpub\FlipKitWeb`
   - Binding: http, port 5000, IP: All Unassigned
   - Application Pool: FlipKitWeb

5. **Configure Permissions:**
   - Grant IIS_IUSRS read access to website folder
   - Grant NETWORK SERVICE write access to database folder

6. **Start Website**

### Option 3: Linux systemd Service

For Linux servers.

**Create Service File:**

```bash
sudo nano /etc/systemd/system/flipkitweb.service
```

**Content:**
```ini
[Unit]
Description=FlipKit Web Application
After=network.target

[Service]
Type=notify
User=flipkit
WorkingDirectory=/opt/FlipKit/Web
ExecStart=/usr/bin/dotnet /opt/FlipKit/Web/FlipKit.Web.dll
Restart=on-failure
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
```

**Enable and Start:**
```bash
sudo systemctl daemon-reload
sudo systemctl enable flipkitweb
sudo systemctl start flipkitweb
sudo systemctl status flipkitweb
```

### Option 4: Docker (Advanced)

For containerized deployment.

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FlipKit.Web/FlipKit.Web.csproj", "FlipKit.Web/"]
COPY ["FlipKit.Core/FlipKit.Core.csproj", "FlipKit.Core/"]
RUN dotnet restore "FlipKit.Web/FlipKit.Web.csproj"
COPY . .
WORKDIR "/src/FlipKit.Web"
RUN dotnet build "FlipKit.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FlipKit.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlipKit.Web.dll"]
```

**Build and Run:**
```bash
docker build -t flipkit-web .
docker run -d -p 5000:5000 --name flipkit-web \
  -v /path/to/flipkit-data:/app/data \
  flipkit-web
```

---

## Configuration

### Database Location

By default, the database is stored at:
- **Windows:** `C:\Users\[YourName]\AppData\Roaming\FlipKit\cards.db`
- **macOS:** `~/Library/Application Support/FlipKit/cards.db`
- **Linux:** `~/.local/share/FlipKit/cards.db`

**To change location:**

Edit `FlipKit.Web/Program.cs`:
```csharp
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "FlipKit", "cards.db");

// Change to custom path:
var dbPath = @"C:\CustomPath\FlipKit\cards.db";
```

### Settings Location

Settings are stored in:
- **Windows:** `C:\Users\[YourName]\AppData\Roaming\FlipKit\settings.json`
- **macOS:** `~/Library/Application Support/FlipKit/settings.json`
- **Linux:** `~/.local/share/FlipKit/settings.json`

**Important:** Settings are managed through the Desktop app. The web app reads settings but does not provide a settings UI.

### Port Configuration

To change the default port (5000):

**Option 1: Launch Settings**

Edit `launchSettings.json`:
```json
"applicationUrl": "http://0.0.0.0:8080"
```

**Option 2: Command Line**

```bash
dotnet run --urls "http://0.0.0.0:8080"
```

**Option 3: Environment Variable**

```bash
export ASPNETCORE_URLS="http://0.0.0.0:8080"
```

**Don't forget to update firewall rules for the new port!**

---

## Firewall Configuration

### Windows Firewall Rules

**Allow Port 5000 (Private Network Only):**

```powershell
New-NetFirewallRule -DisplayName "FlipKit Web" `
  -Direction Inbound `
  -LocalPort 5000 `
  -Protocol TCP `
  -Action Allow `
  -Profile Private
```

**Remove Rule:**
```powershell
Remove-NetFirewallRule -DisplayName "FlipKit Web"
```

**Check if Rule Exists:**
```powershell
Get-NetFirewallRule -DisplayName "FlipKit Web"
```

### Important Security Notes

- ⚠️ **Never allow port 5000 on Public network profile**
- ⚠️ **Do not expose to internet** (no port forwarding on router)
- ⚠️ **Local network only** - FlipKit Web has no authentication
- ✅ Use Private network profile for home/office Wi-Fi only

---

## HTTPS Setup (Optional)

For encrypted connections (recommended if accessing over untrusted networks).

### Development Certificate

**Generate Self-Signed Certificate:**

```bash
dotnet dev-certs https --trust
```

**Run with HTTPS:**

```bash
dotnet run --urls "https://0.0.0.0:5001"
```

**Access:** https://your-ip:5001

**Note:** Mobile devices will show security warnings for self-signed certificates.

### Production Certificate (Let's Encrypt)

For proper HTTPS in production:

1. **Obtain Domain Name** (or use local DNS)
2. **Install Certbot:** https://certbot.eff.org/
3. **Generate Certificate:**
   ```bash
   sudo certbot certonly --standalone -d yourdomain.com
   ```

4. **Configure Kestrel:**

Edit `Program.cs`:
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.UseHttps("/path/to/cert.pfx", "password");
    });
});
```

**Not recommended for local network use** - HTTPS adds complexity for minimal security benefit on trusted home networks.

---

## Troubleshooting

### Application Won't Start

**Error: "Address already in use"**
- Another application is using port 5000
- Solution: Change port or stop conflicting application

**Find what's using port 5000:**

Windows:
```powershell
netstat -ano | findstr :5000
```

macOS/Linux:
```bash
lsof -i :5000
```

**Error: "Database locked"**
- Desktop app has exclusive lock
- Solution: Close Desktop app or enable WAL mode (should be automatic)

**Error: "Unable to open database file"**
- Database directory doesn't exist
- Solution: Create directory or check permissions

### Can't Access from Mobile Device

**Checklist:**
1. ✅ Same Wi-Fi network?
2. ✅ Firewall rule configured?
3. ✅ App listening on `0.0.0.0` (not `localhost`)?
4. ✅ Correct IP address?
5. ✅ Port 5000 in URL?
6. ✅ Web app actually running?

**Test connectivity:**

From mobile device:
```
ping 192.168.1.100
```

From computer:
```bash
curl http://localhost:5000
```

**Check firewall logs (Windows):**

Event Viewer → Windows Logs → Security → Filter for Event ID 5157 (blocked connection)

### Database Conflicts

**Problem:** Desktop and Web apps conflict

**Solution:** Database uses WAL (Write-Ahead Logging) mode to allow concurrent access. Both apps can read/write simultaneously.

**Verify WAL mode:**

```bash
sqlite3 "%APPDATA%\FlipKit\cards.db" "PRAGMA journal_mode;"
# Should return: wal
```

**If not WAL:**

```bash
sqlite3 "%APPDATA%\FlipKit\cards.db" "PRAGMA journal_mode = WAL;"
```

### Performance Issues

**Slow page loads with 1000+ cards:**
- Add database indexes
- Enable response caching
- Increase pagination size
- Consider archiving old data

**High memory usage:**
- Restart application periodically
- Check for memory leaks in custom code
- Monitor with Task Manager or `dotnet-counters`

---

## Maintenance

### Backup Database

**Backup while app is running (WAL mode):**

Windows:
```powershell
Copy-Item "$env:APPDATA\FlipKit\cards.db*" -Destination "C:\Backups\FlipKit\$(Get-Date -Format 'yyyy-MM-dd')\"
```

macOS/Linux:
```bash
cp -r ~/Library/Application\ Support/FlipKit/cards.db* ~/Backups/FlipKit/$(date +%Y-%m-%d)/
```

**Restore from backup:**

1. Stop web app and desktop app
2. Replace `cards.db`, `cards.db-wal`, and `cards.db-shm`
3. Restart applications

### Update Application

**From source:**
```bash
git pull origin master
dotnet build
dotnet run --project FlipKit.Web
```

**From release:**
1. Stop application
2. Download new release
3. Extract to deployment folder (overwrite files)
4. Restart application

### View Logs

**Console output:**
- Visible in terminal where app is running
- Ctrl+C to stop and view full log

**Configure file logging:**

Add Serilog (if not already configured):
```csharp
builder.Host.UseSerilog((context, config) => {
    config.WriteTo.File("Logs/flipkit-web-.txt", rollingInterval: RollingInterval.Day);
});
```

---

## Security Best Practices

### Do:
- ✅ Run on private Wi-Fi networks only
- ✅ Use firewall rules (Private profile only)
- ✅ Keep .NET runtime updated
- ✅ Backup database regularly
- ✅ Monitor access logs

### Don't:
- ❌ Expose to internet (no port forwarding)
- ❌ Allow access on public Wi-Fi
- ❌ Share computer IP address publicly
- ❌ Disable firewall completely
- ❌ Run with elevated privileges unless necessary

### Future: Authentication

**Coming in future versions:**
- User login system
- Role-based access control
- Session management
- Password protection

**Current:** No authentication - local network trust model only.

---

## Support

For deployment issues:
1. Check this guide
2. Review GitHub Issues: https://github.com/mthous72/FlipKit/issues
3. Report new issues with:
   - OS and .NET version
   - Deployment method
   - Error messages
   - Steps to reproduce

---

**End of Deployment Guide**

*This guide covers FlipKit Web v1.0 (February 2026). Deployment procedures may change in future versions.*
