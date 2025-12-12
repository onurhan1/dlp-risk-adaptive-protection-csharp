#!/bin/bash
# .NET SDK Kurulum Script'i - Çift tıklayarak çalıştırılabilir

echo "=== .NET SDK Kurulumu ==="
echo ""
echo "Mac şifreniz istenecek..."
echo ""

# Try Homebrew first
if command -v brew &> /dev/null; then
    brew install --cask dotnet-sdk@8
else
    echo "Homebrew bulunamadı."
    echo "Manuel kurulum için: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# Check installation
if command -v dotnet &> /dev/null; then
    echo ""
    echo "✅ .NET SDK kuruldu: $(dotnet --version)"
    echo ""
    echo "Sonraki adım: ./complete-setup.sh"
else
    echo ""
    echo "⚠️ Kurulum kontrol edilemedi. PATH'e eklenmiş olabilir."
    echo "Yeni bir Terminal penceresi açıp 'dotnet --version' komutunu deneyin."
fi

read -p "Devam etmek için Enter'a basın..."
