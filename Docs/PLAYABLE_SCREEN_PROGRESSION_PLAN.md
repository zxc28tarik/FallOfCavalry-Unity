# Fall of Cavalry oynanabilir ekranlar ilerleme planı

Güncel geliştirme önceliği aşağıdaki Sol High geliştirme sırasıdır. Açık uzun kullanım kapısı: [Y0 devam notu](Y0_HANDOFF.md). Harita adayının kapsamı: [Y1 kapsamı](Y1_MAP_TRAVEL_SCOPE.md).

8 Ekim 2026 önceliği: kullanıcı kılıç ve eyer sonucunu onaylayıp karakter görseli dışındaki büyük işlere devam edilmesini istedi. Y1 ayrı bir dalda geliştirme adayı olarak hazırlanır; Y0 uzun kullanım kabulü ve Y1'in birleşik kabul önkoşulu açık kalır. Bu geliştirme önceliği Implementation 15'i veya sanat kapılarını açmaz.

Harita ve normal oyun ekranları, şehir ve asker sanatını beklemeden geliştirilecek. Amaç, mevcut sistemleri oyuncunun gerçekten kullanabildiği bir kampanya döngüsüne bağlamak. Şehir yönetim ekranının tamamlanması şehirlerin 3D görünüşünün; ordu veya savaş sonuç ekranının tamamlanması asker görsellerinin kabul edildiği anlamına gelmez.

Bu bir ilerleme planıdır. Paketler tamamlanmış değildir ve bu plan Implementation 14C kapsamını genişletmez. Yeni oynanış uygulamaları başlamadan ilgili paketin kapsamı ve gerekli tasarım kararları ayrıca kilitlenir. Implementation 15 başlamaz.

## Kapsam

Dahil: mevcut harita, yolculuk, ticaret, ordu, şehir, diplomasi, rapor, karşılaşma, sözleşme ve savaş ekranlarının işlevleri; gerçek Application servislerine bağlantılar; bu işlevlerin kayıt ve yeniden yükleme sonrası devamı; Windows oyuncusunda arayüz doğrulaması.

Hariç: şehir çevresi ve bina modelleme, asker ve karakter modelleme, kıyafet, rig, texture, at ve ekipman sanatı, karakter animasyon kalitesi, production visual catalog aktivasyonu, ana hikâye ve Implementation 15. Yeni şehirler veya büyük tarihsel içerik genişlemesi ilk oynanabilir döngünün önkoşulu değildir.

Mevcut mimari korunur. Ekranlar oyun durumunun sahibi olmaz; değişiklikler mevcut Application servislerinden geçer. SaveData ile RuntimeState ayrı kalır. RNG, zaman ve işlem sırası deterministik olur. AI ve oyuncu aynı yetki, kaynak ve bilgi kurallarını kullanır. Din farkı tek başına otomatik ceza üretmez.

Mevcut kayıt sürümü v14'tür. İlk bağlantı paketlerinde gereksiz schema değişikliği yapılmaz. Daha sonraki onaylı bir özellik yeni kalıcı durum gerektirirse, migration ve sürüm etkisi uygulama öncesinde ayrı değerlendirilir; yeni durumu kaydetmemek kabul edilemez.

## Başlangıç durumu

7 Ekim 2026 yerel plan başlangıcı:

