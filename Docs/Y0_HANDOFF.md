# Y0 devam notu — 7–8 Ekim 2026

Güncel kullanıcı önceliği: Y0 bellek çalışması durduruldu; Implementation 14C Meshy idle ve sağ el kılıç saldırısına dönüldü. Yeni iki saatlik RAM denemesi başlatma. Y0 READY değildir; Implementation 15 ve Y1 başlamadı. Bellek değişikliklerini koru, ancak bunları 3D pilotunun ön koşulu olarak dayatma. Aşağıdaki eski kapanış kanıtları kendi SHA'larına aittir.

Repository: `zxc28tarik/FallOfCavalry-Unity`

Branch: `codex/impl-14c-production-character-art`

Önceki doğrulanan kod commit'i: `895683adde0ba78585fc866235ccd38fb5536ee3`

## Güncel durum — 7 Ekim gece

HEAD: `119b56f757b901ff8e2e37f26d1b8336295006b1`. Bu temiz SHA'nın .NET 514/514, Unity 761/761, 11 pipeline, Windows save smoke ve kısa oyuncu testi geçmişti. [Foundation CI 37650464845](https://github.com/zxc28tarik/FallOfCavalry-Unity/actions/runs/37650464845) aynı SHA'da success. Fakat normal iki saatlik deneme 833.568 saniye / 145 döngüde 64 MiB managed büyüme sınırını aşıp **FAIL** oldu. İki saat tamamlanmadı; Y0 kapanmadı.

Ardından 900 saniyelik ayrı MemoryAudit 905.227 saniyede **DIAGNOSTIC_COMPLETE** verdi; zorlanan son GC eski kampanya/listeleri geri topladı. Bu kabul PASS değildir ve sıfır sızıntı kanıtı değildir.

Commitlenmemiş düzeltme: aynı save/load çağrısında aynı serialized text için tekrar tekrar DTO oluşturulmasını önleyen iki girişli exact-string validation cache ve mevcut/detail ekran listelerinin bounded slot reuse'u. Atomic disk okumaları, farklı içerik doğrulaması, migration ve son invariant doğrulaması korunur. Reuse edilmiş satır güncel veri/linkten bağlanır; görünmeyen listeler eski veriyi bırakır; satır yıkımı click callback'ini ayırır. Save v14; hiçbir art/gameplay dosyası değiştirilmedi.

Geliştirme kanıtı: .NET **520/520**, Unity **767/767**, failed/skipped 0; Windows save smoke **3/3 DEVELOPMENT_PASS**. Ayrı .NET allocation ölçümünde save yaklaşık %40, service load yaklaşık %32 azaldı; bu Windows kabulünün yerine geçmez. Normal 20 dakikalık dirty-candidate deneme **848.881 saniye / 149 döngüde FAIL** oldu: yine 64 MiB managed büyüme sınırı, normal kayıtlar değişmedi. Manifest: `TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/d971b78d9eb942358ba13c64f1543afe/manifest.json`. Kayıt cache'i tek başına çözüm değildir; Windows içindeki route/UI ve save/load allocation ayrımı sıradaki teşhis adımıdır. Ayrıntılı kanıt ve sınırlar [bellek teşhis kaydında](Y0_WINDOWS_MEMORY_DIAGNOSTIC.md).

Sonraki gerçek Windows allocation tanısı DIAGNOSTIC_COMPLETE olarak bitti; bu kabul PASS değildir. Ekran geçişleri yaklaşık 264 KB/frame-window ölçüldü. UI liste reuse sonrası aynı tanıda yaklaşık 61 KB, yani %77 azalma görüldü. Tanıdaki GC.Alloc timing değerleri byte diye kullanılmaz; allocation event sayısı ile ayrı frame-byte sayacı ayrılmıştır. Eski .NET allocation API'sinin Unity'de sıfır dönmesi ve yanlış marker-unit varsayımı regresyonlarla yakalandı, ilgili oyuncu denemeleri çalıştırılmadı; başarısız teşhis denemeleri gizlenmedi.

Dirty-candidate suite: .NET **520/520**, Unity **773/773**, failed/skipped 0. Korunmuş XML: `TestResults/Y0AllocationAudit/unity-ui-reuse-773-development.xml`. Normal, audit/forced-GC olmayan 20 dakikalık deneme **1203.240 saniye / 212 kayıt-yükleme döngüsünde DEVELOPMENT_PASS**: `TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/66b6dd064ce74b768f3fd439f76dea4a/manifest.json`. Managed büyüme 32,399,360 byte; normal kayıtlar değişmedi. Bu temiz final-SHA iki saatlik kabul değildir. Sonraki scroll reset API derleme hatası gerçek Unity derlemesinde yakalandı ve desteklenen ScrollView sorgusuyla düzeltildi; eski XML yeni çalışmış test sayılmadı.

