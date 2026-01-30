# Daily Risk Score Normalization Algorithm

## Genel Bakış

Bu doküman, DLP Risk Analyzer sisteminde kullanıcı günlük risk skorlarının (user_daily_risk_scores) nasıl hesaplandığını açıklar. Yeni algoritma, sınırsız SUM değerleri yerine **1-100 arası normalize edilmiş** bir skor üretir.

---

## Problem Tanımı

### Eski Hesaplama (Sorunlu)
```sql
daily_risk_score = SUM(incident_risk_scores)
```

**Sorunlar:**
- Günlük skor sınırsız büyüyebiliyordu (örn: 227,148)
- Kullanıcılar arasında karşılaştırma zordu
- Dashboard'da görselleştirme problemi
- Yüksek incident sayısı olan günler orantısız yüksek skor alıyordu

### Örnek Problemli Veri
| User | Date | daily_risk_score | incident_count | avg | max |
|------|------|------------------|----------------|-----|-----|
| user@company.com | 2025-01-15 | **227,148** | 485 | 468 | 471 |

---

## Yeni Normalizasyon Formülü

### Matematiksel Formül

```
DailyScore = MIN(100, 
    (AvgScore / 500 × 50) + 
    (MaxScore / 500 × 30) + 
    MIN(20, LOG₁₀(IncidentCount + 1) × 10)
)
```

### Formül Bileşenleri

| Bileşen | Ağırlık | Maksimum Katkı | Açıklama |
|---------|---------|----------------|----------|
| **Ortalama Risk (Avg)** | 50% | 50 puan | Günün genel risk seviyesi |
| **Maksimum Risk (Max)** | 30% | 30 puan | En ciddi incident'ın etkisi |
| **Incident Sayısı (Count)** | 20% | 20 puan | Frekans faktörü (logaritmik) |

### Neden 500 Bölen?

- Incident risk skorları 0-1000 arasında hesaplanır
- Ancak pratikte çoğu incident **100-500** arasında skor alır
- 500, "yüksek riskli incident" eşiği olarak belirlenmiştir
- Bu sayede orta-yüksek riskli günler bile anlamlı skorlar üretir

---

## Bileşen Detayları

### 1. Ortalama Risk Katkısı (50 puan max)

```
AvgContribution = (AvgScore / 500) × 50
```

| Avg Score | Katkı | Yorum |
|-----------|-------|-------|
| 100 | 10 puan | Düşük risk |
| 250 | 25 puan | Orta risk |
| 400 | 40 puan | Yüksek risk |
| 500+ | 50 puan | Çok yüksek (max) |

### 2. Maksimum Risk Katkısı (30 puan max)

```
MaxContribution = (MaxScore / 500) × 30
```

| Max Score | Katkı | Yorum |
|-----------|-------|-------|
| 100 | 6 puan | Düşük |
| 300 | 18 puan | Orta |
| 500+ | 30 puan | Kritik (max) |

**Neden Max önemli?**
- Tek bir kritik incident bile günü riskli yapabilir
- Örn: 99 düşük riskli + 1 kritik incident = yüksek günlük skor

### 3. Incident Sayısı Katkısı (20 puan max)

```
CountContribution = MIN(20, LOG₁₀(IncidentCount + 1) × 10)
```

**Logaritmik ölçek kullanılmasının nedeni:**
- Lineer ölçek yüksek sayılara aşırı ağırlık verir
- Log ölçek ile 10 incident → 100 incident farkı azalır
- Çok sayıda düşük riskli incident, skoru patlatmaz

| Incident Count | LOG₁₀(n+1) × 10 | Katkı (max 20) |
|----------------|-----------------|----------------|
| 1 | 3.0 | 3 puan |
| 10 | 10.4 | 10 puan |
| 100 | 20.0 | 20 puan (max) |
| 500 | 27.0 | 20 puan (capped) |

---

## Hesaplama Örnekleri

### Örnek 1: Düşük Riskli Gün
```
Avg: 120, Max: 150, Count: 5

AvgContribution  = (120/500) × 50 = 12.0
MaxContribution  = (150/500) × 30 = 9.0
CountContribution = MIN(20, LOG₁₀(6) × 10) = 7.8

DailyScore = 12.0 + 9.0 + 7.8 = 28.8 ≈ 29
```

