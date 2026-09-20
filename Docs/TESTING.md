# Testing

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

It refreshes original procedural proof assets, validates catalog/rig/socket/LOD/material/animation/mount contracts, assembles generic historical configurations and produces a headless performance report. Exit code is authoritative; manual Editor wiring is not required.

## CI

`.github/workflows/ci.yml` restores, compiles, and tests the same solution on Windows, and each command fails the job on a non-zero exit code. A local green run does not imply remote CI green; CI status must be reported independently for the tested commit SHA.

## Critical-skip policy

No critical test may be ignored or skipped to close a package. Missing Editor/tooling is reported as `NOT RUN`, not inferred success.
