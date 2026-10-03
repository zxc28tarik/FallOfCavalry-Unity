# 14C — Persistent MakeHuman retry

Date: 2026-10-03. **DRAFT / NOT READY. No production activation.**

The operator requested an extended autonomous retry before moving to a Meshy
pilot. Starting branch: `codex/impl-14c-production-character-art`; starting clean
HEAD: `6a07de1032f5f11dc9a61b5b92a45d88aa1e9409`. The original donor checkpoint
`3889797a4af1b2a01dcf17da1c6a3e1792b239fc` and earlier rejected stash are preserved.

## Scope and unchanged authority

Hasan only. Same pinned CC0 MakeHuman garments, hm08 human basis, mature head,
hair, and canonical 18 runtime bones. No new source pack, account, paid asset,
Meshy generation, Quaternius revival, procedural replacement tunic, legacy web
art, or Implementation 15. Save schema remains v14. Domain/gameplay truth,
persistent loadout, production visual catalog and ProductionArt rejection rules
are unchanged. All additional behavior is isolated candidate presentation or
authoring/validation tooling, not accepted production animation.

Source archive URLs, authors, filenames and hashes remain in
`ArtSource/HistoricalSlice/Upstream/ClothingDonors/PROVENANCE.md`. Historical
references and their limits remain in
`IMPLEMENTATION_14C_HASAN_DONOR_TRANSFORMATION.md`. Derived binding/sash details
are tailoring interpretations, not newly proven historical reconstruction.

## Distinct repair rounds

- **r5:** recover original `.mhclo` barycentric body correspondence instead of
  spatially nearest unrelated arm/neck weights. Constrain collar/shoulder and
  lower-skirt influences; refine the donor elbow volume. Recover original
  upstream finger rig/weights offline to author Grip_L/R shapes without adding
  runtime finger bones. Repair gait stance/swing and elbow pole targets. The
  first focused suite exposed an excessive run-elbow offset; fix the pose,
  not its 18cm tolerance. Actual renders exposed detached cuff scraps in the
  waist-band extraction and substantial trouser/coat intersections.
- **r6:** keep only the connected torso sash, smooth waist influence changes,
  and author bounded skirt corrective shapes from 20 real joint-pose samples.
  Runtime reads actual joint directions and interpolates nearest-three shapes;
  no capture-frame, animation-name or time-specific correction. Preserve zero
  Neutral and report unresolved intersections. Real Windows captures improve
  considerably, but residual waist clipping and hooked thumb remain.
- **r7:** first refit the waist, then trace the remaining neutral penetration
  to an already-intersecting thigh ring at y=0.643m. Refit 577 original waist/
  skirt vertices against fitted trousers (maximum displacement 36.93mm);
  derive narrow collar/front/cuff/hem facings from the
  existing donor triangles with interpolated original UV/weights. Bind their
  pose corrections to the original coat surface. Replace the thumb's nearly
  straight proximal chain plus 103-degree hooked tip with a continuous
  three-segment arc, preserving lengths/root/target. Measured hinges are about
  55.33 degrees each, length error below 0.03 micrometres. Add natural partial
  grips for idle/run. Close-up and held-out motion views remain mandatory.
- **r8:** diagnose the remaining Run250 front-waist intersections above the
  pinned corrective region, then add up to 28mm of controlled ease to 211
  existing front-waist vertices. Correct and regression-test open-band winding:
  an inward-facing sash had been backface-culled, not hidden by a wrong color.
  Bisect the original neck visibility bridge at y=1.4945, because the unchanged
  mature head already contains its neck from y=1.494; a wider duplicate surface
  had extended to about y=1.52. Preserve interpolated UV/weights and verify outward
  normals. No canonical body/head geometry or skeleton is replaced.
- **Final engineering review:** normalize corrective features in the unique
  skeletal Root frame rather than the outer actor GameObject, so a rigid turn
  cannot spuriously select gait deformation. Add three child-root yaw/translation
  regressions and apply real runtime correctives before all eight real-prefab
  finite-skin checks. Training poses keep the same identity skeletal Root.

## What the new diagnostics do — and do not prove

`HasanDonorPoseAuthoring` exports actual canonical skin matrices from the same
review Animator. `solve_hasan_correctives.py` uses the existing trousers/body
as collision surfaces, inverse skin mapping and bounded donor-vertex offsets.
It preserves source topology/UV/weights, pins the upper garment, and reports
ambiguous or unresolved intersections rather than deleting underlying pants.

The component interpolates six root-local unit-vector features (18 floats),
resets only Pose_ shapes, preserves Grip shapes, uses deterministic serialized
tie order, and does not solve vertices every frame. It is not cloth simulation;
arm-only poses cannot be distinguished by these lower-body/torso features.
Future production animation compatibility and crowd cost are not accepted.

Nearest-three inverse-distance selection is **not guaranteed temporally
continuous** when its third/fourth neighbors swap. A numerical probe between
Walk875 and Walk0 feature vectors found an approximately 20.85mm bind-space
jump; this is a feature-space probe, not a measured video-frame pop. It remains
an explicit candidate limitation. The tests do not certify smooth production
animation and this component is not activated by the production catalog.

Held-out verification reproduces the runtime interpolation in bind space
before skinning. It checks all three LODs and validates standalone/consolidated
shape equality, finite bounded offsets, pinned upper garment and exact zero
Neutral. Collision counts are vertex/surface diagnostics, not complete
triangle collision or aesthetic acceptance. A diagnostic exit 0 is not a
claim of zero intersections. Derived facings follow barycentric coat shapes;
their independent collision clearance is not certified by this numeric test.

