# Installing FlipKit on Linux

This guide explains how to install and run FlipKit Hub on Linux (x64).

**Applies to:** FlipKit v3.7.0 (Linux x64)

FlipKit is distributed as a self-contained build — the .NET runtime is bundled,
so you do **not** need to install .NET separately.

---

## Installation Steps

### Step 1: Download

Download `FlipKit-Hub-Linux-x64-v3.7.0.zip` from the
[releases page](https://github.com/mthous72/FlipKit/releases).

### Step 2: Extract

```bash
unzip FlipKit-Hub-Linux-x64-v3.7.0.zip -d ~/FlipKit
cd ~/FlipKit
```

### Step 3: Make the binary executable

```bash
chmod +x FlipKit.Desktop
# the bundled servers, if you run them directly:
chmod +x servers/FlipKit.Web servers/FlipKit.Api 2>/dev/null || true
```

### Step 4: Run

```bash
./FlipKit.Desktop
```

The desktop window opens directly. The embedded Web and API servers are managed
from **Settings → Servers** (and stop automatically when you close the app).

> A graphical desktop environment is required for the Avalonia UI. On a headless
> server, run the Web server standalone instead — see
> [deployment.md](deployment.md).

---

## First-Time Setup

On first launch a setup wizard configures your API keys:

1. **OpenRouter** (required for AI vision fallback) — https://openrouter.ai/keys
2. **ImgBB** (required for export image hosting) — https://api.imgbb.com/
3. **Preferences** (eBay seller toggle, etc.)

To enable first-pass CardSight recognition later, add your CardSight API key in
**Settings → Scanning** (free tier: 750 identifications/month). See
[../features/ai-scanning.md](../features/ai-scanning.md).

---

## Where Your Data Lives

```
~/.local/share/FlipKit/
├── config.json     ← API keys / settings (secrets encrypted)
├── cards.db        ← SQLite inventory (WAL mode)
├── images/         ← local card photos
├── exports/        ← generated CSV files
└── logs/           ← app logs
```

**Back up** by copying `cards.db` from that folder. **Restore** by copying it
back.

---

## Troubleshooting

### Missing native dependencies
If the app fails to start with a missing-library error, install the common GUI
dependencies for your distro (e.g. on Debian/Ubuntu:
`libice6 libsm6 libfontconfig1`). Avalonia needs an X11 (or XWayland) display.

### `Permission denied` when launching
Re-run `chmod +x FlipKit.Desktop`.

### Can't reach the Web server from a phone
Confirm the server is running (Settings → Servers), the phone is on the same
network (or Tailscale), and the firewall allows the configured port (default
5000). See [deployment.md](deployment.md) and the Tailscale guides.

---

## Getting Help

1. Check the [GitHub Issues](https://github.com/mthous72/FlipKit/issues) page.
2. See the other guides in this folder — installation
   ([Windows](install-windows.md), [Mac](install-mac.md)), the
   [user guide](user-guide.md), and the Tailscale setup guides for remote access.