- Branch: codex/impl-14c-production-character-art.
- Kod başlangıç SHA: 537805b8556d5f20f2f097e8f42c427d63b76ac7.
- Mevcut Windows bootstrap harita, yolculuk ve kayıt/yükleme yüzeyi sunuyor. Ticaret ve Ordu command adapter'larının executable bağlantıları tamamlanmış değil.
- Tarihsel başlangıç kampanyası dört şehir, bir kervan ve küçük bir ordu içeriyor. Başlangıçta Battle, Encounter veya Contract oluşturmuyor.
- Kayıt kurtarma, yedek koruması, izole Windows kayıt testi, yinelenen kayıt callback'leri ve 5000 adımlık sınırlı kampanya replay testi üzerinde çalışma yapıldı.
- İki saatlik Windows testi 882 saniye ve 152 kayıt/yükleme döngüsünden sonra MANAGED_GROWTH_OVER_64_MIB ile FAIL oldu. Normal oyuncu kayıtları değişmedi. Gerçek tutulan nesne artışı, geçici allocation veya ölçüm davranışı ayrımı henüz kesinleşmedi.
- Yeni oynanış paketlerinin hiçbiri bu planın hazırlanmasıyla tamamlanmış veya başlamış sayılmaz.

## Paket bağımlılıkları

Y kodları yalnız bu planın takip etiketleridir; yeni Implementation numaraları değildir. Süreler, gerekli kararlar hazırken geliştirme ve doğrulama için yaklaşık aktif çalışma günüdür. Takvim veya teslim taahhüdü değildir.

| Paket | Oyuncuya sunulan sonuç | Önkoşul | Tahmini süre | Durum |
| --- | --- | --- | --- | --- |
| Y0 Kararlılık | Uzun kullanımda güvenilir ekran ve kayıt akışı | Mevcut başarısız testin teşhisi | 1–3 gün ilk teşhis ve düzeltme turu | NOT READY; temiz 4b4d015 testi 1194 saniyede bellek sınırından kaldı |
| Y1 Harita ve yolculuk | Konum seçme, rota ve varış bilgisi, yolculuk başlatma | Birleşik kabul için Y0 | 2–4 gün | Ayrı dalda geliştirme adayı; kapanış bekliyor |
| Y2 Ticaret | Gerçek alım ve satım, stok, para ve kapasite geri bildirimi | Y1 ve mevcut ekonomi servisleri | 2–4 gün | Teknik aday PASS; Hasan ticaret yetkisi ve birleşik kabul açık |
| Y3 Ordu yönetimi | Asker toplama, ikmal, maaş ve mevcut komuta işlemleri | Y2 ve mevcut askerî servisler | 3–5 gün | Sınırlı teknik UI adayı; final SHA raporu ayrı, birleşik kabul açık |
| Y4 Kampanya zamanı ve kervan | Zamanla ilerleyen üretim, tüketim, yolculuk, teslim ve kervan muhasebesi | Y1–Y3 ve onaylı zamanlama kuralları | 5–10 gün | Karar gerekli |
| Y5 Şehir ve üretim | Alanlar, üretim, ihtiyaçlar ve yetkililerin kullanılabilir ekranı | Tam üretim akışı için Y2 ve Y4 | 5–10 gün | Y5A salt okunur bilgi alt paketi; üretim emri / cadence kararı açık |
| Y6 Diplomasi ve raporlar | Yetkili gönderim, teslim durumu ve bilgi güncelliği | Y4 ve mevcut Diplomacy servisleri | 4–7 gün | Kısmen karar gerekli |
| Y7 Karşılaşma ve sözleşmeler | Olay seçimi, sözleşme kabulü, ilerleme ve sonuç | Y4, Y6 ve onaylı içerik | 5–10 gün | Karar gerekli |
| Y8 Savaş hazırlığı ve sonuç | Yerleştirme, emirler, kurallı çözümleme ve kampanyaya dönüş | Y3 ve onaylı combat politikaları | 10–20 gün | Karar gerekli |
| Y9 Birleşik oyuncu kabulü | Yukarıdaki işlemlerin tek kampanyada güvenilir kullanımı | Uygulanan paketlerin kapanışı | 3–7 gün | Planlandı |

Y0 için süre yalnız ilk inceleme turudur. Sorun derin bir nesne yaşam döngüsü veya engine davranışı çıkarsa yeniden tahmin edilir. Yeni özellikler, başarısız testi gizlemek için eklenmez; sınır yalnız testi geçirmek amacıyla yükseltilmez.

