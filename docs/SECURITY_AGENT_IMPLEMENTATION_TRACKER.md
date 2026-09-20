# Guvenlik Agent'i Uygulama Takibi

Bu dosya Local LLM Laboratuvari ve Guvenlik Agent'i gelistirmesinin kalici
calisma kaydidir. Her asama test edilip commitlenecek; uzak depoya gonderim
kullanici onayiyla yapilacaktir. Bir asama, gerekli kod ve test tamamlandiginda
`Tamamlandi` durumuna gecirilebilir; push durumu kayitta ayrica belirtilir.

## Hedef

Agent; dogal sohbetle analiz istegini anlayacak, milyonlarca olayi modele
tasimadan kanita dayali risk bulgulari uretecek, e-posta ve workflow taslagi
hazirlayacak; gonderim veya etkinlestirme oncesinde insan onayi isteyecek.

## Asama Plani

| No | Asama | Cikti | Durum |
| --- | --- | --- | --- |
| 0 | Entegrasyon tabani | Pull sonrasi cakisma cozumlenir, takip kaydi ve dogrulama yapilir. | Tamamlandi |
| 1 | Yerel model sagligi | Baglanti/model kontrolu, zaman asimi, hata ayrimi ve tekrar deneme. | Tamamlandi |
| 2 | Dogal sohbet yonlendirme | Selamlasma ve yardim sorulari olaysiz; analiz istegi arac ve kapsam secimiyle calisir. | Tamamlandi |
| 3 | Token-butceli risk kaniti | Periyot, aggregate, aday, timeline ve kanit katmanlariyla 20K+ olay ozetlenir. | Tamamlandi |
| 4 | Sahsi e-posta kimlik eslesmesi | Sahsi domain, mailbox-onu kanoniklestirme ve guven skoru hesaplanir. | Tamamlandi |
| 5 | Sablon destekli e-posta taslagi | Kayitli e-posta sablonu, olay kaniti ve model taslagi birlestirilir. | Tamamlandi |
| 6 | Duzenleme, onay ve denetim izi | Taslak duzenleme, yetkili onay/red, audit kaydi ve kontrollu gonderim. | Tamamlandi |
| 7 | Guvenlik agent'i araclari | Kullanici/olay kaniti, workflow simulasyonu ve yalniz taslak olusturma. | Tamamlandi |
| 8 | Kapsam ve kalite | Kural-kapsama denetimi, shadow mode, olcumleme ve regresyon degerlendirmesi. | Tamamlandi |

## Asama 4 - Sahsi E-posta Kimlik Eslesmesi

Bir hedef adresin sahsi domain'de olmasi tek basina ihlal veya kimlik
eslesmesi anlami tasimaz. Karar, gorunen ad yerine dogrulanmis `local@domain`
adresi uzerinden uretilir.

| Kontrol | Kural | Sonuc |
| --- | --- | --- |
| Sahsi domain | Merkezi ve surumlenmis saglayici listesi: Gmail/Googlemail, Hotmail/Outlook/Live/MSN, Yahoo, iCloud/Me, Proton ve Yandex. | `isPersonalDomain` |
| Guvenli ayrisma | Adres bir e-posta ayrisicisi ile okunur; regex yalnizca bicim dogrulamada kullanilir. | Hatali gorunen-ad eslesmesi engellenir. |
| Kanoniklestirme | Kucuk harf, Turkce karakter normalizasyonu, bosluk/nokta/alt-cizgi/tire temizligi. Gmail icin bilinen nokta ve `+etiket` kurali uygulanir; diger domain'lere genellenmez. | Karsilastirilabilir kullanici adi |
| Kesin eslesme | Kurumsal gonderen mailbox-onu ile hedef mailbox-onu ayni kanonik degere sahiptir. | Yuksek guven |
| Isim destegi | Dizin adi/soyadi ile iki parcanin eslesmesi; kisa kullanici adlari, bas harfler ve tek basina benzerlik pozitif sayilmaz. | Orta veya yuksek guven |
| Bulanik eslesme | Yalniz yeterince uzun degerlerde, sinirli edit mesafesi ve token parcasi korumalariyla kullanilir. | Dusuk/orta guven, insan incelemesi |

Ornek: kurumsal kullanici `abc@kuveytturk.com.tr` hedefe
`abc@hotmail.com` gonderiyorsa `exact-local-part` ve yuksek guvenli inceleme
bulgusu uretilir. Bu bulgu otomatik engelleme degil, listeleme/risk sinyali ve
onay akisina girdidir.

## Tamamlanan Isler Kaydi

