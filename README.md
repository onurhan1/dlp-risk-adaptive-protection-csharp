# Forcepoint Risk Adaptive Protection - C# Implementation

Windows native uygulama olarak geliştirilmiş Forcepoint DLP Risk Analiz ve Raporlama Sistemi.

## 📋 Proje Özeti

Bu proje, Forcepoint DLP API'sinden incident kayıtlarını toplayan, kullanıcı bazında risk skorlaması yapan ve yönetici raporları üreten performanslı bir sistemdir.

### Özellikler

- ✅ **Collector Service**: Forcepoint DLP API'den parallel request ile incident toplama
- ✅ **Analyzer API**: ASP.NET Core Web API ile risk analizi ve hesaplama
- ✅ **WPF Dashboard**: Windows native desktop uygulaması
- ✅ **Web Dashboard**: Next.js ile modern web arayüzü
- ✅ **Redis Stream**: Inter-service communication
- ✅ **TimescaleDB**: Time-series veri depolama
- ✅ **Risk Scoring**: Kullanıcı bazında otomatik risk hesaplama
- ✅ **PDF Reports**: Otomatik rapor üretimi
- ✅ **Anomaly Detection**: Anomali tespit algoritması
- ✅ **Policy Management**: Policy yönetimi ve önerileri

## 🏗️ Mimari

```
┌─────────────────┐
│   Forcepoint    │
│   DLP Manager   │
│   (API Server)  │
└────────┬────────┘
         │
         │ REST API (JWT)
         │
┌────────▼────────┐      ┌──────────────┐      ┌─────────────┐
│    Collector    │─────▶│    Redis     │─────▶│  Analyzer   │
│  (.NET Service) │      │   (Stream)   │      │ (ASP.NET)   │
└─────────────────┘      └──────────────┘      └──────┬──────┘
                                                       │
                                              ┌────────▼────────┐
                                              │  TimescaleDB   │
                                              │  (PostgreSQL)  │
                                              └─────────────────┘
                                                       │
                                              ┌────────▼────────┐
                                              │  Web Dashboard  │
                                              │   (Next.js)     │
                                              └─────────────────┘
```

## 🛠️ Teknolojiler

- **.NET 8.0** - Framework
- **ASP.NET Core Web API** - Backend API
- **WPF (Windows Presentation Foundation)** - Desktop UI
- **Next.js 15** - Web Dashboard
- **Entity Framework Core** - ORM
- **TimescaleDB** - Time-series database
- **Redis** - Message streaming
- **QuestPDF** - PDF generation

## 🚀 Hızlı Başlangıç

### Gereksinimler

- .NET SDK 8.0
- PostgreSQL 14+ (TimescaleDB extension ile)
- Redis 6.0+
- Node.js 18+ (Web Dashboard için)

### Kurulum

```bash
# 1. Dependency'leri kur (Windows)
.\install-windows-dependencies.ps1

# 2. NuGet paketlerini restore et
dotnet restore

# 3. Yapılandırma dosyalarını düzenle
# - DLP.RiskAnalyzer.Collector/appsettings.json
# - DLP.RiskAnalyzer.Analyzer/appsettings.json

# 4. Database migration
cd DLP.RiskAnalyzer.Analyzer
dotnet ef database update

# 5. Servisleri başlat
cd ..
.\start-mac.sh  # Mac için
# veya
.\quick-start.ps1  # Windows için
```

Detaylı kurulum için: [`KURULUM_VE_API_BAGLANTI_REHBERI.md`](KURULUM_VE_API_BAGLANTI_REHBERI.md)

## 📁 Proje Yapısı

```
DLP.RiskAnalyzer.Solution/
├── DLP.RiskAnalyzer.Shared/          # Ortak modeller ve servisler
├── DLP.RiskAnalyzer.Collector/       # DLP API veri toplama servisi
├── DLP.RiskAnalyzer.Analyzer/        # ASP.NET Core Web API
├── DLP.RiskAnalyzer.Dashboard/       # WPF Desktop uygulaması
└── dashboard/                         # Next.js Web Dashboard
```

## 📚 Dokümantasyon

- **[Kurulum ve API Bağlantı Rehberi](KURULUM_VE_API_BAGLANTI_REHBERI.md)** - Detaylı kurulum ve yapılandırma
- **[Windows Kurulum Rehberi](WINDOWS_INSTALLATION.md)** - Windows özel kurulum adımları
- **[Mac Test Rehberi](MAC_TESTING_GUIDE.md)** - Mac ortamında test
- **[Bağımlılıklar](DEPENDENCIES.md)** - Tüm dependency'lerin listesi
- **[Özellik Karşılaştırması](FEATURES_COMPARISON.md)** - Python vs C# versiyonu karşılaştırması

## 🔧 Yapılandırma

### Forcepoint DLP API Bağlantısı

`appsettings.json` dosyalarında:

```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD"
  }
}
```

**⚠️ Önemli**: Hassas bilgileri `.gitignore` ile exclude edilmiştir. Production ortamında environment variables kullanın.

## 🎯 API Endpoints

- **Analyzer API**: `http://localhost:8000`
- **Swagger UI**: `http://localhost:8000/swagger`
- **Web Dashboard**: `http://localhost:3001`

### Ana Endpoint'ler

- `GET /api/incidents` - Incident listesi
- `GET /api/risk/trends` - Risk trendleri
- `GET /api/risk/daily-summary` - Günlük özet
- `GET /api/risk/user-list` - Kullanıcı risk listesi
- `POST /api/reports/generate` - Rapor oluştur
- `GET /api/policies` - Policy listesi

## 🔒 Güvenlik

- ✅ Hassas bilgiler `.gitignore` ile exclude edilmiştir
- ✅ SSL certificate validation bypass (development için)
- ✅ JWT token based authentication
- ✅ Environment variables desteği

## 📊 Özellikler

### Risk Skorlama
- Severity bazlı hesaplama
- Repeat count (tekrar sayısı) faktörü
- Data sensitivity değerlendirmesi
- Kullanıcı bazında risk trendleri

### Raporlama
- Günlük özet raporları
- Departman bazlı analiz
- PDF formatında rapor üretimi
- Risk heatmap görselleştirmesi

### Anomali Tespiti
- Z-Score bazlı anomali algılama
- Kullanıcı bazında baseline hesaplama
- Otomatik uyarılar

## 🤝 Katkıda Bulunma

1. Fork edin
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'Add amazing feature'`)
4. Push edin (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

## 📝 Lisans

Bu proje özel bir projedir. Tüm hakları saklıdır.

## 📞 İletişim

Sorularınız için issue açabilirsiniz.

---

**Not**: Bu proje Forcepoint DLP API'sini kullanmak için geçerli lisans ve API erişim hakları gerektirir.
