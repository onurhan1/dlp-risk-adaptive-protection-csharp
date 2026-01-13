# DLP Risk Adaptive Protection - Sunum Slaytları

> **Kullanım:** Her `---` bir slayt ayrımını temsil eder. `[DIAGRAM]` etiketleri diyagram eklemeniz gereken yerleri gösterir.

---

## SLAYT 1: Kapak

# DLP Risk Adaptive Protection

### Yapay Zeka Destekli Veri Kaybı Önleme ve Risk Analiz Sistemi

**Hazırlayan:** [İsminiz]  
**Tarih:** Ocak 2026

---

## SLAYT 2: Gündem

1. Proje Tanıtımı
2. Sistem Mimarisi
3. Teknoloji Yığını
4. Modüller ve Özellikler
5. Risk Skoru Hesaplama
6. AI Davranış Analizi
7. Demo & Sonuç

---

## SLAYT 3: Problem Tanımı

### Kuruluşların Karşılaştığı Zorluklar

- 📊 **Veri Hacmi:** Günlük binlerce DLP olayı
- ⏱️ **Zaman Kaybı:** Manuel olay inceleme
- 🎯 **Önceliklendirme:** Hangi olaylar kritik?
- 🔍 **Görünürlük Eksikliği:** Kullanıcı davranış kalıpları
- 📈 **Trend Analizi:** Geçmiş verilerden öğrenme

---

## SLAYT 4: Çözümümüz

### DLP Risk Adaptive Protection

✅ **Otomatik Risk Skorlama** - Her olay için 0-100 arası skor  
✅ **Davranış Analizi** - Z-Score ile anomali tespiti  
✅ **Gerçek Zamanlı Dashboard** - Anlık görünürlük  
✅ **AI Entegrasyonu** - Azure OpenAI ile akıllı öneriler  
✅ **Raporlama** - PDF ve görsel raporlar  

---

## SLAYT 5: Sistem Mimarisi

### Genel Bakış

```
[DIAGRAM: System Architecture - MERMAID_DIAGRAMS.md #9]
```

> **Eklenecek:** Component diagram - System Architecture flowchart

---

## SLAYT 6: Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| **Frontend** | Next.js 14, React, TypeScript |
| **Backend** | .NET 8, C# Web API |
| **Veritabanı** | PostgreSQL |
| **Cache/Queue** | Redis Streams |
| **AI** | Azure OpenAI (GPT-4) |
| **Raporlama** | iTextSharp PDF |

---

## SLAYT 7: Veri Akışı

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  Forcepoint │────▶│   Redis     │────▶│  Analyzer   │────▶│ PostgreSQL  │
│     DLP     │     │   Stream    │     │   Service   │     │   Database  │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
                                              │
                                              ▼
                                        ┌─────────────┐
                                        │  Dashboard  │
                                        │  (Next.js)  │
                                        └─────────────┘
