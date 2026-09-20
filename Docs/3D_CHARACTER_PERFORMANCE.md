# 3D Character Performance Architecture

## Budget model

Authoring remains modular, but 10–15 independent skinned renderers per ordinary Soldier is not the production default. The default candidate is a cached/prebuilt consolidated body, clothing and body-armor renderer set with rigid weapons/shields and a separate mount. Visual signatures make variants reusable and the cache is bounded/rebuildable.

Catalog assets require LOD0, LOD1 and LOD2. LOD priorities remove face detail, tiny accessories and minor geometry before silhouette-critical body, headgear, weapon, shield and mount forms. A one-renderer crowd proof exists, but billboard/impostor adoption is deferred until animation and battle-camera artifacts are measured.

Shared materials are mandatory. MaterialPropertyBlock may later provide safe per-instance color/wear variation; normal assembly must not clone materials. Shader variants remain limited to skin, cloth/leather, metal, wood and hair/alpha needs.

`VisualSoldierPool` avoids outer-view churn, while the assembler's signature-keyed variant pool retains complete child hierarchies after return. Warm reuse therefore performs no module instantiation or visual destruction. Both variant count and total pooled-instance count are explicit bounds. Binding and Animator state are reset before reuse; gameplay never reads Animator state.

## Benchmark interpretation

The automated Editor benchmark measures warmed prefab assembly time, configured renderer count, unique shared materials and observed managed-memory delta. It compares modular, cached-consolidated and far representations plus infantry/cavalry and Narrative subsets. In `-nographics` it cannot measure GPU, render thread, real frame time, batches or visible LOD selection; these must remain explicitly unavailable rather than inferred.

Measured proof results select cached-consolidated as the standard production starting point because it halves configured renderer count against the modular comparison and improved the warmed 100-actor spawn result on the implementation machine. Far representation is much cheaper but is not authorized as an unconditional animated-Soldier replacement.

## Runtime hardening benchmark

The hardening benchmark measures actual `VisualSoldierPool` plus `VisualSoldier3DAssembler` lifecycle for 100, 250 and 500 mounted Standard actors. Every cold actor creates one outer view and one six-module hierarchy (mount, harness, consolidated rider, headgear and two rigid weapons). After return, the warm phase reuses every view and hierarchy.

| Actors | Cold ms | Warm reuse ms | Warm created views | Warm created representations | Warm module instantiates | Warm reuse hits | Warm destroys |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 99.354 | 38.471 | 0 | 0 | 0 | 100 | 0 |
| 250 | 271.141 | 115.990 | 0 | 0 | 0 | 250 | 0 |
| 500 | 551.506 | 212.801 | 0 | 0 | 0 | 500 | 0 |

These are Editor batchmode CPU assembly measurements, not FPS. Their acceptance value is structural: every warm phase records zero view creation, zero representation creation, zero module instantiation and zero destruction.

## Required future benchmark

Before final battle budgets, run a Development Player or instrumented standalone build at representative battle camera/resolution with production meshes/shaders/animations. Record CPU main/render thread, GPU timing, batches/draws, visible renderers, skinning cost, memory, spawn spikes and frame distribution for 1/50/100/250/500 and, if hardware permits, 750/1000 actors.
