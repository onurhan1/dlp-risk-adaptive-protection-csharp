# DLP Risk Analyzer - Mermaid Diyagramları

> **Sunum için:** [Mermaid Live Editor](https://mermaid.live/) üzerinden diyagram kodunu yapıştırın ve PNG/SVG olarak indirin.

---

## 1. Dashboard Flow

```mermaid
sequenceDiagram
    actor User
    participant FE as Dashboard Page
    participant API as RiskController
    participant INC as IncidentsController
    participant DB as PostgreSQL

    User->>FE: Sayfa açılır
    activate FE
    
    par Paralel API Çağrıları
        FE->>API: GET /api/risk/action-summary
        API->>DB: Action counts query
        DB-->>API: Counts
        API-->>FE: {authorized, block, quarantine, total}
    and
        FE->>API: GET /api/risk/daily-summary?days=30
        API->>DB: Daily aggregation
        DB-->>API: Daily stats
        API-->>FE: [{date, total_incidents, high_risk_count}]
    and
        FE->>INC: GET /api/incidents?limit=5000
        INC->>DB: Select incidents
        DB-->>INC: Incidents list
        INC-->>FE: Incidents data
    end

    FE->>FE: Top Users hesapla (client-side)
    FE->>FE: Top Rules hesapla (client-side)
    FE-->>User: Dashboard render

    User->>FE: BLOCK kartına tıkla
    FE->>API: GET /api/risk/incidents/by-action?action=BLOCK
    API->>DB: Paginated query
    DB-->>API: {items, totalCount, totalPages}
    API-->>FE: Paginated response
    FE-->>User: Modal ile incident listesi göster
```

---

## 2. Investigation Flow

```mermaid
sequenceDiagram
    actor User
    participant FE as Investigation Page
    participant API as RiskController
    participant SVC as RiskAnalyzerService
    participant DB as PostgreSQL

    User->>FE: Tarih aralığı seç
    activate FE

    FE->>API: GET /api/risk/top-users-daily?startDate=X&endDate=Y&limit=20
    activate API
    API->>SVC: GetTopUsersByDayAsync(startDate, endDate)
    SVC->>DB: SELECT user_email, MAX(risk_score), COUNT(*)<br/>FROM incidents WHERE timestamp BETWEEN X AND Y<br/>GROUP BY user_email ORDER BY risk_score DESC
    DB-->>SVC: User stats
    SVC-->>API: Top users list
    API-->>FE: [{user_email, risk_score, total_alerts}]
    deactivate API

    FE->>API: GET /api/risk/top-rules-daily?startDate=X&endDate=Y
    API->>SVC: GetTopRulesByDayAsync()
    SVC->>DB: SELECT policy, COUNT(*) FROM incidents GROUP BY policy
    DB-->>SVC: Rule stats
    SVC-->>API: Top rules list
    API-->>FE: [{rule_name, total_alerts, unique_users}]

    FE-->>User: Investigation data render
```

---

## 3. Reports Flow

```mermaid
sequenceDiagram
    actor User
    participant FE as Reports Page
    participant API as ReportsController
    participant SVC as RiskAnalyzerService
    participant PDF as ReportGeneratorService
    participant DB as PostgreSQL

    User->>FE: Tarih seç (2026-01-12)
    activate FE

    FE->>API: GET /api/reports/daily-summary?date=2026-01-12
    activate API
    API->>SVC: GetDailyReportDataAsync(date)
    SVC->>DB: Query incidents for date
    DB-->>SVC: Daily incidents

    SVC->>SVC: Calculate action_summary
    SVC->>SVC: Calculate top_users
    SVC->>SVC: Calculate channel_breakdown
    SVC->>SVC: Calculate top_policies

    SVC-->>API: DailyReportData
    API-->>FE: {action_summary, top_users, channel_breakdown}
    deactivate API

    FE-->>User: Report display

    User->>FE: Download PDF butonuna tıkla
    FE->>API: GET /api/reports/daily-summary/pdf?date=2026-01-12
    API->>PDF: GenerateDailyReportPdf(date)
    PDF->>SVC: GetDailyReportDataAsync(date)
    SVC-->>PDF: Data
    PDF->>PDF: Create PDF with iTextSharp
    PDF-->>API: PDF bytes
    API-->>FE: application/pdf blob
    FE-->>User: PDF download başlar
```

---

## 4. Users Flow

```mermaid
sequenceDiagram
    actor Admin
    participant FE as Users Page
    participant API as UsersController
    participant MEM as In-Memory Store

    rect rgb(200, 220, 255)
        Note over Admin,MEM: Kullanıcı Listesi
        Admin->>FE: Sayfa aç
        FE->>API: GET /api/users
        API->>MEM: Get all users
        MEM-->>API: Users list
        API-->>FE: [{id, username, email, role, createdAt}]
        FE-->>Admin: Kullanıcı tablosu
    end

    rect rgb(200, 255, 200)
        Note over Admin,MEM: Yeni Kullanıcı Oluştur
        Admin->>FE: Add User tıkla
        Admin->>FE: Form doldur
        FE->>API: POST /api/users
        activate API
        API->>API: ValidatePasswordStrength()
        API->>API: CreatePasswordHash()
        API->>MEM: Save user
        MEM-->>API: Success
        API-->>FE: {id, username, email, role}
        deactivate API
        FE-->>Admin: User created success
    end

    rect rgb(255, 200, 200)
        Note over Admin,MEM: Kullanıcı Sil
        Admin->>FE: Delete tıkla
        FE->>API: DELETE /api/users/{id}
        API->>MEM: Remove user
        MEM-->>API: Success
        API-->>FE: 200 OK
        FE-->>Admin: Tablo güncelle
    end
```

---

## 5. AI Behavioral Flow

```mermaid
sequenceDiagram
    actor User
    participant FE as AI Behavioral Page
    participant API as AIBehavioralController
    participant ENGINE as BehaviorEngineService
    participant CACHE as MemoryCache
    participant DB as PostgreSQL

    User->>FE: Sayfa aç
    FE->>API: GET /api/ai-behavioral/overview?lookbackDays=7
    activate API

    API->>CACHE: Check cache "ai-behavioral-overview-7"
    alt Cache HIT
        CACHE-->>API: Cached data
        API-->>FE: Cached overview
    else Cache MISS
        API->>ENGINE: AnalyzeOverviewAsync(7)
        ENGINE->>DB: SELECT * FROM incidents WHERE timestamp >= NOW() - 7 days
        DB-->>ENGINE: Incidents
        
        ENGINE->>ENGINE: GroupBy user, channel, department
        ENGINE->>ENGINE: Calculate statistics (mean, stddev)
        ENGINE->>ENGINE: Calculate Z-Scores
        ENGINE->>ENGINE: Determine anomaly levels
        
        ENGINE-->>API: AIBehavioralOverviewResponse
        API->>CACHE: Set cache (5 min TTL)
        API-->>FE: Overview data
    end
    deactivate API

    FE-->>User: Dashboard render

    rect rgb(255, 245, 200)
        Note over User,DB: Entity Detay
        User->>FE: "enesa" kullanıcısına tıkla
        FE->>API: GET /api/ai-behavioral/entity/user/enesa?lookbackDays=7
        API->>ENGINE: AnalyzeEntityAsync("user", "enesa", 7)
        ENGINE->>DB: Get user incidents
        ENGINE->>ENGINE: Calculate Z-scores
        ENGINE-->>API: Entity analysis
        API-->>FE: {entityType, entityId, riskScore, zScore, anomalyLevel}
        FE-->>User: Entity detail modal
    end
```

---

## 6. Settings Flow

```mermaid
sequenceDiagram
    actor Admin
    participant FE as Settings Page
    participant API as SettingsController
    participant EMAIL as EmailService
    participant DB as PostgreSQL

    Admin->>FE: Settings sayfasını aç
    FE->>API: GET /api/settings
    API->>DB: SELECT * FROM app_settings
    DB-->>API: Settings data
    API-->>FE: {smtp_host, smtp_port, risk_thresholds}
    FE-->>Admin: Ayarlar formu doldurulmuş göster

    Admin->>FE: SMTP ayarlarını değiştir
    Admin->>FE: Save tıkla
    FE->>API: POST /api/settings
    activate API
    API->>DB: UPSERT app_settings
    DB-->>API: Success
    API-->>FE: {success: true}
    deactivate API
    FE-->>Admin: Settings saved message

    Admin->>FE: Test Email tıkla
    FE->>API: POST /api/settings/test-email
    API->>EMAIL: SendTestEmail(to_address)
    EMAIL->>EMAIL: Connect to SMTP
    EMAIL->>EMAIL: Send email
    EMAIL-->>API: Success/Fail
    API-->>FE: {success: true/false, message}
    FE-->>Admin: Test sonucu göster
```

---

## 7. AI Settings Flow

```mermaid
sequenceDiagram
    actor Admin
    participant FE as AI Settings Page
    participant API as AISettingsController
    participant AI as AzureOpenAIService
    participant CRYPTO as DataProtection
    participant DB as PostgreSQL

    Admin->>FE: AI Settings aç
    FE->>API: GET /api/ai-settings
    API->>DB: SELECT * FROM app_settings WHERE key LIKE 'ai.%'
    DB-->>API: Encrypted settings
    API->>CRYPTO: Decrypt API key
    CRYPTO-->>API: Decrypted (masked for response)
    API-->>FE: {endpoint, deployment, apiKey: "****"}
    FE-->>Admin: Form göster

    Admin->>FE: API Key güncelle
    Admin->>FE: Save tıkla
    FE->>API: POST /api/ai-settings {endpoint, deployment, apiKey}
    API->>CRYPTO: Encrypt API key
    CRYPTO-->>API: Encrypted key
    API->>DB: UPSERT settings
    DB-->>API: Success
    API-->>FE: {success: true}
    FE-->>Admin: Saved message

    Admin->>FE: Test Connection tıkla
    FE->>API: POST /api/ai-settings/test
    API->>AI: TestConnectionAsync()
    AI->>AI: Call Azure OpenAI API
    alt Connection Success
        AI-->>API: {success: true, model: "gpt-4"}
        API-->>FE: Connection successful
    else Connection Failed
        AI-->>API: {success: false, error: "Invalid API key"}
        API-->>FE: Error message
    end
    FE-->>Admin: Test sonucu
```

---

## 8. Logs Flow

```mermaid
sequenceDiagram
    actor Admin
    participant FE as Logs Page
    participant API as LogsController
    participant SVC as AuditLogService
    participant DB as PostgreSQL

    Admin->>FE: Logs sayfasını aç
    FE->>API: GET /api/logs/audit/event-types
    API->>SVC: GetDistinctEventTypesAsync()
    SVC->>DB: SELECT DISTINCT event_type FROM audit_logs
    DB-->>SVC: Event types
    SVC-->>API: ["Login", "Logout", "SettingsChange"]
    API-->>FE: Event types list
    FE-->>Admin: Event type dropdown doldur

    FE->>API: GET /api/logs/audit?page=1&pageSize=100
    API->>SVC: GetAuditLogsAsync(page, pageSize)
    SVC->>DB: SELECT * FROM audit_logs ORDER BY timestamp DESC LIMIT 100
    DB-->>SVC: Logs
    SVC-->>API: Paginated logs
    API-->>FE: {logs, total, page, pageSize, totalPages}
    FE-->>Admin: Logs tablosu

    rect rgb(200, 220, 255)
        Note over Admin,DB: Filtreleme
        Admin->>FE: eventType="Login" seç
        FE->>API: GET /api/logs/audit?eventType=Login&page=1
        API->>SVC: GetAuditLogsAsync(eventType="Login")
        SVC->>DB: SELECT * FROM audit_logs WHERE event_type='Login'
        DB-->>SVC: Filtered logs
        SVC-->>API: Logs
        API-->>FE: Filtered logs
        FE-->>Admin: Filtrelenmiş tablo
    end
```

---

## 9. System Architecture (Component Diagram)

```mermaid
flowchart TB
    subgraph Frontend ["Frontend (Next.js)"]
        DASH[Dashboard]
        INV[Investigation]
        REP[Reports]
        USR[Users]
        AIB[AI Behavioral]
        SET[Settings]
        AIS[AI Settings]
        LOG[Logs]
    end

    subgraph Backend ["Backend (.NET 8 API)"]
        RC[RiskController]
        IC[IncidentsController]
        RPC[ReportsController]
        UC[UsersController]
        ABC[AIBehavioralController]
        SC[SettingsController]
        ASC[AISettingsController]
        LC[LogsController]
    end

    subgraph Services
        RAS[RiskAnalyzerService]
        BES[BehaviorEngineService]
        RGS[ReportGeneratorService]
        ES[EmailService]
        ALS[AuditLogService]
        AOS[AzureOpenAIService]
    end

    subgraph DataLayer ["Data Layer"]
        CTX[AnalyzerDbContext]
        IR[IncidentRepository]
    end

    subgraph Database ["PostgreSQL"]
        INC_T[(incidents)]
        SET_T[(app_settings)]
        LOG_T[(audit_logs)]
        AI_T[(ai_analysis)]
    end

    AZURE[("☁️ Azure OpenAI")]

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

    CTX --> INC_T
    CTX --> SET_T
    CTX --> LOG_T
    CTX --> AI_T
    AOS --> AZURE
```

---

## 10. Risk Score Calculation (SLAYT İÇİN KOMPAKT)

```mermaid
flowchart LR
    A[🎯 Incident] --> B[Hesapla]
    
    B --> C{Tier}
    C -->|≥100| D[60]
    C -->|≥20| E[40]
    C -->|≥5| F[20]
    C -->|1-4| G[10]
    
    D & E & F & G --> H[Base Score]
    
    H --> I{Action}
    I -->|BLOCK| J[×1.0]
    I -->|AUTH| K[×0.2]
    
    J & K --> L[Final]
    
    L --> M{Risk?}
    M -->|≥70| N[🔴 HIGH]
    M -->|≥30| O[🟠 MED]
    M -->|<30| P[🟢 LOW]

    style N fill:#ef4444,color:#fff
    style O fill:#f97316,color:#fff
    style P fill:#22c55e,color:#fff
    style A fill:#3b82f6,color:#fff
```

---

## 10b. Risk Score (KARE FORMAT - 16:9 Slayt)

```mermaid
graph TD
    subgraph row1 [" "]
        A[🎯 Incident Alındı]
    end
    
    subgraph row2 [" "]
        B[RepeatCount × 2] 
        C[Sensitivity × 2]
        D[Tier: 10-60]
    end
    
    subgraph row3 [" "]
        E[Base Score = B + C + D]
    end
    
    subgraph row4 [" "]
        F[BLOCK ×1.0]
        G[AUTH ×0.2]
    end
    
    subgraph row5 [" "]
        H[Final Score]
    end
    
    subgraph row6 [" "]
        I[🔴 HIGH ≥70]
        J[🟠 MED 30-69]
        K[🟢 LOW 0-29]
    end

    A --> B & C & D
    B & C & D --> E
    E --> F & G
    F & G --> H
    H --> I & J & K

    style I fill:#ef4444,color:#fff
    style J fill:#f97316,color:#fff  
    style K fill:#22c55e,color:#fff
    style A fill:#3b82f6,color:#fff
    style H fill:#8b5cf6,color:#fff
```

---

## 10c. Risk Score (EN KOMPAKT - Tek Satır)

```mermaid
flowchart LR
    A((📥)) --> B[Tier<br/>10-60] --> C[Base<br/>Score] --> D{×1.0<br/>×0.2} --> E[Final] --> F((📤))
    
    F --> G[🔴]
    F --> H[🟠]
    F --> I[🟢]

    style G fill:#ef4444,color:#fff
    style H fill:#f97316,color:#fff
    style I fill:#22c55e,color:#fff
```

---

## 📌 Kullanım Kılavuzu

### Sunum için PNG/SVG İndirme

1. **Mermaid Live Editor'ı açın:** https://mermaid.live/
2. Yukarıdaki kod bloklarından birini kopyalayın
3. Editor'a yapıştırın
4. Sağ üstteki **Actions → Export as PNG/SVG** seçin
5. İndirilen dosyayı PowerPoint/Google Slides'a ekleyin

### GitHub'da Otomatik Render

GitHub markdown dosyalarında `mermaid` kod blokları otomatik olarak render edilir.

### VS Code'da Görüntüleme

"Markdown Preview Mermaid Support" eklentisini yükleyin.
