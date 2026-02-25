# Swagger'da DLP API Test Rehberi

Bu rehber, Swagger UI üzerinden Forcepoint DLP API bağlantısını nasıl test edeceğinizi açıklar.

## 🚀 Swagger UI'ya Erişim

1. **Analyzer API'yi başlatın:**
   ```bash
   cd DLP.RiskAnalyzer.Analyzer
   dotnet run
   ```

2. **Swagger UI'yi açın:**
   - Tarayıcınızda şu adrese gidin: `http://localhost:5001/swagger`
   - Veya network IP kullanıyorsanız: `http://192.168.1.100:5001/swagger`

## 📋 Test Endpoint'leri

Swagger'da **`DLPTest`** controller'ı altında 4 test endpoint'i bulunur:

### 1. 🔐 Authentication Test
**Endpoint:** `GET /api/dlptest/auth`

**Açıklama:** Forcepoint DLP API'ye authentication yapıp access token alır.

**Kullanım:**
1. Swagger'da `GET /api/dlptest/auth` endpoint'ini bulun
2. **"Try it out"** butonuna tıklayın
3. **"Execute"** butonuna tıklayın

**Başarılı Yanıt Örneği:**
```json
{
  "success": true,
  "message": "DLP API authentication successful",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenLength": 500,
  "config": {
    "baseUrl": "https://192.168.1.100:8443",
    "managerIP": "192.168.1.100",
    "managerPort": 8443,
    "username": "your_username"
  }
}
```

**Hata Yanıt Örnekleri:**
- **401 Unauthorized:** Kullanıcı adı veya şifre yanlış
- **408 Timeout:** DLP Manager'a erişilemiyor (firewall/network sorunu)
- **503 Service Unavailable:** DLP Manager çalışmıyor veya erişilemiyor

---

### 2. 🔌 Connection Test
**Endpoint:** `GET /api/dlptest/connection`

**Açıklama:** DLP Manager'a network bağlantısını test eder (authentication gerektirmez).

**Kullanım:**
1. Swagger'da `GET /api/dlptest/connection` endpoint'ini bulun
2. **"Try it out"** butonuna tıklayın
3. **"Execute"** butonuna tıklayın

**Başarılı Yanıt:**
```json
{
  "success": true,
  "message": "DLP API connection successful",
  "statusCode": 200,
  "config": {
    "baseUrl": "https://192.168.1.100:8443",
    "managerIP": "192.168.1.100",
    "managerPort": 8443,
    "useHttps": true
  }
}
```

**Hata Durumları:**
- **408 Timeout:** Network bağlantısı yok veya firewall engelliyor
- **503 Service Unavailable:** DLP Manager erişilemiyor

---

### 3. 📊 Incidents Fetch Test
**Endpoint:** `GET /api/dlptest/incidents?hours=24`

**Açıklama:** DLP API'den incident'leri çeker (authentication + incidents fetch).

**Parametreler:**
- `hours` (query parameter, optional): Kaç saat geriye bakılacak (varsayılan: 24)

**Kullanım:**
1. Swagger'da `GET /api/dlptest/incidents` endpoint'ini bulun
2. **"Try it out"** butonuna tıklayın
3. `hours` parametresini ayarlayın (örn: 24, 48, 168)
4. **"Execute"** butonuna tıklayın

**Başarılı Yanıt Örneği:**
```json
{
  "success": true,
  "message": "Incidents fetched successfully",
  "timeRange": {
    "startTime": "2024-01-15T10:00:00Z",
    "endTime": "2024-01-16T10:00:00Z",
    "hours": 24
  },
  "incidents": {
    "incidents": [...],
    "total": 150
  }
}
```

**Hata Durumları:**
- **400 Bad Request:** Username/Password yapılandırılmamış
- **401 Unauthorized:** Authentication başarısız
- **500 Internal Server Error:** Incidents fetch sırasında hata

---

### 4. ⚙️ Configuration Check
**Endpoint:** `GET /api/dlptest/config`

**Açıklama:** `appsettings.json`'daki DLP yapılandırmasını gösterir (şifreler maskelenir).

**Kullanım:**
1. Swagger'da `GET /api/dlptest/config` endpoint'ini bulun
2. **"Try it out"** butonuna tıklayın
3. **"Execute"** butonuna tıklayın

