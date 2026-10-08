# Y5A Şehir inceleme ekranı

Başlangıç SHA `57857604b29f0e1e14d69dd4763f8cadeac95e78`, dal `codex/y5a-city-inspection-ui`. Kullanıcının karakter görseli dışındaki oynanış ekranlarına devam talebi kapsamındadır. Y4'ün otomatik üretim/tüketim cadence'i ve Hasan kervan yetkisi tanımlanmadığından Y4 uygulanmaz veya tamamlandı sayılmaz. Mevcut plandaki Y5'in bağımsız bilgi alt kapsamı geliştirilir; bu, tam üretim yönetimi kabulü değildir.

## Kilitli kapsam

- Kesin bilgisi olan şehirler arasında seçim; dokuz alanın doluluğu ve seçili alanın etkin, kilitli, kaldırılmış ve etkin olmayan bina havuzu.
- Şehir stoku, tanımlı talep ve eksik miktar; arama ve yalnız açıklar filtresi. Bu rakamlar gün başına tüketim veya talebin otomatik fiyat etkisi değildir.
- İçerikteki üretim reçetesinin tek uygulaması için girdi/çıktı ve mekanik uygunluk incelemesi. Mevcut ProductionService preflight koşulları ile aynı bina, stok ve taşma sınırları kontrol edilir. Önizleme üretim emri değildir; zaman, maliyet, verim veya yönetim yetkisi yaratmaz.
- Altyapı kurulum/durum kayıtları; kayıt yokluğu "kurulu değil" sayılmaz. Unassessed şehir ölçütleri sıfır veya hesaplanmış bonus gösterilmez.
- Kethüda referansı ayrı Organization assignment olarak korunur. Bu inceleme yalnız şehirdeki kurum/görev referansını gösterir; kişinin kimliğini ve güncel görev durumunu çözmez. Şehir bilgisi gizli karakter bilgisine sınırsız erişim vermez.
- Aynı şehir pazarına bağlantı, mevcut gezinme/kayıt/yükleme yaşam döngüsü. UI salt okunurdur; bir kampanya fingerprint'i bütün inceleme akışı boyunca değişmez.

## Kapsam dışı ve kabul

Yeni üretim başlatma, inşa, altyapı satın alma, ücret, tüketim, vergi, otomatik AI/diplomasi tick sırası, şehir/karakter/at sanatı ve Implementation 15 yoktur. Save v14, içerik, Domain ve mevcut üretim servisi değiştirilmez.

Read-only davranış, kaynak bilgi sınırı, gerçek ProductionService ile önizleme tutarlılığı, kayıttan sonra aynı veri ve UI callback temizliği test edilir. Gerçek Windows player'da üç render boyutu, şehir/alan/reçete/filtre/pazar/yükleme akışı kaydedilir. Tam .NET/Unity ve mevcut pipeline'lar final SHA üzerinde tekrar yürütülür; GitHub local/remote/CI SHA eşitliği denetlenir.

Y0 iki saatlik normal kullanım, fiziksel giriş, Y2 Hasan ticaret yetkisi, Y4 cadence ve tam Y5 üretim emri kabulü açık kalır. Sentetik UI olayları fiziksel fare/klavye kabulü sayılmaz. Test/kanıt çıktıları yerel ignored TestResults altında tutulur; GitHub'da varmış gibi sunulmaz.

## Uygulama ve doğrulama yolu

`CityInspectionSession` kopyalanmış salt okunur görüntüler üretir. `CityInspectionPanel`, mevcut UI Toolkit kabuğunda retained kontroller ve sınırlı yüksekliğe sahip sanallaştırılmış listeler kullanır. Şehir ekranından çıkınca callback'ler ayrılır; yükleme yeni kampanyaya bağlı yeni oturum kurar. Mal aramasında Unity Mono ile desktop .NET Türkçe collation farkı gözlendi; I/İ katlaması açık, ordinal arama kullanılır ve iki ortamda test edilir.

Üretim önizlemesi `ProductionService.Execute` önkoşul sırasını korur: alan, etkin uyumlu bina, girdi, girdi tüketilmeden önce çıktı taşması. Pozitif testler sadece izole kampanya kopyasında gerçek servisi çalıştırır. Oyuncu ekranı bu servisi çağırmaz. Başlangıçta Bursa dokuma/değirmen reçeteleri mekanik olarak uygundur; İstanbul tabakhanesinin girdisi eksiktir. Mevcut piyasalarda stok talebi karşıladığı için yalnız eksikler filtresi boş döner; kanıt için kampanyaya sahte kıtlık eklenmez.

Yeni testler: `CityInspectionTests` ve `CityInspectionPanelTests`. Windows akışı `Tools/Test-WindowsCityInspection.ps1` ile izole kayıtta çalışır; dokuz alan, İstanbul engeli, Bursa uygunluğu, Türkçe arama/filtre, altyapı, aynı pazar bağlantısı ve gerçek kayıt/yükleme boyunca kampanya fingerprint'i aynı kalır. Normal oyuncu kayıtları önce/sonra hash karşılaştırmasıyla korunur. 1366×768, 1920×1080 ve 2560×1440 gerçek D3D11 panel render hedefleridir; fiziksel masaüstü ekran boyutu ayrıca kaydedilir.

Final SHA raporu ve tam komut/sonuç manifestleri `TestResults/Y5AValidation/` altında; Windows görselleri `TestResults/WindowsCityInspection/<SHA>/<run-id>/` altında tutulur. Bu kaynak belgesi testlerin tamamlandığına dair final-SHA raporu yerine geçmez.

İlk final denemesinde (`350b744`) tam .NET/Unity suite geçmesine rağmen Presentation pipeline, Unity panelindeki doğrudan Domain enum kullanımını reddetti. Panel artık yalnız Presentation indeks/etiket görüntüsü alır; Domain dönüşümü Core içinde kalır. Kaynak sınırını ve snapshot tiplerini denetleyen regression testi eklendi. Mevcut pipeline sınırı değiştirilmedi. Önceki SHA kanıtı düzeltilmiş commit için kullanılmaz.
