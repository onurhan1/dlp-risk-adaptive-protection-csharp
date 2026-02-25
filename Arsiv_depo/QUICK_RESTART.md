# Hızlı Sistem Yeniden Başlatma

## ⚡ Tek Komutla Başlatma

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp"
./restart-all.sh
```

---

## 🔧 Önkoşullar

### 1. Docker Desktop Çalışıyor Olmalı

**macOS**:
```bash
# Docker Desktop'ı açın (Applications klasöründen)
# Veya terminal'den:
open -a Docker
```

**Kontrol**:
```bash
docker info
# Hata yoksa Docker çalışıyor demektir
```

### 2. .NET SDK Kurulu Olmalı

```bash
dotnet --version
# 8.0 veya üzeri olmalı
```

### 3. Node.js Kurulu Olmalı

```bash
node --version
# 18.x veya üzeri olmalı
```

---

## 📋 Manuel Başlatma (Docker Olmadan)

Eğer Docker kullanmıyorsanız ve PostgreSQL/Redis zaten çalışıyorsa:

### 1. Servisleri Durdur

```bash
# Port 5001 (API)
lsof -ti :5001 | xargs kill -9 2>/dev/null

# Port 3002 (Dashboard)
lsof -ti :3002 | xargs kill -9 2>/dev/null
```

### 2. API'yi Başlat

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp/DLP.RiskAnalyzer.Analyzer"
dotnet run
```

### 3. Collector'ı Başlat (Yeni Terminal)

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp/DLP.RiskAnalyzer.Collector"
dotnet run
```

### 4. Dashboard'ı Başlat (Yeni Terminal)

```bash
cd "/Users/onurhany/Desktop/DLP_Adaptive Protection CSharp/dashboard"
npm start
```

---

## ✅ Servis Durumu Kontrolü

```bash
# Tüm servisleri kontrol et
./check-services-mac.sh

# Veya manuel kontrol
curl http://localhost:5001/health  # API
curl http://localhost:3002         # Dashboard
docker ps                           # PostgreSQL, Redis
```

---

## 🆘 Sorun Giderme

### Docker Çalışmıyor

```bash
# Docker Desktop'ı başlat
open -a Docker

# Bekleyin (30-60 saniye)
# Sonra kontrol edin
docker info
```

### Port Zaten Kullanılıyor

```bash
# Hangi process port'u kullanıyor?
lsof -i :5001
lsof -i :3002

# Process'i durdur
lsof -ti :5001 | xargs kill -9
lsof -ti :3002 | xargs kill -9
```

### API Başlamıyor

```bash
# Database bağlantısını kontrol et
cd DLP.RiskAnalyzer.Analyzer
dotnet run

# Hata mesajlarını okuyun
# Genellikle PostgreSQL bağlantı hatası olur
```

---

**Not**: `restart-all.sh` script'i tüm servisleri otomatik olarak başlatır. Docker çalışmıyorsa önce Docker'ı başlatmanız gerekir.

