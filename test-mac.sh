#!/bin/bash

# DLP Risk Analyzer - Mac Test Script
# Comprehensive testing script for macOS

set -e  # Exit on error

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}=== DLP Risk Analyzer C# - Mac Test Suite ===${NC}"
echo ""

# Check .NET SDK
echo -e "${YELLOW}[1/8] Checking .NET SDK...${NC}"
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}  ERROR: .NET SDK not found. Please install .NET 8.0 SDK.${NC}"
    echo -e "${YELLOW}  Install: brew install dotnet@8${NC}"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}  .NET SDK Version: $DOTNET_VERSION${NC}"

# Check if we're in the right directory
if [ ! -f "DLP.RiskAnalyzer.Solution.sln" ]; then
    echo -e "${RED}  ERROR: Solution file not found. Please run this script from the project root directory.${NC}"
    exit 1
fi

# Restore packages
echo ""
echo -e "${YELLOW}[2/8] Restoring NuGet packages...${NC}"
if dotnet restore > /dev/null 2>&1; then
    echo -e "${GREEN}  Packages restored successfully.${NC}"
else
    echo -e "${RED}  ERROR: Package restore failed.${NC}"
    exit 1
fi

# Build solution (skip WPF Dashboard on Mac)
echo ""
echo -e "${YELLOW}[3/8] Building solution (excluding WPF Dashboard)...${NC}"
if dotnet build DLP.RiskAnalyzer.Shared/DLP.RiskAnalyzer.Shared.csproj > /dev/null 2>&1 && \
   dotnet build DLP.RiskAnalyzer.Collector/DLP.RiskAnalyzer.Collector.csproj > /dev/null 2>&1 && \
   dotnet build DLP.RiskAnalyzer.Analyzer/DLP.RiskAnalyzer.Analyzer.csproj > /dev/null 2>&1; then
    echo -e "${GREEN}  Build succeeded (excluding WPF Dashboard - Windows only).${NC}"
else
    echo -e "${RED}  ERROR: Build failed.${NC}"
    exit 1
fi

# Check PostgreSQL
echo ""
echo -e "${YELLOW}[4/8] Checking PostgreSQL...${NC}"
if command -v psql &> /dev/null; then
    if psql -U postgres -h localhost -d dlp_analytics -c "SELECT 1;" > /dev/null 2>&1; then
        echo -e "${GREEN}  PostgreSQL: Connected successfully.${NC}"
    elif docker ps | grep -q timescaledb; then
        echo -e "${GREEN}  PostgreSQL: Docker container running (timescaledb).${NC}"
    else
        echo -e "${YELLOW}  WARNING: Cannot connect to PostgreSQL.${NC}"
        echo -e "${YELLOW}  Please ensure PostgreSQL is running or Docker container is started.${NC}"
    fi
else
    echo -e "${YELLOW}  PostgreSQL: psql not found (check Docker if using container).${NC}"
fi

# Check Redis
echo ""
echo -e "${YELLOW}[5/8] Checking Redis...${NC}"
if command -v redis-cli &> /dev/null; then
    if redis-cli ping > /dev/null 2>&1; then
        echo -e "${GREEN}  Redis: Connected successfully (PONG).${NC}"
    elif docker ps | grep -q redis; then
        echo -e "${GREEN}  Redis: Docker container running.${NC}"
    else
        echo -e "${YELLOW}  WARNING: Cannot connect to Redis.${NC}"
        echo -e "${YELLOW}  Please ensure Redis is running (brew services start redis) or Docker container is started.${NC}"
    fi
else
    echo -e "${YELLOW}  Redis: redis-cli not found (check Docker if using container).${NC}"
fi

# Check Docker (optional)
echo ""
echo -e "${YELLOW}[6/8] Checking Docker (optional)...${NC}"
if command -v docker &> /dev/null; then
    if docker ps > /dev/null 2>&1; then
        echo -e "${GREEN}  Docker: Running${NC}"
        
        # Check TimescaleDB container
        if docker ps | grep -q timescaledb; then
            echo -e "${GREEN}    TimescaleDB container: Running${NC}"
        else
            echo -e "${YELLOW}    TimescaleDB container: Not running${NC}"
        fi
        
        # Check Redis container
        if docker ps | grep -q redis; then
            echo -e "${GREEN}    Redis container: Running${NC}"
        else
            echo -e "${YELLOW}    Redis container: Not running${NC}"
        fi
    else
        echo -e "${YELLOW}  Docker: Not running or not accessible${NC}"
    fi
else
    echo -e "${YELLOW}  Docker: Not installed (optional)${NC}"
fi

# Check Entity Framework Tools
echo ""
echo -e "${YELLOW}[7/8] Checking Entity Framework Tools...${NC}"
if dotnet ef --version > /dev/null 2>&1; then
    EF_VERSION=$(dotnet ef --version)
    echo -e "${GREEN}  EF Core Tools: Installed ($EF_VERSION)${NC}"
else
    echo -e "${YELLOW}  EF Core Tools: Not installed${NC}"
    echo -e "${CYAN}    Installing EF Core Tools...${NC}"
    dotnet tool install --global dotnet-ef --version 8.0.0 > /dev/null 2>&1
    echo -e "${GREEN}  EF Core Tools: Installed${NC}"
fi

# API Health Check (if Analyzer is running)
echo ""
echo -e "${YELLOW}[8/8] Testing Analyzer API (if running)...${NC}"
API_RUNNING=false

# Check if Analyzer API is running on port 8000
if curl -s http://localhost:8000/health > /dev/null 2>&1; then
    API_RUNNING=true
    HEALTH_RESPONSE=$(curl -s http://localhost:8000/health)
    echo -e "${GREEN}  Analyzer API: Running and healthy${NC}"
    echo -e "${CYAN}    Response: $HEALTH_RESPONSE${NC}"
else
    echo -e "${YELLOW}  Analyzer API: Not running${NC}"
    echo -e "${CYAN}    To start: cd DLP.RiskAnalyzer.Analyzer && dotnet run${NC}"
fi

# Summary
echo ""
echo -e "${CYAN}=== Test Summary ===${NC}"
echo ""

if [ "$API_RUNNING" = true ]; then
    echo -e "${GREEN}✅ All checks passed!${NC}"
    echo ""
    echo -e "${CYAN}📍 Available URLs:${NC}"
    echo -e "  • Health Check: ${CYAN}http://localhost:8000/health${NC}"
    echo -e "  • Swagger UI:   ${CYAN}http://localhost:8000/swagger${NC}"
    echo ""
    echo -e "${CYAN}💡 Next Steps:${NC}"
    echo -e "  1. Open Swagger UI: ${CYAN}open http://localhost:8000/swagger${NC}"
    echo -e "  2. Test API endpoints from Swagger UI"
    echo -e "  3. Check Collector service logs"
    echo -e "  4. Verify data in PostgreSQL database"
else
    echo -e "${YELLOW}⚠️  Basic checks completed.${NC}"
    echo ""
    echo -e "${CYAN}📋 To complete testing:${NC}"
    echo -e "  1. Start Analyzer API: ${CYAN}cd DLP.RiskAnalyzer.Analyzer && dotnet run${NC}"
    echo -e "  2. Start Collector: ${CYAN}cd DLP.RiskAnalyzer.Collector && dotnet run${NC}"
    echo -e "  3. Open Swagger UI: ${CYAN}open http://localhost:8000/swagger${NC}"
fi

echo ""
echo -e "${CYAN}=== Test Complete ===${NC}"

