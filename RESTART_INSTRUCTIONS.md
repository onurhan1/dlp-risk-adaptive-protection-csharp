# Tüm Sistem Yeniden Başlatma Rehberi

## 🚀 Hızlı Başlatma

### macOS/Linux

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp"
./restart-all.sh
```

Bu script otomatik olarak:
1. ✅ Tüm çalışan servisleri durdurur
2. ✅ Docker container'ları (PostgreSQL, Redis) yeniden başlatır
3. ✅ Analyzer API'yi başlatır (port 5001)
4. ✅ Collector Service'i başlatır
5. ✅ Dashboard'ı başlatır (port 3002)

---

## 📋 Manuel Başlatma (Adım Adım)

### 1. Mevcut Servisleri Durdur

```bash
# Port 5001 (API)
lsof -ti :5001 | xargs kill -9

# Port 3002 (Dashboard)
lsof -ti :3002 | xargs kill -9

# Docker containers
docker-compose down
```

### 2. Docker Container'ları Başlat

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp"
docker-compose up -d
```

**Kontrol**:
```bash
docker ps
# dlp-timescaledb ve dlp-redis çalışıyor olmalı
```

### 3. Analyzer API'yi Başlat

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp/DLP.RiskAnalyzer.Analyzer"
dotnet run
```

**Kontrol**: Console'da şunu görmelisiniz:
```
INFO: API configured to listen on 0.0.0.0:5001 for network access
API is listening on:
  - http://0.0.0.0:5001
```

### 4. Collector Service'i Başlat

Yeni bir terminal penceresi açın:
```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp/DLP.RiskAnalyzer.Collector"
dotnet run
```

### 5. Dashboard'ı Başlat

Yeni bir terminal penceresi açın:
```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp/dashboard"
npm start
```

**Kontrol**: Console'da şunu görmelisiniz:
```
- Local:        http://localhost:3002
- Network:      http://0.0.0.0:3002
```

---

## ✅ Servis Kontrolü

### Health Check

```bash
# API Health
curl http://localhost:5001/health

# Dashboard (tarayıcıdan)
open http://localhost:3002
```

### Port Kontrolü

```bash
# Hangi portlar kullanılıyor?
lsof -i :5001  # API
lsof -i :3002  # Dashboard
lsof -i :5432  # PostgreSQL
lsof -i :6379  # Redis
```

### Docker Container Kontrolü

```bash
docker ps
# Şunları görmelisiniz:
# - dlp-timescaledb (port 5432)
# - dlp-redis (port 6379)
```

---

## 🔧 Sorun Giderme

### Sorun: Port zaten kullanılıyor

```bash
# Port'u kullanan process'i bul ve durdur
lsof -ti :5001 | xargs kill -9
lsof -ti :3002 | xargs kill -9
```

### Sorun: Docker container'lar başlamıyor

```bash
# Container'ları kontrol et
docker ps -a

# Logları kontrol et
docker logs dlp-timescaledb
docker logs dlp-redis

# Yeniden başlat
docker-compose restart
```

### Sorun: API başlamıyor

```bash
# Database bağlantısını kontrol et
cd DLP.RiskAnalyzer.Analyzer
dotnet run

# Hata mesajlarını kontrol et
# PostgreSQL'in çalıştığından emin olun
```

### Sorun: Dashboard başlamıyor

```bash
cd dashboard

# Dependencies'leri kontrol et
npm install

# Build'i kontrol et
npm run build

# Sonra başlat
npm start
```

---

## 📊 Servis Durumu Kontrol Script'i

```bash
./check-services-mac.sh
```

Bu script tüm servislerin durumunu kontrol eder.

---

## 🌐 Network Erişimi

Servisler başladıktan sonra:

1. **Sunucu IP'sini öğrenin**:
   ```bash
   ifconfig | grep "inet " | grep -v 127.0.0.1
   ```

2. **Başka bir cihazdan erişin**:
   - Dashboard: `http://[SUNUCU_IP]:3002`
   - API: `http://[SUNUCU_IP]:5001/health`

---

## ⚠️ Önemli Notlar

1. **Sıralama Önemli**: 
   - Önce Docker (PostgreSQL, Redis)
   - Sonra API
   - Sonra Collector
   - En son Dashboard

2. **Bekleme Süreleri**:
   - PostgreSQL: ~5-10 saniye
   - Redis: ~2-3 saniye
   - API: ~3-5 saniye
   - Dashboard: ~5-10 saniye

3. **Terminal Pencereleri**:
   - Her servis ayrı terminal penceresinde çalışır
   - Logları görmek için terminal pencerelerini açık tutun

---

**Son Güncelleme**: 2025-01-XX

