# Installing FlipKit on Mac

This guide explains how to install and run FlipKit on macOS.

## Why the Security Warning?

FlipKit is **safe software** distributed independently (not through the Mac App Store). macOS shows a warning for all apps not purchased from the App Store or signed with an Apple Developer certificate.

This is completely normal for indie and open-source software.

## Installation Steps

### Step 1: Download and Open DMG

1. Download `FlipKit-macOS-vX.X.X.dmg` from the releases page
2. Double-click the DMG file to mount it
3. You'll see the FlipKit app and an Applications folder shortcut

### Step 2: Copy to Applications

1. Drag **FlipKit.app** to the **Applications** folder shortcut in the DMG window
2. Wait for the copy to complete
3. Eject the DMG by right-clicking it in Finder and selecting "Eject"

### Step 3: Open with Right-Click (Important!)

**Do NOT double-click the app!** macOS will block it.

Instead:
1. Open your **Applications** folder (Finder → Go → Applications)
2. Find **FlipKit.app**
3. **Right-click** (or Control+click) on FlipKit.app
4. Select **"Open"** from the context menu

### Step 4: Approve the Security Dialog

You'll see a dialog that says FlipKit is from an "unidentified developer":

1. Click **"Open"**
2. FlipKit will launch!

### Done!

After this one-time approval, you can open FlipKit normally by double-clicking.

---

## Troubleshooting

### "FlipKit is damaged and can't be opened"

This error occurs when macOS's extended attributes block the app. Fix it by:

1. Open **Terminal** (Applications → Utilities → Terminal)
2. Run this command:
   ```bash
   xattr -cr /Applications/FlipKit.app
   ```
3. Try the right-click method again

### "FlipKit cannot be opened because the developer cannot be verified"

1. Go to **System Settings** → **Privacy & Security**
2. Scroll down to find "FlipKit was blocked from use because it is not from an identified developer"
3. Click **"Open Anyway"**
4. Enter your password when prompted
5. FlipKit will launch

### App Won't Launch After Security Approval

If FlipKit still won't open:

1. Check if you're running macOS 12 (Monterey) or later
2. Ensure you have at least 500MB free disk space
3. Try restarting your Mac
4. Redownload the DMG and try again

---

## Uninstalling FlipKit

To remove FlipKit:

1. Quit FlipKit if it's running
2. Open **Applications** folder
3. Drag **FlipKit.app** to the Trash
4. Optionally, remove data files:
   - Open Finder and press **Cmd+Shift+G**
   - Enter: `~/Library/Application Support/FlipKit`
   - Delete this folder to remove all FlipKit data

---

## Getting Help

If you encounter issues:

1. Check the [GitHub Issues](https://github.com/your-repo/flipkit/issues) page
2. See other guides in the Docs folder for:
   - Setting up Tailscale for remote access
   - Using FlipKit from your phone
