# Windows Server 2022 Kurulum Rehberi
## DLP Risk Analyzer - Production Deployment

---

## 📋 İçindekiler

1. [Sistem Gereksinimleri](#sistem-gereksinimleri)
2. [Önkoşullar ve Yazılım Kurulumları](#önkoşullar-ve-yazılım-kurulumları)
3. [Docker ile Kurulum (Alternatif)](#docker-ile-kurulum-alternatif)
4. [Veritabanı Kurulumu](#veritabanı-kurulumu)
5. [Redis Kurulumu](#redis-kurulumu)
6. [Proje Kurulumu](#proje-kurulumu)
7. [Yapılandırma](#yapılandırma)
8. [Windows Service Kurulumu](#windows-service-kurulumu)
9. [Firewall Yapılandırması](#firewall-yapılandırması)
10. [IIS Kurulumu (Opsiyonel)](#iis-kurulumu-opsiyonel)
11. [Domain Ortamı Yapılandırması](#domain-ortamı-yapılandırması)
12. [Güvenlik Ayarları](#güvenlik-ayarları)
13. [Monitoring ve Logging](#monitoring-ve-logging)
14. [Backup Stratejileri](#backup-stratejileri)
15. [Troubleshooting](#troubleshooting)
16. [Kurulum Doğrulama Checklist](#kurulum-doğrulama-checklist)

---

## 🖥️ Sistem Gereksinimleri

### Minimum Gereksinimler
- **İşletim Sistemi**: Windows Server 2022 (Standard veya Datacenter)
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

Eğer Windows Server 2022 bir sanal makine (VM) üzerinde çalışacaksa:

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
- **5001**: Analyzer API (HTTP)
- **3002**: Web Dashboard (Next.js)
- **5432**: PostgreSQL
- **6379**: Redis
- **8443**: Forcepoint DLP Manager API (HTTPS - giden bağlantı)

---

## 📦 Önkoşullar ve Yazılım Kurulumları

### 1. Windows Server 2022 Güncellemeleri

```powershell
# Windows Update'i kontrol edin ve güncelleyin
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

### 3. Docker Desktop Kurulumu (PostgreSQL ve Redis için)

Docker kullanarak PostgreSQL ve Redis'i container olarak çalıştırmak istiyorsanız:

#### Docker Desktop for Windows Server Kurulumu

1. **Docker Desktop for Windows** indirin: https://www.docker.com/products/docker-desktop/
   - **Not**: Windows Server 2022 için "Docker Desktop for Windows" kullanın
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

### 4. PostgreSQL 18 Kurulumu

**Kurulum Seçeneği**: Docker kullanmak istiyorsanız [Docker ile Kurulum](#docker-ile-kurulum-alternatif) bölümüne bakın.

#### Yöntem A: PostgreSQL Windows Installer (Native - Önerilen)

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

#### Yöntem B: Chocolatey ile Kurulum

```powershell
# Chocolatey kurulumu (eğer yoksa)
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

# PostgreSQL kurulumu
choco install postgresql18 --params '/Password:YourStrongPassword123!' -y
```

#### PostgreSQL Yapılandırması

```powershell
# PostgreSQL servisini başlatın
Start-Service postgresql-x64-18

# PostgreSQL'e bağlanın
$env:PGPASSWORD = "YourStrongPassword123!"
psql -U postgres -h localhost -d postgres

# PostgreSQL komut satırında:
```

```sql
-- Database oluştur
CREATE DATABASE dlp_analyzer;

-- Kullanıcı oluştur (opsiyonel, güvenlik için)
CREATE USER dlp_user WITH PASSWORD 'YourStrongPassword123!';
GRANT ALL PRIVILEGES ON DATABASE dlp_analyzer TO dlp_user;

-- Bağlantıyı kapat
\q
```

### 4. Redis Kurulumu

#### Yöntem A: Memurai (Windows Native - Önerilen)

1. Memurai indirin: https://www.memurai.com/get-memurai
2. **Memurai Developer Edition** (ücretsiz) veya **Enterprise Edition** kurun
3. Kurulum sırasında:
   - **Port**: `6379` (varsayılan)
   - **Service Account**: `NT AUTHORITY\NetworkService` (varsayılan)
4. Kurulum sonrası servisi başlatın:

```powershell
Start-Service Memurai
Get-Service Memurai
# Beklenen: Running
```

#### Yöntem B: WSL2 ile Redis (Alternatif)

```powershell
# WSL2 kurulumu (eğer yoksa)
wsl --install

# WSL2'de Redis kurulumu
wsl sudo apt-get update
wsl sudo apt-get install redis-server -y
wsl sudo service redis-server start

# Windows'tan erişim için WSL2 IP'sini kullanın
```

#### Yöntem C: Docker ile Redis (Docker Compose Önerilir)

Docker Compose kullanarak Redis kurulumu için [Docker ile Kurulum](#docker-ile-kurulum-alternatif) bölümüne bakın.

**Manuel Docker kurulumu**:
```powershell
# Docker Desktop kurulumu (eğer yoksa)
# https://www.docker.com/products/docker-desktop/

docker run -d `
  --name dlp-redis `
  --restart unless-stopped `
  -p 6379:6379 `
  -v redis_data:/data `
  redis:7-alpine redis-server --appendonly yes
```

**Not**: Production için Docker Compose kullanmanız önerilir (projede `docker-compose.yml` mevcut).

### 5. Node.js 18+ Kurulumu (Dashboard için)

1. Node.js LTS indirin: https://nodejs.org/
2. **Windows Installer (.msi)** indirin (v18.x veya üzeri)
3. Kurulumu tamamlayın
4. Kurulumu doğrulayın:

```powershell
node --version
# Beklenen: v18.x.x veya üzeri
npm --version
# Beklenen: 9.x.x veya üzeri
```

### 6. Git for Windows Kurulumu

1. Git for Windows indirin: https://git-scm.com/download/win
2. Kurulumu tamamlayın (varsayılan ayarlar yeterli)
3. Kurulumu doğrulayın:

```powershell
git --version
```

### 7. NSSM (Non-Sucking Service Manager) Kurulumu

Windows Service olarak çalıştırmak için:

```powershell
# Chocolatey ile
choco install nssm -y

# VEYA manuel indirme
# https://nssm.cc/download
# C:\Program Files\nssm klasörüne çıkarın
```

---

## 🗄️ Veritabanı Kurulumu

### 1. PostgreSQL Bağlantı Testi

```powershell
# PostgreSQL servisinin çalıştığını kontrol edin
Get-Service postgresql-x64-18

# Bağlantı testi
$env:PGPASSWORD = "YourPostgreSQLPassword"
psql -U postgres -h localhost -d postgres -c "SELECT version();"
```

### 2. Database ve Extension Oluşturma

```powershell
# PostgreSQL'e bağlanın
$env:PGPASSWORD = "YourPostgreSQLPassword"
psql -U postgres -h localhost -d postgres
```

```sql
-- Database oluştur
CREATE DATABASE dlp_analyzer
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'Turkish_Turkey.1254'
    LC_CTYPE = 'Turkish_Turkey.1254'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- Database'e bağlan
\c dlp_analyzer

-- TimescaleDB extension'ı etkinleştir (opsiyonel, standart PostgreSQL de yeterli)
-- CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Bağlantıyı kapat
\q
```

### 3. Entity Framework Migrations

```powershell
# Proje klasörüne gidin
cd "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer"

# Entity Framework Core Tools kurun
dotnet tool install --global dotnet-ef

# Migration'ları uygulayın
dotnet ef database update

# Beklenen çıktı:
# Applying migration '20241109184015_AddSystemSettingsTable'.
# Done.
```

---

## 🔴 Redis Kurulumu

### 1. Redis Bağlantı Testi

```powershell
# Memurai için
redis-cli ping
# Beklenen: PONG

# VEYA
redis-cli -h localhost -p 6379 ping
```

### 2. Redis Yapılandırması (Memurai)

Memurai yapılandırma dosyası: `C:\Program Files\Memurai\memurai.conf`

```conf
# Port
port 6379

# Persistence
appendonly yes
appendfsync everysec

# Memory
maxmemory 2gb
maxmemory-policy allkeys-lru

# Security (production için)
# requirepass YourStrongRedisPassword123!
```

Yapılandırmayı değiştirdikten sonra:

```powershell
Restart-Service Memurai
```

---

## 🐳 Docker ile Kurulum (Alternatif)

Docker kullanarak PostgreSQL ve Redis'i container olarak çalıştırmak, kurulumu kolaylaştırır ve yönetimi basitleştirir. Bu yöntem özellikle **sanal sunucular** için önerilir.

### Avantajları
- ✅ Kolay kurulum ve yönetim
- ✅ İzole ortam (diğer servislerden bağımsız)
- ✅ Kolay yedekleme ve geri yükleme
- ✅ Versiyon yönetimi (farklı PostgreSQL/Redis versiyonları)
- ✅ Hızlı başlatma/durdurma

### Dezavantajları
- ⚠️ Ekstra RAM kullanımı (container overhead)
- ⚠️ Docker Desktop lisansı gerekebilir (production için)
- ⚠️ Nested virtualization gerekebilir (VM içinde)

### 1. Docker Compose ile Kurulum

Projede `docker-compose.yml` dosyası mevcuttur. Bu dosya ile PostgreSQL ve Redis'i tek komutla başlatabilirsiniz.

#### docker-compose.yml Yapılandırması

Proje kök dizinindeki `docker-compose.yml` dosyasını kontrol edin:

```yaml
version: '3.8'

services:
  timescaledb:
    image: timescale/timescaledb:latest-pg16
    container_name: dlp-timescaledb
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: dlp_analytics
      TZ: Europe/Istanbul
    ports:
      - "5432:5432"
    volumes:
      - timescaledb_data:/var/lib/postgresql/data
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    container_name: dlp-redis
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    restart: unless-stopped
    command: redis-server --appendonly yes

volumes:
  timescaledb_data:
  redis_data:
```

**Önemli**: Production için şifreleri değiştirin!

#### docker-compose.yml'i Güncelleme (Production)

```powershell
# docker-compose.yml dosyasını düzenleyin
notepad docker-compose.yml

# Şu satırları güncelleyin:
# POSTGRES_PASSWORD: YourStrongPassword123!
# POSTGRES_DB: dlp_analyzer (dlp_analytics yerine)
```

#### Container'ları Başlatma

```powershell
# Proje kök dizinine gidin
cd "C:\DLP_RiskAnalyzer"

# Container'ları başlatın
docker compose up -d

# Beklenen çıktı:
# Creating network "dlp-network" ... done
# Creating volume "dlp-risk-analyzer_timescaledb_data" ... done
# Creating volume "dlp-risk-analyzer_redis_data" ... done
# Creating dlp-timescaledb ... done
# Creating dlp-redis ... done
```

#### Container Durumunu Kontrol Etme

```powershell
# Tüm container'ları listele
docker compose ps

# Beklenen çıktı:
# NAME                STATUS          PORTS
# dlp-redis          Up X minutes     0.0.0.0:6379->6379/tcp
# dlp-timescaledb    Up X minutes     0.0.0.0:5432->5432/tcp

# Log'ları görüntüleme
docker compose logs -f

# Belirli bir servisin log'ları
docker compose logs timescaledb
docker compose logs redis
```

#### Container'ları Durdurma

```powershell
# Container'ları durdurun (veriler korunur)
docker compose stop

# Container'ları durdur ve sil (veriler korunur - volumes)
docker compose down

# Container'ları durdur, sil ve volumes'ları da sil (DİKKAT: Tüm veriler silinir!)
docker compose down -v
```

### 2. PostgreSQL Database Oluşturma (Docker)

```powershell
# PostgreSQL container'ına bağlanın
docker exec -it dlp-timescaledb psql -U postgres

# PostgreSQL komut satırında:
```

```sql
-- Database oluştur
CREATE DATABASE dlp_analyzer
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'Turkish_Turkey.1254'
    LC_CTYPE = 'Turkish_Turkey.1254'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- Database'e bağlan
\c dlp_analyzer

-- TimescaleDB extension'ı etkinleştir (opsiyonel)
-- CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Bağlantıyı kapat
\q
```

**VEYA tek komutla**:

```powershell
docker exec -it dlp-timescaledb psql -U postgres -c "CREATE DATABASE dlp_analyzer;"
```

### 3. Entity Framework Migrations (Docker ile)

```powershell
# Proje klasörüne gidin
cd "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer"

# appsettings.json'da connection string'i Docker için güncelleyin
# Host=127.0.0.1 (localhost yerine, Docker port mapping için)

# Migration'ları uygulayın
dotnet ef database update

# Beklenen çıktı:
# Applying migration '20241109184015_AddSystemSettingsTable'.
# Done.
```

### 4. Redis Bağlantı Testi (Docker)

```powershell
# Redis container'ına bağlanın
docker exec -it dlp-redis redis-cli ping

# Beklenen çıktı: PONG

# Redis CLI'ye bağlanın
docker exec -it dlp-redis redis-cli

# Redis komutları:
# PING
# INFO
# EXIT
```

### 5. Docker Volume Yönetimi

#### Volume'ları Listeleme

```powershell
# Tüm volume'ları listele
docker volume ls

# DLP volume'larını listele
docker volume ls | Select-String "dlp"
```

#### Volume Yedekleme

```powershell
# PostgreSQL volume'unu yedekleme
$backupDir = "C:\Backups\Docker"
New-Item -ItemType Directory -Path $backupDir -Force

# PostgreSQL volume'unu yedekle
docker run --rm `
  -v dlp-risk-analyzer_timescaledb_data:/data `
  -v ${backupDir}:/backup `
  alpine tar czf /backup/postgres_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').tar.gz /data

# Redis volume'unu yedekle
docker run --rm `
  -v dlp-risk-analyzer_redis_data:/data `
  -v ${backupDir}:/backup `
  alpine tar czf /backup/redis_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').tar.gz /data
```

#### Volume Geri Yükleme

```powershell
# PostgreSQL volume'unu geri yükle
docker run --rm `
  -v dlp-risk-analyzer_timescaledb_data:/data `
  -v ${backupDir}:/backup `
  alpine sh -c "cd /data && tar xzf /backup/postgres_backup_YYYYMMDD_HHMMSS.tar.gz"
```

### 6. Docker Compose ile Otomatik Başlatma

Windows Server'da Docker container'larının otomatik başlaması için:

#### Yöntem A: Docker Desktop Auto-start

Docker Desktop Settings → General → "Start Docker Desktop when you log in" seçeneğini işaretleyin.

#### Yöntem B: Task Scheduler ile

```powershell
# Task Scheduler ile otomatik başlatma
$action = New-ScheduledTaskAction -Execute "docker" `
    -Argument "compose -f C:\DLP_RiskAnalyzer\docker-compose.yml up -d" `
    -WorkingDirectory "C:\DLP_RiskAnalyzer"

$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName "Start DLP Docker Containers" `
    -Action $action -Trigger $trigger -Principal $principal -Description "Start PostgreSQL and Redis containers"
```

### 7. Docker vs Native Kurulum Karşılaştırması

| Özellik | Docker | Native (PostgreSQL/Redis) |
|---------|--------|---------------------------|
| **Kurulum Kolaylığı** | ⭐⭐⭐⭐⭐ Çok kolay | ⭐⭐⭐ Orta |
| **RAM Kullanımı** | ⚠️ Daha fazla (overhead) | ✅ Daha az |
| **Performans** | ⚠️ Biraz daha düşük | ✅ Daha yüksek |
| **Yönetim** | ✅ Kolay (docker compose) | ⚠️ Manuel |
| **Yedekleme** | ✅ Volume yedekleme | ⚠️ Dosya yedekleme |
| **Versiyon Yönetimi** | ✅ Kolay (image değiştirme) | ⚠️ Zor (yeniden kurulum) |
| **Sanal Sunucu** | ✅ Önerilir | ⚠️ Daha karmaşık |

**Öneri**: 
- **Sanal sunucu** kullanıyorsanız → **Docker** önerilir
- **Fiziksel sunucu** ve **maksimum performans** istiyorsanız → **Native** önerilir

### 8. Docker Troubleshooting

#### Problem: Container başlamıyor

```powershell
# Container log'larını kontrol edin
docker compose logs timescaledb
docker compose logs redis

# Container'ı yeniden başlatın
docker compose restart timescaledb
```

#### Problem: Port zaten kullanımda

```powershell
# Port'u kullanan process'i bulun
netstat -ano | findstr ":5432"
netstat -ano | findstr ":6379"

# Process'i sonlandırın veya docker-compose.yml'de farklı port kullanın
```

#### Problem: Volume mount hatası

```powershell
# Volume'ları kontrol edin
docker volume ls

# Volume'u yeniden oluşturun
docker compose down -v
docker compose up -d
```

#### Problem: Nested virtualization hatası (VM içinde)

Hyper-V içinde Docker kullanıyorsanız:

```powershell
# Nested virtualization'i etkinleştirin
# Hyper-V Manager → VM Settings → Processor → Enable nested virtualization
```

---

## 📁 Proje Kurulumu

### 1. Projeyi Sunucuya Kopyalama

#### Yöntem A: Git Clone

```powershell
# Proje klasörü oluşturun
New-Item -ItemType Directory -Path "C:\DLP_RiskAnalyzer" -Force

# Git repository'den klonlayın
cd C:\DLP_RiskAnalyzer
git clone <repository-url> .

# VEYA proje dosyalarını doğrudan kopyalayın
```

#### Yöntem B: Manuel Kopyalama

1. Proje dosyalarını `C:\DLP_RiskAnalyzer` klasörüne kopyalayın
2. Tüm klasör yapısını koruyun

### 2. NuGet Paketlerini Restore Etme

```powershell
cd "C:\DLP_RiskAnalyzer"
dotnet restore

# Beklenen çıktı:
# Restored DLP.RiskAnalyzer.Shared\DLP.RiskAnalyzer.Shared.csproj
# Restored DLP.RiskAnalyzer.Collector\DLP.RiskAnalyzer.Collector.csproj
# Restored DLP.RiskAnalyzer.Analyzer\DLP.RiskAnalyzer.Analyzer.csproj
```

### 3. Projeyi Build Etme

```powershell
dotnet build -c Release

# Beklenen çıktı: "Build succeeded."
```

### 4. Dashboard NPM Paketlerini Kurma

```powershell
cd "C:\DLP_RiskAnalyzer\dashboard"
npm install

# Beklenen çıktı:
# added XXX packages, and audited XXX packages in XXs
```

---

## ⚙️ Yapılandırma

### 1. Collector Service Yapılandırması

**Dosya**: `C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector\appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "172.16.245.126",
    "ManagerPort": 8443,
    "Username": "your_dlp_username",
    "Password": "your_dlp_password",
    "UseHttps": true,
    "Timeout": 30
  },
  "Redis": {
    "Host": "127.0.0.1",
    "Port": 6379,
    "StreamName": "dlp:incidents"
  },
  "Collector": {
    "IntervalMinutes": 60,
    "LookbackHours": 24,
    "BatchSize": 100
  },
  "Analyzer": {
    "BaseUrl": "http://localhost:5001",
    "InternalSecret": "ChangeThisSecret",
    "ConfigPollIntervalSeconds": 300
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

**Önemli Notlar**:
- `ManagerIP`: Forcepoint DLP Manager IP adresi (ilk kurulumda placeholder olabilir)
- `Username` ve `Password`: UI üzerinden DLP ayarlarını kaydedene kadar geçici olarak bırakabilirsiniz
- `Redis:Host`: Windows Server'da `127.0.0.1` kullanın (localhost yerine)
- `Analyzer.BaseUrl`: Analyzer API’nin URL’i (`http://localhost:5001`)
- `Analyzer.InternalSecret`: Analyzer `appsettings.json` içindeki `InternalApi.SharedSecret` ile birebir aynı olmalı; Collector bu secret olmadan yeni DLP ayarlarını alamaz.

### 2. Analyzer API Yapılandırması

**Dosya**: `C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer\appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5432;Database=dlp_analyzer;Username=postgres;Password=YourPostgreSQLPassword"
  },
  "Redis": {
    "Host": "127.0.0.1",
    "Port": 6379
  },
  "DLP": {
    "ManagerIP": "172.16.245.126",
    "ManagerPort": 8443,
    "Username": "your_dlp_username",
    "Password": "your_dlp_password",
    "UseHttps": true,
    "Timeout": 30
  },
  "Reports": {
    "Directory": "C:\\DLP_RiskAnalyzer\\DLP.RiskAnalyzer.Analyzer\\reports"
  },
  "Authentication": {
    "Username": "admin",
    "Password": "ChangeThisStrongPassword123!"
  },
  "InternalApi": {
    "SharedSecret": "ChangeThisSecret"
  },
  "Email": {
    "SmtpHost": "smtp.company.com",
    "SmtpPort": 587,
    "SmtpUsername": "dlp-notifications@company.com",
    "SmtpPassword": "YourEmailPassword",
    "SmtpEnableSsl": true,
    "FromEmail": "dlp-notifications@company.com",
    "FromName": "DLP Risk Analyzer"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Önemli Notlar**:
- `ConnectionStrings:DefaultConnection`: PostgreSQL şifrenizi güncelleyin
- `Authentication:Password`: Production için güçlü bir şifre belirleyin
- `Reports:Directory`: Mutlak yol kullanın
- `Redis:Host`: `127.0.0.1` kullanın
- `InternalApi:SharedSecret`: Collector servisindeki `Analyzer.InternalSecret` ile birebir aynı güçlü metin olmalı; dashboard/collector bu secret olmadan şifreli DLP bilgilerini çekemez.

### 3. Dashboard Yapılandırması

**Dosya**: `C:\DLP_RiskAnalyzer\dashboard\.env.local` (oluşturun)

```env
NEXT_PUBLIC_API_URL=http://localhost:5001
# VEYA network IP için:
# NEXT_PUBLIC_API_URL=http://192.168.1.100:5001
```

**Not**: Dashboard dinamik olarak API URL'ini algılar, ancak production için sabit bir değer belirleyebilirsiniz.

### 4. DLP API Ayarlarını Dashboard Üzerinden Yapılandırma

Son güncellemeyle Forcepoint DLP API kimlik bilgileri UI üzerinden yönetiliyor:

1. Analyzer ve Collector servislerini başlatın (Collector artık Analyzer’dan ayar alacak).
2. Tarayıcıdan `http://localhost:3002/settings` → “DLP API Configuration” kartını açın.
3. Manager IP/Port, HTTPS tercihi, Timeout, Username ve Password alanlarını doldurun.
4. `Test Connection` ile IP/port/credentials doğrulaması yapın (başarılı olursa latency ve HTTP kodu gösterilir).
5. `Save DLP Settings`:
   - Analyzer tarafında bilgiler `system_settings` tablosuna kaydedilir, şifre Data Protection ile şifrelenir.
   - Analyzer Redis üzerinden yeni ayarları yayınlar.
   - Collector otomatik olarak yeni HttpClient oluşturur; servis restart gerekmez.
6. Şifre maskelenir; gerektiğinde `Reset` diyerek yeniden girebilirsiniz.

> Artık DLP ayarlarını `appsettings.json` içinde saklamanız gerekmiyor. İlk kurulumda placeholder bırakın, gerçek değerleri dashboard’dan kaydedin.

---

## 🔧 Windows Service Kurulumu

### 1. Collector Service Kurulumu

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

### 2. Analyzer API Service Kurulumu (Opsiyonel)

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

# Environment variable ekleyin (port için)
nssm set DLPRiskAnalyzerAPI AppEnvironmentExtra "ASPNETCORE_URLS=http://0.0.0.0:5001"

# Log klasörü oluşturun
New-Item -ItemType Directory -Path "C:\Services\DLPRiskAnalyzerAPI\logs" -Force

# Service'i başlatın
nssm start DLPRiskAnalyzerAPI

# Service durumunu kontrol edin
Get-Service DLPRiskAnalyzerAPI
```

### 3. Dashboard Service Kurulumu (Opsiyonel)

Dashboard'u Windows Service olarak çalıştırmak için PM2 veya NSSM kullanabilirsiniz:

#### Yöntem A: PM2 (Önerilen)

```powershell
# PM2 global kurulum
npm install -g pm2
npm install -g pm2-windows-startup

# Dashboard'u PM2 ile başlatın
cd "C:\DLP_RiskAnalyzer\dashboard"
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
nssm install DLPRiskAnalyzerDashboard "C:\Program Files\nodejs\node.exe" "C:\DLP_RiskAnalyzer\dashboard\node_modules\.bin\next start -p 3002"
nssm set DLPRiskAnalyzerDashboard AppDirectory "C:\DLP_RiskAnalyzer\dashboard"
nssm set DLPRiskAnalyzerDashboard DisplayName "DLP Risk Analyzer Dashboard"
nssm set DLPRiskAnalyzerDashboard Start SERVICE_AUTO_START
nssm start DLPRiskAnalyzerDashboard
```

### 4. Service Yönetimi

```powershell
# Service durumunu kontrol etme
Get-Service DLPRiskAnalyzer*

# Service'i durdurma
Stop-Service DLPRiskAnalyzerCollector

# Service'i başlatma
Start-Service DLPRiskAnalyzerCollector

# Service'i yeniden başlatma
Restart-Service DLPRiskAnalyzerCollector

# Service'i kaldırma
nssm remove DLPRiskAnalyzerCollector confirm
```

---

## 🔥 Firewall Yapılandırması

### 1. Gerekli Portları Açma

```powershell
# Administrator PowerShell'de çalıştırın

# Analyzer API (5001)
New-NetFirewallRule -DisplayName "DLP Risk Analyzer API" `
    -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow

# Dashboard (3002)
New-NetFirewallRule -DisplayName "DLP Risk Analyzer Dashboard" `
    -Direction Inbound -LocalPort 3002 -Protocol TCP -Action Allow

# PostgreSQL (5432) - Sadece localhost için
New-NetFirewallRule -DisplayName "PostgreSQL DLP" `
    -Direction Inbound -LocalPort 5432 -Protocol TCP -Action Allow `
    -RemoteAddress 127.0.0.1

# Redis (6379) - Sadece localhost için
New-NetFirewallRule -DisplayName "Redis DLP" `
    -Direction Inbound -LocalPort 6379 -Protocol TCP -Action Allow `
    -RemoteAddress 127.0.0.1
```

### 2. Belirli IP'lerden Erişim İzni (Production)

```powershell
# Sadece belirli IP'lerden API erişimi
New-NetFirewallRule -DisplayName "DLP Risk Analyzer API - Restricted" `
    -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow `
    -RemoteAddress 192.168.1.0/24,10.0.0.0/8

# Dashboard için de aynı şekilde
New-NetFirewallRule -DisplayName "DLP Risk Analyzer Dashboard - Restricted" `
    -Direction Inbound -LocalPort 3002 -Protocol TCP -Action Allow `
    -RemoteAddress 192.168.1.0/24,10.0.0.0/8
```

### 3. Firewall Kurallarını Kontrol Etme

```powershell
# Tüm DLP kurallarını listele
Get-NetFirewallRule | Where-Object {$_.DisplayName -like "*DLP*"} | Format-Table DisplayName, Enabled, Direction, Action
```

---

## 🌐 IIS Kurulumu (Opsiyonel)

IIS kullanarak Analyzer API'yi host etmek istiyorsanız:

### 1. IIS ve ASP.NET Core Module Kurulumu

```powershell
# IIS özelliklerini etkinleştirin
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer
Enable-WindowsOptionalFeature -Online -FeatureName IIS-CommonHttpFeatures
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpErrors
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpLogging
Enable-WindowsOptionalFeature -Online -FeatureName IIS-RequestFiltering
Enable-WindowsOptionalFeature -Online -FeatureName IIS-StaticContent
Enable-WindowsOptionalFeature -Online -FeatureName IIS-DefaultDocument
Enable-WindowsOptionalFeature -Online -FeatureName IIS-DirectoryBrowsing
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45

# ASP.NET Core Hosting Bundle indirin ve kurun
# https://dotnet.microsoft.com/download/dotnet/8.0
# "Hosting Bundle" indirin ve kurun
```

### 2. IIS Site Oluşturma

```powershell
# Application pool oluşturun
New-WebAppPool -Name "DLPRiskAnalyzerAPI"
Set-ItemProperty IIS:\AppPools\DLPRiskAnalyzerAPI -Name managedRuntimeVersion -Value ""

# Site oluşturun
New-Website -Name "DLPRiskAnalyzerAPI" `
    -Port 5001 `
    -PhysicalPath "C:\Services\DLPRiskAnalyzerAPI" `
    -ApplicationPool "DLPRiskAnalyzerAPI"

# Site'i başlatın
Start-Website -Name "DLPRiskAnalyzerAPI"
```

### 3. web.config Oluşturma

`C:\Services\DLPRiskAnalyzerAPI\web.config` dosyası oluşturun:

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
$computer = $env:COMPUTERNAME

# Local Security Policy'den veya Group Policy'den ayarlayın
# VEYA:
secedit /export /cfg C:\secpol.cfg
# secpol.cfg dosyasını düzenleyin ve tekrar import edin
```

### 3. Service'leri Domain Account ile Çalıştırma

```powershell
# NSSM ile service account ayarlama
nssm set DLPRiskAnalyzerCollector ObjectName "DOMAIN\svc_dlp_riskanalyzer" "YourPassword123!"

# Service'i yeniden başlatın
Restart-Service DLPRiskAnalyzerCollector
```

### 4. Group Policy ile Yapılandırma

Domain ortamında merkezi yönetim için Group Policy kullanabilirsiniz:

- **Firewall Kuralları**: Merkezi firewall yönetimi
- **Service Başlatma**: Otomatik service başlatma
- **Logging**: Merkezi event log yönetimi

---

## 🔒 Güvenlik Ayarları

### 1. appsettings.json Güvenliği

```powershell
# appsettings.json dosyalarını ACL ile koruyun
$acl = Get-Acl "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector\appsettings.json"
$acl.SetAccessRuleProtection($true, $false)
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("DOMAIN\svc_dlp_riskanalyzer", "Read", "Allow")
$acl.AddAccessRule($rule)
Set-Acl "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector\appsettings.json" $acl
```

### 2. Environment Variables Kullanımı (Önerilen)

Sensitive bilgileri environment variables'da saklayın:

```powershell
# System environment variables oluşturun
[System.Environment]::SetEnvironmentVariable("DLP_MANAGER_IP", "172.16.245.126", "Machine")
[System.Environment]::SetEnvironmentVariable("DLP_USERNAME", "your_username", "Machine")
[System.Environment]::SetEnvironmentVariable("DLP_PASSWORD", "your_password", "Machine")
[System.Environment]::SetEnvironmentVariable("POSTGRES_PASSWORD", "YourPostgreSQLPassword", "Machine")
```

Kodda kullanım:

```csharp
// appsettings.json yerine
var managerIP = Environment.GetEnvironmentVariable("DLP_MANAGER_IP") 
    ?? builder.Configuration["DLP:ManagerIP"];
```

### 3. SSL/TLS Yapılandırması

Production için HTTPS kullanın:

```powershell
# Self-signed certificate oluşturma (test için)
$cert = New-SelfSignedCertificate `
    -DnsName "dlp-analyzer.company.com" `
    -CertStoreLocation "cert:\LocalMachine\My" `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -KeyAlgorithm RSA `
    -KeyLength 2048

# Certificate'i export edin ve IIS'e atayın
```

### 4. Windows Defender Exclusion

```powershell
# Antivirus taramasından hariç tutun
Add-MpPreference -ExclusionPath "C:\DLP_RiskAnalyzer"
Add-MpPreference -ExclusionPath "C:\Services\DLPRiskAnalyzer*"
Add-MpPreference -ExclusionProcess "dotnet.exe"
Add-MpPreference -ExclusionProcess "node.exe"
```

---

## 📊 Monitoring ve Logging

### 1. Event Log Yapılandırması

```powershell
# Custom event log oluşturma
New-EventLog -LogName "DLP Risk Analyzer" -Source "DLPRiskAnalyzerCollector"
New-EventLog -LogName "DLP Risk Analyzer" -Source "DLPRiskAnalyzerAPI"
```

### 2. Performance Counters

```powershell
# Performance counter oluşturma
New-Counter -CounterName "\DLP Risk Analyzer\Incidents Collected" -Description "Number of incidents collected"
New-Counter -CounterName "\DLP Risk Analyzer\API Requests" -Description "Number of API requests"
```

### 3. Log Rotation

PowerShell script ile log rotation:

```powershell
# C:\Scripts\Rotate-DLPLogs.ps1
$logPaths = @(
    "C:\Services\DLPRiskAnalyzerCollector\logs",
    "C:\Services\DLPRiskAnalyzerAPI\logs"
)

foreach ($path in $logPaths) {
    $logs = Get-ChildItem -Path $path -Filter "*.log" | Where-Object {
        $_.LastWriteTime -lt (Get-Date).AddDays(-7)
    }
    foreach ($log in $logs) {
        Compress-Archive -Path $log.FullName -DestinationPath "$($log.DirectoryName)\Archive\$($log.BaseName).zip"
        Remove-Item $log.FullName
    }
}
```

Task Scheduler ile otomatik çalıştırma:

```powershell
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" `
    -Argument "-File C:\Scripts\Rotate-DLPLogs.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "Rotate DLP Logs" -Action $action -Trigger $trigger
```

### 4. Health Check Monitoring

```powershell
# Health check script
$apiHealth = Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing
if ($apiHealth.StatusCode -ne 200) {
    Write-EventLog -LogName "Application" -Source "DLP Monitor" `
        -EventId 1001 -EntryType Error -Message "API Health Check Failed"
}
```

---

## 💾 Backup Stratejileri

### 1. PostgreSQL Backup

```powershell
# Backup script: C:\Scripts\Backup-PostgreSQL.ps1
$backupDir = "C:\Backups\PostgreSQL"
$date = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "$backupDir\dlp_analyzer_$date.backup"

New-Item -ItemType Directory -Path $backupDir -Force

$env:PGPASSWORD = "YourPostgreSQLPassword"
pg_dump -U postgres -h localhost -d dlp_analyzer -F c -f $backupFile

# Eski backup'ları sil (30 günden eski)
Get-ChildItem -Path $backupDir -Filter "*.backup" | Where-Object {
    $_.LastWriteTime -lt (Get-Date).AddDays(-30)
} | Remove-Item
```

Task Scheduler ile otomatik backup:

```powershell
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" `
    -Argument "-File C:\Scripts\Backup-PostgreSQL.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 3am
Register-ScheduledTask -TaskName "Backup PostgreSQL DLP" -Action $action -Trigger $trigger
```

### 2. Redis Backup (Memurai)

Memurai otomatik olarak AOF (Append Only File) kullanır. Backup için:

```powershell
# Redis RDB snapshot backup
$backupDir = "C:\Backups\Redis"
New-Item -ItemType Directory -Path $backupDir -Force

# Memurai data directory'yi kopyalayın
Copy-Item "C:\Program Files\Memurai\data\dump.rdb" `
    -Destination "$backupDir\dump_$(Get-Date -Format 'yyyyMMdd_HHmmss').rdb"
```

### 3. Configuration Backup

```powershell
# Yapılandırma dosyalarını yedekleyin
$backupDir = "C:\Backups\Configuration"
New-Item -ItemType Directory -Path $backupDir -Force

$configFiles = @(
    "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector\appsettings.json",
    "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer\appsettings.json",
    "C:\DLP_RiskAnalyzer\dashboard\.env.local"
)

foreach ($file in $configFiles) {
    if (Test-Path $file) {
        Copy-Item $file -Destination "$backupDir\$(Split-Path $file -Leaf)_$(Get-Date -Format 'yyyyMMdd').json"
    }
}
```

---

## 🔧 Troubleshooting

### Problem 1: PostgreSQL Bağlantı Hatası

**Hata**: `Failed to connect to 127.0.0.1:5432`

**Çözüm**:
```powershell
# PostgreSQL servisini kontrol edin
Get-Service postgresql-x64-18

# Servisi başlatın
Start-Service postgresql-x64-18

# Bağlantıyı test edin
$env:PGPASSWORD = "YourPassword"
psql -U postgres -h 127.0.0.1 -d postgres -c "SELECT 1;"
```

### Problem 2: Redis Bağlantı Hatası

**Hata**: `No connection could be made because the target machine actively refused it`

**Çözüm**:
```powershell
# Memurai servisini kontrol edin
Get-Service Memurai

# Servisi başlatın
Start-Service Memurai

# Bağlantıyı test edin
redis-cli ping
```

### Problem 3: Service Başlamıyor

**Hata**: Service durduruluyor veya başlamıyor

**Çözüm**:
```powershell
# Event log'u kontrol edin
Get-EventLog -LogName Application -Source "DLPRiskAnalyzerCollector" -Newest 10

# Service log dosyalarını kontrol edin
Get-Content "C:\Services\DLPRiskAnalyzerCollector\logs\stderr.log" -Tail 50

# Service'i manuel olarak test edin
cd "C:\Services\DLPRiskAnalyzerCollector"
dotnet DLP.RiskAnalyzer.Collector.dll
```

### Problem 4: Port Kullanımda

**Hata**: `Address already in use`

**Çözüm**:
```powershell
# Port'u kullanan process'i bulun
netstat -ano | findstr ":5001"

# Process'i sonlandırın
taskkill /PID <PID_NUMBER> /F

# VEYA farklı port kullanın
# appsettings.json'da veya environment variable'da
```

### Problem 5: SSL Certificate Hatası

**Hata**: `The SSL connection could not be established`

**Çözüm**:
- Bu hata beklenen bir durumdur (self-signed certificate'lar için)
- Kod içinde SSL validation bypass edilmiştir
- Production için DLP Manager'ın SSL certificate'ını güvenilir CA'lere ekleyin

### Problem 6: Migration Hatası

**Hata**: `Failed executing DbCommand`

**Çözüm**:
```powershell
# Database'i sıfırlayın (DİKKAT: Tüm veriler silinir!)
cd "C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer"
dotnet ef database drop --force
dotnet ef database update
```

### Problem 7: Dashboard API Bağlantı Hatası

**Hata**: Dashboard API'ye bağlanamıyor

**Çözüm**:
```powershell
# API'nin çalıştığını kontrol edin
Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing

# CORS ayarlarını kontrol edin (Program.cs)
# Dashboard URL'ini kontrol edin (dashboard/lib/api-config.ts)
```

---

## ✅ Kurulum Doğrulama Checklist

### Önkoşullar
- [ ] Windows Server 2022 kurulu ve güncel
- [ ] .NET 8.0 SDK kurulu (`dotnet --version`)
- [ ] PostgreSQL 18 kurulu ve çalışıyor **VEYA** Docker Desktop kurulu
- [ ] Redis (Memurai) kurulu ve çalışıyor **VEYA** Docker ile Redis çalışıyor
- [ ] Node.js 18+ kurulu (`node --version`)
- [ ] Git kurulu (`git --version`)
- [ ] NSSM kurulu (service kurulumu için)
- [ ] Docker Desktop kurulu (Docker kullanıyorsanız)

### Veritabanı
- [ ] PostgreSQL servisi çalışıyor
- [ ] `dlp_analyzer` database oluşturuldu
- [ ] Entity Framework migrations uygulandı (`dotnet ef database update`)
- [ ] Database bağlantısı test edildi

### Redis
- [ ] Redis (Memurai) servisi çalışıyor
- [ ] Redis bağlantısı test edildi (`redis-cli ping`)
- [ ] AOF persistence etkin

### Proje Kurulumu
- [ ] Proje dosyaları kopyalandı
- [ ] NuGet paketleri restore edildi (`dotnet restore`)
- [ ] Proje build edildi (`dotnet build -c Release`)
- [ ] Dashboard NPM paketleri kuruldu (`npm install`)

### Yapılandırma
- [ ] Collector `appsettings.json` yapılandırıldı
- [ ] Analyzer `appsettings.json` yapılandırıldı
- [ ] Dashboard `.env.local` yapılandırıldı
- [ ] DLP Manager IP, kullanıcı adı ve şifre ayarlandı
- [ ] PostgreSQL connection string güncellendi
- [ ] Redis connection bilgileri güncellendi

### Windows Services
- [ ] Collector service kuruldu ve çalışıyor
- [ ] Analyzer API service kuruldu ve çalışıyor (veya IIS'te)
- [ ] Dashboard service kuruldu ve çalışıyor (opsiyonel)
- [ ] Service'ler otomatik başlatma için yapılandırıldı

### Firewall
- [ ] Port 5001 (API) açıldı
- [ ] Port 3002 (Dashboard) açıldı
- [ ] Port 5432 (PostgreSQL) sadece localhost için açıldı
- [ ] Port 6379 (Redis) sadece localhost için açıldı

### Test ve Doğrulama
- [ ] API health check başarılı (`http://localhost:5001/health`)
- [ ] Swagger UI erişilebilir (`http://localhost:5001/swagger`)
- [ ] Dashboard erişilebilir (`http://localhost:3002`)
- [ ] Collector servisi incident topluyor
- [ ] Redis stream'e veri yazılıyor
- [ ] Analyzer servisi risk skorları hesaplıyor
- [ ] Dashboard'da gerçek veriler görüntüleniyor

### Güvenlik
- [ ] appsettings.json dosyaları ACL ile korundu
- [ ] Service account'lar oluşturuldu ve yapılandırıldı
- [ ] Windows Defender exclusion'ları eklendi
- [ ] Firewall kuralları sadece gerekli IP'lere izin veriyor
- [ ] Production şifreleri değiştirildi

### Backup
- [ ] PostgreSQL backup script'i oluşturuldu
- [ ] Redis backup script'i oluşturuldu
- [ ] Configuration backup script'i oluşturuldu
- [ ] Scheduled task'lar oluşturuldu
- [ ] Backup dizinleri oluşturuldu

### Monitoring
- [ ] Event log yapılandırıldı
- [ ] Log rotation script'i oluşturuldu
- [ ] Health check monitoring yapılandırıldı

---

## 📞 Destek ve Kaynaklar

### Log Dosyaları
- **Collector Logs**: `C:\Services\DLPRiskAnalyzerCollector\logs\`
- **API Logs**: `C:\Services\DLPRiskAnalyzerAPI\logs\`
- **Windows Event Log**: `Get-EventLog -LogName "Application" -Source "DLPRiskAnalyzer*"`

### API Dokümantasyonu
- **Swagger UI**: http://localhost:5001/swagger
- **Health Check**: http://localhost:5001/health

### Yapılandırma Dosyaları
- **Collector**: `C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector\appsettings.json`
- **Analyzer**: `C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer\appsettings.json`
- **Dashboard**: `C:\DLP_RiskAnalyzer\dashboard\.env.local`

### Service Yönetimi
```powershell
# Tüm DLP servislerini kontrol etme
Get-Service DLPRiskAnalyzer*

# Service log'larını görüntüleme
Get-Content "C:\Services\DLPRiskAnalyzerCollector\logs\stdout.log" -Tail 50
```

---

## 🎯 Sonraki Adımlar

1. **Production Hardening**: SSL/TLS sertifikaları, güvenlik duvarı kuralları
2. **High Availability**: Load balancing, failover yapılandırması
3. **Scaling**: Horizontal scaling için yapılandırma
4. **Integration**: SIEM sistemleri ile entegrasyon
5. **Customization**: Kurumsal gereksinimlere göre özelleştirme

---

**Kurulum tamamlandı! 🎉**

Windows Server 2022 üzerinde DLP Risk Analyzer başarıyla kuruldu ve çalışıyor.

