# DLP Risk Analyzer - Sunum Dokümantasyonu

## 📋 Proje Özeti

**DLP Risk Adaptive Protection** sistemi, Forcepoint DLP'den gelen veri kaybı önleme olaylarını (incidents) analiz eden, risk skorları hesaplayan ve davranışsal anomalileri tespit eden kapsamlı bir güvenlik platformudur.

### Teknoloji Yığını
- **Backend:** .NET 8 (C#) Web API
- **Frontend:** Next.js 14 (React/TypeScript)
- **Database:** PostgreSQL
- **Cache/Message Queue:** Redis
- **AI Integration:** Azure OpenAI (GPT-4)

---

## 🏠 1. Dashboard Sayfası

### Açıklama
Ana kontrol paneli sayfası. Tüm DLP incident'larının özet görünümünü, risk analizlerini ve trend verilerini gösterir.

### Gösterilen Veriler
- **Action Summary Cards:** AUTHORIZED, BLOCK, QUARANTINE, RELEASED, TOTAL sayıları
- **Top Users (Son 24 saat):** En yüksek risk skoruna sahip kullanıcılar
- **Data Movement (30 Gün):** Kanal bazlı veri hareketi grafiği
- **Top Matched Rules:** En çok tetiklenen DLP kuralları
- **Daily Incident Trends:** Günlük incident tablosu

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/risk/action-summary` | GET | Action bazlı incident sayıları |
| `/api/risk/daily-summary` | GET | Günlük özet istatistikler |
| `/api/risk/department-summary` | GET | Departman bazlı özet |
| `/api/incidents` | GET | Incident listesi (filtrelenebilir) |
| `/api/risk/incidents/by-action` | GET | Action'a göre incident'lar (pagination) |

### PlantUML - Sequence Diagram
```plantuml
@startuml Dashboard_Flow
actor User
participant "Dashboard Page" as FE
participant "RiskController" as API
participant "IncidentsController" as INC
database PostgreSQL as DB

User -> FE: Sayfa açılır
activate FE

par Paralel API Çağrıları
    FE -> API: GET /api/risk/action-summary
    API -> DB: Action counts query
    DB --> API: Counts
    API --> FE: {authorized, block, quarantine, total}
    
    FE -> API: GET /api/risk/daily-summary?days=30
    API -> DB: Daily aggregation
    DB --> API: Daily stats
    API --> FE: [{date, total_incidents, high_risk_count}]
    
    FE -> INC: GET /api/incidents?limit=5000
    INC -> DB: Select incidents
    DB --> INC: Incidents list
    INC --> FE: Incidents data
end

FE -> FE: Top Users hesapla (client-side)
FE -> FE: Top Rules hesapla (client-side)
FE --> User: Dashboard render

User -> FE: BLOCK kartına tıkla
FE -> API: GET /api/risk/incidents/by-action?action=BLOCK&page=1
API -> DB: Paginated query
DB --> API: {items, totalCount, totalPages}
API --> FE: Paginated response
FE --> User: Modal ile incident listesi göster

@enduml
```

---

## 🔍 2. Investigation Sayfası

### Açıklama
Detaylı araştırma sayfası. Tarih aralığına göre filtrelenebilen, en riskli kullanıcılar ve en çok tetiklenen kuralları gösteren sayfa.

### Özellikler
- Tarih aralığı seçimi (DatePicker)
- Top 20 Risky Users (Risk skoruna göre sıralı)
- Top Alerts (Kural bazlı)
- Incident timeline grafiği

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/risk/top-users-daily` | GET | Tarih aralığında en riskli kullanıcılar |
| `/api/risk/top-rules-daily` | GET | Tarih aralığında en çok tetiklenen kurallar |
| `/api/risk/daily-summary` | GET | Günlük özet (timeline için) |

### PlantUML - Sequence Diagram
```plantuml
@startuml Investigation_Flow
actor User
participant "Investigation Page" as FE
participant "RiskController" as API
participant "RiskAnalyzerService" as SVC
database PostgreSQL as DB

User -> FE: Tarih aralığı seç
activate FE

FE -> API: GET /api/risk/top-users-daily?startDate=X&endDate=Y&limit=20
activate API
API -> SVC: GetTopUsersByDayAsync(startDate, endDate)
SVC -> DB: SELECT user_email, MAX(risk_score), COUNT(*)\nFROM incidents\nWHERE timestamp BETWEEN X AND Y\nGROUP BY user_email\nHAVING MAX(risk_score) >= 700\nORDER BY MAX(risk_score) DESC, COUNT(*) DESC\nLIMIT 20
DB --> SVC: User stats
SVC --> API: Top users list
API --> FE: [{user_email, risk_score, total_alerts}]
deactivate API

FE -> API: GET /api/risk/top-rules-daily?startDate=X&endDate=Y
API -> SVC: GetTopRulesByDayAsync()
SVC -> DB: SELECT policy, COUNT(*) FROM incidents GROUP BY policy
DB --> SVC: Rule stats
SVC --> API: Top rules list
API --> FE: [{rule_name, total_alerts, unique_users}]

FE --> User: Investigation data render
@enduml
```

---

## 📊 3. Reports Sayfası

### Açıklama
Günlük raporlama sayfası. Seçilen bir gün için detaylı DLP raporu görüntüler ve PDF olarak indirilmesini sağlar.

### Özellikler
- Tek gün seçimi
- Action Summary (günlük)
- Top 10 Users (günlük)
- Channel Breakdown
- Top 10 Policies with Rules
- Top 10 Destinations
- PDF Export

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/reports/daily-summary` | GET | Günlük detaylı özet (JSON) |
| `/api/reports/daily-summary/pdf` | GET | Günlük rapor PDF |
| `/api/reports` | GET | Oluşturulan raporlar listesi |
| `/api/reports/summary` | GET | Özet rapor PDF (tarih aralığı) |

### PlantUML - Sequence Diagram
```plantuml
@startuml Reports_Flow
actor User
participant "Reports Page" as FE
participant "ReportsController" as API
participant "RiskAnalyzerService" as SVC
participant "ReportGeneratorService" as PDF
database PostgreSQL as DB

User -> FE: Tarih seç (2026-01-12)
activate FE

FE -> API: GET /api/reports/daily-summary?date=2026-01-12
activate API
API -> SVC: GetDailyReportDataAsync(date)
SVC -> DB: Query incidents for date
DB --> SVC: Daily incidents

SVC -> SVC: Calculate action_summary
SVC -> SVC: Calculate top_users
SVC -> SVC: Calculate channel_breakdown
SVC -> SVC: Calculate top_policies
SVC -> SVC: Calculate top_destinations

SVC --> API: DailyReportData
API --> FE: {action_summary, top_users, channel_breakdown, ...}
deactivate API

FE --> User: Report display

User -> FE: "Download PDF" butonuna tıkla
FE -> API: GET /api/reports/daily-summary/pdf?date=2026-01-12
API -> PDF: GenerateDailyReportPdf(date)
PDF -> SVC: GetDailyReportDataAsync(date)
SVC --> PDF: Data
PDF -> PDF: Create PDF with iTextSharp
PDF --> API: PDF bytes
API --> FE: application/pdf blob
FE --> User: PDF download başlar

@enduml
```

---

## 👥 4. Users Sayfası

### Açıklama
Sistem kullanıcıları (admin'ler) yönetim sayfası. Kullanıcı oluşturma, düzenleme, silme işlemleri yapılır.

### Özellikler
- Kullanıcı listesi
- Yeni kullanıcı oluşturma (username, email, password, role)
- Kullanıcı düzenleme
- Kullanıcı silme
- Rol yönetimi (admin, viewer, analyst)

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/users` | GET | Tüm kullanıcıları listele |
| `/api/users/{id}` | GET | Tek kullanıcı detayı |
| `/api/users` | POST | Yeni kullanıcı oluştur |
| `/api/users/{id}` | PUT | Kullanıcı güncelle |
| `/api/users/{id}` | DELETE | Kullanıcı sil |

### PlantUML - Sequence Diagram
```plantuml
@startuml Users_Flow
actor Admin
participant "Users Page" as FE
participant "UsersController" as API
database "In-Memory Store" as MEM

== Kullanıcı Listesi ==
Admin -> FE: Sayfa aç
FE -> API: GET /api/users
API -> MEM: Get all users
MEM --> API: Users list
API --> FE: [{id, username, email, role, createdAt}]
FE --> Admin: Kullanıcı tablosu

== Yeni Kullanıcı Oluştur ==
Admin -> FE: "Add User" tıkla
Admin -> FE: Form doldur (username, email, password, role)
FE -> API: POST /api/users
activate API
API -> API: ValidatePasswordStrength()
API -> API: CreatePasswordHash()
API -> MEM: Save user
MEM --> API: Success
API --> FE: {id, username, email, role}
deactivate API
FE --> Admin: "User created" success message

== Kullanıcı Sil ==
Admin -> FE: Delete tıkla
FE -> API: DELETE /api/users/{id}
API -> MEM: Remove user
MEM --> API: Success
API --> FE: 200 OK
FE --> Admin: Tablo güncelle

@enduml
```

---

## 🤖 5. AI Behavioral Sayfası

### Açıklama
Yapay zeka tabanlı davranış analizi sayfası. Z-score ile anomali tespiti yapar, kullanıcı/kanal/departman bazlı risk analizi sunar.

### Özellikler
- Overview Dashboard (cachelendi - 5 dk)
- Entity Analysis (user, channel, department)
- Top Anomalies listesi
- Z-Score hesaplama (son 7/14/30 gün)
- Anomaly Level: Normal, Low, Medium, High, Critical

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/ai-behavioral/overview` | GET | Genel bakış (cached) |
| `/api/ai-behavioral/analyze` | POST | Entity analiz et |
| `/api/ai-behavioral/entity/{type}/{id}` | GET | Entity analizi getir |
| `/api/ai-behavioral/anomalies` | GET | Top anomaliler listesi |

### Z-Score Hesaplama Formülü
```
Z-Score = (X - μ) / σ
Burada:
- X = Güncel değer (incident count, max_matches, vb.)
- μ = Ortalama (mean)
- σ = Standart sapma (standard deviation)

Anomaly Levels:
- Normal: |z| < 1.5
- Low: 1.5 ≤ |z| < 2.0
- Medium: 2.0 ≤ |z| < 2.5
- High: 2.5 ≤ |z| < 3.0
- Critical: |z| ≥ 3.0
```

### PlantUML - Sequence Diagram
```plantuml
@startuml AI_Behavioral_Flow
actor User
participant "AI Behavioral Page" as FE
participant "AIBehavioralController" as API
participant "BehaviorEngineService" as ENGINE
participant "MemoryCache" as CACHE
database PostgreSQL as DB

User -> FE: Sayfa aç
FE -> API: GET /api/ai-behavioral/overview?lookbackDays=7
activate API

API -> CACHE: Check cache "ai-behavioral-overview-7"
alt Cache HIT
    CACHE --> API: Cached data
    API --> FE: Cached overview
else Cache MISS
    API -> ENGINE: AnalyzeOverviewAsync(7)
    ENGINE -> DB: SELECT * FROM incidents WHERE timestamp >= NOW() - 7 days
    DB --> ENGINE: Incidents
    
    ENGINE -> ENGINE: GroupBy user, channel, department
    ENGINE -> ENGINE: Calculate statistics (mean, stddev)
    ENGINE -> ENGINE: Calculate Z-Scores
    ENGINE -> ENGINE: Determine anomaly levels
    
    ENGINE --> API: AIBehavioralOverviewResponse
    API -> CACHE: Set cache (5 min TTL)
    API --> FE: Overview data
end
deactivate API

FE --> User: Dashboard render

== Entity Detay ==
User -> FE: "enesa" kullanıcısına tıkla
FE -> API: GET /api/ai-behavioral/entity/user/enesa?lookbackDays=7
API -> ENGINE: AnalyzeEntityAsync("user", "enesa", 7)
ENGINE -> DB: Get user incidents
ENGINE -> ENGINE: Calculate Z-scores
ENGINE --> API: Entity analysis
API --> FE: {entityType, entityId, riskScore, zScore, anomalyLevel}
FE --> User: Entity detail modal

@enduml
```

---

## ⚙️ 6. Settings Sayfası

### Açıklama
Sistem ayarları sayfası. Email bildirimleri, risk eşikleri, veritabanı bağlantı ayarları yapılır.

### Özellikler
- Email Notification Settings (SMTP)
- Risk Thresholds (High, Medium, Low)
- Database Connection Test
- Test Email gönderme

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/settings` | GET | Tüm ayarları getir |
| `/api/settings` | POST | Ayarları kaydet |
| `/api/settings/test-email` | POST | Test email gönder |

### PlantUML - Sequence Diagram
```plantuml
@startuml Settings_Flow
actor Admin
participant "Settings Page" as FE
participant "SettingsController" as API
participant "EmailService" as EMAIL
database PostgreSQL as DB

Admin -> FE: Settings sayfasını aç
FE -> API: GET /api/settings
API -> DB: SELECT * FROM app_settings
DB --> API: Settings data
API --> FE: {smtp_host, smtp_port, risk_thresholds, ...}
FE --> Admin: Ayarlar formu doldurulmuş göster

Admin -> FE: SMTP ayarlarını değiştir
Admin -> FE: "Save" tıkla
FE -> API: POST /api/settings
activate API
API -> DB: UPSERT app_settings
DB --> API: Success
API --> FE: {success: true}
deactivate API
FE --> Admin: "Settings saved" message

Admin -> FE: "Test Email" tıkla
FE -> API: POST /api/settings/test-email
API -> EMAIL: SendTestEmail(to_address)
EMAIL -> EMAIL: Connect to SMTP
EMAIL -> EMAIL: Send email
EMAIL --> API: Success/Fail
API --> FE: {success: true/false, message}
FE --> Admin: Test sonucu göster

@enduml
```

---

## 🧠 7. AI Settings Sayfası

### Açıklama
Azure OpenAI entegrasyon ayarları sayfası. API key, endpoint, model ayarları yapılır.

### Özellikler
- Azure OpenAI Endpoint
- API Key (şifrelenmiş saklanır)
- Deployment Name (gpt-4, gpt-35-turbo)
- Connection Test
- Model selection

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/ai-settings` | GET | AI ayarlarını getir |
| `/api/ai-settings` | POST | AI ayarlarını kaydet |
| `/api/ai-settings/test` | POST | Bağlantı testi |

### PlantUML - Sequence Diagram
```plantuml
@startuml AI_Settings_Flow
actor Admin
participant "AI Settings Page" as FE
participant "AISettingsController" as API
participant "AzureOpenAIService" as AI
participant "DataProtection" as CRYPTO
database PostgreSQL as DB

Admin -> FE: AI Settings aç
FE -> API: GET /api/ai-settings
API -> DB: SELECT * FROM app_settings WHERE key LIKE 'ai.%'
DB --> API: Encrypted settings
API -> CRYPTO: Decrypt API key
CRYPTO --> API: Decrypted (masked for response)
API --> FE: {endpoint, deployment, apiKey: "****"}
FE --> Admin: Form göster

Admin -> FE: API Key güncelle
Admin -> FE: "Save" tıkla
FE -> API: POST /api/ai-settings {endpoint, deployment, apiKey}
API -> CRYPTO: Encrypt API key
CRYPTO --> API: Encrypted key
API -> DB: UPSERT settings
DB --> API: Success
API --> FE: {success: true}
FE --> Admin: "Saved" message

Admin -> FE: "Test Connection" tıkla
FE -> API: POST /api/ai-settings/test
API -> AI: TestConnectionAsync()
AI -> AI: Call Azure OpenAI API (simple prompt)
alt Connection Success
    AI --> API: {success: true, model: "gpt-4"}
    API --> FE: Connection successful
else Connection Failed
    AI --> API: {success: false, error: "Invalid API key"}
    API --> FE: Error message
end
FE --> Admin: Test sonucu

@enduml
```

---

## 📝 8. Logs Sayfası

### Açıklama
Sistem logları görüntüleme sayfası. Audit logları ve uygulama loglarını gösterir.

### Özellikler
- Audit Logs (kullanıcı işlemleri)
- Application Logs (sistem logları)
- Event Type filtreleme
- Tarih aralığı filtreleme
- Pagination

### API Endpoints

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/api/logs/audit` | GET | Audit logları (paginated) |
| `/api/logs/audit/event-types` | GET | Event tipleri listesi |
| `/api/logs/application` | GET | Uygulama logları |
| `/api/logs/application/collector` | POST | Collector log kaydet (internal) |

### PlantUML - Sequence Diagram
```plantuml
@startuml Logs_Flow
actor Admin
participant "Logs Page" as FE
participant "LogsController" as API
participant "AuditLogService" as SVC
database PostgreSQL as DB

Admin -> FE: Logs sayfasını aç
FE -> API: GET /api/logs/audit/event-types
API -> SVC: GetDistinctEventTypesAsync()
SVC -> DB: SELECT DISTINCT event_type FROM audit_logs
DB --> SVC: Event types
SVC --> API: ["Login", "Logout", "SettingsChange", "CollectorService"]
API --> FE: Event types list
FE --> Admin: Event type dropdown doldur

FE -> API: GET /api/logs/audit?page=1&pageSize=100
API -> SVC: GetAuditLogsAsync(page, pageSize)
SVC -> DB: SELECT * FROM audit_logs ORDER BY timestamp DESC LIMIT 100
DB --> SVC: Logs
SVC --> API: Paginated logs
API --> FE: {logs, total, page, pageSize, totalPages}
FE --> Admin: Logs tablosu

== Filtreleme ==
Admin -> FE: eventType="Login" seç
FE -> API: GET /api/logs/audit?eventType=Login&page=1
API -> SVC: GetAuditLogsAsync(eventType="Login")
SVC -> DB: SELECT * FROM audit_logs WHERE event_type='Login'
DB --> SVC: Filtered logs
SVC --> API: Logs
API --> FE: Filtered logs
FE --> Admin: Filtrelenmiş tablo

@enduml
```

---

## 🏗️ Genel Sistem Mimarisi

### PlantUML - Component Diagram
```plantuml
@startuml System_Architecture
skinparam componentStyle rectangle

package "Frontend (Next.js)" {
    [Dashboard] as DASH
    [Investigation] as INV
    [Reports] as REP
    [Users] as USR
    [AI Behavioral] as AIB
    [Settings] as SET
    [AI Settings] as AIS
    [Logs] as LOG
}

package "Backend (.NET 8 API)" {
    [RiskController] as RC
    [IncidentsController] as IC
    [ReportsController] as RPC
    [UsersController] as UC
    [AIBehavioralController] as ABC
    [SettingsController] as SC
    [AISettingsController] as ASC
    [LogsController] as LC
}

package "Services" {
    [RiskAnalyzerService] as RAS
    [BehaviorEngineService] as BES
    [ReportGeneratorService] as RGS
    [EmailService] as ES
    [AuditLogService] as ALS
    [AzureOpenAIService] as AOS
}

package "Data Layer" {
    [AnalyzerDbContext] as CTX
    [IncidentRepository] as IR
}

database "PostgreSQL" as PG {
    [incidents] as INC_T
    [app_settings] as SET_T
    [audit_logs] as LOG_T
    [ai_analysis] as AI_T
}

cloud "Azure OpenAI" as AZURE

DASH --> RC
DASH --> IC
INV --> RC
REP --> RPC
USR --> UC
AIB --> ABC
SET --> SC
AIS --> ASC
LOG --> LC

RC --> RAS
IC --> IR
RPC --> RGS
ABC --> BES
SC --> ES
ASC --> AOS
LC --> ALS

RAS --> CTX
RGS --> RAS
BES --> CTX
ALS --> CTX

CTX --> PG
AOS --> AZURE

@enduml
```

---

## 🔄 Risk Score Hesaplama

### Formül
```
BaseScore = (PolicyRepeatCount × 2) + (DataSensitivity × 2) + MaxMatchesTier
FinalScore = BaseScore × ActionMultiplier
Score = min(100, FinalScore)

MaxMatchesTier:
- 1-4: 10
- 5-19: 20
- 20-99: 40
- 100+: 60

ActionMultiplier:
- BLOCK/QUARANTINE: 1.0
- AUTHORIZED/RELEASED: 0.2
```

### PlantUML - Activity Diagram
```plantuml
@startuml Risk_Score_Calculation
start
:Incident alındı;
:PolicyRepeatCount hesapla\n(Aynı policy için önceki ihlal sayısı);
:DataSensitivity hesapla\n(Unique classifier count);
:MaxMatchesTier belirle;

if (maxMatches >= 100) then (yes)
    :tier = 60;
elseif (maxMatches >= 20) then (yes)
    :tier = 40;
elseif (maxMatches >= 5) then (yes)
    :tier = 20;
else (1-4)
    :tier = 10;
endif

:repeatScore = min(20, repeatCount) × 2;
:sensitivityScore = dataSensitivity × 2;
:baseScore = repeatScore + sensitivityScore + tier;

if (action == BLOCK or QUARANTINE) then (yes)
    :multiplier = 1.0;
else (AUTHORIZED/RELEASED)
    :multiplier = 0.2;
endif

:finalScore = baseScore × multiplier;
:score = min(100, finalScore);

:Risk Level belirle;
if (score >= 70) then (yes)
    :riskLevel = "High" (Kırmızı);
elseif (score >= 30) then (yes)
    :riskLevel = "Medium" (Turuncu);
else (0-29)
    :riskLevel = "Low" (Yeşil);
endif

:Score ve RiskLevel kaydet;
stop
@enduml
```

---

## 📌 Notlar

1. **Pagination:** Action modal'da server-side pagination uygulandı (100 kayıt/sayfa, max 500)
2. **Caching:** AI Behavioral overview 5 dakika cache'lenir
3. **Security:** API key'ler DataProtection ile şifrelenir
4. **Performance:** Investigation Top Users risk skoruna göre sıralanır
