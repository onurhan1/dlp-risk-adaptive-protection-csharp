#!/bin/bash

# Restart All Services Script for macOS
# Stops all services and starts them fresh

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${CYAN}=== Restarting DLP Risk Analyzer - All Services ===${NC}"
echo ""

# Get project root directory
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Function to kill process on port
kill_port() {
    local port=$1
    local pid=$(lsof -ti :$port 2>/dev/null)
    if [ ! -z "$pid" ]; then
        echo -e "${YELLOW}Stopping process on port $port (PID: $pid)...${NC}"
        kill -9 $pid 2>/dev/null
        sleep 1
    fi
}

# Step 1: Stop all services
echo -e "${CYAN}[Step 1/6] Stopping all services...${NC}"
kill_port 5001  # Analyzer API
kill_port 5000  # Collector (if using different port)
kill_port 3002  # Dashboard
kill_port 3000  # Dashboard (alternative)
kill_port 3001  # Dashboard (alternative)
echo -e "${GREEN}✓ All services stopped${NC}"
echo ""

# Step 2: Check Docker and stop containers
echo -e "${CYAN}[Step 2/6] Checking Docker and stopping containers...${NC}"
cd "$PROJECT_ROOT"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}✗ Docker daemon is not running!${NC}"
    echo -e "${YELLOW}Please start Docker Desktop and try again.${NC}"
    echo -e "${CYAN}On macOS: Open Docker Desktop application${NC}"
    exit 1
fi

docker-compose down 2>/dev/null
echo -e "${GREEN}✓ Docker containers stopped${NC}"
echo ""

# Step 3: Start Docker containers
echo -e "${CYAN}[Step 3/6] Starting Docker containers (PostgreSQL, Redis)...${NC}"
docker-compose up -d
sleep 5

# Wait for PostgreSQL to be ready
echo -e "${CYAN}Waiting for PostgreSQL to be ready...${NC}"
for i in {1..30}; do
    if docker exec dlp-timescaledb pg_isready -U postgres > /dev/null 2>&1; then
        echo -e "${GREEN}✓ PostgreSQL is ready${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}✗ PostgreSQL failed to start${NC}"
        exit 1
    fi
    sleep 1
done

# Wait for Redis to be ready
echo -e "${CYAN}Waiting for Redis to be ready...${NC}"
for i in {1..30}; do
    if docker exec dlp-redis redis-cli ping > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Redis is ready${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}✗ Redis failed to start${NC}"
        exit 1
    fi
    sleep 1
done
echo ""

# Step 4: Start Analyzer API
echo -e "${CYAN}[Step 4/6] Starting Analyzer API (http://0.0.0.0:5001)...${NC}"
cd "$PROJECT_ROOT/DLP.RiskAnalyzer.Analyzer"

# Use osascript to open new Terminal window on macOS
osascript -e "tell application \"Terminal\" to do script \"cd '$PROJECT_ROOT/DLP.RiskAnalyzer.Analyzer' && echo '=== Analyzer API Starting ===' && echo 'Listening on: http://0.0.0.0:5001' && echo 'Swagger UI: http://localhost:5001/swagger' && echo '' && dotnet run\""

sleep 5
echo -e "${GREEN}✓ Analyzer API starting...${NC}"
echo ""

# Step 5: Start Collector Service
echo -e "${CYAN}[Step 5/6] Starting Collector Service...${NC}"
cd "$PROJECT_ROOT/DLP.RiskAnalyzer.Collector"

osascript -e "tell application \"Terminal\" to do script \"cd '$PROJECT_ROOT/DLP.RiskAnalyzer.Collector' && echo '=== Collector Service Starting ===' && echo '' && dotnet run\""

sleep 3
echo -e "${GREEN}✓ Collector Service starting...${NC}"
echo ""

# Step 6: Start Dashboard
echo -e "${CYAN}[Step 6/6] Starting Dashboard (http://0.0.0.0:3002)...${NC}"
cd "$PROJECT_ROOT/dashboard"

# Check if node_modules exists
if [ ! -d "node_modules" ]; then
    echo -e "${YELLOW}Installing dependencies...${NC}"
    npm install
fi

# Build if needed
if [ ! -d ".next" ]; then
    echo -e "${YELLOW}Building dashboard...${NC}"
    npm run build
fi

osascript -e "tell application \"Terminal\" to do script \"cd '$PROJECT_ROOT/dashboard' && echo '=== Dashboard Starting ===' && echo 'Listening on: http://0.0.0.0:3002' && echo 'Access from network: http://[SERVER_IP]:3002' && echo '' && npm start\""

sleep 3
echo -e "${GREEN}✓ Dashboard starting...${NC}"
echo ""

cd "$PROJECT_ROOT"

# Get server IP
SERVER_IP=$(ifconfig | grep "inet " | grep -v 127.0.0.1 | awk '{print $2}' | head -n 1)

echo ""
echo -e "${GREEN}=== All Services Restarted! ===${NC}"
echo ""
echo -e "${CYAN}📍 Service URLs:${NC}"
echo -e "  • Analyzer API:     ${CYAN}http://localhost:5001${NC} (or http://$SERVER_IP:5001)"
echo -e "  • Swagger UI:       ${CYAN}http://localhost:5001/swagger${NC}"
echo -e "  • Health Check:     ${CYAN}http://localhost:5001/health${NC}"
echo -e "  • Dashboard:        ${CYAN}http://localhost:3002${NC} (or http://$SERVER_IP:3002)"
echo -e "  • PostgreSQL:       ${CYAN}localhost:5432${NC}"
echo -e "  • Redis:            ${CYAN}localhost:6379${NC}"
echo ""
echo -e "${CYAN}🌐 Network Access:${NC}"
if [ ! -z "$SERVER_IP" ]; then
    echo -e "  • Dashboard:        ${CYAN}http://$SERVER_IP:3002${NC}"
    echo -e "  • API:              ${CYAN}http://$SERVER_IP:5001${NC}"
else
    echo -e "  ${YELLOW}Could not detect server IP. Use 'ifconfig' to find it.${NC}"
fi
echo ""
echo -e "${CYAN}💡 Quick Tests:${NC}"
echo -e "  • Check API health:  ${CYAN}curl http://localhost:5001/health${NC}"
echo -e "  • Check services:    ${CYAN}./check-services-mac.sh${NC}"
echo ""
echo -e "${YELLOW}⚠ Note: Services are starting in separate Terminal windows.${NC}"
echo -e "${YELLOW}   Check the Terminal windows for startup logs and any errors.${NC}"
echo ""

