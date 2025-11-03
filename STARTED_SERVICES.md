# 🚀 Başlatılan Servisler

## ✅ Çalışan Servisler

### 1. Docker Container'lar
- ✅ **TimescaleDB**: Port 5432 (healthy)
- ✅ **Redis**: Port 6379 (healthy)

### 2. .NET Servisleri
- ✅ **Analyzer API**: http://localhost:8000
- ⚠️ **Collector**: Kontrol ediliyor...

## 📍 Erişim URL'leri

- **Analyzer API**: http://localhost:8000
- **Swagger UI**: http://localhost:8000/swagger
- **Health Check**: http://localhost:8000/health

## 🧪 Test Komutları

```bash
# Health check
curl http://localhost:8000/health

# Incidents listesi
curl http://localhost:8000/api/incidents

# Risk trends
curl http://localhost:8000/api/risk/trends

# Swagger UI aç
open http://localhost:8000/swagger
```

## ⚠️ Önemli Notlar

- **WPF Dashboard** Mac'te çalışmaz (Windows only)
- **Swagger UI** ile tüm API endpoint'lerini test edebilirsiniz
- Collector servisi appsettings.json'da DLP bilgileri gerektirir

## 📝 Yapılandırma Gerekli

`appsettings.json` dosyalarına Forcepoint DLP bilgilerini girin:
- `DLP.RiskAnalyzer.Collector/appsettings.json`
- `DLP.RiskAnalyzer.Analyzer/appsettings.json`

