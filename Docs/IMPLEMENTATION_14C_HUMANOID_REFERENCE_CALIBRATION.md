# Implementation 14C — Humanoid reference-pose calibration

## Authority and scope

This records the current user's calibration request and the experiments performed
against base `3a11ec640448443a72058cfa91cea8d4a30f9da3` on
`codex/impl-14c-production-character-art`. The user message remains authoritative.
Initial local HEAD and remote branch match that SHA; initial worktree is clean.

No Implementation 15, new character/export, clothing/texture generation, donor
search, skeleton/weight rebuild, MakeHuman fallback, gameplay or save changes.
Save remains v14. Original Meshy mesh/rig/weights/FBX/materials, all three texture
profiles and existing LOD assets are protected. Calibration uses separate Avatar
and animation candidates. Production historical catalog remains inactive.

## Required experimental contract

1. Document the actual Generic source -> source Avatar -> HumanPose/muscles ->
   Meshy Avatar -> Animator -> skin path separately from supplied Meshy Walk/Run
   -> original Meshy Avatar -> Animator -> skin. Separate source animation,
   source Avatar, target Avatar, orientation, contact and choreography failures.
2. Audit both original rig/Avatars: for EVERY mapped Human bone record original
   name, parent, rest local position/rotation, world position/right/up/forward,
   Unity mapping, side and humanScale. Include hips, spine/chest/upperchest,
   neck/head, both shoulders/upperarms/lowerarms/hands/upperlegs/lowerlegs/feet/toes.
   Detect forward mismatch, root180yaw, left/right mapping error, mirrored lateral
   axis, A/T pose, shoulder orientation and bodyRotation interpretation.
   Never apply a blind X sign flip or180degree root rotation.
3. Clone and restore exact bind/rest, construct measured canonical Unity T-pose:
   upright torso/head, symmetric vertical legs, forward feet, horizontal arms,
   straight elbows, consistent hands/shoulders, semantic left/right and forward.
   Build separate AVT_MeshyHasan_Calibrated using BuildHumanAvatar or controlled
   HumanDescription. Mesh/weights/bindposes/hierarchy remain unchanged. Restore
   runtime rest afterward. Original Avatar remains available for A/B comparison.
4. Test zero-muscle HumanPose with canonical body position/rotation on both
   calibrated Avatars. Record joints, arm/feet directions, normalized proportions.
   Valid/human flags alone do not establish semantic compatibility.
5. Compare A original target; B calibrated target; C calibrated source+target
   ONLY IF B insufficient. Same Idle, Turn, OneHandedAttack, ArmRaise, Crouch,
   MountedSeated motions/phases. Record wrists/elbows/shoulders/feet/pelvis/head,
   minimum skinY, intended direction and actual Windows screenshots.
   Actual Animator + Avatar + Humanoid clip + SkinnedMeshRenderer is authority,
   not only HumanPoseHandler math. Original diagnostic clips remain diagnostic.
6. If conversion succeeds name new candidates ANM_HumanoidCandidate_<motion>
   and record exact source provenance; never call them final production motion.
7. Attack hard gate: correct direction, shoulder/elbow/wrist path, torso intent
   and weapon side, no both-arms-out pose, mirrored/wrong-side slash or shoulder
   collapse. No finger articulation exists; finger curl alone is not a failure.
8. Reference semantics first, then presentation-only target-specific contact
   cleanup if needed. Allowed sampled per-clip body/root height, foot contact
   curves, Animator IK/two-bone IK or deterministic adapter based on target leg
   lengths/feet/toes/phase. No arbitrary global+7cm, source edits without
   documentation, hidden feet, campaign position/height or gameplay movement change.
9. Source Walking toe penetration down to-0.0369m is independent. Build measured
   Walk AND Run contact cleanup candidates; check sliding, toes/heels, hovering,
   loop seam. No gameplay root motion; world motion remains external.
10. Only after body attack intent works, validate right-hand socket orientation;
    presentation IK/correction allowed. Never bake a weapon or edit loadout truth.
11. Fresh real Windows-player captures under
    Docs/Evidence/Implementation14C/MeshyHasanCalibration/:
    01-original-avatar-idle.png,02-calibrated-avatar-idle.png,
    03-original-avatar-attack.png,04-calibrated-avatar-attack.png,
    05-original-avatar-crouch.png,06-calibrated-avatar-crouch.png,
    07-calibrated-walk-contact-a.png,08-calibrated-walk-contact-b.png,
    09-calibrated-run-contact-a.png,10-calibrated-run-contact-b.png.
    Label dual-calibration evidence distinctly. No Inspector/Blender substitutions,
    retouching or synthetic images.
