# Forcepoint DLP Risk Adaptive Protection - C# Windows Native Application

## 📋 Proje Yapısı

Bu proje, Python/Go/Next.js versiyonunun C# ile Windows native uygulama olarak yeniden implementasyonudur.

### Solution Yapısı

```
DLP.RiskAnalyzer.Solution/
├── DLP.RiskAnalyzer.Shared/          # Ortak modeller ve servisler
├── DLP.RiskAnalyzer.Collector/       # Windows Service - DLP API veri toplama
├── DLP.RiskAnalyzer.Analyzer/        # ASP.NET Core Web API - Risk analizi
└── DLP.RiskAnalyzer.Dashboard/       # WPF Application - Windows native UI
```

## 🛠️ Teknolojiler

- **.NET 8.0** - Framework
- **WPF (Windows Presentation Foundation)** - Desktop UI
- **ASP.NET Core Web API** - Backend API
- **Entity Framework Core** - ORM (PostgreSQL/TimescaleDB)
- **StackExchange.Redis** - Redis client
- **HttpClient** - Forcepoint DLP API bağlantısı
- **QuestPDF** veya **iTextSharp** - PDF generation
- **LiveCharts** veya **OxyPlot** - Chart visualization

## 📦 NuGet Paketleri

### Shared Library
- `System.Text.Json` - JSON serialization
- `Microsoft.Extensions.Configuration` - Configuration

### Collector
- `StackExchange.Redis` - Redis client
- `Newtonsoft.Json` - JSON handling
- `System.Net.Http` - HTTP client

### Analyzer (Web API)
- `Microsoft.EntityFrameworkCore` - EF Core
- `Npgsql.EntityFrameworkCore.PostgreSQL` - PostgreSQL provider
- `StackExchange.Redis` - Redis client
- `Swashbuckle.AspNetCore` - Swagger UI
- `QuestPDF` - PDF generation

### Dashboard (WPF)
- `Microsoft.Extensions.Hosting` - Application hosting
- `CommunityToolkit.Mvvm` - MVVM pattern
- `LiveCharts.Wpf` - Charts
- `MaterialDesignThemes` - Modern UI (opsiyonel)

## 🚀 Kurulum

### Gereksinimler
- .NET 8.0 SDK
- Visual Studio 2022 veya Visual Studio Code
- PostgreSQL/TimescaleDB
- Redis Server
- Windows 10/11

### Adımlar

1. **Solution'ı Aç**
   ```bash
   cd "Risk Adaptive Protection CSharp"
   dotnet restore
   ```

2. **appsettings.json Yapılandır**
   - `appsettings.json` dosyalarını düzenle
   - Forcepoint DLP API credentials'ları ekle
   - Database connection string'leri ayarla

3. **Database Migration**
   ```bash
   cd DLP.RiskAnalyzer.Analyzer
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

4. **Çalıştır**
   - Analyzer: `dotnet run --project DLP.RiskAnalyzer.Analyzer`
   - Dashboard: `dotnet run --project DLP.RiskAnalyzer.Dashboard`

## 🔧 Özellikler

- ✅ Windows Native WPF UI
- ✅ ASP.NET Core Web API
- ✅ Entity Framework Core ile database
- ✅ Redis Stream desteği
- ✅ Risk skorlama algoritması
- ✅ PDF rapor üretimi
- ✅ Real-time dashboard
- ✅ UTC+3 timezone desteği

## 📚 Dokümantasyon

Detaylı dokümantasyon için:
- `docs/` klasörüne bakın
- API dokümantasyonu: `http://localhost:8000/swagger`

