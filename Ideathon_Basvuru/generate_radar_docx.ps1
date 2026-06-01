# RADAR Word Document Generator - Emanet formatinda birebir
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Add()

# --- Page Setup (72pt = 1 inch all sides) ---
$sec = $doc.Sections.Item(1)
$sec.PageSetup.TopMargin = 72
$sec.PageSetup.BottomMargin = 72
$sec.PageSetup.LeftMargin = 72
$sec.PageSetup.RightMargin = 72

# --- Color constants ---
$darkColor = 2236962    # koyu yesil-gri (Emanet'teki renk)
$grayColor = 6710886    # gri (#666666)

# --- Alignment constants ---
$wdAlignCenter = 1
$wdAlignLeft = 0
$wdAlignJustify = 3

# --- Helper function: Add paragraph ---
function Add-Para {
    param(
        [string]$Text,
        [int]$FontSize = 11,
        [bool]$Bold = $false,
        [bool]$Italic = $false,
        [int]$Color = $darkColor,
        [int]$Alignment = 0,
        [float]$SpaceBefore = 0,
        [float]$SpaceAfter = 5,
        [float]$LineSpacing = 13.8,
        [bool]$IsBullet = $false
    )
    $sel = $word.Selection
    $sel.TypeParagraph()
    
    $para = $sel.Paragraphs.Item(1)
    $para.Alignment = $Alignment
    $para.SpaceBefore = $SpaceBefore
    $para.SpaceAfter = $SpaceAfter
    $para.LineSpacing = $LineSpacing
    
    if ($IsBullet) {
        $para.Style = $doc.Styles.Item("Liste Paragraf")
        $sel.Range.ListFormat.ApplyBulletDefault()
        $para.SpaceAfter = 3
    }
    
    $sel.Font.Name = "Calibri"
    $sel.Font.Size = $FontSize
    $sel.Font.Bold = $Bold
    $sel.Font.Italic = $Italic
    $sel.Font.Color = $Color
    
    $sel.TypeText($Text)
}

# --- Helper: Add body paragraph (justified, 11pt) ---
function Add-Body {
    param([string]$Text)
    Add-Para -Text $Text -FontSize 11 -Alignment $wdAlignJustify -SpaceAfter 5 -LineSpacing 13.8
}

# --- Helper: Add question heading (12pt bold) ---
function Add-Heading {
    param([string]$Text)
    Add-Para -Text $Text -FontSize 12 -Bold $true -Alignment $wdAlignLeft -SpaceBefore 12 -SpaceAfter 4 -LineSpacing 12
}

# --- Helper: Add bullet item ---
function Add-Bullet {
    param([string]$Text)
    Add-Para -Text $Text -FontSize 11 -Alignment $wdAlignLeft -SpaceAfter 3 -LineSpacing 13.8 -IsBullet $true
}

# --- Helper: Add body with bold prefix ---
function Add-BodyBoldPrefix {
    param([string]$BoldPart, [string]$NormalPart)
    $sel = $word.Selection
    $sel.TypeParagraph()
    
    $para = $sel.Paragraphs.Item(1)
    $para.Alignment = $wdAlignJustify
    $para.SpaceBefore = 0
    $para.SpaceAfter = 5
    $para.LineSpacing = 13.8
    
    $sel.Font.Name = "Calibri"
    $sel.Font.Size = 11
    $sel.Font.Bold = $true
    $sel.Font.Italic = $false
    $sel.Font.Color = $darkColor
    $sel.TypeText($BoldPart)
    
    $sel.Font.Bold = $false
    $sel.TypeText($NormalPart)
}

