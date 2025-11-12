# Investigation Sayfası Çalışma Rehberi

Bu doküman, Investigation sayfasının nasıl çalıştığını ve veri akışını detaylı olarak açıklar.

## Genel Bakış

Investigation sayfası 3 kolonlu bir layout'a sahiptir:

1. **Sol Panel:** Kullanıcı listesi (risk skorlarına göre sıralı)
2. **Orta Panel:** Seçili kullanıcının timeline'ı (incident geçmişi)
3. **Sağ Panel:** Seçili incident'in detayları

## Veri Akışı

```
PostgreSQL Database (Incidents)
    ↓
Analyzer API Endpoints
    ↓
Dashboard Components
```

## 1. Sol Panel - Kullanıcı Listesi

### Component: `InvestigationUsersList.tsx`

**API Endpoint:**
```
GET /api/risk/user-list?page=1&page_size=15
```

**Response Format:**
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

### Özellikler:

1. **Pagination:** Sayfa başına 15 kullanıcı gösterir
2. **Search:** Kullanıcı email'ine göre arama yapılabilir
3. **Risk Filter:** Risk seviyesine göre filtreleme:
   - All Risk Levels
   - Critical (80+)
   - High (50-79)
   - Medium (30-49)
   - Low (0-29)
4. **Risk Score Görselleştirme:** Her kullanıcı için circular progress bar
   - Kırmızı (80+): Critical
   - Turuncu (50-79): High
   - Sarı (30-49): Medium
   - Yeşil (0-29): Low

### Kullanıcı Seçimi:

Kullanıcı listesinden bir kullanıcı seçildiğinde:
- `onUserSelect(email, riskScore)` callback'i çağrılır
- Seçili kullanıcı state'e kaydedilir
- Timeline component'i bu kullanıcı için incident'leri yükler

## 2. Orta Panel - Timeline

### Component: `InvestigationTimeline.tsx`

**API Endpoint:**
```
GET /api/incidents?user={userEmail}&limit=50&order_by=timestamp_desc
```

**Response Format:**
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

### Özellikler:

