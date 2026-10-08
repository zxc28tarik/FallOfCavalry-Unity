# S3 — Diplomatik ilişki ve anlaşma defteri

Başlangıç: `a62fce707702094da30374bd84a8a84614155443`, temiz `codex/s2-envoy-tracking-ui`; origin aynı SHA ve Foundation CI `37807234474` success olarak doğrulandı. Çalışma branch'i `codex/s3-diplomatic-relations-ui`.

## Kilitli kapsam

Diplomasi ekranına salt okunur, arama ve kayıt türü filtresi bulunan bir defter eklenir. Kendi Faction'ının taraf olduğu mevcut ilişki kayıtları, nitel tutum, kayıt güncelleme zamanı ve kaynak/yön/olay zamanı ayrı ayrı gösterilir. Etkenler puana toplanmaz; kaynak referansı canlı dünya sorgusuna dönüşmez. Gelecek tarihli ilişki güncellemesi veya etken şimdiki bilgi gibi gösterilmez.

Kendi Faction'ının taraf olduğu, imza zamanı geçmiş veya şu an olan anlaşmalar yerel taraf kayıtları olarak gösterilir. Tür, taraf referansları, imza/yürürlük/bitiş tarihleri, kayıtlı status ve mevcut `IsEffectiveAt(WorldClock.Now)` sonucu ayrı tutulur. Active kaydı yürürlükte demek değildir; gelecekte yürürlüğe girebilir veya süre sınırını geçmiş olabilir. Ekran Expired/Terminated yazmaz, zaman veya kayıt durumunu değiştirmez. Maddeler mevcut kontrollü enum etiketleridir; tutar, icra kanıtı, yaptırım veya Economy/Battle etkisi uydurulmaz.

Bilgi yetkisi `BuildDiplomacy`'nin mevcut kendi-aktör ilişki projeksiyonuna dayanır. Anlaşma erişimi aynı aktörün imzalı taraf kaydıyla sınırlıdır; anlaşma tüm tarafların canlı durumuna erişim vermez. Debug veya yabancı kayda verilen unrelated exact ref bu deftere küresel erişim kazandırmaz. Taraf adları canlı registry yerine saklanan Faction referanslarıdır.

Yabancı-yabancı çiftlerin ilişki, etken ve anlaşmaları listeye, sayaca, aramaya veya detay fallback'ine girmez. Teslim edilmiş yabancı diplomatik gözlemler S1 rapor defterinde tarih/precision/provenance ile okunabilir; bu paket onları güncel disposition, etken veya anlaşma teyidine dönüştürmez. Yeni dış ilişki/anlaşma bilgi teslim sözleşmesi bu sınırlı paketin dışındadır.

## Korunan sınırlar

Save v14 ve gameplay değişmez. Emir, anlaşma yaratma/iptal, savaş, ticaret etkisi, Character/Army/AI/cadence ve sanat yok. S1 rapor ve S2 elçi ekranları korunur. Yeni normal başlangıç fixture'ı eklenmez; tarihsel başlangıçtaki mevcut ilişki ve gerçek boş anlaşma listesi gösterilir. Seçim/arama geçicidir; load yeni runtime ve session bağlar. Implementation 15 başlamaz.

## Kabul

Typed relation/agreement seçimi, filtre, Türkçe arama, kararlı sıralama, bilinmeyen deep-link, callback temizliği ve save/load test edilir. Yabancı kayıt değişikliği görünür defteri değiştiremez. Bütün kampanya/save/RNG/time payload'u inceleme öncesi/sonrası aynı olmalıdır. Tam .NET/Unity, mevcut 11 pipeline/build, izole gerçek Windows D3D11 defter akışı ve save smoke final SHA üzerinde yeniden çalışır. S1/S2 Windows regresyonu korunur; üç çözünürlükte yeni görüntüler incelenir. Local/origin/CI SHA eşit ve worktree temiz olmadan kapanmaz.

Windows tanılama örnek kayıtları yalnız opt-in ve izole save root içinde yaratır; normal kampanya önce doğrulanır. Attached UI Toolkit sentetik olayları fiziksel fare/klavye veya sistem DPI kabulü değildir. Y0 soak, entegre oyuncu döngüsü, yabancı diplomatik teyit semantiği ve ProductionArt ayrı kapılar olarak açık kalır.
