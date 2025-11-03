#!/bin/bash

# GitHub Push Script
# Bu script projeyi GitHub'a push eder

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${CYAN}=== GitHub Push Script ===${NC}"
echo ""

cd "$(dirname "$0")"

# Remote kontrolü
if ! git remote get-url origin > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  Remote repository bulunamadı. Ekleniyor...${NC}"
    git remote add origin https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git
fi

echo -e "${CYAN}📋 Remote Repository:${NC}"
git remote -v
echo ""

# Repository var mı kontrol et
echo -e "${CYAN}🔍 Repository durumu kontrol ediliyor...${NC}"
git ls-remote --heads origin main > /dev/null 2>&1
REPO_EXISTS=$?

if [ $REPO_EXISTS -ne 0 ]; then
    echo -e "${YELLOW}⚠️  Repository GitHub'da bulunamadı!${NC}"
    echo ""
    echo -e "${CYAN}📝 Lütfen önce GitHub'da repository oluşturun:${NC}"
    echo -e "   1. https://github.com/new adresine gidin"
    echo -e "   2. Repository name: ${GREEN}dlp-risk-adaptive-protection-csharp${NC}"
    echo -e "   3. Description: ${GREEN}Forcepoint Risk Adaptive Protection - C# Implementation${NC}"
    echo -e "   4. ✅ ${GREEN}Private${NC} seçin"
    echo -e "   5. ❌ README, .gitignore, license ${YELLOW}işaretlemeyin${NC}"
    echo -e "   6. 'Create repository' tıklayın"
    echo ""
    echo -e "${CYAN}Repository oluşturduktan sonra tekrar bu script'i çalıştırın.${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Repository bulundu!${NC}"
echo ""

# Değişiklikleri kontrol et
if [ -n "$(git status --porcelain)" ]; then
    echo -e "${CYAN}📦 Değişiklikler bulundu, commit ediliyor...${NC}"
    git add .
    
    read -p "Commit mesajı girin (Enter için default): " COMMIT_MSG
    if [ -z "$COMMIT_MSG" ]; then
        COMMIT_MSG="Update: $(date '+%Y-%m-%d %H:%M:%S')"
    fi
    
    git commit -m "$COMMIT_MSG"
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}❌ Commit başarısız!${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✅ Commit oluşturuldu${NC}"
    echo ""
fi

# Push et
echo -e "${CYAN}🚀 GitHub'a push ediliyor...${NC}"
echo -e "${YELLOW}Not: GitHub kullanıcı adı ve şifreniz istenebilir${NC}"
echo ""

git push -u origin main

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}╔════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║   ✅ BAŞARILIYLA PUSH EDİLDİ!                  ║${NC}"
    echo -e "${GREEN}╚════════════════════════════════════════════════╝${NC}"
    echo ""
    echo -e "${CYAN}📦 Repository URL:${NC}"
    echo -e "   ${GREEN}https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp${NC}"
    echo ""
else
    echo ""
    echo -e "${RED}❌ Push başarısız!${NC}"
    echo ""
    echo -e "${YELLOW}Olası nedenler:${NC}"
    echo -e "   1. GitHub kimlik doğrulama hatası"
    echo -e "   2. Repository henüz oluşturulmamış"
    echo -e "   3. İnternet bağlantısı sorunu"
    echo ""
    echo -e "${CYAN}Çözümler:${NC}"
    echo -e "   - SSH key kullanın: ${GREEN}git remote set-url origin git@github.com:onurhan1/dlp-risk-adaptive-protection-csharp.git${NC}"
    echo -e "   - Personal Access Token kullanın"
    echo -e "   - Repository'nin oluşturulduğundan emin olun"
    exit 1
fi

