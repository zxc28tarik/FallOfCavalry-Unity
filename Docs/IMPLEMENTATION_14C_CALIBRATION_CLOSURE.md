# Implementation 14C — calibration closure evidence

This document records review-only validation. It does not activate the
production visual catalog and does not change SaveData (schema remains v14).

Continuation audit on 2026-10-06 supersedes the previous attack, contact and
mounted acceptance labels. Branch is `codex/impl-14c-production-character-art`;
the continuation started at clean `cec6acabbe5c896fda8d75238a89a3ea7dde0543`.
Final-commit test and CI evidence must be generated after committing this work.

## MODEL/AVATAR

PASS. The Meshy Hasan source remains unchanged. Unity 6000.3.16f1 imported the
23-bone Humanoid and the calibrated Avatar is valid/human. Meshy Walking and
Running remain on the original source Avatar. The calibrated runtime path uses
the retained CC0 UAL Walk/Jog/Sprint bank because the measured direct
cross-Avatar rebake was lossy.

## ATTACK/WEAPON

The original `WeaponAttack2` attachment was not accepted: the guard-centred
weapon origin sat at the wrist and the hand lay against the blade. Right-hand
parent ownership alone did not prove grip quality. The revised isolated fit
rotates the unchanged kilic across the palm and aligns its existing grip
midpoint to the right-hand palm target. Fresh side/three-quarter captures in
`TestResults/MeshyMotionClosure/WeaponFit20261006/` show the right-side slash and
torso contribution without the previous pivot error. The source has no finger
bones: final close-up grip is still excluded from pilot acceptance.

## LOCOMOTION CONTACT

Partial pass, not full locomotion acceptance. The previous review omitted the
requested FootIK flag in this path and used a floor 15mm below zero. Fresh
graphics-enabled review uses floor zero, FootIK and an explicit opt-in measured
sole correction. Before correction, raw Walking/Running penetrated 40.93/64.38mm.
After correction their worst heel/toe clearances were -0.90/+0.50mm over 121
phases. Actual airborne intervals are preserved; the adapter never translates
the actor or snaps a positive-height foot down to the floor. Source clips,
mesh, skeleton and weights remain unchanged.

Evidence is in `TestResults/MeshyMotionClosure/LocomotionCleaned20261006/`.
All five sampled clips have zero measured loop surface gap and zero actor root
travel. These in-place captures do not prove world-space stance locking at an
external movement speed. Foot sliding remains an explicit acceptance item,
not a PASS inferred from vertical penetration numbers. Existing Idle/Crouch/
Torch contact profiles and the rejected Sword contact diagnostic retain their
separate status.

## MOUNTED

The former `MountedReview2` PASS is withdrawn: a chair-sitting clip left the
boots in front of the saddle, above the stirrups. Revised presentation fitting
aligns the measured pelvis to the existing `Socket_Rider`, the ankles to the
existing stirrup geometry, and the hands to the existing rein endpoints after
Mecanim sampling. No model, bind pose, weights, horse or tack asset is edited.
Fresh side/three-quarter and Idle/Walk/Gallop captures in
`TestResults/MeshyMotionClosure/MountedFitted20261006/` show feasible mounted
placement; measured pelvis/ankle/hand anchor residuals are below 0.001mm.
This is isolated mounted feasibility, not activation of a mounted production
profile or a final horse/rider animation library.

## LOD2

Repaired review candidate: 9586/5752/5606 triangles. LOD0 and LOD1 are unchanged.
Strict derivation from preserved LOD1 freezes 2304 head/belt triangles and
preserves UV/material seams. The known bright belt triangle is gone in actual
Windows-player close/tactical captures in
`TestResults/MeshyLod2Candidate20261006/`. Face readability is improved.
The desired reduction was not reached: LOD2 is only 2.5% below LOD1, rather than
being mislabeled as an aggressive crowd LOD. Further reduction is an art/performance
limitation, not permission to damage LOD0 or unlock UV seams.

`Calibration/LOD2Derivation.json` records exactly the two changed LOD2 outputs
and the immutable original calibration snapshot. The original calibration
manifest is not refreshed to hide a change. All other protected source,
Avatar, motion, material and LOD1 hashes must still match.

## PERFORMANCE

The earlier historical benchmark did not include the newly measured sole
cleanup. A fresh, otherwise identical 100-actor review on `79e2c10` found
46.28ms animation evaluation / 61.34ms scene CPU with per-actor measured
cleanup, versus 8.32ms / 30.20ms without it. That regression is NOT a PASS.