# --- Helper: Add bullet with bold prefix ---
function Add-BulletBoldPrefix {
    param([string]$BoldPart, [string]$NormalPart)
    $sel = $word.Selection
    $sel.TypeParagraph()
    
    $para = $sel.Paragraphs.Item(1)
    $para.Alignment = $wdAlignLeft
    $para.SpaceBefore = 0
    $para.SpaceAfter = 3
    $para.LineSpacing = 13.8
    $para.Style = $doc.Styles.Item("Liste Paragraf")
    $sel.Range.ListFormat.ApplyBulletDefault()
    
    $sel.Font.Name = "Calibri"
    $sel.Font.Size = 11
    $sel.Font.Bold = $true
    $sel.Font.Italic = $false
    $sel.Font.Color = $darkColor
    $sel.TypeText($BoldPart)
    
    $sel.Font.Bold = $false
    $sel.TypeText($NormalPart)
}

# ============================================================
# DOCUMENT CONTENT
# ============================================================

# --- First paragraph (title) - handle specially ---
$sel = $word.Selection
$firstPara = $sel.Paragraphs.Item(1)
$firstPara.Alignment = $wdAlignCenter
$firstPara.SpaceBefore = 0
$firstPara.SpaceAfter = 3
$firstPara.LineSpacing = 12
$sel.Font.Name = "Calibri"
$sel.Font.Size = 18
$sel.Font.Bold = $true
$sel.Font.Color = $darkColor
$sel.TypeText("RADAR")

# Subtitle
Add-Para -Text "Risk Analysis Data Adaptive Response" -FontSize 11 -Italic $true -Alignment $wdAlignCenter -SpaceAfter 2 -LineSpacing 12

# Date line
Add-Para -Text "R&D Techathon 2026 — Proje Başvurusu" -FontSize 10 -Color $grayColor -Alignment $wdAlignCenter -SpaceAfter 18 -LineSpacing 12

# ===================== SORU 1 =====================
Add-Heading "Projede yapay zekâ nerede kullanılıyor?"

Add-Body "Proje, kurumun DLP (Data Loss Prevention) altyapısından toplanan kullanıcı davranış verilerini yapay zekâ ile analiz ederek dinamik risk skorlaması yapan ve koruma politikalarını otomatik uyarlayan bir platformdur. Yapay zekâ şu noktalarda kullanılır:"

Add-BulletBoldPrefix "Davranışsal risk analizi: " "Kullanıcıların dosya hareketleri, e-posta gönderim örüntüleri, USB kullanımı, ekran görüntüsü alma gibi DLP olaylarını analiz ederek her kullanıcıya dinamik bir risk skoru (0–100) atanması."

Add-BulletBoldPrefix "Anomali tespiti: " "Kullanıcının 20 günlük kayar pencere (rolling window) bazlı geçmiş davranış profilinden sapmaların 3-sigma kuralı ile istatistiksel olarak algılanması; ani yükselişler, olağandışı kanal kullanımı, yeni hedef adreslere veri çıkışı gibi anomalilerin otomatik tespiti."

Add-BulletBoldPrefix "AI destekli davranış açıklaması: " "Azure OpenAI entegrasyonu ile her kullanıcının risk profilinin doğal dilde açıklanması — “Bu kullanıcı son 7 günde normal ortalamanın 3 katı hassas dosya transferi gerçekleştirdi” gibi anlaşılır özetlerin otomatik üretilmesi."

Add-BulletBoldPrefix "Akıllı sınıflandırma: " "DLP olaylarının hassasiyet düzeyine göre otomatik sınıflandırılması (düşük / orta / yüksek / kritik)."

Add-BulletBoldPrefix "Copilot (ChatBot) arayüzü: " "Güvenlik analistlerinin doğal dilde sorular sorarak (“En riskli 5 kullanıcı kimdir?”, “Geçen hafta hangi departmandan en çok ihlal geldi?”) sistemi sorgulamasını sağlayan AI chatbot."

# ===================== SORU 2 =====================
Add-Heading "Projenin konusu ve kapsamı nedir?"

Add-Body "Kurum, Forcepoint DLP altyapısı üzerinden veri kaybı önleme politikaları uyguluyor. Ancak mevcut DLP sistemi statik, kural tabanlı çalışıyor: her kullanıcıya aynı politika aynı şiddette uygulanıyor. Bir kullanıcının düşük riskli bir dosya paylaşımıyla, kasıtlı veri sızdırma girişimi aynı seviyede değerlendirildiğinde hem güvenlik ekipleri gereksiz uyarı yığını altında kalıyor hem de gerçek tehditler gözden kaçabiliyor."

