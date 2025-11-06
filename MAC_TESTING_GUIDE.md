# Mac Test Rehberi - C# Versiyonu

## 📋 Önemli Not

**⚠️ WPF Dashboard Mac'te çalışmaz!** WPF sadece Windows için tasarlanmıştır.

Mac'te test edilebilecekler:
- ✅ **Collector Service** - Cross-platform çalışır
- ✅ **Analyzer API** - Cross-platform çalışır
- ✅ **Swagger UI** - Tarayıcıdan API test edilebilir
- ❌ **WPF Dashboard** - Sadece Windows'ta çalışır

**Alternatif Dashboard Çözümleri:**
- Swagger UI (http://localhost:8000/swagger)
- Postman/Insomnia ile API testleri
- Basit HTML/JavaScript dashboard (ileride eklenebilir)

---

## 🔧 Gereksinimler

### 1. .NET 8.0 SDK Kurulumu

```bash
# Homebrew ile (önerilen)
brew install dotnet@8

# VEYA Manuel kurulum
# https://dotnet.microsoft.com/download/dotnet/8.0
# macOS x64 veya ARM64 indirin ve kurun

# Kurulumu doğrulayın
dotnet --version
# Beklenen: 8.0.xxx
```

### 2. PostgreSQL + TimescaleDB

#### Seçenek A: Homebrew ile PostgreSQL (Manuel TimescaleDB)

```bash
# PostgreSQL kurun
brew install postgresql@16

# PostgreSQL'i başlatın
brew services start postgresql@16

# TimescaleDB extension'ı kurun
# https://docs.timescale.com/install/latest/self-hosted/installation-macos/
```

#### Seçenek B: Docker ile TimescaleDB (Önerilen - Kolay)

```bash
# Docker Desktop kurun: https://www.docker.com/products/docker-desktop/

# TimescaleDB container'ı çalıştırın
docker run -d \
  --name timescaledb \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=dlp_analytics \
  -p 5432:5432 \
  timescale/timescaledb:latest-pg16
```

### 3. Redis Server

#### Seçenek A: Homebrew ile Redis

```bash
brew install redis

# Redis'i başlatın
brew services start redis

# VEYA manuel başlatma
redis-server
```

#### Seçenek B: Docker ile Redis

```bash
docker run -d \
  --name redis \
  -p 6379:6379 \
  redis:7-alpine
```

### 4. Git (Genellikle zaten kurulu)

```bash
# Kontrol edin
git --version
```

---

## 📁 Proje Kurulumu

### 1. Proje Klasörüne Gidin

```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"
```

### 2. Solution'ı Restore Edin

```bash
# NuGet paketlerini restore edin
dotnet restore

# Beklenen çıktı:
# Determining projects to restore...
# Restored DLP.RiskAnalyzer.Shared...
# ...
```

### 3. Projeyi Build Edin

```bash
# Tüm solution'ı build edin
dotnet build

# Beklenen çıktı: "Build succeeded."
```

**Not**: WPF Dashboard build edilmeye çalışıldığında uyarı verebilir. Bu normaldir, Mac'te WPF çalışmadığı için.

---

## 🗄️ Veritabanı Kurulumu

### 1. PostgreSQL Connection Test

```bash
# PostgreSQL'in çalıştığını kontrol edin
psql -U postgres -h localhost -d postgres

# VEYA Docker container için:
docker exec -it timescaledb psql -U postgres
```

### 2. Database Oluşturma (PostgreSQL CLI)

```sql
-- PostgreSQL'e bağlanın ve şunu çalıştırın:
CREATE DATABASE dlp_analytics;

-- Database'e geçin
\c dlp_analytics

-- TimescaleDB extension'ı etkinleştir (TimescaleDB kuruluysa)
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Çıkış
\q
```

### 3. Entity Framework Migrations

```bash
# Analyzer projesine gidin
cd "DLP.RiskAnalyzer.Analyzer"

# Entity Framework Core Tools kurun (ilk kez)
dotnet tool install --global dotnet-ef --version 8.0.0

# Migration oluştur (eğer yoksa)
dotnet ef migrations add InitialCreate

# Database'i oluştur ve güncelle
dotnet ef database update

# Ana klasöre dönün
cd ../..
```

---

## ⚙️ Yapılandırma

### 1. Collector Service Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Collector/appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD"
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### 2. Analyzer API Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Analyzer/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analytics;Username=postgres;Password=postgres"
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD"
  },
  "Reports": {
    "Directory": "reports"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**⚠️ ÖNEMLİ NOTLAR:**
- PostgreSQL şifresi: Docker kullanıyorsanız `postgres`, Homebrew kullanıyorsanız Mac user şifreniz
- **Forcepoint DLP Bilgileri**: `YOUR_DLP_MANAGER_IP`, `YOUR_DLP_USERNAME`, `YOUR_DLP_PASSWORD` placeholder'larını **kendi ortamınıza göre** doldurmanız gerekiyor!
  - `YOUR_DLP_MANAGER_IP`: Forcepoint DLP Manager sunucusunun IP adresi veya hostname (örnek: `10.0.0.100` veya `dlp.company.com`)
  - `YOUR_DLP_USERNAME`: Forcepoint DLP API için oluşturulmuş kullanıcı adı
  - `YOUR_DLP_PASSWORD`: Forcepoint DLP API kullanıcı şifresi

---

## 🚀 Servisleri Çalıştırma (Mac)

### Terminal 1: Analyzer API

```bash
cd "DLP.RiskAnalyzer.Analyzer"
dotnet run

# Beklenen çıktı:
# Now listening on: http://localhost:8000
# Swagger UI: http://localhost:8000/swagger
```

### Terminal 2: Collector Service

```bash
cd "DLP.RiskAnalyzer.Collector"
dotnet run

# Collector arka planda çalışacak, her saat başı veri toplayacak
# Log çıktılarını göreceksiniz
```

### Terminal 3: (Opsiyonel) Log Monitoring

```bash
# API loglarını takip etmek için
tail -f logs/api.log

# VEYA Collector loglarını
tail -f logs/collector.log
```

---

## ✅ Test Adımları

### 1. Health Check Test

```bash
# Terminal'de:
curl http://localhost:8000/health

# Beklenen response:
# {"status":"healthy","timestamp":"2024-11-03T12:00:00+03:00"}

# VEYA tarayıcıda açın:
# http://localhost:8000/health
```

### 2. Swagger UI Test

Tarayıcıda açın: **http://localhost:8000/swagger**

- Tüm API endpoint'lerini görebilirsiniz
- Endpoint'leri tarayıcıdan test edebilirsiniz
- Request/Response örneklerini görebilirsiniz

### 3. API Endpoint Testleri (cURL)

#### Get Incidents
```bash
curl -X GET "http://localhost:8000/api/incidents?limit=10" \
  -H "accept: application/json"
```

#### Get User Risk Trends
```bash
curl -X GET "http://localhost:8000/api/risk/trends?days=30" \
  -H "accept: application/json"
```

#### Get Daily Summary
```bash
curl -X GET "http://localhost:8000/api/risk/daily-summary?days=7" \
  -H "accept: application/json"
```

#### Process Redis Stream
```bash
curl -X POST "http://localhost:8000/api/process/redis-stream" \
  -H "accept: application/json"
```

#### Run Daily Analysis
```bash
curl -X POST "http://localhost:8000/api/analyze/daily" \
  -H "accept: application/json"
```

### 4. Collector Service Test

Collector'ın çalıştığını kontrol edin:

```bash
# Collector loglarında şunları görmelisiniz:
# - "DLP Collector Service started"
# - "Starting incident collection..."
# - "Access token obtained"
# - "Fetched X incidents from DLP API"
# - "Successfully collected and pushed X incidents"
```

---

## 🔍 Kapsamlı Test Senaryoları

### Senaryo 1: End-to-End Data Flow Test

1. **Collector'ı başlatın** (Terminal 2)
2. **Collector'ın veri topladığını kontrol edin**
3. **Redis Stream'den veri okunup okunmadığını kontrol edin**
4. **Analyzer API'yi başlatın** (Terminal 1)
5. **Redis Stream'i process edin**: `POST /api/process/redis-stream`
6. **Incident'leri sorgulayın**: `GET /api/incidents`
7. **Risk skorlarının hesaplandığını doğrulayın**: `POST /api/analyze/daily`

### Senaryo 2: API Functionality Test

Tüm endpoint'leri Swagger UI'dan test edin:

1. ✅ Health Check
2. ✅ Get Incidents (filters ile)
3. ✅ Get Risk Trends
4. ✅ Get Daily Summary
5. ✅ Get Department Summary
6. ✅ Get Risk Heatmap
7. ✅ Get User List
8. ✅ Get Channel Activity
9. ✅ Get IOB Detections
10. ✅ Policy Recommendations
11. ✅ Anomaly Detection
12. ✅ Classification Details
13. ✅ Reports Generation

### Senaryo 3: Database Integration Test

```bash
# PostgreSQL'e bağlanın
psql -U postgres -d dlp_analytics

# Tabloların oluşturulduğunu kontrol edin
\dt

# Incident kayıtlarını kontrol edin
SELECT COUNT(*) FROM incidents;

# Risk skorlarını kontrol edin
SELECT user_email, AVG(risk_score) as avg_risk 
FROM incidents 
WHERE risk_score IS NOT NULL 
GROUP BY user_email 
ORDER BY avg_risk DESC 
LIMIT 10;

\q
```

### Senaryo 4: Redis Integration Test

```bash
# Redis CLI'ye bağlanın
redis-cli

# Stream'i kontrol edin
XINFO STREAM dlp:incidents

# Son mesajları okuyun
XREAD COUNT 10 STREAMS dlp:incidents 0

# Çıkış
exit
```

---

## 🧪 Otomatik Test Script'i

`test-mac.sh` script'i oluşturuldu (aşağıda detaylar):

```bash
# Test script'ini çalıştırın
chmod +x test-mac.sh
./test-mac.sh
```

Bu script:
- ✅ Service durumlarını kontrol eder
- ✅ API health check yapar
- ✅ Temel endpoint'leri test eder
- ✅ Database bağlantısını kontrol eder
- ✅ Redis bağlantısını kontrol eder

---

## 🔧 Troubleshooting (Mac Özel)

### Problem 1: "dotnet: command not found"

**Çözüm**:
```bash
# Homebrew ile kurun
brew install dotnet@8

# PATH'e ekleyin (~/.zshrc veya ~/.bash_profile)
echo 'export PATH="/opt/homebrew/bin:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

### Problem 2: PostgreSQL Connection Error

**Çözüm**:
```bash
# PostgreSQL'in çalıştığını kontrol edin
brew services list | grep postgresql

# VEYA Docker container için:
docker ps | grep timescaledb

# Eğer Docker container durmuşsa:
docker start timescaledb
```

### Problem 3: Redis Connection Error

**Çözüm**:
```bash
# Redis'in çalıştığını kontrol edin
brew services list | grep redis

# VEYA manuel başlatın:
redis-server

# VEYA Docker container için:
docker start redis
```

### Problem 4: Port Already in Use

**Çözüm**:
```bash
# Port 8000'i kullanan process'i bulun
lsof -i :8000

# Process'i sonlandırın (PID ile)
kill -9 <PID>

# VEYA tüm .NET process'lerini:
pkill -f dotnet
```

### Problem 5: Migration Hatası

**Çözüm**:
```bash
cd "DLP.RiskAnalyzer.Analyzer"

# Database'i sıfırlayın (DİKKAT: Tüm veriler silinir!)
dotnet ef database drop --force
dotnet ef database update

cd ../..
```

### Problem 6: SSL Certificate Hatası (DLP API)

**Çözüm**:
- Mac'te de SSL validation bypass kullanılıyor
- Eğer hala hata alıyorsanız, `PolicyService.cs` ve `RemediationService.cs` dosyalarındaki SSL ayarlarını kontrol edin

### Problem 7: WPF Build Uyarıları

**Çözüm**:
- WPF Mac'te çalışmadığı için build uyarıları normaldir
- Sadece Collector ve Analyzer projelerini test edebilirsiniz
- Dashboard için Swagger UI kullanın

---

## 📊 Servis Durumu Kontrolü

### Manuel Kontrol

```bash
# PostgreSQL
psql -U postgres -h localhost -d dlp_analytics -c "SELECT 1;"

# Redis
redis-cli ping
# Beklenen: PONG

# Analyzer API
curl http://localhost:8000/health
```

### Otomatik Kontrol Script'i

`check-services-mac.sh` script'ini kullanın (aşağıda detaylar).

---

## 🎯 Test Özeti Checklist

Test edilecek öğeler:

- [ ] .NET 8 SDK kurulu ve çalışıyor
- [ ] PostgreSQL çalışıyor ve bağlanılabiliyor
- [ ] Redis çalışıyor ve bağlanılabiliyor
- [ ] Database migrations başarılı
- [ ] Collector Service başlatılabiliyor
- [ ] Analyzer API başlatılabiliyor
- [ ] Health check endpoint çalışıyor
- [ ] Swagger UI açılabiliyor
- [ ] GET /api/incidents endpoint çalışıyor
- [ ] Risk calculation endpoint'leri çalışıyor
- [ ] Redis stream processing çalışıyor
- [ ] Database'e veri yazılabiliyor
- [ ] Reports generation çalışıyor

---

## 📝 Sonuç

Mac'te test edilebilecekler:
- ✅ **Collector Service**: Çalışır ✅
- ✅ **Analyzer API**: Çalışır ✅
- ✅ **Swagger UI**: Çalışır ✅
- ✅ **Database Operations**: Çalışır ✅
- ✅ **Redis Integration**: Çalışır ✅
- ❌ **WPF Dashboard**: Çalışmaz (Windows only)

**Mac'te test tamamlandığında, Windows'ta WPF Dashboard'ı test etmeniz gerekecek.**

---

## 🔗 Hızlı Referans

```bash
# Tüm servisleri başlatmak için (3 terminal)
# Terminal 1:
cd DLP.RiskAnalyzer.Analyzer && dotnet run

# Terminal 2:
cd DLP.RiskAnalyzer.Collector && dotnet run

# Test için:
curl http://localhost:8000/health
open http://localhost:8000/swagger
```

---

**Mac test rehberi hazır! 🍎✅**

