# Fall of Cavalry

This repository contains the clean Unity/C# production baseline for Fall of Cavalry. It is intentionally separate from the legacy browser prototype.

Implementation 0 established the deterministic, save-safe foundation. Implementations 1–7 added Character Core, Organization/House/Clique, Religion/Sect, City V2, Trade/Production/Caravan, Diplomacy/Envoy/Report and Army/Recruitment/Logistics. Implementation 8 adds persistent Soldiers, typed equipment definitions and instances, explicit multi-slot loadouts, City-stock acquisition and save v9. Implementation 8.5 establishes the automated modular 3D Presentation architecture, original proof assets, validation and performance tooling without introducing Battle semantics or changing save v9.

## Layout

- `UnityProject/` — Unity 6.3 LTS PC project.
- `UnityProject/Assets/FOC/` — layered game source and Unity tests.
- `Build/` — Unity-independent .NET project mirrors used for fast local/CI validation.
- `Docs/` — architecture, determinism, save schema, testing, scope, and audit records.

## Local validation

```powershell
dotnet restore FallOfCavalry.sln
dotnet build FallOfCavalry.sln --configuration Release --no-restore
dotnet test FallOfCavalry.sln --configuration Release --no-build --no-restore
```

Unity Editor validation requires the version pinned in `UnityProject/ProjectSettings/ProjectVersion.txt`.