Add-BodyBoldPrefix "RADAR" ", kurumun DLP altyapısından akan olay verilerini toplayarak kullanıcı bazlı risk skorlaması yapan, bu skora göre DLP politikalarını dinamik olarak uyarlayan ve güvenlik ekiplerine AI destekli içgörüler sunan bir platformdur. Proje kapsamı:"

Add-BulletBoldPrefix "Veri Toplama (Collector): " "Forcepoint DLP’den Splunk/SIEM üzerinden akan olay loglarının gerçek zamanlı toplanması ve standart formata dönüştürülmesi."

Add-BulletBoldPrefix "Risk Analizi (Analyzer): " "Toplanan verilerin yapay zekâ algoritmaları ile analiz edilmesi; kullanıcı bazlı risk skoru, trend analizi ve anomali tespiti."

Add-BulletBoldPrefix "Davranış Motoru (Behavior Engine): " "Kullanıcının günlük, haftalık ve aylık davranış profilinin Z-score tabanlı çok boyutlu analiz ile çıkarılması; 30/60/90 gün adaptif baseline seçimi; departman ve rol bazlı karşılaştırmalar; IOB (Indicators of Behavior) tespiti."

Add-BulletBoldPrefix "Adaptif Politika Yönetimi: " "Risk skoruna göre DLP politikalarının otomatik sıkılaştırılması veya gevşetilmesi (izin ver → uyar → onayla → engelle)."

Add-BulletBoldPrefix "Dashboard ve Raporlama: " "Gerçek zamanlı risk haritası, kullanıcı detay sayfaları, trend grafikleri ve PDF/Excel rapor üretimi."

Add-BulletBoldPrefix "Olay Yönetimi (Investigation): " "Yüksek riskli olayların incelenmesi, remediation (düzeltme) aksiyonlarının takibi ve denetim izi (audit trail) oluşturulması."

Add-Body "Proje, mevcut DLP altyapısının yerine geçmez; ona paralel çalışan bir akıl katmanı olarak konumlanır."

# ===================== SORU 3 =====================
Add-Heading "Projenin amacı ve hedefi nedir? (Asıl fayda kime?)"

Add-BodyBoldPrefix "Asıl fayda kuruma ve dolaylı olarak müşteriyedir." " Projenin temel yaklaşımı şudur: güvenlik, iş yapabilirliğin önündeki engel değil, onu mümkün kılan zemin olmalıdır."

Add-Body "Klasik DLP yaklaşımı “herkese aynı katılığı uygula” mantığıyla çalışır. Bu, düşük riskli çalışanların gereksiz yere engellenmesine (verimlilik kaybı) ve yüksek riskli davranışların alarm yığını içinde kaybolmasına (güvenlik açığı) neden olur. RADAR bu dengeyi kurumun lehine çevirir:"

Add-BulletBoldPrefix "Güvenlik ekiplerinin gerçek tehditlere odaklanması " "— yanlış pozitif oranının %60–80 azaltılması hedefi."

Add-Bullet "Düşük riskli kullanıcılara gereksiz engel konmaması — çalışan memnuniyeti ve operasyonel verimlilik artışı."

Add-Bullet "Yüksek riskli davranışların proaktif tespiti — veri sızıntısı gerçekleşmeden müdahale imkânı."

Add-Bullet "İç tehditlerin (insider threat) davranış analizi ile erken dönemde yakalanması."

Add-Bullet "KVKK ve düzenleyici kurum gerekliliklerine uyumlu denetim izinin otomatik oluşturulması."

Add-Bullet "Kurum içi güvenliğin güçlenmesi, müşteri verisinin daha iyi korunması anlamına gelir."

Add-Bullet "Hassas müşteri verisine erişen kullanıcıların risk skorları sürekli izlenir; anormal davranış tespit edildiğinde otomatik politika sıkılaştırması ile müşteri verisi korunur."

