# Windows Kurulum Rehberi - C# Versiyonu

## 📋 İçindekiler

1. [Gereksinimler](#gereksinimler)
2. [Yazılım Kurulumları](#yazılım-kurulumları)
3. [Proje Kurulumu](#proje-kurulumu)
4. [Veritabanı Kurulumu](#veritabanı-kurulumu)
5. [Yapılandırma](#yapılandırma)
6. [Servisleri Çalıştırma](#servisleri-çalıştırma)
7. [Troubleshooting](#troubleshooting)

---

## 🔧 Gereksinimler

### Minimum Sistem Gereksinimleri
- **İşletim Sistemi**: Windows 10 (1809 veya üzeri) / Windows 11
- **RAM**: 8 GB (önerilen: 16 GB)
- **Disk**: 10 GB boş alan
- **İşlemci**: x64 architecture (Intel/AMD)

### Gerekli Yazılımlar
1. **.NET 8.0 SDK**
2. **Visual Studio 2022** (Community/Professional/Enterprise) veya **Visual Studio Code**
3. **PostgreSQL 16+** (TimescaleDB extension ile)
4. **Redis Server**
5. **Git for Windows**
6. **PowerShell 5.1+** (Windows 10/11'de varsayılan gelir)

---

## 📦 Yazılım Kurulumları

### 1. .NET 8.0 SDK Kurulumu

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

#### Seçenek A: PostgreSQL + TimescaleDB Extension (Manuel)

1. PostgreSQL 16 indirin: https://www.postgresql.org/download/windows/
2. PostgreSQL'i kurun (şifreyi not edin!)
3. TimescaleDB extension'ı kurun: https://docs.timescale.com/install/latest/self-hosted/installation-windows/
4. PostgreSQL'i başlatın

#### Seçenek B: Docker Desktop ile TimescaleDB (Kolay)

1. Docker Desktop kurun: https://www.docker.com/products/docker-desktop/
2. PowerShell'de çalıştırın:

```powershell
docker run -d `
  --name timescaledb `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=dlp_analytics `
  -p 5432:5432 `
  timescale/timescaledb:latest-pg16
```

3. Docker Desktop'ta container'ın çalıştığını doğrulayın

### 4. Redis Server Kurulumu

#### Seçenek A: Redis for Windows (Memurai - Önerilen)

1. Memurai indirin: https://www.memurai.com/get-memurai
2. Kurulumu tamamlayın
3. Windows Services'den **Memurai** servisini başlatın

#### Seçenek B: Docker ile Redis

```powershell
docker run -d `
  --name redis `
  -p 6379:6379 `
  redis:7-alpine
```

#### Seçenek C: WSL2 ile Redis (Gelişmiş)

```powershell
# WSL2'de Redis kurun
wsl sudo apt-get update
wsl sudo apt-get install redis-server
wsl sudo service redis-server start
```

### 5. Git for Windows Kurulumu

1. https://git-scm.com/download/win adresine gidin
2. Git for Windows'u indirin ve kurun
3. Kurulum sırasında varsayılan seçenekleri kullanın

---

## 📁 Proje Kurulumu

### 1. Projeyi Klonlayın veya Kopyalayın

```powershell
# Eğer Git repository'den klonluyorsanız
cd C:\Projects
git clone <repository-url>
cd "Risk Adaptive Protection CSharp"

# VEYA proje klasörünü doğrudan kopyalayın
# Örnek: C:\Projects\DLP_RiskAnalyzer
```

### 2. Solution'ı Restore Edin

```powershell
# Proje klasörüne gidin
cd "C:\Projects\Risk Adaptive Protection CSharp"

# NuGet paketlerini restore edin
dotnet restore
```

**Beklenen çıktı:**
```
  Determining projects to restore...
  Restored DLP.RiskAnalyzer.Shared\DLP.RiskAnalyzer.Shared.csproj (in XXX ms).
  Restored DLP.RiskAnalyzer.Collector\DLP.RiskAnalyzer.Collector.csproj (in XXX ms).
  Restored DLP.RiskAnalyzer.Analyzer\DLP.RiskAnalyzer.Analyzer.csproj (in XXX ms).
  Restored DLP.RiskAnalyzer.Dashboard\DLP.RiskAnalyzer.Dashboard.csproj (in XXX ms).
```

### 3. Projeyi Build Edin

```powershell
# Tüm solution'ı build edin
dotnet build

# Beklenen çıktı: "Build succeeded."
```

---

## 🗄️ Veritabanı Kurulumu

### 1. PostgreSQL Connection Test

```powershell
# PostgreSQL'in çalıştığını kontrol edin
# pgAdmin veya psql ile bağlanın

# psql ile:
psql -U postgres -h localhost -d postgres
# Şifre sorulacak (kurulum sırasında belirlediğiniz)
```

### 2. Database Oluşturma

PostgreSQL'e bağlandıktan sonra:

```sql
-- Database oluştur
CREATE DATABASE dlp_analytics;

-- TimescaleDB extension'ı etkinleştir
\c dlp_analytics
CREATE EXTENSION IF NOT EXISTS timescaledb;
```

### 3. Entity Framework Migrations

```powershell
# Analyzer projesine gidin
cd "DLP.RiskAnalyzer.Analyzer"

# Entity Framework Core Tools kurun (eğer yoksa)
dotnet tool install --global dotnet-ef

# Migration oluştur
dotnet ef migrations add InitialCreate

# Database'i oluştur ve güncelle
dotnet ef database update
```

**Not**: Eğer `dotnet ef` komutu çalışmıyorsa:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.0
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

**Dosya**: `DLP.RiskAnalyzer.Analyzer\appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analytics;Username=postgres;Password=your_postgres_password"
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

**Önemli**: 
- `postgres` → PostgreSQL şifreniz (Docker kullanıyorsanız varsayılan `postgres`, kendi kurulumunuzsa belirlediğiniz şifre)
- `YOUR_DLP_MANAGER_IP` → Forcepoint DLP Manager IP adresi (örnek: 10.0.0.100 veya dlp.company.com)
- `YOUR_DLP_USERNAME` → Forcepoint DLP API kullanıcı adı
- `YOUR_DLP_PASSWORD` → Forcepoint DLP API şifresi

**⚠️ Dikkat**: Bu değerleri kendi ortamınıza göre doldurun! `YOUR_DLP_MANAGER_IP`, `YOUR_DLP_USERNAME`, `YOUR_DLP_PASSWORD` placeholder'larını gerçek değerlerle değiştirin.

### 3. Dashboard Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Dashboard\appsettings.json`

```json
{
  "ApiBaseUrl": "http://localhost:8000"
}
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
     - ✅ `DLP.RiskAnalyzer.Dashboard` (WPF App)
   - Collector'ı daha sonra manuel olarak çalıştıracaksınız

3. **F5 ile çalıştırın**
   - Analyzer API: http://localhost:8000
   - Dashboard: WPF penceresi açılacak

### Senaryo 2: PowerShell ile Çalıştırma

#### Terminal 1: Analyzer API

```powershell
cd "C:\Projects\Risk Adaptive Protection CSharp\DLP.RiskAnalyzer.Analyzer"
dotnet run

# Beklenen çıktı:
# Now listening on: http://localhost:8000
# Swagger UI: http://localhost:8000/swagger
```

#### Terminal 2: Collector Service

```powershell
cd "C:\Projects\Risk Adaptive Protection CSharp\DLP.RiskAnalyzer.Collector"
dotnet run

# Collector arka planda çalışacak, her saat başı veri toplayacak
```

#### Terminal 3: Dashboard

```powershell
cd "C:\Projects\Risk Adaptive Protection CSharp\DLP.RiskAnalyzer.Dashboard"
dotnet run

# WPF uygulaması açılacak
```

### Senaryo 3: Windows Service olarak Collector'ı Kurma (Gelişmiş)

1. **NSSM (Non-Sucking Service Manager)** indirin:
   ```powershell
   # Chocolatey ile:
   choco install nssm -y
   
   # VEYA manuel: https://nssm.cc/download
   ```

2. **Collector'ı Windows Service olarak kurun**:
   ```powershell
   cd "C:\Projects\Risk Adaptive Protection CSharp\DLP.RiskAnalyzer.Collector"
   
   # Publish edin
   dotnet publish -c Release -o "C:\Services\DLPRiskAnalyzerCollector"
   
   # Service kurun
   nssm install DLPRiskAnalyzerCollector "C:\Program Files\dotnet\dotnet.exe" "C:\Services\DLPRiskAnalyzerCollector\DLP.RiskAnalyzer.Collector.dll"
   
   # Service'i başlatın
   nssm start DLPRiskAnalyzerCollector
   ```

---

## ✅ Doğrulama

### 1. API Health Check

Tarayıcıda veya PowerShell'de:

```powershell
# Health check
Invoke-WebRequest -Uri "http://localhost:8000/health" -Method GET

# Beklenen response:
# {"status":"healthy","timestamp":"2024-11-03T12:00:00+03:00"}
```

### 2. Swagger UI

Tarayıcıda açın: http://localhost:8000/swagger

Tüm API endpoint'lerini göreceksiniz.

### 3. Dashboard Bağlantısı

1. WPF Dashboard'ı açın
2. "Dashboard" sekmesine gidin
3. Verilerin yüklendiğini kontrol edin

---

## 🔧 Troubleshooting

### Problem 1: "dotnet: command not found"

**Çözüm**:
```powershell
# .NET SDK'nın PATH'e eklendiğini kontrol edin
$env:PATH -split ';' | Select-String "dotnet"

# Eğer yoksa, PATH'e ekleyin
# Windows → System Properties → Environment Variables → PATH
# C:\Program Files\dotnet ekleyin
```

### Problem 2: PostgreSQL Bağlantı Hatası

**Hata**: `could not translate host name "localhost" to address`

**Çözüm**:
1. PostgreSQL servisinin çalıştığını kontrol edin:
   ```powershell
   Get-Service -Name postgresql*
   ```

2. `appsettings.json`'daki connection string'i kontrol edin
3. PostgreSQL'in `pg_hba.conf` dosyasında bağlantı izinlerini kontrol edin

### Problem 3: Redis Bağlantı Hatası

**Hata**: `No connection could be made because the target machine actively refused it`

**Çözüm**:
1. Redis'in çalıştığını kontrol edin:
   ```powershell
   # Memurai için:
   Get-Service -Name Memurai*
   
   # Docker için:
   docker ps | Select-String redis
   ```

2. Port 6379'un açık olduğunu kontrol edin:
   ```powershell
   netstat -an | Select-String "6379"
   ```

### Problem 4: SSL Certificate Hatası (DLP API)

**Hata**: `The SSL connection could not be established`

**Çözüm**:
- Bu hata beklenen bir durumdur (self-signed certificate'lar için)
- Kod içinde SSL validation bypass edilmiştir
- Eğer hala hata alıyorsanız, `PolicyService.cs` ve `RemediationService.cs` dosyalarındaki `ServerCertificateCustomValidationCallback` ayarlarını kontrol edin

### Problem 5: Migration Hatası

**Hata**: `Failed executing DbCommand`

**Çözüm**:
```powershell
# Database'i sıfırlayın (DİKKAT: Tüm veriler silinir!)
cd "DLP.RiskAnalyzer.Analyzer"
dotnet ef database drop --force
dotnet ef database update
```

### Problem 6: Port 8000 Kullanımda

**Hata**: `Address already in use`

**Çözüm**:
```powershell
# Port'u kullanan process'i bulun
netstat -ano | Select-String "8000"

# Process'i sonlandırın (PID numarasını kullanın)
taskkill /PID <PID_NUMBER> /F

# VEYA appsettings.json'da farklı bir port kullanın
# "Urls": "http://localhost:8001"
```

### Problem 7: WPF Dashboard Açılmıyor

**Hata**: `System.Windows.Markup.XamlParseException`

**Çözüm**:
1. MaterialDesign NuGet paketlerinin yüklendiğini kontrol edin:
   ```powershell
   cd "DLP.RiskAnalyzer.Dashboard"
   dotnet restore
   ```

2. Windows'ta .NET Desktop Runtime'ın yüklü olduğunu kontrol edin:
   ```powershell
   dotnet --list-runtimes | Select-String "Microsoft.WindowsDesktop.App"
   ```

---

## 📊 Servis Durumu Kontrolü

### PowerShell Script ile Tüm Servisleri Kontrol Etme

`check-services.ps1` dosyası oluşturun:

```powershell
# check-services.ps1

Write-Host "=== Service Status Check ===" -ForegroundColor Green

# PostgreSQL
$pgService = Get-Service -Name postgresql* -ErrorAction SilentlyContinue
if ($pgService) {
    Write-Host "PostgreSQL: $($pgService.Status)" -ForegroundColor $(if($pgService.Status -eq 'Running'){'Green'}else{'Red'})
} else {
    Write-Host "PostgreSQL: Not found (check Docker if using container)" -ForegroundColor Yellow
}

# Redis/Memurai
$redisService = Get-Service -Name Memurai* -ErrorAction SilentlyContinue
if ($redisService) {
    Write-Host "Redis (Memurai): $($redisService.Status)" -ForegroundColor $(if($redisService.Status -eq 'Running'){'Green'}else{'Red'})
} else {
    Write-Host "Redis: Check Docker or WSL2" -ForegroundColor Yellow
}

# API Health Check
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -Method GET -TimeoutSec 2
    Write-Host "Analyzer API: Healthy" -ForegroundColor Green
} catch {
    Write-Host "Analyzer API: Not responding" -ForegroundColor Red
}

Write-Host "`n=== Port Status ===" -ForegroundColor Green
netstat -an | Select-String "8000|5432|6379" | ForEach-Object {
    Write-Host $_ -ForegroundColor Cyan
}
```

Çalıştırın:
```powershell
.\check-services.ps1
```

---

## 🎯 Hızlı Başlangıç (Quick Start)

Tüm adımları otomatikleştiren PowerShell script'i:

`quick-start.ps1`:

```powershell
# Quick Start Script
Write-Host "=== DLP Risk Analyzer Quick Start ===" -ForegroundColor Green

# 1. Restore packages
Write-Host "`n[1/5] Restoring packages..." -ForegroundColor Yellow
dotnet restore

# 2. Build solution
Write-Host "`n[2/5] Building solution..." -ForegroundColor Yellow
dotnet build

# 3. Database migration (if needed)
Write-Host "`n[3/5] Running database migrations..." -ForegroundColor Yellow
cd "DLP.RiskAnalyzer.Analyzer"
dotnet ef database update
cd ..

# 4. Check services
Write-Host "`n[4/5] Checking required services..." -ForegroundColor Yellow
# PostgreSQL ve Redis kontrolü buraya eklenebilir

# 5. Start services
Write-Host "`n[5/5] Starting services..." -ForegroundColor Yellow
Write-Host "Starting Analyzer API..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\DLP.RiskAnalyzer.Analyzer'; dotnet run"

Start-Sleep -Seconds 5

Write-Host "Starting Collector..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\DLP.RiskAnalyzer.Collector'; dotnet run"

Start-Sleep -Seconds 3

Write-Host "Starting Dashboard..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\DLP.RiskAnalyzer.Dashboard'; dotnet run"

Write-Host "`n=== All services started! ===" -ForegroundColor Green
Write-Host "API: http://localhost:8000" -ForegroundColor Cyan
Write-Host "Swagger: http://localhost:8000/swagger" -ForegroundColor Cyan
```

Çalıştırın:
```powershell
.\quick-start.ps1
```

---

## 📝 Sonraki Adımlar

1. **Veri Toplama**: Collector servisi Forcepoint DLP API'den veri toplayacak
2. **Analiz**: Analyzer API risk skorlarını hesaplayacak
3. **Görselleştirme**: Dashboard'da verileri görüntüleyebilirsiniz

---

## 🔒 Güvenlik Notları

1. **Production ortamında**:
   - `appsettings.json` dosyalarını `.gitignore`'a ekleyin
   - Şifreleri environment variables veya Azure Key Vault'ta saklayın
   - HTTPS kullanın
   - SSL certificate validation'ı production'da etkinleştirin

2. **Windows Firewall**:
   - Gerekli portları açın (8000, 5432, 6379)
   - Sadece gerekli IP'lerden erişime izin verin

---

## 📞 Destek

Sorun yaşarsanız:
1. `FEATURES_COMPARISON.md` dosyasını kontrol edin
2. Swagger UI'dan API endpoint'lerini test edin
3. Log dosyalarını inceleyin
4. PostgreSQL ve Redis bağlantılarını doğrulayın

---

**Kurulum tamamlandı! 🎉**