## Sol High geliştirme sırası

8 Ekim 2026 kullanıcı kararı: bağımsız Sol High işleri önce geliştirilir; bunlar tamamlandıktan sonra Astra gerektiren paketlere geçilir. Bu sıra model ayarını kendiliğinden değiştirmez. S etiketleri yalnız takip sırasıdır; yeni Implementation numaraları değildir.

Teknik başlangıç dalı codex/y5a-city-inspection-ui, kod SHA a3f5e7776999d524844f3cba2786064c549f2baa. Harita/yolculuk, ticaret, ordu/ikmal ve Y5A şehir inceleme adayları korunur. Y0 uzun kullanım, fiziksel giriş, Hasan ticaret yetkisi ve birleşik kabul açık kalır. Bu öncelik değişikliği onları PASS yapmaz.

İlk dalga mevcut gerçek verileri oyuncunun bilgi sınırları içinde gösterir. Yeni yetki, dünya bilgisi, fiyat, üretim/tüketim takvimi, savaş veya kaynak yaratmaz. Salt okunur ekranlarda kampanya durumu inceleme boyunca değişmez. Başlangıçta veri bulunmayan ekranlar açıklamalı boş durum gösterir; test fixture'ları oyuncu kampanyasına eklenmez.

| Sıra | Önceki 50 maddelik listedeki işler | Sol High kapsamı | Bağımlılık ve sınır |
| --- | --- | --- | --- |
| S1 | 1–2 Rapor kutusu ve rapor ayrıntısı | Arama/filtre, kaynak, gözlem/teslim zamanı, bilgi eskiliği ve belirsizlik | Yalnız oyuncuya teslim edilmiş raporlar; gönderim veya yeni teslim zamanlaması yok |
| S2 | 3 Elçi takibi | Bilinen görevlerin mevcut lifecycle ve teslim durumları | Görev bilgisi için erişim sınırı; yeni elçi gönderimi yok |
| S3 | 4 Diplomatik ilişkiler | Bilinen ilişkiler, faktörler ve anlaşma ayrıntıları | Gizli veya uzaktaki ilişkiler için sınırsız dünya erişimi yok; yeni anlaşma etkisi yok |
| S4 | 5 Karakter ayrıntıları | Bilinen kimlik, görev, konum, ilişki ve durum kayıtları | Karakter görüntüsü ve yeni Character üretimi yok; güncel gizli konum açılmaz |
| S5 | 6 Organizasyon ayrıntıları | Üyelik, görev, rol ve yetkiyi ayrı gösterme; izinli karakter ayrıntısına bağlantı | S4 bağlantıları; yeni atama veya komuta yetkisi yok |
| S6 | 7 Aile ve hane | Mevcut akrabalık, üyelik, hane başı, servet ve mülk referansları | Family, Household ve House ayrımı; veraset ve servet dağıtımı yok |
| S7 | 8–9 Klik, din ve mezhep bilgileri | Mevcut üyeler, lider, nüfuz kaynakları ve bilinen bağlılıklar | Yeni siyasi etki veya din farkından otomatik bonus/ceza yok |
| S8 | 10 Asker listesi | Gerçek Soldier kayıtları, birlik bağlantıları ve durumları | Toplam UnitGroup personeli Soldier instance sayısı gibi gösterilmez |
| S9 | 11 Teçhizat inceleme | S8'deki askerin kalıcı loadout'u, gerçek ekipman ve at kayıtları | Salt okunur; yeni Soldier veya ücretsiz ekipman yok; model/rig/texture değişmez |
| S10 | 12 Kervan muhasebesi | Bilinen kervanların mevcut alış/satış/gider/kayıp/net toplamları | Yeni işlem geçmişi tutulmaz; kişisel, hane ve kervan parası birleştirilmez |
| S11 | 13 Kayıt seçme ve kurtarma arayüzü | Mevcut kayıt/yedek seçimini ve kurtarma açıklamalarını kullanılabilir hâle getirme | Mevcut recovery koordinatörü ve kayıt güvenliği korunur; otomatik silme, üzerine yazma veya yeni schema yok |
| S12 | 14 Arayüz iyileştirmeleri | Yeni ekranlar arasında gezinme, uzun listeler, metinler, kaydırma ve hata açıklamaları | S1–S11 sonrası ortak düzenleme; ilgisiz ekranların mimarisi yeniden yazılmaz |
| S13 | 36–37 Fiziksel giriş ve çözünürlük/DPI | Uygulanmış ekranları gerçek Windows player'da fare/klavye ve ekran ölçeğiyle doğrulama | Sentetik olaylar fiziksel kabul sayılmaz; destekli gerçek giriş yolu yoksa Not Run açıkça raporlanır |
| S14 koşullu | 31–32 Savaş yerleştirme ve emir ekranları | Mevcut deployment/order sözleşmelerini oyuncu yüzeyine bağlama | Yalnız gerçek Battle kaydı, bilgi ve emir yetkisi mevcutsa; başlangıç kampanyasında Battle yok. Eksik önkoşul için savaş/otorite uydurulmaz; bu işler Astra sonrası sıraya devredilir |