# ===================== SORU 4 =====================
Add-Heading "Mevcut durumda sorun ne?"

Add-BulletBoldPrefix "Statik politika sorunu: " "Forcepoint DLP, kural tabanlı çalışıyor. “Hassas dosya USB’ye kopyalanırsa engelle” kuralı, finans departmanındaki bir çalışan ile IT destek personeli için aynı şekilde uygulanıyor. Bağlam yok, risk değerlendirmesi yok."

Add-BulletBoldPrefix "Uyarı yorgunluğu (Alert Fatigue): " "DLP sistemi günde yüzlerce/binlerce olay üretiyor. Güvenlik ekibi bu olayları manuel olarak inceliyor; çoğu düşük riskli veya yanlış pozitif. Gerçek tehditler uyarı yığınının içinde kayboluyor."

Add-BulletBoldPrefix "Davranış bağlamı eksikliği: " "Mevcut DLP “ne oldu” sorusuna cevap veriyor ama “kim, ne sıklıkla, normalden ne kadar farklı” sorularına cevap veremiyor. Bir kullanıcının ilk kez mi yoksa yüzüncü kez mi ihlal yaptığı bilinmiyor."

Add-BulletBoldPrefix "Reaktif yaklaşım: " "Mevcut sistem olay gerçekleştikten sonra log tutuyor. Proaktif risk tespiti, trend analizi ve erken uyarı mekanizması bulunmuyor."

Add-BulletBoldPrefix "Merkezi görünürlük eksikliği: " "Kullanıcı bazlı bütünleşik bir risk görünümü yok. Farklı kanallardan (e-posta, endpoint, ağ) gelen olaylar ayrı ayrı değerlendiriliyor; kullanıcının toplam risk profili oluşturulamyor."

Add-BulletBoldPrefix "Manuel politika yönetimi: " "Politika değişiklikleri güvenlik ekibi tarafından manuel yapılıyor. Riskin dinamik doğasına ayak uydurmak pratik olarak mümkün değil."

# ===================== SORU 5 =====================
Add-Heading "Rakiplerden farkı ne?"

Add-Body "Bu alanda güçlü oyuncular var. Aşağıda temel karşılaştırma:"

Add-BodyBoldPrefix "Forcepoint Risk-Adaptive Protection (RAP): " "130+ davranış göstergesi (IOB) ile kullanıcı risk skoru (0–100) hesaplıyor; DLP politikalarını otomatik uyarlıyor. ARIA (AI asistan) ile doğal dilde politika oluşturma desteği sunuyor. Ancak ticari lisans maliyeti çok yüksek, kuruma özel uyarlama imkânı sınırlı (vendor lock-in), Türkçe ve KVKK’ya özel uyarlama yok."

Add-BodyBoldPrefix "Microsoft Purview Adaptive Protection: " "Insider Risk Management ile DLP’yi entegre ederek kullanıcılara Minor/Moderate/Elevated risk seviyesi atıyor. Ancak yalnızca Microsoft ekosisteminde (Exchange, Teams, Endpoint) çalışıyor. Forcepoint DLP gibi üçüncü parti DLP verileriyle entegre olmuyor. E5 lisansı gerektiriyor."

Add-BodyBoldPrefix "Symantec / Digital Guardian / Trellix DLP: " "UEBA (User Entity Behavior Analytics) modülleri ile risk skorlama yapıyorlar. Ancak risk-adaptif politika uyarlama bu ürünlerde birden fazla platform entegrasyonu gerektiriyor; tek başlarına tam adaptif döngü sunmuyorlar."

Add-BodyBoldPrefix "Bizim farkımız: " "(1) Aynı vizyonu, kurumun kendi altyapısında, açık kaynak teknolojiler üzerine kurulu olarak sunuyoruz — lisans maliyeti sıfır. (2) Vendor-agnostik: Forcepoint, Splunk veya herhangi bir SIEM/DLP kaynağından veri alabiliriz. (3) Uçtan uca döngü: veri toplama → analiz → risk skoru → politika uyarlama → remediation → doğrulama. (4) Türkçe, KVKK ve kurum bağlamına özel. Bu birleşimi sunan bir örnek yok."

