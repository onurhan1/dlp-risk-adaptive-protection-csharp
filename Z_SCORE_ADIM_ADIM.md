# Z-Score Hesaplaması - Adım Adım Açıklama

## 🎯 Z-Score Ne İşe Yarar?

Z-Score, bir değerin "normal"den ne kadar uzak olduğunu söyler.

**Basit örnek:** Bir kullanıcı normalde günde 2 incident yapıyorsa ve bugün 10 incident yaptıysa, bu anormal mi? Z-Score bunu matematiksel olarak hesaplar.

---

## 📐 Formül

```
Z = (X - μ) / σ
```

---

## 🔢 Her Sayının Açıklaması

### 1️⃣ X (Gözlem Değeri)
**Ne demek?** Şu an ölçtüğümüz değer.

**Örnek:** Kullanıcı bugün **10 incident** yaptı → X = 10

---

### 2️⃣ μ (Mu - Ortalama)
**Ne demek?** Geçmişteki değerlerin ortalaması. "Normal" davranışı temsil eder.

**Nasıl hesaplanır?**
```
μ = Toplam / Gün Sayısı
```

**Örnek:** Son 5 günde kullanıcının incident sayıları:
| Gün | Incident |
|-----|----------|
| Pazartesi | 2 |
| Salı | 3 |
| Çarşamba | 1 |
| Perşembe | 2 |
| Cuma | 2 |

```
μ = (2 + 3 + 1 + 2 + 2) / 5 = 10 / 5 = 2
```

**Yani:** Bu kullanıcı normalde günde ortalama **2 incident** yapıyor.

---

### 3️⃣ σ (Sigma - Standart Sapma)
**Ne demek?** Değerlerin ortalamadan ne kadar dağıldığı. "Değişkenlik" ölçüsü.

**Nasıl hesaplanır?** (4 adımda)

**Adım 1:** Her değerin ortalamadan farkını bul
| Gün | Incident | Fark (X - μ) |
|-----|----------|--------------|
| Pazartesi | 2 | 2 - 2 = 0 |
| Salı | 3 | 3 - 2 = 1 |
| Çarşamba | 1 | 1 - 2 = -1 |
| Perşembe | 2 | 2 - 2 = 0 |
| Cuma | 2 | 2 - 2 = 0 |

**Adım 2:** Farkların karesini al
| Fark | Kare |
|------|------|
| 0 | 0² = 0 |
| 1 | 1² = 1 |
| -1 | (-1)² = 1 |
| 0 | 0² = 0 |
| 0 | 0² = 0 |

**Adım 3:** Karelerin ortalamasını al (Varyans)
```
Varyans = (0 + 1 + 1 + 0 + 0) / 5 = 2 / 5 = 0.4
```

**Adım 4:** Karekök al
```
σ = √0.4 = 0.63
```

**Yani:** Kullanıcının günlük incident sayısı ortalamadan ~0.63 kadar sapıyor.

---

## 📊 Tam Hesaplama Örneği

**Senaryo:** 
- Kullanıcı bugün **10 incident** yaptı (X = 10)
- Baseline ortalaması: **2 incident** (μ = 2)
- Standart sapma: **0.63** (σ = 0.63)

**Z-Score hesapla:**
```
Z = (X - μ) / σ
Z = (10 - 2) / 0.63
Z = 8 / 0.63
Z = 12.7
```

---

## 🚨 Z-Score Yorumlama

| Z-Score | Ne Anlama Geliyor? |
|---------|-------------------|
| **0** | Tam ortalamada, normal |
| **1** | Ortalamanın 1 sapma üstünde |
| **2** | %95 verinin dışında, **şüpheli** |
| **3** | %99.7 verinin dışında, **KESİNLİKLE ANORMAL** |
| **12.7** | Çok çok anormal! 🔴 |

**Yukarıdaki örneğimizde Z = 12.7**, yani bu kullanıcının davranışı AŞIRI anormal.

---

## 📈 Gerçek Sistem Örneği

### Email Kanalı Z-Score Hesabı

**Senaryo:** 
Kullanıcının son 30 günlük email kanalı incident verileri:

| Baseline (Geçmiş 30 gün) | Değer |
|--------------------------|-------|
| Ortalama günlük email incident | 1.5 |
| Standart sapma | 0.8 |

| Bugün | Değer |
|-------|-------|
| Email incident sayısı | 5 |

**Hesaplama:**
```
Z_email = (5 - 1.5) / 0.8
Z_email = 3.5 / 0.8
Z_email = 4.375
```

**Yorum:** Email kanalında Z-Score = 4.375 → **Kritik anomali!**

---

## 🎯 Risk Score'a Dönüşüm

Sistemimizde 5 farklı Z-Score hesaplanır:
1. Incident sayısı Z-Score
2. Severity Z-Score
3. Email kanalı Z-Score
4. Web kanalı Z-Score
5. Endpoint kanalı Z-Score

**Risk Score = En yüksek |Z-Score|'a göre belirlenir:**

| En Yüksek |Z| | Risk Score |
|--------------|------------|
| ≥ 3 | 100 (Kritik) |
| ≥ 2 | 80 (Yüksek) |
| ≥ 1 | 50 (Orta) |
| < 1 | 30 (Düşük) |

---

## 📝 Özet

```
1. X = Bugünkü değer (ölçtüğümüz şey)
2. μ = Geçmiş ortalaması (normal ne?)
3. σ = Standart sapma (ne kadar değişkenlik var?)
4. Z = (Bugün - Normal) / Değişkenlik
5. |Z| > 2 ise → ANORMAL!
```
