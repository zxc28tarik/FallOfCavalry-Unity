# Development playable build

Run:

```powershell
.\Tools\Build-Windows-Development.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The Unity batch method validates and round-trips the vertical-slice campaign, generates/uses the deterministic development scene and builds a Windows x64 Development player under ignored `Artifacts/WindowsDevelopment/`. This local acceptance artifact explicitly uses the installed Mono scripting backend; it is not a release-distribution choice.

The player starts `DevelopmentCampaignBootstrap`, constructs `VERTICAL_SLICE_HISTORICAL-1648-09-01-r1`, opens the production UI Toolkit map centered on İstanbul and exposes route/travel plus isolated Save/Load controls. It does not call `IntegratedProofCampaignFactory`. Save and load use `CampaignSaveCoordinator`, `CampaignSaveTextSerializer`, `CampaignSaveValidator`, the v1→v14 migration pipeline and `AtomicFileSaveStore`. Saves live below `persistentDataPath/FOC/VerticalSliceSaves`; proof slots are not reused.

Automated smoke launches the real executable with `-focSmokeTest`. It waits for UI startup, executes Save then Load through the same coordinator, emits `FOC_DEVELOPMENT_BOOTSTRAP_READY`, `FOC_SAVE_UI SAVED`, `FOC_SAVE_UI LOADED` and `FOC_DEVELOPMENT_SMOKE_PASS`, then exits 0. The Implementation 14B artifact totals 154,374,870 bytes; the 667,648-byte executable SHA-256 is `080EAE12A6B1C874F8B5C96E3D22CEB1F8A278017507A46901DBE0B6396CFD1B`.

The development-only `-focCaptureScreenshot -focScreenshotPath <path>` flow captures the rendered framebuffer after UI layout without adding a production dependency on Unity's optional ScreenCapture module. `-focScreen <Map|City|Character|Trade|Army>` and `-focSubject <production-id>` select deterministic evidence screens. `-focSoldierPreview` hides the shell and assembles persistent `soldier-hasan-01` through the same production visual catalog/assembler used by the runtime. No rendered FPS or GPU timing is inferred from these screenshots.

## Real Windows UI-flow and soak validation

After rebuilding the player, run `Tools/Test-WindowsPlayerSoak.ps1 -DurationSeconds 30` for the short flow gate, or omit the duration for the separate **7200-second (two-hour)** soak. The player is launched with Direct3D11, 1366×768, background updates and no `-nographics`. The native launch is hidden; no operator modeling or manual clicking is required. A short flow PASS is not a long-soak PASS. `runKind`, requested/observed durations, commands, binary hash, process ID, start/end SHA and worktree are recorded in ignored `TestResults/WindowsPlayerSoak/<SHA>/<run-id>/`.

The Development-only `-focPlayerSoak` mode uses a new temporary GUID save directory under the same validated isolation policy as save smoke. It cannot be combined with screenshot, Soldier preview, travel-demo or smoke mode. Normal interactive startup and player saves are unchanged. UI saves/loads first detach any prior callbacks before rebinding; disabling/destroying the bootstrap detaches them. Two regressions initially reproduced one activation causing five writes after five rebinds, and a disabled surface still saving. These are Presentation lifecycle fixes, not gameplay changes.

The harness sends **synthetic UI Toolkit NavigationSubmit/KeyDown events to real attached runtime controls**. This exercises the shipped shell/controller, command bindings, application services, serializer and filesystem; it does not call navigation or save handlers directly. It is not physical OS mouse/keyboard automation and does not certify pointer hit-testing, manual scroll feel, art quality or every command on every screen.

Coverage: ten existing screen routes, Back/Forward/Escape, navigation leaving the canonical campaign payload unchanged, existing Edirne travel start/hour-advance controls, invalid-save and missing-load rejection, then repeated successful save/reconstruction with exact canonical fingerprints and attached UI after reload. Travel advances for at most 50 cycles; the remaining soak stresses UI/reload lifecycle, not continuous economy/AI/combat simulation. Trade/Army command adapters lacking executable bootstrap bindings are not presented as tested functional operations. No new scenario, balance, history, story, model, rig, clothing, texture or save schema is added; save remains v14 and Implementation 15 remains closed.

Every frame records wall-clock frame intervals in a bounded histogram (1 ms bins, overflow at 2000 ms), plus maximum interval and UI-event dispatch time. The player records Unity native allocated memory, managed heap and GC counts without forcing GC; the launcher independently records working/private bytes and process CPU seconds every two seconds. These are Development-build workload measurements, not isolated GPU timings or release-performance certification. Window minimization, focus, OS scheduling and other workloads can affect them.

Workload guards: at least five frame samples per requested second; no frame gap ≥15 seconds; after three cycles no managed growth >64 MiB, Unity native allocation growth >128 MiB or attached UI-node growth >50; process working-set growth >256 MiB fails; no advancing UI heartbeat for 90 seconds fails; requested duration plus 120-second startup/shutdown allowance is enforced. A stalled owned process is stopped, never an unrelated Unity/player instance. Unexpected Unity errors/asserts/exceptions fail. Normal player-save hashes, sizes, paths and modification times must match before/after. These limits catch gross regressions; they do not prove zero leaks, acceptable gameplay latency or power-loss durability.

All final evidence must be produced on an unchanged clean commit. `-AllowDirty` explicitly labels development diagnostics `DEVELOPMENT_PASS`. The original input ZIP/art assets and production-art acceptance are outside this gate. A running test is `RUNNING`, never accepted before its complete report and expected exit code exist.
