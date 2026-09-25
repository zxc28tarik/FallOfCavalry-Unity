STATUS: NOT READY

> Historical checkpoint report for `9bcae4638a36d1cb6484eec3de363c18c9c9b372`.
> The same-branch continuation is recorded in [IMPLEMENTATION_14C_CONTINUATION.md](IMPLEMENTATION_14C_CONTINUATION.md). Neither report promotes the art or opens Implementation 15.

Implementation: 14C — production character / horse / equipment art, **partial draft checkpoint**, not a completed replacement package.

Branch: `codex/impl-14c-production-character-art`

Base SHA: `92aff4078f59e8faedc3b19ff74f8b8089dc509e`

Commit SHA: use the commit containing this report; exact final SHA and post-commit runs are recorded in `TestResults/14c-final-validation.json` and the delivery message. No self-referential SHA is embedded here.

## Scope completed

- Fetched origin; verified clean authoritative base and baseline CI run 36158211112 success.
- Locked scope; independently inventoried old browser material as REFERENCE_ONLY. No old web geometry, textures or silhouettes were used.
- Provisioned official SHA256-verified Blender 4.5.9 portable. Versioned licensed upstream anatomy and reproducible source tools; no external AI 3D account needed, no manual modeling requested from the user.
- Authored/imported **25 Draft assets**, outside VisualProof: three human builds, four heads, three clothing sets, mail, four consolidated human drafts, horse, and nine equipment/headgear/tack drafts. Anatomical source meshes genuinely replace primitive geometry *in the drafts*, not by renaming fixtures.
- Horse has visible skinned LOD0/1/2 (14,986 / 7,788 / 3,440 triangles), repaired hair/eye/tail bindings, a separate generic rig and Rider socket, and five authored animation clips. Clip existence/motion does **not** establish production gait quality.
- Human variants use valid canonical Humanoid avatars. Major runtime draft prefabs have one skin renderer per active LOD; no twelve-skinned-renderer soldier design was introduced.
- Added a production dependency gate that rejects proof IDs, transitive VisualProof dependencies and built-in primitive mesh references, including renamed fixture prefabs.
- Stopped the proof generator from overwriting the historical runtime catalog. Added a regression test that performs regeneration and restores fixture bytes afterward.
- Built a real Windows review player and captured six unretouched draft images. Corrected a blank hidden-swapchain capture and an actual winding/import defect exposed by visual inspection. Neither erroneous output was accepted.

**Production catalog activation is NOT completed.** Existing historical runtime profiles still resolve to proof assets. Drafts were deliberately not promoted after failed visual QA; the gate reports this honestly.

## Changed files

`Tools/Art`, new editor import/gate/review/benchmark code and tests; `ArtSource/HistoricalSlice` licensed inputs; Unity `ArtSource/HistoricalSlice` intermediates; `Presentation/{Characters,Mounts,Equipment,Materials}/Ottoman1648`; this report, scope and evidence. Existing gameplay code, planner, variant cache, assembler, persistent loadout and save code were not changed. The only existing presentation-tool behavior changed is proof-generator isolation.

New public contracts: editor-only source DTO/import/validation entry points and development-only review component. No new gameplay or save contract.

Changed public contracts: none in Domain/Application/Core/runtime assembly.

Save schema version: 14, unchanged.

Migrations: none added or changed.

Legacy audit: see [scope](IMPLEMENTATION_14C_SCOPE.md); KEEP architecture/fixtures, ADAPT proof publication boundary, production catalog remapping pending; browser assets REFERENCE_ONLY.

## Exact test commands

Run from repository root in PowerShell:

```powershell
.\Tools\Art\Prepare-HistoricalArt.ps1
# Authoring/import (not part of immutable final-SHA validation):
& 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe' -batchmode -nographics -projectPath UnityProject -executeMethod FOC.Editor.Visuals.HistoricalArtCandidatePipeline.Run -logFile TestResults/14c-candidate-import.log

# Full post-commit evidence, requiring initially clean worktree:
.\Tools\Test-HistoricalArtDrafts.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'

# Independent production dependency gate (currently expected to FAIL acceptance):
.\Tools\Test-ProductionArt.ps1 -UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
```

The final runner records expanded exact commands, exit codes, SHA and XML counts. It executes `dotnet restore`, Release build, Release tests, real Unity EditMode, Windows review build, draft prefab benchmark, six player captures and the production gate. A nonzero production gate is a real failed acceptance gate, not a skipped test or a successful closure.

Passed / Failed / Skipped / Not Run: pre-commit development runs passed .NET **486/486**, Unity **522/522**, zero test failures/skips. These are development observations, not substituted for final-SHA evidence. Production dependency gate failed with **130 issues**. Full assembled production visual acceptance, rider-fit acceptance and gameplay-scale graphics benchmark: **NOT RUN / NOT COMPLETE**.

Unity Editor validation: real 6000.3.16f1 import/compile succeeded for drafts. License was not a blocker.

Unity EditMode results: post-commit exact results are in `TestResults/14c-final-editmode.xml`; pre-commit count is given above without claiming final SHA.

