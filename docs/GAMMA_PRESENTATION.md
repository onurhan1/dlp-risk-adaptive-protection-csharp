# DLP Risk Adaptive Protection
## Yapay Zeka Destekli Veri Kaybı Önleme Sistemi

---

# 🎯 Neden Bu Projeye İhtiyaç Var?

Kurumlar her gün **binlerce DLP olayıyla** karşı karşıya. Geleneksel yöntemlerle bu olayları analiz etmek:

- ⏱️ **Zaman alıcı** - manuel inceleme
- 🔍 **Önceliklendirme zorluğu** - hangi olaylar kritik?
- 📊 **Görünürlük eksikliği** - kullanıcı davranış kalıpları
- 🧠 **İnsan hatası riski** - yorgunluk ve dikkat dağınıklığı

> **Çözüm:** Otomatik risk skorlama ve AI destekli anomali tespiti

---

# ✨ DLP Risk Adaptive Protection

Forcepoint DLP verilerini **akıllı bir platforma** dönüştüren kapsamlı güvenlik çözümü.

### Temel Özellikler

| Özellik | Açıklama |
|---------|----------|
| 🎯 **Risk Skorlama** | Her incident için 0-100 arası otomatik skor |
| 🤖 **AI Analiz** | Azure OpenAI ile akıllı öneriler |
| 📊 **Dashboard** | Gerçek zamanlı görselleştirme |
| 📈 **Trend Analizi** | Davranış değişikliklerini takip |
| 📄 **PDF Raporlar** | Profesyonel raporlama |

---

# 🏗️ Sistem Mimarisi

```
┌──────────────────────────────────────────────────────────────┐
│                        FRONTEND                               │
│                   Next.js 14 + React                          │
│  ┌─────────┬─────────┬─────────┬─────────┬─────────┐        │
│  │Dashboard│Investig.│ Reports │  Users  │AI Behav.│        │
│  └────┬────┴────┬────┴────┬────┴────┬────┴────┬────┘        │
└───────┼─────────┼─────────┼─────────┼─────────┼──────────────┘
        │         │         │         │         │
        ▼         ▼         ▼         ▼         ▼
┌──────────────────────────────────────────────────────────────┐
│                      BACKEND API                              │
│                     .NET 8 (C#)                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ RiskController │ ReportsController │ AIController    │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ RiskAnalyzerService │ BehaviorEngine │ EmailService  │   │
│  └──────────────────────────────────────────────────────┘   │
└───────────────────────────┬──────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  PostgreSQL  │    │    Redis     │    │ Azure OpenAI │
│   Database   │    │   Streams    │    │    GPT-4     │
└──────────────┘    └──────────────┘    └──────────────┘
```

---

# 💻 Teknoloji Yığını

### Frontend
- **Framework:** Next.js 14
- **Dil:** TypeScript
- **UI:** React + Tailwind CSS
- **Charts:** Recharts

### Backend
- **Framework:** .NET 8 Web API
- **Dil:** C#
- **ORM:** Entity Framework Core

### Altyapı
- **Database:** PostgreSQL
- **Cache:** Redis Streams
- **AI:** Azure OpenAI (GPT-4)
- **PDF:** iTextSharp

---

# 📊 Dashboard Modülü

Ana kontrol paneli - tüm DLP aktivitelerinin tek bakışta görünümü.

### Gösterilen Veriler

| Bileşen | Açıklama |
|---------|----------|
| **Action Cards** | Block, Quarantine, Authorized sayıları |
| **Top Users** | Son 24 saatte en riskli kullanıcılar |
| **Data Movement** | 30 günlük kanal bazlı grafik |
| **Top Rules** | En çok tetiklenen DLP kuralları |
| **Daily Trends** | Günlük incident tablosu |

### API Endpoints
```
GET /api/risk/action-summary
GET /api/risk/daily-summary
GET /api/incidents
```

---

# 🔍 Investigation Modülü

Detaylı araştırma ve tarihsel analiz sayfası.

### Özellikler

