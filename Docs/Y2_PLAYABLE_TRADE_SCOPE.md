# Y2 Pazar ekranı ve yetkili kervan işlemleri

Bu geliştirme adayı, kullanıcının karakter görseli dışındaki oynanış işlerine devam talebiyle Y1 harita/yolculuk adayının üzerine kurulur. Başlangıç SHA `a9e381cd2421e12d71e1d0c701e2ef920a3ead68`, dal `codex/y2-playable-trade`.

## Kabul sınırı

Y0 uzun kullanım ve fiziksel giriş kabulü açık kalır. Bu paket bunları kaldırmaz ve tam Y2 oyuncu döngüsünü READY ilan etmez. Başlangıçtaki tek kervanın sahibi/yöneticisi Mehmed Çelebi'dir; Hasan Ağa değildir. Hasan'a NPC kervanının parası, yükü veya yönetim yetkisi verilmez. Hasan'ın ticarete katılacağı meşru gameplay yolu ayrı açık tasarım kararıdır. Kullanıcıya bu seçenek sorulmuştur; cevap gelene kadar mevcut yetkiler korunur.

Normal Windows kampanyasında Hasan bildiği pazarın malını, stok ve talebini, içerikteki referans fiyatını ve seçtiği miktarın toplamını görebilir. Yetkisi olmayan alım/satım kapalıdır ve nedeni görünür. Kesin şehir bilgisi, yabancı kervanın yükü veya işletme bakiyesini görme izni değildir.

## Kilitli kapsam

- Gerçek kampanya kayıtlarından pazar, mal, kervan ve miktar seçimi.
- Miktar/fiyat önizlemesi; alım veya satış için açık onay ve vazgeçme.
- Mevcut manager kimliğinin doğrulanması; ölü/esir veya etkin olmayan aktörün işlem yapamaması.
- Mevcut TradeTransactionService üzerinden atomik stok, para, yük ve muhasebe güncellemesi.
- Kaynak fiyatı: TradePriceRules + içerikteki reference value; ek PriceAdjustment yok. Talep otomatik fiyat çarpanı yapılmaz.
- Alım yalnız çıkış pazarında AtOrigin, satış yalnız varış pazarında AtDestination. Taşıma UI'si veya Y4 ortak zamanlama eklenmez.
- Yetersiz stok/yük/para/kapasite, konum, geçersiz miktar, taşma ve yetki için görünür engel.
- Tek kullanımlık onay. Beklerken ilgili stok/para/yük/muhasebe değişirse eski onay reddedilir. Seçim, ekran değişimi veya yeniden yükleme eski onayı iptal eder.
- Bounded UI yaşam döngüsü, v14 kayıt devamlılığı ve gerçek Windows panel kanıtı.

Domain, save şeması, mevcut fiyat katsayıları, başlangıç serveti, sahiplik ve at/karakter/şehir sanatı değiştirilmez. Implementation 15 başlamaz. Orijinal çalışma kopyası korunur.

## Uygulama sınırları

TradeOrderSession uygulama katmanında actor-bound kontrol ve geçici onayı tutar; transfer için mevcut servisi çağırır. TradePanelSession sunum seçimini ve bilgi sınırını tutar. TradePanel yalnız retained UI Toolkit kontrolleridir. Onay nesnesi SaveData değildir; yüklemede yeni session oluşturulur. RNG veya dünya saati UI seçiminden etkilenmez.

Mevcut BuildTrade sorgusundaki şehir bilgisi üzerinden yabancı kervan yükünün açığa çıkması kapatılmıştır. CanReadExact hâlâ komut yetkisi değildir. Kervan sahibi olmak da manager kontrolünün yerine geçirilmez.

## Doğrulama

Yeni davranış testleri yetki, görünürlük, salt okunur seçim, atomiklik, para/mal korunumu, çift onay, eski onay, kapasite ve bakiye reddi, gerçek yolculuk sonrası satış ve v14 save roundtrip kapsar. Unity panel testleri varsayılan Hasan engelini, retained kontrol yenilemesini, tek işlem ve callback temizliğini sınar.

`Tools/Test-WindowsTrade.ps1` yalnız izole test kayıt dizininde çalışır. Önce normal Hasan ekranının engelini denetler. Sonra açıkça etiketlenmiş test viewer'ı olarak mevcut yönetici Mehmed'i kullanır; kampanya kimliği, sahiplik, kaynak veya production kontrol aktörü değiştirilmez. Satış fixture'ı mevcut TravelCommandService ile gerçek rotayı tamamlar. Bu, oyuncu-facing kervan sevk emri veya Y4 kabulü değildir. V14 serializer/validator ve disk roundtrip ayrıca doğrulanır; normal Windows save/load smoke ayrı çalıştırılır.

Windows PNG'leri gerçek D3D11 UI Toolkit panelinin render hedefinden alınır. Sentetik ChangeEvent/NavigationSubmit girdileri fiziksel fare/klavye kabulü sayılmaz. İstenen render boyutu ve gerçek pencere boyutu ayrı kaydedilir. Normal oyuncu kayıtlarının hash, boyut ve zaman damgaları değişmemelidir.

## Açık kapılar

- Y0 normal iki saatlik Windows kullanım kabulü.
- Fiziksel fare/klavye kullanıcı kabulü.
- Hasan'ın kişisel ticaret yetkisi, kervan edinmesi veya temsil ilişkisi için authority kararı; ücretsiz kaynak veya NPC yönetim devri yok.
- Y4 oyuncu-facing kervan sevki/ortak kampanya zamanı; bu aday otomatik sefer veya kâr dağıtımı eklemez.
- Tam 14C art ve ProductionArt kapıları ayrıdır.

Final commit için taze .NET, Unity, 11 mevcut pipeline, Windows save smoke, harita regression, ticaret render testleri ve Foundation CI gerekir. Geliştirme testleri final SHA kanıtı olarak taşınmaz.
