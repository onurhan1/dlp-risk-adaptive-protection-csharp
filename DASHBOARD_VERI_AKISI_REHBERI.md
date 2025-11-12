# Dashboard Veri Akışı Rehberi

Bu doküman, Forcepoint DLP API'den gelen verilerin dashboard'da gösterilmesi için gerekli veri akışını açıklar.

## Veri Akışı

```
Forcepoint DLP Manager API
    ↓
Collector Service (DLP.RiskAnalyzer.Collector)
    ↓
Redis Stream (dlp:incidents)
    ↓
Analyzer Service (DLP.RiskAnalyzer.Analyzer)
    ↓
PostgreSQL Database
    ↓
Analyzer API Endpoints
    ↓
Dashboard (Next.js)
```

## 1. Collector Service

**Dosya:** `DLP.RiskAnalyzer.Collector/Services/DLPCollectorService.cs`

### Yapılan Değişiklikler

1. **HTTP Method:** GET → POST
2. **Endpoint:** `/dlp/rest/v1/incidents/` (trailing slash eklendi)
3. **Request Body Format:**
   ```json
   {
     "type": "INCIDENTS",
     "from_date": "dd/MM/yyyy HH:mm:ss",
     "to_date": "dd/MM/yyyy HH:mm:ss"
   }
   ```
4. **Date Format:** ISO 8601 → `dd/MM/yyyy HH:mm:ss` (Forcepoint DLP API formatı)
5. **Response Parsing:** Hem array hem de object formatını destekler

### Çalışma Şekli

- `CollectorBackgroundService` her 60 dakikada bir (configurable) çalışır
- Son 24 saat içindeki incidents'leri çeker
- Her incident'i Redis stream'e push eder

## 2. Redis Stream

**Stream Name:** `dlp:incidents`

**Mesaj Formatı:**
```
user: <email>
department: <department>
severity: <1-5>
data_type: <data type>
timestamp: <ISO 8601>
policy: <policy name>
channel: <channel>
```

## 3. Analyzer Service

**Dosya:** `DLP.RiskAnalyzer.Analyzer/Services/DatabaseService.cs`

### Yapılan İyileştirmeler

1. **Redis Consumer Groups:** Stream position'ı takip etmek için consumer group kullanılıyor
2. **Duplicate Detection:** Aynı incident'in tekrar işlenmesini önler
3. **Message Acknowledgment:** İşlenen mesajlar acknowledge edilir
4. **Error Handling:** Hatalı mesajlar atlanır, işlem devam eder

### Çalışma Şekli

- `ProcessRedisStreamAsync()` metodu çağrıldığında:
  1. Redis stream'den yeni mesajları okur (consumer group kullanarak)
  2. Her mesajı parse eder ve `Incident` modeline dönüştürür
  3. Duplicate kontrolü yapar
  4. Veritabanına kaydeder
  5. Mesajı acknowledge eder

### API Endpoint'leri

**Redis Stream'i İşlemek İçin:**
```bash
POST /api/analysis/process/redis-stream
```

**Risk Skorlarını Hesaplamak İçin:**
```bash
POST /api/analysis/daily
```

## 4. Database

**Model:** `DLP.RiskAnalyzer.Shared.Models.Incident`

**Alanlar:**
- `Id` (int)
- `UserEmail` (string)
- `Department` (string?)
- `Severity` (int)
- `DataType` (string?)
- `Timestamp` (DateTime)
- `Policy` (string?)
- `Channel` (string?)
- `RiskScore` (int?) - Analyzer tarafından hesaplanır
- `RepeatCount` (int) - Analyzer tarafından hesaplanır
- `DataSensitivity` (int) - Analyzer tarafından hesaplanır

## 5. Analyzer API Endpoints

### Incidents Endpoint

**GET** `/api/incidents`

**Query Parameters:**
- `startDate` (DateTime?)
- `endDate` (DateTime?)
- `user` (string?)
- `department` (string?)
- `limit` (int, default: 100)
- `orderBy` (string, default: "timestamp_desc")

**Response:**
```json
[
  {
    "id": 1,
    "userEmail": "user@example.com",
    "department": "IT",
    "severity": 4,
    "dataType": "PII",
    "timestamp": "2024-01-15T10:00:00Z",
    "policy": "Data Loss Prevention",
    "channel": "Email",
    "riskScore": 85,
    "repeatCount": 3,
    "dataSensitivity": 8,
    "riskLevel": "High",
    "recommendedAction": "Block",
    "iobs": ["IOB-502"]
  }
]
```

