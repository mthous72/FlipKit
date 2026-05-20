# Setting Up Remote Access on Windows

This guide explains how to set up Tailscale for remote access to FlipKit from your phone or other devices, even when you're away from home.

## What is Tailscale?

Tailscale creates a secure private network between your devices. Once set up, your phone can access FlipKit from anywhere - at home, at work, or on cellular data.

**Benefits:**
- Access FlipKit from anywhere
- Secure encrypted connection
- No need to configure your router
- Free for personal use

---

## Step 1: Install Tailscale on Your Computer

1. **Download Tailscale** from [https://tailscale.com/download/windows](https://tailscale.com/download/windows)
2. **Run the installer** (TailscaleSetup-X.XX.X.exe)
3. Click **Install** and follow the prompts
4. Once installed, the Tailscale icon appears in your system tray (bottom right)

### Sign In

1. Click the **Tailscale icon** in the system tray
2. Click **Sign in**
3. Your browser opens - sign in with:
   - Google account
   - Microsoft account
   - GitHub account
   - Or create an email account

4. After signing in, Tailscale shows "Connected"

---

## Step 2: Install Tailscale on Your Phone

### iPhone/iPad

1. Open the **App Store**
2. Search for **"Tailscale"**
3. Tap **Get** to install
4. Open the Tailscale app
5. Tap **Sign in** and use the **same account** as your computer

### Android

1. Open the **Google Play Store**
2. Search for **"Tailscale"**
3. Tap **Install**
4. Open the Tailscale app
5. Tap **Sign in** and use the **same account** as your computer

---

## Step 3: Connect to FlipKit

1. **On your computer:**
   - Open FlipKit
   - Go to **Settings** → scroll to **Mobile Access**
   - You'll see two QR codes: Local Network and Tailscale

2. **On your phone:**
   - Make sure Tailscale is connected (shows green in the app)
   - Open your phone's camera
   - Point it at the **Tailscale QR code**
   - Tap the notification to open FlipKit in your browser

3. **Bookmark** the page for easy access later!

---

## Troubleshooting

### Tailscale QR code shows "Not configured"

- Check that Tailscale is running on your computer (system tray icon should be active)
- Make sure Tailscale shows "Connected" status
- Click **Refresh Network Status** in FlipKit settings

### Phone can't connect

- Ensure Tailscale app on phone shows "Connected"
- Both devices must use the **same Tailscale account**
- Try toggling Tailscale off and on in the phone app
- Check that the FlipKit web server is running

### Connection is slow

- Tailscale uses direct connections when possible
- First connection may be slow while establishing the route
- Subsequent connections should be faster

---

## Using FlipKit Remotely

Once connected:

1. **Save the URL** - You can type it directly or bookmark it
2. **Tailscale URL format:** `http://100.X.X.X:5000`
3. Use all FlipKit features normally
4. Card images and data are stored on your computer

---

## Security Notes

- Tailscale connections are encrypted end-to-end
- Only devices signed into your Tailscale account can access FlipKit
- No ports are opened on your router
- Your data stays on your computer
