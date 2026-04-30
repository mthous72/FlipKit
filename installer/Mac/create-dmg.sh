#!/bin/bash
# FlipKit macOS DMG Creation Script
# Creates a distributable DMG file with the FlipKit app bundle

set -e

VERSION=${1:-"3.3.0"}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish"
OUTPUT_DIR="$PROJECT_ROOT/installers"
APP_NAME="FlipKit"
DMG_NAME="FlipKit-macOS-v${VERSION}.dmg"

echo "Creating FlipKit macOS DMG v${VERSION}..."

# Ensure output directory exists
mkdir -p "$OUTPUT_DIR"

# Check if create-dmg is installed
if ! command -v create-dmg &> /dev/null; then
    echo "Error: create-dmg not found. Install with: brew install create-dmg"
    exit 1
fi

# Check for published app
if [ ! -d "$PUBLISH_DIR/osx-x64/FlipKit.app" ] && [ ! -d "$PUBLISH_DIR/osx-arm64/FlipKit.app" ]; then
    echo "Error: FlipKit.app not found in publish directory."
    echo "Run 'dotnet publish' first with -r osx-x64 or -r osx-arm64"
    exit 1
fi

# Determine which architecture to use
if [ -d "$PUBLISH_DIR/osx-arm64/FlipKit.app" ]; then
    APP_SOURCE="$PUBLISH_DIR/osx-arm64/FlipKit.app"
    echo "Using ARM64 (Apple Silicon) build..."
elif [ -d "$PUBLISH_DIR/osx-x64/FlipKit.app" ]; then
    APP_SOURCE="$PUBLISH_DIR/osx-x64/FlipKit.app"
    echo "Using x64 (Intel) build..."
fi

# Create staging directory
STAGING_DIR=$(mktemp -d)
cp -R "$APP_SOURCE" "$STAGING_DIR/"

# Copy README-INSTALL.txt
if [ -f "$SCRIPT_DIR/README-INSTALL.txt" ]; then
    cp "$SCRIPT_DIR/README-INSTALL.txt" "$STAGING_DIR/"
fi

# Remove any existing DMG
rm -f "$OUTPUT_DIR/$DMG_NAME"

# Create DMG
echo "Creating DMG..."
create-dmg \
    --volname "FlipKit $VERSION" \
    --volicon "$PROJECT_ROOT/FlipKit.Desktop/Assets/flipkit.icns" \
    --window-pos 200 120 \
    --window-size 600 400 \
    --icon-size 100 \
    --icon "FlipKit.app" 150 185 \
    --hide-extension "FlipKit.app" \
    --app-drop-link 450 185 \
    --no-internet-enable \
    "$OUTPUT_DIR/$DMG_NAME" \
    "$STAGING_DIR" || {
        # create-dmg returns non-zero if no changes were made
        # Check if DMG was created anyway
        if [ -f "$OUTPUT_DIR/$DMG_NAME" ]; then
            echo "DMG created with warnings (this is normal)"
        else
            echo "Error creating DMG"
            rm -rf "$STAGING_DIR"
            exit 1
        fi
    }

# Cleanup
rm -rf "$STAGING_DIR"

echo ""
echo "DMG created: $OUTPUT_DIR/$DMG_NAME"
echo ""
echo "Note: This DMG is unsigned. Users will need to:"
echo "  1. Right-click the app"
echo "  2. Select 'Open'"
echo "  3. Click 'Open' in the security dialog"