# ===================== SORU 6 =====================
Add-Heading "Proje hangi adımlarla uygulanır?"

Add-Bullet "Veri toplama altyapısı kurulumu: Forcepoint DLP / Splunk’tan olay loglarının Redis Stream üzerinden gerçek zamanlı toplanması; veri formatının standartlaştırılması ve veritabanına (PostgreSQL) yazılması."

Add-Bullet "Risk skorlama motorunun geliştirilmesi: Kullanıcı bazlı çok boyutlu risk skoru algoritmasının oluşturulması — olay türü ağırlıkları, sıklık analizi, hassasiyet seviyesi, zaman bağlamı, departman bazlı normal davranış profili."

Add-Bullet "Anomali tespit modülü: Kullanıcının geçmiş davranış ortalamasından sapmaların istatistiksel ve yapay zekâ tabanlı yöntemlerle tespit edilmesi; anlık risk artışının tetiklenmesi."

Add-Bullet "AI davranış analizi entegrasyonu: Azure OpenAI / OpenAI API ile davranış profillerinin doğal dilde açıklanması; güvenlik analistleri için anlaşılır, eyleme dönüştürülebilir özetlerin üretilmesi."

Add-Bullet "Adaptif politika yönetimi: Risk skoru seviyelerine göre DLP politikalarının otomatik uyarlanması — düşük risk: izin ver, orta risk: uyarı göster, yüksek risk: onay iste, kritik risk: engelle."

Add-Bullet "Dashboard ve raporlama: Next.js tabanlı modern web arayüzünde gerçek zamanlı risk haritası, kullanıcı detay sayfaları, trend grafikleri, olay inceleme ekranları ve otomatik rapor üretimi (PDF/Excel)."

Add-Bullet "Olay yönetimi ve remediation: Yüksek riskli olayların inceleme sürecine alınması, düzeltme aksiyonlarının takibi ve denetim izinin tutulması."

Add-Bullet "Test ve doğrulama: DLP test senaryoları ile sistemin uçtan uca doğrulanması; farklı risk seviyelerinde politika uyarlama davranışının test edilmesi."

Add-Bullet "Pilot uygulama: Seçili bir departman veya kullanıcı grubu üzerinde kontrollü pilot çalışma; sonuçların ölçülmesi ve ince ayar."

# ===================== SORU 7 =====================
Add-Heading "Hangi teknolojiler kullanılacak?"

Add-Bullet "Backend API: ASP.NET Core 8 (C#) — risk analizi, politika yönetimi, kullanıcı yönetimi REST API."
Add-Bullet "Veri Toplama: Redis Streams — Forcepoint/Splunk’tan gelen olay verilerinin gerçek zamanlı işlenmesi."
Add-Bullet "Veritabanı: PostgreSQL — kullanıcı profilleri, risk skorları, olay logları, konfigürasyon."
Add-Bullet "AI / LLM: Azure OpenAI / OpenAI API — davranış açıklaması, chatbot, anomali yorumlama."
Add-Bullet "Web Dashboard: Next.js (React) + TypeScript + TailwindCSS — modern, gerçek zamanlı yönetim arayüzü."
Add-Bullet "Raporlama: ClosedXML (Excel) + QuestPDF (PDF) — otomatik rapor üretimi."
Add-Bullet "Arka Plan: Background Services (.NET) — sürekli çalışan analiz, senkronizasyon ve temizlik görevleri."
Add-Bullet "SIEM Entegrasyon: Splunk REST API — DLP olay verilerinin çekilmesi."
Add-Bullet "Container: Docker — dağıtım ve ortam bağımsızlığı."
Add-Bullet "CI/CD: GitHub Actions — otomatik build, test ve dağıtım pipeline’ı."

# ===================== SORU 8 =====================
Add-Heading "Riskler, kısıtlar ve maliyetler neler?"

