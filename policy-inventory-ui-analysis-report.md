# Politika Envanteri UI Analiz Raporu

Tarih: 2026-07-02

## Kapsam

Bu rapor, `dashboard/app/exceptions/policy-inventory` ekranındaki mevcut politika envanteri UI akisinin, source/destination gosteriminin, arama/filtreleme davranisinin ve export yapisinin incelenmesiyle hazirlanmistir.

Incelenen ana dosyalar:

- `dashboard/app/exceptions/policy-inventory/page.tsx`
- `dashboard/app/exceptions/policy-inventory/_components/PolicyInventoryTable.tsx`
- `dashboard/app/exceptions/policy-inventory/_components/PolicyInventoryToolbar.tsx`
- `dashboard/app/exceptions/policy-inventory/_components/PolicyInventoryImportExport.tsx`
- `dashboard/app/exceptions/policy-inventory/_components/RuleFormModal.tsx`
- `dashboard/app/exceptions/policy-inventory/_components/ExceptionFormModal.tsx`
- `dashboard/app/exceptions/policy-inventory/_lib/types.ts`
- `DLP.RiskAnalyzer.Analyzer/Controllers/PolicyInventoryController.cs`
- `DLP.RiskAnalyzer.Analyzer/Services/PolicyInventoryService.cs`
- `DLP.RiskAnalyzer.Shared/Models/PolicyInventory.cs`

## Mevcut Durum Ozeti

Politika envanteri ekrani su an uc seviyeli bir yapi kullaniyor:

1. Policy
2. Rule
3. Exception

Her rule ve exception altinda classifier, severity action, source ve destination verileri bulunuyor. Backend modeli bu ayrimi zaten destekliyor. `PIRuleSource`, `PIRuleDestination`, `PIRuleChannelResource`, `PIExceptionSource`, `PIExceptionDestination` ve `PIExceptionChannelResource` ayri entity'ler olarak mevcut.

UI tarafinda source ve destination verileri ayni `SourceDestinationBlock` icinde gosteriliyor. Bu blok rule detayinda ve exception detayinda ayni sekilde kullaniliyor. Bu nedenle kullanici source ve destination'i zihinsel olarak ayri alanlar gibi degil, tek bir karisik bolum gibi algiliyor.

## Bulgular

### 1. Source ve Destination ayni panel icinde gosteriliyor

Mevcut tablo bileseninde `SourceDestinationBlock` hem source hem destination verisini tek bilesende ciziyor. Rule detayinda ve exception detayinda panel basligi da `Source / Destination` olarak geciyor.

Kod referanslari:

- `PolicyInventoryTable.tsx:206` civarinda `SourceDestinationBlock`
- `PolicyInventoryTable.tsx:309` rule detayindaki `Source / Destination` paneli
- `PolicyInventoryTable.tsx:420` exception detayindaki `Source / Destination` paneli

Etkisi:

- Severity Actions ayri bir panel olarak okunabilirken source/destination tek panelde kalabaliklasiyor.
- Destination icinde channel type ve resource listesi uzun oldugunda panel hizla uzuyor.
- Kullanici bir rule veya exception icinde "kaynak nerede bitiyor, hedef nerede basliyor" ayrimini rahat yapamiyor.

Oneri:

- `SourcePanel` ve `DestinationPanel` adinda iki ayri UI bolumu olusturulmali.
- Rule detay grid'i `Severity Actions`, `Source`, `Destination` olarak 3 alanli veya iki satirli dengeli bir yerlesime gecmeli.
- Exception detayinda da ayni ayri bloklar kullanilmali.
- Source paneli daha kompakt chip listesi olarak kalabilir; destination paneli ise type/resource odakli daha gelismis bir tasarima gecmeli.

### 2. Destination resources uzun listelerde okunabilir degil

Mevcut destination gosterimi her destination channel'i altinda tum `channel_resources` listesini dikey olarak basiyor. Resource sayisi fazla olunca panel uzuyor.

Kod referanslari:

- `PolicyInventoryTable.tsx:239-252`

Etkisi:

- Ornegin `OnlineMeetingApp` gibi resource'lar uzun listede kayboluyor.
- Hangi resource'un hangi destination type altinda oldugu goruluyor ama secilebilir/filtrelenebilir bir deneyim yok.
- Liste cok uzadiginda rule/exception detay panelinin diger bolumlerini asagi itiyor.

Oneri:

