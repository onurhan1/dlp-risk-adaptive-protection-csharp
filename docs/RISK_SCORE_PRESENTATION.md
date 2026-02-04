# DLP Risk Skoru Hesaplama Sistemi
## İki Aşamalı Hibrit Yaklaşım (v2.0 - Güncellenmiş)

---

# 🎯 Genel Bakış

```
┌─────────────────────────────────────────────────────────────────┐
│                    DLP RİSK SKORLAMA SİSTEMİ                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   AŞAMA 1: Incident-Level Risk Score                           │
│   ─────────────────────────────────────                         │
│   • Her DLP olayına 0-100 arası skor atanır                    │
│   • Max: (85 × 1.0) + 15 = 100                                 │
│                                                                 │
│                          ↓                                      │
│                                                                 │
│   AŞAMA 2: Daily Risk Score                                    │
│   ─────────────────────────────────────                         │
│   • Günlük aggregation ile 0-100 final skor                    │
│   • Dashboard'da gösterilir                                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

# 📊 AŞAMA 1: Incident Score Formülü

```
IncidentScore = ((MaxMatchesTier × ChannelMultiplier) + DestinationScore) × ActionMultiplier
```

| Bileşen | Max Değer | Açıklama |
|---------|-----------|----------|
| MaxMatchesTier | **85** | Hassas veri sayısına göre |
| DestinationScore | **15** | Hedef risk seviyesi |
| **Toplam** | **100** | Doğrudan 0-100 ölçeği |

---

# 🔢 MaxMatchesTier Tablosu (Max: 85)

| MaxMatches | Tier Puanı | Risk Seviyesi |
|------------|------------|---------------|
| 0-15       | 7          | 🟢 Düşük      |
| 16-30      | 14         | 🟡 Orta-Düşük |
| 31-50      | 25         | 🟡 Orta       |
| 51-100     | 40         | 🟠 Yüksek     |
| 101-250    | 55         | 🟠 Çok Yüksek |
| 251-500    | 70         | 🔴 Kritik     |
| 500+       | **85**     | 🔴 Maksimum   |

---

# 🔢 Destination Score Tablosu (Max: 15)

| Hedef Tipi | Skor | Açıklama |
|------------|------|----------|
| SPL / NDA Var | 2 | Güvenli, sözleşmeli hedef |
| Printer | 5 | Orta risk |
| Unknown / NDA Yok | 8 | Belirsiz hedef |
| Personal Email | **15** | Gmail, Hotmail, Yahoo |

---

# 🔢 Kanal Çarpanları

| Kanal | Çarpan | Açıklama |
|-------|--------|----------|
| ENDPOINT_LAN | 0.2 | İç ağ, düşük risk |
| ENDPOINT_PRINTING | 0.4 | Yazıcı, orta risk |
| Email, Cloud, USB | 1.0 | Dış hedef, yüksek risk |

---

# 🔢 Aksiyon Çarpanları

| Aksiyon | Çarpan | Açıklama |
|---------|--------|----------|
| BLOCK / QUARANTINE | 1.0 | %100 - Engellenen tehdit |
| AUTHORIZED / PERMIT | 0.2 | %20 - İzin verilmiş |
| RELEASED | 0.0 | %0 - Sonradan serbest |

---

# 📝 AŞAMA 1: Örnek Hesaplama

```
Senaryo: Gmail'e 150 veri gönderimi, DLP BLOCK uyguladı

────────────────────────────────────────
MaxMatches: 150 → Tier: 55
Channel: Email → Multiplier: 1.0
Destination: Gmail → Score: 15
Action: BLOCK → Multiplier: 1.0
────────────────────────────────────────
BaseScore = (55 × 1.0) + 15 = 70
IncidentScore = 70 × 1.0 = 70