İlk uygulama paketi S1'dir; salt okunur rapor kutusu adayı codex/s1-report-inbox-ui dalında geliştirilir. Kilitli sınırlar ve final doğrulama yolu [S1 rapor kapsamındadır](S1_REPORT_INBOX_SCOPE.md). Her paket öncesinde mevcut SHA/worktree, kullanılan gerçek servisler, bilgi/işlem yetkileri ve acceptance kapsamı kilitlenir. Bilgi erişimini yeni bir tasarım kuralı gerektirmeden güvenli biçimde kurmak mümkün değilse yalnız o alt kapsam ertelenir; bağımsız sıradaki işe devam edilir. Ertelenen iş tamamlandı olarak raporlanmaz.

S1 salt okunur teknik adayı 44413f930808d5fed301d2261ddc758d7b9da142 üzerinde 639 .NET ve 978 Unity testi, mevcut pipeline/build kapıları, izole kayıt smoke ve üç çözünürlükte Windows rapor akışıyla doğrulandı. Bu Y0 veya birleşik kabul değildir. S2 codex/s2-envoy-tracking-ui dalında bu SHA'dan ilerler; [elçi defteri kapsamı](S2_ENVOY_TRACKING_SCOPE.md) uzak canlı phase ve otomatik teslim teyidini açmaz. Onaylı teyit sözleşmesi olmayan alt kapsam ertelenir, tamamlandı sayılmaz.

Her pakette ilgili yeni regression testleri, tam .NET/Unity suite, mevcut ilgili pipeline'lar ve gerçek Windows akışı çalıştırılır. Push kapsamındaki kapanışta local/remote/CI SHA eşitliği kontrol edilir. Bunlar 41. maddedeki final doğrulamanın paket düzeyindeki kısmıdır; bütün kampanyanın kabulü değildir. Yeni final SHA için eski test kanıtı kullanılmaz.

Sol dalgasında 38–40. maddelerden uygulanmış ekranların kayıt sürekliliği ve sınırlı UI yük ölçümleri yapılabilir. Henüz geliştirilmemiş üretim, diplomatik gönderim, karşılaşma ve savaş döngülerinin birleşik kabulü tamamlandı sayılmaz. S11'de bir kayıt güvenliği problemi veya herhangi bir pakette kararlılığı engelleyen kritik hata bulunursa gerekli kapı FAIL kalır; model sırasını korumak uğruna hata gizlenmez.

## Astra aşamasına geçiş

