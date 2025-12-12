#!/bin/bash

# Service Status Check Script for macOS
# Checks the status of all required services for DLP Risk Analyzer

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}=== DLP Risk Analyzer - Service Status Check (macOS) ===${NC}"
echo ""

# PostgreSQL Check
echo -e "${YELLOW}[PostgreSQL]${NC}"
if command -v psql &> /dev/null; then
    if psql -U postgres -h localhost -d dlp_analytics -c "SELECT 1;" > /dev/null 2>&1; then
        echo -e "  ${GREEN}✓ Connected successfully${NC}"
        
        # Check if TimescaleDB extension is enabled
        TIMESCALE_CHECK=$(psql -U postgres -h localhost -d dlp_analytics -t -c "SELECT COUNT(*) FROM pg_extension WHERE extname = 'timescaledb';" 2>/dev/null | xargs)
        if [ "$TIMESCALE_CHECK" = "1" ]; then
            echo -e "  ${GREEN}✓ TimescaleDB extension enabled${NC}"
        else
            echo -e "  ${YELLOW}⚠ TimescaleDB extension not enabled${NC}"
        fi
    else
        echo -e "  ${RED}✗ Connection failed${NC}"
    fi
else
    echo -e "  ${YELLOW}⚠ psql not found (check if using Docker)${NC}"
fi

# Docker PostgreSQL Container
if command -v docker &> /dev/null; then
    TIMESCALE_CONTAINER=$(docker ps --filter "name=timescaledb" --format "{{.Names}}" 2>/dev/null)
    if [ ! -z "$TIMESCALE_CONTAINER" ]; then
        TIMESCALE_STATUS=$(docker ps --filter "name=timescaledb" --format "{{.Status}}" 2>/dev/null)
        echo -e "  ${GREEN}✓ Docker Container: $TIMESCALE_CONTAINER - $TIMESCALE_STATUS${NC}"
    fi
fi

echo ""

# Redis Check
echo -e "${YELLOW}[Redis]${NC}"
if command -v redis-cli &> /dev/null; then
    REDIS_PING=$(redis-cli ping 2>/dev/null)
    if [ "$REDIS_PING" = "PONG" ]; then
        echo -e "  ${GREEN}✓ Connected successfully (PONG)${NC}"
    else
        echo -e "  ${RED}✗ Connection failed${NC}"
    fi
else
    echo -e "  ${YELLOW}⚠ redis-cli not found${NC}"
fi

# Docker Redis Container
if command -v docker &> /dev/null; then
    REDIS_CONTAINER=$(docker ps --filter "name=redis" --format "{{.Names}}" 2>/dev/null)
    if [ ! -z "$REDIS_CONTAINER" ]; then
        REDIS_STATUS=$(docker ps --filter "name=redis" --format "{{.Status}}" 2>/dev/null)
        echo -e "  ${GREEN}✓ Docker Container: $REDIS_CONTAINER - $REDIS_STATUS${NC}"
    fi
fi

# Homebrew Services
if command -v brew &> /dev/null; then
    echo ""
    echo -e "${YELLOW}[Homebrew Services]${NC}"
    POSTGRES_SERVICE=$(brew services list 2>/dev/null | grep postgresql | awk '{print $2}')
    REDIS_SERVICE=$(brew services list 2>/dev/null | grep redis | awk '{print $2}')
    
    if [ ! -z "$POSTGRES_SERVICE" ]; then
        echo -e "  PostgreSQL: $POSTGRES_SERVICE"
    fi
    
    if [ ! -z "$REDIS_SERVICE" ]; then
        echo -e "  Redis: $REDIS_SERVICE"
    fi
fi

# Analyzer API Check
echo ""
echo -e "${YELLOW}[Analyzer API]${NC}"
if curl -s http://localhost:8000/health > /dev/null 2>&1; then
    HEALTH_RESPONSE=$(curl -s http://localhost:8000/health)
    echo -e "  ${GREEN}✓ Running and healthy${NC}"
    echo -e "  ${CYAN}Response: $HEALTH_RESPONSE${NC}"
else
    echo -e "  ${RED}✗ Not responding${NC}"
    echo -e "  ${YELLOW}  Start with: cd DLP.RiskAnalyzer.Analyzer && dotnet run${NC}"
fi

# Port Status
echo ""
echo -e "${YELLOW}[Port Status]${NC}"
PORTS=(8000 5432 6379)
for port in "${PORTS[@]}"; do
    if lsof -i :$port > /dev/null 2>&1; then
        PROCESS=$(lsof -i :$port | tail -n 1 | awk '{print $1}')
        echo -e "  ${GREEN}✓ Port $port: Open (Process: $PROCESS)${NC}"
    else
        echo -e "  ${RED}✗ Port $port: Closed${NC}"
    fi
done

# .NET Runtime Check
echo ""
echo -e "${YELLOW}[.NET Runtime]${NC}"
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo -e "  ${GREEN}✓ .NET SDK: $DOTNET_VERSION${NC}"
    
    # Check ASP.NET Core runtime
    ASPNET_RUNTIME=$(dotnet --list-runtimes | grep -i "Microsoft.AspNetCore.App" | tail -n 1)
    if [ ! -z "$ASPNET_RUNTIME" ]; then
        echo -e "  ${GREEN}✓ ASP.NET Core Runtime: Installed${NC}"
    else
        echo -e "  ${YELLOW}⚠ ASP.NET Core Runtime: Not found${NC}"
    fi
else
    echo -e "  ${RED}✗ .NET SDK: Not found${NC}"
fi

# Docker Status
echo ""
echo -e "${YELLOW}[Docker]${NC}"
if command -v docker &> /dev/null; then
    if docker ps > /dev/null 2>&1; then
        echo -e "  ${GREEN}✓ Docker: Running${NC}"
        CONTAINER_COUNT=$(docker ps | tail -n +2 | wc -l | xargs)
        echo -e "  ${CYAN}  Running containers: $CONTAINER_COUNT${NC}"
    else
        echo -e "  ${RED}✗ Docker: Not running or not accessible${NC}"
    fi
else
    echo -e "  ${YELLOW}⚠ Docker: Not installed${NC}"
fi

echo ""
echo -e "${CYAN}=== Check Complete ===${NC}"

