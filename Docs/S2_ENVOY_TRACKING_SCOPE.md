# S2 Elçi görev defteri

Başlangıç SHA 44413f930808d5fed301d2261ddc758d7b9da142; dal codex/s2-envoy-tracking-ui. Oyuncuya mevcut görev kayıtlarını salt okunur sunar. Yeni elçi gönderimi, diplomatik sonuç, gameplay, görsel asset veya Implementation 15 yoktur. Save v14 değişmez.

## Bilgi sınırı

Mevcut BuildDiplomacy sorgusunun kendi aktörüne ait görev kapsamı korunur. Görev ID, elçi/organizasyon/atama referansları, hedef, tür, mandate, oluşturma ve yerel sevk kaydı gösterilir. Referanslar canlı Character konumu veya yabancı dünya sorgusuyla tamamlanmaz.

Uzak görev phase'i yalnız PresentationViewerContext.CanReadExact içinde açıkça izin verilmiş EnvoyMission referansı için okunur; developmentDebug izin değildir. Normal bootstrap böyle bir izin yaratmaz. İzin yoksa yerel sevk kaydı gösterilir, varış/audience/dönüş/başarı/başarısızlık bilinmiyor kalır. Filtre ve sıralama da gizli phase veya teslim zamanını kullanamaz. Otomatik güncel lifecycle bilgisi, onaylı görev teyidi sözleşmesi bulunana kadar ertelenir.

Gönderilen mesajın yabancı alıcıya teslim edilmesi oyuncuya otomatik teyit değildir. Gerçekten oyuncu aktörüne teslim edilmiş, ResponseTo ile o görevin gönderilen mesajına bağlı ve doğru karşı taraftan gelen yanıt ayrı gösterilir. Yanıtın ulaşması emir kabulü veya görev tamamlanması anlamına gelmez; mesajda bulunmayan içerik/sonuç üretilmez. Envoy raporları yalnız Character referansı taşır; aynı elçiye ait rapor bir görev teyidi olarak eşleştirilmez. Raporlar S1 gelen kutusunda kalır.

## Ekran ve yaşam döngüsü

Diplomasi ekranında Türkçe arama, yalnız bilinen duruma göre filtre ve typed görev seçimi bulunur. Liste oluşturma zamanı azalan, eşitlikte ordinal ID sıralıdır. Başka aktöre ait veya bulunmayan görev ayrıntısı açılmaz. Sanallaştırılmış satırlar ve retained kontroller kullanılır; çıkış/yüklemede callback ve session bırakılır. Ekran zamanı, RNG'yi ve kampanya payload'unu değiştirmez. Var olan gönderim descriptor'ı gerçek executable bağlayıcısı bulunmadığından devre dışı açıklamalı kalır; S2 gönderim eklemez.

## Kabul

Erişim, gizli phase/delivery değişiminden bağımsız görünüm, exact izinli lifecycle, doğru yanıt bağı, filtre/arama/sıralama, geçersiz seçim, save roundtrip ve değişmez kampanya regression testleri gerekir. Gerçek Unity attached-panel kontrolleri ve callback yaşam döngüsü doğrulanır. Windows D3D11 oyuncusunda önce normal boş başlangıç, sonra yalnız opt-in izole test görevleri kullanılır; üretim başlangıcına fixture eklenmez. Gerçek Application kayıt/sevk/varış hizmetleri test kurulumunda kullanılır.

Tam .NET ve Unity suite, mevcut 11 pipeline/build aşaması, Windows save smoke, S1 regression ve S2 ekranları final SHA üzerinde yürütülür. Local, origin ve CI SHA eşitliği ve temiz worktree kapanış şartıdır. Sentetik UI olayları fiziksel giriş/DPI kabulü değildir. Y0, birleşik kampanya, sanat/ProductionArt ve otomatik uzak görev teyidi kapıları açık kalır. Komutlar ve final kanıtlar ignored TestResults/S2Validation altında tutulur.