The continuation now samples the actual clip/Avatar/FootIK path once and shares
a bounded eight-profile, 240-interval contact lookup. Runtime applies six leg
rotation deltas; source assets, arms, gameplay position and save remain unchanged.
The binding rejects changed Avatar, scale, floor/root height and orientation.
First-profile preparation is deliberately reported separately from assembly
and frame timing: it is not a free operation or hidden from the benchmark.

Pre-commit diagnostic benchmark with calibrated Avatar and all contact cleanup:
1 / 12 / 100 actors used 0.32 / 1.91 / 8.87ms animation CPU and
16.71 / 16.81 / 28.82ms scene CPU. First profile plus Animator setup took
360.85ms; 12 / 100 warm-profile Animator setups took 15.25 / 120.32ms.
Warm cache/pool reuse passed, with zero active leases after return.
Evidence: `TestResults/MeshyMotionClosure/LookupDiagnostic20261006/StrideBenchmark/`.
These are dirty-worktree diagnostics, not final-SHA acceptance. GPU frame
timings are unavailable on this driver, not zero. This is technical pilot
viability on the MX130, not a universal 60fps production budget.

## Separate horizontal support review

Vertical contact alone is not a sliding test. Fixed heel/toe surface centroids
are now measured from actual skinned output. A diagnostic external +Z speed is
fit using even samples; odd support samples are held out. The speed is derived
from the native clip stride, NOT assigned to gameplay or silently changed in
the clip. At native playback the Meshy Walking / Running fits are 3.857 /
5.262m/s; Walking's short source loop therefore needs playback-rate matching
for an ordinary walking speed. The calibrated UAL Walk fit is 0.959m/s.

Presentation-only support correction blends in/out around measured support
windows, preserves ankle roll and is bounded to 12cm horizontally. It never
edits clips, skeleton/weights, source geometry, campaign position or the motion
library. Real-player side captures show fixed ground support markers while an
isolated review actor moves externally at the documented fitted speed.

Pre-commit before/after held-out maximum horizontal support drift:
- Meshy Walking: approximately 7cm -> 3.29cm (toe/heel roll overlap retained).
- Meshy Running: approximately 10cm -> 1.62cm.
- Calibrated UAL Walk: approximately 4cm -> 1.39cm.

The supplied two clips retain -0.91mm / +0.25mm worst heel/toe clearance and
zero measured loop surface/hand gap; airborne feet are not snapped down.
Supplemental Jog/Sprint still show 3.31 / 6.42cm support residuals, respectively.
Those results must not be advertised as final high-speed production contact.
All values are flat-floor review evidence, not terrain, turning, transition,
arbitrary-speed or production movement activation. Exact-source Walking and
Running still use their original Avatar; calibrated Avatar uses the retained
UAL clips. Direct rejected cross-Avatar rebake is not promoted.

Evidence: `TestResults/MeshyMotionClosure/LookupDiagnostic20261006/StanceCorrected/`.
The executable regression measures 481 actual baked poses between lookup knots,
checks floor contact and unchanged hand/root positions, and rejects stale
bindings. A separate synthetic regression proves held-out sliding is not
hidden by the fitted speed or an automatic PASS label.

## Tests

- .NET Release: 486/486 passed, 0 failed, 0 skipped.
- Unity EditMode: 703/703 passed, 0 failed, 0 skipped.
- Windows player: attack, locomotion and mounted review processes returned 0.

On 2026-10-06 the pre-commit diagnostic Unity suite passed 709/709 (zero skips),
and two additional actual-skin Walking/Running contact regressions passed in a
separate focused run. .NET restore, Release build and 486/486 tests passed.
These are diagnostic results, not final-SHA acceptance. Full Unity, all eleven
existing pipelines, real-player captures and the calibrated 1/12/100 benchmark
must be repeated on the final clean commit, followed by push and exact-SHA CI.

On clean `79e2c10128b4c7721ae22a6c829ca4d893e0900f`, full .NET 486/486,
Unity 711/711 and all eleven existing pipelines passed with zero failures or
skips. Foundation CI run `37494754914` succeeded on that exact SHA. Its scope is
.NET restore/build/test; it does not stand in for local Unity/player validation.
Those results are historical after the lookup/support refinements above and
must NOT be reused as final evidence for a later commit.

## Continuation boundary