**Yanıt Örneği:**
```json
{
  "config": {
    "managerIP": "192.168.1.100",
    "managerPort": 8443,
    "useHttps": true,
    "timeout": 30,
    "baseUrl": "https://192.168.1.100:8443",
    "usernameConfigured": true,
    "passwordConfigured": true,
    "username": "adm***",
    "password": "***"
  },
  "note": "This endpoint shows configuration without exposing sensitive data"
}
```

---

## 🔍 Sorun Giderme

### 1. Authentication Başarısız (401)
**Kontrol Listesi:**
- ✅ `appsettings.json`'da `DLP:Username` ve `DLP:Password` doğru mu?
- ✅ Forcepoint DLP Manager'da Application Administrator kullanıcısı oluşturuldu mu?
- ✅ Kullanıcı adı ve şifre doğru mu?

**Çözüm:**
```json
// DLP.RiskAnalyzer.Analyzer/appsettings.json
{
  "DLP": {
    "ManagerIP": "192.168.1.100",
    "ManagerPort": 8443,
    "Username": "your_username",
    "Password": "your_password",
    "UseHttps": true,
    "Timeout": 30
  }
}
```

### 2. Connection Timeout (408)
**Kontrol Listesi:**
- ✅ DLP Manager IP adresi doğru mu?
- ✅ DLP Manager port'u doğru mu? (genelde 8443)
- ✅ Firewall'da port açık mı?
- ✅ DLP Manager çalışıyor mu?

**Test:**
```bash
# Windows PowerShell
Test-NetConnection -ComputerName 192.168.1.100 -Port 8443

# Linux/Mac
telnet 192.168.1.100 8443
# veya
nc -zv 192.168.1.100 8443
```

### 3. SSL Certificate Error
**Sorun:** Self-signed sertifika kullanılıyor.

**Çözüm:** Kod zaten SSL doğrulamasını bypass ediyor (`ServerCertificateCustomValidationCallback`). Eğer hala sorun varsa, `appsettings.json`'da `UseHttps: false` deneyin (eğer DLP Manager HTTP destekliyorsa).

### 4. Incidents Boş Geliyor
**Kontrol Listesi:**
- ✅ Belirtilen zaman aralığında incident var mı?
- ✅ `hours` parametresini artırın (örn: 168 = 7 gün)
- ✅ DLP Manager'da incident'ler gerçekten var mı?

---

## 📝 Test Senaryoları

### Senaryo 1: İlk Kurulum Testi
1. **Config Check:** `/api/dlptest/config` → Yapılandırmanın doğru olduğunu kontrol edin
2. **Connection Test:** `/api/dlptest/connection` → Network bağlantısını test edin
3. **Authentication Test:** `/api/dlptest/auth` → Login bilgilerini test edin
4. **Incidents Test:** `/api/dlptest/incidents?hours=168` → Veri çekmeyi test edin

### Senaryo 2: Sorun Giderme
1. **Connection Test** başarısız → Firewall/Network sorunu
2. **Connection Test** başarılı ama **Authentication Test** başarısız → Kullanıcı adı/şifre sorunu
3. **Authentication Test** başarılı ama **Incidents Test** başarısız → API endpoint sorunu veya veri yok

---

## 🎯 Diğer Test Endpoint'leri

Swagger'da başka test edebileceğiniz endpoint'ler:

### Policies API
- `GET /api/policies` → Tüm policy'leri getirir
- `GET /api/policies/{policyId}` → Belirli bir policy'yi getirir

**Not:** Bu endpoint'ler de DLP API'ye bağlanır, ancak hata mesajları daha az detaylıdır. Sorun giderme için `DLPTest` endpoint'lerini kullanın.

---

## ✅ Başarı Kriterleri

Tüm testler başarılı olduğunda:
- ✅ DLP Manager'a network erişimi var
- ✅ Authentication çalışıyor
- ✅ Access token alınabiliyor
- ✅ Incidents çekilebiliyor
- ✅ Collector servisi çalıştırılabilir

---

## 📚 İlgili Dokümantasyon

- [Forcepoint DLP REST API Documentation](https://help.forcepoint.com/dlp/90/restapi/)
- [Windows API Bağlantı Rehberi](./WINDOWS_API_BAGLANTI_REHBERI.md)
- [Network Access Setup](./NETWORK_ACCESS_SETUP.md)

---

**Son Güncelleme:** 2024-01-16