- Destination paneli su sekilde yeniden tasarlanmali:
  - Ustte channel type segmentleri veya tab'leri: `EMAIL`, `HTTP`, `ENDPOINT_APPLICATION`, `CASB_NEAR_REAL_TIME` vb.
  - Her type icin count badge: `ENDPOINT_APPLICATION (24)`
  - Secili type altinda resource listesi.
  - Resource listesi icinde lokal arama input'u.
  - Include/Exclude icin mini filtre.
  - Ilk 8-12 resource gosterilip `daha fazla` acilabilir.
  - Kayit sayisi coksa sabit yukseklik ve scroll kullanilmali.

Bu tasarimda destination type secildiginde yalnizca o type icindeki resources gorunur. Bu, kullanicinin istedigi "type sectigimde o type icinde resourcelar gorunsun" ihtiyacini dogrudan karsilar.

### 3. Arama policy seviyesinde donuyor, kural/exception seviyesinde sonuc uretmiyor

Mevcut filtreleme `policies.filter(...)` ile calisiyor. Yani eslesen herhangi bir rule veya exception varsa tum policy listeye giriyor. Ancak hangi rule'da veya hangi exception'da eslestigi sonuc modeli olarak ayrilmiyor.

Kod referansi:

- `page.tsx:79-115`

Etkisi:

- `OnlineMeetingApp` aratildiginda ilgili policy gorunebilir, fakat hangi rule veya exception icinde bulundugu net bir arama sonucu olarak cikmaz.
- Ekran policy bazli kaldigi icin kullanici policy'yi acip rule/exception seviyesinde manuel aramak zorunda kalir.
- Arama "tüm politika, kural ve exception içinde ara" beklentisini tam karsilamaz.

Oneri:

- Mevcut `filteredPolicies` yerine ikinci bir arama modeli eklenmeli:
  - `PolicyInventorySearchResult[]`
  - Her sonuc satiri su alanlari tasimali:
    - `policyId`
    - `policyName`
    - `ruleId`
    - `ruleName`
    - `scope`: `rule` veya `exception`
    - `exceptionId`
    - `exceptionName`
    - `matchArea`: `source`, `destination`, `classifier`, `severity`, `policy`, `rule`, `exception`
    - `matchField`: `resource_name`, `resource_type`, `channel_type`, `email_monitor_directions`, vb.
    - `matchedValue`
    - `include`
    - `destinationType`
    - `enabled`
  - Arama sonucu ekraninda "Policy / Rule / Exception / Alan / Eslesen Deger" kolonlari olmali.

Bu sayede `OnlineMeetingApp` aramasi sonucunda "hangi politikada, hangi kuralda, rule mu exception mi, destination type ne" bilgisi tek satirda gorulebilir.

### 4. Source ve destination aramasi sadece resource_name odakli ve eksik kapsamli

Mevcut aramada source icin sadece `resource_name` kontrol ediliyor. Destination icin `channel_type` ve `channel_resources.resource_name` kontrol ediliyor. Resource type, include, email monitor directions gibi alanlar arama kapsaminda degil.

Kod referansi:

- `page.tsx:92-101`

Oneri:

- Source aramasi su alanlari kapsamalidir:
  - `resource_name`
  - `resource_type`
  - `include`
  - scope: rule source, exception source
- Destination aramasi su alanlari kapsamalidir:
  - `channel_type`
  - `channel_enabled`
  - `email_monitor_directions`
  - `channel_resources.resource_name`
  - `channel_resources.resource_type`
  - `channel_resources.include`
  - scope: rule destination, exception destination

### 5. Export mevcut filtre sonucundan habersiz

Frontend export bileseni su an dogrudan `/api/policy-inventory/export/{format}` endpointine gidiyor. Bu endpoint filtre bilgisini, arama sorgusunu veya UI'da gorunen sonucu almiyor.

Kod referanslari:

- `PolicyInventoryImportExport.tsx:46-54`
- `PolicyInventoryController.cs:104-117`

Etkisi:

- Kullanici detayli filtreleme yaptiginda export yine tum envanteri indirebilir.
- Kullanici "filtre sonucunu export et" beklentisini alamaz.
- Mevcut export policy bazli ana veriyi indiriyor; arama sonucundaki eslesen satirlarin baglami ayrica cikmiyor.

Oneri:

- Export iki moda ayrilmali:
  - `Export Inventory`: tum politika envanteri, mevcut klasik 53 kolonlu yapi.
  - `Export Search Results`: arama/filtre sonucu, sonuc satiri bazli sade ve okunur kolonlar.
