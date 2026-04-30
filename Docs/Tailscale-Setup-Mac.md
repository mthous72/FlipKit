# Setting Up Remote Access on Mac

This guide explains how to set up Tailscale for remote access to FlipKit from your phone or other devices.

## What is Tailscale?

Tailscale creates a secure private network between your devices. Once set up, your phone can access FlipKit from anywhere - at home, at work, or on cellular data.

---

## Step 1: Install Tailscale on Your Mac

### Option A: Mac App Store (Recommended)

1. Open the **Mac App Store**
2. Search for **"Tailscale"**
3. Click **Get** to install
4. Open Tailscale from Applications

### Option B: Direct Download

1. Go to [https://tailscale.com/download/mac](https://tailscale.com/download/mac)
2. Download the DMG file
3. Open the DMG and drag Tailscale to Applications
4. Open Tailscale from Applications

### Sign In

1. Click the **Tailscale icon** in the menu bar (top right)
2. Click **Sign in**
3. Your browser opens - sign in with:
   - Google account
   - Apple ID
   - Microsoft account
   - GitHub account

4. Return to the Tailscale menu - it should show "Connected"

---

## Step 2: Install Tailscale on Your Phone

### iPhone/iPad

1. Open the **App Store**
2. Search for **"Tailscale"**
3. Tap **Get** to install
4. Open the Tailscale app
5. Tap **Sign in** and use the **same account** as your Mac

### Android

1. Open the **Google Play Store**
2. Search for **"Tailscale"**
3. Tap **Install**
4. Open the Tailscale app
5. Tap **Sign in** and use the **same account** as your Mac

---

## Step 3: Connect to FlipKit

1. **On your Mac:**
   - Open FlipKit
   - Go to **Settings** → scroll to **Mobile Access**
   - Start the Web Server if not running
   - You'll see two QR codes: Local Network and Tailscale

2. **On your phone:**
   - Make sure Tailscale is connected
   - Open your phone's camera
   - Point it at the **Tailscale QR code**
   - Tap the notification to open FlipKit

3. **Bookmark** the page for easy access later!

---

## Troubleshooting

### Tailscale QR code shows "Not configured"

- Check that Tailscale is running (menu bar icon)
- Click the Tailscale icon and verify it shows "Connected"
- Try signing out and back in
- Click **Refresh Network Status** in FlipKit

### "Tailscale wants to add VPN configurations"

This is normal! Tailscale needs VPN permissions to create the secure network. Click **Allow** when prompted.

### Mac App Store version vs Direct Download

Both are identical. The App Store version is easier to update. The direct download version is useful if you don't use the App Store.

---

## Using FlipKit Remotely

Once connected:

1. **Tailscale URL format:** `http://100.X.X.X:5000`
2. Works from anywhere with internet
3. All features work the same as local access
4. Data stays on your Mac

---

## Security Notes

- Tailscale connections are encrypted end-to-end
- Only your devices can access FlipKit
- Your data never leaves your Mac