Add-BodyBoldPrefix "Veri kalitesi riski: " "DLP sisteminden gelen olay verilerinin formatı, eksiksizliği ve tutarlılığı risk skorlamasının doğruluğunu doğrudan etkiler. Önlem: Veri doğrulama ve temizleme katmanı; eksik/tutarsız verilerin raporlanması."

Add-BodyBoldPrefix "Yanlış pozitif/negatif riski: " "Risk skoru algoritması ilk aşamada yeterli hassasiyete ulaşamayabilir. Önlem: İnsan denetimi destekli çalışma; pilot dönemde “yalnızca izle” modunda başlayıp kademeli olarak otomatik politika uyarlamaya geçiş."

Add-BodyBoldPrefix "Adaptif politika yan etkileri: " "Otomatik politika değişikliklerinin öngörülemeyen operasyonel etkileri olabilir. Önlem: Politika değişikliklerinde sandbox modu; değişiklik öncesi denetim izi ve geri alma mekanizması."

Add-BodyBoldPrefix "Kısıt: " "Proje, mevcut DLP altyapısının (Forcepoint) ürettiği olay verilerine bağımlıdır. DLP politikalarının kendisine müdahale etmez; adaptif uyarlama, DLP API’si üzerinden gerçekleşir."

Add-BodyBoldPrefix "Maliyet: " "Açık kaynak teknolojiler (ASP.NET Core, PostgreSQL, Next.js, Redis) üzerine kuruludur; lisans maliyeti yoktur. Ana maliyet geliştirme süresi ve Azure OpenAI API kullanım ücretidir (token bazlı, düşük–orta düzey). Mevcut kurum altyapısı kullanılabilir."

# ===================== SORU 9 =====================
Add-Heading "Finansal faydası ne?"

Add-Body "Güvenlik araçları ilk bakışta yalnızca bir maliyet kalemi gibi görünür; ancak bu proje doğrudan gelire dönüşen temel faydalar sağlar:"

Add-BulletBoldPrefix "Ticari ürün lisans tasarrufu: " "Forcepoint RAP veya Microsoft Purview E5 gibi ticari risk-adaptif çözümlerin yıllık lisans maliyeti kullanıcı başına 30–80 USD arasındadır. Kurum ölçeğinde yıllık yüz binlerce USD ticari lisans maliyetinden kaçınılır."

Add-BulletBoldPrefix "Güvenlik ekibi verimlilik artışı: " "Yanlış pozitif azaltımı ile güvenlik analistlerinin olay inceleme süresinin %40–60 düşürülmesi hedeflenir."

Add-BulletBoldPrefix "Veri sızıntısı önleme: " "IBM’in 2025 Cost of a Data Breach raporuna göre bir veri ihlalinin ortalama maliyeti 4,88 milyon USD’dir. Proaktif risk tespiti ile bu riskin ciddi ölçüde azaltılması hedeflenir."

Add-BulletBoldPrefix "Düzenleyici ceza riski azaltımı: " "KVKK kapsamında veri ihlali durumunda 1–10 milyon TL arası idari para cezası uygulanabilir. Denetim izi ve uyum raporlaması ile ceza riskinin minimize edilmesi."

Add-BulletBoldPrefix "Operasyonel verimlilik: " "Düşük riskli kullanıcıların gereksiz engellerle karşılaşmaması, iş süreçlerinin aksamadan devam etmesi."

Add-BulletBoldPrefix "Ölçeklenebilirlik: " "Tek bir araçla kurumdaki tüm DLP kullanıcılarının risk profilinin yönetilmesi — yüksek tekrar kullanım değeri. İleride farklı veri kaynakları (e-posta güvenliği, CASB, endpoint) eklenerek kapsamın genişletilebilmesi."

# --- Save ---
$outputPath = "c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Ideathon_Basvuru\RADAR_Basvuru_Draft_1.docx"
$doc.SaveAs([ref]$outputPath, [ref]16)  # 16 = wdFormatDocumentDefault (.docx)
$doc.Close()
$word.Quit()

Write-Host "Done: $outputPath"