- Frontend, arama sonucu varsa export menusu icinde `Filtre Sonucunu Excel'e Aktar` ve `Filtre Sonucunu JSON'a Aktar` seceneklerini gostermeli.
- Kucuk ve orta veri setlerinde filtre sonucu frontend'de uretilip client-side XLSX/CSV olarak export edilebilir.
- Kurumsal ve buyuk veri setleri icin backend'e query parametreli veya POST body'li endpoint eklenmeli:
  - `POST /api/policy-inventory/search`
  - `POST /api/policy-inventory/search/export/excel`
  - `POST /api/policy-inventory/search/export/json`

### 6. Export JSON, channel_resources verisini tam yuklemiyor olabilir

`ExportJsonAsync` ve `ExportExcelAsync` icinde destinations include ediliyor, ancak `ThenInclude(d => d.ChannelResources)` yok. Lazy loading kapaliysa destination resource listeleri bos gelebilir.

Kod referanslari:

- `PolicyInventoryService.cs:727-735`
- `PolicyInventoryService.cs:753-760`

Not:

Excel export kodu daha asagida `d.ChannelResources` uzerinden satir olusturuyor. Fakat sorgu bu koleksiyonu eager load etmedigi icin resource kolonlari eksik kalabilir. Bu, kullanicinin "bazi sutunlari katmadan cikabiliyor" tespitine uyuyor.

Oneri:

- Export sorgularinda rule destination ve exception destination icin `ThenInclude(ChannelResources)` eklenmeli.
- JSON export da ayni sekilde nested resource verisini garanti etmeli.

### 7. Excel export kolon adlarinda tutarsizlik var

Exception destination resource kolonlarinda C36 ve C37 basliklari tekil `resource` olarak yazilmis, C35 ise `resources`. Rule tarafinda C51-C53 `resources` olarak tutarli.

Kod referansi:

- `PolicyInventoryService.cs:813-815`

Oneri:

- C36 ve C37 su sekilde duzeltilmeli:
  - `Value.rules.exception_rules.exception_rules.rule_destination.channels.resources.type`
  - `Value.rules.exception_rules.exception_rules.rule_destination.channels.resources.include`

### 8. Export satir modeli hizalama kaybina acik

Excel export, exception severity, source, destination ve rule severity/source/destination listelerini ayni `i` index'i ile satira yaziyor. Bu yaklasim, alanlar birbirinden bagimsiz listeler oldugu icin yanlis iliski algisi yaratabilir.

Kod referanslari:

- `PolicyInventoryService.cs:881-885`
- `PolicyInventoryService.cs:931-1005`

Etkisi:

- Ayni satirda gorunen exception severity ile exception destination resource gercekte ayni nested kombinasyona ait olmayabilir; sadece ayni index'e denk gelmistir.
- Liste uzunluklari farkliysa bazi alanlar bos kalabilir.
- Export okunabilir gorunse de analitik olarak yaniltici olabilir.

Oneri:

- Klasik envanter export icin iki secenek dusunulmeli:
  - Normalize edilmis coklu sheet:
    - Policies
    - Rules
    - RuleClassifiers
    - RuleSeverityActions
    - RuleSources
    - RuleDestinations
    - RuleDestinationResources
    - Exceptions
    - ExceptionClassifiers
    - ExceptionSeverityActions
    - ExceptionSources
    - ExceptionDestinations
    - ExceptionDestinationResources
  - Arama sonucu export icin tek flat sheet:
    - Policy
    - Rule
    - Scope
    - Exception
    - Match Area
    - Match Field
    - Matched Value
    - Destination Type
    - Resource Type
    - Include
    - Enabled

### 9. Rule/Exception CRUD update alt koleksiyonlari guncellemiyor

Rule ve Exception modal'lari classifier, severity, source, destination array'lerini save payload'una koyuyor. Ancak backend update metodlari sadece ana alanlari guncelliyor. Alt koleksiyonlar update edilmiyor.

Kod referanslari:

- `RuleFormModal.tsx` save payload icinde `classifiers`, `severity_actions`, `sources`, `destinations` gonderiliyor.
- `ExceptionFormModal.tsx` save payload icinde ayni alt koleksiyonlar gonderiliyor.
- `PolicyInventoryService.cs:1064-1075`
- `PolicyInventoryService.cs:1100-1116`

Etkisi:

- UI'da source/destination duzenlense bile update isleminde veritabanina yansimayabilir.
- Mevcut destination resource duzenleme zaten UI'da yok; ileride eklenirse backend update de mutlaka koleksiyon replace/upsert desteklemeli.

Oneri:

- UpdateRule ve UpdateException icin transactional replace stratejisi uygulanmali:
  - Existing rule/exception altindaki child koleksiyonlari include ile yukle.
  - Gelen payload'a gore classifier/severity/source/destination/resource koleksiyonlarini replace et.
  - Id varsa update, yoksa insert, payload'da olmayan eski kayit varsa delete stratejisi netlestir.

