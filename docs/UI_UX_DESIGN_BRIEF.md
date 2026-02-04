# DLP Risk Adaptive Protection - UI/UX Design Brief

## 🎯 Proje Özeti

**Uygulama Adı:** DLP Risk Adaptive Protection System
**Sektör:** Kurumsal Siber Güvenlik / Veri Kaybı Önleme
**Hedef Kullanıcı:** Güvenlik analistleri, SOC ekipleri, CISO'lar, IT yöneticileri
**Platform:** Web tabanlı dashboard (masaüstü öncelikli, tablet uyumlu)

---

## 🔐 Projenin Amacı

Bu sistem, büyük kurumlarda veri sızıntısı riskini **proaktif olarak tespit eden** ve **kullanıcı bazlı risk skorlaması** yapan bir siber güvenlik platformudur.

### Temel Sorun
- Çalışanlar günlük işlerinde hassas verileri e-posta, bulut, USB veya yazıcı gibi kanallarla dışarı gönderebilir
- Geleneksel DLP sistemleri sadece olayı kaydeder, risk analizi yapmaz
- Güvenlik ekipleri binlerce alert arasında kaybolur

### Çözüm
- Yapay zeka destekli davranış analizi
- Kullanıcı bazlı risk skorlaması (0-100)
- Gerçek zamanlı tehdit tespiti
- İncelenebilir ve raporlanabilir dashboard

---

## 🧠 Temel Kavramlar

### Risk Skoru (0-100)
Kullanıcıların ne kadar "riskli" olduğunu gösteren bir metrik:
- **0-25:** 🟢 Düşük Risk - Normal kullanım
- **26-50:** 🟡 Orta Risk - İzleme gerektirir
- **51-75:** 🟠 Yüksek Risk - Araştırma gerekli
- **76-100:** 🔴 Kritik Risk - Acil müdahale

### Incident (Olay)
DLP sisteminin algıladığı her veri güvenliği ihlali:
- Kimin yaptığı (kullanıcı)
- Ne zaman yaptığı (timestamp)
- Hangi kanaldan (Email, USB, Cloud, Printer)
- Nereye gönderildi (destination)
- Ne yapıldı (Block, Quarantine, Authorized, Released)
- Kaç hassas veri eşleşti (max_matches)

### Action Types (Eylem Tipleri)
- **BLOCK:** DLP olayı engelledi - En ciddi durum
- **QUARANTINE:** İnceleme için karantinaya alındı
- **AUTHORIZED:** Yönetici tarafından izin verildi
- **RELEASED:** Karantinadan serbest bırakıldı

### Channels (Kanallar)
- Email (kurumsal/kişisel)
- Cloud Storage (OneDrive, Google Drive, Dropbox)
- USB/Removable Media
- Network Share (LAN)
- Printer/Yazıcı

---

## 📊 Ana Sayfalar ve Fonksiyonlar

### 1. Dashboard (Ana Sayfa)
**Amaç:** Günün/haftanın genel güvenlik durumunu tek bakışta görmek

**Gösterilecek Metrikler:**
- Toplam Incident sayısı (bugün/bu hafta)
- Aksiyon bazlı dağılım (Block, Quarantine, Authorized, Released)
- Risk skoru dağılımı (pie chart)
- Top 10 Riskli Kullanıcı listesi
- Günlük trend grafiği
- Kanal bazlı breakdown

**Etkileşimler:**
- Aksiyon kartlarına tıklayınca detay modal
- Kullanıcıya tıklayınca profil sayfası
- Tarih filtresi

---

### 2. Investigation (Araştırma)
**Amaç:** 30 günlük trendleri incelemek, pattern'ları tespit etmek

**Gösterilecek Veriler:**
- 30 günlük incident trendi (çizgi grafik)
- Günlük en riskli kullanıcılar (heat map veya bar chart)
- Kural/Policy bazlı alert dağılımı
- Departman bazlı karşılaştırma

**Etkileşimler:**
- Belirli bir güne tıklayınca o günün detayları
- Kullanıcı filtreleme
- Export to PDF/Excel

---

### 3. Reports (Raporlar)
**Amaç:** Günlük/haftalık/aylık raporları görüntülemek ve indirmek

