# Implementation 14C — production character art replacement

## Locked authority and scope

The user's 14C instruction supersedes the earlier blanket 14B READY: 14B systems/content are accepted, but its character/mount art is not. Base: `92aff4078f59e8faedc3b19ff74f8b8089dc509e`, branch `codex/impl-14b-historical-slice-content`. Origin matched after fetch; worktree was clean; Foundation CI run 36158211112 succeeded on that exact SHA. Work branch: `codex/impl-14c-production-character-art`.

Replace visible historical character/mount/equipment assets, preserve gameplay/save/identity and modular presentation contracts. No Implementation 15. No production designation based only on automated tests. Actual Windows-player renders and visual review are mandatory. Any unmet gate keeps 14C NOT READY.

## Audit classification

| Material | Decision | Reason |
|---|---|---|
| Domain, persistent loadout, deterministic planner/signatures, save v14 | KEEP | Gameplay is outside art scope. |
| VisualSoldier3DAssembler, bounded cache/pool, LOD/socket separation | KEEP / narrowly ADAPT | Preserve architecture; address asset fitting/consolidation only where required by replacement. |
| Generated/VisualProof assets, generator and architecture tests | KEEP as fixtures | Never promote by renaming. |
| Historical runtime catalog with `_Proof` dependencies | REWRITE mappings | Not acceptable production art. |
| Generator writing proof catalog over runtime catalog | ADAPT | Fixture generation must not overwrite production mappings. |
| Legacy JavaScript/Phaser/web art | REFERENCE_ONLY, excluded from authoring inputs | No geometry, silhouettes, textures or model sheets may be reused. |
| Existing 14B local screenshots | REFERENCE_ONLY acceptance evidence | Show prior state, not new work. |

## Required gates

- Anatomical body variants, four heads, distinct Hasan, historical clothing/equipment.
- Anatomical horse, articulated rig and Idle/Walk/Trot/Gallop/Turn; fitted saddle/rider.
- Production-only profile dependency graph, provenance, LOD0/1/2, compatible skinning, shared materials and runtime consolidation.
- Actual player captures: Hasan standing; Sipahi front/side/mounted; horse alone/side; Cebeli; Tufekci; equipment detail; group of at least 12.
- Visual QA (anatomy, clipping, pose, scale, surface differentiation), 100/250/500 actor comparison, final .NET/Unity/CI evidence.

## Tool audit

Initial PATH and standard installation-directory audit found no Blender, Assimp or MeshLab. Unity Editor 6000.3.16f1 is available. A repository-local portable Blender distribution is being provisioned from blender.org, with upstream SHA256 verification. Tool downloads/cache stay under ignored `Artifacts`; production source/exports and provenance are versioned separately.

## Current state

IN PROGRESS / NOT READY. This document does not assert that any art gate has passed.

## Independent legacy audit

Read-only audit found the older browser repository at `C:\Users\zxc28\Desktop\FallOfCavalry`. Its package.json identifies a browser tactical game with Vite/Vitest and a battle-placeholder generator; assets/manifest.json maps sprite atlases and infantry frame animations. `assets/battle/tiny_swords_*` and sprite-preview.html are present. These files were inventoried, not imported, traced, rendered as model sheets or modified. The new DCC scripts have only explicitly versioned CC0 anatomy and original surface-authoring inputs.