The transfer initially rejected ill-conditioned barycentric coordinates on
tiny donor triangles. A double-precision closest-triangle calculation fixes
the instability; the strict 10mm maximum binding distance is not relaxed.
This was a real authoring failure, not a Unity import failure; validation was
not bypassed.

## Mounted and grip evidence limits

Numerical mounted audit puts pelvis at mount-space `(0,1.785,-0.445)` against
saddle center `z=-0.44`; the pelvis is not simply ahead of the saddle. Original
stirrup ankle targets are retained while knee flexion improves to about 50
degrees. Hand/rein origins agree within roughly 13mm. These numbers do not
approve the visual seat, thigh/cloth contact or production mounted animation.

The kilic review socket aligns its actual handle midpoint to the authored
grip, not its guard-origin. Existing canonical sockets/loadout mappings are
unchanged. Both dorsal and palmar player views are required; a single camera
had hidden the old hooked-thumb defect. Existing weapon art is not upgraded by
improving a hand grip.

## Reproduction and final-SHA separation

Authoring, in order:

1. Blender `Tools/Art/adapt_hasan_donors.py` (regenerates basis, clears poses).
2. Unity `FOC.Editor.Visuals.HasanDonorPipeline.Run` (import/pose sample export).
3. Blender `Tools/Art/solve_hasan_correctives.py -- --selfcheck` and `-- --apply`.
4. Unity `FOC.Editor.Visuals.HasanDonorPipeline.Run` again (import correctives).
5. Unity EditMode, review Windows build, actual player capture/visual review.

On the frozen clean final commit, run:

```powershell
pwsh -NoProfile -File Tools/Test-HasanDonorClosure.ps1 -UnityEditor 'C:/Users/zxc28/AppData/Local/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe'
pwsh -NoProfile -File Tools/Test-14CExistingPipelines.ps1 -UnityEditor 'C:/Users/zxc28/AppData/Local/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe'
```

The first runner records fresh .NET, full Unity EditMode, read-only pose export,
Blender selfcheck and held-out verification, Windows build and 20 player images,
and the unchanged ProductionArt gate. It deliberately cannot accept artwork.
Its final JSON is `TestResults/HasanDonor/Final/validation.json`; prior-suite
results are `TestResults/14c-existing-pipelines.json`. Final SHA/CI evidence
belongs to that run, not to this precommit authoring record. Foundation CI
covers .NET, not licensed Unity Editor or visual review.

No new character derivative, production catalog activation, Accepted folder or
100/250/500-actor and mixed-cavalry claim is permitted until Hasan passes.

## Final draft visual decision

**Hasan remains NOT ACCEPTED after four additional repair rounds.** Technical
defects were genuinely repaired, but the requested commercial historical
semi-realism has not been reached. This is a failed acceptance result, not a
claim that all MakeHuman donor topology is inherently unusable.

The r8 final-draft focused Unity run passes 71/71, zero failed/skipped; its
Windows player produces 20 actual images with no matching exception/error logs.
These are **precommit authoring results**, not substitutes for the final-SHA
suite. The versioned images, intermediate trials and diagnostics are under
`Docs/Evidence/Implementation14C/Drafts/HasanPersistentRetry/`. Their manifests
truthfully record the old HEAD plus dirty worktree in which they were captured;
they must not be relabeled as final-commit captures.

| Evidence | Result and remaining defect |
| --- | --- |
| r8-final 01–03, 07 | Sash visible and source band winding repaired. Still generic smooth red coat, straight zip-like front, weak layered historical tailoring, simplified surface detail. Turn no longer selects a deformation solely from rigid skeletal-root rotation. |
| r8-final 04–06, 18–19 | Shoulder/arm pose and major skirt intersections improved. Front trouser penetration remains, most visibly in held-out run phase 0.3125. This alone rejects motion acceptance. |
| r8-final 10–11, 20 | No exploded mesh. Crouch envelope improved; rigid drape, shoulder/collar transition and skin tabs remain unsatisfactory. Removing duplicate upper neck does not certify the remaining collar fit. |
| r8-final 12–13, 15 | Better knee flexion/grip; numerical saddle/stirrup alignment maintained. Mounted garment drape, tack contact and shared production animation remain unaccepted. |
| r8-final 14, 17 | Hooked thumb repaired. Both angles show a credible closed handle grip with no obvious floating weapon; isolated contact improvement, not full character/equipment approval. Guard/blade join remains crude at inspection distance. |
| Numeric pose diagnostics | Authored samples still record 116 inside-vertex incidences / 191 clearance violations across pose–LOD combinations. Held-out set records 277 / 991. These are summed incidences, not distinct mesh-vertex counts. Ambiguous cases and LOD2 neutral residuals are retained, not hidden. |

The unchanged ProductionArt gate must remain closed. Other characters and
crowd benchmarks were not advanced to manufacture a READY result.

Recommendation after this persistent attempt: move to the previously discussed
**single high-quality rigged Meshy input pilot**, with weapons/mount separate,
then preserve FOC's existing loadout/assembler/pooling architecture during
integration. No Meshy account/API operation, generation or credit expenditure
has been performed here. The existing draft and useful tools are preserved;
the production catalog has not been switched to any new source strategy.
