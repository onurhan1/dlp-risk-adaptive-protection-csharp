#!/bin/bash

# Create project zip for Windows Server deployment
# Only excludes Mac-specific files (.DS_Store, ._*, etc.)
# Includes everything else: node_modules, .next, bin/, obj/, etc.

set -e

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${CYAN}=== Creating Project Zip for Windows Server ===${NC}"
echo ""

# Get project root directory
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

# Output to Desktop
DESKTOP_PATH="$HOME/Desktop"
ZIP_NAME="DLP_RiskAnalyzer_$(date +%Y%m%d_%H%M%S).zip"
ZIP_PATH="$DESKTOP_PATH/$ZIP_NAME"

echo -e "${CYAN}Project root: $PROJECT_ROOT${NC}"
echo -e "${CYAN}Output file: $ZIP_PATH${NC}"
echo ""

# Remove existing zip if exists
if [ -f "$ZIP_PATH" ]; then
    echo -e "${YELLOW}Removing existing zip file...${NC}"
    rm -f "$ZIP_PATH"
fi

# Create temporary exclude file - ONLY Mac-specific files
EXCLUDE_FILE=$(mktemp)
cat > "$EXCLUDE_FILE" << 'EOF'
# Mac-specific files only
**/.DS_Store
**/._*
**/.AppleDouble
**/.AppleDB
**/.AppleDesktop
**/.VolumeIcon.icns
**/.fseventsd
**/.Spotlight-V100
**/.TemporaryItems
**/.Trashes
**/.localized
EOF

echo -e "${CYAN}Creating zip file (this may take a few minutes)...${NC}"
echo -e "${YELLOW}Excluding only Mac-specific files:${NC}"
echo -e "  - .DS_Store"
echo -e "  - ._* (AppleDouble files)"
echo -e "  - .AppleDouble directories"
echo -e "  - Other Mac system files"
echo ""
echo -e "${GREEN}Including everything else:${NC}"
echo -e "  ✓ node_modules/"
echo -e "  ✓ .next/ (build folder)"
echo -e "  ✓ bin/, obj/ (build folders)"
echo -e "  ✓ All source files"
echo ""

# Create zip with only Mac file exclusions
# Don't exclude the script itself or zip files in the project
zip -r "$ZIP_PATH" . \
    -x@"$EXCLUDE_FILE" \
    -x "*.zip" \
    > /dev/null 2>&1

# Check if zip was created successfully
if [ -f "$ZIP_PATH" ]; then
    ZIP_SIZE=$(du -h "$ZIP_PATH" | cut -f1)
    echo ""
    echo -e "${GREEN}✓ Zip file created successfully!${NC}"
    echo -e "${CYAN}  File: $ZIP_PATH${NC}"
    echo -e "${CYAN}  Size: $ZIP_SIZE${NC}"
    echo ""
    echo -e "${YELLOW}Next steps:${NC}"
    echo -e "  1. Copy $ZIP_NAME from Desktop to Windows Server"
    echo -e "  2. Extract the zip file on Windows Server"
    echo -e "  3. Everything is ready - no extra installation needed!"
else
    echo -e "${RED}✗ Failed to create zip file${NC}"
    exit 1
fi

# Clean up
rm -f "$EXCLUDE_FILE"

echo -e "${GREEN}Done!${NC}"