12. Executable quality checks: valid/human, not swapped left/right, neutral arm
    direction, intended attack wrist side, bounded unexpected divergence,
    documented foot/mesh floor tolerance, finite values, loop continuity and
    in-place root. Sensible target-specific metrics support, never replace QA.
13. Only AFTER animation calibration passes, repair LOD2 belt triangle/nearface
    damage/temporal issues; preserve seams/material/silhouette, use more triangles
    if needed. Never change LOD0.
14. On-foot pass requires sound original locomotion, natural idle, intended attack,
    crouch feet abovefloor, plausible armraise, correctorientation, unchanged
    material, no catastrophiccloth and usable LODs at intended distance.
15. Only AFTER on-foot pass: existinghorse mountedQA with11-mounted-side,
    12-mounted-three-quarter,13-mounted-motion-sample. Validate pelvis/saddle,
    thighs/knees,boots/stirrups,hands/reins,coatintersection,torso,mountedmotion.
    Do not rebuild horse.
16. Only AFTER on-foot pass: actual assembler/cache/pool calibratedAvatar candidate
    clip benchmarks1/12/100 actors: cold,warm,renderers,skins,materials,texture
    memory,CPU/GPU(ifavailable),allocations. Actorcount screenshots are not benchmarks.
17. Overall pilot selection needs superior model, solvedreferenceattack, solved
    crouch/walk/runcontact, feasiblemountfit and viable100actors. ProductionArt=0
    is not required yet; do not activate all historical profiles.
18. Failure must name incompatible layer: targetAvatar,sourceAvatar,root,
    musclemapping,sourcechoreography,contact,mount. If diagnostic source motions
    are conclusively unsuitable after calibration, exact marker:
    VALIDATED_PRODUCTION_HUMANOID_MOTION_LIBRARY_REQUIRED. Never blame model or
    ask for newcharacter/secondFBX.
19. Preserve .NET486 andUnity659 baselines. Run all11existingpipelines, nofailed
    tests or criticalskips. Commit only validated work; push and verify localSHA=
    remoteSHA=CI SHA, cleanworktree. Visual FAIL is not relabeled PASS by unit tests.

## Actual paths before calibration

- Generic diagnostic path: HasanMotionReview/ANM_HasanDonor_* Transform curves
  sampled on original donor hierarchy with a temporary Generic sampling Avatar;
  saved donor Human Avatar extracts HumanPose. Existing Retargeted clips hold
  sampled muscle/RootT/RootQ curves. Meshy pilot Animator with original imported
  Meshy Avatar executes them through an AnimationClipPlayable and actual skin.
- Supplied path: optimized unchanged-animation Meshy FBX -> imported Walking /
  Running Humanoid clips -> original Meshy Avatar -> same actual Animator/skin.
  World root motion is disabled; vertical source movement is retained in pose.
- VisualProof named actions are pelvis-only proof curves, not a production
  Humanoid motion library. Articulated HasanMotionReview sources are diagnostic.

## Experiments and decision

