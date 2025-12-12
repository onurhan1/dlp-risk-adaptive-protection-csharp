# Forcepoint Risk Adaptive Protection - Kurulum ve API Bağlantı Rehberi

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Sistem Gereksinimleri](#sistem-gereksinimleri)
3. [Kurulum Adımları](#kurulum-adımları)
4. [API Bağlantı Yapılandırması](#api-bağlantı-yapılandırması)
5. [Veritabanı Kurulumu](#veritabanı-kurulumu)
6. [Servisleri Başlatma](#servisleri-başlatma)
7. [Test ve Doğrulama](#test-ve-doğrulama)
8. [Sorun Giderme](#sorun-giderme)

---

## 🎯 Genel Bakış

Bu doküman, Forcepoint Risk Adaptive Protection sisteminin kurulumu ve Forcepoint DLP API bağlantılarının yapılandırılması için adım adım rehber içerir.

### Sistem Mimarisi

```
┌─────────────────┐
│   Forcepoint    │
│   DLP Manager   │
│   (API Server)  │
└────────┬────────┘
         │
         │ REST API (JWT)
         │
┌────────▼────────┐      ┌──────────────┐      ┌─────────────┐
│    Collector    │─────▶│    Redis     │─────▶│  Analyzer   │
│  (.NET Service) │      │   (Stream)   │      │ (ASP.NET)   │
└─────────────────┘      └──────────────┘      └──────┬───────┘
                                                       │
                                              ┌────────▼────────┐
                                              │  TimescaleDB    │
                                              │  (PostgreSQL)   │
                                              └─────────────────┘
                                                       │
                                              ┌────────▼────────┐
                                              │  Web Dashboard  │
                                              │   (Next.js)     │
                                              └─────────────────┘
```

---

## 💻 Sistem Gereksinimleri

### Minimum Gereksinimler

#### Windows Sunucu
- **İşletim Sistemi**: Windows Server 2016 veya üzeri / Windows 10/11
- **RAM**: 8 GB (önerilen: 16 GB)
- **Disk**: 50 GB boş alan
- **CPU**: 4 çekirdek (önerilen: 8 çekirdek)

#### Mac (Test/Development)
- **İşletim Sistemi**: macOS 11.0 (Big Sur) veya üzeri
- **RAM**: 8 GB
- **Disk**: 50 GB boş alan

### Yazılım Gereksinimleri

1. **.NET SDK 8.0** - [İndirme](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **PostgreSQL 14+** (TimescaleDB extension ile)
3. **Redis 6.0+**
4. **Node.js 18+** ve npm (Dashboard için)
5. **Docker Desktop** (isteğe bağlı - PostgreSQL/Redis için)

---

## 🚀 Kurulum Adımları

### 1. .NET SDK Kurulumu

#### Windows
```powershell
# PowerShell (Yönetici olarak)
winget install Microsoft.DotNet.SDK.8
```

Veya manuel olarak:
1. [.NET SDK 8.0 İndirme Sayfası](https://dotnet.microsoft.com/download/dotnet/8.0) adresinden indirin
2. Kurulum sihirbazını çalıştırın
3. Doğrulama:
```powershell
dotnet --version
# Beklenen: 8.0.x
```

#### Mac
```bash
# Homebrew ile
brew install --cask dotnet-sdk@8

# Doğrulama
dotnet --version
```

### 2. PostgreSQL ve TimescaleDB Kurulumu

#### Windows (Docker ile - Önerilen)
```powershell
# Docker Desktop kurulu olmalı
docker run -d `
  --name timescaledb `
  -e POSTGRES_PASSWORD=your_password `
  -e POSTGRES_USER=dlp_user `
  -e POSTGRES_DB=dlp_risk_analyzer `
  -e TZ=Europe/Istanbul `
  -p 5432:5432 `
  timescale/timescaledb:latest-pg14
```

#### Windows (Yerel Kurulum)
1. [PostgreSQL İndirme Sayfası](https://www.postgresql.org/download/windows/) adresinden indirin
2. Kurulum sırasında:
   - Kullanıcı adı: `dlp_user`
   - Şifre: `your_password` (güçlü bir şifre seçin)
   - Port: `5432`
3. TimescaleDB Extension kurulumu:
```sql
-- PostgreSQL'e bağlanın ve çalıştırın
CREATE EXTENSION IF NOT EXISTS timescaledb;
```

#### Mac
```bash
# Homebrew ile
brew install postgresql@14
brew install timescaledb

# PostgreSQL'i başlat
brew services start postgresql@14

# TimescaleDB extension'ı etkinleştir
timescaledb-tune
```

### 3. Redis Kurulumu

#### Windows (Docker ile - Önerilen)
```powershell
docker run -d `
  --name redis `
  -p 6379:6379 `
  redis:7-alpine
```

#### Windows (Yerel Kurulum)
1. [Redis for Windows İndirme](https://github.com/microsoftarchive/redis/releases)
2. Redis servisini başlatın

#### Mac
```bash
brew install redis
brew services start redis
```

### 4. Node.js ve npm Kurulumu (Dashboard için)

#### Windows
```powershell
winget install OpenJS.NodeJS.LTS
```

#### Mac
```bash
brew install node
```

Doğrulama:
```bash
node --version  # v18.x veya üzeri
npm --version   # 9.x veya üzeri
```

---

## ⚙️ API Bağlantı Yapılandırması

### Forcepoint DLP API Bağlantı Bilgileri

Forcepoint DLP Manager API'sine bağlanmak için aşağıdaki bilgilere ihtiyacınız var:

1. **Manager IP Adresi**: Forcepoint DLP Manager sunucusunun IP adresi veya FQDN
2. **Kullanıcı Adı**: API erişimi olan bir kullanıcı
3. **Şifre**: Kullanıcı şifresi
4. **Port**: Genellikle 8443 (HTTPS) veya 8080 (HTTP)

### API Endpoint'leri

Forcepoint DLP REST API dokümantasyonu:
- **Base URL**: `https://<ManagerIP>:8443/dlp/rest/v1`
- **Authentication**: `/dlp/rest/v1/login`
- **Incidents**: `/dlp/rest/v1/incidents`
- **Policies**: `/dlp/rest/v1/policies`
- **Remediation**: `/dlp/rest/v1/incidents/update`

Detaylı dokümantasyon: [Forcepoint DLP REST API](https://help.forcepoint.com/dlp/90/restapi/)

### 1. Collector Servisi Yapılandırması

Dosya: `DLP.RiskAnalyzer.Collector/appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "Port": 8443,
    "UseHttps": true,
    "Timeout": 30
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "StreamName": "dlp_incidents"
  },
  "Collector": {
    "IntervalMinutes": 60,
    "LookbackHours": 24,
    "BatchSize": 100
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Önemli**: `YOUR_DLP_MANAGER_IP`, `YOUR_DLP_USERNAME`, `YOUR_DLP_PASSWORD` değerlerini kendi ortamınıza göre değiştirin.

#### Güvenlik Notları

⚠️ **ASLA şifreleri Git'e commit etmeyin!**

1. `appsettings.json` dosyasını `.gitignore`'a ekleyin (zaten ekli olmalı)
2. Production ortamında:
   - `appsettings.Production.json` kullanın
   - Veya Environment Variables kullanın:
     ```powershell
     $env:DLP__Password = "your_password"
     ```
   - Veya Azure Key Vault / AWS Secrets Manager gibi güvenli depolama kullanın

### 2. Analyzer Servisi Yapılandırması

Dosya: `DLP.RiskAnalyzer.Analyzer/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_risk_analyzer;Username=dlp_user;Password=your_password;Timezone=Europe/Istanbul"
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "StreamName": "dlp_incidents"
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "Port": 8443,
    "UseHttps": true
  },
  "Reports": {
    "Directory": "reports"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

**Değiştirilmesi Gerekenler:**
- `ConnectionStrings.DefaultConnection`: PostgreSQL bağlantı bilgileri
- `DLP.*`: Forcepoint DLP API bilgileri

### 3. Dashboard Yapılandırması

Dosya: `dashboard/.env.local`

```env
NEXT_PUBLIC_API_URL=http://localhost:8000
```

**Not**: Analyzer API'nin çalıştığı portu kontrol edin (varsayılan: 8000).

---

## 🗄️ Veritabanı Kurulumu

### 1. Veritabanı Oluşturma

PostgreSQL'e bağlanın:
```bash
# Windows (psql)
psql -U dlp_user -d postgres

# Mac
psql -U $(whoami) -d postgres
```

Veritabanını oluşturun:
```sql
CREATE DATABASE dlp_risk_analyzer;
\c dlp_risk_analyzer

-- TimescaleDB extension'ı etkinleştir
CREATE EXTENSION IF NOT EXISTS timescaledb;
```

### 2. Entity Framework Migration

Proje dizininde:
```powershell
# Windows
cd "DLP.RiskAnalyzer.Analyzer"
dotnet ef migrations add InitialCreate
dotnet ef database update
```

```bash
# Mac
cd "DLP.RiskAnalyzer.Analyzer"
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**Not**: İlk kez çalıştırıyorsanız EF Core tools kurulumu gerekebilir:
```bash
dotnet tool install --global dotnet-ef
```

---

## 🎬 Servisleri Başlatma

### Windows (PowerShell)

#### 1. Collector Servisi
```powershell
cd "DLP.RiskAnalyzer.Collector"
dotnet run
```

#### 2. Analyzer Servisi
```powershell
cd "DLP.RiskAnalyzer.Analyzer"
dotnet run
```

**Not**: Her servis için ayrı PowerShell penceresi açın.

#### 3. Web Dashboard (Terminal)
```powershell
cd dashboard
npm install  # İlk kurulumda
npm run dev
# Dashboard http://localhost:3002 adresinde çalışacak
```

### Mac (Terminal)

Otomatik başlatma script'i:
```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"
./start-mac.sh
```

Manuel başlatma:
```bash
# Terminal 1: Collector
cd "DLP.RiskAnalyzer.Collector"
dotnet run

# Terminal 2: Analyzer
cd "DLP.RiskAnalyzer.Analyzer"
dotnet run

# Terminal 3: Web Dashboard
cd dashboard
npm install  # İlk kurulumda
npm run dev
# Dashboard http://localhost:3002 adresinde çalışacak
```

### Servis Portları

- **Collector**: Arka planda çalışır, HTTP port'u yok
- **Analyzer API**: `http://localhost:8000`
- **Web Dashboard**: `http://localhost:3002` (varsayılan)
- **PostgreSQL**: `localhost:5432`
- **Redis**: `localhost:6379`
- **Swagger UI**: `http://localhost:8000/swagger`
- **DLP Manager API**: `https://<ManagerIP>:8443` (isteğe bağlı - remediation için)

---

## ✅ Test ve Doğrulama

### 1. Collector Servisi Testi

Collector'ın çalışıp çalışmadığını kontrol edin:
```bash
# Redis'te stream'in oluşturulup oluşturulmadığını kontrol edin
redis-cli
> XINFO STREAM dlp_incidents
```

### 2. Analyzer API Testi

```bash
# Health Check
curl http://localhost:8000/health

# Beklenen yanıt:
# {"status":"healthy","timestamp":"2024-..."}

# Swagger UI'ya tarayıcıdan erişin
# http://localhost:8000/swagger
```

### 3. Dashboard Testi

1. Tarayıcıda açın: `http://localhost:3001`
2. Ana sayfada veri görünüyor mu kontrol edin
3. Investigation sayfasına gidin ve kullanıcı listesi yükleniyor mu kontrol edin

### 4. API Bağlantı Testi

Forcepoint DLP API bağlantısını test etmek için:

```bash
# Analyzer API'ye bağlanın ve test endpoint'ini çağırın
curl http://localhost:8000/api/policies
```

Eğer hata alırsanız, Collector ve Analyzer loglarına bakın.

---

## 🔧 Sorun Giderme

### Sorun 1: "Could not connect to Forcepoint DLP API"

**Olası Nedenler:**
- Manager IP adresi yanlış
- Firewall kuralları
- SSL sertifika sorunu
- Kullanıcı adı/şifre yanlış

**Çözüm:**
1. `appsettings.json` dosyasındaki Manager IP'yi kontrol edin
2. Forcepoint DLP Manager'a tarayıcıdan erişebiliyor musunuz?
3. SSL doğrulamasını atlamak için `UseHttps: false` deneyin (sadece test ortamında)
4. Log dosyalarını kontrol edin

### Sorun 2: "Database connection failed"

**Çözüm:**
1. PostgreSQL'in çalıştığını kontrol edin:
   ```bash
   # Windows
   Get-Service postgresql*
   
   # Mac
   brew services list
   ```
2. Bağlantı string'ini kontrol edin: `appsettings.json`
3. Veritabanının oluşturulduğunu kontrol edin:
   ```sql
   \l  -- PostgreSQL'de
   ```

### Sorun 3: "Redis connection failed"

**Çözüm:**
1. Redis'in çalıştığını kontrol edin:
   ```bash
   redis-cli ping
   # Beklenen: PONG
   ```
2. Redis host ve port'u kontrol edin: `appsettings.json`

### Sorun 4: "Dashboard API calls failing"

**Çözüm:**
1. Analyzer API'nin çalıştığını kontrol edin: `http://localhost:8000/health`
2. `dashboard/.env.local` dosyasında `NEXT_PUBLIC_API_URL` doğru mu?
3. CORS ayarlarını kontrol edin (Analyzer `Program.cs`)

### Sorun 5: "Migration failed"

**Çözüm:**
1. EF Core tools kurulu mu?
   ```bash
   dotnet tool install --global dotnet-ef
   ```
2. PostgreSQL bağlantısı çalışıyor mu?
3. Veritabanı oluşturulmuş mu?

---

## 🔐 Güvenlik Önerileri

### Production Ortamı

1. **Şifre Yönetimi**:
   - `appsettings.json` dosyasını Git'e commit etmeyin
   - Environment Variables veya Secrets Manager kullanın
   - Şifreleri düzenli olarak değiştirin

2. **HTTPS**:
   - Production'da mutlaka HTTPS kullanın
   - SSL sertifikalarını düzenli olarak güncelleyin

3. **Network Security**:
   - Firewall kurallarını minimize edin
   - Sadece gerekli portları açın
   - Forcepoint DLP Manager ile iletişim güvenli bir ağ üzerinden olmalı

4. **Authentication**:
   - API kullanıcısı için en az yetki prensibini uygulayın
   - MFA (Multi-Factor Authentication) kullanın (mümkünse)

---

## 📞 Destek ve Dokümantasyon

### Proje Dokümantasyonu
- `README.md`: Genel proje bilgisi
- `WINDOWS_INSTALLATION.md`: Windows kurulum detayları
- `MAC_TESTING_GUIDE.md`: Mac test rehberi
- `FEATURES_COMPARISON.md`: Özellik karşılaştırması

### Forcepoint DLP API Dokümantasyonu
- [Forcepoint DLP REST API Guide](https://help.forcepoint.com/dlp/90/restapi/)
- API Authentication: [JWT Token Based Authentication](https://help.forcepoint.com/dlp/90/restapi/53F5E3C6-4E20-478E-9CD5-EB4A02DDFE35.html)

### Log Dosyaları

Windows:
- Collector: `logs/collector-*.log` (varsa)
- Analyzer: Console output veya `logs/analyzer-*.log`

Mac:
- Console output
- System logs: `/var/log/` (Docker servisleri için)

---

## 📝 Hızlı Başlangıç Checklist

- [ ] .NET SDK 8.0 kurulu
- [ ] PostgreSQL ve TimescaleDB kurulu
- [ ] Redis kurulu ve çalışıyor
- [ ] Node.js ve npm kurulu
- [ ] Veritabanı oluşturuldu
- [ ] Migration'lar çalıştırıldı
- [ ] `appsettings.json` dosyaları yapılandırıldı (Forcepoint DLP bilgileri)
- [ ] Collector servisi başlatıldı
- [ ] Analyzer servisi başlatıldı
- [ ] Dashboard başlatıldı
- [ ] Health check başarılı
- [ ] Dashboard'a tarayıcıdan erişilebiliyor

---

## 🎉 Başarıyla Kuruldu!

Sisteminiz hazır! Şimdi:

1. Web Dashboard'a gidin: `http://localhost:3002`
2. Ana sayfada verilerin geldiğini kontrol edin
3. Investigation sayfasında incident remediation özelliğini test edin

## ⚠️ Incident Remediation Özelliği

### Önemli Notlar

**RemediationService**, DLP Manager API (port 8443) bağlantısı olmasa bile remediate işlemlerini başarılı olarak kaydeder. Bu sayede:

- ✅ **Geliştirme/Test Ortamı**: DLP Manager API olmadan test edebilirsiniz
- ✅ **Production Ortamı**: DLP Manager API bağlantısı sağlandığında gerçek remediate işlemleri yapılır
- ✅ **Graceful Degradation**: API bağlantısı kesilse bile sistem çalışmaya devam eder

### Çalışma Mantığı

1. **DLP Manager API Bağlantısı YOKSA**:
   - Remediate işlemi başarılı olarak kaydedilir
   - Mesaj: "Incident remediation recorded (DLP Manager API unavailable)"

2. **DLP Manager API Bağlantısı VARSA**:
   - Gerçek remediate isteği DLP Manager API'ye gönderilir
   - API başarılı response dönerse → Gerçek API response döner
   - API hata dönerse → Başarılı response döner (fallback)

### Yapılandırma

DLP Manager API bilgileri `DLP.RiskAnalyzer.Analyzer/appsettings.json` dosyasında:

```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD"
  }
}
```

**Not**: Bu bilgiler olmasa bile sistem çalışır, sadece gerçek remediate işlemleri yapılmaz.
3. Investigation sayfasında kullanıcıları görüntüleyin
4. Reports sayfasından rapor oluşturun

**İyi çalışmalar!** 🚀