- 📅 **Tarih Aralığı Seçimi** - DatePicker ile filtreleme
- 👥 **Top 20 Risky Users** - Risk skoruna göre sıralı
- ⚠️ **Top Alerts** - Kural bazlı uyarılar
- 📈 **Timeline Grafiği** - Zaman serisi görselleştirme

### Kullanım Senaryosu
> "Son 7 günde en çok BLOCK alan 20 kullanıcıyı görmek istiyorum"

```
GET /api/risk/top-users-daily?startDate=2026-01-05&endDate=2026-01-12&limit=20
```

---

# 📄 Reports Modülü

Profesyonel PDF raporlama sistemi.

### Rapor İçerikleri

| Bölüm | Detay |
|-------|-------|
| **Action Summary** | Günlük aksiyon özeti |
| **Top 10 Users** | En riskli kullanıcılar |
| **Channel Breakdown** | Email, USB, Cloud dağılımı |
| **Top Policies** | En çok ihlal edilen politikalar |
| **Top Destinations** | Veri gönderim hedefleri |

### PDF Export
```
GET /api/reports/daily-summary/pdf?date=2026-01-12
```

Yanıt: `application/pdf` dosyası otomatik indirilir

---

# 🧮 Risk Skoru Hesaplama

### Formül

```
BaseScore = (PolicyRepeatCount × 2) + (DataSensitivity × 2) + Tier

FinalScore = BaseScore × ActionMultiplier

Score = min(100, FinalScore)
```

### Tier Değerleri

| MaxMatches | Tier Değeri |
|------------|-------------|
| 1-4 eşleşme | 10 |
| 5-19 eşleşme | 20 |
| 20-99 eşleşme | 40 |
| 100+ eşleşme | 60 |

### Action Multiplier

| Aksiyon | Çarpan |
|---------|--------|
| BLOCK / QUARANTINE | 1.0 |
| AUTHORIZED / RELEASED | 0.2 |

---

# 🚦 Risk Seviyeleri

Hesaplanan skora göre kullanıcılar 3 kategoride sınıflandırılır:

| Skor | Seviye | Renk | Aksiyon |
|------|--------|------|---------|
| **70-100** | 🔴 HIGH | Kırmızı | Acil inceleme gerekli |
| **30-69** | 🟠 MEDIUM | Turuncu | Takip edilmeli |
| **0-29** | 🟢 LOW | Yeşil | Rutin kontrol |

### Örnek Hesaplama

```
Kullanıcı: enesa@kuveytturk.com
- PolicyRepeatCount: 5 → 10 puan
- DataSensitivity: 3 → 6 puan  
- MaxMatches: 25 → Tier 40
- Action: BLOCK → Multiplier 1.0

BaseScore = 10 + 6 + 40 = 56
FinalScore = 56 × 1.0 = 56
Risk Level: 🟠 MEDIUM
```

---

# 🤖 AI Behavioral Analysis

Z-Score tabanlı anomali tespit sistemi.

### Z-Score Formülü

```
Z-Score = (X - μ) / σ

X = Güncel değer
μ = Ortalama (mean)
σ = Standart sapma
```

### Anomaly Seviyeleri

| Z-Score | Seviye | Açıklama |
|---------|--------|----------|
| < 1.5 | ✅ Normal | Beklenen davranış |
| 1.5 - 2.0 | 🟡 Low | Hafif sapma |
| 2.0 - 2.5 | 🟠 Medium | Dikkat gerektiren |
| 2.5 - 3.0 | 🔴 High | Anormal davranış |
| ≥ 3.0 | ⛔ Critical | Ciddi anomali |

---

# ⚙️ Settings & AI Settings

### Sistem Ayarları

| Ayar | Açıklama |
|------|----------|
| **SMTP** | Email bildirim yapılandırması |
| **Risk Thresholds** | Eşik değer ayarları |
| **Database** | Bağlantı testi |

### AI Ayarları

| Ayar | Açıklama |
|------|----------|
| **Azure Endpoint** | OpenAI API adresi |
| **API Key** | Şifreli saklanan anahtar |
| **Model** | gpt-4 / gpt-35-turbo |

