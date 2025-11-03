#!/bin/bash

# .NET SDK Kurulum Script'i
# Bu script .NET SDK'yı kurmak için kullanılır

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}=== .NET 8.0 SDK Kurulumu ===${NC}"
echo ""

# Check if already installed
if command -v dotnet &> /dev/null; then
    VERSION=$(dotnet --version)
    echo -e "${GREEN}✅ .NET SDK zaten kurulu: $VERSION${NC}"
    exit 0
fi

echo -e "${YELLOW}.NET SDK bulunamadı. Kurulum başlatılıyor...${NC}"
echo ""

# Method 1: Try Homebrew
echo -e "${CYAN}[1/3] Homebrew ile kurulum deneniyor...${NC}"
if command -v brew &> /dev/null; then
    echo -e "${YELLOW}Homebrew ile kurulum için sudo şifresi gerekecek.${NC}"
    echo ""
    echo -e "${CYAN}Şu komutu Terminal'de çalıştırın:${NC}"
    echo -e "${GREEN}brew install --cask dotnet-sdk@8${NC}"
    echo ""
    read -p "Şimdi Homebrew ile kurmak istiyor musunuz? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        if brew install --cask dotnet-sdk@8; then
            echo -e "${GREEN}✅ .NET SDK kuruldu!${NC}"
            dotnet --version
            exit 0
        else
            echo -e "${YELLOW}⚠️ Homebrew kurulumu başarısız, alternatif yöntem deneniyor...${NC}"
        fi
    fi
else
    echo -e "${YELLOW}Homebrew bulunamadı.${NC}"
fi

# Method 2: Manual download
echo ""
echo -e "${CYAN}[2/3] Manuel indirme seçeneği...${NC}"
echo -e "${YELLOW}Manuel kurulum için:${NC}"
echo ""
echo "1. Tarayıcıda şu adresi açın:"
echo -e "   ${CYAN}https://dotnet.microsoft.com/download/dotnet/8.0${NC}"
echo ""
echo "2. 'macOS' için '.NET SDK 8.0' indirin"
echo "   - ARM64 (Apple Silicon) VEYA x64 (Intel) seçin"
echo ""
echo "3. İndirilen .pkg dosyasını çalıştırın"
echo ""

# Check if package already downloaded
PACKAGE_PATH=$(find ~/Library/Caches/Homebrew/downloads -name "*dotnet-sdk*.pkg" 2>/dev/null | head -1)

if [ ! -z "$PACKAGE_PATH" ]; then
    echo -e "${GREEN}✓ İndirilmiş paket bulundu: $PACKAGE_PATH${NC}"
    echo ""
    echo -e "${CYAN}[3/3] İndirilmiş paketi kurma...${NC}"
    echo -e "${YELLOW}Paketi kurmak için şu komutu çalıştırın (sudo şifresi gerekecek):${NC}"
    echo -e "${GREEN}sudo installer -pkg \"$PACKAGE_PATH\" -target /${NC}"
    echo ""
    read -p "Şimdi installer ile kurmak istiyor musunuz? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        if sudo installer -pkg "$PACKAGE_PATH" -target /; then
            echo -e "${GREEN}✅ .NET SDK kuruldu!${NC}"
            # Add to PATH if needed
            if ! command -v dotnet &> /dev/null; then
                echo -e "${YELLOW}PATH'e ekleniyor...${NC}"
                export PATH="/usr/local/share/dotnet:$PATH"
                if command -v dotnet &> /dev/null; then
                    dotnet --version
                    echo ""
                    echo -e "${YELLOW}PATH'e kalıcı olarak eklemek için ~/.zshrc veya ~/.bash_profile'a şunu ekleyin:${NC}"
                    echo -e "${CYAN}export PATH=\"/usr/local/share/dotnet:\$PATH\"${NC}"
                fi
            else
                dotnet --version
            fi
            exit 0
        else
            echo -e "${RED}✗ Installer başarısız${NC}"
        fi
    fi
fi

echo ""
echo -e "${YELLOW}=== Kurulum Talimatları ===${NC}"
echo ""
echo -e "${CYAN}Seçenek 1: Homebrew (Önerilen)${NC}"
echo "  brew install --cask dotnet-sdk@8"
echo ""
echo -e "${CYAN}Seçenek 2: Manuel İndirme${NC}"
echo "  1. https://dotnet.microsoft.com/download/dotnet/8.0"
echo "  2. macOS için .NET SDK 8.0 indirin"
echo "  3. .pkg dosyasını çalıştırın"
echo ""
echo -e "${CYAN}Kurulum sonrası kontrol:${NC}"
echo "  dotnet --version"
echo ""

