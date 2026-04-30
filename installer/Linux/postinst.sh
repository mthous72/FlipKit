#!/bin/bash
# FlipKit Linux Post-Installation Script

# Create symlink in /usr/local/bin for easy command-line access
if [ -f /opt/flipkit/FlipKit.Desktop ]; then
    ln -sf /opt/flipkit/FlipKit.Desktop /usr/local/bin/flipkit
fi

# Create desktop entry
cat > /usr/share/applications/flipkit.desktop << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=FlipKit
Comment=Sports Card Inventory Management
Exec=/opt/flipkit/FlipKit.Desktop
Icon=/opt/flipkit/flipkit.png
Terminal=false
Categories=Office;Utility;
StartupNotify=true
EOF

# Update desktop database
if command -v update-desktop-database &> /dev/null; then
    update-desktop-database /usr/share/applications
fi

echo "FlipKit installed successfully!"
echo "Run 'flipkit' from terminal or find FlipKit in your applications menu."
