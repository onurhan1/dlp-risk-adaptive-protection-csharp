#!/bin/bash

# Create project zip for Windows Server deployment
# Excludes Mac-specific files and build artifacts

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

# Output zip file name
ZIP_NAME="DLP_RiskAnalyzer_$(date +%Y%m%d_%H%M%S).zip"
ZIP_PATH="$PROJECT_ROOT/$ZIP_NAME"

echo -e "${CYAN}Project root: $PROJECT_ROOT${NC}"
echo -e "${CYAN}Output file: $ZIP_PATH${NC}"
echo ""

# Remove existing zip if exists
if [ -f "$ZIP_PATH" ]; then
    echo -e "${YELLOW}Removing existing zip file...${NC}"
    rm -f "$ZIP_PATH"
fi

# Remove Mac-specific files before zipping
echo -e "${CYAN}Cleaning Mac-specific files...${NC}"

# Find and remove .DS_Store files
find . -name ".DS_Store" -type f -delete 2>/dev/null || true
echo -e "${GREEN}✓ Removed .DS_Store files${NC}"

# Find and remove ._* (AppleDouble) files
find . -name "._*" -type f -delete 2>/dev/null || true
echo -e "${GREEN}✓ Removed AppleDouble files${NC}"

# Find and remove .AppleDouble directories
find . -name ".AppleDouble" -type d -exec rm -rf {} + 2>/dev/null || true
echo -e "${GREEN}✓ Removed .AppleDouble directories${NC}"

echo ""

# Create temporary exclude file
EXCLUDE_FILE=$(mktemp)
cat > "$EXCLUDE_FILE" << 'EOF'
# Mac-specific files
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

# Build artifacts
**/bin/
**/obj/
**/node_modules/
**/.next/
**/out/
**/.pnp/
**/.pnp.js
**/coverage/
**/build/
**/dist/

# Logs
**/*.log
**/logs/
**/api.log

# IDE files
**/.vs/
**/.idea/
**/.vscode/
**/*.suo
**/*.user
**/*.userosscache
**/*.sln.docstates
**/*.sln.iml

# OS files
**/Thumbs.db
**/Desktop.ini
**/.directory

# Cache and temp
**/.cache/
**/tmp/
**/temp/
**/*.tmp
**/*.cache

# Environment files
**/.env*.local
**/.env

# TypeScript
**/*.tsbuildinfo
**/next-env.d.ts

# Reports (PDF files in reports folder - optional, comment out if needed)
# **/reports/*.pdf
EOF

echo -e "${CYAN}Creating zip file (this may take a few minutes)...${NC}"
echo -e "${YELLOW}Excluding:${NC}"
echo -e "  - Mac-specific files (.DS_Store, ._*, etc.)"
echo -e "  - Build artifacts (bin/, obj/, node_modules/, .next/)"
echo -e "  - Log files"
echo -e "  - IDE files"
echo ""

# Create zip with exclusions
zip -r "$ZIP_PATH" . \
    -x@"$EXCLUDE_FILE" \
    -x "*.zip" \
    -x "create-project-zip.sh" \
    -x "create-node-modules-zip.ps1" \
    -x "node_modules.zip" \
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
    echo -e "  1. Copy $ZIP_NAME to Windows Server"
    echo -e "  2. Extract the zip file"
    echo -e "  3. Run install-dashboard-offline.ps1 (if node_modules needed)"
    echo -e "  4. Follow Windows Server setup guide"
else
    echo -e "${RED}✗ Failed to create zip file${NC}"
    exit 1
fi

# Clean up
rm -f "$EXCLUDE_FILE"

echo -e "${GREEN}Done!${NC}"

