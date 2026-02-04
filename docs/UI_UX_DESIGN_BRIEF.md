# DLP Risk Adaptive Protection - UI/UX Design Brief (Güncel)
## Mevcut Sistem Analizi ve Tasarım Prompt'u

---

# 🎯 Proje Özeti

**Uygulama Adı:** RADAR - DLP Risk Adaptive Protection  
**Sektör:** Kurumsal Siber Güvenlik / Veri Kaybı Önleme  
**Hedef Kullanıcı:** SOC ekipleri, güvenlik analistleri, CISO, IT yöneticileri  
**Platform:** Web tabanlı dashboard (masaüstü öncelikli)

---

# 📂 Mevcut Sayfa Yapısı

## Sidebar Navigasyonu
```
📍 RADAR Logo (tema'ya göre değişir)
├── 🏠 Dashboard (/)
├── 🔍 Investigation (/investigation) [Admin Only]
├── 🤖 AI Behavioral (/ai-behavioral) [Admin Only]
├── 📊 Analytics (/analytics) [Admin Only]
├── ⚙️ Settings (/settings) [Admin Only]
├── ❓ F.A.Q (/faq) [Herkes]
└── 🔐 Login (/login)
```

---

# 📊 SAYFA 1: Dashboard (Ana Sayfa)

## Amaç
Günlük güvenlik durumunu tek bakışta görmek, hızlı aksiyon almak

## Yapı & Bileşenler

### Üst Bölüm - Date Range Picker
- Başlangıç/Bitiş tarihi seçimi
- Varsayılan: Son 30 gün

### Action Summary Kartları (4 adet)
| Kart | İkon | Renk | İçerik |
|------|------|------|--------|
| BLOCK | 🚫 | Kırmızı | Engellenen olay sayısı |
| QUARANTINE | ⏸️ | Turuncu | Karantinaya alınan |
| AUTHORIZED | ✅ | Yeşil | İzin verilen |
| RELEASED | 🔓 | Mavi | Serbest bırakılan |

**Etkileşim:** Karta tıklayınca → **ActionIncidentsModal** açılır

### ActionIncidentsModal (33KB - Büyük Modal)
- Sayfalandırmalı tablo (page, pageSize)
- Filtreleme: User, Destination, Channel, Policy, Rule
- Debounce ile arama
- Kolonlar: Login Name, Destination, Channel, Policy, Rule, Action, Timestamp, Max Matches
- Export seçenekleri

### Top 10 Riskli Kullanıcılar Tablosu
- Kullanıcı email/login name
- Risk skoru (0-100)
- Department
- Toplam alert sayısı

**Etkileşim:** Satıra tıklayınca → **UserInsightsModal** açılır

### UserInsightsModal (25KB)
- Kullanıcı profil özeti
- Risk skoru trendi grafiği
- Günlük incident detayları
- Action breakdown

### Günlük Trend Grafiği (Plotly.js)
- Son 7-30 günün trendi
- X: Tarih, Y: Incident sayısı
- Hover ile detay

**Etkileşim:** Güne tıklayınca → **HighRiskUsersModal** açılır

### HighRiskUsersModal (11KB)
- O gündeki yüksek riskli kullanıcılar
- Risk skorları
- Incident sayıları

### High Impact Alerts Bölümü
- Tek günde yüksek etkili olaylar
- Max matches yüksek olanlar
- Severity level göstergesi
- Expandable detay

### Channel Breakdown (Pie/Donut Chart)
- Email, USB, Cloud, LAN, Printer dağılımı
- Yüzdelik gösterim

### Top Policies/Rules
- En çok tetiklenen kurallar
- Alert sayıları

### Report Download Butonu
- PDF rapor indirme
- **ReportModal** ile rapor önizleme (38KB)

---

# 🔍 SAYFA 2: Investigation

## Amaç
Belirli bir kullanıcıyı veya olayı detaylı araştırmak

## Yapı & Bileşenler

### Sol Panel - Kullanıcı Listesi
- **InvestigationUsersList** component
- Riskli kullanıcılar listesi
- Risk skoru renk kodlaması
- Arama/filtreleme

### Orta Panel - Timeline View
- **InvestigationTimeline** component (19KB)
- Kronolojik olay listesi
- Her olay: timestamp, severity, channel, action
- Tag'ler: policy, rule, classification
- Severity renkleri: Critical (kırmızı), High (turuncu), Medium (sarı), Low (yeşil)

