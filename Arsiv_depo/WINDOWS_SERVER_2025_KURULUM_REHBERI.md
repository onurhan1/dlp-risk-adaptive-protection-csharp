# Windows Server 2025 Kurulum Rehberi
## DLP Risk Analyzer - Production Deployment

---

## 📋 İçindekiler

1. [Sistem Gereksinimleri](#sistem-gereksinimleri)
2. [Önkoşullar ve Yazılım Kurulumları](#önkoşullar-ve-yazılım-kurulumları)
3. [Docker ile Kurulum (Önerilen)](#docker-ile-kurulum-önerilen)
4. [Veritabanı Kurulumu](#veritabanı-kurulumu)
5. [Redis Kurulumu](#redis-kurulumu)
6. [Proje Kurulumu](#proje-kurulumu)
7. [Yapılandırma](#yapılandırma)
8. [Network IP Erişimi Yapılandırması](#network-ip-erişimi-yapılandırması)
9. [Windows Service Kurulumu](#windows-service-kurulumu)
10. [Firewall Yapılandırması](#firewall-yapılandırması)
11. [IIS Kurulumu (Opsiyonel)](#iis-kurulumu-opsiyonel)
12. [Domain Ortamı Yapılandırması](#domain-ortamı-yapılandırması)
13. [Güvenlik Ayarları](#güvenlik-ayarları)
14. [Monitoring ve Logging](#monitoring-ve-logging)
15. [Backup Stratejileri](#backup-stratejileri)
16. [Troubleshooting](#troubleshooting)
17. [Kurulum Doğrulama Checklist](#kurulum-doğrulama-checklist)

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

### ⚠️ Sanal Sunucu (VM) İçin Özel Notlar

Eğer Windows Server 2025 bir sanal makine (VM) üzerinde çalışacaksa:

#### Hyper-V / VMware / VirtualBox için:
- **RAM**: En az 16 GB (32 GB önerilir) - Docker kullanıyorsanız ekstra 4-8 GB daha
- **Disk**: 
  - En az 100 GB (200 GB önerilir)
  - **Thin provisioning** kullanıyorsanız, gerçek kullanımı izleyin
  - **Docker volumes** için ekstra alan ayırın (en az 50 GB)
- **CPU**: 
  - En az 4 vCPU (8+ vCPU önerilir)
  - **CPU affinity** ayarlayın (performans için)
  - **Hyperthreading** etkin olmalı
- **Network**: 
  - **VMXNET3** (VMware) veya **Synthetic** (Hyper-V) adapter kullanın
  - **NAT** yerine **Bridged** veya **Internal** network kullanın (production için)
- **Docker için**: 
  - **Nested virtualization** etkin olmalı (Hyper-V içinde Docker için)
  - **VT-x/AMD-V** etkin olmalı
- **Snapshot**: Production'da snapshot kullanmayın (performans düşüşü)
- **Time Sync**: VM time sync'i etkin tutun (Windows Time Service)

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

#### Yöntem C: Winget ile Kurulum (Windows Server 2025'te mevcut)

```powershell
# Winget ile kurulum
winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
```

### 3. Docker Desktop Kurulumu (PostgreSQL ve Redis için)

Docker kullanarak PostgreSQL ve Redis'i container olarak çalıştırmak istiyorsanız:

#### Docker Desktop for Windows Server Kurulumu

1. **Docker Desktop for Windows** indirin: https://www.docker.com/products/docker-desktop/
   - **Not**: Windows Server 2025 için "Docker Desktop for Windows" kullanın
   - Alternatif: **Docker Engine** (CLI-only, daha hafif)

2. **Kurulum Seçenekleri**:
   - **Docker Desktop** (GUI + CLI) - Önerilen
   - **Docker Engine** (sadece CLI) - Production için daha uygun

3. **Docker Desktop Kurulumu**:
   ```powershell
   # Chocolatey ile
   choco install docker-desktop -y
   
   # VEYA manuel indirme ve kurulum
   # https://desktop.docker.com/win/main/amd64/Docker%20Desktop%20Installer.exe
   ```

4. **Kurulum Sonrası**:
   ```powershell
   # Docker'ın çalıştığını kontrol edin
   docker --version
   # Beklenen: Docker version 24.x.x veya üzeri
   
   # Docker servisini başlatın
   Start-Service docker
   
   # Test edin
   docker run hello-world
   ```

5. **Sanal Sunucu için Docker Ayarları**:
   ```powershell
   # Docker Desktop Settings → Resources
   # - Memory: En az 4 GB (8 GB önerilir)
   # - CPUs: En az 2 (4+ önerilir)
   # - Disk image size: En az 60 GB
   ```

#### Docker Compose Kurulumu

Docker Compose genellikle Docker Desktop ile birlikte gelir:

```powershell
# Docker Compose'un kurulu olduğunu kontrol edin
docker compose version
# Beklenen: Docker Compose version v2.x.x
```

**Not**: Projede `docker-compose.yml` dosyası mevcuttur. Bu dosya ile PostgreSQL ve Redis'i tek komutla başlatabilirsiniz.

### 4. Node.js 18+ Kurulumu (Dashboard için)

#### Yöntem A: Winget ile (Önerilen)

```powershell
winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
```

#### Yöntem B: Web Installer

1. https://nodejs.org/ adresine gidin
2. **LTS** versiyonunu (18.x veya üzeri) indirin
3. Kurulum sihirbazını takip edin

#### Kurulumu Doğrulama

```powershell
node --version
# Beklenen: v18.x.x veya üzeri

npm --version
# Beklenen: 9.x.x veya üzeri
```

### 5. Git Kurulumu (Opsiyonel - Proje klonlama için)

```powershell
winget install Git.Git --accept-package-agreements --accept-source-agreements
```

---

## 🐳 Docker ile Kurulum (Önerilen)

### 1. Docker Compose ile PostgreSQL ve Redis Başlatma

Proje kök dizininde `docker-compose.yml` dosyası mevcuttur:

```powershell
# Proje dizinine gidin
cd "C:\DLP_RiskAnalyzer"

# Docker Compose ile PostgreSQL ve Redis'i başlatın
docker-compose up -d

# Container'ların çalıştığını kontrol edin
docker ps

# Beklenen çıktı:
# CONTAINER ID   IMAGE                          STATUS
# xxxxx          timescale/timescaledb:latest   Up X minutes
# xxxxx          redis:7-alpine                  Up X minutes
```

### 2. Docker Container Durum Kontrolü

```powershell
# Tüm container'ları listele
docker ps -a

# PostgreSQL container loglarını görüntüle
docker logs dlp-timescaledb

# Redis container loglarını görüntüle
docker logs dlp-redis

# Container'ları durdur
docker-compose down

# Container'ları yeniden başlat
docker-compose restart
```

### 3. Docker Volume Yönetimi

```powershell
# Volume'ları listele
docker volume ls

# Volume'ları temizle (DİKKAT: Veri kaybına neden olur!)
docker-compose down -v
```

---

## 🗄️ Veritabanı Kurulumu

### Seçenek A: Docker ile TimescaleDB (Önerilen)

Yukarıdaki [Docker ile Kurulum](#docker-ile-kurulum-önerilen) bölümünü takip edin.

### Seçenek B: Native PostgreSQL + TimescaleDB Kurulumu

#### 1. PostgreSQL 18 Kurulumu

1. PostgreSQL 18 indirin: https://www.postgresql.org/download/windows/
2. **EnterpriseDB PostgreSQL Installer**'ı indirin
3. Kurulum sırasında:
   - **Installation Directory**: `C:\Program Files\PostgreSQL\18`
   - **Data Directory**: `C:\Program Files\PostgreSQL\18\data`
   - **Port**: `5432` (varsayılan)
   - **Superuser Password**: Güçlü bir şifre belirleyin ve kaydedin
   - **Locale**: `Turkish, Turkey` veya `English, United States`
4. Kurulum tamamlandıktan sonra PostgreSQL servisinin çalıştığını kontrol edin:

```powershell
Get-Service -Name postgresql*
# Beklenen: postgresql-x64-18 (Running)
```

#### 2. TimescaleDB Extension Kurulumu

1. TimescaleDB indirin: https://docs.timescale.com/install/latest/self-hosted/installation-windows/
2. Kurulum sihirbazını takip edin
3. PostgreSQL'e bağlanın ve extension'ı aktif edin:

```powershell
# psql ile bağlanın
psql -U postgres -d postgres

# TimescaleDB extension'ını aktif edin
CREATE DATABASE dlp_analyzer;
\c dlp_analyzer
CREATE EXTENSION IF NOT EXISTS timescaledb;
\q
```

### 3. Veritabanı Migration'larını Çalıştırma

```powershell
# Analyzer projesine gidin
cd "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer"

# Migration'ları çalıştırın
dotnet ef database update

# Migration durumunu kontrol edin
dotnet ef migrations list
```

---

## 🔴 Redis Kurulumu

### Seçenek A: Docker ile Redis (Önerilen)

Yukarıdaki [Docker ile Kurulum](#docker-ile-kurulum-önerilen) bölümünü takip edin.

### Seçenek B: Memurai (Windows Native Redis)

1. Memurai indirin: https://www.memurai.com/get-memurai
2. Kurulum sihirbazını takip edin
3. Windows Services'den **Memurai** servisini başlatın:

```powershell
Start-Service Memurai
Get-Service Memurai
# Beklenen: Running
```

---

## 📁 Proje Kurulumu

### 1. Projeyi İndirme/Klonlama

```powershell
# Git ile klonlama (eğer repository'de ise)
git clone <repository-url> "C:\DLP_RiskAnalyzer"

# VEYA proje dosyalarını C:\DLP_RiskAnalyzer klasörüne kopyalayın
```

### 2. NuGet Paketlerini Restore Etme

```powershell
cd "C:\DLP_RiskAnalyzer"
dotnet restore DLP.RiskAnalyzer.Solution.sln
```

### 3. Projeyi Build Etme

```powershell
dotnet build DLP.RiskAnalyzer.Solution.sln --configuration Release
```

### 4. Dashboard Bağımlılıklarını Yükleme

```powershell
cd "C:\DLP_RiskAnalyzer\dashboard"
npm install
npm run build
```

---

## ⚙️ Yapılandırma

### 1. Analyzer API Yapılandırması

`DLP.RiskAnalyzer.Analyzer\appsettings.json` dosyasını düzenleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analyzer;Username=postgres;Password=YOUR_PASSWORD"
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
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:3001",
      "http://localhost:3002"
    ],
    "AllowInternalNetwork": true
  },
  "Jwt": {
    "SecretKey": "YOUR_SUPER_SECRET_KEY_AT_LEAST_32_CHARACTERS_LONG_CHANGE_THIS_IN_PRODUCTION",
    "Issuer": "DLP-RiskAnalyzer",
    "Audience": "DLP-RiskAnalyzer-Client",
    "ExpirationHours": 8
  }
}
```

**ÖNEMLİ**: 
- `Jwt:SecretKey` production'da mutlaka değiştirilmeli (en az 32 karakter)
- DLP Manager bilgileri dashboard'dan da yapılandırılabilir (önerilen)

### 2. Collector Service Yapılandırması

`DLP.RiskAnalyzer.Collector\appsettings.json` dosyasını düzenleyin:

```json
{
  "Redis": {
    "Host": "localhost",
    "Port": 6379
  },
  "AnalyzerBridge": {
    "BaseUrl": "http://localhost:5001",
    "SharedSecret": "ChangeThisSecret"
  }
}
```

### 3. Dashboard Yapılandırması

Dashboard otomatik olarak API URL'ini algılar (`lib/api-config.ts`). Ek yapılandırma gerekmez.

---

## 🌐 Network IP Erişimi Yapılandırması

### Önemli: Internal Network Erişimi

Uygulama internal network'te IP adresi ile erişilebilir şekilde yapılandırılmıştır:

- **Dashboard**: `0.0.0.0:3002` üzerinde dinler
- **API**: `0.0.0.0:5001` üzerinde dinler
- **CORS**: Internal network IP'lerini otomatik kabul eder

### 1. API Network IP Yapılandırması

API zaten `0.0.0.0:5001` üzerinde dinleyecek şekilde yapılandırılmıştır (`Program.cs`):

```csharp
// Program.cs - Zaten yapılandırılmış
string defaultUrl = "http://0.0.0.0:5001";
app.Urls.Add(defaultUrl);
```

**Kontrol**:
```powershell
# API başlatıldığında console'da şunu görmelisiniz:
# INFO: API configured to listen on 0.0.0.0:5001 for network access
# API is listening on:
#   - http://0.0.0.0:5001
```

### 2. Dashboard Network IP Yapılandırması

Dashboard `package.json`'da zaten yapılandırılmıştır:

```json
{
  "scripts": {
    "start": "next start -H 0.0.0.0 -p 3002"
  }
}
```

**Kontrol**:
```powershell
# Dashboard başlatıldığında console'da şunu görmelisiniz:
# - Local:        http://localhost:3002
# - Network:      http://0.0.0.0:3002
```

### 3. CORS Yapılandırması

CORS internal network IP'lerini otomatik kabul eder (`appsettings.json`):

```json
{
  "Cors": {
    "AllowInternalNetwork": true
  }
}
```

**Test**:
```powershell
# Sunucu IP'sini öğrenin
ipconfig
# Örnek: 192.168.1.100

# Başka bir cihazdan test edin
# Tarayıcıdan: http://192.168.1.100:3002
# API: http://192.168.1.100:5001/health
```

---

## 🔧 Windows Service Kurulumu

### 1. NSSM (Non-Sucking Service Manager) Kurulumu

Windows Service olarak çalıştırmak için:

```powershell
# Chocolatey ile
choco install nssm -y

# VEYA manuel indirme
# https://nssm.cc/download
# C:\Program Files\nssm klasörüne çıkarın
```

### 2. Collector Service Kurulumu

```powershell
# Projeyi publish edin
cd "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector"
dotnet publish -c Release -o "C:\Services\DLPRiskAnalyzerCollector"

# NSSM ile service kurun
nssm install DLPRiskAnalyzerCollector "C:\Program Files\dotnet\dotnet.exe" "C:\Services\DLPRiskAnalyzerCollector\DLP.RiskAnalyzer.Collector.dll"

# Service ayarlarını yapılandırın
nssm set DLPRiskAnalyzerCollector AppDirectory "C:\Services\DLPRiskAnalyzerCollector"
nssm set DLPRiskAnalyzerCollector DisplayName "DLP Risk Analyzer Collector"
nssm set DLPRiskAnalyzerCollector Description "Collects DLP incidents from Forcepoint DLP Manager and pushes to Redis"
nssm set DLPRiskAnalyzerCollector Start SERVICE_AUTO_START
nssm set DLPRiskAnalyzerCollector AppStdout "C:\Services\DLPRiskAnalyzerCollector\logs\stdout.log"
nssm set DLPRiskAnalyzerCollector AppStderr "C:\Services\DLPRiskAnalyzerCollector\logs\stderr.log"

# Log klasörü oluşturun
New-Item -ItemType Directory -Path "C:\Services\DLPRiskAnalyzerCollector\logs" -Force

# Service'i başlatın
nssm start DLPRiskAnalyzerCollector

# Service durumunu kontrol edin
Get-Service DLPRiskAnalyzerCollector
```

### 3. Analyzer API Service Kurulumu

IIS kullanmıyorsanız, Analyzer API'yi de Windows Service olarak çalıştırabilirsiniz:

```powershell
# Projeyi publish edin
cd "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer"
dotnet publish -c Release -o "C:\Services\DLPRiskAnalyzerAPI"

# NSSM ile service kurun
nssm install DLPRiskAnalyzerAPI "C:\Program Files\dotnet\dotnet.exe" "C:\Services\DLPRiskAnalyzerAPI\DLP.RiskAnalyzer.Analyzer.dll"

# Service ayarlarını yapılandırın
nssm set DLPRiskAnalyzerAPI AppDirectory "C:\Services\DLPRiskAnalyzerAPI"
nssm set DLPRiskAnalyzerAPI DisplayName "DLP Risk Analyzer API"
nssm set DLPRiskAnalyzerAPI Description "DLP Risk Analyzer REST API Service"
nssm set DLPRiskAnalyzerAPI Start SERVICE_AUTO_START
nssm set DLPRiskAnalyzerAPI AppStdout "C:\Services\DLPRiskAnalyzerAPI\logs\stdout.log"
nssm set DLPRiskAnalyzerAPI AppStderr "C:\Services\DLPRiskAnalyzerAPI\logs\stderr.log"

# Environment variable ekleyin (network IP erişimi için)
nssm set DLPRiskAnalyzerAPI AppEnvironmentExtra "ASPNETCORE_URLS=http://0.0.0.0:5001"

# Log klasörü oluşturun
New-Item -ItemType Directory -Path "C:\Services\DLPRiskAnalyzerAPI\logs" -Force

# Service'i başlatın
nssm start DLPRiskAnalyzerAPI

# Service durumunu kontrol edin
Get-Service DLPRiskAnalyzerAPI
```

### 4. Dashboard Service Kurulumu

Dashboard'u Windows Service olarak çalıştırmak için PM2 veya NSSM kullanabilirsiniz:

#### Yöntem A: PM2 (Önerilen)

```powershell
# PM2 global kurulum
npm install -g pm2
npm install -g pm2-windows-startup

# Dashboard'u build edin
cd "C:\DLP_RiskAnalyzer\dashboard"
npm run build

# Dashboard'u PM2 ile başlatın
pm2 start npm --name "dlp-dashboard" -- run start

# PM2'yi Windows startup'a ekleyin
pm2 startup
pm2 save
```

#### Yöntem B: NSSM ile

```powershell
# Dashboard'u build edin
cd "C:\DLP_RiskAnalyzer\dashboard"
npm run build

# NSSM ile service kurun
nssm install DLPRiskAnalyzerDashboard "C:\Program Files\nodejs\node.exe"
nssm set DLPRiskAnalyzerDashboard AppParameters "C:\DLP_RiskAnalyzer\dashboard\node_modules\.bin\next start -H 0.0.0.0 -p 3002"
nssm set DLPRiskAnalyzerDashboard AppDirectory "C:\DLP_RiskAnalyzer\dashboard"
nssm set DLPRiskAnalyzerDashboard DisplayName "DLP Risk Analyzer Dashboard"
nssm set DLPRiskAnalyzerDashboard Description "DLP Risk Analyzer Web Dashboard (Next.js)"
nssm set DLPRiskAnalyzerDashboard Start SERVICE_AUTO_START
nssm set DLPRiskAnalyzerDashboard AppStdout "C:\DLP_RiskAnalyzer\dashboard\logs\stdout.log"
nssm set DLPRiskAnalyzerDashboard AppStderr "C:\DLP_RiskAnalyzer\dashboard\logs\stderr.log"

# Log klasörü oluşturun
New-Item -ItemType Directory -Path "C:\DLP_RiskAnalyzer\dashboard\logs" -Force

# Service'i başlatın
nssm start DLPRiskAnalyzerDashboard

# Service durumunu kontrol edin
Get-Service DLPRiskAnalyzerDashboard
```

### 5. Service Yönetimi

```powershell
# Service'leri başlat
Start-Service DLPRiskAnalyzerCollector
Start-Service DLPRiskAnalyzerAPI
Start-Service DLPRiskAnalyzerDashboard

# Service'leri durdur
Stop-Service DLPRiskAnalyzerCollector
Stop-Service DLPRiskAnalyzerAPI
Stop-Service DLPRiskAnalyzerDashboard

# Service'leri yeniden başlat
Restart-Service DLPRiskAnalyzerCollector
Restart-Service DLPRiskAnalyzerAPI
Restart-Service DLPRiskAnalyzerDashboard

# Service durumunu kontrol et
Get-Service DLPRiskAnalyzer*

# Service'leri kaldır
nssm remove DLPRiskAnalyzerCollector confirm
nssm remove DLPRiskAnalyzerAPI confirm
nssm remove DLPRiskAnalyzerDashboard confirm
```

---

## 🔥 Firewall Yapılandırması

### 1. Gerekli Portları Açma

```powershell
# PowerShell'i Administrator olarak açın

# Analyzer API (Port 5001)
New-NetFirewallRule -DisplayName "DLP Risk Analyzer API" -Direction Inbound -Protocol TCP -LocalPort 5001 -Action Allow

# Dashboard (Port 3002)
New-NetFirewallRule -DisplayName "DLP Risk Analyzer Dashboard" -Direction Inbound -Protocol TCP -LocalPort 3002 -Action Allow

# PostgreSQL (Port 5432) - Sadece internal network için
New-NetFirewallRule -DisplayName "PostgreSQL" -Direction Inbound -Protocol TCP -LocalPort 5432 -Action Allow -RemoteAddress 192.168.0.0/16,10.0.0.0/8,172.16.0.0/12

# Redis (Port 6379) - Sadece localhost için
New-NetFirewallRule -DisplayName "Redis" -Direction Inbound -Protocol TCP -LocalPort 6379 -Action Allow -RemoteAddress 127.0.0.1
```

### 2. Firewall Kurallarını Kontrol Etme

```powershell
# Tüm DLP kurallarını listele
Get-NetFirewallRule -DisplayName "DLP*"

# Kural detaylarını görüntüle
Get-NetFirewallRule -DisplayName "DLP Risk Analyzer API" | Get-NetFirewallAddressFilter
```

### 3. Firewall Kuralını Kaldırma

```powershell
Remove-NetFirewallRule -DisplayName "DLP Risk Analyzer API"
```

---

## 🌐 IIS Kurulumu (Opsiyonel)

IIS kullanarak Analyzer API'yi reverse proxy olarak kullanabilirsiniz:

### 1. IIS ve ASP.NET Core Module Kurulumu

```powershell
# IIS ve gerekli özellikleri kurun
Install-WindowsFeature -Name Web-Server,Web-Mgmt-Tools,Web-WebServer,Web-Common-Http,Web-Default-Doc,Web-Dir-Browsing,Web-Http-Errors,Web-Static-Content,Web-Health,Web-Http-Logging,Web-Performance,Web-Stat-Compression,Web-Security,Web-Filtering,Web-Basic-Auth,Web-Windows-Auth,Web-App-Dev,Web-Net-Ext45,Web-Asp-Net45,Web-ISAPI-Ext,Web-ISAPI-Filter,Web-Mgmt-Console

# ASP.NET Core Hosting Bundle indirin ve kurun
# https://dotnet.microsoft.com/download/dotnet/8.0
# "Hosting Bundle" seçeneğini indirin
```

### 2. IIS Site Yapılandırması

```powershell
# IIS Manager'ı açın
# Yeni bir site oluşturun:
# - Site name: DLP Risk Analyzer API
# - Physical path: C:\Services\DLPRiskAnalyzerAPI
# - Binding: http, localhost:80 (veya istediğiniz port)
```

### 3. web.config Dosyası

`C:\Services\DLPRiskAnalyzerAPI\web.config` dosyasını oluşturun:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" 
                  arguments=".\DLP.RiskAnalyzer.Analyzer.dll" 
                  stdoutLogEnabled="true" 
                  stdoutLogFile=".\logs\stdout" 
                  hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

---

## 🏢 Domain Ortamı Yapılandırması

### 1. Service Account Oluşturma

```powershell
# Domain'de service account oluşturun (Domain Admin gerekli)
# Active Directory Users and Computers kullanarak:
# - Yeni bir kullanıcı oluşturun: svc_dlp_riskanalyzer
# - "Password never expires" seçeneğini işaretleyin
# - "User cannot change password" seçeneğini işaretleyin
# - "Log on as a service" hakkı verin
```

### 2. Service Account'a Gerekli Hakları Verme

```powershell
# "Log on as a service" hakkı
$user = "DOMAIN\svc_dlp_riskanalyzer"
$right = "SeServiceLogonRight"

# Local Security Policy'den veya Group Policy'den ayarlayın
```

### 3. Service'leri Domain Account ile Çalıştırma

```powershell
# NSSM ile service account ayarlama
nssm set DLPRiskAnalyzerCollector ObjectName "DOMAIN\svc_dlp_riskanalyzer" "YourPassword123!"

# Service'i yeniden başlatın
Restart-Service DLPRiskAnalyzerCollector
```

---

## 🔒 Güvenlik Ayarları

### 1. JWT Secret Key Değiştirme

**KRİTİK**: Production'da mutlaka değiştirin!

```json
{
  "Jwt": {
    "SecretKey": "YOUR_SUPER_SECRET_KEY_AT_LEAST_32_CHARACTERS_LONG_CHANGE_THIS_IN_PRODUCTION"
  }
}
```

### 2. DLP Manager Şifre Güvenliği

DLP Manager şifreleri artık Data Protection API ile şifrelenir ve `system_settings` tablosunda saklanır. Dashboard'dan yapılandırılabilir.

### 3. CORS Yapılandırması

Internal network için:
```json
{
  "Cors": {
    "AllowInternalNetwork": true
  }
}
```

Production'da sadece belirli origin'ler:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://192.168.1.100:3002",
      "https://dlp-analyzer.company.com"
    ],
    "AllowInternalNetwork": false
  }
}
```

### 4. HTTPS Yapılandırması (Önerilen)

Internal network'te bile HTTPS kullanılması önerilir:

```powershell
# IIS'de SSL sertifikası yapılandırın
# VEYA reverse proxy (nginx, IIS) kullanın
```

---

## 📊 Monitoring ve Logging

### 1. Event Log Yapılandırması

```powershell
# Custom event log oluştur
New-EventLog -LogName "DLP Risk Analyzer" -Source "DLPRiskAnalyzerAPI"
New-EventLog -LogName "DLP Risk Analyzer" -Source "DLPRiskAnalyzerCollector"
```

### 2. Log Dosyaları

- **API Logs**: `C:\Services\DLPRiskAnalyzerAPI\logs\`
- **Collector Logs**: `C:\Services\DLPRiskAnalyzerCollector\logs\`
- **Dashboard Logs**: `C:\DLP_RiskAnalyzer\dashboard\logs\`

### 3. Performance Monitoring

```powershell
# Performance Monitor'ü açın
perfmon

# Şu metrikleri izleyin:
# - CPU Usage
# - Memory Usage
# - Network I/O
# - Disk I/O
# - Database Connection Pool
```

---

## 💾 Backup Stratejileri

### 1. Veritabanı Yedekleme

```powershell
# PostgreSQL backup script
$backupPath = "C:\Backups\DLP_RiskAnalyzer"
New-Item -ItemType Directory -Path $backupPath -Force

# pg_dump ile backup
& "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -U postgres -d dlp_analyzer -F c -f "$backupPath\dlp_analyzer_$(Get-Date -Format 'yyyyMMdd_HHmmss').backup"

# VEYA Docker ile
docker exec dlp-timescaledb pg_dump -U postgres dlp_analyzer > "$backupPath\dlp_analyzer_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"
```

### 2. Otomatik Yedekleme (Task Scheduler)

```powershell
# Task Scheduler'da yeni görev oluştur
$action = New-ScheduledTaskAction -Execute "C:\Scripts\backup-dlp-database.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "DLP Database Backup" -Action $action -Trigger $trigger -Principal $principal
```

---

## 🔧 Troubleshooting

### Sorun 1: API Network IP'den Erişilemiyor

**Çözüm**:
```powershell
# API'nin 0.0.0.0:5001'de dinlediğini kontrol edin
netstat -an | findstr :5001
# Beklenen: TCP    0.0.0.0:5001           0.0.0.0:0              LISTENING

# Firewall kuralını kontrol edin
Get-NetFirewallRule -DisplayName "DLP Risk Analyzer API"
```

### Sorun 2: CORS Hatası

**Çözüm**:
```powershell
# appsettings.json'da AllowInternalNetwork: true olduğundan emin olun
# API'yi yeniden başlatın
Restart-Service DLPRiskAnalyzerAPI
```

### Sorun 3: Database Bağlantı Hatası

**Çözüm**:
```powershell
# PostgreSQL servisinin çalıştığını kontrol edin
Get-Service postgresql*

# Connection string'i kontrol edin
# appsettings.json'da ConnectionStrings:DefaultConnection
```

### Sorun 4: Redis Bağlantı Hatası

**Çözüm**:
```powershell
# Redis container'ının çalıştığını kontrol edin
docker ps | findstr redis

# VEYA Memurai servisinin çalıştığını kontrol edin
Get-Service Memurai
```

---

## ✅ Kurulum Doğrulama Checklist

### Önkoşullar
- [ ] Windows Server 2025 kurulu ve güncel
- [ ] .NET 8.0 SDK kurulu (`dotnet --version`)
- [ ] Node.js 18+ kurulu (`node --version`)
- [ ] Docker Desktop kurulu ve çalışıyor (`docker --version`)
- [ ] PostgreSQL/TimescaleDB çalışıyor
- [ ] Redis çalışıyor

### Proje Kurulumu
- [ ] Proje klonlandı/kopyalandı
- [ ] NuGet paketleri restore edildi (`dotnet restore`)
- [ ] Proje build edildi (`dotnet build`)
- [ ] Dashboard bağımlılıkları yüklendi (`npm install`)
- [ ] Dashboard build edildi (`npm run build`)

### Yapılandırma
- [ ] `appsettings.json` dosyaları yapılandırıldı
- [ ] Database migration'ları çalıştırıldı (`dotnet ef database update`)
- [ ] JWT Secret Key değiştirildi
- [ ] DLP Manager bilgileri yapılandırıldı

### Network Erişimi
- [ ] API `0.0.0.0:5001` üzerinde dinliyor
- [ ] Dashboard `0.0.0.0:3002` üzerinde dinliyor
- [ ] CORS `AllowInternalNetwork: true` yapılandırıldı
- [ ] Firewall kuralları eklendi

### Servisler
- [ ] Collector Service kuruldu ve çalışıyor
- [ ] Analyzer API Service kuruldu ve çalışıyor
- [ ] Dashboard Service kuruldu ve çalışıyor

### Test
- [ ] API health check başarılı (`http://localhost:5001/health`)
- [ ] Dashboard erişilebilir (`http://localhost:3002`)
- [ ] Network IP'den erişim başarılı (`http://[SERVER_IP]:3002`)
- [ ] Login çalışıyor
- [ ] DLP Manager bağlantısı test edildi

---

## 📞 Destek

Sorun yaşarsanız:
1. Log dosyalarını kontrol edin
2. Event Viewer'da hataları kontrol edin
3. `Troubleshooting` bölümüne bakın
4. GitHub Issues'da sorun bildirin

---

**Son Güncelleme**: 2025-01-XX  
**Versiyon**: 1.0.0  
**Windows Server 2025 Uyumlu**: ✅