Unity6.3 official guidance was read using Firecrawl before implementation:
[BuildHumanAvatar](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AvatarBuilder.BuildHumanAvatar.html)
requires the topmost hierarchy root and valid-Avatar verification.
[Avatar configuration](https://docs.unity3d.com/6000.3/Documentation/Manual/ConfiguringtheAvatar.html)
distinguishes successful required-bone matching from correct T-pose setup.
[SetHumanPose](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HumanPoseHandler.SetHumanPose.html)
defines normalized body position/rotation relative to the humanoid root. A
zero-muscle sample is evaluated separately; it is not assumed to equal T-pose.

### Measured reference result

The first B-only trial was insufficient; A/B was reviewed before authoring C.
The final reference experiment uses gravity/world +Y, not the original leaning
hip-to-neck vector. Root transforms remain identity. Original prefab, imported
Avatar, mesh, bindposes, weights, FBX, texture/material and source clips remain
unchanged; their file hashes are guarded by the calibration provenance manifest.

The donor's named Left side is physically +X, while its weighted boot geometry
points +Z. Name-derived forward therefore opposes physical forward (dot about
-1). Meshy's Left is -X; actual toe and head-front markers agree with +Z. There
is no evidence authorizing a root180yaw. C corrects twelve paired assignments
in a NEW source HumanDescription, not the source names, hierarchy or clip curves.
In that anatomically corrected mapping, original `Hand_R` is `HumanLeftHand`.

Both new reference Avatars are valid/human. Measured arm/leg segment alignment
error is below0.1degree (horizontal straight arms and gravity-vertical legs).
The target's head-front marker faces the measured forward. `head_end` and
`headfront` are intrinsically nonorthogonal (about81.018degrees); its raw
8.982degree head-end/up difference is not silently relabeled a new head defect.
Missing donor toes/shoulders are explicitly unavailable in the joint arrays;
they are not synthetic bones. Finger/palm roll lacks independent landmarks and
remains unverified, not accepted from `isValid` alone.

An early candidate incorrectly flattened the ankle-to-toe segment. That segment
is naturally sloped approximately41degrees down, not a horizontal sole axis.
This caused artificial toe-up boots and was corrected before the retained
candidate: original world foot pitch/roll is preserved; only each projected toe
heading is yaw-aligned. Original/retained pitch differences are below0.00004deg.
No foot bones, weights, mesh or global height were edited. The before-fix trial
is not the accepted/reference candidate used in the versioned review images.

Zero-muscle tests use centered normalized COM height and identity bodyRotation,
recording the originally extracted values separately. Corrected source/target
zero-pose arm directions agree to approximately1.616degrees; legs agree within
floating-point precision. Per-foot directions differ1.104/2.260degrees. The
earlier14.246degree averaged-foot-axis result was a measurement artifact: it
discarded each bind foot's splay. The retained audit uses each target toe axis
and each no-toe donor foot's weighted-sole longitudinal axis, records the old
artifact separately, and keeps the10degree review tolerance unchanged. Zero
muscles are not assumed to mean T-pose. Normalized wrist-position difference
is0.1321 and is recorded as a proportion difference, not a universal failure.

| Path | Mean limb direction difference | Maximum | Minimum skin Y |
| --- | ---: | ---: | ---: |
| A: original source / original target | 48.9547deg | 165.1518deg | -0.071745m |
| B: original source / calibrated target | 63.3527deg | 162.8198deg | -0.088073m |
| C: calibrated source / calibrated target | 5.6946deg | 14.0025deg | -0.033308m |

These are measured comparison statistics over six motions and eight phases,
NOT arbitrary art scores or automatic acceptance thresholds. Each includes
actual Mecanim Humanoid clip playback and baked LOD0 skin. Source/target limb
directions are compared in their recorded reference frames.

Executable C quality diagnostics retain exactly three failed review checks:
required right-hand attack ownership, absent authored torso intent, and sampled
ground penetration (2mm tolerance). Loop muscle endpoint difference is about
2.024e-6; normalized body endpoint displacement converted to target scale is
1.061e-8m; actor root translation/rotation remain zero. Endpoint continuity is
not a claim that foot sliding, temporal seams or contact cleanup have passed.

### Actual Windows visual QA

The existing review player has an opt-in `--meshy-calibration true` mode. It
uses the SAME VisualProfile -> VisualSoldier3DAssembler -> signature/cache/pool
path, with the candidate Avatar assigned only to a leased review actor. Its
original Avatar/controller are restored before returning the lease. Normal
37-image pilot review remains a separate mode with its original Avatar.

A/B/C captures use identical camera framing and actual groundY=0, not the old
pilot's -0.015m display floor. There are62 unretouched Windows images: three
phases for each of six actions in A/B/C, plus two raw Walk/Run phases on A/B.
The player finishes with0leases, one created view and21reuses. Those counts
verify the review path, NOT a1/12/100 performance benchmark.

Visual findings from the C images:

- Idle arms now fall naturally; the old lateral arm spread is gone. Materials
  and character identity are preserved. The artificial boot dorsiflexion is
  removed by the measured reference-foot fix, not a skin or garment edit.
- Attack now transfers the source down-forward arm gesture, rather than the old
  outward/upward failure. However it is anatomical LEFT-handed. At phase.5 the
  right hand remains by the body. Across measured phases left-wrist travel is
  about0.591m, right-wrist travel only0.0021m from body translation.
- Source hips/spine/chest have exactly zero positional/rotational action travel;
  target torso rotations likewise remain unchanged. This is an arm-only
  diagnostic gesture, not the required right-weapon attack with torso intent.
  A clip mirror could change sides but would not supply missing choreography.
  No mirror, weapon-side swap or invented torso motion is hidden in C.
- ArmRaise is substantially more plausible. Crouch is recognizable but still
  penetrates the actual floor by0.033308m at phase.5. Idle minimum skinY is
  -0.011361m. Neither is accepted as solved contact.
- Raw original Walking/Running controls are captured separately on A and B.
  They are not contact-corrected clips, and changing Avatar alone does not
  establish unchanged source-locomotion quality. B Walking at phase.75 visibly
  raises an arm that the original A path does not: the imported muscle curves
  were authored against the ORIGINAL Meshy reference. They cannot be promoted
  unchanged onto the new reference. The original working A path is retained.
  A non-destructive original-pose -> calibrated-muscle locomotion conversion
  remains required before choosing the calibrated Avatar as the unified path;
  no second FBX or model replacement is implied.

### Failure layers and gated work

Reference transfer has materially improved. Overall calibration/on-foot
acceptance is still FAIL, not a failure of the Meshy character model.

| Layer | Finding / decision |
| --- | --- |
| Source animation | Existing Generic diagnostic actions, not validated production Humanoid motion |
| Source Avatar | Non-T reference and reflected name-based side convention; separate C candidate repairs measured semantics |
| Target Avatar | Separate measured reference candidate valid; original preserved; accidental foot-pitch change corrected, palm-roll remains unverified |
| Root/orientation | No blind root yaw, X flip, gameplay root motion or campaign transform change |
| Muscle/clip execution | Actual Animator reproduces corrected action much more faithfully; finite measurements and fresh execution required |
| Choreography | C faithfully transfers LEFT-arm-only action; required right-side attack/torso hard gate fails |
| Contact | Crouch penetration and boot orientation remain; source Walking toe penetration is separate and unsolved |
| Mounted/performance | NOT RUN; on-foot prerequisite has not passed |

Presentation contact cleanup is deliberately not represented as completed:
attack intent must pass first. Required07–10 contact screenshots are NOT_RUN;
raw locomotion images are Additional controls, never substitutes. Right-hand
socket/IK acceptance is also deferred rather than attaching the weapon to the
wrong hand. No finger rig was requested or added.

LOD2 belt/face cleanup is NOT_RUN because animation acceptance did not pass.
Original LOD0/1/2 remain unchanged. Mounted11–13 and1/12/100 actual-path
benchmarks are NOT_RUN, not skipped passing tests. ProductionArt stays closed;
the historical production catalog is not activated. Save remains v14.

`VALIDATED_PRODUCTION_HUMANOID_MOTION_LIBRARY_REQUIRED`

This means a validated right-handed full-body motion source is needed for the
remaining action-quality gate, NOT that the Meshy mesh/rig/export should be
replaced. Original Meshy locomotion needs compatible-reference conversion before
candidate promotion. Target-specific contact/ankle cleanup and later conditional gates
still need validation with those motions. No new motion library was searched,
downloaded or authored in this bounded calibration experiment.

### Reproduction and evidence policy

Generate the separate A/B assets with
`FOC.Editor.Visuals.MeshyHumanoidCalibration.Run`; after B has been reviewed,
`RunBoth` authors C too. `RunVerify` freshly replays diagnostics without asset
mutation. Reports are under `TestResults/MeshyCalibration/calibration.json`.
`CalibrationProvenance.json` beside the new Avatar assets retains the original
input/output hashes and authored reference-bone audit.

Unity text asset types (`anim`, `prefab`, `mat`, `controller`, `unity`) now have
explicit LF checkout policy alongside the existing `asset`/`meta` rules. All31
protected inputs of these types were already LF. This prevents `core.autocrlf`
from changing their guarded bytes on another checkout; no asset content or
production semantics were changed to achieve it.

`Tools/Test-MeshyCalibration.ps1 -UnityEditor <detected Unity.exe>` is the final
clean-HEAD runner: .NET restore/Release build/test; original import/motion
checks; ALL EditMode tests including discovered calibration tests; Windows
build; original37 images; read-only A/B/C verify; fresh62-image player matrix;
and all11 existing pipelines. Technical PASS is expressly NOT visual PASS.

Pre-commit diagnostic run `0d42f52bdd7c4dc9ac9002f7576b55ae` executed .NET486/486
and Unity674/674 with0failed/0skipped. The15 added cases comprise13 reference/
diagnostic-integrity tests and2 actual candidate-Avatar lease-lifecycle tests.
Tests explicitly require unresolved quality flags to stay visible; they do not
assert that the current diagnostic attack or contact has passed. This dirty
pre-commit run is NOT reused as final-SHA acceptance evidence.

`Tools/Publish-MeshyCalibrationEvidence.ps1 -CaptureDirectory <actual captures>
-AllowDiagnosticSnapshot` stores a byte-exact review snapshot under
`Docs/Evidence/Implementation14C/MeshyHasanCalibration/`. Its manifest explicitly
labels pre-commit/dirty review images as NOT final-SHA evidence. ScenarioC
files are distinctly named under `Additional/`. Missing contact views stay
NOT_RUN, and old managed images are moved to recoverable ignored backups.

Fresh final-SHA verification, CI, exact commands and test counts are recorded
after committing under `TestResults/MeshyCalibration/Runs/<finalSHA>/<runId>/`
and the final report. This document does not reuse base-SHA tests for a new SHA.
Implementation15 is not started.