> 🔐 API Key'ler **DataProtection** ile şifrelenir

---

# 📝 Logs Modülü

Kapsamlı audit ve uygulama log sistemi.

### Log Türleri

| Tür | Açıklama |
|-----|----------|
| **Audit Logs** | Kullanıcı işlemleri (login, logout, settings) |
| **Application Logs** | Sistem logları |
| **Collector Logs** | Veri toplama logları |

### Filtreleme Özellikleri
- Event type seçimi
- Tarih aralığı
- Kullanıcı bazlı
- Pagination (100 kayıt/sayfa)

---

# 🔐 Güvenlik Özellikleri

### Authentication & Authorization

| Özellik | Uygulama |
|---------|----------|
| 🔑 **JWT Tokens** | Bearer token authentication |
| 👥 **Roller** | Admin, Viewer, Analyst |
| 🔒 **Şifreleme** | DataProtection API |
| 📝 **Audit** | Tüm işlemler loglanır |
| 🌐 **HTTPS** | TLS şifreli iletişim |

### Rol Yetkileri

| Rol | Yetkiler |
|-----|----------|
| **Admin** | Tam erişim, kullanıcı yönetimi |
| **Analyst** | Veri analizi, raporlama |
| **Viewer** | Sadece görüntüleme |

---

# ⚡ Performans Optimizasyonları

### Uygulanan Teknikler

| Teknik | Uygulama Alanı |
|--------|----------------|
| **Server-Side Pagination** | Max 500 kayıt/sayfa |
| **Memory Cache** | AI overview - 5 dk TTL |
| **Redis Streams** | Async message processing |
| **Client Aggregation** | Dashboard hesaplamaları |
| **DB Indexing** | Sorgulama optimizasyonu |

### Metrikler
- Dashboard yükleme: **< 2 saniye**
- Report generation: **< 5 saniye**
- AI analysis cache: **5 dakika**

---

# 🛣️ Gelecek Geliştirmeler

### Roadmap v2.0

| Özellik | Durum |
|---------|-------|
| Real-time Alerting (WebSocket) | 📋 Planlandı |
| Mobile App (React Native) | 📋 Planlandı |
| SIEM Entegrasyonu | 📋 Planlandı |
| Custom Rule Engine | 🔄 Tasarım |
| Multi-tenant Support | 🔄 Tasarım |
| ML Anomaly Prediction | 💡 Araştırma |

---

# 📈 Proje Metrikleri

### Geliştirme İstatistikleri

| Metrik | Değer |
|--------|-------|
| **Toplam Endpoint** | 31 |
| **Frontend Sayfa** | 8 |
| **Backend Service** | 12 |
| **Database Tablosu** | 5 |
| **API Response Time** | < 200ms |

### Kod Kalitesi
- TypeScript strict mode
- C# nullable reference types
- Unit test coverage: %80+
- Code review: zorunlu

---

# 🎬 Demo

### Gösterilecek Akışlar

1. **Dashboard** → Action kartları ve Top Users
2. **Investigation** → Tarih filtreleme
3. **Reports** → PDF indirme
4. **AI Behavioral** → Z-Score analizi
5. **Settings** → Test email gönderme

---

# ✅ Özet

### DLP Risk Adaptive Protection

✓ **Forcepoint DLP** ile tam entegrasyon  
✓ **Otomatik risk skorlama** algoritması  
✓ **AI destekli** anomali tespiti  
✓ **Profesyonel raporlama** sistemi  
✓ **Modern web arayüzü**  
✓ **Güvenli ve ölçeklenebilir** mimari  

> 🎯 **Sonuç:** Güvenlik ekiplerinin veri kaybı olaylarını hızlı ve etkili bir şekilde yönetmesini sağlayan kapsamlı bir platform.

---

# 🙋 Sorular?

### İletişim

📧 **Email:** [email@domain.com]  
🔗 **GitHub:** [repository-link]  
📄 **Dokümantasyon:** `/docs` klasörü

---

**Teşekkürler!**