## Onerilen UI Tasarimi

### Politika envanteri ana ekran

Ana ekran iki moda sahip olmali:

1. Envanter agaci
   - Policy > Rule > Exception hiyerarsisi
   - Detay panellerinde Classifier, Severity, Source, Destination ayri bloklar

2. Arama sonucu modu
   - Arama sorgusu varsa otomatik veya kullanici secimiyle aktif olur
   - Policy bazli degil, eslesen rule/exception satirlari bazli liste verir
   - Satira tiklayinca ilgili policy/rule/exception expand edilir veya detail drawer acilir

### Destination paneli

Destination paneli su yapida olmali:

- Header:
  - `Destination`
  - toplam channel count
  - toplam resource count
  - active channel count
- Type selector:
  - segmented control veya chip tabs
  - `All`, `EMAIL`, `HTTP`, `ENDPOINT_APPLICATION`, vb.
- Resource filter:
  - local search input
  - include/exclude toggle
  - enabled channel toggle
- Resource list:
  - kompakt rows
  - resource name
  - resource type badge
  - include/exclude badge
  - channel type badge
  - max height + scroll

### Source paneli

Source daha basit tutulabilir:

- resource type'e gore group
- local search
- include/exclude badge
- count badge

## Onerilen Teknik Plan

### Faz 1: UI ayrimi ve destination okunabilirligi

- `SourceDestinationBlock` iki bilesene ayrilsin:
  - `SourcePanel`
  - `DestinationPanel`
- Destination paneline type filtre state'i eklensin.
- Resource listesi scroll'lu ve search'lu hale getirilsin.
- Rule ve exception detaylarinda `Source / Destination` paneli kaldirilip iki ayri panel kullanilsin.

### Faz 2: Granuler arama sonucu

- `buildPolicyInventorySearchResults(policies, query, filters)` helper'i eklensin.
- Arama sonucu tipi tanimlansin.
- Toolbar'a arama kapsami ve alan filtreleri eklensin:
  - All
  - Policy
  - Rule
  - Exception
  - Source
  - Destination
  - Classifier
  - Severity
- Sonuc tablosu eklensin.
- Eslesen deger highlight edilsin.

### Faz 3: Filtre sonucu export

- Frontend'de `ImportExport` bileseni mevcut search result listesini prop olarak alabilsin.
- Arama sonucu varsa export menusu sunlari gostersin:
  - `Tum Envanteri Export Et`
  - `Filtre Sonucunu Export Et`
- Ilk uygulama icin client-side CSV/XLSX yeterli olabilir.
- Backend destekli export gerekiyorsa search endpointleri eklenmeli.

### Faz 4: Backend export dogrulugu

- Export sorgularina `ThenInclude(ChannelResources)` eklenmeli.
- Exception destination resource kolon basliklari duzeltilmeli.
- Mümkünse normalize coklu sheet export yapisi eklenmeli.
- Mevcut 53 kolonlu export korunacaksa adi `Legacy Inventory Export` gibi ayrilmali.

### Faz 5: CRUD child collection update

- Rule ve Exception update metodlari alt koleksiyonlari da guncelleyecek sekilde genisletilmeli.
- Destination resource editor UI'a eklendikten sonra bu backend destek zorunlu hale gelir.

## Onceliklendirme

Yuksek oncelik:

- Source ve Destination panellerinin ayrilmasi
- Destination type/resource filtreli panel
- Granuler arama sonucu modeli
- Filtre sonucu export
- Export sorgularina `ChannelResources` include eklenmesi

Orta oncelik:

- Excel export kolon basliklarinin duzeltilmesi
- Export'un normalize multi-sheet hale getirilmesi
- Search result satirindan ilgili rule/exception detayina jump

Daha sonra:

- Rule/Exception form modal'larinda destination resource editor
- Backend child collection upsert/replace mekanizmasi
- Arama ve export icin backend tarafli endpointler

## Sonuc

Mevcut backend veri modeli source/destination ayrimini ve destination resource hiyerarsisini destekliyor. Sorunun ana kaynagi veri modelinden cok UI sunumu, filtre sonucunun policy seviyesinde kalmasi ve export'un UI filtre baglamindan kopuk olmasi.

En dogru ilerleme sirasi:

1. UI'da Source ve Destination'i ayirmak.
2. Destination panelini type secimli ve resource filtreli hale getirmek.
3. Policy bazli filtre yerine granuler arama sonucu uretmek.
4. Bu arama sonucunu export edebilmek.
5. Backend export include ve kolon sorunlarini duzeltmek.