1. **User Profile Header:**
   - Kullanıcı adı (email'den türetilir)
   - Risk skoru (circular progress bar ile)
   - "User Insights" butonu

2. **Timeline Events:**
   - Incident'ler tarihe göre gruplanır
   - Her incident için:
     - Timestamp (HH:mm UTC formatında)
     - Description (incident açıklaması)
     - Severity badge (High/Medium/Low)
     - Tags (Data exfiltration, High severity, vb.)
     - Channel bilgisi

3. **Event Selection:**
   - Bir incident'e tıklandığında:
     - `onEventSelect(event)` callback'i çağrılır
     - Seçili event state'e kaydedilir
     - Sağ panel'de detaylar gösterilir

### Description Oluşturma:

```typescript
const getDescription = (incident: any): string => {
  if (incident.channel === 'Email' && incident.data_type) {
    return `Email sent to ${incident.data_type}`
  }
  if (incident.channel === 'Removable Storage') {
    return 'Suspicious number of files copied to removable storage'
  }
  if (incident.policy) {
    return incident.policy
  }
  return 'Security incident detected'
}
```

### Tags Oluşturma:

```typescript
const getTags = (incident: any): string[] => {
  const tags: string[] = []
  if (incident.data_type === 'PII' || incident.data_type === 'PCI' || incident.data_type === 'CCN') {
    tags.push('Data exfiltration')
  }
  if (incident.severity >= 4) {
    tags.push('High severity')
  }
  return tags
}
```

## 3. Sağ Panel - Alert Details

### Component: `InvestigationAlertDetails.tsx`

**Veri Kaynağı:** Seçili event'in state'inden gelir (API'den çekilmez)

### Özellikler:

1. **Event Enrichment:**
   - Seçili event, `handleEventSelect` fonksiyonunda zenginleştirilir
   - Eksik alanlar varsayılan değerlerle doldurulur:
     - `destination`: Channel'a göre (Email → gmail.com)
     - `classification`: Tags'e göre (Data exfiltration → ['PCI', 'CCN', 'PII'])
     - `matched_rules`: Policy'ye göre
     - `source_application`: Channel'a göre (Email → outlook.exe)
     - `email_subject`: Channel'a göre
     - `recipients`: Channel'a göre
     - `iob_number`: Varsayılan '904'
     - `files`: Varsayılan dosya listesi

2. **Detay Bilgileri:**
   - Severity badge
   - Timestamp
   - Channel
   - Destination
   - Classification tags
   - Matched rules
   - Files (eğer varsa)
   - Source application
   - Email subject/recipients (eğer Email channel ise)
   - IOB number

## API Endpoint Detayları

### 1. User List Endpoint

**Controller:** `RiskController.cs`
**Method:** `GetUserList`

```csharp
[HttpGet("user-list")]
public async Task<ActionResult<Dictionary<string, object>>> GetUserList(
    [FromQuery] int page = 1,
    [FromQuery] int page_size = 15)
```

**Backend Logic:**
- `RiskAnalyzerService.GetUserListAsync()` çağrılır
- Veritabanından tüm incident'ler çekilir
- Kullanıcılara göre gruplanır
- Her kullanıcı için:
  - En yüksek risk skoru hesaplanır
  - Toplam incident sayısı hesaplanır
- Pagination uygulanır

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

### 2. Incidents Endpoint

**Controller:** `IncidentsController.cs`
**Method:** `GetIncidents`

```csharp
[HttpGet]
public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] string? user,
    [FromQuery] string? department,
    [FromQuery] int limit = 100,
    [FromQuery] string orderBy = "timestamp_desc")
```

**Backend Logic:**
- `DatabaseService.GetIncidentsAsync()` çağrılır
- Veritabanından incident'ler filtrelenir:
  - `user` parametresi varsa, sadece o kullanıcının incident'leri
  - `startDate` ve `endDate` varsa, tarih aralığı filtresi
  - `department` varsa, departman filtresi
- `orderBy` parametresine göre sıralanır:
  - `timestamp_desc`: En yeni önce
  - `timestamp_asc`: En eski önce
  - `risk_score_desc`: En yüksek risk skoru önce
- `limit` parametresine göre sınırlanır
- Her incident için risk level, recommended action ve IOBs hesaplanır

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

## Kullanım Senaryosu

### Senaryo 1: Kullanıcı Araştırması

1. **Kullanıcı Listesini Görüntüleme:**
   - Investigation sayfası açılır
   - Sol panel'de risk skorlarına göre sıralı kullanıcılar görünür
   - Search box'a email yazılarak arama yapılabilir
   - Risk filter ile risk seviyesine göre filtreleme yapılabilir

2. **Kullanıcı Seçimi:**
   - Listeden bir kullanıcı seçilir
   - Orta panel'de o kullanıcının timeline'ı yüklenir
   - Timeline'da incident'ler tarihe göre gruplanmış şekilde görünür

3. **Incident Detaylarını Görüntüleme:**
   - Timeline'dan bir incident seçilir
   - Sağ panel'de o incident'in detayları gösterilir
   - Classification, matched rules, files gibi bilgiler görüntülenir

### Senaryo 2: Risk Analizi

1. **Yüksek Riskli Kullanıcıları Bulma:**
   - Risk filter'dan "Critical (80+)" seçilir
   - Sadece risk skoru 80 ve üzeri kullanıcılar görünür

2. **Kullanıcı Geçmişini İnceleme:**
   - Yüksek riskli bir kullanıcı seçilir
   - Timeline'da o kullanıcının tüm incident'leri görünür
   - Tekrarlayan pattern'ler tespit edilebilir

3. **Incident Analizi:**
   - Her incident'in detayları incelenir
   - IOBs (Indicators of Behavior) kontrol edilir
   - Recommended action'lara göre aksiyon alınabilir

## Sorun Giderme

### Kullanıcı Listesi Boş Görünüyor

1. **API Endpoint'i Kontrol Edin:**
   ```bash
   curl http://localhost:5001/api/risk/user-list?page=1&page_size=15
   ```

2. **Veritabanında Incident Var mı?**
   ```sql
   SELECT COUNT(*) FROM "Incidents";
   SELECT DISTINCT "UserEmail" FROM "Incidents";
   ```

3. **Risk Skorları Hesaplandı mı?**
   ```sql
   SELECT "UserEmail", MAX("RiskScore") as max_risk
   FROM "Incidents"
   GROUP BY "UserEmail"
   ORDER BY max_risk DESC;
   ```

4. **Browser Console'da Hata Var mı?**
   - Network tab'ında API çağrılarını kontrol edin
   - Response'ları inceleyin

### Timeline Boş Görünüyor

1. **Kullanıcı Seçildi mi?**
   - Sol panel'den bir kullanıcı seçildiğinden emin olun

2. **API Endpoint'i Kontrol Edin:**
   ```bash
   curl "http://localhost:5001/api/incidents?user=user@example.com&limit=50&order_by=timestamp_desc"
   ```

3. **Veritabanında O Kullanıcı İçin Incident Var mı?**
   ```sql
   SELECT * FROM "Incidents"
   WHERE "UserEmail" = 'user@example.com'
   ORDER BY "Timestamp" DESC;
   ```

### Alert Details Boş Görünüyor

1. **Event Seçildi mi?**
   - Timeline'dan bir incident seçildiğinden emin olun

2. **Event Enrichment Çalışıyor mu?**
   - Browser console'da `handleEventSelect` fonksiyonunun çağrıldığını kontrol edin
   - Seçili event'in state'de olduğunu kontrol edin

## Performans İyileştirmeleri

### 1. Caching

- Kullanıcı listesi için cache eklenebilir (5 dakika)
- Timeline için cache eklenebilir (1 dakika)

### 2. Lazy Loading

- Timeline event'leri scroll edildikçe yüklenebilir
- Pagination için infinite scroll kullanılabilir

### 3. Real-time Updates

- WebSocket veya SignalR ile real-time güncellemeler eklenebilir
- Yeni incident'ler otomatik olarak timeline'a eklenebilir

## Sonraki Adımlar

1. **Export Functionality:** Investigation sonuçlarını PDF/Excel olarak export etme
2. **Remediation Actions:** Incident'lerden direkt remediation action'ları tetikleme
3. **Annotations:** Timeline'a notlar/annotation'lar ekleme
4. **Collaboration:** Birden fazla kullanıcının aynı investigation'ı paylaşması
5. **Advanced Filtering:** Daha gelişmiş filtreleme seçenekleri (tarih aralığı, channel, severity, vb.)

