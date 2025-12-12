# Windows Kurulum Rehberi - Sıfırdan Kurulum

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Sistem Gereksinimleri](#sistem-gereksinimleri)
3. [Yazılım Kurulumları](#yazılım-kurulumları)
4. [Proje Kurulumu](#proje-kurulumu)
5. [Veritabanı Kurulumu](#veritabanı-kurulumu)
6. [Yapılandırma](#yapılandırma)
7. [Servisleri Çalıştırma](#servisleri-çalıştırma)
8. [Dashboard Kurulumu](#dashboard-kurulumu)
9. [Test ve Doğrulama](#test-ve-doğrulama)
10. [Sorun Giderme](#sorun-giderme)

---

## 🎯 Genel Bakış

Bu rehber, **Forcepoint DLP Risk Adaptive Protection** sisteminin Windows ortamında sıfırdan kurulumunu adım adım anlatır.

### Sistem Mimarisi

```
┌─────────────────────┐
│  Forcepoint DLP     │
│  Manager (API)      │
│  Port: 8443 (HTTPS) │
└──────────┬──────────┘
           │
           │ REST API v1
           │ (JWT Authentication)
           │
┌──────────▼──────────┐      ┌──────────────┐      ┌─────────────┐
│   Collector Service │─────▶│    Redis     │─────▶│  Analyzer   │
│   (.NET 8.0)        │      │   (Stream)   │      │  (ASP.NET)  │
│   Background        │      │   Port: 6379  │      │  Port: 8000 │
└─────────────────────┘      └──────────────┘      └──────┬───────┘
                                                           │
                                                  ┌────────▼────────┐
                                                  │  PostgreSQL     │
                                                  │  (TimescaleDB)  │
                                                  │  Port: 5432     │
                                                  └────────┬─────────┘
                                                           │
                                                  ┌────────▼────────┐
                                                  │  Web Dashboard  │
                                                  │   (Next.js)     │
                                                  │  Port: 3002     │
                                                  └─────────────────┘
```

---

## 💻 Sistem Gereksinimleri

### Minimum Sistem Gereksinimleri

- **İşletim Sistemi**: Windows 10 (1809 veya üzeri) / Windows 11 / Windows Server 2016+
- **RAM**: 8 GB (önerilen: 16 GB)
- **Disk**: 20 GB boş alan
- **İşlemci**: x64 architecture (Intel/AMD), 4 çekirdek (önerilen: 8 çekirdek)
- **Ağ**: Forcepoint DLP Manager'a erişim (Port 8443)

### Gerekli Yazılımlar

1. **.NET 8.0 SDK** - [İndirme](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Visual Studio 2022** (Community/Professional/Enterprise) veya **Visual Studio Code**
3. **PostgreSQL 16+** (TimescaleDB extension ile) veya **Docker Desktop**
4. **Redis Server** (Memurai veya Docker)
5. **Git for Windows** - [İndirme](https://git-scm.com/download/win)
6. **Node.js 18+** ve npm (Dashboard için) - [İndirme](https://nodejs.org/)
7. **PowerShell 5.1+** (Windows 10/11'de varsayılan gelir)

---

## 📦 Yazılım Kurulumları

### 1. .NET 8.0 SDK Kurulumu

#### Yöntem A: Winget ile (Önerilen)

```powershell
# PowerShell'i Yönetici olarak açın
winget install Microsoft.DotNet.SDK.8
```

#### Yöntem B: Manuel Kurulum

1. Tarayıcınızda https://dotnet.microsoft.com/download/dotnet/8.0 adresine gidin
2. **.NET 8.0 SDK** (x64) indirin
3. İndirilen `.exe` dosyasını çalıştırın ve kurulum sihirbazını takip edin
4. Kurulumu doğrulayın:

```powershell
dotnet --version
# Beklenen çıktı: 8.0.xxx
```

### 2. Visual Studio 2022 Kurulumu (Önerilen)

1. https://visualstudio.microsoft.com/downloads/ adresine gidin
2. **Visual Studio 2022 Community** (ücretsiz) veya Professional/Enterprise indirin
3. Kurulum sırasında şu iş yüklerini seçin:
   - ✅ **.NET desktop development** (WPF için)
   - ✅ **ASP.NET and web development** (API için)
   - ✅ **.NET Multi-platform App UI development** (opsiyonel)

**Alternatif: Visual Studio Code**

```powershell
# VS Code indirin
# https://code.visualstudio.com/download

# VS Code için gerekli extension'lar:
# - C# (Microsoft)
# - .NET Extension Pack
# - C# Dev Kit (opsiyonel)
```

### 3. PostgreSQL + TimescaleDB Kurulumu

#### Seçenek A: Docker Desktop ile (Önerilen - Kolay)

1. **Docker Desktop** kurun: https://www.docker.com/products/docker-desktop/
2. Docker Desktop'ı başlatın ve çalıştığını doğrulayın
3. PowerShell'de çalıştırın:

```powershell
# PostgreSQL + TimescaleDB container'ı başlat
docker run -d `
  --name timescaledb `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=dlp_analyzer `
  -p 5432:5432 `
  timescale/timescaledb:latest-pg16

# Container'ın çalıştığını kontrol et
docker ps
```

4. Bağlantıyı test edin:

```powershell
# psql ile test (Docker container içinde)
docker exec -it timescaledb psql -U postgres -d dlp_analyzer
# \q ile çıkış yapın
```

#### Seçenek B: PostgreSQL + TimescaleDB Extension (Manuel)

1. PostgreSQL 16 indirin: https://www.postgresql.org/download/windows/
2. PostgreSQL'i kurun (şifreyi not edin!)
3. TimescaleDB extension'ı kurun: https://docs.timescale.com/install/latest/self-hosted/installation-windows/
4. PostgreSQL servisini başlatın

### 4. Redis Server Kurulumu

#### Seçenek A: Docker Desktop ile (Önerilen)

```powershell
# Redis container'ı başlat
docker run -d `
  --name redis `
  -p 6379:6379 `
  redis:7-alpine

# Container'ın çalıştığını kontrol et
docker ps
```

#### Seçenek B: Memurai (Windows Native)

1. Memurai indirin: https://www.memurai.com/get-memurai
2. Kurulumu tamamlayın
3. Windows Services'den **Memurai** servisini başlatın

### 5. Node.js Kurulumu (Dashboard için)

1. Node.js 18+ indirin: https://nodejs.org/
2. Kurulum sihirbazını çalıştırın
3. Kurulumu doğrulayın:

```powershell
node --version
npm --version
```

### 6. Git for Windows Kurulumu

1. Git for Windows indirin: https://git-scm.com/download/win
2. Kurulum sihirbazını takip edin
3. Kurulumu doğrulayın:

```powershell
git --version
```

---

## 🚀 Proje Kurulumu

### 1. Projeyi İndirin

```powershell
# İstediğiniz dizine gidin
cd C:\Projects

# GitHub'dan projeyi klonlayın
git clone https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git

# Proje dizinine gidin
cd dlp-risk-adaptive-protection-csharp
```

### 2. Projeyi Build Edin

```powershell
# Solution'ı build et
dotnet build DLP.RiskAnalyzer.Solution.sln

# Başarılı olursa şu çıktıyı görmelisiniz:
# Build succeeded.
```

### 3. NuGet Paketlerini Restore Edin

```powershell
# NuGet paketlerini restore et
dotnet restore DLP.RiskAnalyzer.Solution.sln
```

---

## 🗄️ Veritabanı Kurulumu

### 1. Veritabanını Oluşturun

```powershell
# PostgreSQL'e bağlanın (Docker kullanıyorsanız)
docker exec -it timescaledb psql -U postgres

# Veritabanını oluşturun
CREATE DATABASE dlp_analyzer;

# Çıkış yapın
\q
```

### 2. Entity Framework Migrations'ı Çalıştırın

```powershell
# Analyzer projesine gidin
cd DLP.RiskAnalyzer.Analyzer

# Migrations'ı uygula
dotnet ef database update

# Başarılı olursa tablolar oluşturulur
```

### 3. System Settings Tablosunu Oluşturun

```powershell
# SQL script'ini çalıştırın (Docker kullanıyorsanız)
docker exec -i timescaledb psql -U postgres -d dlp_analyzer < ..\create_system_settings_table.sql
```

---

## ⚙️ Yapılandırma

### 1. Collector Service Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Collector\appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "UseHttps": true,
    "Timeout": 30
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "StreamName": "dlp:incidents"
  },
  "Collector": {
    "IntervalMinutes": 60,
    "LookbackHours": 24,
    "BatchSize": 100
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**⚠️ Önemli**: `YOUR_DLP_MANAGER_IP`, `YOUR_DLP_USERNAME`, `YOUR_DLP_PASSWORD` değerlerini gerçek değerlerle değiştirin!

### 2. Analyzer API Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Analyzer\appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analyzer;Username=postgres;Password=postgres"
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "UseHttps": true,
    "Timeout": 30
  },
  "Reports": {
    "Directory": "reports"
  },
  "Authentication": {
    "Username": "admin",
    "Password": "admin123"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "",
    "SmtpPassword": "",
    "SmtpEnableSsl": true,
    "FromEmail": "",
    "FromName": "DLP Risk Analyzer"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

**⚠️ Önemli**: 
- `YOUR_DLP_MANAGER_IP`, `YOUR_DLP_USERNAME`, `YOUR_DLP_PASSWORD` değerlerini gerçek değerlerle değiştirin!
- PostgreSQL şifresini (`postgres`) kendi şifrenizle değiştirin!

### 3. Dashboard Yapılandırması

**Dosya**: `dashboard\.env.local` (oluşturun)

```env
NEXT_PUBLIC_API_URL=http://localhost:8000
```

---

## 🚀 Servisleri Çalıştırma

### Senaryo 1: Visual Studio ile Çalıştırma

1. **Solution'ı açın**:
   - Visual Studio 2022'yi açın
   - `DLP.RiskAnalyzer.Solution.sln` dosyasını açın

2. **Multiple Startup Projects ayarlayın**:
   - Solution'a sağ tıklayın → Properties
   - Multiple startup projects seçin
   - Şu projeleri "Start" olarak ayarlayın:
     - ✅ `DLP.RiskAnalyzer.Analyzer` (Web API)
     - ✅ `DLP.RiskAnalyzer.Dashboard` (WPF App - opsiyonel)
   - Collector'ı daha sonra manuel olarak çalıştıracaksınız

3. **F5 ile çalıştırın**
   - Analyzer API: http://localhost:8000
   - Dashboard: WPF penceresi açılacak

### Senaryo 2: PowerShell ile Çalıştırma

#### Terminal 1: Analyzer API

```powershell
cd DLP.RiskAnalyzer.Analyzer
dotnet run
```

#### Terminal 2: Collector Service

```powershell
cd DLP.RiskAnalyzer.Collector
dotnet run
```

#### Terminal 3: Dashboard (Next.js)

```powershell
cd dashboard
npm install
npm run dev
```

Dashboard: http://localhost:3002

---

## 🌐 Dashboard Kurulumu

### 1. Bağımlılıkları Yükleyin

```powershell
cd dashboard
npm install
```

### 2. Dashboard'u Başlatın

```powershell
npm run dev
```

Dashboard: http://localhost:3002

**Varsayılan Giriş Bilgileri:**
- Kullanıcı adı: `admin`
- Şifre: `admin123`

---

## ✅ Test ve Doğrulama

### 1. Analyzer API Testi

```powershell
# Health check
curl http://localhost:8000/health

# Swagger UI
# Tarayıcıda açın: http://localhost:8000/swagger
```

### 2. Collector Service Testi

Collector Service çalıştığında loglarda şunları görmelisiniz:

```
[Information] DLP Collector Service started - Forcepoint DLP REST API v1 integration
[Information] Starting incident collection from Forcepoint DLP REST API v1...
[Information] Requesting access token from https://YOUR_DLP_MANAGER_IP:8443/dlp/rest/v1/auth/access-token
[Information] Access token obtained successfully, expires at ...
[Information] Fetching incidents from ...
[Information] Fetched X incidents from Forcepoint DLP API
[Information] Successfully collected and pushed X incidents to Redis
```

### 3. Database Testi

```powershell
# PostgreSQL'e bağlanın
docker exec -it timescaledb psql -U postgres -d dlp_analyzer

# Tabloları kontrol edin
\dt

# Incident sayısını kontrol edin
SELECT COUNT(*) FROM "Incidents";

# Çıkış yapın
\q
```

### 4. Redis Testi

```powershell
# Redis'e bağlanın (Docker kullanıyorsanız)
docker exec -it redis redis-cli

# Stream'i kontrol edin
XINFO STREAM dlp:incidents

# Çıkış yapın
exit
```

---

## 🔧 Sorun Giderme

### Problem: .NET SDK bulunamıyor

**Çözüm:**
```powershell
# .NET SDK'nın kurulu olduğunu kontrol edin
dotnet --version

# Kurulu değilse, yeniden kurun
winget install Microsoft.DotNet.SDK.8
```

### Problem: PostgreSQL bağlantı hatası

**Çözüm:**
```powershell
# PostgreSQL'in çalıştığını kontrol edin
docker ps | findstr timescaledb

# Çalışmıyorsa başlatın
docker start timescaledb

# Bağlantıyı test edin
docker exec -it timescaledb psql -U postgres -d dlp_analyzer
```

### Problem: Redis bağlantı hatası

**Çözüm:**
```powershell
# Redis'in çalıştığını kontrol edin
docker ps | findstr redis

# Çalışmıyorsa başlatın
docker start redis

# Bağlantıyı test edin
docker exec -it redis redis-cli ping
# Beklenen: PONG
```

### Problem: DLP API bağlantı hatası

**Çözüm:**
1. `appsettings.json` dosyalarındaki DLP Manager IP, Username, Password değerlerini kontrol edin
2. Forcepoint DLP Manager'a erişilebilir olduğunu test edin:
   ```powershell
   Test-NetConnection -ComputerName YOUR_DLP_MANAGER_IP -Port 8443
   ```
3. API kullanıcı bilgilerinin doğru olduğunu kontrol edin
4. Firewall kurallarını kontrol edin

### Problem: Port zaten kullanımda

**Çözüm:**
```powershell
# Port 8000'i kullanan process'i bulun
netstat -ano | findstr :8000

# Process'i sonlandırın (PID'yi yukarıdaki komuttan alın)
taskkill /PID <PID> /F
```

### Problem: Dashboard açılmıyor

**Çözüm:**
1. Node.js'in kurulu olduğunu kontrol edin: `node --version`
2. Bağımlılıkları yeniden yükleyin: `npm install`
3. Port 3002'nin kullanılabilir olduğunu kontrol edin
4. Analyzer API'nin çalıştığını kontrol edin: http://localhost:8000/health

---

## 📚 Ek Kaynaklar

- [Forcepoint DLP REST API v1 Dokümantasyonu](https://help.forcepoint.com/dlp/90/restapi/)
- [.NET 8.0 Dokümantasyonu](https://learn.microsoft.com/dotnet/)
- [PostgreSQL Dokümantasyonu](https://www.postgresql.org/docs/)
- [Redis Dokümantasyonu](https://redis.io/docs/)
- [Next.js Dokümantasyonu](https://nextjs.org/docs)

---

## 🎉 Kurulum Tamamlandı!

Kurulum başarıyla tamamlandı! Artık sisteminizi kullanmaya başlayabilirsiniz:

1. **Dashboard**: http://localhost:3002
2. **API Swagger UI**: http://localhost:8000/swagger
3. **API Health Check**: http://localhost:8000/health

**Sonraki Adımlar:**
- [API Bağlantı Rehberi](WINDOWS_API_BAGLANTI_REHBERI.md) dosyasını okuyun
- Forcepoint DLP Manager bilgilerini `appsettings.json` dosyalarına ekleyin
- Collector Service'i başlatın ve gerçek DLP verilerini çekmeye başlayın

---

**Sorularınız için**: GitHub Issues veya dokümantasyonu kontrol edin.

