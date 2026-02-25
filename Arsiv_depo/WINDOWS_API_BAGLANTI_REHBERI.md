# Windows API Bağlantı Rehberi - Forcepoint DLP REST API v1

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Forcepoint DLP REST API v1](#forcepoint-dlp-rest-api-v1)
3. [Authentication (Kimlik Doğrulama)](#authentication-kimlik-doğrulama)
4. [Incident API (Olay Yönetimi)](#incident-api-olay-yönetimi)
5. [Yapılandırma](#yapılandırma)
6. [Test ve Doğrulama](#test-ve-doğrulama)
7. [Sorun Giderme](#sorun-giderme)

---

## 🎯 Genel Bakış

Bu rehber, **Forcepoint DLP REST API v1** ile sistemin nasıl entegre edileceğini ve gerçek DLP verilerinin nasıl çekileceğini anlatır.

### API Akışı

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Authentication (Kimlik Doğrulama)                        │
│    POST /dlp/rest/v1/auth/access-token                      │
│    Request: { "username": "...", "password": "..." }       │
│    Response: { "accessToken": "JWT_TOKEN" }                 │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Incident API (Olay Çekme)                               │
│    GET /dlp/rest/v1/incidents?startTime=...&endTime=...    │
│    Header: Authorization: Bearer JWT_TOKEN                 │
│    Response: { "incidents": [...], "total": ... }          │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Remediation API (Olay Düzeltme)                          │
│    POST /dlp/rest/v1/incidents/update                      │
│    Header: Authorization: Bearer JWT_TOKEN                 │
│    Request: { "incidentId": "...", "action": "..." }        │
└─────────────────────────────────────────────────────────────┘
```

---

## 📚 Forcepoint DLP REST API v1

### Dokümantasyon

Resmi dokümantasyon: [https://help.forcepoint.com/dlp/90/restapi/](https://help.forcepoint.com/dlp/90/restapi/)

### Base URL

```
https://<DLP Manager IP>:<DLP Manager Port>/dlp/rest/v1
```

**Örnek:**
```
https://10.0.0.100:8443/dlp/rest/v1
```

### Önemli Notlar

1. **HTTPS Kullanımı**: Forcepoint DLP API varsayılan olarak HTTPS (port 8443) kullanır
2. **SSL Sertifikası**: Development ortamında self-signed sertifikalar için SSL doğrulaması bypass edilir
3. **JWT Token**: Her API isteğinde Bearer token gereklidir
4. **Token Expiry**: Token'lar genellikle 1 saat geçerlidir

---

## 🔐 Authentication (Kimlik Doğrulama)

### 1. Application Administrator Oluşturma

Forcepoint DLP Manager'da API kullanıcısı oluşturmanız gerekir:

1. **Forcepoint Security Manager**'a giriş yapın
2. **Global Settings** > **General** > **Administrators** yolunu izleyin
3. **New Administrator** butonuna tıklayın
4. **Type**: `Application` seçin
5. **Username** ve **Password** belirleyin
6. Gerekli izinleri verin (API erişimi için)
7. Kullanıcıyı kaydedin

### 2. Access Token Alma

**Endpoint:**
```
POST https://<DLP Manager IP>:<DLP Manager Port>/dlp/rest/v1/auth/access-token
```

**Request Body:**
```json
{
  "username": "YOUR_DLP_USERNAME",
  "password": "YOUR_DLP_PASSWORD"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**PowerShell ile Test:**
```powershell
$body = @{
    username = "YOUR_DLP_USERNAME"
    password = "YOUR_DLP_PASSWORD"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://YOUR_DLP_MANAGER_IP:8443/dlp/rest/v1/auth/access-token" `
    -Method Post `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck

$token = $response.accessToken
Write-Host "Access Token: $token"
```

### 3. Token Kullanımı

Token'ı her API isteğinde `Authorization` header'ında kullanın:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## 📊 Incident API (Olay Yönetimi)

### 1. Incident Listesi Çekme

**Endpoint:**
```
GET https://<DLP Manager IP>:<DLP Manager Port>/dlp/rest/v1/incidents
```

**Query Parameters:**
- `startTime`: Başlangıç zamanı (ISO 8601 format: `yyyy-MM-ddTHH:mm:ssZ`)
- `endTime`: Bitiş zamanı (ISO 8601 format: `yyyy-MM-ddTHH:mm:ssZ`)
- `page`: Sayfa numarası (varsayılan: 1)
- `pageSize`: Sayfa boyutu (varsayılan: 100)

**Örnek:**
```
GET /dlp/rest/v1/incidents?startTime=2024-01-01T00:00:00Z&endTime=2024-01-02T00:00:00Z&page=1&pageSize=100
```

**Headers:**
```
Authorization: Bearer <JWT_TOKEN>
Accept: application/json
```

**Response:**
```json
{
  "incidents": [
    {
      "id": 12345,
      "user": "user@company.com",
      "department": "IT",
      "severity": 5,
      "dataType": "PII",
      "timestamp": "2024-01-01T10:30:00Z",
      "policy": "Data Loss Prevention",
      "channel": "Email"
    }
  ],
  "total": 150
}
```

**PowerShell ile Test:**
```powershell
$headers = @{
    Authorization = "Bearer $token"
    Accept = "application/json"
}

$startTime = (Get-Date).AddHours(-24).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$endTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$uri = "https://YOUR_DLP_MANAGER_IP:8443/dlp/rest/v1/incidents?startTime=$startTime&endTime=$endTime&page=1&pageSize=100"

$response = Invoke-RestMethod -Uri $uri `
    -Method Get `
    -Headers $headers `
    -SkipCertificateCheck

Write-Host "Total Incidents: $($response.total)"
Write-Host "Fetched: $($response.incidents.Count)"
```

### 2. Incident Remediation (Olay Düzeltme)

**Endpoint:**
```
POST https://<DLP Manager IP>:<DLP Manager Port>/dlp/rest/v1/incidents/update
```

**Request Body:**
```json
{
  "incidentId": "12345",
  "action": "allow",
  "reason": "False positive",
  "notes": "Approved by security team"
}
```

**Headers:**
```
Authorization: Bearer <JWT_TOKEN>
Content-Type: application/json
```

**PowerShell ile Test:**
```powershell
$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

$body = @{
    incidentId = "12345"
    action = "allow"
    reason = "False positive"
    notes = "Approved by security team"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://YOUR_DLP_MANAGER_IP:8443/dlp/rest/v1/incidents/update" `
    -Method Post `
    -Headers $headers `
    -Body $body `
    -SkipCertificateCheck

Write-Host "Remediation Result: $($response | ConvertTo-Json)"
```

---

## ⚙️ Yapılandırma

### Collector Service Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Collector\appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "10.0.0.100",
    "ManagerPort": 8443,
    "Username": "dlp_api_user",
    "Password": "SecurePassword123!",
    "UseHttps": true,
    "Timeout": 30
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "StreamName": "dlp:incidents"
  },
  "Collector": {
    "IntervalMinutes": 60,
    "LookbackHours": 24,
    "BatchSize": 100
  }
}
```

### Analyzer API Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Analyzer\appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "10.0.0.100",
    "ManagerPort": 8443,
    "Username": "dlp_api_user",
    "Password": "SecurePassword123!",
    "UseHttps": true,
    "Timeout": 30
  }
}
```

**⚠️ Önemli**: 
- `ManagerIP`: Forcepoint DLP Manager'ın IP adresi veya hostname
- `Username`: Application Administrator kullanıcı adı
- `Password`: Application Administrator şifresi
- `ManagerPort`: Genellikle 8443 (HTTPS)

---

## ✅ Test ve Doğrulama

### 1. API Bağlantısını Test Etme

**PowerShell Script:**

```powershell
# DLP Manager IP ve bilgileri
$dlpIP = "YOUR_DLP_MANAGER_IP"
$dlpPort = 8443
$username = "YOUR_DLP_USERNAME"
$password = "YOUR_DLP_PASSWORD"

# 1. Access Token Alma
Write-Host "Step 1: Getting access token..." -ForegroundColor Yellow
$body = @{
    username = $username
    password = $password
} | ConvertTo-Json

try {
    $tokenResponse = Invoke-RestMethod -Uri "https://${dlpIP}:${dlpPort}/dlp/rest/v1/auth/access-token" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    $token = $tokenResponse.accessToken
    Write-Host "✓ Access token obtained successfully!" -ForegroundColor Green
    Write-Host "Token: $($token.Substring(0, 50))..." -ForegroundColor Gray
} catch {
    Write-Host "✗ Failed to get access token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 2. Incident Listesi Çekme
Write-Host "`nStep 2: Fetching incidents..." -ForegroundColor Yellow
$headers = @{
    Authorization = "Bearer $token"
    Accept = "application/json"
}

$startTime = (Get-Date).AddHours(-24).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$endTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$uri = "https://${dlpIP}:${dlpPort}/dlp/rest/v1/incidents?startTime=$startTime&endTime=$endTime&page=1&pageSize=10"

try {
    $incidentsResponse = Invoke-RestMethod -Uri $uri `
        -Method Get `
        -Headers $headers `
        -SkipCertificateCheck
    
    Write-Host "✓ Incidents fetched successfully!" -ForegroundColor Green
    Write-Host "Total Incidents: $($incidentsResponse.total)" -ForegroundColor Cyan
    Write-Host "Fetched: $($incidentsResponse.incidents.Count)" -ForegroundColor Cyan
    
    if ($incidentsResponse.incidents.Count -gt 0) {
        Write-Host "`nFirst Incident:" -ForegroundColor Yellow
        $incidentsResponse.incidents[0] | ConvertTo-Json -Depth 3
    }
} catch {
    Write-Host "✗ Failed to fetch incidents: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n✓ All tests passed!" -ForegroundColor Green
```

### 2. Collector Service Loglarını Kontrol Etme

Collector Service çalıştığında şu logları görmelisiniz:

```
[Information] DLP Collector Service started - Forcepoint DLP REST API v1 integration
[Information] Starting incident collection from Forcepoint DLP REST API v1...
[Debug] Requesting access token from https://10.0.0.100:8443/dlp/rest/v1/auth/access-token
[Information] Access token obtained successfully, expires at 2024-01-01 12:00:00
[Debug] Fetching incidents from https://10.0.0.100:8443/dlp/rest/v1/incidents?startTime=...
[Information] Fetched 25 incidents from Forcepoint DLP API (page 1, total: 150)
[Information] Successfully collected and pushed 25 incidents to Redis
```

### 3. Analyzer API Loglarını Kontrol Etme

Analyzer API çalıştığında şu logları görmelisiniz:

```
[Information] Now listening on: http://localhost:8000
[Information] Application started. Press Ctrl+C to shut down.
```

---

## 🔧 Sorun Giderme

### Problem: Access Token Alınamıyor

**Hata:**
```
Failed to get access token: The remote server returned an error: (401) Unauthorized
```

**Çözüm:**
1. Username ve Password'ün doğru olduğunu kontrol edin
2. Application Administrator hesabının aktif olduğunu kontrol edin
3. API erişim izinlerinin verildiğini kontrol edin
4. Forcepoint DLP Manager'a erişilebilir olduğunu test edin:
   ```powershell
   Test-NetConnection -ComputerName YOUR_DLP_MANAGER_IP -Port 8443
   ```

### Problem: SSL Sertifika Hatası

**Hata:**
```
The SSL connection could not be established
```

**Çözüm:**
- Development ortamında: `SkipCertificateCheck` kullanın (kodda zaten var)
- Production ortamında: SSL sertifikasını doğru şekilde yapılandırın

### Problem: Incident Çekilemiyor

**Hata:**
```
Failed to fetch incidents: The remote server returned an error: (401) Unauthorized
```

**Çözüm:**
1. Token'ın geçerli olduğunu kontrol edin (1 saat geçerlilik süresi)
2. Token'ın doğru şekilde Authorization header'ında gönderildiğini kontrol edin
3. Token'ı yeniden alın

### Problem: Timeout Hatası

**Hata:**
```
The operation timed out
```

**Çözüm:**
1. `appsettings.json`'da `Timeout` değerini artırın (varsayılan: 30 saniye)
2. Network bağlantısını kontrol edin
3. Firewall kurallarını kontrol edin

### Problem: Port 8443 Erişilemiyor

**Çözüm:**
```powershell
# Port erişilebilirliğini test edin
Test-NetConnection -ComputerName YOUR_DLP_MANAGER_IP -Port 8443

# Erişilemiyorsa:
# 1. Firewall kurallarını kontrol edin
# 2. Forcepoint DLP Manager'ın çalıştığını kontrol edin
# 3. Network bağlantısını kontrol edin
```

---

## 📚 Ek Kaynaklar

- [Forcepoint DLP REST API v1 Dokümantasyonu](https://help.forcepoint.com/dlp/90/restapi/)
- [JWT Token Hakkında](https://jwt.io/)
- [ISO 8601 Tarih Formatı](https://en.wikipedia.org/wiki/ISO_8601)

---

## 🎉 Bağlantı Başarılı!

API bağlantısı başarıyla yapılandırıldı! Artık gerçek DLP verilerini çekebilirsiniz:

1. **Collector Service**: Düzenli olarak DLP incident'lerini çeker
2. **Analyzer API**: Incident'leri analiz eder ve risk skorları hesaplar
3. **Dashboard**: Verileri görselleştirir ve raporlar oluşturur

**Sonraki Adımlar:**
- Collector Service'i başlatın
- Dashboard'da incident'leri görüntüleyin
- Risk analizlerini inceleyin

---

**Sorularınız için**: GitHub Issues veya dokümantasyonu kontrol edin.