```

---

## SLAYT 8: Dashboard Modülü

### Ana Kontrol Paneli

**Özellikler:**
- Action Summary kartları (Block, Quarantine, Authorized)
- Top 20 Riskli Kullanıcılar
- Son 30 gün veri hareketi grafiği
- En çok tetiklenen kurallar
- Günlük incident trend tablosu

```
[DIAGRAM: Dashboard Flow - MERMAID_DIAGRAMS.md #1]
```

---

## SLAYT 9: Dashboard - Ekran Görüntüsü

```
[SCREENSHOT: Dashboard sayfası ekran görüntüsü ekleyin]
```

---

## SLAYT 10: Investigation Modülü

### Detaylı Araştırma

**Özellikler:**
- Tarih aralığı seçimi
- Top 20 Risky Users
- Top Alerts (kural bazlı)
- Timeline grafiği

```
[DIAGRAM: Investigation Flow - MERMAID_DIAGRAMS.md #2]
```

---

## SLAYT 11: Reports Modülü

### Raporlama Sistemi

**Özellikler:**
- Günlük detaylı raporlar
- Action Summary
- Top 10 Users/Policies/Destinations
- Channel Breakdown
- **PDF Export**

```
[DIAGRAM: Reports Flow - MERMAID_DIAGRAMS.md #3]
```

---

## SLAYT 12: Kullanıcı Yönetimi

### Users Modülü

**Özellikler:**
- Admin kullanıcı yönetimi
- Rol tabanlı erişim (Admin, Viewer, Analyst)
- CRUD işlemleri
- Şifre güvenliği

```
[DIAGRAM: Users Flow - MERMAID_DIAGRAMS.md #4]
```

---

## SLAYT 13: Risk Skoru Hesaplama

### Algoritma

```
BaseScore = (PolicyRepeatCount × 2) + (DataSensitivity × 2) + MaxMatchesTier

FinalScore = BaseScore × ActionMultiplier

Score = min(100, FinalScore)
```

| MaxMatches | Tier | Action | Multiplier |
|------------|------|--------|------------|
| 1-4 | 10 | BLOCK/QUARANTINE | 1.0 |
| 5-19 | 20 | AUTHORIZED | 0.2 |
| 20-99 | 40 | RELEASED | 0.2 |
| 100+ | 60 | - | - |

---

## SLAYT 14: Risk Skoru - Akış Diyagramı

```
[DIAGRAM: Risk Score Calculation - MERMAID_DIAGRAMS.md #10]
```

---

## SLAYT 15: Risk Seviyeleri

| Skor Aralığı | Risk Seviyesi | Renk | Aksiyon |
|--------------|---------------|------|---------|
| 70-100 | 🔴 HIGH | Kırmızı | Acil inceleme |
| 30-69 | 🟠 MEDIUM | Turuncu | Takip |
| 0-29 | 🟢 LOW | Yeşil | Rutin |

---

## SLAYT 16: AI Behavioral Analysis

### Yapay Zeka Tabanlı Davranış Analizi

**Z-Score Formülü:**
```
Z-Score = (X - μ) / σ
```

**Anomaly Seviyeleri:**
| Z-Score | Seviye |
|---------|--------|
| < 1.5 | Normal |
| 1.5 - 2.0 | Low |
| 2.0 - 2.5 | Medium |
| 2.5 - 3.0 | High |
| ≥ 3.0 | Critical |

---

## SLAYT 17: AI Behavioral - Akış

```
[DIAGRAM: AI Behavioral Flow - MERMAID_DIAGRAMS.md #5]
```

---

## SLAYT 18: Settings & AI Settings

### Sistem Ayarları

**Settings:**
- SMTP Email yapılandırması
- Risk eşik değerleri
- Veritabanı bağlantı testi

**AI Settings:**
- Azure OpenAI endpoint
- API Key (şifreli)
- Model seçimi (GPT-4)
- Bağlantı testi

```
[DIAGRAM: Settings Flow - MERMAID_DIAGRAMS.md #6]
[DIAGRAM: AI Settings Flow - MERMAID_DIAGRAMS.md #7]
```

---

## SLAYT 19: Logs Modülü

### Audit & Application Logs

**Özellikler:**
- Kullanıcı işlem logları
- Sistem logları
- Event type filtreleme
- Tarih aralığı filtreleme
- Pagination

```
[DIAGRAM: Logs Flow - MERMAID_DIAGRAMS.md #8]
```

---

## SLAYT 20: API Endpoint Özeti

| Modül | Endpoint Sayısı | Ana Endpoint |
|-------|-----------------|--------------|
| Dashboard | 5 | `/api/risk/*` |
| Investigation | 3 | `/api/risk/top-*` |
| Reports | 4 | `/api/reports/*` |
| Users | 5 | `/api/users/*` |
| AI Behavioral | 4 | `/api/ai-behavioral/*` |
| Settings | 3 | `/api/settings/*` |
| AI Settings | 3 | `/api/ai-settings/*` |
| Logs | 4 | `/api/logs/*` |

---

## SLAYT 21: Güvenlik Özellikleri

### Security Features

🔐 **JWT Authentication** - Token tabanlı kimlik doğrulama  
🔑 **Role-Based Access** - Admin, Viewer, Analyst rolleri  
🔒 **Data Protection** - API key şifreleme  
📝 **Audit Logging** - Tüm işlemler kayıt altında  
🛡️ **HTTPS** - Şifreli iletişim  

---

## SLAYT 22: Performans Optimizasyonları

### Performance

⚡ **Server-Side Pagination** - Max 500 kayıt/sayfa  
📦 **Memory Caching** - AI overview 5 dk cache  
🔄 **Redis Streams** - Async message processing  
📊 **Client-Side Aggregation** - Dashboard hesaplamaları  
🗃️ **Database Indexing** - Optimized queries  

---

## SLAYT 23: Demo

### Canlı Demo

```
[DEMO NOTLARI]

1. Dashboard açılışı - Action kartları
2. Bir kullanıcıya tıklama - Detay modal
3. Investigation sayfası - Tarih filtreleme
4. Reports - PDF indirme
5. AI Behavioral - Z-Score analizi
6. Settings - Test email gönderme
```

---

## SLAYT 24: Gelecek Geliştirmeler

### Roadmap

📌 **v2.0 Planları:**
- Real-time alerting (WebSocket)
- Mobile app (React Native)
- SIEM entegrasyonu
- Custom rule engine
- Multi-tenant support
- Machine Learning (anomaly prediction)

---

## SLAYT 25: Sonuç

### Özet

✅ Forcepoint DLP entegrasyonu  
✅ Otomatik risk skorlama  
✅ AI destekli anomali tespiti  
✅ Kapsamlı raporlama  
✅ Modern web arayüzü  
✅ Güvenli ve ölçeklenebilir  

---

## SLAYT 26: Sorular

# Sorular?

📧 [email@domain.com]  
🔗 [GitHub Repository Link]

---

## DİYAGRAM REFERANS TABLOSU

| Slayt | Diyagram | Kaynak Dosya | Bölüm |
|-------|----------|--------------|-------|
| 5 | System Architecture | MERMAID_DIAGRAMS.md | #9 |
| 8 | Dashboard Flow | MERMAID_DIAGRAMS.md | #1 |
| 10 | Investigation Flow | MERMAID_DIAGRAMS.md | #2 |
| 11 | Reports Flow | MERMAID_DIAGRAMS.md | #3 |
| 12 | Users Flow | MERMAID_DIAGRAMS.md | #4 |
| 14 | Risk Score Calculation | MERMAID_DIAGRAMS.md | #10 |
| 17 | AI Behavioral Flow | MERMAID_DIAGRAMS.md | #5 |
| 18 | Settings Flow | MERMAID_DIAGRAMS.md | #6 |
| 18 | AI Settings Flow | MERMAID_DIAGRAMS.md | #7 |
| 19 | Logs Flow | MERMAID_DIAGRAMS.md | #8 |

---

## SUNUM HAZIRLAMA TALİMATLARI

### Adım 1: Diyagramları PNG Olarak İndirin
1. https://mermaid.live/ açın
2. MERMAID_DIAGRAMS.md dosyasından kodu kopyalayın
3. PNG olarak export edin

### Adım 2: PowerPoint/Google Slides'a Aktarın
1. Her slayt için yeni slide oluşturun
2. Metinleri kopyalayın
3. `[DIAGRAM]` yerlerine PNG'leri ekleyin

### Adım 3: Ekran Görüntüleri Ekleyin
1. Dashboard, Investigation, Reports sayfalarının ekran görüntülerini alın
2. İlgili slaytlara ekleyin

### Adım 4: Tema ve Tasarım
- Koyu tema önerilir (profesyonel görünüm)
- Font: Inter, Roboto veya Segoe UI
- Accent renk: #3B82F6 (mavi)
