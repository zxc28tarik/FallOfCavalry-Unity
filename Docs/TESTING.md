# Testing

## Implementation 14B historical-content gate

Run `Tools/Test-HistoricalContentPipeline.ps1 -UnityEditor <Unity.exe>`. It runs the focused .NET tests, the same tests under Unity EditMode, regenerates the visual assets/catalog without manual Inspector work, then executes the Unity historical-content batch validator. Coverage includes truth/provenance separation, forbidden proof-ID absence, complete cross-domain invariants, City V2 buildings/infrastructure, executable production and trade accounting, persistent military/Soldier/equipment identity, schema v14 roundtrip, a hardcoded v13→v14 active-journey fixture, deterministic initial save, production visual profiles/mappings and measured parse/validation/save work.

After the focused gate, run the full .NET and Unity suites, every existing package pipeline, `Tools/Build-Windows-Development.ps1`, player smoke and the required Map/City/Character/Trade/Army/Soldier screenshots. No critical skip is permitted.

## Implementation 14A world-map gate

Run `Tools/Test-WorldMapPipeline.ps1`. It validates content/provenance, projection bounds, explicit route connectivity, stable equal-cost path tie-breaks, Character/Army/Caravan lifecycle, external Envoy/Messenger ports, AI service reuse and fairness, information isolation, v12→v13 migration, mid-journey semantic continuation, map projection and measured graph/path/marker/route-projection work. Run `Tools/Build-WorldMapAssets.ps1` to verify the committed original map art, exact SHA-256 provenance, dimensions and Unity import metadata without network access or manual Inspector work. Then run the full .NET, Unity, earlier focused pipelines and Windows Development build; zero critical skips are permitted.

## Implementation 13 integration and persistence gate

Run `Tools/Test-SaveHardeningPipeline.ps1`, `Tools/Test-IntegrationPipeline.ps1`, `Tools/Test-LongRunPipeline.ps1`, `Tools/Benchmark-ProductionFoundation.ps1` and `Tools/Build-Windows-Development.ps1`. The focused gates cover atomic interruption/recovery/concurrency/path safety, exact v1→v14 migration, whole-campaign roundtrip/continuation/replay, deterministic periodic reload, real production mapping/serialization performance, a Unity batch integration proof and a real Windows x64 Development player. The complete .NET and Unity suites plus every earlier package pipeline remain mandatory regressions.

`-focSmokeTest` is accepted only by the Development proof bootstrap. It runs the real player save/load path and quits; it is not a gameplay feature or release mode.

## Implementation 12 Presentation gate

Run:

```powershell
.\Tools\Test-PresentationPipeline.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The gate runs Presentation-focused .NET tests, Presentation Unity EditMode tests and an actual Unity batch method. It validates UXML/USS imports, all registered screens, 12 shell bindings, back/forward lifecycle, command registry, foreign-information isolation, fixed-height ListView virtualization, 1366×768/1920×1080/2560×1440/3440×1440 structural layouts and measured 100/500/1000/5000-row construction. `-nographics` means screenshots, rendered FPS and real scroll latency are reported `NOT RUN`, never inferred.

## Implementation 11 AI gate

`Tools/Test-AIPipeline.ps1` runs the .NET and Unity EditMode `AIDecisionMakingTests`. Coverage includes perception isolation, delivered-report/staleness metadata, deterministic utility/tie-break, hard eligibility, priority/personality proof profiles, decision-quality fairness, Application-service symmetry, tactical orders, low-importance aggregation, source/boundary audits, v12 migration/roundtrip/canonical save, and deterministic post-load continuation. The full Unity, Visual, Battle, and Encounter/Contract gates remain mandatory regressions.

## Implementation 10

Encounter/Contract coverage locks exact authoritative families/types/categories, removed-type absence, definition/instance separation, lifecycle guards, deterministic RNG continuation, atomic preflight, typed variable targets, issuer/assignee validation, evidence-driven progress, separate task profiles, Encounter↔Contract links, real Battle/Trade/Report/City integration, schema v11 semantic roundtrip, v10 migration and canonical serialization.

Run the focused gate with the exact Editor executable:

```powershell
.\Tools\Test-EncounterContractPipeline.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

It runs the focused .NET suite and the same focused tests through Unity EditMode. Nonzero exit means the package gate failed.

## Implementation 9

Battle coverage includes typed identity/lifecycle, no fake Soldier expansion, no loadout reroll, no active-battle campaign mutation, multi-Unit deployment and sector validation, command authority/adjacency/hostility, declared weapon capabilities, explicit ranged ammunition consumption, deterministic RNG continuation, one-time atomic reconciliation, survivor/equipment identity, killed Soldier historical identity, schema v10 active-battle roundtrip, hardcoded v9 migration, canonical insertion ordering, Unity-free Domain guards, existing visual-pipeline binding, generated proof scene, and 100/250/500 actor batch smoke.

Run `Tools/Test-BattlePipeline.ps1` with the exact Unity executable for the agent/CI-friendly Battle smoke. It runs Battle-filtered .NET tests, generates/validates the Unity proof scene, records measured actor assembly/release data, and exits nonzero on failure.

## Engine-independent suite

Run from repository root:

```powershell
dotnet restore FallOfCavalry.sln
dotnet build FallOfCavalry.sln --configuration Release --no-restore
dotnet test FallOfCavalry.sln --configuration Release --no-build --no-restore --logger "console;verbosity=normal"
```

The `Build/*.csproj` projects compile the authoritative sources under `UnityProject/Assets/FOC`; they are not a second implementation. Warnings are errors. The suite covers Implementation 0–7 regression plus Soldier identity/provenance, roster/headcount bounds, typed definitions/slots, multi-attack weapons, armor/shield/mount restrictions, atomic City-stock acquisition, no-reroll loadout reads, historical content capability, schema v1→v2→v3→v4→v5→v6→v7→v8→v9 migration and schema v9 semantic roundtrip.

## Unity Editor suite

Install the exact Unity version from `ProjectVersion.txt`, then run:

```powershell
.\Tools\Test-Unity.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The command runs EditMode tests in batch mode and writes XML/log output under ignored `TestResults/`. A package cannot claim Unity tests passed unless this command (or an equivalent Editor invocation) actually ran.

Implementation 8.5 also runs the fully automated visual gate:

```powershell
.\Tools\Test-VisualPipeline.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

It refreshes original procedural proof assets, validates catalog/rig/socket/LOD/material/animation/mount contracts, assembles generic historical configurations and produces a headless performance report. Runtime-hardening coverage additionally proves same-signature hierarchy reuse, zero normal-return destruction, identity isolation, loadout-change replacement, bounded/active-safe eviction, explicit invalidation, mounted rider/harness reuse, Animator reset and executable Standard/Narrative/Crowd policies. Exit code is authoritative; manual Editor wiring is not required.

## CI

`.github/workflows/ci.yml` restores, compiles, and tests the same solution on Windows, and each command fails the job on a non-zero exit code. A local green run does not imply remote CI green; CI status must be reported independently for the tested commit SHA.

## Critical-skip policy

No critical test may be ignored or skipped to close a package. Missing Editor/tooling is reported as `NOT RUN`, not inferred success.
