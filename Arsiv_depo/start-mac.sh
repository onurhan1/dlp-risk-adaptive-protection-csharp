#!/bin/bash

# Start All Services Script for macOS
# Starts Analyzer API and Collector Service in separate terminal windows

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}=== Starting DLP Risk Analyzer Services (macOS) ===${NC}"
echo ""

# Get project root directory
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check if services are already running
if lsof -i :8000 > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠ Port 8000 is already in use.${NC}"
    echo -e "${YELLOW}  Please stop the existing service first.${NC}"
    echo -e "${CYAN}  Command: lsof -ti :8000 | xargs kill -9${NC}"
    exit 1
fi

# Start Analyzer API
echo -e "${CYAN}[1/2] Starting Analyzer API (http://localhost:8000)...${NC}"
cd "$PROJECT_ROOT/DLP.RiskAnalyzer.Analyzer"

# Use osascript to open new Terminal window on macOS
osascript -e "tell application \"Terminal\" to do script \"cd '$PROJECT_ROOT/DLP.RiskAnalyzer.Analyzer' && echo 'Analyzer API starting...' && dotnet run\""

sleep 3

# Start Collector Service
echo -e "${CYAN}[2/2] Starting Collector Service...${NC}"
cd "$PROJECT_ROOT/DLP.RiskAnalyzer.Collector"

osascript -e "tell application \"Terminal\" to do script \"cd '$PROJECT_ROOT/DLP.RiskAnalyzer.Collector' && echo 'Collector Service starting...' && dotnet run\""

cd "$PROJECT_ROOT"

sleep 2

echo ""
echo -e "${GREEN}=== All Services Started! ===${NC}"
echo ""
echo -e "${CYAN}📍 URLs:${NC}"
echo -e "  • Analyzer API: ${CYAN}http://localhost:8000${NC}"
echo -e "  • Swagger UI:   ${CYAN}http://localhost:8000/swagger${NC}"
echo ""
echo -e "${CYAN}💡 Tips:${NC}"
echo -e "  • Check health: ${CYAN}curl http://localhost:8000/health${NC}"
echo -e "  • Open Swagger: ${CYAN}open http://localhost:8000/swagger${NC}"
echo -e "  • Check services: ${CYAN}./check-services-mac.sh${NC}"
echo ""
echo -e "${YELLOW}Note: WPF Dashboard cannot run on macOS (Windows only).${NC}"
echo -e "${YELLOW}      Use Swagger UI to test the API endpoints.${NC}"
echo ""

