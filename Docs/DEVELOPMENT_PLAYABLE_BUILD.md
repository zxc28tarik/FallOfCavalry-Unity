# Development playable build

Run:

```powershell
.\Tools\Build-Windows-Development.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The Unity batch method validates and round-trips the vertical-slice campaign, generates/uses the deterministic development scene and builds a Windows x64 Development player under ignored `Artifacts/WindowsDevelopment/`. This local acceptance artifact explicitly uses the installed Mono scripting backend; it is not a release-distribution choice.

The player starts `DevelopmentCampaignBootstrap`, constructs `VERTICAL_SLICE_DEVELOPMENT-geography-1`, opens the production UI Toolkit map centered on İstanbul and exposes route/travel plus isolated Save/Load controls. Save and load use `CampaignSaveCoordinator`, `CampaignSaveTextSerializer`, `CampaignSaveValidator`, the v1→v13 migration pipeline and `AtomicFileSaveStore`. Saves live below `persistentDataPath/FOC/VerticalSliceSaves`; proof slots are not reused.

Automated smoke launches the real executable with `-focSmokeTest`. It waits for UI startup, executes Save then Load through the same coordinator, emits `FOC_DEVELOPMENT_BOOTSTRAP_READY`, `FOC_SAVE_UI SAVED`, `FOC_SAVE_UI LOADED` and `FOC_DEVELOPMENT_SMOKE_PASS`, then exits 0. The validated artifact total was 153,938,406 bytes; executable SHA-256 was `080EAE12A6B1C874F8B5C96E3D22CEB1F8A278017507A46901DBE0B6396CFD1B`.

The development-only `-focCaptureScreenshot -focScreenshotPath <path>` flow captures the rendered framebuffer after UI layout without adding a production dependency on Unity's optional ScreenCapture module. Final evidence includes the full İstanbul-selected map and the active İstanbul→Edirne route/progress overlay. No rendered FPS or GPU timing is claimed; performance evidence is limited to the deterministic path/marker benchmarks.
