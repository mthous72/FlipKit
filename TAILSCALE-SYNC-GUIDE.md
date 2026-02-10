# Tailscale Sync Setup Guide

## Overview

The Tailscale Sync feature allows you to access your card inventory from multiple computers on your private Tailscale network. Your main computer runs a sync server, and other computers sync their local database with it.

## Architecture

```
Main Computer (always on):
├── CardLister Desktop App (local SQLite database)
└── CardLister.Api (sync server on port 5000)
        ↓
    Tailscale Network (private, encrypted)
        ↓
Remote Computer (laptop/travel):
└── CardLister Desktop App (syncs via Tailscale)
```

## Prerequisites

1. **Tailscale installed** on both computers ([Download](https://tailscale.com/download))
2. Both computers connected to the same Tailscale network
3. .NET 8 SDK installed

## Setup Instructions

### Step 1: Start Sync Server on Main Computer

Open a terminal on your main computer (the one that's always on):

```bash
# Navigate to the API project
cd C:\Users\Houston Smlr Laptop\source\repos\CardLister\CardLister.Api

# Run the sync server
dotnet run
```

You should see output like:
```
CardLister Sync API
Database: C:\Users\Houston Smlr Laptop\AppData\Local\CardLister\cards.db
Listening on: http://0.0.0.0:5000
Access via Tailscale IP on port 5000
```

**Keep this terminal open** - the sync server needs to stay running.

### Step 2: Get Your Tailscale IP

In another terminal on the main computer:

```bash
tailscale ip -4
```

Example output: `100.64.1.5`

Copy this IP address - you'll need it for the next step.

### Step 3: Configure Sync on Remote Computer

1. Open CardLister Desktop on your remote computer
2. Go to **Settings** (in the navigation menu)
3. Scroll down to the **Tailscale Sync** section
4. Check **"Enable sync"**
5. In **"Sync Server URL"**, enter: `http://100.64.1.5:5000` (use your actual Tailscale IP)
6. Optionally check:
   - **"Auto-sync on startup"** - Syncs when app opens
   - **"Auto-sync on exit"** - Syncs when app closes
7. Click **"Save Settings"**

### Step 4: Test Sync

Click the **"Sync Now"** button.

You should see:
- "Syncing..." with a progress indicator
- After a few seconds: "✓ Sync complete! Pushed: X, Pulled: Y"

Your cards from the main computer should now appear in the inventory!

## How Sync Works

### Simple Conflict Resolution

Since you use one computer at a time, the sync uses a simple rule:
- **Newest timestamp wins** - The card with the most recent `UpdatedAt` is kept
- No complex merge logic needed

### What Gets Synced

- All card data (88 fields per card)
- Card images (if using ImgBB URLs - recommended)
- Price histories
- Settings are **NOT synced** (each computer has its own settings)

### When Sync Happens

1. **Manual**: Click "Sync Now" in Settings
2. **On Startup**: Automatic (if enabled)
3. **On Exit**: Automatic (if enabled)

## Testing Scenarios

### Scenario 1: Add Card on Main Computer

1. On main computer: Add a new card
2. On remote computer: Click "Sync Now"
3. **Expected**: New card appears in remote inventory

### Scenario 2: Edit Card on Remote Computer

1. On remote computer: Edit a card's price
2. Click "Sync Now"
3. On main computer: Restart app (or sync manually)
4. **Expected**: Price update appears on main computer

### Scenario 3: Auto-Sync on Startup

1. On main computer: Add a card
2. On remote computer: Close and reopen CardLister
3. **Expected**: New card appears automatically (check logs)

## Troubleshooting

### "Cannot connect to server"

**Problem**: Remote computer can't reach the sync server

**Solutions**:
1. Verify Tailscale is running on both computers: `tailscale status`
2. Check the sync server is running: Should see "Listening on..." message
3. Verify the IP is correct: Run `tailscale ip -4` on main computer
4. Test connectivity: `ping 100.64.1.5` from remote computer
5. Check firewall: Allow port 5000 on main computer

### "Sync failed: Unauthorized" or 403 errors

**Problem**: API endpoint protection (shouldn't happen with current setup)

**Solution**: The API has CORS enabled for all origins - check server logs for details

### Server crashes or stops

**Problem**: Sync server stopped running

**Solution**:
1. Check the terminal where `dotnet run` was executed
2. Look for error messages
3. Restart: `cd CardLister.Api && dotnet run`

### Cards not appearing after sync

**Problem**: Sync succeeded but cards don't show

**Solutions**:
1. Click **Refresh** in the inventory view
2. Restart CardLister on remote computer
3. Check sync status: "Pushed: 0, Pulled: 0" means no changes to sync

### ImgBB images not loading

**Problem**: Local file paths don't work across computers

**Solution**:
1. Use ImgBB to host images: **Inventory** → Select cards → **Upload Images for Selected**
2. ImgBB URLs work across all computers automatically
3. Sync will use the `ImageUrl1` and `ImageUrl2` fields

## Running Sync Server as Windows Service

For production use, you may want the sync server to start automatically with Windows.

### Option 1: Task Scheduler (Simple)

1. Open Task Scheduler
2. Create Basic Task:
   - **Name**: CardLister Sync Server
   - **Trigger**: At startup
   - **Action**: Start a program
   - **Program**: `dotnet`
   - **Arguments**: `run --project "C:\Users\Houston Smlr Laptop\source\repos\CardLister\CardLister.Api\CardLister.Api.csproj"`
3. Set to run whether user is logged in or not

### Option 2: NSSM (Non-Sucking Service Manager)

```bash
# Download NSSM from https://nssm.cc/download

# Publish the API
dotnet publish CardLister.Api -c Release -o ./api-publish

# Install as service
nssm install CardListerSync "C:\path\to\api-publish\CardLister.Api.exe"
nssm set CardListerSync Start SERVICE_AUTO_START
nssm start CardListerSync
```

## Logs

Sync operations are logged to:
- **Main computer**: Console output where `dotnet run` is running
- **Remote computer**: `%LOCALAPPDATA%\CardLister\logs\cardlister.log`

Search for "sync" in the logs to see sync activity.

## Security

- **Private network**: Sync only works on Tailscale (not public internet)
- **No authentication**: Trust is based on Tailscale network membership
- **Encrypted**: Tailscale provides end-to-end encryption
- **No data leaves Tailscale**: Your card data never goes to public cloud

## Performance

- **Fast**: Local network speeds via Tailscale (even when remote)
- **Efficient**: Only syncs cards changed since last sync
- **Lightweight**: Small data transfer (text + metadata, images via ImgBB)

## Limitations

- **No real-time sync**: Changes don't push automatically (must click Sync Now or restart)
- **No conflict UI**: If same card edited on both computers, newest wins (manual review needed)
- **Single sync server**: Can't sync between multiple main computers
- **No offline editing merge**: If remote computer offline for long time, sync may have many conflicts

## Future Enhancements (Not Yet Implemented)

- Real-time sync with SignalR (push changes immediately)
- Conflict resolution UI (side-by-side comparison)
- Sync history/audit log
- Multiple sync servers (redundancy)
- Background sync timer (auto-sync every X minutes)

## Support

If you encounter issues:
1. Check server logs (terminal output)
2. Check client logs (`%LOCALAPPDATA%\CardLister\logs\`)
3. Verify Tailscale connectivity: `tailscale status`
4. Test server health: Open `http://100.64.1.5:5000/api/sync/status` in browser

## Summary

✅ **Zero cost** - No cloud hosting fees
✅ **Private** - Stays on your Tailscale network
✅ **Fast** - Local network speeds
✅ **Simple** - Just run server and enter IP
✅ **Secure** - Tailscale encryption
✅ **Reliable** - Timestamp-based sync works for one-at-a-time usage

Enjoy accessing your cards from anywhere on your Tailscale network!
