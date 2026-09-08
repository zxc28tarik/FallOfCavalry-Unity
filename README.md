# Fall of Cavalry

This repository contains the clean Unity/C# production baseline for Fall of Cavalry. It is intentionally separate from the legacy browser prototype.

Implementation 0 establishes deterministic, save-safe foundations only. Gameplay packages begin only after this package passes its acceptance gate.

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

