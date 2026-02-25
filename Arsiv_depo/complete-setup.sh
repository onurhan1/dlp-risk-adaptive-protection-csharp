#!/bin/bash

# Tam Kurulum Script'i - .NET SDK kurulduktan sonra çalıştırın
# Mac'te WPF Dashboard build edilmez (Windows only)

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}=== DLP Risk Analyzer - Tam Kurulum (macOS) ===${NC}"
echo ""

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${YELLOW}⚠️  .NET SDK bulunamadı!${NC}"
    echo -e "${CYAN}Lütfen önce .NET SDK'yı kurun:${NC}"
    echo -e "  ${CYAN}brew install --cask dotnet-sdk@8${NC}"
    echo ""
    echo -e "VEYA: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

echo -e "${GREEN}✓ .NET SDK: $(dotnet --version)${NC}"
echo ""

# Project root
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

# 1. Restore packages (excluding WPF Dashboard on Mac)
echo -e "${CYAN}[1/5] Restoring NuGet packages (excluding WPF Dashboard)...${NC}"
dotnet restore DLP.RiskAnalyzer.Shared/DLP.RiskAnalyzer.Shared.csproj
dotnet restore DLP.RiskAnalyzer.Collector/DLP.RiskAnalyzer.Collector.csproj
dotnet restore DLP.RiskAnalyzer.Analyzer/DLP.RiskAnalyzer.Analyzer.csproj
echo -e "${GREEN}✓ Packages restored (WPF Dashboard skipped - Windows only)${NC}"
echo ""

# 2. Build projects (excluding WPF Dashboard - Windows only)
echo -e "${CYAN}[2/5] Building projects (excluding WPF Dashboard - Windows only)...${NC}"
dotnet build DLP.RiskAnalyzer.Shared/DLP.RiskAnalyzer.Shared.csproj --no-restore
dotnet build DLP.RiskAnalyzer.Collector/DLP.RiskAnalyzer.Collector.csproj --no-restore
dotnet build DLP.RiskAnalyzer.Analyzer/DLP.RiskAnalyzer.Analyzer.csproj --no-restore
echo -e "${GREEN}✓ Build completed (WPF Dashboard skipped - Windows only)${NC}"
echo ""

# 3. Install EF Tools
echo -e "${CYAN}[3/5] Installing Entity Framework Tools...${NC}"
if ! dotnet ef --version &> /dev/null; then
    dotnet tool install --global dotnet-ef --version 8.0.0
fi
EF_VERSION=$(dotnet ef --version)
echo -e "${GREEN}✓ EF Core Tools: $EF_VERSION${NC}"
echo ""

# 4. Database migration
echo -e "${CYAN}[4/5] Running database migrations...${NC}"
cd DLP.RiskAnalyzer.Analyzer

# Check database connection
if PGPASSWORD=postgres psql -h localhost -U postgres -d dlp_analytics -c "SELECT 1;" > /dev/null 2>&1; then
    echo "Database bağlantısı başarılı"
    dotnet ef database update --no-build
    echo -e "${GREEN}✓ Database migrations completed${NC}"
else
    echo -e "${YELLOW}⚠️  Database bağlantısı başarısız. PostgreSQL'in çalıştığından emin olun.${NC}"
    echo -e "${CYAN}  Docker container kontrolü: docker ps | grep timescaledb${NC}"
fi

cd "$PROJECT_ROOT"
echo ""

# 5. Final check
echo -e "${CYAN}[5/5] Final checks...${NC}"

# Check Docker containers
if docker ps | grep -q timescaledb; then
    echo -e "${GREEN}✓ TimescaleDB container running${NC}"
else
    echo -e "${YELLOW}⚠️  TimescaleDB container not running${NC}"
fi

if docker ps | grep -q redis; then
    echo -e "${GREEN}✓ Redis container running${NC}"
else
    echo -e "${YELLOW}⚠️  Redis container not running${NC}"
fi

echo ""
echo -e "${GREEN}=== Kurulum Tamamlandı! ===${NC}"
echo ""
echo -e "${CYAN}📋 Sonraki Adımlar:${NC}"
echo ""
echo -e "1. ${YELLOW}Yapılandırma dosyalarını düzenleyin:${NC}"
echo -e "   ${CYAN}DLP.RiskAnalyzer.Collector/appsettings.json${NC}"
echo -e "   ${CYAN}DLP.RiskAnalyzer.Analyzer/appsettings.json${NC}"
echo -e "   ${YELLOW}(YOUR_DLP_MANAGER_IP, YOUR_DLP_USERNAME, YOUR_DLP_PASSWORD değerlerini doldurun)${NC}"
echo ""
echo -e "2. ${CYAN}Servisleri başlatın:${NC}"
echo -e "   ${CYAN}./start-mac.sh${NC}"
echo ""
echo -e "3. ${CYAN}Test edin:${NC}"
echo -e "   ${CYAN}./test-mac.sh${NC}"
echo -e "   ${CYAN}curl http://localhost:8000/health${NC}"
echo ""
echo -e "${YELLOW}Not: WPF Dashboard Mac'te çalışmaz. API'yi Swagger UI'dan test edebilirsiniz:${NC}"
echo -e "${CYAN}open http://localhost:8000/swagger${NC}"
echo ""
