# Bağımlılıklar (Dependencies) - Windows

Bu dosya, Forcepoint Risk Adaptive Protection sisteminin Windows ortamında çalışması için gerekli tüm bağımlılıkları listeler.

## 📦 Hızlı Kurulum

Otomatik kurulum için PowerShell script'ini çalıştırın:

```powershell
.\install-windows-dependencies.ps1
```

Manuel kurulum için aşağıdaki detayları takip edin.

---

## 1. .NET SDK 8.0

### Gereksinim
- **Versiyon**: 8.0.x veya üzeri
- **Zorunlu**: Evet

### Kurulum

**Winget ile (Önerilen)**:
```powershell
winget install Microsoft.DotNet.SDK.8
```

**Manuel**:
- [.NET SDK 8.0 İndirme Sayfası](https://dotnet.microsoft.com/download/dotnet/8.0)
- x64 installer'ı indirin ve kurun

**Doğrulama**:
```powershell
dotnet --version
# Beklenen: 8.0.xxx
```

---

## 2. PostgreSQL + TimescaleDB

### Gereksinim
- **PostgreSQL**: 14.0+ veya 16.0+
- **TimescaleDB Extension**: latest-pg14 veya latest-pg16
- **Zorunlu**: Evet

### Kurulum Seçenekleri

#### Seçenek A: Docker (Önerilen)

```powershell
docker run -d `
  --name timescaledb `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=dlp_risk_analyzer `
  -e POSTGRES_USER=dlp_user `
  -e TZ=Europe/Istanbul `
  -p 5432:5432 `
  timescale/timescaledb:latest-pg16
```

#### Seçenek B: Manuel Kurulum

1. [PostgreSQL İndirme Sayfası](https://www.postgresql.org/download/windows/)
2. PostgreSQL'i kurun
3. [TimescaleDB Extension](https://docs.timescale.com/install/latest/self-hosted/installation-windows/) kurun

**Doğrulama**:
```powershell
# Docker için
docker ps | Select-String timescaledb

# Yerel için
Get-Service -Name postgresql*
```

---

## 3. Redis Server

### Gereksinim
- **Versiyon**: 6.0+ veya 7.0+
- **Zorunlu**: Evet

### Kurulum Seçenekleri

#### Seçenek A: Docker (Önerilen)

```powershell
docker run -d `
  --name redis `
  -p 6379:6379 `
  redis:7-alpine
```

#### Seçenek B: Memurai (Windows Native)

1. [Memurai İndirme Sayfası](https://www.memurai.com/get-memurai)
2. Memurai'yi kurun (Redis Windows uyumlu)

**Doğrulama**:
```powershell
# Docker için
docker ps | Select-String redis

# Memurai için
Get-Service -Name Memurai*

# Test
redis-cli ping
# Beklenen: PONG
```

---

## 4. Node.js ve npm

### Gereksinim
- **Node.js**: 18.0+ veya üzeri
- **npm**: 9.0+ (Node.js ile birlikte gelir)
- **Zorunlu**: Evet (Web Dashboard için)

### Kurulum

**Winget ile**:
```powershell
winget install OpenJS.NodeJS.LTS
```

**Manuel**:
- [Node.js İndirme Sayfası](https://nodejs.org/)
- LTS versiyonunu indirin ve kurun

**Doğrulama**:
```powershell
node --version  # v18.x.x veya üzeri
npm --version   # 9.x.x veya üzeri
```

---

## 5. NuGet Paketleri

Tüm NuGet paketleri `.csproj` dosyalarında tanımlıdır ve otomatik olarak restore edilir.

### Otomatik Restore

```powershell
dotnet restore DLP.RiskAnalyzer.Solution.sln
```

### Ana Paketler

#### Collector Service
- `StackExchange.Redis` >= 2.7.0
- `Newtonsoft.Json` >= 13.0.3
- `Microsoft.Extensions.Http` >= 8.0.0
- `Microsoft.Extensions.Hosting` >= 8.0.0

#### Analyzer Service
- `Microsoft.EntityFrameworkCore` >= 8.0.0
- `Npgsql.EntityFrameworkCore.PostgreSQL` >= 8.0.0
- `StackExchange.Redis` >= 2.7.0
- `Swashbuckle.AspNetCore` >= 6.5.0
- `QuestPDF` >= 2024.3.10

#### Dashboard (WPF)
- `MaterialDesignThemes` >= 4.9.0
- `CommunityToolkit.Mvvm` >= 8.2.2
- `Microsoft.Extensions.Hosting` >= 8.0.0

---

## 6. NPM Paketleri (Web Dashboard)

Dashboard için gerekli paketler `dashboard/package.json` dosyasında tanımlıdır.

### Kurulum

```powershell
cd dashboard
npm install
```

### Ana Paketler
- `next` >= 15.0.0
- `react` >= 18.2.0
- `axios` >= 1.6.2
- `plotly.js` >= 2.27.0
- `tailwindcss` >= 3.4.18

---

## 7. Opsiyonel Araçlar

### Visual Studio 2022
- [İndirme Sayfası](https://visualstudio.microsoft.com/downloads/)
- Community Edition (ücretsiz) yeterli
- İş yükleri: .NET desktop development, ASP.NET and web development

### Git for Windows
- [İndirme Sayfası](https://git-scm.com/download/win)
- Proje klonlama için gerekli

### Chocolatey (Paket Yöneticisi)
- [İndirme Sayfası](https://chocolatey.org/install)
- Opsiyonel ama kullanışlı

### NSSM (Windows Service Manager)
- Collector'ı Windows Service olarak kurmak için
- [İndirme Sayfası](https://nssm.cc/download)

---

## 8. Network Gereksinimleri

### Açık Portlar
- **8000**: Analyzer API (HTTP)
- **3001**: Web Dashboard (Next.js)
- **5432**: PostgreSQL
- **6379**: Redis
- **8443**: Forcepoint DLP API (HTTPS - gelen değil)

### Firewall Ayarları
Windows Firewall'da yukarıdaki portların açık olduğundan emin olun.

---

## 9. Sistem Gereksinimleri

### Minimum
- **RAM**: 8 GB
- **Disk**: 50 GB boş alan
- **CPU**: 4 çekirdek
- **OS**: Windows 10 (1809+) / Windows 11

### Önerilen
- **RAM**: 16 GB
- **Disk**: 100 GB boş alan (SSD)
- **CPU**: 8 çekirdek
- **OS**: Windows 11 / Windows Server 2022

---

## 10. Yapılandırma Dosyaları

Kurulumdan sonra aşağıdaki dosyaları yapılandırmanız gerekir:

1. **Collector**: `DLP.RiskAnalyzer.Collector/appsettings.json`
   - Forcepoint DLP API bilgileri
   - Redis bağlantı bilgileri

2. **Analyzer**: `DLP.RiskAnalyzer.Analyzer/appsettings.json`
   - PostgreSQL bağlantı string'i
   - Forcepoint DLP API bilgileri
   - Redis bağlantı bilgileri

3. **Dashboard**: `dashboard/.env.local`
   - Analyzer API URL'i

Detaylı yapılandırma için `KURULUM_VE_API_BAGLANTI_REHBERI.md` dosyasına bakın.

---

## ✅ Kurulum Doğrulama Checklist

Kurulum sonrası kontrol edin:

- [ ] `.NET SDK 8.0` kurulu (`dotnet --version`)
- [ ] `PostgreSQL` çalışıyor (Docker veya Service)
- [ ] `TimescaleDB extension` etkinleştirildi
- [ ] `Redis` çalışıyor (Docker veya Memurai)
- [ ] `Node.js 18+` kurulu (`node --version`)
- [ ] `npm` kurulu (`npm --version`)
- [ ] NuGet paketleri restore edildi (`dotnet restore`)
- [ ] NPM paketleri kuruldu (`npm install` - dashboard klasöründe)
- [ ] Yapılandırma dosyaları düzenlendi
- [ ] Database migration'lar çalıştırıldı (`dotnet ef database update`)

---

## 🐛 Sorun Giderme

### "dotnet: command not found"
- .NET SDK PATH'e eklenmemiş olabilir
- Yeni bir PowerShell penceresi açın
- PATH'i kontrol edin: `$env:PATH -split ';' | Select-String "dotnet"`

### PostgreSQL bağlantı hatası
- Servis çalışıyor mu? `Get-Service -Name postgresql*`
- Port 5432 açık mı? `netstat -an | Select-String "5432"`
- Connection string doğru mu? `appsettings.json`

### Redis bağlantı hatası
- Docker container çalışıyor mu? `docker ps | Select-String redis`
- Memurai servisi çalışıyor mu? `Get-Service -Name Memurai*`
- Port 6379 açık mı? `netstat -an | Select-String "6379"`

### NuGet restore hatası
- İnternet bağlantısı var mı?
- Corporate proxy varsa ayarlanmalı
- `dotnet nuget locals all --clear` komutuyla cache temizleyin

---

## 📚 İlgili Dokümanlar

- `KURULUM_VE_API_BAGLANTI_REHBERI.md`: Detaylı kurulum rehberi
- `WINDOWS_INSTALLATION.md`: Windows kurulum adımları
- `CONFIGURATION_NOTES.md`: Yapılandırma notları
- `requirements-windows.txt`: Text formatında dependency listesi

---

**Son Güncelleme**: 2024-11-03

