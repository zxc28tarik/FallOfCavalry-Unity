# Testing

## Engine-independent suite

Run from repository root:

```powershell
dotnet restore FallOfCavalry.sln
dotnet build FallOfCavalry.sln --configuration Release --no-restore
dotnet test FallOfCavalry.sln --configuration Release --no-build --no-restore --logger "console;verbosity=normal"
```

The `Build/*.csproj` projects compile the authoritative sources under `UnityProject/Assets/FOC`; they are not a second implementation. Warnings are errors. The suite covers Implementation 0–6 regression plus Army/Unit Group identity, finite source recruitment, real-Character command, hierarchy cycles, City/Caravan goods conservation, supply shortages, real-cash payroll and arrears, military information boundaries, schema v1→v2→v3→v4→v5→v6→v7→v8 migration and schema v8 semantic roundtrip.

## Unity Editor suite

Install the exact Unity version from `ProjectVersion.txt`, then run:

```powershell
.\Tools\Test-Unity.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The command runs EditMode tests in batch mode and writes XML/log output under ignored `TestResults/`. A package cannot claim Unity tests passed unless this command (or an equivalent Editor invocation) actually ran.

## CI

`.github/workflows/ci.yml` restores, compiles, and tests the same solution on Windows, and each command fails the job on a non-zero exit code. A local green run does not imply remote CI green; CI status must be reported independently for the tested commit SHA.

## Critical-skip policy

No critical test may be ignored or skipped to close a package. Missing Editor/tooling is reported as `NOT RUN`, not inferred success.
