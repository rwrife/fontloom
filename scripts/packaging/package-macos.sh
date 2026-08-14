#!/usr/bin/env bash
set -euo pipefail

PUBLISH_DIR="${1:?Usage: package-macos.sh <publish-dir> <output-dir> [version]}"
OUTPUT_DIR="${2:?Usage: package-macos.sh <publish-dir> <output-dir> [version]}"
APP_VERSION="${3:-1.0.0}"

APP_NAME="fontloom"
BUNDLE_NAME="${APP_NAME}.app"
EXECUTABLE_NAME="Fontloom.Desktop"

PUBLISH_DIR="$(cd "$PUBLISH_DIR" && pwd)"
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

if [[ ! -f "$PUBLISH_DIR/$EXECUTABLE_NAME" ]]; then
  echo "Expected executable not found at $PUBLISH_DIR/$EXECUTABLE_NAME" >&2
  exit 1
fi

BUNDLE_DIR="$OUTPUT_DIR/$BUNDLE_NAME"
rm -rf "$BUNDLE_DIR"
mkdir -p "$BUNDLE_DIR/Contents/MacOS" "$BUNDLE_DIR/Contents/Resources"

cp -R "$PUBLISH_DIR"/. "$BUNDLE_DIR/Contents/MacOS/"
chmod +x "$BUNDLE_DIR/Contents/MacOS/$EXECUTABLE_NAME"

cat > "$BUNDLE_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>com.rwrife.fontloom</string>
    <key>CFBundleVersion</key>
    <string>${APP_VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${APP_VERSION}</string>
    <key>CFBundleExecutable</key>
    <string>${EXECUTABLE_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
</dict>
</plist>
PLIST

(
  cd "$OUTPUT_DIR"
  /usr/bin/zip -qry fontloom-macos.app.zip "$BUNDLE_NAME"
)

DMG_PATH="$OUTPUT_DIR/fontloom-macos.dmg"
rm -f "$DMG_PATH"
hdiutil create -volname "$APP_NAME" -srcfolder "$BUNDLE_DIR" -ov -format UDZO "$DMG_PATH" >/dev/null

echo "Created $OUTPUT_DIR/fontloom-macos.app.zip"
echo "Created $DMG_PATH"
