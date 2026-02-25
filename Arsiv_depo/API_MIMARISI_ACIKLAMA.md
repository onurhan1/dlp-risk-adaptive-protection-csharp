# API Mimarisi Açıklaması

Bu doküman, sistemde kullanılan iki farklı API'nin mimarisini ve farklarını açıklar.

## İki Farklı API

### 1. Forcepoint DLP Manager API (External API)
**Endpoint:** `POST https://<DLP Manager IP>:<DLP Manager port>/dlp/rest/v1/incidents/`

**Kullanım:**
- Collector Service tarafından çağrılır
- Forcepoint DLP Manager'dan gerçek incident verilerini çeker
- POST method kullanır (Forcepoint DLP API dokümantasyonuna göre)

**Request Format:**
```json
{
  "type": "INCIDENTS",
  "from_date": "dd/MM/yyyy HH:mm:ss",
  "to_date": "dd/MM/yyyy HH:mm:ss"
}
```

**Response:** Forcepoint DLP Manager'dan gelen ham incident verileri

**Kod Yeri:** `DLP.RiskAnalyzer.Collector/Services/DLPCollectorService.cs`

```csharp
// POST method kullanıyor
var request = new HttpRequestMessage(HttpMethod.Post, incidentsUrl);
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
request.Content = content; // JSON body
```

### 2. Analyzer API (Internal API)
**Endpoint:** `GET http://localhost:5001/api/incidents`

**Kullanım:**
- Dashboard (Next.js) tarafından çağrılır
- Analyzer Service'in kendi API'sidir
- PostgreSQL veritabanından veri çeker
- GET method kullanır (RESTful API standartlarına göre)

**Request Format:**
```
GET /api/incidents?user={email}&limit=50&order_by=timestamp_desc
```

**Response:** Veritabanından çekilen ve zenginleştirilmiş incident verileri

**Kod Yeri:** `DLP.RiskAnalyzer.Analyzer/Controllers/IncidentsController.cs`

```csharp
// GET method kullanıyor
[HttpGet]
public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] string? user,
    [FromQuery] string? department,
    [FromQuery] int limit = 100,
    [FromQuery] string orderBy = "timestamp_desc")
```

## Veri Akışı

```
┌─────────────────────────────────────┐
│  Forcepoint DLP Manager API         │
│  (External - POST method)           │
└──────────────┬──────────────────────┘
               │
               │ POST /dlp/rest/v1/incidents/
               │ { type: "INCIDENTS", from_date: "...", to_date: "..." }
               │
               ▼
┌─────────────────────────────────────┐
│  Collector Service                  │
│  (DLPCollectorService.cs)           │
└──────────────┬──────────────────────┘
               │
               │ Push to Redis Stream
               │
               ▼
┌─────────────────────────────────────┐
│  Redis Stream (dlp:incidents)       │
└──────────────┬──────────────────────┘
               │
               │ Process & Save to DB
               │
               ▼
┌─────────────────────────────────────┐
│  PostgreSQL Database                 │
│  (Incidents table)                  │
└──────────────┬──────────────────────┘
               │
               │ GET /api/incidents?user=...
               │
               ▼
┌─────────────────────────────────────┐
│  Analyzer API                       │
│  (Internal - GET method)            │
│  (IncidentsController.cs)           │
└──────────────┬──────────────────────┘
               │
               │ HTTP Response
               │
               ▼
┌─────────────────────────────────────┐
│  Dashboard (Next.js)               │
│  (Investigation Page)               │
└─────────────────────────────────────┘
```

## Neden Farklı Method'lar?

### Forcepoint DLP Manager API (POST)
- **Sebep:** Forcepoint DLP API dokümantasyonuna göre incidents endpoint'i POST method kullanır
- **Neden POST?** 
  - Request body'de tarih aralığı, filtreler gibi kompleks parametreler gönderilir
  - POST, request body ile veri göndermek için daha uygundur
  - Forcepoint DLP API'nin tasarımı böyle