Yeni Meshy `(1).zip` içinden Idle_12 ve Right_Hand_Sword_Slash izole olarak alındı. Unity Humanoid importu, mevcut Hasan'ın original/calibrated Avatar A/B denemesi ve gerçek Windows kılıçlı görüntüleri çalıştırıldı. Mevcut model/rig/material değişmedi; yeni raw FBX/ZIP public repository'ye aktarılmadı. Güncel sonuçlar ve sınırlar [3D pilot kaydında](IMPLEMENTATION_14C_MESHY_MOTION_PACKAGE_INTAKE.md). ProductionArt ve tam 14C kabulü kapalı kalır.

## Tamamlanan iş

Windows UI/kayıt testi bellek ölçümleri mevcut beş saniyelik beklemenin sonuna taşındı. 64 MiB sınırı yükseltilmedi; normal kabul testi GC zorlamaz. Eski kampanya ve ekran listeleri sınırlı weak-reference tanısıyla izleniyor. Ayrı MemoryAudit sonucu hiçbir koşulda kabul PASS olarak etiketlenmez. Hata anında güncel bellek değerleri ve ham oyuncu raporu korunur.

Model, rig, kıyafet, texture ve gameplay değiştirilmedi. Save v14. ProductionArt ve Implementation 14C tam sanat kabulü ayrı, kapalı kapılar olarak kalır.

## Gerçek test sonuçları

Yalnız yukarıdaki **895683a kod SHA'sı** için:

- .NET restore/build: PASS, sıfır derleme hatası/uyarısı; test 514/514, failed 0, skipped 0. Yerel sonuç: `Build/TestResults/y0-final-895683a.trx`.
- Unity 6000.3.16f1 EditMode: 761/761, failed 0, skipped 0, process exit 0. Yerel sonuç: `TestResults/unity-editmode.xml`.
- Mevcut 11 pipeline: hepsi exit 0, executionStatus COMPLETED; start/end SHA aynı, worktree clean. Yerel manifest: `TestResults/14c-existing-pipelines.json`. Manifestin genel NOT READY etiketi 14C sanat kapısını açmaz.
- [Foundation CI 37649125495](https://github.com/zxc28tarik/FallOfCavalry-Unity/actions/runs/37649125495): success, aynı 895683a SHA.

Önceki dirty-worktree geliştirme denemesi: 1202.456 saniye, 208 kayıt/yükleme döngüsü, DEVELOPMENT_PASS, normal oyuncu kayıtları değişmedi. Bu, temiz final-SHA iki saatlik kabul testi değildir. Ayrıntılar [bellek teşhis kaydında](Y0_WINDOWS_MEMORY_DIAGNOSTIC.md).

## Önceki notun bekleyen işleri

895683a notu yazıldığında Windows save smoke/kısa flow ve iki saatlik test başlatılmamıştı. Daha sonraki 119b56f denemelerinin gerçek sonucu yukarıdaki güncel bölümde kaydedilmiştir.

Bu devam notu daha sonraki bir dokümantasyon commit'ine aittir. 895683a test sonuçlarını yeni HEAD için çalıştırılmış gibi raporlama. READY öncesinde güncel temiz SHA için bütün kabul kanıtını yeniden üret.

## Devam sırası

**8 Ekim kullanıcı kararı aşağıdaki eski Y0 sırasının önündedir: önce 3D Meshy pilotunda ilerle.** Bellek kapanış sırası yalnız kullanıcı tekrar bu işe dönülmesini istediğinde uygulanır. Yeni karakter, rig, kıyafet veya texture üretme; mevcut Hasan ve mevcut atı koru. Implementation 15'e geçme.

1. Branch, HEAD, remote ve temiz worktree'yi doğrula; bu notu ve [ilerleme planını](PLAYABLE_SCREEN_PROGRESSION_PLAN.md) oku.
2. Güncel SHA üzerinde tam .NET ve Unity suite ile `Tools/Test-14CExistingPipelines.ps1` çalıştır. Unity Editor aynı project üzerinde eşzamanlı açılmamalı.
3. Son yeniden oluşturulan Windows sürümünde `Tools/Test-WindowsSaveSmoke.ps1` ve `Tools/Test-WindowsPlayerSoak.ps1 -DurationSeconds 30` çalıştır.
4. Normal, GC zorlamayan `Tools/Test-WindowsPlayerSoak.ps1 -DurationSeconds 7200` çalıştır. AllowDirty veya MemoryAudit kullanma; HEAD/worktree test boyunca değişmesin.
5. Normal kayıtların değişmediğini, kritik failure/skip olmadığını, local = remote = CI SHA eşitliğini ve temiz worktree'yi doğrula. Tam test bitmeden PASS/READY deme.
6. Y0 gerçekten kapanırsa Y1 harita/yolculuk kapsamını kilitle. Yeni gameplay, tasarım oranları veya Implementation 15 ekleme.

Unity Editor doğrulanan yolu: `C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe`. Yeniden çalıştırırken kurulumun hâlâ mevcut olduğunu doğrula. TestResults yerel, ignored kanıttır; GitHub'a ham kaynak art veya normal oyuncu kayıtları yüklenmedi.
