# Z-Score Anomali Tespiti - Teknik Dokümantasyon

## 📊 Genel Bakış

AI Behavioral Analysis sistemi, kullanıcı/kanal/departman davranışlarındaki anormallikleri **Z-score istatistiksel yöntemi** ile tespit eder.

---

## 🧮 Z-Score Formülü

```
Z = (X - μ) / σ
```

| Sembol | Anlam | Açıklama |
|--------|-------|----------|
| **X** | Gözlem | Mevcut dönemdeki değer |
| **μ** (mu) | Ortalama | Baseline dönemindeki ortalama |
| **σ** (sigma) | Standart Sapma | Baseline dönemindeki değişkenlik |

### Örnek:
- Ortalama günlük incident: 5 (baseline)
- Standart sapma: 2
- Bugün: 11 incident

```
Z = (11 - 5) / 2 = 3.0
```

**Yorum:** 3 standart sapma yukarıda → **Yüksek anomali**

---

## ⏱️ Dönem Karşılaştırması

```
Lookback = 7 gün örneği:

├─ Current Period: Son 7 gün (18-25 Aralık)
│   → Bu dönemdeki ortalama/toplam hesaplanır
│
└─ Baseline Period: Önceki 7-28 gün (Adaptive)
    → Normal davranış ortalaması ve std sapması hesaplanır
```

### Adaptive Baseline Seçimi:
1. İlk önce standart baseline dönemi kontrol edilir (lookback × 1)
2. Yetersiz veri varsa, pencere genişletilir (lookback × 2, × 3, × 4)
3. Hala veri yoksa, mevcut dönem ikiye bölünerek karşılaştırılır

---

## 📈 Hesaplanan Z-Score'lar

| Z-Score | Neyi Ölçer |
|---------|-----------|
| `z_score_incident_count` | Günlük incident sayısı değişimi |
| `z_score_severity` | Ortalama severity değişimi |
| `z_score_channel_email` | Email kanalı aktivite değişimi |
| `z_score_channel_web` | Web kanalı aktivite değişimi |
| `z_score_channel_endpoint` | Endpoint kanalı aktivite değişimi |

---

## 🎯 Risk Skoru Hesaplama

Tüm Z-score'ların **maksimum mutlak değeri** alınır ve Risk Skoru'na dönüştürülür:

```
maxZ = Max(|z_incident|, |z_severity|, |z_email|, |z_web|, |z_endpoint|)
```

| Max |Z| | Risk Skoru | Anomaly Level |
|---------|------------|---------------|
| ≥ 3.0 | **100** | 🔴 HIGH |
| ≥ 2.0 | **80** | 🔴 HIGH |
| ≥ 1.0 | **50** | 🟡 MEDIUM |
| < 1.0 | **30** | 🟢 LOW |

---

## 📋 Metadata Örneği

```json
{
  "current_period_days": 7,
  "baseline_period_days": 14,
  "baseline_mode": "historical",
  "current_incident_count": 28,
  "baseline_incident_count": 15,
  "z_score_incident_count": 2.34,
  "z_score_severity": 0.18,
  "z_score_channel_email": -0.50,
  "z_score_channel_web": 1.20,
  "z_score_channel_endpoint": 3.10,
  "current_mean_incidents": 4.0,
  "baseline_mean_incidents": 2.14,
  "risk_score": 100
}
```

**Yorum:** `z_score_channel_endpoint = 3.10` → En yüksek anomali endpoint kanalında

---

## 🔍 Sonuç Yorumlama

| Z-Score | Anlam |
|---------|-------|
| **Z > 2** | Normal davranıştan **anlamlı** sapma |
| **Z > 3** | **Kritik** anomali, acil inceleme gerekli |
| **Z ≈ 0** | Normal davranış içinde |
| **Z < -2** | Aktivite **beklenenden düşük** (belki tatil?) |

---

## 📐 İstatistiksel Arka Plan

Normal dağılımda:
- %68 veri → μ ± 1σ arasında
- %95 veri → μ ± 2σ arasında  
- %99.7 veri → μ ± 3σ arasında

**Z > 2** demek: Bu davranış tüm gözlemlerin sadece **%5'inde** görülür → **Anomali!**
