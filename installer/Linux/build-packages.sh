#!/bin/bash
# FlipKit Linux Package Build Script
# Creates .deb, .rpm, and .tar.gz packages

set -e

VERSION=${1:-"3.3.0"}
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish/linux-x64"
OUTPUT_DIR="$PROJECT_ROOT/installers"
APP_NAME="flipkit"

echo "Building FlipKit Linux packages v${VERSION}..."

# Ensure output directory exists
mkdir -p "$OUTPUT_DIR"

# Check if publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Linux publish directory not found at $PUBLISH_DIR"
    echo "Run: dotnet publish FlipKit.Desktop -c Release -r linux-x64 --self-contained -o publish/linux-x64"
    exit 1
fi

# Create tar.gz package
echo "Creating tar.gz package..."
TARBALL_NAME="flipkit-${VERSION}-linux-x64.tar.gz"
cd "$PUBLISH_DIR"
tar -czvf "$OUTPUT_DIR/$TARBALL_NAME" .
echo "Created: $OUTPUT_DIR/$TARBALL_NAME"

# Check if fpm is installed for .deb and .rpm packages
if command -v fpm &> /dev/null; then
    echo "Building .deb package..."

    # Create .deb package
    fpm -s dir -t deb \
        -n "$APP_NAME" \
        -v "$VERSION" \
        --description "FlipKit - Sports Card Inventory Management" \
        --url "https://github.com/your-repo/flipkit" \
        --maintainer "FlipKit Team" \
        --license "MIT" \
        --category "Office" \
        --prefix /opt/flipkit \
        --after-install "$SCRIPT_DIR/postinst.sh" \
        -p "$OUTPUT_DIR/flipkit-${VERSION}-linux-x64.deb" \
        "$PUBLISH_DIR/=/opt/flipkit"

    echo "Created: $OUTPUT_DIR/flipkit-${VERSION}-linux-x64.deb"

    echo "Building .rpm package..."

    # Create .rpm package
    fpm -s dir -t rpm \
        -n "$APP_NAME" \
        -v "$VERSION" \
        --description "FlipKit - Sports Card Inventory Management" \
        --url "https://github.com/your-repo/flipkit" \
        --maintainer "FlipKit Team" \
        --license "MIT" \
        --category "Office" \
        --prefix /opt/flipkit \
        --after-install "$SCRIPT_DIR/postinst.sh" \
        -p "$OUTPUT_DIR/flipkit-${VERSION}-linux-x64.rpm" \
        "$PUBLISH_DIR/=/opt/flipkit"

    echo "Created: $OUTPUT_DIR/flipkit-${VERSION}-linux-x64.rpm"
else
    echo ""
    echo "Note: fpm not found. Only tar.gz package was created."
    echo "To create .deb and .rpm packages, install fpm:"
    echo "  gem install fpm"
    echo ""
fi

echo ""
echo "Linux packages created in: $OUTPUT_DIR"
echo ""
echo "Installation instructions:"
echo "  .deb: sudo dpkg -i flipkit-${VERSION}-linux-x64.deb"
echo "  .rpm: sudo rpm -i flipkit-${VERSION}-linux-x64.rpm"
echo "  .tar.gz: Extract to a directory and run ./FlipKit.Desktop"
