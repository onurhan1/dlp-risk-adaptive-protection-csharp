# 📚 Sıkça Sorulan Sorular (SSS) - Kullanıcı Kılavuzu

## İçindekiler

1. [Dashboard (Ana Sayfa)](#1-dashboard-ana-sayfa)
2. [Investigation (Soruşturma)](#2-investigation-soruşturma)
3. [Reports (Raporlar)](#3-reports-raporlar)
4. [Users (Kullanıcılar)](#4-users-kullanıcılar)
5. [AI Behavioral (Yapay Zeka Davranış Analizi)](#5-ai-behavioral-yapay-zeka-davranış-analizi)
6. [Analytics (Analitik)](#6-analytics-analitik)
7. [Settings (Ayarlar)](#7-settings-ayarlar)
8. [AI Settings (Yapay Zeka Ayarları)](#8-ai-settings-yapay-zeka-ayarları)
9. [Logs (Günlükler)](#9-logs-günlükler)

---

## 1. Dashboard (Ana Sayfa)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Günlük Özet Kartları** | Toplam olay sayısı, yüksek riskli olay sayısı, ortalama risk skoru, etkilenen departman sayısı |
| **Aksiyon Özeti** | AUTHORIZED, BLOCKED, QUARANTINED, RELEASED eylemlerinin günlük dağılımı |
| **Günlük Trend Grafiği** | Son 7-30 günlük olay eğilimi (Plotly grafik) |
| **Yüksek Etkili Uyarılar** | En yüksek etkiye sahip kullanıcılar ve olayları |
| **En Riskli Kullanıcılar** | Günlük bazda en yüksek risk skoruna sahip kullanıcılar |
| **En Çok Tetiklenen Kurallar** | Hangi DLP kurallarının en sık tetiklendiği |
| **Departman Dağılımı** | Olayların departmanlara göre dağılımı |

### Sık Sorulan Sorular

**S: BLOCKED, QUARANTINED gibi kartlara tıkladığımda ne olur?**
> Tıkladığınızda o aksiyona ait tüm olayların detaylı listesini gösteren bir modal açılır. Tarih aralığına göre filtreleme yapabilirsiniz.

**S: Yüksek Etkili Uyarılar ne anlama geliyor?**
> Bu liste, en yüksek `max_matches` değerine sahip ve potansiyel olarak en ciddi veri sızıntısı riski taşıyan olayları gösterir. Impact Score = Risk Skoru × Max Matches hesaplamasına göre sıralanır.

**S: Rapor indirebilir miyim?**
> Evet, sağ üst köşedeki "📊 Günlük Rapor" butonuna tıklayarak PDF formatında detaylı rapor indirebilirsiniz.

---

## 2. Investigation (Soruşturma)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Kullanıcı Listesi** | Tüm riskli kullanıcıların risk skoru ile birlikte listesi |
| **Timeline (Zaman Çizelgesi)** | Seçili kullanıcının tüm olaylarının kronolojik görünümü |
| **Alert Details (Uyarı Detayları)** | Seçili olayın tam detayları |
| **AI Behavioral Analysis** | Yapay zeka destekli davranış analizi özeti |
| **User Insights Modal** | Kullanıcı hakkında derinlemesine analiz |

### Alert Details Kısmında Neler Var?

- **Channel**: Olayın kanalı (EMAIL, HTTPS, ENDPOINT_PRINTING vb.)
- **Action**: Alınan aksiyon (BLOCKED, QUARANTINED, AUTHORIZED, RELEASED)
- **Destination**: Hedef adres veya uygulama
- **Matched Policy**: Tetiklenen DLP politikası
- **Matched Rules**: Tetiklenen kurallar ve classifier eşleşme sayıları
- **Details**: Login name, email adresi, dosya adı bilgileri
- **Remediate Button**: Olayı çözüldü olarak işaretleme

### Sık Sorulan Sorular

**S: Bir kullanıcının tüm olaylarını nasıl görebilirim?**
> Sol paneldeki kullanıcı listesinden ilgili kullanıcıya tıklayın. Timeline otomatik olarak o kullanıcının tüm olaylarını gösterecektir.

**S: User Insights butonu ne işe yarar?**
> Bu buton, kullanıcının haftalık trend analizi, davranış kalıpları, en sık tetiklediği kurallar ve AI destekli risk değerlendirmesini içeren kapsamlı bir modal açar.

**S: Remediate butonu ne yapar?**
> Bu buton ile olayı "çözüldü" olarak işaretleyebilir, alınan aksiyonu ve notları kaydedebilirsiniz. Çözülen olaylar yeşil işaret ile gösterilir.

**S: Risk skoru nasıl hesaplanıyor?**
> Risk skoru, kullanıcının olay sayısı, aksiyonların ağırlıkları ve tetiklenen politikaların kritiklik seviyelerine göre Z-Score algoritması ile hesaplanır.

---

## 3. Reports (Raporlar)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Tarih Seçici** | Rapor için tarih aralığı belirleme |
| **Aksiyon Özet Kartları** | AUTHORIZED, BLOCK, QUARANTINE, RELEASED sayıları |
| **En Riskli Kullanıcılar Tablosu** | Risk skoruna göre sıralı kullanıcılar |
| **En Çok Tetiklenen Politikalar** | Politika bazlı kural dağılımı (genişletilebilir) |
| **Kanal Dağılımı** | EMAIL, HTTPS, ENDPOINT gibi kanalların yüzdelik dağılımı |
| **En Popüler Hedefler** | En sık veri gönderilen destinasyonlar |
| **Geçmiş Raporlar** | Daha önce oluşturulmuş raporların listesi |

### Sık Sorulan Sorular

**S: PDF rapor nasıl oluşturulur?**
> "📥 PDF Oluştur" butonuna tıklayın. Tarih aralığına göre detaylı bir rapor oluşturulur ve indirilir.

**S: Politikaların altındaki kuralları nasıl görebilirim?**
> Her politika satırının solundaki ok işaretine tıklayarak o politikaya ait tüm kuralları ve tetiklenme sayılarını görebilirsiniz.

**S: Aksiyon kartlarına tıkladığımda ne olur?**
> İlgili aksiyona ait tüm olayların detaylı listesi modal olarak açılır.

---

## 4. Users (Kullanıcılar)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Kullanıcı Listesi** | Sistemdeki tüm kullanıcıların tablosu |
| **Kullanıcı Bilgileri** | Email, rol (Admin/User), durum (Aktif/Pasif) |
| **Oluşturma Tarihi** | Kullanıcının ne zaman eklendiği |
| **Aksiyonlar** | Düzenle, Sil butonları |

### Sık Sorulan Sorular

**S: Yeni kullanıcı nasıl eklenir?**
> "➕ Yeni Kullanıcı Ekle" butonuna tıklayın. Email, şifre ve rol bilgilerini girin.

**S: Kullanıcı rolü ne işe yarar?**
> **Admin**: Tüm sayfalara erişim, kullanıcı yönetimi, ayar değişikliği yapabilir.
> **User**: Sadece Dashboard sayfasına erişim hakkına sahiptir.

**S: Kullanıcı şifresini nasıl sıfırlarım?**
> Düzenle butonuna tıklayın ve yeni şifre alanını doldurun.

> [!IMPORTANT]
> Bu sayfa sadece Admin rolündeki kullanıcılar tarafından görülebilir.

---

## 5. AI Behavioral (Yapay Zeka Davranış Analizi)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Genel Bakış Kartları** | Toplam analiz edilen sayı, anomali seviyeleri dağılımı |
| **Entity Tab'ları** | Users, Channels, Departments, Destinations, Rules bazında analiz |
| **Anomali Listesi** | Risk skoru ve anomali seviyesi ile sıralı entity'ler |
| **Entity Detail Modal** | Tıklanan entity'nin detaylı AI analizi |

### Entity Detail Modal İçeriği

- **Risk Score**: 0-100 arası hesaplanan risk skoru
- **Anomaly Level**: LOW, MEDIUM, HIGH, CRITICAL
- **AI Explanation**: Yapay zekanın analiz açıklaması
- **AI Recommendation**: Önerilen aksiyonlar
- **Reference Incidents**: İlişkili olaylar
- **Weekly Trends**: Haftalık olay trendi ve günlük detaylar
- **Behavioral Metrics**: Davranış metrikleri ve Z-Score değerleri

### Sık Sorulan Sorular

**S: Anomali seviyesi nasıl belirleniyor?**
> Z-Score algoritması kullanılır. Kullanıcının davranışı, tüm kullanıcıların ortalamasından ne kadar sapıyorsa o kadar yüksek anomali seviyesi atanır.

**S: "View AI Analysis" butonu ne yapar?**
> İlgili entity için yapay zeka destekli kapsamlı bir analiz çalıştırır ve sonuçları modal olarak gösterir.

**S: Refresh butonu ne işe yarar?**
> Tüm AI analizlerini yeniden hesaplayarak güncel verilere göre sonuç üretir.

---

## 6. Analytics (Analitik)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Tarih Filtresi** | Analiz için tarih aralığı belirleme |
| **Incident Heatmap** | Gün ve saat bazlı olay yoğunluğu ısı haritası |
| **Domain Features Manager** | Domain bazlı özellik yönetimi tablosu |
| **Filtreleme Seçenekleri** | Policy, Channel, Severity, Department filtreleri |

### Domain Features (Destinasyon Özellikleri)

| Özellik | Açıklama |
|---------|----------|
| **Gizlilik Sözleşmesi** | Hedef ile gizlilik anlaşması var mı? |
| **Eğitim** | Güvenlik eğitimi verilmiş mi? |
| **Noterlik** | Noter onaylı anlaşma var mı? |
| **Denetim** | Düzenli denetim yapılıyor mu? |
| **Banka** | Hedef bir banka mı? |
| **Hukuk Firması** | Hedef bir hukuk firması mı? |
| **İştirak** | Kurum iştiraki mi? |

### Sık Sorulan Sorular

**S: Heatmap nasıl okunur?**
> X ekseni saatleri (0-23), Y ekseni günleri (Pazartesi-Pazar) gösterir. Koyu renkler yüksek olay yoğunluğunu ifade eder.

**S: Domain özelliklerini nasıl düzenlerim?**
> Domain Features Manager tablosunda ilgili satırdaki değerleri tıklayarak değiştirebilirsiniz. Değişiklikler otomatik kaydedilir.

**S: CSV ile toplu güncelleme yapabilir miyim?**
> Evet, "CSV Yükle" butonu ile hazırladığınız CSV dosyasını yükleyerek toplu güncelleme yapabilirsiniz.

---

## 7. Settings (Ayarlar)

### Bu sayfada ne görebilirim?

| Tab | İçerik |
|-----|--------|
| **Genel Ayarlar** | Email bildirimi, günlük rapor saati, risk eşikleri, admin email |
| **DLP API** | Symantec DLP Manager bağlantı ayarları |
| **Email** | SMTP sunucu ayarları, test email gönderimi |
| **Splunk SIEM** | Splunk HEC entegrasyon ayarları |

### Sık Sorulan Sorular

**S: Risk eşiklerini nasıl ayarlarım?**
> Genel Ayarlar sekmesinde "Risk Eşikleri" bölümünden LOW, MEDIUM, HIGH eşik değerlerini belirleyebilirsiniz.

**S: DLP API bağlantısını nasıl test ederim?**
> DLP API sekmesinde bilgileri girdikten sonra "Test Bağlantısı" butonuna tıklayın.

**S: Email ayarları ne işe yarar?**
> Günlük raporların ve uyarıların email olarak gönderilmesi için SMTP sunucu ayarlarını yapılandırmanız gerekir.

**S: Splunk entegrasyonu ne sağlar?**
> Tüm DLP olaylarının Splunk SIEM'e otomatik olarak gönderilmesini sağlar.

---

## 8. AI Settings (Yapay Zeka Ayarları)

### Bu sayfada ne görebilirim?

| Ayar | Açıklama |
|------|----------|
| **Model Provider** | OpenAI, Azure OpenAI veya Copilot seçimi |
| **API Key** | Seçilen provider için API anahtarı |
| **Model Name** | Kullanılacak model (gpt-4o, gpt-4-turbo vb.) |
| **Temperature** | Yanıt yaratıcılık seviyesi (0.0 - 1.0) |
| **Max Tokens** | Maksimum yanıt uzunluğu |
| **Enabled** | AI özelliklerinin aktif/pasif durumu |

### Sık Sorulan Sorular

**S: Hangi AI provider'ı seçmeliyim?**
> - **OpenAI**: Genel kullanım için, api.openai.com üzerinden
> - **Azure OpenAI**: Kurumsal Azure hesabı olanlar için
> - **Copilot**: GitHub Copilot entegrasyonu için

**S: Temperature ne anlama geliyor?**
> 0'a yakın değerler daha tutarlı ve muhafazakâr yanıtlar üretir. 1'e yakın değerler daha yaratıcı ve çeşitli yanıtlar üretir.

**S: Test Connection butonu ne yapar?**
> Girdiğiniz API ayarlarının doğru çalışıp çalışmadığını kontrol eder.

---

## 9. Logs (Günlükler)

### Bu sayfada ne görebilirim?

| Bileşen | Açıklama |
|---------|----------|
| **Sistem Logları** | Backend servislerinin log kayıtları |
| **Hata Kayıtları** | Oluşan hataların detayları |
| **API İstekleri** | API çağrılarının kayıtları |
| **Filtreleme** | Tarih, seviye (INFO, WARNING, ERROR) bazlı filtreleme |

### Sık Sorulan Sorular

**S: Log kayıtları ne kadar süre saklanır?**
> Varsayılan olarak son 30 günlük loglar saklanır. Bu ayar Settings sayfasından değiştirilebilir.

**S: Hata oluştuğunda ne yapmalıyım?**
> ERROR seviyesindeki logları filtreleyerek hatanın detaylarını görüntüleyin. Stack trace bilgisi sorunun kaynağını belirlemenize yardımcı olacaktır.

---

## 🔐 Erişim Yetkileri Özeti

| Sayfa | Admin | User |
|-------|:-----:|:----:|
| Dashboard | ✅ | ✅ |
| Investigation | ✅ | ❌ |
| Reports | ✅ | ❌ |
| Users | ✅ | ❌ |
| AI Behavioral | ✅ | ❌ |
| Analytics | ✅ | ❌ |
| Settings | ✅ | ❌ |
| AI Settings | ✅ | ❌ |
| Logs | ✅ | ❌ |

---

## 📞 Destek

Sorularınız için:
- Sidebar'ın altındaki "Help & Support" bölümünü kullanabilirsiniz
- Sistem yöneticinize başvurabilirsiniz

---

*Son Güncelleme: Şubat 2026*
