# Production Deployment Checklist - Windows Server 2025

## ✅ Pre-Deployment Kontrolleri

### 1. Offline Bağımlılık Kontrolü

- [x] **Google Fonts kaldırıldı** - Dashboard offline çalışıyor
- [x] **Sistem fontları kullanılıyor** - Windows Server 2025 (Segoe UI)
- [x] **node_modules dahil** - Zip'te mevcut
- [x] **OpenAI/Azure OpenAI opsiyonel** - Local model varsayılan
- [x] **Splunk opsiyonel** - Zorunlu değil
- [x] **DLP API internal network'te** - Internet gerektirmez

### 2. DLP API Bağlantısı

- [ ] **DLP Manager IP doğru** - Dashboard Settings sayfasından ayarlanmış
- [ ] **DLP Manager Port doğru** - Dashboard Settings sayfasından ayarlanmış (genellikle 8443)
- [ ] **DLP Username/Password doğru** - Dashboard Settings sayfasından ayarlanmış
- [ ] **Network erişimi test edildi** - DLP Manager'a ping/curl ile
- [ ] **SSL sertifikası bypass** - Self-signed cert için kod'da var

**ÖNEMLİ:** DLP API ayarları **Dashboard Settings sayfasından** yapılmalı. `appsettings.json`'daki placeholder değerler kullanılmaz. Settings sayfasından ayar yapılmadan Collector çalışmayacaktır.

### 3. Veritabanı Hazırlığı

- [ ] **PostgreSQL kurulu** - Windows Server 2025'te
- [ ] **Veritabanı oluşturuldu** - `CREATE DATABASE dlp_analyzer;`
- [ ] **Kullanıcı oluşturuldu** - `postgres` veya özel kullanıcı
- [ ] **Şifre ayarlandı** - `appsettings.json`'da
- [ ] **Migration otomatik** - Uygulama başlarken çalışacak

### 4. Redis Hazırlığı

- [ ] **Redis kurulu** - Windows Server 2025'te
- [ ] **Redis çalışıyor** - Port 6379
- [ ] **Bağlantı test edildi** - `redis-cli ping`

### 5. Konfigürasyon Dosyaları

#### DLP.RiskAnalyzer.Analyzer/appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analyzer;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

**ÖNEMLİ:** DLP API ayarları `appsettings.json`'da **YAPILMAMALI**. Tüm DLP ayarları Dashboard Settings sayfasından yapılmalı. `appsettings.json`'daki placeholder değerler kullanılmaz.

#### DLP.RiskAnalyzer.Collector/appsettings.json
```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "UseHttps": true,
    "Timeout": 30
  }
}
```

**ÖNEMLİ:** Collector `appsettings.json`'daki değerleri sadece başlangıçta kullanır. Gerçek ayarlar Dashboard Settings sayfasından yapılmalı ve Analyzer API üzerinden Collector'a aktarılır.

## 🚀 Deployment Adımları

### 1. Dosyaları Aktarın

```powershell
# Zip dosyasını açın
Expand-Archive -Path "DLP_RiskAnalyzer_*.zip" -DestinationPath "C:\DLP_RiskAnalyzer" -Force
```

### 2. Veritabanı Kurulumu

```sql
-- PostgreSQL'de
CREATE DATABASE dlp_analyzer;
CREATE USER postgres WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE dlp_analyzer TO postgres;
```

### 3. Konfigürasyon

```powershell
# appsettings.json dosyalarını düzenleyin
# - DLP Manager IP/Port/Username/Password
# - PostgreSQL connection string
# - Redis host/port
```

### 4. Build ve Test

```powershell
# Analyzer API
cd C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer
dotnet build
dotnet run  # Test için

# Collector Service
cd C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Collector
dotnet build
dotnet run  # Test için

# Dashboard
cd C:\DLP_RiskAnalyzer\dashboard
npm run build
npm start  # Test için
```

### 5. Servis Olarak Kurulum

Detaylı kurulum için `WINDOWS_SERVER_2025_KURULUM_REHBERI.md` dosyasına bakın.

## 🔍 Kritik Testler

### 1. DLP API Bağlantı Testi

```powershell
# API başladıktan sonra
curl http://localhost:5001/api/dlp/test/connection
```

**Beklenen:** `"success": true` ve DLP API'ye bağlanabildiğini gösterir.

### 2. Veri Akışı Testi

1. **Collector çalışıyor mu?**
   - Log'larda "DLP Collector Service started" görünmeli
   - Her saatte bir "Starting incident collection" görünmeli

2. **DLP API'den veri geliyor mu?**
   - Log'larda "Fetched X incidents" görünmeli
   - Eğer 0 incident varsa, DLP Manager'da incident olup olmadığını kontrol edin

3. **Redis'e veri yazılıyor mu?**
   - Log'larda "Successfully collected and pushed X incidents to Redis" görünmeli