**Örnek:**
```bash
POST https://172.16.245.126:9443/dlp/rest/v1/incidents/
Authorization: Bearer <token>
Content-Type: application/json

{
  "type": "INCIDENTS",
  "from_date": "15/01/2024 10:00:00",
  "to_date": "16/01/2024 10:00:00"
}
```

### Analyzer API (GET)
- **Sebep:** RESTful API standartlarına göre veri okuma işlemleri GET method kullanır
- **Neden GET?**
  - Sadece veri okuma (read-only) işlemi
  - Query parameters ile filtreleme yapılır
  - Cache'lenebilir
  - RESTful API best practices

**Örnek:**
```bash
GET http://localhost:5001/api/incidents?user=user@example.com&limit=50&order_by=timestamp_desc
```

## API Endpoint Karşılaştırması

| Özellik | Forcepoint DLP Manager API | Analyzer API |
|---------|---------------------------|--------------|
| **Type** | External API | Internal API |
| **Method** | POST | GET |
| **Base URL** | `https://<DLP Manager IP>:<port>` | `http://localhost:5001` |
| **Endpoint** | `/dlp/rest/v1/incidents/` | `/api/incidents` |
| **Authentication** | Bearer Token (JWT) | (Internal, güvenlik eklenebilir) |
| **Request Body** | JSON body (type, from_date, to_date) | Query parameters |
| **Response** | Ham DLP incident verileri | Zenginleştirilmiş incident verileri |
| **Kullanım** | Collector Service | Dashboard |
| **Veri Kaynağı** | Forcepoint DLP Manager | PostgreSQL Database |

## Kod Örnekleri

### Collector Service (POST - External API)

```csharp
// DLPCollectorService.cs
public async Task<List<DLPIncident>> FetchIncidentsAsync(DateTime startTime, DateTime endTime)
{
    var token = await GetAccessTokenAsync();
    
    var incidentsUrl = "/dlp/rest/v1/incidents/";
    var requestBody = new
    {
        type = "INCIDENTS",
        from_date = startTime.ToString("dd/MM/yyyy HH:mm:ss"),
        to_date = endTime.ToString("dd/MM/yyyy HH:mm:ss")
    };
    
    var jsonBody = JsonConvert.SerializeObject(requestBody);
    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
    
    // POST method kullanıyor
    var request = new HttpRequestMessage(HttpMethod.Post, incidentsUrl);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = content;
    
    var response = await _httpClient.SendAsync(request);
    // ...
}
```

### Analyzer API (GET - Internal API)

```csharp
// IncidentsController.cs
[HttpGet]
public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] string? user,
    [FromQuery] string? department,
    [FromQuery] int limit = 100,
    [FromQuery] string orderBy = "timestamp_desc")
{
    // GET method - query parameters ile filtreleme
    var incidents = await _dbService.GetIncidentsAsync(
        startDate, endDate, user, department, limit, orderBy);
    
    // Zenginleştirilmiş veri döndür
    var enrichedIncidents = incidents.Select(incident => {
        var riskLevel = _riskAnalyzer.GetRiskLevel(incident.RiskScore ?? 0);
        var policyAction = _riskAnalyzer.GetPolicyAction(riskLevel, incident.Channel ?? "");
        var iobs = _riskAnalyzer.DetectIOB(incident);
        
        return new IncidentResponse { /* ... */ };
    }).ToList();
    
    return Ok(enrichedIncidents);
}
```

## Özet

1. **Forcepoint DLP Manager API (POST):**
   - External API
   - Collector Service tarafından çağrılır
   - Forcepoint DLP Manager'dan gerçek veri çeker
   - POST method (Forcepoint API dokümantasyonuna göre)

2. **Analyzer API (GET):**
   - Internal API
   - Dashboard tarafından çağrılır
   - PostgreSQL veritabanından veri çeker
   - GET method (RESTful API standartlarına göre)

**İkisi de farklı amaçlara hizmet ediyor:**
- **POST API:** Dışarıdan veri çekmek için (Collector → DLP Manager)
- **GET API:** İçeriden veri sunmak için (Dashboard → Analyzer → Database)