Sol dalgasının tamamlanan ve ertelenen işleri, final kod SHA'sı, test/Windows kanıtları ve açık kararları tek devam notunda toplanır. Ayrı sanat kapıları, Save v14 ve Implementation 15 sınırı korunur. Bu aşama planı mekanik sayıları veya yeni authority kararlarını kendiliğinden onaylamaz.

Astra sırası:

1. 35 Uzun kullanım bellek teşhisi ve Y0 kabulü. Önce gerçek neden; ardından gerekli minimum düzeltme ve normal uzun test.
2. 15–20 Kampanya zamanı, oyuncu kervan yetkisi, tam kervan döngüsü, üretim, tüketim ve fiyat kuralları.
3. 21–27 Teçhizatlandırma, yetkili atamalar, maaş yükümlülüğü, inşa, altyapı ve şehir ölçütleri.
4. 28–30 Diplomatik gönderim, karşılaşma ve sözleşme akışları; onaylı yetki, zamanlama ve küçük içerik setiyle.
5. Koşullu S14'ten devredilen 31–32 ve 33–34 Gerçek savaş politikaları, emir/yerleştirme ve kampanyaya dönüş.
6. 38–41 Tüm uygulanmış sistemlerin birleşik kampanya, kayıt sürekliliği, performans ve final kabulü.
7. 42–50 Tımar, veraset, siyaset, Soldier→Character, bağımsız AI, bakım, nüfus/vergi, dünya genişlemesi, ganimet/fidye/kuşatma/deniz savaşı. Bunlar ilk oynanabilir döngünün dışındaki ayrı kapsamlar olarak kalır; yalnız bu sıraya yazılmaları geliştirme başlangıcı değildir.

Sol dalgasının 1–14 ekran işleri için önceki kaba tahmin 2–4 aktif çalışma haftasıdır. Fiziksel giriş/DPI düzeltmeleri ve koşullu savaş ekranları bu tahmine otomatik dahil değildir. Önkoşul, regression veya teknik hata ortaya çıktığında süre yeniden değerlendirilir; model değiştirmek teslim süresi garantisi vermez.

## Y0 Kararlılık

Bellek artışının kaynağı ayrı ölçülür. Gerekirse yalnız kanıtlanan yaşam döngüsü veya test ölçümü problemi düzeltilir. Model ve gameplay değişmez.

Teşhis ve deneylerin sonuçları [Y0 Windows bellek incelemesinde](Y0_WINDOWS_MEMORY_DIAGNOSTIC.md) tutulur. Toplama zorlayan tanı deneyi kabul PASS sayılmaz; normal testte sınırlar yükseltilmez.

Bitiş: kısa akış ve gerçek iki saatlik Windows testi temiz final kod SHA üzerinde geçer; normal oyuncu kayıtları korunur. Herhangi bir düzeltmede tüm ilgili regression kapıları yeniden çalışır. Kısa testin PASS olması uzun testin PASS olması yerine geçmez.

## Y1 Harita ve yolculuk

Oyuncu mevcut haritada yer seçer, izin verilen rotayı ve varış süresini görür, yolculuğu başlatır ve ilerlemeyi takip eder. Mevcut harita genişletilir; sıfırdan yeniden yapılmaz. Uzak aktörlere ilişkin bilgi yalnız mevcut bilgi sınırları içinde gösterilir.

Bitiş: gerçek şehirler arasında yolculuk tamamlanır; devam eden yolculuk kayıt/yükleme sonrası aynı noktadan sürer; geçersiz hedef açıklamalı reddedilir; fare ve klavye ile kontroller çalışır. Harita üzerinde sahte konum veya anlık teleport kullanılmaz.

## Y2 Ticaret

Oyuncu mal ve miktar seçer, onaylı fiyatı görür, satın alır veya satar. Ekran para, şehir stoğu, kervan yükü ve kapasiteyi işlemden sonra yeniler. Yetersiz para, stok, kapasite veya uygun olmayan konum açıkça gösterilir.