**İçerik:**
- Tarih seçici (takvim)
- Seçilen günün özeti
- Top kullanıcılar
- Top politikalar/kurallar
- Action dağılımı
- PDF export butonu

---

### 4. Users (Kullanıcılar)
**Amaç:** Tüm kullanıcıları ve risk skorlarını listelemek

**Gösterilecekler:**
- Kullanıcı tablosu (sayfalandırmalı)
- Risk skoru, departman, son aktivite
- Arama ve filtreleme
- Sıralama (risk skoruna göre)

**Kullanıcı Detay Modalı:**
- Kullanıcı profili
- Risk skoru trendi (grafik)
- Son 30 günlük aktivite
- Incident geçmişi
- Departman/şube bilgisi

---

### 5. AI Behavioral Analysis (Yapay Zeka Davranış Analizi)
**Amaç:** AI tarafından analiz edilen kullanıcıları ve anomali tespitlerini göstermek

**İçerik:**
- AI ile analiz edilen kullanıcı listesi
- Anomali skoru
- Risk kategorisi (Critical, High, Medium, Low)
- Haftalık trend analizi
- Weekly incidents grafiği
- Pattern/davranış açıklamaları

**Entity Detail Modal:**
- AI özet analizi
- Haftalık kırılım
- Anomalik günler
- Öneriler

---

### 6. Settings (Ayarlar)
**Amaç:** Sistem konfigürasyonu

**Sekmeler:**
- Profil ayarları
- Bildirim tercihleri
- DLP API bağlantısı
- Redis/Database ayarları

---

### 7. AI Settings (AI Ayarları)
**Amaç:** Azure OpenAI entegrasyonu yapılandırması

**İçerik:**
- API endpoint
- API key (masked)
- Model adı (gpt-4, gpt-4o, vb.)
- Max tokens
- Temperature
- Test connection butonu

---

### 8. Logs (Loglar)
**Amaç:** Sistem loglarını görüntülemek (debug/monitoring)

**İçerik:**
- Tarih/saat filtresi
- Seviye filtresi (Info, Warning, Error)
- Kaynak filtresi
- Log tablosu (sayfalandırmalı)

---

## 🎨 Tasarım Dili ve Hissi

