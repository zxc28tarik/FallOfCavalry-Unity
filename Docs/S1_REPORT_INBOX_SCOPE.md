# S1 Rapor gelen kutusu ve ayrıntıları

Başlangıç kod SHA a3f5e7776999d524844f3cba2786064c549f2baa; dal codex/s1-report-inbox-ui. Kullanıcının Sol High öncelik sırasındaki ilk paketidir. Mevcut ilerleme planı değişikliği korunur.

## Kilitli kapsam

ActorInformationState içindeki oyuncu aktörüne gerçekten teslim edilmiş raporlar okunur. Gelen kutusu tür filtresi ve Türkçe arama sunar; sıralama teslim zamanı azalan, eşitlikte ordinal rapor ID'sidir. Ayrıntılar yalnız raporda kayıtlı kaynak referansını, gözlem/gönderim/teslim zamanlarını, kaliteyi, ayrıntı düzeyini ve her gözlemin kesinlik/değerini gösterir. Dünya zamanı saniye tick'idir; bilgi eskiliği gözlemden geçen zamandır, teslimden geçen zaman değildir.

Kaynak ve konu referansları rapor metadata'sıdır; güncel Character/City/Army/Economy verileriyle tamamlanmaz. Kesin, yaklaşık, aralık, nitel ve bilinmeyen gözlemler ayrı gösterilir. Karışık kesinlik tek bir kesin rapor gibi sunulmaz. Geçersiz, başka alıcıya ait veya erişilemeyen rapor seçimi bilgi döndürmez. Filtrede sonuç olmaması ile henüz teslim edilmiş rapor olmaması ayrılır.

UI mevcut shell içine retained ve sanallaştırılmış liste olarak eklenir. Çıkışta callback ve satırlar ayrılır; kayıt/yükleme yeni runtime'a bağlı yeni session kurar. Arama ve filtre kalıcı oyun durumu değildir. İnceleme boyunca tam kampanya fingerprint'i ve RNG değişmez; Save v14 korunur.

## Kapsam dışı

Elçi/mesaj gönderimi, üretim/tüketim cadence'i, yeni rapor içeriği, authority değişikliği, canlı gizli dünya sorgusu, karakter/at/şehir sanatı ve Implementation 15 yoktur. Başlangıç kampanyasındaki henüz teslim edilmemiş rapor normal UI'da görünmez; boş durum gerçektir.

## Kabul ve kanıt

.NET testleri erişim sınırı, tüm kesinlik biçimleri, filtre/sıralama, seçim reddi, değişmez snapshot, save roundtrip, zaman ve dispose davranışını kapsar. Unity testleri gerçek shell kontrollerini, retained callback yaşam döngüsünü, tekrar gezinmeyi ve source boundary'yi doğrular. Opt-in Windows kabulü normal başlangıçtaki boş kutuyu ve ayrı izole test kurulumunda mevcut ReportDeliveryService üzerinden teslim edilen raporu sınar. Test teslimi oyuncu gameplay'i veya gerçek başlangıç içeriği kabul edilmez; normal oyuncu kayıtlarına dokunulmaz.

Tam .NET/Unity suite, mevcut 11 pipeline/build aşaması, Windows save smoke ve üç çözünürlükte gerçek D3D11 rapor ekranı final SHA üzerinde yürütülür. Sentetik UI girdisi fiziksel fare/klavye kabulü değildir. Y0, kapsamlı DPI ve birleşik kampanya kapıları açık kalır. ProductionArt ve 14C kapanışı ayrı kalır. Final SHA, tam komutlar ve sonuçlar yerel ignored TestResults/S1Validation altında raporlanır; çalıştırılmayan test PASS sayılmaz.