Bitiş: başarılı işlemde mal ve para tam korunur; reddedilen işlem hiçbir kısmi değişiklik yapmaz; çift aktivasyon kaynak üretmez; kayıt/yükleme aynı sonucu korur. Fiyat katsayıları arayüz içinde uydurulmaz.

## Y3 Ordu yönetimi

Mevcut yetkili kaynaklardan asker toplama, şehir veya kervandan ikmal aktarma, maaş borcunu ödeme ve mevcut komuta işlemleri bağlanır. Birlik mevcudu, ikmal ihtiyacı, borç ve işlem engelleri gösterilir. Yeni asker veya ekipman otomatik ve ücretsiz üretilmez.

Bitiş: kaynak ve yetki kontrolleri gerçek servislerce uygulanır; asker, mal ve para çoğalmaz; komutan ve birlik kimlikleri korunur; işlem sonuçları kayıt/yükleme sonrası aynı kalır. Maaş ve ikmal oranları gerekiyorsa tasarım kararı beklenir.

## Y4 Kampanya zamanı ve kervan

Oyuncunun zamanı ilerletmesi, izin verilen olayları ortak kampanya döngüsünde işletir. Üretim, tüketim, yolculuk, rapor teslimi ve AI işleri için cadence ve işlem sırası açıkça tanımlanır. İlk kapsam mevcut küçük tarihsel bölge ve servislerle sınırlıdır.

Kervan yükleme, yola çıkış, varış ve satış zinciri gerçek stok ve operasyonel para üzerinden çalışır. Kervan işletme parası hane veya kişisel servetle sessizce birleştirilmez. Kâr dağıtımı için ayrı onaylı kural gerekir.

Bitiş: aynı başlangıç ve aynı komutlar aynı sonucu üretir; kayıt sınırından geçen olaylar iki kez uygulanmaz; kervan varışı ticaret konumuna doğru yansır; oyuncu sonuçları stok, varış, rapor ve işlem kayıtlarında görür.

## Y5 Şehir ve üretim

İlk alt kapsam mevcut dokuz alanı, üretim girdilerini ve çıktılarını, stokları, ihtiyaçları ve gerçek yetkilileri kullanılabilir ekranda gösterir. Oyuncuya yalnız mevcut izinli işlemler sunulur.

İnşa, altyapı iyileştirme, nüfus büyümesi, vergi veya sağlık etkileri mevcut kurallarla desteklenmiyorsa ayrı alt paket olur. Tasarım onayı olmadan maliyet, süre veya bonus eklenmez. Unassessed değer sıfır veya hesaplanmış sonuç gibi gösterilmez.

Bitiş: üretim gerçek girdiyi tüketir ve gerçek çıktıyı ekler; eksik girdi açıkça gösterilir; yetki kontrolü korunur; durum kayıt/yükleme sonrası sürer. Şehirlerin 3D görünüşü bu kapıya dahil değildir.

## Y6 Diplomasi ve raporlar

Mevcut yetki ve görev sisteminden mesaj veya elçi gönderme, bekleyen görevleri takip etme ve teslim edilen raporları okuma akışı bağlanır. Raporun kaynağı, gözlem zamanı, ulaşma zamanı ve belirsizliği gösterilir.

Yeni antlaşma sonuçları, pazarlık, kriz veya savaş ilanı mekanikleri ilk ekran bağlantısına otomatik dahil edilmez. Bunlar karar gerektiren ayrı alt kapsamlardır.

Bitiş: iletişim anlık değildir; yetkisiz işlem reddedilir; gözlem ile dünya gerçeği karışmaz; aynı teslim iki kez sonuç üretmez; bekleyen görevler kayıt/yükleme sonrası devam eder.

## Y7 Karşılaşma ve sözleşmeler

Mevcut Encounter ve Contract kuralları onaylı, küçük bir içerik setiyle yolculuk ve ekranlara bağlanır. Oyuncu olaya seçenek verir, sözleşmeyi kabul eder ve gerçek Trade, Report, City veya Battle kanıtından ilerlemeyi görür.

