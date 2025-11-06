# Mac Kurulum Durumu

## ✅ Tamamlanan Kurulumlar

### 1. Docker Container'lar ✅
- **TimescaleDB**: Çalışıyor (Port 5432)
- **Redis**: Çalışıyor (Port 6379)

### 2. Database Hazırlığı ✅
- **Database**: `dlp_analytics` oluşturuldu
- **TimescaleDB Extension**: Hazır
- **Connection**: localhost:5432

### 3. Yapılandırma Dosyaları ✅
- `DLP.RiskAnalyzer.Collector/appsettings.json` - Hazır (placeholder değerlerle)
- `DLP.RiskAnalyzer.Analyzer/appsettings.json` - Hazır (placeholder değerlerle)

### 4. Script'ler ✅
- `setup-mac.sh` - Kurulum script'i
- `start-mac.sh` - Servis başlatma
- `test-mac.sh` - Test suite
- `check-services-mac.sh` - Durum kontrolü
- `complete-setup.sh` - **Tam kurulum (yeni)**

---

## ❌ Manuel Kurulum Gereken

### .NET 8.0 SDK

**⚠️ ÖNEMLİ**: .NET SDK kurulumu için **sudo şifresi** gerekiyor.

#### Seçenek 1: Homebrew ile (Önerilen)
```bash
brew install --cask dotnet-sdk@8
```
*(Sudo şifresi istenecek)*

#### Seçenek 2: Manuel İndirme
1. Tarayıcıda açın: https://dotnet.microsoft.com/download/dotnet/8.0
2. **macOS** için **.NET SDK 8.0** indirin
3. ARM64 veya x64 seçin (Mac'inize göre)
4. İndirilen `.pkg` dosyasını çalıştırın

#### Kurulum Kontrolü
```bash
dotnet --version
# Beklenen: 8.0.xxx
```

---

## 🚀 .NET SDK Kurulduktan Sonra

### Hızlı Kurulum (Önerilen):
```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"
./complete-setup.sh
```

Bu script otomatik olarak:
1. ✅ NuGet paketlerini restore eder
2. ✅ Projeleri build eder
3. ✅ Entity Framework Tools kurar
4. ✅ Database migration'ı çalıştırır

### VEYA Adım Adım:
```bash
# 1. Restore
dotnet restore

# 2. Build
dotnet build

# 3. EF Tools
dotnet tool install --global dotnet-ef --version 8.0.0

# 4. Migration
cd DLP.RiskAnalyzer.Analyzer
dotnet ef database update
cd ../..
```

---

## ⚙️ Yapılandırma

.NET SDK kurulduktan ve migration tamamlandıktan sonra:

### 1. Collector Yapılandırması
**Dosya**: `DLP.RiskAnalyzer.Collector/appsettings.json`
```json
{
  "DLP": {
    "ManagerIP": "GERÇEK_IP_ADRESİ",
    "Username": "GERÇEK_KULLANICI_ADI",
    "Password": "GERÇEK_ŞİFRE"
  }
}
```

### 2. Analyzer Yapılandırması
**Dosya**: `DLP.RiskAnalyzer.Analyzer/appsettings.json`
```json
{
  "DLP": {
    "ManagerIP": "GERÇEK_IP_ADRESİ",
    "Username": "GERÇEK_KULLANICI_ADI",
    "Password": "GERÇEK_ŞİFRE"
  }
}
```

---

## ✅ Test

Yapılandırma tamamlandıktan sonra:

```bash
# Servisleri başlat
./start-mac.sh

# VEYA manuel:
cd DLP.RiskAnalyzer.Analyzer && dotnet run
# (Başka terminal)
cd DLP.RiskAnalyzer.Collector && dotnet run

# Health check
curl http://localhost:8000/health

# Swagger UI
open http://localhost:8000/swagger
```

---

## 📊 Mevcut Durum

| Öğe | Durum |
|-----|-------|
| Docker | ✅ Çalışıyor |
| TimescaleDB | ✅ Container çalışıyor |
| Redis | ✅ Container çalışıyor |
| Database | ✅ Oluşturuldu |
| Script'ler | ✅ Hazır |
| .NET SDK | ❌ **Manuel kurulum gerekiyor** |
| NuGet Restore | ⏳ .NET SDK sonrası |
| Build | ⏳ .NET SDK sonrası |
| Migration | ⏳ .NET SDK sonrası |

---

## 🎯 Sonraki Adım

**Önce .NET SDK'yı kurun:**
```bash
brew install --cask dotnet-sdk@8
```

**Sonra tam kurulumu çalıştırın:**
```bash
./complete-setup.sh
```

**Son olarak yapılandırma ve test:**
1. `appsettings.json` dosyalarını düzenleyin
2. `./start-mac.sh` ile servisleri başlatın
3. `./test-mac.sh` ile test edin

---

**Kurulum %80 tamamlandı! .NET SDK kurulumu sonrası hazırsınız! 🚀**