### Renk Paleti Önerisi
- **Primary:** Koyu mavi tonları (#1E3A5F, #2C5282) - Kurumsal, güvenilir
- **Accent:** Elektrik mavisi (#3182CE) - Dikkat çekici, modern
- **Success:** Yeşil (#38A169) - Düşük risk
- **Warning:** Turuncu (#DD6B20) - Orta risk
- **Danger:** Kırmızı (#E53E3E) - Kritik risk
- **Background:** Çok koyu gri veya siyah (#0D1117, #161B22) - Dark mode
- **Text:** Açık gri/beyaz - Okunabilirlik

### Tasarım Karakteri
- **Profesyonel:** Banka/finans sektörü güvenlik ekipleri kullanacak
- **Minimal ama bilgi yoğun:** Çok data gösterilecek ama karmaşık görünmemeli
- **Dark mode varsayılan:** Güvenlik analistleri uzun saatler çalışır
- **Data-driven:** Grafikler, tablolar, metrikler ön planda
- **Responsive:** Genişliğe göre uyarlanabilir grid sistemi

### Tipografi
- Monospace fontlar için: JetBrains Mono, Fira Code
- Başlıklar için: Inter, Roboto, SF Pro
- Okunabilirlik öncelikli, çok küçük font kullanılmamalı

### Ikonografi
- Güvenlik temalı ikonlar: kalkan, kilit, göz, alarm
- Minimalist, filled veya outline tutarlılığı
- Lucide Icons, Heroicons, Feather Icons uyumlu

---

## 📐 Sayfa Yapısı

### Layout
```
┌─────────────────────────────────────────────────────┐
│  Sidebar (Sol)  │        Main Content Area           │
│                 │                                    │
│  • Logo         │   ┌──────────────────────────────┐ │
│  • Navigation   │   │  Header (Sayfa başlığı)      │ │
│    - Dashboard  │   ├──────────────────────────────┤ │
│    - Investig.  │   │                              │ │
│    - Reports    │   │  Content Area                │ │
│    - Users      │   │  (Kartlar, grafikler,        │ │
│    - AI Behav.  │   │   tablolar)                  │ │
│    - Settings   │   │                              │ │
│    - AI Settings│   │                              │ │
│    - Logs       │   │                              │ │
│                 │   └──────────────────────────────┘ │
│  • Footer       │                                    │
│    (Version)    │                                    │
└─────────────────────────────────────────────────────┘
```

### Bileşenler
1. **Stat Cards:** İkon + Sayı + Label + Trend göstergesi
2. **Charts:** Line, Bar, Pie, Heatmap
3. **Tables:** Sayfalandırmalı, sıralanabilir, filtrelenebilir
4. **Modals:** Detay görünümleri için
5. **Date Pickers:** Tarih seçimi
6. **Dropdowns:** Filtreler
7. **Search Bars:** Arama
8. **Toast Notifications:** Başarı/hata mesajları

---

## 🔔 Kullanıcı Deneyimi Notları

1. **First Impression:** Dashboard açıldığında "bugün kaç olay oldu, en riskli kim" hemen görülmeli
2. **Drill-down:** Her metrikten detaya gidebilmeli (tıkla → modal veya sayfa)
3. **Color Coding:** Risk seviyeleri her yerde tutarlı renk kullanmalı
4. **Loading States:** Data yüklenirken skeleton loader
5. **Empty States:** Veri yoksa boş ekran değil, açıklayıcı mesaj
6. **Error Handling:** API hatalarında kullanıcı bilgilendirilmeli
7. **Keyboard Shortcuts:** Power user'lar için (opsiyonel)

---

## 📱 Örnek Akış Senaryoları

### Senaryo 1: Sabah Kontrolü
1. Güvenlik analisti dashboard'u açar
2. "Dün 5 BLOCK olayı olmuş" görür
3. BLOCK kartına tıklar → Modal açılır
4. En riskli kullanıcıyı görür → Kullanıcı profiline gider
5. Son 30 günlük trendi inceler
6. Gerekirse rapor indirir

### Senaryo 2: Anomali İncelemesi
1. AI Behavioral sayfasını açar
2. "Critical" risk seviyeli kullanıcıları filtreler
3. Bir kullanıcıyı seçer → Detay modalı açılır
4. AI'ın belirlediği anomali açıklamasını okur
5. Haftalık incident grafiğini inceler
6. Gerekli aksiyonu alır (raporlar, IT'ye bildirir)

---

## 🛡️ Güvenlik ve Uyumluluk Sembolleri

Tasarımda kullanılabilecek semboller:
- Kalkan (Shield) - Koruma
- Kilit (Lock) - Güvenlik
- Göz (Eye) - İzleme/Monitoring
- Grafik (Chart) - Analiz
- Kullanıcı (User) - Kişi bazlı
- Zaman (Clock) - Gerçek zamanlı
- Uyarı (Alert/Bell) - Bildirim
- Dosya (File) - Veri
- Bulut (Cloud) - Cloud storage
- E-posta (Mail) - Email kanalı
- USB - Removable media
- Yazıcı (Printer) - Print kanalı

---

## 💡 Ekstra Öneriler

- **Gamification:** Departmanlar arası "en güvenli departman" gibi olumlu rekabet
- **Trend Indicators:** ↑↓ okları ile değişim gösterimi
- **Comparison:** "Önceki haftaya göre %15 artış" gibi karşılaştırmalar
- **Tooltips:** Karmaşık metriklerde açıklayıcı tooltip'ler
- **Collapsible Sections:** Uzun listelerde collapse/expand
- **Favorites/Pinned:** Sık bakılan kullanıcıları pinleme

---

## 📦 Çıktı Beklentisi

Bu brief'i kullanarak:
1. Dashboard ana sayfa mockup'ı
2. Kullanıcı listesi sayfası
3. Kullanıcı detay modalı
4. Action incidents modalı
5. AI Behavioral analysis sayfası
6. Settings sayfası

Dark theme, modern, veri yoğun ama sade bir tasarım oluşturulmalıdır.