### Sağ Panel - Alert Details
- **InvestigationAlertDetails** component (21KB)
- Seçilen olayın tam detayı:
  - File name, size
  - Source application
  - Email subject, recipients
  - Violation triggers (JSON parse)
  - IOB number
  - Classification listesi
  - Matched rules

### Floating Modal
- **UserInsightsModal** - Kullanıcı profil detayı

---

# 🤖 SAYFA 3: AI Behavioral Analysis

## Amaç
AI tarafından analiz edilen kullanıcı davranışlarını görmek, anomali tespiti

## Yapı & Bileşenler

### Özet Kartları (4 adet)
- Total Analyzed
- High Anomaly Count
- Medium Anomaly Count  
- Low Anomaly Count

### Filtreler
- Entity Type (User/Channel/Department)
- Anomaly Level
- Show AI Analyzed Only toggle

### Entity Tablosu
- Entity ID (kullanıcı email)
- Risk Score
- Anomaly Level (badge)
- AI Explanation (truncated)
- Analysis Date

**Etkileşim:** Satıra tıklayınca → **EntityDetailModal** açılır (71KB - En büyük component)

### EntityDetailModal İçeriği
- Risk skoru göstergesi
- AI Analysis Summary
- Weekly Incidents Chart (bar chart)
- Daily breakdown (tablo)
- AI Recommendation
- Incident details listesi
- Action counts (Block/Quarantine/Authorized)

---

# 📊 SAYFA 4: Analytics

## Amaç
Veri analitiği, domain yönetimi, heatmap görünümü

## Yapı & Bileşenler

### Date Range Filter
- Başlangıç/Bitiş tarihi

### Heatmap Görünümü
- Domain × Day matrisi
- Renk yoğunluğu: incident sayısı
- Hover ile detay

### Domain Features Manager (39KB)
- **DomainFeaturesManager** component
- Domain listesi tablosu
- Her domain için özellikler:
  - Gizlilik Sözleşmesi (Evet/Hayır)
  - Eğitim
  - Noterlik
  - Denetim
  - Banka
  - Hukuk Firması
  - İştirak
- CSV yükleme
- Manuel düzenleme

### İstatistikler
- Toplam unique domain sayısı
- Incident dağılımı

---

# ⚙️ SAYFA 5: Settings

## Amaç
Sistem konfigürasyonu, entegrasyonlar

## Tab Yapısı (6 sekme)

### Tab 1: Genel Ayarlar
- Email notifications toggle
- Daily report time
- Risk thresholds (Low/Medium/High)
- Admin email

### Tab 2: DLP API Ayarları
- Manager IP
- Manager Port
- HTTPS toggle
- Timeout
- Username/Password
- Test Connection butonu
- Save butonu

### Tab 3: Email (SMTP) Ayarları
- SMTP Host/Port
- SSL toggle
- Username/Password
- From Email/Name
- Test Email butonu

### Tab 4: Splunk Entegrasyonu
- Enabled toggle
- HEC URL
- HEC Token (masked)
- Index, Source, Sourcetype
- Test Connection

### Tab 5: Users (Kullanıcı Yönetimi)
- **UsersTab** component
- Kullanıcı listesi
- Rol yönetimi (Admin/User)
- CRUD işlemleri

### Tab 6: AI Settings
- **AISettingsTab** component
- Azure OpenAI endpoint
- API Key (masked)
- Model seçimi
- Max tokens, Temperature
- Test butonu

### Tab 7: Logs
- **LogsTab** component
- Sistem logları
- Tarih/seviye filtresi
- Sayfalandırma

---

# ❓ SAYFA 6: FAQ

## Amaç
Kullanıcı rehberi, SSS

## Yapı & Bileşenler

### Dil Seçici
- Türkçe / English toggle

### İçerik Bölümleri
- Accordion tarzı Q&A
- SectionTitle, SubSection components
- Tablolar (risk seviyeleri, kanallar)
- Başlangıç rehberi
- Risk skoru açıklaması
- Kanal açıklamaları

---

# 🎨 Tasarım Dili

## Renk Paleti
```css
/* Risk Seviyeleri */
--risk-critical: #E53E3E;  /* 76-100 */
--risk-high: #DD6B20;      /* 51-75 */
--risk-medium: #D69E2E;    /* 26-50 */
--risk-low: #38A169;       /* 0-25 */

/* UI */
--primary: #2C5282;
--accent: #3182CE;
--bg-dark: #0D1117;
--bg-card: #161B22;
--border: #30363D;
--text: #E6EDF3;
--text-muted: #8B949E;
```

