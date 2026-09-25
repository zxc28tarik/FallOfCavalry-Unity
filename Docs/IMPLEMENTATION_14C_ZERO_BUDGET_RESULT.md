# Implementation 14C final zero-budget rescue — NOT READY

Authoritative starting SHA: `241ce2513d34f74e53ce47c19fbe0ebbdcd4655b`.
Same branch: `codex/impl-14c-production-character-art`. 0 TL spent. No base replacement, gameplay change, save migration, production promotion or Implementation 15.

## What was actually attempted

The nine Continuation images were inspected first; see [pre-edit analysis](IMPLEMENTATION_14C_VISUAL_FAILURE_ANALYSIS.md). Tool, historical-reference and public-license audit: [specification](IMPLEMENTATION_14C_ZERO_BUDGET_SPEC.md).

Only individually audited CC0 RehmanPolanski facial-hair sources were selectively recovered from the prior stash. Its failed independent ring/armhole garment and generated geometry were not restored. Stash remains intact.

Blender 4.5.9 regenerated **Hasan only**: connected anatomical shoulder/sleeve patch, connectivity-based waist extrusion, Catmull-Clark surface subdivision, actual-trouser envelope clearance, bounded solid thickness, seam/front vent, separately constructed collar, cuff and sole surfaces. Original skin weights interpolate through subdivision; canonical bones/positions are unchanged. No new anatomical base. Beard/moustache use fitted CC0 cards instead of random fibers. All output remains `Draft`.

Four Windows render/review iterations were performed. Observed defects drove actual changes:

1. R1: improved shoulder transition, but trousers pierced skirt; patch-selected collar and sash had jagged edges.
2. R2: measured trouser envelope reduced penetration; cutting detail patches on exact planes improved boundaries. Collar was still a cropped shoulder strip.
3. R3: moving a whole-body neck cut was insufficient; shoulder geometry still contaminated the collar. Solidify's even-offset compensation produced extreme thin spikes at acute seams. This iteration was rejected.
4. R4: head removal is localized, collar is an independent narrow sewn band (not a body/sleeve substitute), and solid thickness no longer uses unbounded miter correction. Gross spikes and most visible trouser intrusion are gone. **Still rejected:** broad tubular skirt and abrupt waist volume, poor sash/layer readability, rough card beard/hair, dull generic face, weak mail appearance, generic unequipped silhouette. The standing hands and voluminous coat also need proper animation-aware clearance.

Final diagnostic PNGs are in `Docs/Evidence/Implementation14C/Drafts/ZeroBudgetRescue/`. Straight = front; front = three-quarter in the existing review player's argument convention. They are actual Windows player camera output, lossless BMP-to-PNG conversion only, no retouching. They are NOT persistent-loadout game screenshots, NOT tactical-camera evidence and NOT Accepted images.

## Why this method did not close the gate

Connected topology, thickness and containment fix specific technical failures but do not supply convincing tailoring, folds, material art direction or character identity. The conservative collision envelope solves much of the overlap at the cost of an overly barrel-like silhouette. Adding subdivision raised cost without delivering the required artistic result: current Hasan LOD0/1/2 = 94,276 / 40,253 / 16,769 triangles. This is a draft topology count, **not a runtime benchmark**. Performance optimization was not substituted for failed visual acceptance.

**Current human base assessment: usable**, provisionally. Anatomical face/hands/limbs and canonical rig do not demonstrate an inherently broken base. The observed root blockers are garment construction/deformation and presentation quality. This is NOT a claim of complete animation certification. The next reserved comparison may assess the whole authoring pipeline, but the evidence does not justify blaming or silently replacing the anatomy now.

## Milestone stop and retained boundaries

Hasan did not pass, so mounted Sipahi and then Cebeli/Tüfekçi were not advanced. Weapons, standalone mail asset, horse, coat variants, tack and gaits retain their prior draft state. No natural motion, mounted attack, stirrup/rein fit or all-four profile acceptance is claimed.

Production catalog and gate logic are unchanged. Baseline graph remains 23 proof IDs, 77 proof dependency findings, 28 primitive/fixture mesh findings, one missing `hasan-aga` profile and one fallback issue: 130 total. A fresh gate must still fail; it must not be waived. Accepted images remain **0/12**. No tactical/gameplay acceptance screenshot. No 100/250/500 full assembled runtime or mixed-army benchmark, because activation/visual prerequisites failed.

Persistent equipment truth, SoldierInstance/VisualSoldier separation, consolidation/pool/cache architecture, deterministic state, domain semantics and save v14 are untouched. The review gallery explicitly does NOT prove real-loadout integration of new art.

## Reproduction and final-SHA evidence

An intermediate full run passed .NET 486 and Unity 544 tests, but correctly failed
the clean-worktree check: Standard material validation re-enabled `_NORMALMAP`
on four hair-card materials because an unused normal texture was still assigned.
The importer now clears the texture as well as the keyword; regression assertions
cover both. Final evidence must be regenerated on the follow-up commit. The
intermediate pass is not final-SHA proof.

The eleven-pipeline sweep then exposed the same Windows mapped-file 1224 failure
in the GENERAL Development build's settings restoration, after successfully
producing the executable. Its direct truncating write now delegates to the
existing atomic snapshot helper, and the memory-mapping regression is parameterized
over both review and general-build entry points. The temporary Mono override was
restored to the original IL2CPP setting. No build/gameplay semantics changed.
The failed intermediate build is not reported as PASS; all final checks must run
again on the follow-up SHA (Unity test count increases to 545).

Authoring:

```powershell
& 'Artifacts/DccTools/blender-4.5.9-windows-x64/blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Art/build_human_candidates.py -- --hasan-rescue
```

Unity importer: `FOC.Editor.Visuals.HistoricalArtCandidatePipeline.Run`; review build: `FOC.Editor.Visuals.HistoricalArtReviewBuild.Run`; both executed on Unity 6000.3.16f1. Four review rounds imported/built with exit 0. Three new regression tests check canonical rig/draft status and bounded geometry, licensed card/material dependencies, and a neck collar that cannot contain shoulder caps. They are structural checks, not art acceptance.

Final-SHA validation entry points (results must be read from actual execution, not assumed from these commands):

```powershell
pwsh -NoProfile -File Tools/Test-14CRescue.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
pwsh -NoProfile -File Tools/Test-14CExistingPipelines.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The first runner records exact commands, final SHA, .NET restore/Release build/test, Unity EditMode, Windows review build, three new diagnostic captures and fresh production gate in `TestResults/14c-rescue-final-validation.json`. The second records eleven existing pipeline/build commands in `TestResults/14c-existing-pipelines.json`. CI is Foundation CI (.NET), **not remote Unity/art validation**. The final handoff must identify the actual run URL and SHA. No old-SHA evidence may be relabeled.

## Decision

NOT READY regardless of technical regression results: Hasan's first visual milestone failed. Do not activate any partial catalog; do not create Accepted evidence or run a proof/prefab benchmark as a production substitute. Preserve the original stash and this reproducible rejected diagnostic without calling either production art.

Implementation 14C requires alternative base-character A/B strategy.

That reserved strategy was NOT executed. DO NOT START 15.
