# Mac Kurulum Özeti

## ✅ Hazır Olanlar

- ✅ **Docker**: Çalışıyor
- ✅ **TimescaleDB Container**: Çalışıyor (Port 5432)
- ✅ **Redis Container**: Çalışıyor (Port 6379)
- ✅ **Proje Dosyaları**: Mevcut

## ❌ Kurulması Gerekenler

### 1. .NET 8.0 SDK

**Seçenek A: Homebrew ile (Önerilen)**
```bash
brew install --cask dotnet-sdk@8
```

**Seçenek B: Manuel İndirme**
1. https://dotnet.microsoft.com/download/dotnet/8.0 adresine gidin
2. macOS için .NET SDK 8.0 indirin (ARM64 veya x64)
3. İndirilen .pkg dosyasını çalıştırın

**Kurulum Kontrolü:**
```bash
dotnet --version
# Beklenen: 8.0.xxx
```

## 🚀 Hızlı Kurulum

Tüm kurulumu otomatik yapmak için:

```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"
./setup-mac.sh
```

Bu script:
- .NET SDK'yı kontrol eder/kurur
- Docker container'larını kontrol eder/başlatır
- NuGet paketlerini restore eder
- Projeyi build eder
- Entity Framework Tools kurar

## 📝 Yapılandırma

Kurulumdan sonra yapılandırma dosyalarını düzenleyin:

1. **`DLP.RiskAnalyzer.Collector/appsettings.json`**
   - `YOUR_DLP_MANAGER_IP` → Gerçek IP adresi
   - `YOUR_DLP_USERNAME` → Gerçek kullanıcı adı
   - `YOUR_DLP_PASSWORD` → Gerçek şifre

2. **`DLP.RiskAnalyzer.Analyzer/appsettings.json`**
   - `YOUR_DLP_MANAGER_IP` → Gerçek IP adresi
   - `YOUR_DLP_USERNAME` → Gerçek kullanıcı adı
   - `YOUR_DLP_PASSWORD` → Gerçek şifre
   - PostgreSQL şifresi (Docker için genellikle `postgres`)

## 🗄️ Database Migration

```bash
cd DLP.RiskAnalyzer.Analyzer

# EF Tools kur (ilk kez)
dotnet tool install --global dotnet-ef --version 8.0.0

# Migration çalıştır
dotnet ef database update

cd ../..
```

## ▶️ Servisleri Başlatma

### Otomatik:
```bash
./start-mac.sh
```

### Manuel:
```bash
# Terminal 1: Analyzer API
cd DLP.RiskAnalyzer.Analyzer
dotnet run

# Terminal 2: Collector
cd DLP.RiskAnalyzer.Collector
dotnet run
```

## ✅ Test

```bash
# Servis durumu kontrolü
./check-services-mac.sh

# API health check
curl http://localhost:8000/health

# Swagger UI
open http://localhost:8000/swagger
```

## 📚 Detaylı Dokümantasyon

- `MAC_TESTING_GUIDE.md` - Detaylı test rehberi
- `CONFIGURATION_NOTES.md` - Yapılandırma notları

---

**Kurulum tamamlandıktan sonra test edebilirsiniz! 🎉**