Tetikleme sıklığı, ödül, ceza ve süre tasarım onayı gerektirir. Bu paket ana hikâyeyi, hikâye görev zincirlerini veya yeni NPC kopyalarını eklemez.

Bitiş: deterministik karşılaşma devamı ve sözleşme ilerlemesi kayıtta korunur; aynı kanıt veya ödül iki kez uygulanmaz; tamamlanma ve başarısızlık gerçek dünya durumundan türetilir. Battle kanıtlı sözleşmeler gerekiyorsa Y8'i bekler; ekonomik ve diplomatik alt kapsam önce kapanabilir.

## Y8 Savaş hazırlığı ve sonuç

İlk alt kapsam mevcut deployment, sektör ve emir kurallarını oyuncu ekranına bağlar. İkinci alt kapsam için gerçek combat resolver, zırh ve mühimmat politikaları ile gerekli sayısal kararlar onaylanır. Testlerin sabit sonuç üreten resolver'ı production gameplay olarak kullanılmaz.

Bitiş: geçerli yerleştirme ve emirler çalışır; onaylı kurallar savaş sonucunu üretir; sonuç kampanyaya yalnız bir kez uygulanır; asker ve ekipman kimlikleri korunur; aktif savaş kayıt/yükleme sonrası devam eder. Savaş sonrası rapor akışı mevcut bilgi sistemini kullanır.

Bu işlevsel kabul asker sanatını, animasyon ve silah temasını veya yayın kalitesinde savaş görüntüsünü kabul etmez. Ganimet, fidye, kuşatma ve deniz savaşı ayrı kapsam olarak kalır.

## Y9 Birleşik oyuncu kabulü

Oyuncu gerçek Windows sürümünde rota seçme, ticaret, ikmal, üretim, rapor ve uygulanmış diğer işlemleri tek kampanyada kullanır. Kontroller fare ve klavye ile doğrulanır; yalnız yapay UI olayları fiziksel girdi kabulü yerine geçmez. 1366×768, 1920×1080 ve 2560×1440 ile gerekli ekran ölçeklendirmesinde taşma, kesilen bilgi ve kaydırma kontrol edilir.

Bitiş: kesintisiz akış ile ara kayıt/yüklemeli akış tutarlıdır; açıklamalı hatalar oyun durumunu bozmaz; bellek ve işlem süreleri ölçülür; uzun test hedeflenen gerçek iş yükünü kapsar. Görsel savaş kabulü bu rapordan ayrı tutulur.

## Her paketin kapanış ölçütleri

İşlem paketlerinde oyuncu işlemi yapar, gerçek oyun durumu değişir, ekran doğru sonucu gösterir ve kayıt/yükleme sonrası sonuç korunur. Salt okunur bilgi paketlerinde oyuncu izinli gerçek veriyi inceler; kampanya durumu değişmez ve kayıt/yükleme sonrası doğru bilgi yeniden gösterilir. Yalnız bir düğmenin görünmesi veya servis testinin geçmesi tamamlanma değildir.

Her uygulama paketinde:

- Kapsam, başlangıç SHA ve tasarım kararları kilitlenir; ilgisiz kullanıcı değişiklikleri korunur.
- Başarılı işlem, reddedilen işlem, tekrar aktivasyon, yetki ve kaynak koruması test edilir.
- Tam .NET ve Unity suite, mevcut ilgili paket pipeline'ları ve Windows kabul akışı çalıştırılır.
- Kritik failure, skip veya çalıştırılmamış kabul testi varsa paket kapanmaz.
- Yeni final commit oluşursa eski SHA kanıtı yeni SHA için kullanılmaz. Push onayı kapsamında local, remote ve CI SHA eşitliği ile temiz worktree doğrulanır.
- Tamamlanan iş, ertelenen alt kapsam ve sanat kabulü açıkça ayrı raporlanır.

