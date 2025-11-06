#!/bin/bash

# Start Next.js Dashboard Script for macOS

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}=== Starting Next.js Dashboard ===${NC}"
echo ""

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT/dashboard"

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo -e "${YELLOW}⚠️  Node.js bulunamadı!${NC}"
    echo -e "${CYAN}Lütfen Node.js kurun:${NC}"
    echo -e "  ${CYAN}brew install node${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Node.js: $(node --version)${NC}"
echo ""

# Check if dependencies are installed
if [ ! -d "node_modules" ]; then
    echo -e "${CYAN}Installing dependencies...${NC}"
    npm install
fi

# Port configuration (default: 3001)
DASHBOARD_PORT=${DASHBOARD_PORT:-3001}

# Check if port is available
if lsof -i :$DASHBOARD_PORT > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠ Port $DASHBOARD_PORT is already in use.${NC}"
    echo -e "${YELLOW}  Please stop the existing service first.${NC}"
    echo -e "${CYAN}  Command: lsof -ti :$DASHBOARD_PORT | xargs kill -9${NC}"
    exit 1
fi

echo -e "${CYAN}Starting Next.js development server...${NC}"
echo -e "${CYAN}Dashboard will be available at: http://localhost:$DASHBOARD_PORT${NC}"
echo -e "${CYAN}Port: $DASHBOARD_PORT${NC}"
echo ""

npm run dev
