# Y3 Ordu, asker toplama ve ikmal ekranı

Başlangıç: `c6b0c4694497f2561b8f1691cce3e2faee418112`; dal: `codex/y3-army-logistics-ui`. Kullanıcının karakter görseli dışındaki oynanış işlerine devam kararı uygulanır. Y0, fiziksel giriş, Y2 ticaret yetkisi ve ProductionArt kapıları kaldırılmaz. Implementation 15 başlamaz.

## Kilitli kapsam

Hasan'ın mevcut ordusu için kaynak ve birlik türü seçimi, miktar, inceleme/onay/vazgeçme ve açıklamalı engeller. Asker toplama mevcut sonlu kaynağı tüketir, RecruitmentRecord ve UnitGroup üretir. Bu servis SoldierInstance veya ekipman üretmez; UI bunu açıkça belirtir. Seçilebilir türler aynı ordu/kaynakta zaten bulunan tanımlarla sınırlıdır; yeni asker sınıfı veya teçhizat politikası eklenmez.

Şehir ikmali, mevcut ArmySupplyService ve AI supply adapter'ındaki komutan + aynı şehir sınırında gerçek stok aktarımıdır; satın alma değildir, fiyat veya bedelsiz stok yaratmaz. Kervan ikmali için ayrıca mevcut manager kimliği gerekir. Bilgi erişimi emir yetkisi değildir. NPC kervanı Hasan'a devredilmez.

Maaş ödemesi yalnız zaten kayıtlı yükümlülüğün belirlenmiş kaynağından yapılabilir. Yeni borç, ücret, ödeme takvimi veya finansman seçimi eklenmez. Kervan finansmanında manager kontrolü korunur. Başlangıç kampanyasında maaş yükümlülüğü yoktur; ödeme yolu yalnız açıkça etiketli izole test verisiyle ayrıca sınanır.

Komutan/Organization görevi, fiziksel mevcudiyet, canlı/esir olmama, ordu lifecycle ve kaynak yetkisi her onayda yeniden doğrulanır. Onay tek kullanımlıktır; zaman/kaynak değişimi, gezinme ve yükleme eski onayı geçersiz kılar. Kimlikler kalıcı kayıtlarda kullanılmamış deterministik sıra ile üretilir; RNG ve saat UI tarafından ilerletilmez.

Komuta zinciri salt okunurdur. Atama değiştirme application servisi olmayan yeni bir atama workflow'u eklenmez. Domain ve v14 save şeması, denge değerleri, karakter/at/şehir sanatı korunur.

## Doğrulama sınırı

Kaynak/personel ve mal korunumu; mevcut Soldier/loadout/komutan kimliklerinin değişmemesi; borç ve gerçek nakit güncellemesi; yetki/konum/taşma/tekrar/eski onay reddi; v14 save/load; retained UI callback temizliği. Gerçek Windows player panelinde ordu, toplama, ikmal ve kayıt/yükleme akışı; .NET, Unity ve mevcut pipeline regression'ları final SHA üzerinde yürütülür. Sentetik UI girdisi fiziksel fare/klavye kabulü diye raporlanmaz.

Bu paket teknik geliştirme adayıdır. Y0 normal iki saatlik kullanım ve fiziksel giriş kabulü kapanmadan birleşik oyuncu kabulü READY değildir.

## Uygulama ve kanıt yolu

`ArmyOrderSession` mevcut servislerin önüne actor-bound canlı kontrolleri ve tek kullanımlık önizleme koyar. `ArmyPanelSession` bilgi erişimini ve geçici seçimi tutar. `ArmyPanel`, mevcut shell/host içinde retained UI Toolkit kontrolleridir. Yükleme host'u yeniden kurar ve eski onayları atar. Yeni domain/save kontratı yoktur.

`Tools/Test-WindowsArmy.ps1` normal oyuncu kayıtlarına dokunmadan izole dizinde gerçek Hasan akışını çalıştırır: iki personel toplama, üç tahıl aktarımı, vazgeçme, geçersiz miktar, gerçek save/load düğmeleri ve yükleme sonrası eski onay reddi. Maaş yokluğu ve NPC kervanı engeli ayrıca kontrol edilir. Altı PNG gerçek D3D11 panel çıktısıdır; istenen render boyutu ile fiziksel pencere boyutu ayrı kaydedilir. 1366×768, 1920×1080 ve 2560×1440 hedefleri kullanılır.

Yeni davranış testleri 31 senaryodur; meşru çift yetkili kervan ve mevcut maaş borcu yalnız izole test fixture'larında eklenir, başlangıç içeriğinde değil. İki Unity panel testi retained kontrolleri, tekrar onayı ve yaşam döngüsünü sınar; bağlı kontrollerin ChangeEvent/NavigationSubmit akışı Windows player testinin sorumluluğudur.

Çalışma zamanındaki yeni personel hâlâ aggregate UnitGroup kaydıdır. Bu aday tam teçhizatlandırma, komutan atama, maaş tanımlama veya Y4 zaman politikası sağlamaz. Nihai komutlar, test sayıları, SHA ve CI bağlantısı yerel `TestResults/Y3Validation/Y3_FINAL_REPORT.md` dosyasına yazılır; ignored kanıt dosyaları GitHub'da varmış gibi sunulmaz.