## Süre ve öncelik

İlk plan Y0–Y3 için kararlı kayıt akışı üzerinde harita, ticaret ve ordu işlemlerini hedefledi. Y1–Y3 ve Y5A teknik adayları korunur. Güncel uygulama önceliği bağımsız Sol High bilgi ekranları, ardından Astra aşamasındaki karar ve entegrasyon işleridir. Y0 teşhisi ve birleşik kabul gerekliliği devam eder; yeni geliştirme sırası bu kapıları kaldırmaz.

Karar gerektiren kampanya ve savaş alt kapsamları dahil geniş plan yaklaşık 3–6 aylık aktif çalışma ölçeğindedir. Tasarım bekleme, yeni kapsam veya önemli teknik sorunlar bunu uzatabilir. Şehir ve asker sanatı, ana hikâye ve yayın hazırlığı bu süreye dahil değildir. Paketler arası ilerleme otomatik zaman veya teslim taahhüdü değildir.

Hane, tımar, siyasi tepkiler, Soldier→Character ilerlemesi ve büyük tarihsel dünya genişlemesi ilk harita ticaret ordu döngüsünün dışında kalır. Gerekli ekranların temel verileri gösterilebilir; yeni mekanikler ayrı tasarım ve uygulama paketlerinde ele alınır. Eksik kararlarla çalışan bir mekanik görünümü verilmez.

## Dayanak belgeler

Y5A [şehir inceleme kapsamı](Y5A_CITY_INSPECTION_SCOPE.md), Y3 teknik temel `57857604b29f0e1e14d69dd4763f8cadeac95e78` üzerinde geliştirilir. Y4 atlanmış veya tamamlanmış sayılmaz: üretim/tüketim cadence'i ve Hasan kervan yetkisi tanımlanmadığından yalnız bağımsız bilgi ekranı yapılır. Dokuz alan, stok/talep, tek reçete uygunluğu ve altyapı kayıtları salt okunurdur. Kethüda'nın yalnız görev referansı gösterilir. Tam Y5 üretim yönetimi ile fiziksel/uzun kullanım kabulü ayrı kalır.

Y2 geliştirme adayı [pazar ekranı kapsamına](Y2_PLAYABLE_TRADE_SCOPE.md) kaydedildi. Başlangıç kervanı NPC yöneticisine aittir; Hasan'ın ticaret yetkisi uydurulmaz. Y0, fiziksel giriş ve tam Y2 oyuncu döngüsü kabulü açık kalır. Bu çalışma Y1 adayının üzerine eklenir; art/Implementation 15 ilerlemesi değildir.

Y3 adayı [ordu ve ikmal kapsamına](Y3_ARMY_LOGISTICS_UI.md) kaydedildi. Y2 teknik temel `c6b0c4694497f2561b8f1691cce3e2faee418112` korunur. Yeni personel kaydı ekipmanlı Soldier üretmez; mevcut maaş yükümlülüğü olmayan kampanyaya ücret uydurulmaz. Komuta ataması salt okunurdur. Teknik testler ve Windows kanıtı ayrı raporlanır; Y0/fiziksel giriş kapıları açık kalır.

- [Mevcut Windows sürümü ve doğrulama sınırları](DEVELOPMENT_PLAYABLE_BUILD.md)
- [Kampanya entegrasyonu ve test kapsamı](FULL_CAMPAIGN_INTEGRATION.md)
- [Tarihsel başlangıç kesitinin kapsamı](IMPLEMENTATION_14B_SCOPE.md)
- [Arayüz ve bilgi sınırları](IMPLEMENTATION_12_SCOPE.md)
- [Savaş çözümleme sınırları](BATTLE_ARCHITECTURE.md)
- [Kayıt güvenliği ve kurtarma](SAVE_HARDENING_AND_RECOVERY.md)
