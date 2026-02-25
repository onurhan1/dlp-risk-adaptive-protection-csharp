# Windows Hızlı Başlangıç Rehberi - C# Versiyonu

## 🚀 5 Dakikada Başlangıç

### 1. Gereksinimleri Kurun

#### .NET 8.0 SDK
```powershell
# PowerShell'i Administrator olarak açın
winget install Microsoft.DotNet.SDK.8
```

#### PostgreSQL (Docker ile - Kolay)
```powershell
# Docker Desktop kurulumu: https://www.docker.com/products/docker-desktop/
docker run -d --name timescaledb -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=dlp_analytics -p 5432:5432 timescale/timescaledb:latest-pg16
```

#### Redis (Docker ile - Kolay)
```powershell
docker run -d --name redis -p 6379:6379 redis:7-alpine
```

### 2. Projeyi Hazırlayın

```powershell
# Proje klasörüne gidin
cd "C:\Projects\Risk Adaptive Protection CSharp"

# NuGet paketlerini restore edin
dotnet restore

# Projeyi build edin
dotnet build
```

### 3. Database'i Hazırlayın

```powershell
# Entity Framework Tools kurun (ilk kez)
dotnet tool install --global dotnet-ef --version 8.0.0

# Analyzer projesine gidin
cd DLP.RiskAnalyzer.Analyzer

# Database migration çalıştırın
dotnet ef database update

cd ..
```

### 4. Yapılandırma Dosyalarını Düzenleyin

#### `DLP.RiskAnalyzer.Collector\appsettings.json`
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
  }
}
```

#### `DLP.RiskAnalyzer.Analyzer\appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analytics;Username=postgres;Password=postgres"
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD"
  }
}
```

**⚠️ ÖNEMLİ**: `YOUR_DLP_MANAGER_IP`, `YOUR_DLP_USERNAME`, `YOUR_DLP_PASSWORD` placeholder'larını gerçek Forcepoint DLP bilgilerinizle değiştirin!

### 5. Servisleri Başlatın

#### Otomatik (Önerilen):
```powershell
.\quick-start.ps1
```

#### Manuel:
```powershell
# Terminal 1: Analyzer API
cd DLP.RiskAnalyzer.Analyzer
dotnet run

# Terminal 2: Collector
cd DLP.RiskAnalyzer.Collector
dotnet run

# Terminal 3: Dashboard
cd DLP.RiskAnalyzer.Dashboard
dotnet run
```

### 6. Doğrulayın

1. **API Health Check**: http://localhost:8000/health
2. **Swagger UI**: http://localhost:8000/swagger
3. **Dashboard**: WPF penceresi açıldı mı?

---

## ✅ Servis Kontrolü

```powershell
.\check-services.ps1
```

---

## 🔧 Sorun Giderme

### Problem: "dotnet: command not found"
**Çözüm**: .NET SDK'yı PATH'e ekleyin veya yeniden kurun

### Problem: PostgreSQL bağlantı hatası
**Çözüm**: Docker container'ının çalıştığını kontrol edin
```powershell
docker ps | Select-String timescaledb
```

### Problem: Port 8000 kullanımda
**Çözüm**: Port'u kullanan process'i sonlandırın
```powershell
netstat -ano | Select-String "8000"
taskkill /PID <PID> /F
```

---

## 📚 Detaylı Dokümantasyon

Tam kurulum rehberi için: `WINDOWS_INSTALLATION.md`

---

**Başarılar! 🎉**

