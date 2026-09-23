# Development playable build

Run:

```powershell
.\Tools\Build-Windows-Development.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The Unity batch method validates and round-trips the integrated proof campaign, generates the deterministic `IntegratedProof` scene and builds a Windows x64 Development player under ignored `Artifacts/WindowsDevelopment/`. This local acceptance artifact explicitly uses the installed Mono scripting backend; it is not a release-distribution choice.

The player starts `DevelopmentCampaignBootstrap`, constructs the `PROOF_ONLY` campaign, opens the production UI Toolkit Presentation shell and exposes a validated slot field plus Save/Load controls. Save and load use `CampaignSaveCoordinator`, `CampaignSaveTextSerializer`, `CampaignSaveValidator`, the v1→v12 migration pipeline and `AtomicFileSaveStore`. Errors and backup recovery are visible in the header feedback and player log.

Automated smoke launches the real executable with `-focSmokeTest`. It waits for UI startup, executes Save then Load through the same coordinator, emits `FOC_DEVELOPMENT_BOOTSTRAP_READY`, `FOC_SAVE_UI SAVED`, `FOC_SAVE_UI LOADED` and `FOC_DEVELOPMENT_SMOKE_PASS`, then exits 0. The validated artifact total was 146,855,922 bytes; executable SHA-256 was `080EAE12A6B1C874F8B5C96E3D22CEB1F8A278017507A46901DBE0B6396CFD1B`.

An automated screenshot was attempted during hardening, but this installed Unity profile does not expose the ScreenCapture module to the bootstrap assembly. No screenshot, rendered FPS or GPU timing is claimed. The real D3D11 player startup/save/load smoke is the graphics/runtime evidence.
