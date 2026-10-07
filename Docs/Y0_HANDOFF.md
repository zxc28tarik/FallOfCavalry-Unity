# Y0 devam notu — 7 Ekim 2026

Kullanıcının bilgisayarı kapatma isteği üzerine çalışma burada bırakıldı. Kod ve bu not GitHub'a kaydedilecek. Implementation 15 başlamadı; Y1 başlamadı. Y0 henüz READY değildir.

Repository: `zxc28tarik/FallOfCavalry-Unity`

Branch: `codex/impl-14c-production-character-art`

Doğrulanan kod commit'i: `895683adde0ba78585fc866235ccd38fb5536ee3`

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

## Yapılmayan ve bekleyen işler

Final kod SHA'sında yeni Windows save smoke ve kısa UI-flow testi henüz çalıştırılmadı. Yeni iki saatlik Windows testi **başlatılmadı**. Bilgisayar kapanırken yarım kalan bir uzun test yok; Unity ve Windows oyuncu süreçleri sona ermiş durumda.

Bu devam notu daha sonraki bir dokümantasyon commit'ine aittir. 895683a test sonuçlarını yeni HEAD için çalıştırılmış gibi raporlama. READY öncesinde güncel temiz SHA için bütün kabul kanıtını yeniden üret.

## Devam sırası

1. Branch, HEAD, remote ve temiz worktree'yi doğrula; bu notu ve [ilerleme planını](PLAYABLE_SCREEN_PROGRESSION_PLAN.md) oku.
2. Güncel SHA üzerinde tam .NET ve Unity suite ile `Tools/Test-14CExistingPipelines.ps1` çalıştır. Unity Editor aynı project üzerinde eşzamanlı açılmamalı.
3. Son yeniden oluşturulan Windows sürümünde `Tools/Test-WindowsSaveSmoke.ps1` ve `Tools/Test-WindowsPlayerSoak.ps1 -DurationSeconds 30` çalıştır.
4. Normal, GC zorlamayan `Tools/Test-WindowsPlayerSoak.ps1 -DurationSeconds 7200` çalıştır. AllowDirty veya MemoryAudit kullanma; HEAD/worktree test boyunca değişmesin.
5. Normal kayıtların değişmediğini, kritik failure/skip olmadığını, local = remote = CI SHA eşitliğini ve temiz worktree'yi doğrula. Tam test bitmeden PASS/READY deme.
6. Y0 gerçekten kapanırsa Y1 harita/yolculuk kapsamını kilitle. Yeni gameplay, tasarım oranları veya Implementation 15 ekleme.

Unity Editor doğrulanan yolu: `C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe`. Yeniden çalıştırırken kurulumun hâlâ mevcut olduğunu doğrula. TestResults yerel, ignored kanıttır; GitHub'a ham kaynak art veya normal oyuncu kayıtları yüklenmedi.