## Layout
```
┌─────────────────────────────────────────────────────┐
│  Sidebar (80px)  │       Main Content (fluid)       │
│                  │                                  │
│  ┌──────────┐   │  ┌────────────────────────────┐  │
│  │   LOGO   │   │  │  Header + Date Picker      │  │
│  └──────────┘   │  ├────────────────────────────┤  │
│  ┌──────────┐   │  │                            │  │
│  │  Icons   │   │  │  Card Grid                 │  │
│  │    +     │   │  │  (4 col responsive)        │  │
│  │  Labels  │   │  │                            │  │
│  │          │   │  ├────────────────────────────┤  │
│  │          │   │  │                            │  │
│  │          │   │  │  Tables / Charts           │  │
│  │          │   │  │                            │  │
│  └──────────┘   │  └────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

## Bileşen Stilleri
- **Kartlar:** Rounded corners, subtle shadow, border
- **Tablolar:** Zebra striping, hover highlight
- **Modaller:** Centered, overlay, max-width 90vw
- **Butonlar:** Filled primary, outline secondary
- **Inputs:** Dark background, focus ring
- **Badges:** Pill shape, color-coded

---

# 📋 Tasarım Prompt'u (AI Design Tools için)

```
Design a professional cybersecurity dashboard called "RADAR - DLP Risk Adaptive Protection System" for enterprise security analysts.

STYLE:
- Dark mode with #0D1117 background
- Cards with #161B22 background and #30363D borders
- Risk color coding: Red (critical), Orange (high), Yellow (medium), Green (low)
- Clean, data-dense layout with minimal whitespace
- Modern sans-serif typography (Inter or similar)
- Icon-based sidebar navigation (80px width)

MAIN DASHBOARD PAGE:
1. Header with logo "RADAR" and date range picker
2. Four action summary cards in a row: BLOCK (red), QUARANTINE (orange), AUTHORIZED (green), RELEASED (blue)
3. Top 10 Risky Users table with columns: User, Risk Score (0-100 with color badge), Department, Alerts
4. Daily trend line chart (Plotly style)
5. High Impact Alerts section with expandable items
6. Channel breakdown pie chart (Email, USB, Cloud, Printer, LAN)

MODALS:
1. Action Incidents Modal: Large table with filters (User, Destination, Channel, Policy), pagination
2. User Insights Modal: Profile header, risk trend chart, activity breakdown
3. Entity Detail Modal: AI analysis summary, weekly incidents bar chart, recommendations

OTHER PAGES:
1. Investigation: 3-column layout (user list | timeline | alert details)
2. AI Behavioral: Entity table with anomaly badges, expandable AI explanations
3. Analytics: Heatmap grid, domain features table with Yes/No badges
4. Settings: Tab-based (General, DLP API, Email, Splunk, Users, AI, Logs)
5. FAQ: Accordion-style Q&A with Turkish/English toggle

INTERACTIONS:
- Card click opens modal
- Table row click shows detail
- Hover states on all interactive elements
- Loading skeletons for data fetching
- Toast notifications for success/error

Make it look premium, professional, and suitable for a bank's security operations center.
```

---

## 📱 Responsive Breakpoints

| Viewport | Sidebar | Grid Cols | Notes |
|----------|---------|-----------|-------|
| Desktop (>1280px) | Visible | 4 | Full layout |
| Tablet (768-1279px) | Collapsed | 2 | Hamburger menu |
| Mobile (<768px) | Hidden | 1 | Bottom nav |

---

## 🔗 Component Dependency Map

```
Dashboard (page.tsx)
├── ActionIncidentsModal
├── HighRiskUsersModal
├── UserInsightsModal
├── ReportModal
└── Plot (Plotly.js)

Investigation (page.tsx)
├── InvestigationUsersList
├── InvestigationTimeline
├── InvestigationAlertDetails
└── UserInsightsModal

AI Behavioral (page.tsx)
└── EntityDetailModal

Analytics (page.tsx)
└── DomainFeaturesManager

Settings (page.tsx)
├── UsersTab
├── AISettingsTab
└── LogsTab
```

---

**Son Güncelleme:** 2026-02-04  
**Aktif Sayfa Sayısı:** 6 (+ Login)  
**Toplam Component:** 25
