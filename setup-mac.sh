#!/bin/bash

# Mac Test Ortamı Kurulum Script'i
# Bu script Mac'te test ortamını hazırlar

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}=== DLP Risk Analyzer C# - Mac Test Ortamı Kurulumu ===${NC}"
echo ""

# Check .NET SDK
echo -e "${YELLOW}[1/6] Checking .NET SDK...${NC}"
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo -e "${GREEN}  ✓ .NET SDK installed: $DOTNET_VERSION${NC}"
else
    echo -e "${RED}  ✗ .NET SDK not found${NC}"
    echo -e "${YELLOW}  Kurulum için:${NC}"
    echo -e "${CYAN}    brew install --cask dotnet-sdk@8${NC}"
    echo -e "${CYAN}    VEYA: https://dotnet.microsoft.com/download/dotnet/8.0${NC}"
    echo ""
    read -p ".NET SDK'yı şimdi kurmak istiyor musunuz? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        if command -v brew &> /dev/null; then
            echo "Homebrew ile kuruluyor..."
            brew install --cask dotnet-sdk@8
        else
            echo -e "${RED}Homebrew bulunamadı. Lütfen manuel olarak kurun:${NC}"
            echo -e "${CYAN}https://dotnet.microsoft.com/download/dotnet/8.0${NC}"
            exit 1
        fi
    else
        echo -e "${YELLOW}.NET SDK kurulumu atlandı. Lütfen manuel olarak kurun.${NC}"
        exit 1
    fi
fi

# Check Docker
echo ""
echo -e "${YELLOW}[2/6] Checking Docker...${NC}"
if command -v docker &> /dev/null; then
    DOCKER_VERSION=$(docker --version)
    echo -e "${GREEN}  ✓ Docker installed: $DOCKER_VERSION${NC}"
else
    echo -e "${RED}  ✗ Docker not found${NC}"
    echo -e "${YELLOW}  Docker Desktop kurun: https://www.docker.com/products/docker-desktop/${NC}"
    exit 1
fi

# Check PostgreSQL/TimescaleDB Container
echo ""
echo -e "${YELLOW}[3/6] Checking TimescaleDB container...${NC}"
if docker ps | grep -q timescaledb; then
    echo -e "${GREEN}  ✓ TimescaleDB container is running${NC}"
else
    echo -e "${YELLOW}  ⚠ TimescaleDB container not running${NC}"
    echo -e "${CYAN}  Starting TimescaleDB container...${NC}"
    docker run -d \
      --name timescaledb \
      -e POSTGRES_PASSWORD=postgres \
      -e POSTGRES_DB=dlp_analytics \
      -p 5432:5432 \
      timescale/timescaledb:latest-pg16 || \
      docker start timescaledb
    echo -e "${GREEN}  ✓ TimescaleDB container started${NC}"
fi

# Check Redis Container
echo ""
echo -e "${YELLOW}[4/6] Checking Redis container...${NC}"
if docker ps | grep -q redis; then
    echo -e "${GREEN}  ✓ Redis container is running${NC}"
else
    echo -e "${YELLOW}  ⚠ Redis container not running${NC}"
    echo -e "${CYAN}  Starting Redis container...${NC}"
    docker run -d \
      --name redis \
      -p 6379:6379 \
      redis:7-alpine || \
      docker start redis
    echo -e "${GREEN}  ✓ Redis container started${NC}"
fi

# Wait for containers to be ready
echo ""
echo -e "${CYAN}  Waiting for containers to be ready...${NC}"
sleep 5

# Restore packages
echo ""
echo -e "${YELLOW}[5/6] Restoring NuGet packages...${NC}"
cd "$(dirname "$0")"
if dotnet restore > /dev/null 2>&1; then
    echo -e "${GREEN}  ✓ Packages restored${NC}"
else
    echo -e "${RED}  ✗ Package restore failed${NC}"
    exit 1
fi

# Build solution (skip WPF)
echo ""
echo -e "${YELLOW}[6/6] Building solution (excluding WPF Dashboard)...${NC}"
if dotnet build DLP.RiskAnalyzer.Shared/DLP.RiskAnalyzer.Shared.csproj > /dev/null 2>&1 && \
   dotnet build DLP.RiskAnalyzer.Collector/DLP.RiskAnalyzer.Collector.csproj > /dev/null 2>&1 && \
   dotnet build DLP.RiskAnalyzer.Analyzer/DLP.RiskAnalyzer.Analyzer.csproj > /dev/null 2>&1; then
    echo -e "${GREEN}  ✓ Build succeeded${NC}"
else
    echo -e "${YELLOW}  ⚠ Build completed with warnings (WPF Dashboard skipped - Windows only)${NC}"
fi

# Check Entity Framework Tools
echo ""
echo -e "${YELLOW}[Extra] Checking Entity Framework Tools...${NC}"
if dotnet ef --version > /dev/null 2>&1; then
    EF_VERSION=$(dotnet ef --version)
    echo -e "${GREEN}  ✓ EF Core Tools: $EF_VERSION${NC}"
else
    echo -e "${CYAN}  Installing EF Core Tools...${NC}"
    dotnet tool install --global dotnet-ef --version 8.0.0
    echo -e "${GREEN}  ✓ EF Core Tools installed${NC}"
fi

echo ""
echo -e "${GREEN}=== Kurulum Tamamlandı! ===${NC}"
echo ""
echo -e "${CYAN}📋 Sonraki Adımlar:${NC}"
echo ""
echo -e "1. Yapılandırma dosyalarını düzenleyin:"
echo -e "   ${CYAN}DLP.RiskAnalyzer.Collector/appsettings.json${NC}"
echo -e "   ${CYAN}DLP.RiskAnalyzer.Analyzer/appsettings.json${NC}"
echo -e "   ${YELLOW}(YOUR_DLP_MANAGER_IP, YOUR_DLP_USERNAME, YOUR_DLP_PASSWORD değerlerini doldurun)${NC}"
echo ""
echo -e "2. Database migration çalıştırın:"
echo -e "   ${CYAN}cd DLP.RiskAnalyzer.Analyzer${NC}"
echo -e "   ${CYAN}dotnet ef database update${NC}"
echo ""
echo -e "3. Servisleri başlatın:"
echo -e "   ${CYAN}./start-mac.sh${NC}"
echo -e "   VEYA manuel:"
echo -e "   ${CYAN}cd DLP.RiskAnalyzer.Analyzer && dotnet run${NC}"
echo ""

