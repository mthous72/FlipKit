# Setting Up Remote Access on Linux

This guide explains how to set up Tailscale for remote access to FlipKit from your phone or other devices.

## What is Tailscale?

Tailscale creates a secure private network between your devices. Once set up, your phone can access FlipKit from anywhere.

---

## Step 1: Install Tailscale on Linux

### Ubuntu/Debian

```bash
# Add Tailscale's GPG key and repository
curl -fsSL https://pkgs.tailscale.com/stable/ubuntu/jammy.noarmor.gpg | sudo tee /usr/share/keyrings/tailscale-archive-keyring.gpg >/dev/null
curl -fsSL https://pkgs.tailscale.com/stable/ubuntu/jammy.tailscale-keyring.list | sudo tee /etc/apt/sources.list.d/tailscale.list

# Install Tailscale
sudo apt-get update
sudo apt-get install tailscale

# Start Tailscale
sudo tailscale up
```

### Fedora/RHEL

```bash
# Add Tailscale's repository
sudo dnf config-manager --add-repo https://pkgs.tailscale.com/stable/fedora/tailscale.repo

# Install Tailscale
sudo dnf install tailscale

# Enable and start the service
sudo systemctl enable --now tailscaled

# Connect
sudo tailscale up
```

### Arch Linux

```bash
# Install from official repositories
sudo pacman -S tailscale

# Enable and start the service
sudo systemctl enable --now tailscaled

# Connect
sudo tailscale up
```

### Other Distributions

Visit [https://tailscale.com/download/linux](https://tailscale.com/download/linux) for instructions for your distribution.

---

## Step 2: Sign In

After running `sudo tailscale up`:

1. A URL will be printed in the terminal
2. Open this URL in your browser
3. Sign in with:
   - Google account
   - Microsoft account
   - GitHub account

4. Return to terminal - you should see "Success"

### Check Status

```bash
tailscale status
```

This shows your Tailscale IP (starts with 100.) and connected devices.

---

## Step 3: Install Tailscale on Your Phone

### iPhone/iPad

1. Open the **App Store**
2. Search for **"Tailscale"**
3. Tap **Get** to install
4. Open Tailscale and sign in with the **same account**

### Android

1. Open the **Google Play Store**
2. Search for **"Tailscale"**
3. Tap **Install**
4. Open Tailscale and sign in with the **same account**

---

## Step 4: Connect to FlipKit

1. **On your Linux machine:**
   - Open FlipKit
   - Go to **Settings** → **Mobile Access**
   - Start the Web Server if not running
   - You'll see two QR codes

2. **On your phone:**
   - Ensure Tailscale shows "Connected"
   - Scan the **Tailscale QR code**
   - Or manually enter the URL: `http://YOUR_TAILSCALE_IP:5000`

3. Bookmark the page for easy access!

---

## Troubleshooting

### Find Your Tailscale IP

```bash
tailscale ip -4
```

### Check Connection Status

```bash
tailscale status
```

### Restart Tailscale

```bash
sudo systemctl restart tailscaled
```

### View Logs

```bash
journalctl -u tailscaled -f
```

### QR Code Shows "Not configured"

1. Check that `tailscale status` shows "Connected"
2. Verify the Tailscale IP is assigned: `tailscale ip -4`
3. Click **Refresh Network Status** in FlipKit

---

## Firewall Configuration

If using UFW or firewalld, Tailscale should work without changes. If you have custom iptables rules, ensure Tailscale traffic is allowed:

```bash
# Check if Tailscale interface exists
ip link show tailscale0

# Tailscale handles its own firewall rules automatically
```

---

## Using FlipKit Remotely

Once connected:

1. Access FlipKit via `http://100.X.X.X:5000`
2. Works from anywhere with internet
3. All features function normally
4. Data stays on your Linux machine

---

## Security Notes

- End-to-end encrypted connections
- Only your devices can access FlipKit
- No ports opened on your router
- Your data stays local
