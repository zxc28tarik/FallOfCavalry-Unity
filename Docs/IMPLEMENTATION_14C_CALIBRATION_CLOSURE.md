# Implementation 14C — calibration closure evidence

This document records review-only validation. It does not activate the
production visual catalog and does not change SaveData (schema remains v14).

## MODEL/AVATAR

PASS. The Meshy Hasan source remains unchanged. Unity 6000.3.16f1 imported the
23-bone Humanoid and the calibrated Avatar is valid/human. Meshy Walking and
Running remain on the original source Avatar. The calibrated runtime path uses
the retained CC0 UAL Walk/Jog/Sprint bank because the measured direct
cross-Avatar rebake was lossy.

## ATTACK/WEAPON

PASS for the isolated review. `WPN_Kilic_01` is instantiated under the real
`VisualSocket.RightHand` / `Socket_RightHand` at runtime. Evidence:
`TestResults/MeshyMotionClosure/WeaponAttack2/weapon-attack-evidence.json` and
the eight real-player PNGs in that directory. Visual review shows a right-side
one-handed downward slash with believable wrist/torso contribution and no
floating weapon. Finger articulation remains unavailable on the source rig.

## LOCOMOTION CONTACT

PASS for the reviewed runtime path, with source limitations recorded. Real
Windows captures are in
`TestResults/MeshyMotionClosure/LocomotionReview2/`. The raw Meshy Walking and
Running clips are preserved on their source Avatar; calibrated locomotion uses
UAL Walk/Jog/Sprint. Numeric contact profiles pass for Idle, Crouch and Torch;
the Sword contact profile remains a diagnostic failure during airborne/stance
transitions and is not promoted as a gameplay contact profile.

## MOUNTED

PASS for feasibility/fit review. The existing Anatolian horse, harness,
`Socket_Rider`, calibrated Hasan and horse Idle/Walk/Gallop clips were rendered
in the real Windows player. Side and three-quarter evidence is in
`TestResults/MeshyMotionClosure/MountedReview2/`. The rider is seated on the
saddle without catastrophic coat/horse intersection; no horse or gameplay
asset was rebuilt.

## LOD2

NOT READY. LOD0/LOD1 remain unchanged and runtime inventory is 9586/5752/2396
triangles. A new constrained LOD2 candidate was not adopted: the seam/feature
guard correctly refused a candidate when protected head/belt regions prevented
a genuine reduction below LOD1. The existing LOD2 remains the authoritative
runtime asset until a candidate is rendered and visually accepted.

## PERFORMANCE

PASS for the technical assembler/cache/pool benchmark. Evidence:
`TestResults/MeshyMotionClosure/CalibratedBenchmark1/calibrated-benchmark.json`.
The effective Avatar is `AVT_MeshyHasan_Calibrated`; warm reuse passed for 1,
12 and 100 actors with zero active leases after return. GPU frame timings were
not available on the MX130 driver and are reported as unavailable, not zero.

## Tests

- .NET Release: 486/486 passed, 0 failed, 0 skipped.
- Unity EditMode: 703/703 passed, 0 failed, 0 skipped.
- Windows player: attack, locomotion and mounted review processes returned 0.

The full existing-pipeline script requires a clean worktree and is run only
after the validated changes are committed.
