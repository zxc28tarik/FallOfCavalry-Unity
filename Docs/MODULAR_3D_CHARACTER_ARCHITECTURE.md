# Modular 3D Character Architecture

## Authoritative direction

FOC's final Soldier and Character Presentation direction is modular 3D. The current Unity project is scratch-built; there is no current 2D Soldier renderer to migrate. Legacy browser/Phaser visual material is reference-only.

The one-way production flow is:

```text
SoldierInstance + persistent SoldierLoadout + TroopDefinition + VisualProfileId
    -> VisualSoldierPlanner (engine-independent)
    -> deterministic VisualAssemblyPlan + VisualSoldierSignature
    -> VisualSoldier3DAssembler (Unity Presentation)
    -> shared rig + modular/cached body + rigid equipment + optional mount
```

Presentation cannot select equipment, alter the loadout, consume campaign RNG or become save truth. Replacing a mesh/prefab under the same semantic visual mapping changes no Soldier, gameplay definition or save.

## Assembly boundaries

- `FOC.Visuals.Core` is engine-independent and references Domain only. It owns typed visual IDs, specifications, catalog records, profiles, rig/LOD rules, deterministic planning/signatures, binding state and bounded signature-cache policy.
- `FOC.Visuals.Unity` owns ScriptableObjects, prefabs, GameObjects, renderers, Animator policy, pooling and runtime assembly.
- `FOC.Editor.Visuals` owns import policy, proof generation, validation, smoke tests and benchmark automation.
- Domain owns `SoldierInstance`, equipment identity and loadout. It contains no Unity type, prefab, asset GUID or rendering cache.

## Human rig decision

Production human imports use Unity Humanoid. This is preferred over Generic for humans because the planned asset scale benefits from retargeting, a shared capability-based animation library and easier validation of independently generated source assets. Humanoid does not replace explicit FOC hierarchy/socket validation.

Mounts use Generic rigs because horse anatomy is not a humanoid retarget target. Rider and mount remain separate objects; a mount exposes `Socket_Rider` and the normal human view attaches there.

Canonical human bones include Root, Pelvis, Spine, Chest, Neck, Head, paired upper/lower arms, hands, upper/lower legs and feet. Finger and facial bones are not required for ordinary battle Soldiers. Canonical sockets are RightHand, LeftHand, Head, BackPrimary, BackSecondary, HipLeft, HipRight and ShieldBack. The import pipeline exposes these transforms when GameObject optimization is enabled.

Validation rejects missing required bones/sockets/bindposes, more than four skin influences, non-meter scale and non-`+Z` forward orientation. Procedural proof rigs demonstrate the contract; they are not final anatomical art.

## Profiles and deterministic appearance

A `VisualProfile` maps the existing Domain `VisualProfileId` to body family, head, clothing, optional headgear, cultural family and Narrative/Standard/Crowd quality. Visual-only variation is the stable FNV-derived hash of Soldier identity plus profile ID. Rendering performs no random draw.

Body/head/face composition supports multiple heads and profile quality. Ordinary Soldiers do not require facial rigs. Named Characters may later receive Narrative profiles, higher texture budgets and optional facial features without changing ordinary Soldier costs.

## Equipment catalog

`VisualCatalogAsset` explicitly maps gameplay definition identity to Presentation assets:

```text
WeaponDefinitionId -> weapon visual asset
ArmorDefinitionId  -> armor visual asset
ShieldDefinitionId -> shield visual asset
MountDefinitionId  -> mount visual asset
Auxiliary definition -> auxiliary visual asset
```

Mappings carry body-family and socket compatibility. Missing mappings are validation errors. Gameplay slots and visual modules are deliberately not forced into a one-to-one model.

## Runtime structure

- Authoring is modular: body, head, hair/beard hooks, clothing, armor, headgear, weapons, shield, auxiliary equipment, mount, harness and mount armor.
- Standard runtime target is a cached consolidated body/clothing/armor variant plus separate rigid weapon/shield/mount renderers.
- `VisualSoldierSignature` keys rebuildable bounded caches. Cache state is never saved.
- `VisualSoldierPool` reuses view objects; returning a view clears Soldier identity/signature before rebind.
- Shared materials are used through `sharedMaterial`; the normal path never calls `new Material` per Soldier.
- LOD0/LOD1/LOD2 are mandatory for catalog assets. A separate cheap far/crowd representation exists for measured future use; animated billboards are not the default.
- Animator uses `CullUpdateTransforms`. `VisualDistancePolicy` supports full near, half-rate medium and quarter-rate far evaluation decisions without affecting gameplay simulation.
- No per-frame LINQ or visual reconstruction runs in the view update path. Planning and assembly occur on bind/loadout-change boundaries.

## Animation

The animation catalog is capability-driven: Unarmed, OneHanded, Shield, Spear, Lance, Polearm, Firearm, Throwing and Mounted. It is not keyed by troop name. Proof clips cover Idle, Walk, Run, Turn, one-handed attack, thrust, polearm action, firearm aim/fire/reload, hit, death and mounted idle/locomotion/attack.

Animation Rigging is not currently installed. Built-in sockets and authored clips satisfy the proof. Runtime constraints may be adopted only after representative hand/off-hand/rider alignment quality and cost are benchmarked; distance disabling or baked alignment is preferred for crowds.

## Content delivery decisions

The project remains on the Built-in Render Pipeline for this package. Addressables is not installed and is deferred: the proof catalog is small, and adopting Addressables before real cultural/DLC packs would add operational complexity without measured benefit. The catalog uses direct Unity references while gameplay definitions stay engine-free.

GPU Resident Drawer is not assumed to optimize skinned characters. It may help later static/far representations, but the production character decision requires a rendered battle-camera benchmark on target hardware.
