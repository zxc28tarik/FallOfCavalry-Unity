# 3D Asset Automation Pipeline

## Target flow

```text
machine-readable spec
 -> original/approved source generation
 -> optional headless DCC processing
 -> rig/cleanup and LOD generation
 -> export
 -> Unity import policy
 -> prefab/catalog registration
 -> validation and smoke assembly
 -> benchmark
```

The user is not expected to create folders, drag assets, configure importers, wire Animator/LOD manually or register hundreds of mappings.

## Structured specification

`VisualAssetSpec` records stable visual ID, category, period, culture, role, body family, material description, required rig/sockets, compatible body families, LOD and texture profiles, scale, gameplay-definition mapping, historical description, style tags, provenance/license/revision and lifecycle. JSON examples under `Assets/FOC/Generated/VisualProof/Specs` prove the machine-readable format.

## DCC policy

No Blender executable was detected on the implementation machine. This is not a blocker: Unity Editor generates original low-detail proof meshes, skinned rigs, prefabs and animations procedurally. For production art, the same specification is designed to drive a headless Blender or equivalent pipeline when installed. A production DCC stage must preserve canonical hierarchy, meters/`+Z`, UV/material assignments, bindposes, maximum four influences and required sockets.

## Unity import and registration

`VisualModelImportPostprocessor` applies policy only beneath `Assets/FOC/ArtSource`:

- human assets import as Humanoid; mounts as Generic;
- meters scale and file scale are preserved;
- cameras/lights/blendshapes are excluded by default;
- materials are not silently extracted/duplicated;
- meshes use controlled compression and are non-readable at runtime;
- optimized GameObjects keep canonical sockets exposed.

`VisualProofAssetGenerator` creates deterministic folder structure, original proof meshes/materials, canonical rigs, three-tier LOD prefabs, shared animation controller/clips, catalog assets, specifications and the benchmark scene. Future bulk tools should reuse these APIs instead of Inspector work.

## Validation command

```powershell
.\Tools\Test-VisualPipeline.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The batch command generates/refreshes proof assets, validates IDs, mappings, meshes, materials, rig/bindposes, sockets, scale/orientation, LOD, animations, mount separation and budgets; assembles Deli/Humbaraci/Bostanci; runs benchmarks; writes `TestResults/visual-pipeline-result.txt` and `TestResults/visual-benchmark.json`; and returns nonzero on failure.

## Provenance and external safety

Repository proof assets are generated from Unity primitives/custom mesh data and marked project-owned. Future external or AI-generated assets must include an approved license and source record before `ProductionCandidate`. Ripped commercial-game assets are never accepted.

## Bulk-production readiness

A future request such as “produce 50 helmets” can provide 50 specs, run DCC processing when available, place exports under the controlled ArtSource folders, trigger import/prefab/LOD/catalog automation and fail the batch gate for missing or incompatible results. Historical and subjective approval can remain human; technical integration does not require manual Editor operation.
