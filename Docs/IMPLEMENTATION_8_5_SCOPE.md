# Implementation 8.5 Scope — AI-First Modular 3D Character Production Architecture

Authority: the latest explicit Implementation 8.5 package, then the established Implementation 0–8 contracts and current repository documentation. Authoritative base: `977dcc23c886a84abdf5f7a023e1741b7ce3dc42`.

## Verified project context

- The current Unity project is a clean, scratch-built project. It is not a migration of the browser/JavaScript/Phaser project.
- No current Unity 2D Soldier architecture exists; no migration is required.
- Legacy browser/Phaser visuals remain `REFERENCE_ONLY`. No legacy runtime, renderer, canvas, sprite-composition or scene architecture will be introduced.
- The project currently uses Unity 6000.3.16f1 with the Built-in Render Pipeline. Addressables and Animation Rigging are not installed. Presentation contains only its foundation marker; there are no gameplay scenes or character renderers to refactor.
- No scriptable DCC such as Blender is installed. This does not block the package: original procedural proof assets will be generated through deterministic Unity Editor tooling.

## In scope

- Make modular 3D the authoritative final Soldier/Character Presentation direction.
- Preserve Domain gameplay truth and build a one-way `SoldierInstance`/persistent loadout → visual plan → Unity view boundary.
- Add a Unity-free visual-planning core: typed visual IDs/categories, machine-readable asset specifications, provenance, quality lifecycle, canonical human/mount rig contracts, sockets, LOD policy, deterministic profiles/signatures, catalog validation, compatibility rules, binding and bounded pooling state.
- Add Unity Presentation assets and runtime: ScriptableObject catalogs/profiles/LOD/animation sets, `VisualSoldier3D` binding, assembler, shared-material path, cached signatures, pooling, LOD and animator culling.
- Add Editor automation: controlled model import policy, deterministic proof-asset/catalog/prefab/animation/benchmark-scene generation, validation, smoke execution and performance measurement.
- Generate original procedural proof content for human/body/head/clothing/armor/headgear/weapons/shield/mount/harness and capability-based proof animation clips.
- Prove Deli, Humbaraci and Bostanci configurations through generic IDs/mappings with no troop-name branches.
- Add repeatable command-line visual validation/smoke/benchmark tooling and tests.
- Document architecture, art direction, AI/DCC/import flow and measured performance results.

## Out of scope

- Implementation 9 Battle, Deploy, sectors, formations, combat AI, hit/damage/casualty simulation or battle reconciliation.
- Final historical production art, final mocap, dialogue facial animation, final VFX/UI, copyrighted commercial assets or a large cultural content pack.
- Render-pipeline migration, broad Addressables adoption or an Animation Rigging package dependency without measured need.
- Gameplay/save changes, equipment selection, loadout reroll, combat modifiers or visual-to-gameplay mutation.
- Fabricated GPU/render-thread measurements in headless batch mode.

## Locked boundaries

- `SoldierInstance != VisualSoldier3D`; `Character != CharacterView3D`; `EquipmentInstance != EquipmentVisual`.
- Domain contains no Unity asset or engine references. Unity GUIDs, prefabs, meshes, materials, animation and caches remain in Presentation/Editor assemblies.
- Persistent `SoldierLoadout` is the only equipment selection truth. The assembler resolves it; it never creates or rerolls equipment and never consumes campaign RNG.
- Visual variation derives from stable identity/profile hashing and cannot change gameplay state.
- Rider, Mount, Harness and MountArmor are separate visual modules.
- Cache/pool state is bounded, rebuildable and save-independent.
- Save schema remains v9 because this package adds only Presentation state.

## Production decisions to validate

- Human imports use Unity Humanoid for scalable retargeting and shared animation; mounts use Generic rigs. Canonical bone/socket validation remains explicit and independent of Animator type.
- Authoring remains modular. Runtime defaults to cached consolidated body/clothing/armor variants plus separate rigid weapons/shields/mounts when benchmark results support it.
- Three mandatory LOD levels are required. Far animated Soldiers retain silhouette; billboard impostors are deferred until a measured benefit exists.
- Shared materials and property blocks are allowed; per-Soldier material instantiation is forbidden on the normal path.
- Animator culling and quality-tier/distance policies reduce visual work without affecting gameplay simulation.

## Acceptance gates

- Baseline 292 tests plus new visual-core and Unity tests pass with no critical skip.
- `Tools/Test-VisualPipeline.ps1` generates/loads proof assets, validates catalogs/rig/sockets/LOD/animations/mounts, assembles generic historical examples, benchmarks available scenarios and exits 0.
- Final-SHA .NET restore/build/test, Unity import/compile/EditMode, visual smoke, GitHub Actions, matching remote SHA and clean worktree all pass.
- Performance results report measured CPU/assembly/memory/renderer/material data and explicitly mark unavailable headless GPU/render-thread/frame metrics.

## Authority TODO

- Final polygon/texture budgets, production shaders, facial system, Animation Rigging adoption, GPU crowd technology, impostors, Addressables segmentation and hardware FPS targets require production assets and representative battle-camera benchmarks.
