# Implementation 14C — Motion compatibility closure

## Current authority

The user's latest instruction is to continue until successful. This authorizes
continuing the existing 14C pilot after the measured calibration failure at
`0c6ccff569f7d37b87f36ad660baa6a6f9e94d13`, not skipping its visual gates.

The same Meshy Hasan mesh, hierarchy, weights, material and persistent gameplay
identity remain authoritative. No new character/export, garment generation,
paid purchase or Implementation 15. Save v14 and production catalog remain
unchanged. Original A/B/C diagnostic evidence must not be relabeled successful.

## Work sequence

1. Acquire a properly licensed no-cost motion source with real right-handed
   attack/torso motion. Record upstream identity, license, archive and selected
   file hashes before production use; only animation, not a replacement body.
2. Compare source motion on original and calibrated Meshy Avatars using actual
   Animator/skin output. Choose based on measured and Windows visual results;
   do not assume the newest Avatar must win.
3. After body attack intent passes, apply measured target-specific contact
   cleanup to separate candidate clips. Preserve flight phases and external
   world movement. No arbitrary global lift or unreported source edits.
4. Verify Idle, Turn, Attack, ArmRaise, Crouch, supplied Walk/Run, loop/contact,
   sockets and material/deformation. Only then repair the known derived LOD2
   defects, preserving LOD0 and an explicit provenance trail.
5. Only after on-foot passes, validate mounted fit with the existing horse and
   measure actual assembler/cache/pool performance at 1/12/100 actors.
6. Re-run all baseline tests/pipelines and Windows evidence at final SHA;
   commit/push/CI and clean-worktree checks. No READY from test counts alone.

This document is a work record; no unexecuted gate is marked PASS here.

## Licensed motion source and import diagnosis

The free CC0 Quaternius Universal Animation Library Standard v3 was acquired
from its official itch.io distribution. Only `Unity/UAL1_Standard.fbx` (the
non-root-motion export) is used. Its source body is NEVER substituted for Hasan.
The archive, selected export, included license and exact URLs are recorded in
the MotionLibrary provenance ledger. The actual FBX contains 43 Humanoid clips;
old advertising counts are not treated as inventory evidence.

The first trial exposed two importer problems, not a Meshy skinning defect:

- The global FOC importer optimized this foreign source and stripped live bones.
  The intake now has a narrow source-root exception, as the Meshy source already
  does. Canonical production imports retain their existing policy.
- Enabling the upstream-prescribed axis baking while retaining a previous
  auto-generated HumanDescription left a stale reference skeleton. Unity logged
  calf/foot length mismatches up to 0.59 m; the source itself played upside down.
  The intake now rebuilds ONLY this source's auto-mapping after axis conversion.
  No blind target root yaw, X-sign flip, source mesh, weights or FBX edit is used.

Independent Blender source sampling confirmed an upright source. After the
controlled reimport, actual Unity source playback is upright with ground-level
feet. A regression test samples Idle and Sword using Mecanim and the real skin;
`Avatar.isValid` alone would not have caught the stale-reference failure.

## Body-pose comparison before contact cleanup

Fresh dirty-worktree trial: `TestResults/MeshyMotionClosure/Trial3/`, 48 actual
Windows player images through the existing assembler/cache/pool. These are
development evidence, NOT final-SHA acceptance evidence. Earlier failed trials
are retained and are not relabeled successful.

On the original Meshy Avatar the licensed Idle folds the arms behind the body.
The separately calibrated Avatar restores natural arms-down Idle. In four
Sword phases (.15/.35/.55/.75), the calibrated target performs a right-arm
windup, forward/downward slash and recovery with torso involvement and a
counterbalancing left arm. It no longer reproduces the old bilateral arm-open
diagnostic failure. Bare hands remain unarticulated; weapon grip is not claimed.

Actual sampled Sword measurements (60 Hz, no Foot IK): source right-wrist
travel 0.8333 m vs left 0.5174 m, torso rotation 60.56 degrees; calibrated Hasan
right 0.9263 m vs left 0.5790 m, torso rotation 67.61 degrees. This supports the
visible action direction; it is not a universal quality score.

Ground contact is still FAIL at this stage: calibrated Sword skin minimum
-0.1277 m and crouch approximately -0.14 m. The source is approximately grounded
(-0.0052 m for Sword). Correct body intent permits contact experiments, NOT an
on-foot PASS, mounted approval, or ProductionArt acceptance. Sitting is merely
a seated candidate, not proof of a usable riding animation.

## Contact and locomotion experiments

Enabling Unity's built-in Humanoid Foot IK on the same calibrated target and
unchanged licensed clips reduced maximum skin penetration from 144.6 mm to
12.0 mm (Crouch), 127.7 mm to 16.3 mm (Sword), and 48.1 mm to 10.2 mm (Idle).
Windows Trial4FootIK confirms the corrected leg poses preserve body action.
The remaining target-boot sole difference is being addressed by explicit
phase-sampled presentation-child Y profiles, not gameplay movement, a global
offset, changed body dimensions or rewritten source muscle curves.

A separate attempt to re-encode the supplied Meshy Walking/Running for the
calibrated reference was **rejected**. Actual source Animator playback was
copied to a same-hierarchy clone, read with HumanPose, baked, and checked at
authored and held-out phases. Even immediate Get/Set redistributes axial twist:
at Running phase .604651 the calibrated left upper-arm rotation differs by
64.923 degrees while the hand position remains within 1 micrometre. A surface
vertex weighted 95.166% to that upper arm moves 91.983 mm. The original-Avatar
control is also not lossless, but the changed reference amplifies this effect.
No root drift, bone-side flip, helper-bone problem or changed weight was found.

The strict 5 mm / 1 degree preservation gate is NOT relaxed. Rejected conversion
clips must not enter a selected runtime bank. Original Meshy Walking/Running
continue to be validated on their original Avatar; the common candidate runtime
bank will instead test the licensed library's own Walk/Jog/Sprint on the same
calibrated Avatar as its actions. This avoids unsupported crossfading between
two different Avatar reference interpretations and preserves original sources.