### User List Endpoint

**GET** `/api/risk/user-list`

**Query Parameters:**
- `page` (int, default: 1)
- `page_size` (int, default: 15)

**Response:**
```json
{
  "users": [
    {
      "user_email": "user@example.com",
      "risk_score": 85,
      "total_incidents": 10
    }
  ],
  "total": 50,
  "page": 1,
  "page_size": 15
}
```

## 6. Dashboard

### Ana Sayfa (`app/page.tsx`)

**API Çağrıları:**
- `/api/risk/daily-summary?days=30`
- `/api/risk/department-summary?start_date=...&end_date=...`
- `/api/incidents?start_date=...&end_date=...&limit=1000&order_by=risk_score_desc`

### Investigation Sayfası (`app/investigation/page.tsx`)

**API Çağrıları:**
- `/api/risk/user-list?page=1&page_size=15` (InvestigationUsersList component)
- `/api/incidents?user=...&limit=50&order_by=timestamp_desc` (InvestigationTimeline component)

## Veri Akışını Test Etme

### 1. Collector Service'i Başlatma

```bash
cd DLP.RiskAnalyzer.Collector
dotnet run
```

**Kontrol:**
- Log'larda "Fetched X incidents from Forcepoint DLP API" mesajını görmelisiniz
- Redis'te `dlp:incidents` stream'inde mesajlar olmalı

### 2. Analyzer Service'i Başlatma

```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet run
```

**Redis Stream'i İşleme:**
```bash
curl -X POST http://localhost:5001/api/analysis/process/redis-stream
```

**Risk Skorlarını Hesaplama:**
```bash
curl -X POST http://localhost:5001/api/analysis/daily
```

### 3. Veritabanını Kontrol Etme

```sql
SELECT COUNT(*) FROM "Incidents";
SELECT * FROM "Incidents" ORDER BY "Timestamp" DESC LIMIT 10;
```

### 4. API Endpoint'lerini Test Etme

```bash
# Incidents listesi
curl http://localhost:5001/api/incidents?limit=10

# User listesi
curl http://localhost:5001/api/risk/user-list?page=1&page_size=15
```

### 5. Dashboard'u Kontrol Etme

1. Dashboard'u başlatın: `cd dashboard && npm run dev`
2. Ana sayfada veriler görünmeli
3. Investigation sayfasında kullanıcı listesi görünmeli

## Sorun Giderme

### Dashboard'da Veri Görünmüyor

1. **Collector Service çalışıyor mu?**
   - Log'larda "Fetched X incidents" mesajını kontrol edin
   - Redis'te stream'de mesaj var mı kontrol edin: `redis-cli XINFO STREAM dlp:incidents`

2. **Redis Stream işlendi mi?**
   - `POST /api/analysis/process/redis-stream` endpoint'ini çağırın
   - Veritabanında incident kayıtları var mı kontrol edin

3. **Risk skorları hesaplandı mı?**
   - `POST /api/analysis/daily` endpoint'ini çağırın
   - Veritabanında `RiskScore` değerleri null değil mi kontrol edin

4. **API endpoint'leri çalışıyor mu?**
   - Swagger UI'da test edin: `http://localhost:5001/swagger`
   - Browser console'da hata var mı kontrol edin

5. **Dashboard API URL'i doğru mu?**
   - `dashboard/lib/api-config.ts` dosyasını kontrol edin
   - Browser network tab'ında API çağrılarını kontrol edin

### Redis Stream'de Mesaj Birikiyor

- `ProcessRedisStreamAsync()` metodunu düzenli olarak çağırmak için bir background service eklenebilir
- Şu anda manuel olarak `/api/analysis/process/redis-stream` endpoint'i çağrılmalı

### Duplicate Incidents

- `DatabaseService.ProcessRedisStreamAsync()` duplicate kontrolü yapıyor
- Aynı `UserEmail`, `Timestamp` ve `Policy` kombinasyonu varsa kayıt edilmez

## Sonraki Adımlar

1. **Background Service Ekleme:** Analyzer service'e Redis stream'i otomatik işleyen bir background service eklenebilir
2. **Scheduled Jobs:** Risk skorlarını düzenli olarak hesaplamak için scheduled job eklenebilir
3. **Real-time Updates:** Dashboard'a WebSocket veya SignalR ile real-time güncellemeler eklenebilir

