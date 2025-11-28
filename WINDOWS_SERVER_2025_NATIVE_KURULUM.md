# Windows Server 2025 - Native Kurulum Rehberi (Docker Olmadan)
## DLP Risk Analyzer - Production Deployment

---

## 📋 İçindekiler

1. [Sistem Gereksinimleri](#sistem-gereksinimleri)
2. [Önkoşullar ve Yazılım Kurulumları](#önkoşullar-ve-yazılım-kurulumları)
3. [PostgreSQL + TimescaleDB Kurulumu](#postgresql--timescaledb-kurulumu)
4. [Redis Kurulumu](#redis-kurulumu)
5. [Proje Kurulumu](#proje-kurulumu)
6. [Veritabanı Yapılandırması](#veritabanı-yapılandırması)
7. [Yapılandırma Dosyaları](#yapılandırma-dosyaları)
8. [Network IP Erişimi Yapılandırması](#network-ip-erişimi-yapılandırması)
9. [Windows Service Kurulumu](#windows-service-kurulumu)
10. [Firewall Yapılandırması](#firewall-yapılandırması)
11. [Monitoring ve Logging](#monitoring-ve-logging)
12. [Backup Stratejileri](#backup-stratejileri)
13. [Troubleshooting](#troubleshooting)
14. [Kurulum Doğrulama Checklist](#kurulum-doğrulama-checklist)

---

## 🖥️ Sistem Gereksinimleri

### Minimum Gereksinimler
- **İşletim Sistemi**: Windows Server 2025 (Standard veya Datacenter)
- **RAM**: 16 GB (önerilen: 32 GB)
- **Disk**: 100 GB boş alan (SSD önerilir)
- **CPU**: 4 çekirdek (önerilen: 8+ çekirdek)
- **Network**: Gigabit Ethernet bağlantısı

### Önerilen Production Gereksinimleri
- **RAM**: 32 GB veya daha fazla
- **Disk**: 500 GB+ SSD (RAID 1 veya RAID 10 önerilir)
- **CPU**: 8+ çekirdek (Intel Xeon veya AMD EPYC)
- **Network**: 10 Gbps bağlantı (büyük veri akışı için)
- **Backup**: Otomatik yedekleme çözümü

### Network Port Gereksinimleri
- **5001**: Analyzer API (HTTP) - **0.0.0.0** üzerinde dinler (network IP erişimi için)
- **3002**: Web Dashboard (Next.js) - **0.0.0.0** üzerinde dinler (network IP erişimi için)
- **5432**: PostgreSQL
- **6379**: Redis
- **8443**: Forcepoint DLP Manager API (HTTPS - giden bağlantı)

---

## 📦 Önkoşullar ve Yazılım Kurulumları

### 1. Windows Server 2025 Güncellemeleri

```powershell
# PowerShell'i Administrator olarak açın
# Windows Update'i kontrol edin ve güncelleyin
Install-Module -Name PSWindowsUpdate -Force
Get-WindowsUpdate
Install-WindowsUpdate -AcceptAll -AutoReboot
```

### 2. .NET 8.0 SDK ve Runtime Kurulumu

#### Yöntem A: Web Installer (Önerilen)

1. Tarayıcıda https://dotnet.microsoft.com/download/dotnet/8.0 adresine gidin
2. **.NET 8.0 SDK** (x64) indirin
3. İndirilen `.exe` dosyasını **Administrator olarak çalıştırın**
4. Kurulum sihirbazını takip edin

#### Yöntem B: PowerShell ile Kurulum

```powershell
# Administrator PowerShell'de çalıştırın
# .NET 8.0 SDK indirme ve kurulum
$url = "https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1"
Invoke-WebRequest -Uri $url -OutFile "$env:TEMP\dotnet-install.ps1"
& "$env:TEMP\dotnet-install.ps1" -Channel 8.0 -InstallDir "C:\Program Files\dotnet"

# PATH'e ekleyin (genellikle otomatik eklenir)
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Program Files\dotnet", "Machine")

# Kurulumu doğrulayın
dotnet --version
# Beklenen çıktı: 8.0.xxx
```

### 3. Node.js ve npm Kurulumu (Dashboard için)

#### Yöntem A: Web Installer

1. https://nodejs.org/ adresine gidin
2. **LTS** versiyonunu indirin (v20.x veya üzeri)
3. Kurulum sihirbazını takip edin

#### Yöntem B: Chocolatey ile

```powershell
# Chocolatey kurulumu (eğer yoksa)
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

# Node.js kurulumu
choco install nodejs-lts -y

# Kurulumu doğrulayın
node --version
npm --version
```

### 4. Git Kurulumu

```powershell
# Chocolatey ile
choco install git -y

# VEYA manuel: https://git-scm.com/download/win
```

---

## 🗄️ PostgreSQL + TimescaleDB Kurulumu

### 1. PostgreSQL 18 Kurulumu

#### Yöntem A: EnterpriseDB PostgreSQL Installer (Önerilen)

1. PostgreSQL 18 indirin: https://www.postgresql.org/download/windows/
2. **EnterpriseDB PostgreSQL Installer**'ı indirin
3. Kurulum sırasında:
   - **Installation Directory**: `C:\Program Files\PostgreSQL\18`
   - **Data Directory**: `C:\Program Files\PostgreSQL\18\data`
   - **Port**: `5432` (varsayılan)
   - **Superuser Password**: Güçlü bir şifre belirleyin ve kaydedin (örn: `YourStrongPassword123!`)
   - **Locale**: `Turkish, Turkey` veya `English, United States`
4. Kurulum tamamlandıktan sonra PostgreSQL servisinin çalıştığını kontrol edin:

```powershell
Get-Service -Name postgresql*
# Beklenen: postgresql-x64-18 (Running)

# Eğer çalışmıyorsa başlatın
Start-Service postgresql-x64-18
```

#### Yöntem B: Chocolatey ile Kurulum

```powershell
# PostgreSQL 18 kurulumu
choco install postgresql18 --params '/Password:YourStrongPassword123!' -y

# Servisi başlat
Start-Service postgresql-x64-18
```

### 2. TimescaleDB Extension Kurulumu

#### Adım 1: TimescaleDB Installer'ını İndirin

1. TimescaleDB Windows installer'ını indirin: https://docs.timescale.com/self-hosted/latest/install/installation-windows/
2. PostgreSQL sürümünüze uygun installer'ı seçin (PostgreSQL 18 için)

#### Adım 2: TimescaleDB Kurulumu

1. İndirdiğiniz `.msi` dosyasını **Administrator olarak çalıştırın**
2. Kurulum sihirbazında:
   - PostgreSQL sürümünüzü seçin (18)
   - PostgreSQL kurulum dizinini belirtin: `C:\Program Files\PostgreSQL\18`
   - Kurulumu tamamlayın

#### Adım 3: PostgreSQL Yapılandırması

```powershell
# postgresql.conf dosyasını düzenleyin
notepad "C:\Program Files\PostgreSQL\18\data\postgresql.conf"

# Şu satırı bulun veya ekleyin:
# shared_preload_libraries = 'timescaledb'
```

**Not**: Eğer `shared_preload_libraries` satırı yoksa, dosyanın sonuna ekleyin:
```
shared_preload_libraries = 'timescaledb'
```

#### Adım 4: PostgreSQL Servisini Yeniden Başlatın

```powershell
# PostgreSQL servisini yeniden başlat
Restart-Service postgresql-x64-18

# VEYA
net stop postgresql-x64-18
net start postgresql-x64-18
```

#### Adım 5: Database ve Extension Oluşturma

```powershell
# PostgreSQL'e bağlanın (şifrenizi girin)
$env:PGPASSWORD = "YourStrongPassword123!"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost
```

PostgreSQL komut satırında:

```sql
-- Database oluştur
CREATE DATABASE dlp_analyzer;

-- Database'e bağlan
\c dlp_analyzer

-- TimescaleDB extension'ını yükle
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Extension'ın başarıyla yüklendiğini kontrol et
SELECT * FROM pg_extension WHERE extname = 'timescaledb';

-- TimescaleDB versiyonunu kontrol et
SELECT extversion FROM pg_extension WHERE extname = 'timescaledb';

-- Çıkış
\q
```

#### Sorun Giderme: TimescaleDB Extension Yüklenmiyorsa

```powershell
# Extension dosyalarının varlığını kontrol edin
Test-Path "C:\Program Files\PostgreSQL\18\lib\timescaledb.dll"
Test-Path "C:\Program Files\PostgreSQL\18\share\extension\timescaledb.control"

# Eğer dosyalar yoksa, TimescaleDB installer'ını tekrar çalıştırın
# Veya manuel olarak kopyalayın (TimescaleDB dokümantasyonuna bakın)
```

---

## 🔴 Redis Kurulumu

### Yöntem A: Memurai (Windows Native - Önerilen)

Memurai, Windows için Redis uyumlu bir çözümdür ve production ortamlarında önerilir.

#### Adım 1: Memurai İndirme ve Kurulum

1. Memurai indirin: https://www.memurai.com/get-memurai
2. **Memurai Developer Edition** (ücretsiz) veya **Enterprise Edition** kurun
3. Kurulum sırasında:
   - **Port**: `6379` (varsayılan)
   - **Service Name**: `Memurai` (varsayılan)
   - **Start Service**: Evet

#### Adım 2: Memurai Servisini Başlatma

```powershell
# Memurai servisini başlat
Start-Service Memurai

# Servis durumunu kontrol et
Get-Service Memurai
# Beklenen: Running
```

#### Adım 3: Redis Bağlantı Testi

```powershell
# Memurai CLI ile test (eğer PATH'te varsa)
memurai-cli ping
# Beklenen: PONG

# VEYA Redis CLI kullanarak (eğer ayrı kurulduysa)
redis-cli ping
# Beklenen: PONG
```

### Yöntem B: Redis for Windows (Alternatif)

Eğer Memurai kullanmak istemiyorsanız:

1. Redis for Windows indirin: https://github.com/microsoftarchive/redis/releases
2. Kurulumu tamamlayın
3. Redis servisini başlatın:

```powershell
# Redis servisini başlat
Start-Service redis

# Servis durumunu kontrol et
Get-Service redis
```

### Yöntem C: WSL2 ile Redis (Gelişmiş)

Eğer WSL2 kuruluysa:

```powershell
# WSL2'de Redis kurun
wsl sudo apt-get update
wsl sudo apt-get install redis-server -y
wsl sudo service redis-server start

# Redis'in çalıştığını kontrol et
wsl redis-cli ping
# Beklenen: PONG
```

---

## 📁 Proje Kurulumu

### 1. Projeyi İndirme veya Klonlama

```powershell
# Git repository'den klonlama
cd C:\Projects
git clone https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git
cd "dlp-risk-adaptive-protection-csharp"

# VEYA proje klasörünü doğrudan kopyalayın
# Örnek: C:\Projects\DLP_RiskAnalyzer
```

### 2. NuGet Paketlerini Restore Etme

```powershell
# Solution'ı restore edin
dotnet restore DLP.RiskAnalyzer.Solution.sln

# VEYA her projeyi ayrı ayrı restore edin
cd "DLP.RiskAnalyzer.Analyzer"
dotnet restore

cd "..\DLP.RiskAnalyzer.Collector"
dotnet restore

cd "..\dashboard"
npm install
```

### 3. Entity Framework Tools Kurulumu

```powershell
# EF Core tools'u global olarak kurun (migration'lar için)
dotnet tool install --global dotnet-ef

# Kurulumu doğrulayın
dotnet ef --version
```

---

## 🗄️ Veritabanı Yapılandırması

### 1. Migration'ları Çalıştırma

```powershell
# Analyzer projesine gidin
cd "C:\Projects\dlp-risk-adaptive-protection-csharp\DLP.RiskAnalyzer.Analyzer"

# Migration'ları çalıştırın
dotnet ef database update

# Migration durumunu kontrol edin
dotnet ef migrations list
```

**Beklenen çıktı:**
```
Applying migration '20241109184015_AddSystemSettingsTable'.
Applying migration '20241117155157_AddAIBehavioralAnalysis'.
Applying migration '20241117182303_AddAuditLogs'.
Done.
```

### 2. Veritabanı Bağlantı Testi

```powershell
# PostgreSQL'e bağlanın
$env:PGPASSWORD = "YourStrongPassword123!"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d dlp_analyzer -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';"
```

### 3. System Settings Tablosunu Oluşturma (Eğer Gerekirse)

```powershell
# create_system_settings_table.sql dosyasını çalıştırın
$env:PGPASSWORD = "YourStrongPassword123!"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d dlp_analyzer -f "create_system_settings_table.sql"
```

---

## ⚙️ Yapılandırma Dosyaları

### 1. Analyzer API Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Analyzer/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analyzer;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
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
  "InternalApi": {
    "SharedSecret": "ChangeThisSecret"
  },
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!ChangeThisInProduction!",
    "Issuer": "DLP-RiskAnalyzer",
    "Audience": "DLP-RiskAnalyzer-Client",
    "ExpirationHours": 8
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:3001",
      "http://localhost:3002"
    ],
    "AllowInternalNetwork": true
  },
  "Splunk": {
    "Enabled": false,
    "HecUrl": "https://your-splunk-instance:8088/services/collector/event",
    "HecToken": "your-hec-token-here",
    "Index": "dlp_risk_analyzer",
    "Source": "dlp-risk-analyzer",
    "Sourcetype": "dlp:audit"
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

**⚠️ Önemli Değişiklikler:**
- `YOUR_POSTGRES_PASSWORD`: PostgreSQL şifrenizi yazın
- `YOUR_DLP_MANAGER_IP`: Forcepoint DLP Manager IP adresini yazın
- `YOUR_DLP_USERNAME`: Forcepoint DLP API kullanıcı adını yazın
- `YOUR_DLP_PASSWORD`: Forcepoint DLP API şifresini yazın

### 2. Collector Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Collector/appsettings.json`

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
    "Port": 6379
  }
}
```

### 3. Dashboard Yapılandırması

**Dosya**: `dashboard/.env.local` (oluşturun)

```env
NEXT_PUBLIC_API_URL=http://localhost:5001
```

**Not**: Dashboard otomatik olarak `window.location.hostname` kullanarak API URL'ini belirler, ancak development için bu dosyayı oluşturabilirsiniz.

---

## 🌐 Network IP Erişimi Yapılandırması

### 1. Analyzer API - 0.0.0.0 Binding

**Dosya**: `DLP.RiskAnalyzer.Analyzer/Properties/launchSettings.json`

```json
{
  "profiles": {
    "localhost": {
      "commandName": "Project",
      "applicationUrl": "http://0.0.0.0:5001",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Program.cs** zaten `0.0.0.0:5001` üzerinde dinleyecek şekilde yapılandırılmış olmalı.

### 2. Dashboard - 0.0.0.0 Binding

**Dosya**: `dashboard/package.json`

```json
{
  "scripts": {
    "start": "next start -H 0.0.0.0 -p 3002"
  }
}
```

---

## 🔧 Windows Service Kurulumu

### 1. NSSM (Non-Sucking Service Manager) Kurulumu

```powershell
# NSSM indirin: https://nssm.cc/download
# VEYA Chocolatey ile
choco install nssm -y
```

### 2. Analyzer API Service Kurulumu

```powershell
# Projeyi publish edin
cd "C:\Projects\dlp-risk-adaptive-protection-csharp\DLP.RiskAnalyzer.Analyzer"
dotnet publish -c Release -o "C:\Services\DLPRiskAnalyzerAPI"

# NSSM ile service oluşturun
nssm install DLPRiskAnalyzerAPI "C:\Program Files\dotnet\dotnet.exe" "C:\Services\DLPRiskAnalyzerAPI\DLP.RiskAnalyzer.Analyzer.dll"

# Service ayarları
nssm set DLPRiskAnalyzerAPI AppDirectory "C:\Services\DLPRiskAnalyzerAPI"
nssm set DLPRiskAnalyzerAPI DisplayName "DLP Risk Analyzer API"
nssm set DLPRiskAnalyzerAPI Description "DLP Risk Analyzer REST API Service"
nssm set DLPRiskAnalyzerAPI Start SERVICE_AUTO_START
nssm set DLPRiskAnalyzerAPI AppStdout "C:\Services\DLPRiskAnalyzerAPI\logs\stdout.log"
nssm set DLPRiskAnalyzerAPI AppStderr "C:\Services\DLPRiskAnalyzerAPI\logs\stderr.log"

# Environment variables
nssm set DLPRiskAnalyzerAPI AppEnvironmentExtra "ASPNETCORE_URLS=http://0.0.0.0:5001"

# Log klasörü oluştur
New-Item -ItemType Directory -Path "C:\Services\DLPRiskAnalyzerAPI\logs" -Force

# Service'i başlat
nssm start DLPRiskAnalyzerAPI

# Service durumunu kontrol et
Get-Service DLPRiskAnalyzerAPI
```

### 3. Collector Service Kurulumu

```powershell
# Projeyi publish edin
cd "C:\Projects\dlp-risk-adaptive-protection-csharp\DLP.RiskAnalyzer.Collector"
dotnet publish -c Release -o "C:\Services\DLPRiskAnalyzerCollector"

# NSSM ile service oluşturun
nssm install DLPRiskAnalyzerCollector "C:\Program Files\dotnet\dotnet.exe" "C:\Services\DLPRiskAnalyzerCollector\DLP.RiskAnalyzer.Collector.dll"

# Service ayarları
nssm set DLPRiskAnalyzerCollector AppDirectory "C:\Services\DLPRiskAnalyzerCollector"
nssm set DLPRiskAnalyzerCollector DisplayName "DLP Risk Analyzer Collector"
nssm set DLPRiskAnalyzerCollector Description "Collects DLP incidents from Forcepoint DLP Manager and pushes to Redis"
nssm set DLPRiskAnalyzerCollector Start SERVICE_AUTO_START
nssm set DLPRiskAnalyzerCollector AppStdout "C:\Services\DLPRiskAnalyzerCollector\logs\stdout.log"
nssm set DLPRiskAnalyzerCollector AppStderr "C:\Services\DLPRiskAnalyzerCollector\logs\stderr.log"

# Log klasörü oluştur
New-Item -ItemType Directory -Path "C:\Services\DLPRiskAnalyzerCollector\logs" -Force

# Service'i başlat
nssm start DLPRiskAnalyzerCollector

# Service durumunu kontrol et
Get-Service DLPRiskAnalyzerCollector
```

### 4. Dashboard Service Kurulumu (PM2 ile)

```powershell
# PM2 global olarak kurun
npm install -g pm2

# Dashboard'u build edin
cd "C:\Projects\dlp-risk-adaptive-protection-csharp\dashboard"
npm run build

# PM2 ile service oluşturun
pm2 start npm --name "dlp-dashboard" -- start

# PM2'yi Windows Service olarak kaydedin
pm2 startup
pm2 save
```

---

## 🔥 Firewall Yapılandırması

```powershell
# PowerShell'i Administrator olarak çalıştırın

# Analyzer API için firewall kuralı
New-NetFirewallRule -DisplayName "DLP Risk Analyzer API" `
    -Direction Inbound -Protocol TCP -LocalPort 5001 -Action Allow

# Dashboard için firewall kuralı
New-NetFirewallRule -DisplayName "DLP Risk Analyzer Dashboard" `
    -Direction Inbound -Protocol TCP -LocalPort 3002 -Action Allow

# PostgreSQL için firewall kuralı (sadece localhost için)
New-NetFirewallRule -DisplayName "PostgreSQL" `
    -Direction Inbound -Protocol TCP -LocalPort 5432 -Action Allow `
    -RemoteAddress 127.0.0.1

# Redis için firewall kuralı (sadece localhost için)
New-NetFirewallRule -DisplayName "Redis" `
    -Direction Inbound -Protocol TCP -LocalPort 6379 -Action Allow `
    -RemoteAddress 127.0.0.1

# Firewall kurallarını kontrol et
Get-NetFirewallRule -DisplayName "DLP*" | Format-Table DisplayName, Enabled, Direction, Action
```

---

## 📊 Monitoring ve Logging

### 1. Log Dosyaları Konumları

- **Analyzer API**: `C:\Services\DLPRiskAnalyzerAPI\logs\`
- **Collector**: `C:\Services\DLPRiskAnalyzerCollector\logs\`
- **Dashboard**: PM2 logs (`pm2 logs dlp-dashboard`)

### 2. Event Log Yapılandırması

```powershell
# Custom event log oluştur
New-EventLog -LogName "DLP Risk Analyzer" -Source "DLPRiskAnalyzerAPI"
New-EventLog -LogName "DLP Risk Analyzer" -Source "DLPRiskAnalyzerCollector"

# Event log'ları görüntüle
Get-EventLog -LogName "DLP Risk Analyzer" -Newest 50
```

### 3. Performance Monitoring

```powershell
# CPU ve Memory kullanımını izle
Get-Process | Where-Object {$_.ProcessName -like "*DLP*"} | Format-Table ProcessName, CPU, WorkingSet

# Service durumlarını kontrol et
Get-Service | Where-Object {$_.DisplayName -like "*DLP*"} | Format-Table DisplayName, Status
```

---

## 💾 Backup Stratejileri

### 1. PostgreSQL Backup

```powershell
# Backup klasörü oluştur
$backupDir = "C:\Backups\PostgreSQL"
New-Item -ItemType Directory -Path $backupDir -Force

# Günlük backup script'i
$backupFile = "$backupDir\dlp_analyzer_$(Get-Date -Format 'yyyyMMdd_HHmmss').backup"
$env:PGPASSWORD = "YourStrongPassword123!"
& "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -U postgres -d dlp_analyzer -F c -f $backupFile

# Backup'ı kontrol et
Test-Path $backupFile
```

### 2. Otomatik Backup (Task Scheduler)

```powershell
# Task Scheduler ile günlük backup
$action = New-ScheduledTaskAction -Execute "C:\Scripts\backup-postgresql.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "DLP PostgreSQL Backup" -Action $action -Trigger $trigger -Description "Daily backup of dlp_analyzer database"
```

### 3. Redis Backup (Memurai)

Memurai otomatik olarak persistence sağlar. Manuel backup için:

```powershell
# Redis data dizinini yedekle
$redisDataDir = "C:\ProgramData\Memurai"
$backupDir = "C:\Backups\Redis"
New-Item -ItemType Directory -Path $backupDir -Force
Copy-Item -Path "$redisDataDir\*" -Destination "$backupDir\$(Get-Date -Format 'yyyyMMdd')" -Recurse
```

---

## 🔍 Troubleshooting

### 1. PostgreSQL Bağlantı Sorunları

```powershell
# PostgreSQL servisinin çalıştığını kontrol et
Get-Service postgresql-x64-18

# PostgreSQL port'unu kontrol et
netstat -an | findstr :5432

# PostgreSQL log'larını kontrol et
Get-Content "C:\Program Files\PostgreSQL\18\data\log\postgresql-*.log" -Tail 50
```

### 2. Redis Bağlantı Sorunları

```powershell
# Memurai servisinin çalıştığını kontrol et
Get-Service Memurai

# Redis port'unu kontrol et
netstat -an | findstr :6379

# Redis bağlantı testi
memurai-cli ping
```

### 3. API Bağlantı Sorunları

```powershell
# API'nin çalıştığını kontrol et
Invoke-WebRequest -Uri "http://localhost:5001/swagger" -UseBasicParsing

# API log'larını kontrol et
Get-Content "C:\Services\DLPRiskAnalyzerAPI\logs\stdout.log" -Tail 50
```

### 4. Migration Sorunları

```powershell
# Migration'ları sıfırdan çalıştır (DİKKAT: Veri kaybı olabilir)
cd "C:\Projects\dlp-risk-adaptive-protection-csharp\DLP.RiskAnalyzer.Analyzer"
dotnet ef database drop --force
dotnet ef database update
```

### 5. TimescaleDB Extension Sorunları

```powershell
# Extension'ın yüklü olduğunu kontrol et
$env:PGPASSWORD = "YourStrongPassword123!"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d dlp_analyzer -c "SELECT * FROM pg_extension WHERE extname = 'timescaledb';"

# Eğer yüklü değilse, extension'ı yükle
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d dlp_analyzer -c "CREATE EXTENSION IF NOT EXISTS timescaledb;"
```

---

## ✅ Kurulum Doğrulama Checklist

### Sistem Gereksinimleri
- [ ] Windows Server 2025 kurulu ve güncel
- [ ] .NET 8.0 SDK kurulu (`dotnet --version`)
- [ ] Node.js ve npm kurulu (`node --version`, `npm --version`)
- [ ] Git kurulu (`git --version`)

### PostgreSQL
- [ ] PostgreSQL 18 kurulu ve çalışıyor
- [ ] `dlp_analyzer` database oluşturuldu
- [ ] TimescaleDB extension yüklü
- [ ] PostgreSQL servisi otomatik başlatılıyor

### Redis
- [ ] Memurai (veya Redis) kurulu ve çalışıyor
- [ ] Redis port 6379'da dinliyor
- [ ] Redis bağlantı testi başarılı (`ping` → `PONG`)

### Proje Kurulumu
- [ ] Proje klonlandı/kopyalandı
- [ ] NuGet paketleri restore edildi
- [ ] Dashboard npm paketleri yüklendi
- [ ] Entity Framework migrations çalıştırıldı

### Yapılandırma
- [ ] `appsettings.json` düzenlendi (PostgreSQL şifresi, DLP API bilgileri)
- [ ] `dashboard/.env.local` oluşturuldu (opsiyonel)

### Windows Services
- [ ] Analyzer API service kuruldu ve çalışıyor
- [ ] Collector service kuruldu ve çalışıyor
- [ ] Dashboard PM2 ile çalışıyor

### Network ve Firewall
- [ ] Port 5001 (API) firewall'da açık
- [ ] Port 3002 (Dashboard) firewall'da açık
- [ ] Port 5432 (PostgreSQL) sadece localhost için açık
- [ ] Port 6379 (Redis) sadece localhost için açık

### Test ve Doğrulama
- [ ] API Swagger UI erişilebilir: `http://localhost:5001/swagger`
- [ ] Dashboard erişilebilir: `http://localhost:3002`
- [ ] Network IP'den erişim test edildi: `http://SERVER_IP:5001` ve `http://SERVER_IP:3002`
- [ ] DLP API bağlantısı test edildi (Settings → DLP API Configuration → Test)

### Backup
- [ ] PostgreSQL backup script'i hazırlandı
- [ ] Otomatik backup task scheduler'da ayarlandı
- [ ] Backup klasörü oluşturuldu

---

## 📞 Destek ve İletişim

Sorun yaşarsanız:
1. Log dosyalarını kontrol edin
2. Event Viewer'da hataları kontrol edin
3. GitHub Issues'da sorun bildirin: https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp/issues

---

**Son Güncelleme**: 2024-11-XX
**Versiyon**: 1.0.0