### Örnek 2: Orta Riskli Gün
```
Avg: 300, Max: 400, Count: 25

AvgContribution  = (300/500) × 50 = 30.0
MaxContribution  = (400/500) × 30 = 24.0
CountContribution = MIN(20, LOG₁₀(26) × 10) = 14.1

DailyScore = 30.0 + 24.0 + 14.1 = 68.1 ≈ 68
```

### Örnek 3: Yüksek Riskli Gün (Önceki Problemli Veri)
```
Avg: 468, Max: 471, Count: 485

AvgContribution  = (468/500) × 50 = 46.8
MaxContribution  = (471/500) × 30 = 28.3
CountContribution = MIN(20, LOG₁₀(486) × 10) = MIN(20, 26.9) = 20.0

DailyScore = 46.8 + 28.3 + 20.0 = 95.1 ≈ 95
```

**Sonuç:** 227,148 → **95** (anlamlı ve karşılaştırılabilir)

### Örnek 4: Kritik Gün (Tek Ciddi Incident)
```
Avg: 800, Max: 950, Count: 1

AvgContribution  = (800/500) × 50 = 80.0 → capped at 50
MaxContribution  = (950/500) × 30 = 57.0 → capped at 30
CountContribution = MIN(20, LOG₁₀(2) × 10) = 3.0

DailyScore = 50 + 30 + 3 = 83
```

---

## Implementasyon

### SQL (PostgreSQL)

```sql
LEAST(100, 
    (COALESCE(AVG(risk_score), 0) / 500.0 * 50) + 
    (COALESCE(MAX(risk_score), 0) / 500.0 * 30) + 
    LEAST(20, LOG(COUNT(*) + 1) * 10)
) as daily_risk_score
```

> **Not:** PostgreSQL'de `LOG()` fonksiyonu doğal logaritma (ln) değil, 10 tabanında logaritmadır.

### C# (.NET)

```csharp
var normalizedScore = Math.Min(100,
    (avgRiskScore / 500.0 * 50) +
    (maxRiskScore / 500.0 * 30) +
    Math.Min(20, Math.Log10(incidentCount + 1) * 10)
);
var totalRiskScore = Math.Round(normalizedScore, 2);
```

---

## Skor Yorumlama Tablosu

| Skor Aralığı | Risk Seviyesi | Renk Kodu | Aksiyon |
|--------------|---------------|-----------|---------|
| 0-25 | 🟢 Düşük | Yeşil | Normal izleme |
| 26-50 | 🟡 Orta | Sarı | Dikkat gerektirir |
| 51-75 | 🟠 Yüksek | Turuncu | Araştırma gerekli |
| 76-100 | 🔴 Kritik | Kırmızı | Acil müdahale |

---

## Backfill İşlemi

Mevcut historical verileri yeni formülle güncellemek için:

```bash
psql -U your_user -d dlp_database -f backfill-daily-risk-scores.sql
```

Bu script:
1. Gerekli kolonları ekler (team, full_name)
2. Tüm günlük skorları yeni formülle yeniden hesaplar
3. Team ve full_name bilgilerini incidents tablosundan doldurur

---

## Dosya Referansları

| Dosya | Açıklama |
|-------|----------|
| [backfill-daily-risk-scores.sql](../backfill-daily-risk-scores.sql) | Historical data backfill script |
| [RiskAnalyzerService.cs](../DLP.RiskAnalyzer.Analyzer/Services/RiskAnalyzerService.cs) | C# runtime hesaplama |

---

## Versiyon Geçmişi

| Tarih | Değişiklik |
|-------|------------|
| 2026-01-29 | Normalizasyon algoritması eklendi (v2) |
| 2026-01-29 | 500 bölen değeri belirlendi |
| 2026-01-29 | Backfill script güncellendi |

---

## Teknik Notlar

1. **Neden MIN(100)?**
   - Ekstrem durumlarda bile skor 100'ü geçmemeli
   - Dashboard'da tutarlı görselleştirme

2. **Neden LOG₁₀?**
   - Incident sayısının etkisini azaltır
   - 10x artış = sadece +10 puan
   - Spam-like incident'lar skoru patlatmaz

3. **Neden 50-30-20 dağılımı?**
   - Ortalama en önemli (günün genel resmi)
   - Max önemli ama tek başına belirleyici değil
   - Count tamamlayıcı faktör (sıklık)
