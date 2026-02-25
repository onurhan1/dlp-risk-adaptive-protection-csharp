# C# Windows Native Application - Proje Yapısı

## 📁 Solution Yapısı

```
DLP.RiskAnalyzer.Solution/
├── DLP.RiskAnalyzer.Shared/          # Ortak kütüphane
│   ├── Models/
│   │   ├── Incident.cs
│   │   └── RiskModels.cs
│   └── Services/
│       └── RiskAnalyzer.cs
│
├── DLP.RiskAnalyzer.Collector/       # Windows Service/Console App
│   ├── Services/
│   │   ├── DLPCollectorService.cs
│   │   └── CollectorBackgroundService.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── DLP.RiskAnalyzer.Analyzer/        # ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── IncidentsController.cs
│   │   └── RiskController.cs
│   ├── Data/
│   │   └── AnalyzerDbContext.cs
│   ├── Services/
│   │   ├── DatabaseService.cs
│   │   └── ReportGeneratorService.cs
│   ├── Program.cs
│   └── appsettings.json
│
└── DLP.RiskAnalyzer.Dashboard/      # WPF Desktop Application
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    ├── App.xaml
    ├── App.xaml.cs
    └── appsettings.json
```

## 🔧 Özellikler

### ✅ Collector Service
- Forcepoint DLP API'den veri toplama
- Redis Stream'e yazma
- Background service olarak çalışma
- JWT token caching
- SSL certificate bypass (self-signed certs için)

### ✅ Analyzer API
- ASP.NET Core Web API
- Entity Framework Core (PostgreSQL)
- Redis entegrasyonu
- Swagger/OpenAPI dokümantasyonu
- Risk skorlama ve analiz
- PDF rapor üretimi (QuestPDF)

### ✅ WPF Dashboard
- Modern Material Design UI
- Real-time risk monitoring
- Interactive data grids
- User investigation timeline
- Windows native application

### ✅ Shared Library
- Ortak modeller (Incident, RiskTrends, vb.)
- RiskAnalyzer servisi
- IOB detection
- Policy action recommendations

## 🚀 Çalıştırma

1. **Analyzer API:**
   ```bash
   cd DLP.RiskAnalyzer.Analyzer
   dotnet run
   ```

2. **Collector:**
   ```bash
   cd DLP.RiskAnalyzer.Collector
   dotnet run
   ```

3. **Dashboard:**
   - Visual Studio ile açın
   - F5 ile çalıştırın

## 📦 NuGet Paketleri

Tüm dependency'ler `.csproj` dosyalarında tanımlı. `dotnet restore` ile yüklenir.

