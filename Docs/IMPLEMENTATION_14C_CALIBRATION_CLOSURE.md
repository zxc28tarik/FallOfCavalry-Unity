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

Historical PASS for the technical assembler/cache/pool benchmark. Evidence:
`TestResults/MeshyMotionClosure/CalibratedBenchmark1/calibrated-benchmark.json`.
The effective Avatar is `AVT_MeshyHasan_Calibrated`; warm reuse passed for 1,
12 and 100 actors with zero active leases after return. GPU frame timings were
not available on the MX130 driver and are reported as unavailable, not zero.

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

## Continuation boundary

Continue from the calibrated Meshy assets and these measured presentation fixes.
Do not return to procedural clothing, regenerate the model/rig, weaken the
ProductionArt gate, change gameplay/save schema, or begin Implementation 15.
Full 14C completion and production catalog activation remain separate gates.