CI status + exact final SHA evidence: final remote run is reported in the delivery message. Foundation CI tests .NET; it does not replace Unity or art acceptance.

## Visual QA — FAIL / incomplete

These are actual Windows-player camera renders, losslessly converted BMP → PNG. Not Inspector images, not mockups, not AI retouching. They are **draft asset reviews**, not claimed as the ten required assembled-production acceptance shots.

| Evidence | Review |
|---|---|
| [Horse three-quarter](Evidence/Implementation14C/Drafts/horse-three-quarter.png) | Anatomical improvement over proof; not a cube/cylinder body. Coat/tail/eye/hoof polish and variants still require work. |
| [Horse side](Evidence/Implementation14C/Drafts/horse-side.png) | Useful silhouette review; saddle/rider not present, so rider fit is unproven. |
| [Horse trot sample](Evidence/Implementation14C/Drafts/horse-trot.png) | A real sampled animation frame, **not proof of an acceptable gait cycle or foot contact**. |
| [Hasan body draft](Evidence/Implementation14C/Drafts/hasan-body-draft.png) | FAIL: shoulder/garment seam gaps, crude hair/beard/skin treatment, exposed feet/unfinished boots, incomplete equipped pose. Not an accepted Hasan profile. |
| [Cebeli body draft](Evidence/Implementation14C/Drafts/cebeli-body-draft.png) | FAIL: similar fitting/material shortcomings; not an accepted historical soldier. |
| [Matchlock draft](Evidence/Implementation14C/Drafts/matchlock-draft.png) | Shaped stock/barrel/lock draft; hand alignment and 1648-specific historical support remain unverified. |

Mandatory accepted shots of full Hasan, Sipahi front/side/mounted, Cebeli, Tufekci, fitted equipment and a 12-soldier group are still missing. Two horse review angles do not close the entire screenshot gate.

## Performance — preliminary, NOT actor acceptance

[Raw draft measurements](Evidence/Implementation14C/draft-prefab-benchmark.json) compare individual prefab instantiation/reactivation at 100/250/500, in Editor `-nographics`. They do not use the complete Soldier assembler/loadout/cache path and **are not FPS, GPU timing, skinning timing or complete battle memory measurements**. GC deltas are noisy, not per-actor memory budgets.

At 500 prefabs, the development observation was: proof human 254.30 ms cold / 55.10 ms reactivation, draft human 121.30 / 34.01; proof horse 90.36 / 19.06, draft horse 183.92 / 62.89. Draft human near: one renderer, six shared materials, 17,148 triangles; horse: one renderer, three materials, 14,986 triangles. Faster empty/headless hierarchy work does not establish cheaper rendered geometry. Material atlas consolidation and rendered actor benchmarks remain required. Final runner produces fresh measurements separately.

Invariants verified: no Domain UnityEngine dependency introduced; no gameplay/SaveData/runtime identity modifications; no save migration changes; persistent loadout/planner/cache/pool untouched; fixture dependency rejection and proof-publication isolation tested; source skin weights finite, normalized and at most four influences; three LODs; valid Humanoid avatars; no disabled proxy skin used to fake the horse rig.

Known limitations: all new assets remain Draft; human visuals fail art quality; production mappings remain proof; full anatomy deformation, shared animation retargeting, harness/rider fitting and three coat variants are not accepted. Gait curves are drafts, not natural-motion certification. Equipment LOD reductions are conservative for small meshes, with some identical triangle counts. Final texture/normal/roughness work and historically defensible firearm/sabre interpretation remain open.

TODO_TECHNICAL: complete fit and rig validation (including explicit permitted mount extension bones), production-only catalog and per-profile consolidated variants, persistent-loadout integration, warmed assembler/pool regression with production assets, mounted locomotion/contact QA, ten acceptance images, 100/250/500 rendered actor metrics.

TODO_ART: repair shoulder/cuff/collar surfaces, proper boots/coat fit, credible mail surface, face/hair/beard/skin, distinct equipped Hasan; finish headgear and weapons; saddle/bridle/reins/stirrups and mounted pose; horse coat variants, gait polish, lighting/material QA.

TODO_BALANCE: none; gameplay balance untouched.

TODO_CONTENT: confirm 1648-specific firearm/sabre interpretation. Later museum survivors were labeled as later references, not silently asserted contemporary.

TODO_DESIGN_AUTHORITY: no new gameplay decision required; no authority changes made.

Blockers: failed human visual QA; missing accepted mounted/equipped profiles; proof dependencies still in runtime production graph; incomplete mandatory screenshots; full rendered actor performance and animation acceptance absent. **No Unity license, browser authentication or unavailable AI account is being used as an excuse/blocker.**

Design deviations: this is an explicitly incomplete draft checkpoint, not a claim to have fulfilled 14C. No primitive geometry has been relabeled ProductionCandidate. The art replacement was not activated while these gates remain open.

READY / NOT READY reason: technical tests passing cannot override the failed/missing art acceptance gates.

Next implementation: **continue 14C, not Implementation 15**.