Continue from the calibrated Meshy assets and these measured presentation fixes.
Do not return to procedural clothing, regenerate the model/rig, weaken the
ProductionArt gate, change gameplay/save schema, or begin Implementation 15.
Full 14C completion and production catalog activation remain separate gates.

## Additional Meshy motion preparation — 2026-10-06

The user is preparing extra motions on the same retained Meshy Hasan and asked
for useful parallel preparation. This supersedes the earlier prohibition on
requesting additional exports only to the extent that the user voluntarily
supplies more animations; no replacement character, clothing, texture, rig or
gameplay work is authorized. Implementation 15 remains outside scope.

Preparation started on clean `3d30cedfe3df17050fb30b8f3caae3a7204add30`, with
the same SHA verified on `origin/codex/impl-14c-production-character-art`.
Prior calibration evidence remains evidence for that SHA, not new motion
acceptance or test results for a later preparation commit.

`Tools/Art/audit_meshy_animation_intake.py` is a read-only binary-FBX preflight.
It accepts one FBX or reads one FBX member directly from a ZIP WITHOUT extracting
files or copying anything into Unity. SHA-256 provenance and original input
integrity are recorded in ignored `TestResults/MeshyAnimationIntake/` reports.
The default comparison is the existing authoritative runtime Hasan FBX.

It compares decoded Geometry (including UV/material-index layers), Model rest
properties, Deformer/skin data, bind Pose, protected object connections and
coordinate/unit settings. Exporter object-ID renumbering, object/connection
ordering and array compression do not create false character-change reports.
All other exact-data differences require review; ordinary DCC float/layout
differences are NOT silently accepted as equivalent. Animation stacks use
their in-file defaults to inventory durations; labels do not prove motion
quality, handedness, looping or in-place movement.

The original supplied ZIP compared to the media-stripped runtime FBX returns
`CONTINUITY_MATCH_NOT_ACCEPTANCE`, with only Running (0.708333333s) and Walking
(0.433333333s), no added clips, and unchanged source hashes. Its ZIP hash remains
`58acdcc19240ebde805dc1aa1fa877dee844eceaab8319f500de68054b141cde`.
The preflight has 28 synthetic regressions, including changed geometry, UV,
weights, parent/rest/bind transforms, axes, malformed data, object-ID changes,
ZIP selection and archive path-traversal names (no extraction occurs).

Commands (Python 3.11+; no Blender or extra Python package required):

```powershell
python Tools/Art/test_meshy_animation_intake.py
python Tools/Art/audit_meshy_animation_intake.py --candidate "C:\path\new-motions.zip"
# If the ZIP contains multiple FBX exports, choose exactly one member:
python Tools/Art/audit_meshy_animation_intake.py --candidate "C:\path\new-motions.zip" --fbx-member "exact/path/model.fbx"
```

Exit 0 means exact protected-character continuity ONLY; exit 2 requests manual
continuity review; malformed/unsupported inputs fail with a nonzero exit.
No original, runtime asset or material is overwritten, and no motion is
automatically installed, retargeted or promoted. External textures, shader
settings and material quality still need the existing Unity/player checks.

When the actual new export arrives:

1. Keep it in ignored local intake storage and record container/FBX hashes,
   source Meshy identity and the user's selected motion names. Review new
   incorporated-motion provenance before any public distribution; the prior
   license audit is not a blanket permission to redistribute standalone clips.
2. Run continuity preflight. Investigate differences without changing the
   authoritative character; matching data does not certify an Avatar or motion.
3. Test each actual clip on the retained original Meshy Avatar first, then A/B
   against the calibrated Avatar. Do NOT force native Meshy clips onto the
   calibrated cross-Avatar path that was previously measured as lossy.
4. Evaluate real Mecanim/skinned output through the existing assembler,
   cache/pool and unchanged right-hand kilic attachment. Compare native versus
   existing candidate Idle and Attack; check right-side slash/torso, socket,
   contact, sliding, loops and material preservation in Windows player captures.
5. Keep missing finger articulation and final weapon-grasp limitations explicit.
   Extra animation cannot introduce bones the model does not have.
6. Promote only visually validated isolated candidates, repeat affected tests
   and final-SHA CI, and leave production-catalog/full-14C gates separate.

At preparation closure, NEW_MESHY_MOTION_QA = NOT RUN: the additional animation
export has not been provided. No new Avatar, attack, locomotion, mounted or
production-art PASS is asserted by this preparation work. Save remains v14.