Sonuç: 70/100 → 🟠 Yüksek Risk
────────────────────────────────────────
```

---

# 📊 AŞAMA 2: Daily Score Formülü

```
DailyScore = (AvgScore × 0.50) + (MaxScore × 0.30) + MIN(20, LOG₁₀(Count+1) × 10)
```

| Bileşen | Ağırlık | Max Katkı |
|---------|---------|-----------|
| Ortalama Risk | %50 | 50 puan |
| Maksimum Risk | %30 | 30 puan |
| Olay Sayısı | %20 | 20 puan |

---

# 📈 Olay Sayısı (Logaritmik)

| Incident Sayısı | LOG₁₀ × 10 | Katkı |
|-----------------|------------|-------|
| 1 | 3.0 | 3 puan |
| 10 | 10.4 | 10 puan |
| 50 | 17.1 | 17 puan |
| 100+ | 20+ | **20 puan (max)** |

> Logaritmik ölçek spam'i önler!

---

# 📝 AŞAMA 2: Örnek Hesaplama

```
Kullanıcı: ahmet@sirket.com
Tarih: 04 Şubat 2026
────────────────────────────────────────
Incident Sayısı: 25
Ortalama Incident Score: 60
Maksimum Incident Score: 80
────────────────────────────────────────
Avg Katkısı   = 60 × 0.50 = 30.0
Max Katkısı   = 80 × 0.30 = 24.0
Count Katkısı = LOG₁₀(26) × 10 = 14.1
────────────────────────────────────────
TOPLAM = 30 + 24 + 14.1 = 68.1

Daily Score: 68/100 → 🟠 Yüksek Risk
────────────────────────────────────────
```

---

# 🎨 Risk Seviyeleri

| Skor | Seviye | Renk | Aksiyon |
|------|--------|------|---------|
| 0-25 | Düşük | 🟢 Yeşil | Normal izleme |
| 26-50 | Orta | 🟡 Sarı | Dikkat gerektirir |
| 51-75 | Yüksek | 🟠 Turuncu | Araştırma gerekli |
| 76-100 | Kritik | 🔴 Kırmızı | Acil müdahale |

---

# 🔄 Veri Akış Diyagramı

```
┌─────────────────┐
│   DLP API       │
└────────┬────────┘
         ▼
┌─────────────────┐     ┌──────────────────────────┐
│   Collector     │────▶│  Incident Score (0-100)  │
└────────┬────────┘     │  ((Tier×Channel)+Dest)   │
         │              │        ×Action           │
         ▼              └────────────┬─────────────┘
┌─────────────────┐                  │
│   incidents     │◀─────────────────┘
│   tablosu       │
└────────┬────────┘
         │  Günlük Aggregation
         ▼
┌─────────────────────────────────────┐
│   Daily Score (0-100)               │
│   (Avg×0.50)+(Max×0.30)+LOG₁₀(n)×10 │
└────────┬────────────────────────────┘
         ▼
┌─────────────────┐
│   Dashboard     │
└─────────────────┘
```

---

# ✅ Özet

| Aşama | Formül | Max |
|-------|--------|-----|
| **1** | (MaxTier×Channel)+Dest)×Action | 100 |
| **2** | (Avg×0.50)+(Max×0.30)+LOG(n)×10 | 100 |

## Avantajlar
- ✅ Tek ölçek: Her iki aşama da 0-100
- ✅ Çift normalizasyon yok
- ✅ Anlaşılır ve tutarlı

---

# 📚 Referanslar

- [RiskAnalyzer.cs](file:///c:/Users/abdul/Desktop/dlp-risk-adaptive-protection-csharp-main/DLP.RiskAnalyzer.Shared/Services/RiskAnalyzer.cs)
- [RiskConstants.cs](file:///c:/Users/abdul/Desktop/dlp-risk-adaptive-protection-csharp-main/DLP.RiskAnalyzer.Shared/Constants/RiskConstants.cs)
- [RiskAnalyzerService.cs](file:///c:/Users/abdul/Desktop/dlp-risk-adaptive-protection-csharp-main/DLP.RiskAnalyzer.Analyzer/Services/RiskAnalyzerService.cs)
- [Migration Script](file:///c:/Users/abdul/Desktop/dlp-risk-adaptive-protection-csharp-main/database/migrations/risk_score_migration_0100.sql)