| Tarih | Asama | Durum | Not |
| --- | --- | --- | --- |
| 2026-09-20 | 0 | Tamamlandi | Uzak depodaki Local LLM Laboratuvari ve Guvenlik Agent'i degisiklikleri alindi. Exceptions sayfasindaki cakismada sayfali yukleme korunarak cozumlendi. `dotnet build --no-restore` ve `dashboard npm run build` basarili. |
| 2026-09-20 | 4 | Tamamlandi | Merkezi e-posta ayrisici/eslestirici eklendi. Yuksek guvenli mailbox-onu eslesmeleri haftalik inceleme listesine ve sahsi sablon yonlendirmesine baglandi; arayuz kaniti gosteriyor. 331 backend testi ve `npx tsc --noEmit` basarili. |
| 2026-09-20 | 1 | Devam ediyor | Yerel model yanit hatalari saglik kontrolu, ayrintili hata kodu ve kontrollu tekrar deneme ile ele aliniyor. Bu ve sonraki asamalar sadece commitlenecek; push kullanici talimatiyla yapilacak. |
| 2026-09-20 | 1 | Tamamlandi | Baglanti testi yapilandirilmis saglik sonucu donduruyor. Erisilemeyen sunucu, timeout, bulunamayan model/endpoint, mesgul model, gecersiz veya bos yanit ayrildi; gecici hatalarda bir kontrollu tekrar deneme eklendi. 335 backend testi ve TypeScript kontrolu basarili. Commit var, push kullanici talimati bekliyor. |
| 2026-09-20 | 2 | Tamamlandi | Sohbet niyeti ayristirildi. Selamlasma ve tesekkur yerel yanitla, model ve olay kaydi cagirmadan tamamlanir; serbest sohbet kucuk model baglamiyla, olay/risk/mail/workflow talepleri ise olay kanitiyla calisir. 336 backend testi basarili. Commit var, push kullanici talimati bekliyor. |
| 2026-09-20 | 3 | Devam ediyor | Kapsamli analizdeki aday ve timeline kaniti sabit bir prompt butcesine sinirlanacak; 20K+ olay ham olarak modele aktarilmayacak. |
| 2026-09-20 | 3 | Tamamlandi | Kapsamli analizde model kaniti 8 kullanici, kullanici basina 8 sayisal grup, 18 timeline satiri ve 3 kanal oruntusu ile sinirlandi; kanit metni 24.000 karakter tavanina alindi. Sunucu tum donemi taramaya devam eder, kanal gecisi taramasi dogrusal hale getirildi. 337 backend testi ve TypeScript kontrolu basarili. Commit var, push kullanici talimati bekliyor. |
| 2026-09-20 | 5 | Tamamlandi | Taslaklar olay hedefi, politika ve sablon icerigiyle puanlanarak kayitli kurum sablonunu kullanir. Uygun sablon yoksa model en fazla bir kez yeni JSON sablon onerir; bu onerinin kalici katalogya yazilmasi yasaktir ve taslakta ayri etiketlenir. Model onerisi alinamazsa denetlenebilir varsayilan metin kullanilir. Alici, konu ve govde her durumda kullanici onayindan once duzenlenebilir. 339 backend testi ve TypeScript kontrolu basarili. Commit var, push kullanici talimati bekliyor. |
| 2026-09-20 | 6 | Tamamlandi | Taslak sahibi, kendi sohbetindeki taslagi duzenleyebilir, onaylayabilir veya reddedebilir; sorgular sahiplik filtresiyle sinirlidir. Onay/red karari veren kullanici, zaman ve karar kalici saklanir ve arayuzde gorunur. Genel audit kaydi Local LLM istek govdelerini (e-posta icerigi dahil) kaydetmez; taslak islemlerinde kaynak kimligi izlenir. 340 backend testi ve TypeScript kontrolu basarili. Commit var, push kullanici talimati bekliyor. |
| 2026-09-20 | 7 | Tamamlandi | Agent mevcut workflowlari forceDryRun ile simule eder; calistirma kaydi ve node/etki ozeti olusur ancak e-posta gonderimi veya workflow etkinlestirmesi olmaz. Kapsama kaniti salt-okunur baglamda kalir; model yalniz pasif workflow taslagi olusturabilir. 340 backend testi ve TypeScript kontrolu basarili. Commit var, push kullanici talimatiyla yapilacak. |
| 2026-09-20 | 8 | Devam ediyor | Mevcut incident, gunluk risk ve Isolation Forest aciklama verileri uzerinde aksiyon almayan, aciklanabilir risk shadow katmani tasarlaniyor. |
| 2026-09-20 | 8 | Tamamlandi | Gunluk risk, kisisel baz cizgisi farki ve Isolation Forest kanitlari birlesen, salt-okunur shadow risk katmani eklendi. Agent ve arayuz adaylari skor, guven ve kanitla gosterir. Analist dogrulama/yanlis-pozitif geri bildirimi birakabilir; precision metriği kayitli geri bildirimlerden hesaplanir. Bu katman skor, workflow veya mail akislarini otomatik degistirmez. Shadow risk ve mevcut risk skorlama testleri basarili; commit var, push kullanici talimati bekliyor. |
