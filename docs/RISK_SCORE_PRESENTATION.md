# DLP Risk Skoru Hesaplama Sistemi
## İki Aşamalı Hibrit Yaklaşım

---

# 🎯 Genel Bakış

```
┌─────────────────────────────────────────────────────────────────┐
│                    DLP RİSK SKORLAMA SİSTEMİ                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   AŞAMA 1: Incident-Level Risk Score (Her Olay İçin)           │
│   ─────────────────────────────────────────────────             │
│   • Her DLP olayına 0-1000 arası skor atanır                   │
│   • incidents tablosunda saklanır                               │
│   • CalculateRiskScoreV2() fonksiyonu kullanılır               │
│                                                                 │
│                          ↓                                      │
│                                                                 │
│   AŞAMA 2: Daily Risk Score (Kullanıcı Günlük Özet)            │
│   ─────────────────────────────────────────────────             │
│   • Günlük incident'lar normalize edilir (0-100)               │
│   • user_daily_risk_scores tablosunda saklanır                 │
│   • Dashboard'da gösterilir                                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

# 📊 AŞAMA 1: Incident-Level Risk Score

## Formül

```
BaseScore = (MaxMatchesTier × ChannelMultiplier) + DestinationScore
FinalScore = BaseScore × ActionMultiplier × 14.3
```

**Sonuç: 0-1000 ölçeğinde skor**

---

# 🔢 AŞAMA 1: Bileşenler

## MaxMatches Tier (Veri Eşleşme Sayısı)

| MaxMatches | Tier Puanı | Risk Seviyesi |
|------------|------------|---------------|
| 0-15       | 5          | Düşük         |
| 16-30      | 10         | Orta-Düşük    |
| 31-50      | 18         | Orta          |
| 51-100     | 28         | Yüksek        |
| 101-250    | 38         | Çok Yüksek    |
| 251-500    | 48         | Kritik        |
| 500+       | 60         | Maksimum      |

---

# 🔢 AŞAMA 1: Kanal Çarpanları

## Channel Multipliers

| Kanal             | Çarpan | Açıklama              |
|-------------------|--------|-----------------------|
| ENDPOINT_LAN      | 0.2    | Düşük risk - iç ağ    |
| ENDPOINT_PRINTING | 0.4    | Orta risk - yazıcı    |
| Email, Cloud, USB | 1.0    | Yüksek risk - dış     |

---

# 🔢 AŞAMA 1: Hedef Skorları

## Destination Scores

| Hedef Tipi        | Skor | Açıklama                    |
|-------------------|------|-----------------------------|
| SPL / NDA Var     | 1    | Güvenli, sözleşmeli hedef   |
| Printer           | 3    | Orta risk                   |
| NDA Yok / Unknown | 5    | Belirsiz hedef              |
| Personal Email    | 10   | Yüksek risk (gmail, hotmail)|

---

# 🔢 AŞAMA 1: Aksiyon Çarpanları

## Action Multipliers

| Aksiyon           | Çarpan | Açıklama                        |
|-------------------|--------|---------------------------------|
| BLOCK / QUARANTINE| 1.0    | %100 - Engellenen tehdit        |
| AUTHORIZED / PERMIT| 0.2   | %20 - İzin verilmiş             |
| RELEASED          | 0.0    | %0 - Sonradan serbest bırakılmış|

> **Not:** RELEASED = 0 ile skor sıfırlanır (yanlış alarm düzeltmesi)

---

# 📝 AŞAMA 1: Örnek Hesaplama

```
Senaryo: Kullanıcı Gmail'e 150 hassas veri içeren dosya göndermeye çalıştı
         DLP sistemi BLOCK aksiyonu uyguladı

Hesaplama:
────────────────────────────────────────────────────
MaxMatches: 150 → Tier: 38 (Çok Yüksek)
Channel: Email → Multiplier: 1.0
Destination: Gmail (Personal) → Score: 10
Action: BLOCK → Multiplier: 1.0
────────────────────────────────────────────────────
BaseScore = (38 × 1.0) + 10 = 48
FinalScore = 48 × 1.0 × 14.3 = 686

Incident Risk Score = 686 (0-1000 ölçeği)
Dashboard'da = 68.6 (0-100 ölçeği)
────────────────────────────────────────────────────
```

---

# 📊 AŞAMA 2: Daily Risk Score

## Formül

```
DailyScore = MIN(100,
    (AvgScore / 500 × 50) +      ← Ortalama Risk (%50)
    (MaxScore / 500 × 30) +      ← Maksimum Risk (%30)
    MIN(20, LOG₁₀(Count+1) × 10) ← Olay Sayısı (%20)
)
```

**Sonuç: 0-100 ölçeğinde normalize edilmiş skor**

---

# 🔢 AŞAMA 2: Bileşen Ağırlıkları

## Ağırlık Dağılımı

```
┌────────────────────────────────────────────────┐
│                                                │
│   ████████████████████████████   50%  Avg     │
│   ██████████████████             30%  Max     │
│   ████████████                   20%  Count   │
│                                                │
└────────────────────────────────────────────────┘
```

| Bileşen        | Ağırlık | Max Puan | Açıklama                    |
|----------------|---------|----------|-----------------------------| 
| Ortalama Risk  | %50     | 50       | Günün genel risk seviyesi   |
| Maksimum Risk  | %30     | 30       | En ciddi olayın etkisi      |
| Olay Sayısı    | %20     | 20       | Logaritmik frekans faktörü  |

---

# 📈 AŞAMA 2: Olay Sayısı (Logaritmik)

## Neden LOG₁₀ kullanılıyor?

```
Lineer: 10 incident = 10 puan, 500 incident = 500 puan ❌
Log:    10 incident = 10 puan, 500 incident = 20 puan ✓
```

| Incident Sayısı | LOG₁₀ × 10 | Katkı (max 20) |
|-----------------|------------|----------------|
| 1               | 3.0        | 3 puan         |
| 10              | 10.4       | 10 puan        |
| 50              | 17.1       | 17 puan        |
| 100+            | 20+        | 20 puan (max)  |

> **Amaç:** Spam-like düşük riskli incident'lar skoru patlatmasın!

---

# 📝 AŞAMA 2: Örnek Hesaplama

```
Kullanıcı: ahmet@sirket.com
Tarih: 04 Şubat 2026
Günlük Incident Sayısı: 25
Ortalama Risk Score: 300
Maksimum Risk Score: 450
────────────────────────────────────────────────────
Avg Katkısı   = (300/500) × 50 = 30.0
Max Katkısı   = (450/500) × 30 = 27.0
Count Katkısı = MIN(20, LOG₁₀(26) × 10) = 14.1
────────────────────────────────────────────────────
TOPLAM = 30.0 + 27.0 + 14.1 = 71.1

