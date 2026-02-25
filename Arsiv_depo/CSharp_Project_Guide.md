# C# Windows Native Application - Kurulum Rehberi

## 📋 Proje Yapısı

```
DLP.RiskAnalyzer.Solution/
├── DLP.RiskAnalyzer.Shared/          # Ortak modeller
├── DLP.RiskAnalyzer.Collector/       # Windows Service/Console
├── DLP.RiskAnalyzer.Analyzer/        # ASP.NET Core Web API
└── DLP.RiskAnalyzer.Dashboard/       # WPF Desktop Application
```

## 🚀 Kurulum

### 1. Gereksinimler
- .NET 8.0 SDK
- Visual Studio 2022 veya Rider
- PostgreSQL/TimescaleDB
- Redis Server

### 2. Solution'ı Restore Et
```bash
cd "Risk Adaptive Protection CSharp"
dotnet restore
```

### 3. Database Migration
```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Configuration
- `DLP.RiskAnalyzer.Collector/appsettings.json` - DLP API credentials
- `DLP.RiskAnalyzer.Analyzer/appsettings.json` - Database connection
- `DLP.RiskAnalyzer.Dashboard/appsettings.json` - API URL

### 5. Çalıştır
```bash
# Terminal 1: Analyzer API
cd DLP.RiskAnalyzer.Analyzer
dotnet run

# Terminal 2: Collector
cd DLP.RiskAnalyzer.Collector
dotnet run

# Terminal 3: Dashboard (Visual Studio ile aç)
```

## 📦 NuGet Paketleri

Tüm paketler proje dosyalarında tanımlı. `dotnet restore` ile yüklenir.

## 🎨 WPF Dashboard Özellikleri

- Modern Material Design UI
- Real-time risk monitoring
- Interactive charts
- User investigation timeline
- Alert details view

## 🔧 Development

1. Visual Studio'da Solution'ı açın
2. Startup projects ayarlayın:
   - Analyzer (Web API)
   - Dashboard (WPF)
3. Collector'ı arka planda çalıştırın veya Windows Service olarak kurun

