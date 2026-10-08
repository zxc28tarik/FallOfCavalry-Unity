# Y1 Harita ve yolculuk geliştirme adayı

Kullanıcı 8 Ekim 2026 tarihinde kabul ettiği kılıç ve eyer çalışmasından sonra karakter görseli dışındaki büyük oynanış işlerine devam edilmesini istedi. Bu aday, mevcut Marmara haritasını gerçek hedef seçimi, rota önizlemesi ve gidiş dönüş yolculuğuna bağlar. Implementation 15 başlamaz; karakter ve şehir sanatı değişmez.

Başlangıç kodu `4b4d015f867266bef4e31ddca7da2a228b1f4ab6`, geliştirme dalı `codex/y1-map-travel-ui`.

## Kabul sınırı

Y0 uzun süreli kararlılık kabulü açık kalır. Y1 ayrı çalışma kopyasında geliştirilir; ana kopyadaki temiz SHA üzerinde çalışan Y0 testinin dosyaları değiştirilmez. Bu paralel geliştirme Y0 önkoşulunu kaldırmaz. Y1 veya birleşik oyuncu kabulü yalnız bu geliştirme çalışmasıyla READY sayılmaz; yeni kodun kendi final SHA testleri ayrıca gerekir.

## Kilitli kapsam

- Mevcut yerlerden hedef seçme; seçim kampanya durumunu değiştirmez.
- Gerçek yol grafiğinden rota, mesafe, süre ve tahmini varış gösterme.
- Mevcut TravelCommandService ile yolculuğu başlatma ve bir saat ilerletme.
- Mevcut ara durak varışlarından tekrar yola çıkabilme ve İstanbul dahil önceki şehre dönebilme.
- Aktif yolculuğun kalan süresini ve gerçek konumunu gösterme; kayıt ve yükleme sonrası devam.
- Geçersiz, ulaşılamayan, zaten bulunulan hedefler ile ölü, esir veya halen yolculuktaki aktörler için açıklamalı engel.
- Fare ve klavye ile kullanılabilir kontroller; gerçek Windows ekran kanıtı.

Hızlar ve mesafeler mevcut slice ayarlarından gelir. Zaman adımı mevcut yolculuk servisine aittir; üretim, tüketim, AI veya diplomasi zamanlaması bu pakete gizlice eklenmez. UI dünya durumunun sahibi olmaz. Save v14, gameplay kimlikleri, RNG ve bilgi sınırları korunur. Ordu oyuncuyla otomatik taşınmaz.

## Doğrulanmış başlangıç eksikleri

Gerçek tarihsel kampanya ile salt okunur sorgu deneyi Edirne seçiminin İstanbul olarak döndüğünü, İstanbul'a dönüş emrinin bulunmadığını, Edirne'deyken Edirne emrinin etkin kaldığını ve Çorlu varışından sonra yeni yolculuğun reddedildiğini gösterdi. Başlangıç sürümünde İstanbul Edirne yolculuğu mevcut kuralla 182880 saniyedir; bu sayı yeni dengeleme kararı değildir.

## Kapanış kanıtı

Salt okunur önizleme, tüm geçersiz işlemlerde değişmez kampanya, çift başlatmanın reddi, ara duraktan devam, gerçek dönüş ve kayıt sınırında aynı rota ve süre test edilir. .NET ve Unity suite, ilgili mevcut pipeline'lar ve Windows kayıt testi çalıştırılır. Sentetik UI olayları fiziksel fare klavye kabulü yerine geçmez. Y0 ve çalıştırılmamış fiziksel girdi veya uzun kullanım kapıları açıkça ayrı tutulur.

## Test ortamı düzeltmeleri

Mimari testlerinin kök bulucusu yalnız .git klasörünü kabul ediyordu; gerçek Git worktree .git dosyası da desteklendi. Mimari kurallar gevşetilmedi. Üç mevcut Meshy provenance JSON dosyasının kayıtlı SHA değerleri CRLF baytlarına aitti; yeni checkout LF üretince koruma testi hata veriyordu. Yalnız bu üç yol için .gitattributes CRLF koruması eklendi. İçerik, mesh, rig, texture, LOD veya kayıtlı hash değiştirilmedi.

## Geliştirme doğrulaması

17 yeni .NET davranış testi ve beş Unity panel testi eklendi. Geliştirme turunda .NET 537/537 ve Unity 866/866 geçti; son etiket yerleşimi testi sonrası final Unity alt sınırı 867'dir. Bu geliştirme sayıları final commit kanıtı değildir. İlk turlardaki worktree kökü, satır sonu ve ayrık EditMode ağacında olay gönderimi hataları giderildi.

Windows harita testi gerçek UI Toolkit kontrollerinden İstanbul Edirne Çorlu İstanbul akışını, üç varışı ve üç kayıt yeniden yüklemesini çalıştırır. Batch oyuncusunun sistem framebuffer'ı okunamadığından ekran kanıtı gerçek D3D11 oyuncu panelinin PanelSettings.targetTexture çıktısından alınır; bu masaüstü ekran görüntüsü veya fiziksel tıklama testi olarak adlandırılmaz. Boş görüntü, oyuncu hatası veya eksik varış testi başarısız yapar.

Fiziksel giriş denemesinde Windows güvenlik duvarı penceresi görüldü; izin değiştirilmedi. Bu kapı ayrı kalır.

## Harita sunumu

Mevcut Marmara arka planının provenance kaydı onu dekoratif olarak tanımlar; resim gerçek MapPoint koordinatlarına kayıtlı değildir. Geniş alanda crop edilince kara yolları denizin üzerinde görünüyordu. Konumları resme uydurmak yerine yolculuk paneli açıkça etiketlenmiş şematik görünüm kullanır. Eski resim silinmez veya değiştirilmez. Konum noktaları ve yol grafiği korunur; yalnız çakışan etiketler bağlantı çizgileriyle kaydırılır. Coğrafi olarak hizalı kıyı/zemin katmanı ve zoom/pan bu pakette yoktur.

## Final doğrulama reçetesi

Temiz final SHA üzerinde `dotnet restore FallOfCavalry.sln`, `dotnet build FallOfCavalry.sln --configuration Release --no-restore`, `dotnet test Build/FOC.Tests.csproj --configuration Release --no-build --no-restore` çalıştırılır. Unity 6000.3.16f1 ile `Tools/Test-Unity.ps1` ve mevcut 11 pipeline çalıştırılır; ardından `Tools/Test-WindowsSaveSmoke.ps1` ve `Tools/Test-WindowsMapTravel.ps1` kullanılır. Harita testi 1366×768, 1920×1080 ve 2560×1440 boyutlarında yinelenir. `-AllowDirty` yalnız geliştirme içindir; final kanıtında kullanılmaz.

Test sonuçları `TestResults/Y1Validation/Final/<sha>/.../manifest.json`, `TestResults/WindowsMapTravel/<sha>/<run>/manifest.json` ve ilgili TRX/XML/PNG dosyalarındadır. Bunlar çalışma sırasında üretilen yerel kanıtlardır; bu belgenin varlığı testlerin geçtiği anlamına gelmez. CI aynı SHA üzerinde ayrıca doğrulanır. Y0, fiziksel fare/klavye ve tam sanat kabulü bu teknik testlerin yerine geçmez.