4. **Analyzer veriyi işliyor mu?**
   - Log'larda "Processed X incidents from Redis stream" görünmeli

5. **Dashboard'da veri görünüyor mu?**
   - http://localhost:3002 adresinde incident'ler görünmeli

### 3. Hata Senaryoları Testi

#### DLP API Bağlantısı Kesilirse
- ✅ Collector servisi durmamalı
- ✅ Log'larda warning görünmeli
- ✅ Sonraki interval'de tekrar denemeli

#### Veritabanı Bağlantısı Kesilirse
- ✅ Analyzer servisi durmamalı
- ✅ Retry mekanizması çalışmalı
- ✅ Log'larda hata görünmeli

#### Redis Bağlantısı Kesilirse
- ✅ Collector servisi durmamalı
- ✅ Retry mekanizması çalışmalı
- ✅ Log'larda hata görünmeli

## ⚠️ Kritik Notlar

### DLP API Bağlantısı

**ÖNEMLİ:** DLP API bağlantısı başarısız olsa bile:
- ✅ Collector servisi çalışmaya devam eder
- ✅ Her interval'de tekrar dener
- ✅ Log'larda warning görünür (hata değil)
- ✅ Servis crash olmaz

**Veri Gelmezse:**
1. DLP Manager IP/Port doğru mu kontrol edin
2. DLP Manager'a network erişimi var mı test edin
3. DLP Username/Password doğru mu kontrol edin
4. DLP Manager'da incident var mı kontrol edin
5. DLP API endpoint'leri erişilebilir mi test edin

### Offline Çalışma

**Tüm bağımlılıklar offline:**
- ✅ Dashboard - Sistem fontları, node_modules dahil
- ✅ Analyzer API - Sadece internal network (DLP API, PostgreSQL, Redis)
- ✅ Collector - Sadece internal network (DLP API, Redis)
- ✅ OpenAI/Azure - Opsiyonel (local model varsayılan)
- ✅ Splunk - Opsiyonel

**Internet gerektiren durumlar:**
- ❌ Yok - Tüm servisler offline çalışır

## 📋 Post-Deployment Kontrolleri

- [ ] Tüm servisler çalışıyor
- [ ] DLP API bağlantısı başarılı
- [ ] Veritabanı migration'ları uygulandı
- [ ] Redis bağlantısı başarılı
- [ ] Dashboard erişilebilir
- [ ] DLP API'den veri geliyor
- [ ] Dashboard'da veri görünüyor
- [ ] Log'larda hata yok (sadece warning'ler normal)

## 🔧 Sorun Giderme

### DLP API'den Veri Gelmiyor

1. **Settings sayfasından ayar yapıldı mı?**
   - Dashboard → Settings → DLP API Configuration
   - Manager IP, Port, Username, Password girildi mi?
   - "Test Connection" butonu ile bağlantı test edildi mi?
   - "Save DLP API Settings" butonu ile kaydedildi mi?

2. **Bağlantı testi:**
   ```powershell
   curl -k https://YOUR_DLP_MANAGER_IP:8443/dlp/rest/v1/auth/access-token -X POST -H "username: YOUR_USERNAME" -H "password: YOUR_PASSWORD"
   ```

3. **Log kontrolü:**
   ```powershell
   # Collector log'larını kontrol edin
   # "DLP API settings are not configured" hatası var mı?
   # "Failed to get access token" veya "Failed to fetch incidents" hataları var mı?
   ```

4. **Network kontrolü:**
   ```powershell
   Test-NetConnection -ComputerName YOUR_DLP_MANAGER_IP -Port 8443
   ```

5. **Veritabanı kontrolü:**
   ```sql
   -- PostgreSQL'de
   SELECT * FROM system_settings WHERE key LIKE 'dlp_%';
   -- dlp_manager_ip, dlp_manager_port, dlp_username, dlp_password_protected değerleri görünmeli
   ```

### Migration Hataları

```powershell
# Manuel migration
cd C:\DLP_RiskAnalyzer\DLP.RiskAnalyzer.Analyzer
dotnet ef database update
```

### Dashboard Açılmıyor

1. **node_modules kontrolü:**
   ```powershell
   cd C:\DLP_RiskAnalyzer\dashboard
   Test-Path node_modules
   ```

2. **Build kontrolü:**
   ```powershell
   npm run build
   ```

3. **Port kontrolü:**
   ```powershell
   netstat -ano | findstr :3002
   ```

## 📞 Destek

Sorun yaşarsanız:
1. Log dosyalarını kontrol edin
2. `PRODUCTION_DEPLOYMENT_CHECKLIST.md` dosyasını gözden geçirin
3. `WINDOWS_SERVER_2025_KURULUM_REHBERI.md` dosyasına bakın