Daily Risk Score = 71.1 → 🟠 Yüksek Risk
────────────────────────────────────────────────────
```

---

# 🎨 Risk Seviyeleri

## Renk Kodlaması

| Skor Aralığı | Seviye  | Renk     | Aksiyon           |
|--------------|---------|----------|-------------------|
| 0-25         | Düşük   | 🟢 Yeşil | Normal izleme     |
| 26-50        | Orta    | 🟡 Sarı  | Dikkat gerektirir |
| 51-75        | Yüksek  | 🟠 Turuncu| Araştırma gerekli|
| 76-100       | Kritik  | 🔴 Kırmızı| Acil müdahale    |

---

# 🔄 Veri Akış Diyagramı

```
┌─────────────────┐
│   DLP API       │
│ (Symantec/etc)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────────────────┐
│   Collector     │────▶│  CalculateRiskScoreV2()  │
│   Service       │     │  (incident.risk_score)   │
└────────┬────────┘     └──────────────────────────┘
         │                         │
         ▼                         ▼
┌─────────────────┐     ┌──────────────────────────┐
│   Redis Stream  │     │   incidents tablosu      │
│                 │     │   (0-1000 ölçeği)        │
└────────┬────────┘     └────────────┬─────────────┘
         │                           │
         ▼                           ▼
┌─────────────────┐     ┌──────────────────────────┐
│   Analyzer      │────▶│  Daily Aggregation       │
│   Service       │     │  (Günlük Özet)           │
└─────────────────┘     └────────────┬─────────────┘
                                     │
                                     ▼
                        ┌──────────────────────────┐
                        │ user_daily_risk_scores   │
                        │ (0-100 ölçeği)           │
                        └────────────┬─────────────┘
                                     │
                                     ▼
                        ┌──────────────────────────┐
                        │   Dashboard API          │
                        │   (Frontend Display)     │
                        └──────────────────────────┘
```

---

# 📦 Veritabanı Tabloları

## incidents (Her Olay)

| Kolon      | Tip    | Açıklama                      |
|------------|--------|-------------------------------|
| risk_score | INT    | 0-1000 ölçeği (Aşama 1)       |
| max_matches| INT    | Hassas veri sayısı            |
| action     | VARCHAR| BLOCK, AUTHORIZED, RELEASED   |
| channel    | VARCHAR| Email, Cloud, USB, Printer    |
| destination| VARCHAR| Hedef adres/domain            |

## user_daily_risk_scores (Günlük Özet)

| Kolon            | Tip    | Açıklama                    |
|------------------|--------|-----------------------------|
| daily_risk_score | DECIMAL| 0-100 ölçeği (Aşama 2)      |
| avg_risk_score   | DECIMAL| Günlük ortalama             |
| max_risk_score   | INT    | Günlük maksimum             |
| incident_count   | INT    | Günlük olay sayısı          |

---

# ✅ Özet

## İki Aşamalı Sistem

| Aşama | Ölçek   | Tablo                    | Kullanım            |
|-------|---------|--------------------------|---------------------|
| 1     | 0-1000  | incidents.risk_score     | Her olay analizi    |
| 2     | 0-100   | user_daily_risk_scores   | Dashboard, raporlar |

## Avantajlar

- ✅ Her olay detaylı analiz edilir
- ✅ Günlük özet normalize ve karşılaştırılabilir
- ✅ Logaritmik sayaç spam'i önler
- ✅ Aksiyon çarpanları yanlış alarmları düzeltir
- ✅ Hedef tipi ve kanal bazlı risk farklılaştırması

---

# 📚 Referanslar

- [RiskAnalyzer.cs](../DLP.RiskAnalyzer.Shared/Services/RiskAnalyzer.cs) - Aşama 1 implementasyonu
- [RiskAnalyzerService.cs](../DLP.RiskAnalyzer.Analyzer/Services/RiskAnalyzerService.cs) - Aşama 2 implementasyonu
- [RiskConstants.cs](../DLP.RiskAnalyzer.Shared/Constants/RiskConstants.cs) - Sabit değerler
- [DAILY_RISK_SCORE_ALGORITHM.md](./DAILY_RISK_SCORE_ALGORITHM.md) - Detaylı algoritma dokümanı
