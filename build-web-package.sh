#!/bin/bash
# CardLister Web - Build Distributable Package
# Creates self-contained deployments for macOS and Linux

echo "================================"
echo "CardLister Web - Build Package"
echo "================================"
echo ""

VERSION="2.0.2"

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf publish
mkdir -p publish

# Build for macOS (Intel)
echo ""
echo "Building macOS (Intel) package..."
dotnet publish CardLister.Web/CardLister.Web.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishReadyToRun=true \
  -o publish/CardLister-Web-macOS-Intel

if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: macOS Intel build failed!"
    exit 1
fi

# Build for macOS (Apple Silicon)
echo ""
echo "Building macOS (Apple Silicon) package..."
dotnet publish CardLister.Web/CardLister.Web.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishReadyToRun=true \
  -o publish/CardLister-Web-macOS-ARM

if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: macOS ARM build failed!"
    exit 1
fi

# Build for Linux
echo ""
echo "Building Linux package..."
dotnet publish CardLister.Web/CardLister.Web.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishReadyToRun=true \
  -o publish/CardLister-Web-Linux

if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: Linux build failed!"
    exit 1
fi

# Create launcher scripts
echo ""
echo "Creating launcher scripts..."

# macOS/Linux launcher script
create_launcher() {
    local OUTPUT_DIR=$1

    cat > "$OUTPUT_DIR/start-web.sh" << 'EOF'
#!/bin/bash
# CardLister Web Application Launcher

export ASPNETCORE_URLS="http://0.0.0.0:5000"
export ASPNETCORE_ENVIRONMENT="Production"

echo "====================================="
echo "  CardLister Web Application"
echo "====================================="
echo ""
echo "Starting server on http://localhost:5000"
echo "Access from mobile: http://YOUR-IP:5000"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

# Try to open browser
if command -v open &> /dev/null; then
    # macOS
    sleep 2 && open http://localhost:5000 &
elif command -v xdg-open &> /dev/null; then
    # Linux
    sleep 2 && xdg-open http://localhost:5000 &
fi

# Start the app
./CardLister.Web
EOF

    chmod +x "$OUTPUT_DIR/start-web.sh"
    chmod +x "$OUTPUT_DIR/CardLister.Web"
}

create_launcher "publish/CardLister-Web-macOS-Intel"
create_launcher "publish/CardLister-Web-macOS-ARM"
create_launcher "publish/CardLister-Web-Linux"

# Create README files
echo ""
echo "Creating README files..."

create_readme() {
    local OUTPUT_DIR=$1
    local PLATFORM=$2

    cat > "$OUTPUT_DIR/README.md" << EOF
# CardLister Web Application

## Quick Start

1. Run the launcher script:
   \`\`\`bash
   ./start-web.sh
   \`\`\`

2. Your browser will open to http://localhost:5000

3. Press Ctrl+C in the terminal to stop

## First Time Setup

Make the launcher executable:
\`\`\`bash
chmod +x start-web.sh
chmod +x CardLister.Web
\`\`\`

## Mobile Access

1. Find your computer's IP address:
   \`\`\`bash
   ifconfig
   # Look for "inet" address (e.g., 192.168.1.100)
   \`\`\`

2. On your phone/tablet (same Wi-Fi network):
   - Open browser to: http://YOUR-IP:5000
   - Example: http://192.168.1.100:5000

## Firewall Setup

**macOS:**
- System Preferences → Security & Privacy → Firewall
- Allow CardLister.Web to accept incoming connections

**Linux (ufw):**
\`\`\`bash
sudo ufw allow 5000/tcp
\`\`\`

## Database Location

The database is stored at:
- macOS: \`~/Library/Application Support/CardLister/cards.db\`
- Linux: \`~/.local/share/CardLister/cards.db\`

This is shared with CardLister Desktop if installed.

## Documentation

See \`Docs/WEB-USER-GUIDE.md\` for complete user guide.
See \`Docs/DEPLOYMENT-GUIDE.md\` for advanced deployment options.

## Troubleshooting

**"Port 5000 already in use":**
- Edit start-web.sh and change ASPNETCORE_URLS to use port 8080

**Can't access from phone:**
- Ensure same Wi-Fi network
- Check firewall allows port 5000
- Verify app is running (terminal window open)

**Permission denied:**
\`\`\`bash
chmod +x start-web.sh
chmod +x CardLister.Web
\`\`\`

## Platform

$PLATFORM

## Version

CardLister Web v$VERSION
Built: $(date)
EOF

    # Copy documentation
    mkdir -p "$OUTPUT_DIR/Docs"
    cp Docs/WEB-USER-GUIDE.md "$OUTPUT_DIR/Docs/" 2>/dev/null || true
    cp Docs/DEPLOYMENT-GUIDE.md "$OUTPUT_DIR/Docs/" 2>/dev/null || true
}

create_readme "publish/CardLister-Web-macOS-Intel" "macOS (Intel x64)"
create_readme "publish/CardLister-Web-macOS-ARM" "macOS (Apple Silicon ARM64)"
create_readme "publish/CardLister-Web-Linux" "Linux (x64)"

# Create archives
echo ""
echo "Creating ZIP archives..."

cd publish

# macOS Intel
zip -r -q "CardLister-Web-macOS-Intel-v$VERSION.zip" CardLister-Web-macOS-Intel
echo "Created: CardLister-Web-macOS-Intel-v$VERSION.zip"

# macOS ARM
zip -r -q "CardLister-Web-macOS-ARM-v$VERSION.zip" CardLister-Web-macOS-ARM
echo "Created: CardLister-Web-macOS-ARM-v$VERSION.zip"

# Linux
tar -czf "CardLister-Web-Linux-v$VERSION.tar.gz" CardLister-Web-Linux
echo "Created: CardLister-Web-Linux-v$VERSION.tar.gz"

cd ..

echo ""
echo "================================"
echo "BUILD COMPLETE!"
echo "================================"
echo ""
echo "Packages created:"
ls -lh publish/*.zip publish/*.tar.gz 2>/dev/null | awk '{print $9, "(" $5 ")"}'
echo ""
echo "To test locally:"
echo "  cd publish/CardLister-Web-[PLATFORM]"
echo "  ./start-web.sh"
echo ""
echo "To distribute:"
echo "  1. Upload archives to GitHub Releases"
echo "  2. Users download, extract, and run start-web.sh"
echo ""
