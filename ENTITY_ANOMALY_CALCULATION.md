# Entity Türlerine Göre Anomali Hesaplama Detayları

## 📋 Genel Hesaplama Mantığı

Tüm entity türleri için **aynı hesaplama metodolojisi** kullanılır:

```
1. Current Period: Son N gün (lookbackDays)
2. Baseline Period: Önceki N-4N gün (adaptive)
3. Z-Score hesapla: z = (current - baseline_mean) / baseline_std
4. Max Z-Score'u al → Risk Score'a dönüştür
```

---

## 👤 USER (Kullanıcı) Analizi

### Veri Kaynağı:
```sql
SELECT * FROM incidents 
WHERE user_email = 'ahmet@kuveytturk.com.tr' 
  AND timestamp BETWEEN [startDate] AND [endDate]
```

### Örnek Senaryo:
```
Kullanıcı: ahmet@kuveytturk.com.tr

Current Period (18-25 Aralık):
├─ Toplam incident: 21
├─ Günlük ortalama: 3.0
├─ Ortalama severity: 7.5
├─ Email: 10, Web: 6, Endpoint: 5

Baseline Period (4-18 Aralık):
├─ Toplam incident: 28 (14 gün)
├─ Günlük ortalama: 2.0
├─ Std sapma: 0.8
├─ Ortalama severity: 5.0

Z-Score Hesaplama:
├─ Incident: (3.0 - 2.0) / 0.8 = 1.25
├─ Severity: (7.5 - 5.0) / 1.5 = 1.67
├─ Email: (10 - 4) / 2 = 3.0 ← EN YÜKSEK
├─ Web: (6 - 3) / 1.5 = 2.0
├─ Endpoint: (5 - 3) / 1 = 2.0

Sonuç:
├─ Max Z: 3.0 (Email)
├─ Risk Score: 100 (Z ≥ 3)
├─ Anomaly Level: HIGH
└─ Açıklama: "Email channel activity anomaly detected (Z-score: 3.00)"
```

---

## 📡 CHANNEL (Kanal) Analizi

### Veri Kaynağı:
```sql
SELECT * FROM incidents 
WHERE channel = 'ENDPOINT_LAN' 
  AND timestamp BETWEEN [startDate] AND [endDate]
```

### Örnek Senaryo:
```
Kanal: ENDPOINT_LAN

Current Period (18-25 Aralık):
├─ Toplam incident: 450
├─ Günlük ortalama: 64.3
├─ Ortalama severity: 6.2

Baseline Period (4-18 Aralık):
├─ Toplam incident: 280 (14 gün)
├─ Günlük ortalama: 20.0
├─ Std sapma: 8.0

Z-Score Hesaplama:
├─ Incident: (64.3 - 20.0) / 8.0 = 5.54 ← ÇOKYÜKSEK

Sonuç:
├─ Max Z: 5.54
├─ Risk Score: 100
├─ Anomaly Level: HIGH
└─ Açıklama: "Incident frequency increased significantly (Z-score: 5.54)"
```

---

## 🏢 DEPARTMENT (Departman) Analizi

### Veri Kaynağı:
```sql
SELECT * FROM incidents 
WHERE department = 'Bilgi Teknolojileri' 
  AND timestamp BETWEEN [startDate] AND [endDate]
```

### Örnek Senaryo:
```
Departman: Bilgi Teknolojileri

Current Period (18-25 Aralık):
├─ Toplam incident: 85
├─ Günlük ortalama: 12.1
├─ Ortalama severity: 5.8

Baseline Period (4-18 Aralık):
├─ Günlük ortalama: 10.0
├─ Std sapma: 3.0

Z-Score Hesaplama:
├─ Incident: (12.1 - 10.0) / 3.0 = 0.7

Sonuç:
├─ Max Z: 0.7
├─ Risk Score: 30 (Z < 1)
├─ Anomaly Level: LOW
└─ Açıklama: "No significant behavioral anomalies detected"
```

---

## 🎯 DESTINATION (Hedef) Analizi

### Veri Kaynağı:
```sql
SELECT * FROM incidents 
WHERE destination = 'external-storage.com' 
  AND timestamp BETWEEN [startDate] AND [endDate]
```

### Örnek Senaryo:
```
Destination: external-storage.com

Current Period (18-25 Aralık):
├─ Toplam incident: 45
├─ Günlük ortalama: 6.4
├─ Ortalama severity: 8.2

Baseline Period (4-18 Aralık):
├─ Günlük ortalama: 1.5
├─ Std sapma: 0.8

Z-Score Hesaplama:
├─ Incident: (6.4 - 1.5) / 0.8 = 6.13 ← KRITIK

Sonuç:
├─ Max Z: 6.13
├─ Risk Score: 100
├─ Anomaly Level: HIGH
└─ Açıklama: "CRITICAL: High anomaly detected. External storage destination shows 6x normal activity"
```

---

## 📜 RULE (Kural) Analizi

### Veri Kaynağı:
```sql
SELECT * FROM incidents 
WHERE violation_triggers::jsonb @> '[{"RuleName": "Hesap Ekstresi-Others"}]'
  AND timestamp BETWEEN [startDate] AND [endDate]
```

### Örnek Senaryo:
```
Rule: Hesap Ekstresi-Others

Current Period (18-25 Aralık):
├─ Toplam incident: 120
├─ Günlük ortalama: 17.1
├─ Ortalama severity: 7.0

Baseline Period (4-18 Aralık):
├─ Günlük ortalama: 8.0
├─ Std sapma: 2.5

Z-Score Hesaplama:
├─ Incident: (17.1 - 8.0) / 2.5 = 3.64

Sonuç:
├─ Max Z: 3.64
├─ Risk Score: 100
├─ Anomaly Level: HIGH
└─ Açıklama: "Rule 'Hesap Ekstresi-Others' triggered 2x more than normal"
```

---

## 📊 Özet Tablo

| Entity Type | Örnek EntityId | Karşılaştırma Kriteri |
|-------------|----------------|----------------------|
| **USER** | ahmet@company.com | Kullanıcının incident'ları |
| **CHANNEL** | ENDPOINT_LAN | O kanaldaki tüm incident'lar |
| **DEPARTMENT** | Bilgi Teknolojileri | O departmandaki incident'lar |
| **DESTINATION** | usb-drive | O hedefe giden incident'lar |
| **RULE** | Block-SSN | O kuralı tetikleyen incident'lar |

---

## 🧮 Risk Score Dönüşüm Tablosu

| Max |Z-Score| | Risk Score | Anomaly Level | Renk |
|-----------------|------------|---------------|------|
| Z ≥ 3.0 | 100 | HIGH | 🔴 |
| Z ≥ 2.0 | 80 | HIGH | 🔴 |
| Z ≥ 1.0 | 50 | MEDIUM | 🟡 |
| Z < 1.0 | 30 | LOW | 🟢 |
