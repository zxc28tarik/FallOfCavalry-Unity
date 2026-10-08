# Meshy Hasan idle and sword motion pilot

Updated 8 October 2026. The user's latest decision prioritizes 3D motion work and pauses Y0 memory closure; Y0 is not a prerequisite to this isolated pilot. Idle_12 and Right_Hand_Sword_Slash now run on the retained Hasan in a real Windows player. No replacement character, rig, clothing, texture, gameplay or save change. Implementation 15 and production catalog activation remain closed.

## Source integrity

Local source: `C:/Users/zxc28/Downloads/Meshy_AI_hasan_agha_rigged_biped (1).zip`.

Archive size: 1,079,780,247 bytes. SHA-256: `d88ac5898a6b5edf90762a1c121648107bf99fdc67fd73051cd92a3ab8930730`.

The archive contains fourteen individually animated FBXs and three textures. No raw source was uploaded to GitHub. The user previously confirmed Meshy Pro generation in their own account. Source page: <https://www.meshy.ai/tr/agent/T93y9f6f-pnFlcy0V9qei?chat=JElYNRmR9stRXm_gaQKrD>. This is recorded provenance, not independent verification of account history or reference-image rights.

## Motions actually present

Durations below are FBX LocalStart/LocalStop values, not proof of motion quality, looping, handedness or in-place behavior.

| Motion filename portion | Duration (seconds) |
| --- | ---: |
| Idle_12 | 6.000000 |
| Right_Hand_Sword_Slash | 1.500000 |
| Sword_Parry | 1.866667 |
| Sword_and_Shield_Alert_Turn_Left | 1.333333 |
| Sword_and_Shield_Alert_Turn_Right | 3.366667 |
| Walking | 1.033333 |
| Running | 0.633333 |
| Casual_Walk | 4.200000 |
| Left_Slash | 3.166667 |
| Double_Combo_Attack | 2.833333 |
| Stand_and_Chat | 5.166667 |
| Sword_Shout | 6.166667 |
| Fast_Stair_Climb | 0.766667 |
| Archery_Shot | 5.000000 |

No crouch or mounted stack is supplied. New Walking/Running durations differ from the retained original clips; old contact/loop evidence cannot be reused for them.

First isolated candidates: **Idle_12** and **Right_Hand_Sword_Slash**. Their source FBX hashes are respectively `c02617853820fa38f5e7e6fe00d8389309dd371aa4e4fa7fcba0757a3d730ea4` and `b3c635a6b1c7534975241a74408e1baae2514396aa623a44e81b2e5fddd4439e`.

## Continuity result

All fourteen preflights reported `CONTINUITY_REVIEW_REQUIRED` (exit 2), not animation FAIL. Each new source has one `char1` mesh, 4,694 FBX control points, 9,586 triangles and 24 LimbNodes. The retained source has 13,520 control points, the same triangle count and 23 LimbNodes. Runtime vertices after UV/normal splitting are not the same counter.

The new `Armature` skeleton uses different bone names and mesh-local axes. New files share geometry, deformer and bind-pose digests with each other, but not with the retained source. Per-clip model transform properties also vary. A diagnostic axis rotation did not establish exact geometry equality. No blind root rotation, source overwrite or skeleton replacement is authorized by this result.

All three external texture hashes match the original package:

- Base: `9f9db1fdf8a3119b140cba87df8477f62a9725ab8a83ddace8f1a2c2a54ba563`.
- Metallic: `70a7df9313df05901a4734241889054a7fc267855e1b8bf61a122b3b4a177490`.
- Roughness: `bfe86bdcf518eaefbb55439f7e95969e65b6085dde88210f6a861922f8d341b7`.

The existing 2048 runtime material remains unchanged. No finger articulation is inferred from this export.

## Executed and not executed

Intake code regression: `python Tools/Art/test_meshy_animation_intake.py`, **30/30 PASS**. `audit_meshy_animation_intake.py --candidate <ZIP> --fbx-member <exact member>` inspected each of the fourteen members. Per-member JSON reports and the complete filename/hash inventory are local ignored evidence under `TestResults/MeshyAnimationIntake/`; overview: `PACKAGE_20261007.md`. Inspection was on clean `119b56f757b901ff8e2e37f26d1b8336295006b1`, equal to origin at that time.

The original 7 October intake was read-only. On 8 October the two selected hashes were verified after local extraction, imported as Humanoid with their own valid Avatars in Unity 6000.3.16f1, and compared on the retained original and calibrated Hasan Avatars. The raw donors and temporary review scene are under ignored `Assets/FOC/ArtSource/HistoricalSlice/TestResults/MeshyUserMotion`; the production prefab is not overwritten. Canonical-rig stripping and Quaternius-specific axis baking are not applied to these sources.

Real Windows player capture completed with exit 0, 44 A/B images and no outstanding leases. The original Avatar visibly distorts the idle arms; the calibrated Avatar preserves the idle and right-handed attack intent. The existing `WPN_Kilic_01` is separate and parented to the actual `Socket_RightHand`, not baked into the character. Fingers remain unarticulated; final close-up grip is not accepted.

Raw captures: `TestResults/MeshyUserMotion/Captures/8b062420446644618b35d55acc5d8c66/`. Source/original/calibrated/FootIK comparison: `TestResults/MeshyUserMotion/comparison.json` (eight actual Mecanim/skinned cases). Built-in FootIK did not solve the attack elevation and was not selected for this candidate.

Measured presentation-child contact profiles preserve the source-relative vertical motion after removing the per-clip source floor baseline and adapting to target humanScale. This is not an arbitrary global offset, bone/weight edit or gameplay-root movement. The maximum attack correction is 0.107767 m; the profile bound remains 0.12 m. Contact captures completed exit 0 with 44 images: `TestResults/MeshyUserMotion/ContactCaptures/41fbe7e75b7f43bd94c6bd8caf56cbb0/`. At the captured calibrated phases, idle clearance is 0.54–1.12 mm and attack clearance 0–17.88 mm; render-boundary stability passed. These are sampled development measurements, not full continuous foot-contact certification.

Mounted Windows review completed exit 0 with the existing horse, harness, seated donor and presentation fitting; no new Meshy mounted stack is claimed. Evidence: `TestResults/MeshyUserMotion/mounted-5352d81757f7421db79bc8809a9473ea/`. Seat/limb anchor errors are below 0.001 mm, but numeric anchor fit does not accept coat/horse clipping or final rein grip.

Actual assembler/cache/pool benchmark used the **new Right_Hand_Sword_Slash, calibrated Avatar and measured contact profile**, at 1/12/100 actors. All views/representations were warm-reused and all leases returned. At 100 actors, cold assembly was 154.903 ms, warm reuse 57.721 ms, manual animation evaluation mean 7.008 ms and wall-frame interval mean 19.289 ms over 30 frames. One shared material; approximately 10.67 MiB Unity-reported shared texture memory. GPU timing was unavailable, not zero. This is an empty-equipment review workload on GeForce MX130, not release-performance certification. Evidence: `TestResults/MeshyUserMotion/benchmark-ebb19bcbbec14f839703d00c53582936/calibrated-benchmark.json`.

Fresh .NET development restore/Release build/test: **520/520 PASS**, failed/skipped 0, errors/warnings 0; `TestResults/MeshyUserMotion/DotNet/meshy-user-motion-development.trx`. Full real Unity EditMode: **781/781 PASS**, failed/skipped 0, process exit 0; `TestResults/MeshyUserMotion/unity-tests.xml`, completed 8 October 00:51:59 Istanbul time. This includes seven new motion-selection regressions and the latest lifecycle regressions. These runs started at 119b56f with uncommitted changes and are not new final-SHA acceptance. New Walking/Running, new turn/parry/combo clips, continuous foot sliding, LOD2 artifact repair and full ProductionArt acceptance remain outside these completed motion captures.

Eight selected screenshots were copied byte-for-byte into `Docs/Evidence/Implementation14C/MeshyUserMotion/`: idle front/quarter, sword windup/slash/side, mounted side/quarter/walk. These are actual Windows render output from the development runs above, not Inspector images, retouched concepts or final-SHA acceptance captures. The horse remains unchanged; visual coat/horse intersections and open-handed grip prevent a final mounted/art claim.

## Next safe experiment

The subsequent user-approved sword grip and saddle-fit work is recorded in [Grip and mounted fitting](IMPLEMENTATION_14C_GRIP_AND_MOUNTED_FIT.md). R4 hand shape and sword alignment are now accepted; the current priority is seated rider/eyer fit. Preserve that newer decision when continuing this older intake record.

Continue the calibrated pilot, assess the remaining grip/coat clipping limitations, and validate additional selected motions individually. Do not restart RAM closure, request another character/export, replace the retained rig or activate all historical production profiles. Save remains v14.

The separate `MeshyUserMotionIntake` selects only the two pinned donors. Executed Unity entry points: `RunImport`, `RunAudit`, `RunContactCandidates`, then `MeshyHasanPilotBuild.RunUserMotion`. Player flags: `--meshy-user-motion true --meshy-user-contact true --meshy-worktree DIRTY_DEVELOPMENT --meshy-output <fresh directory> --meshy-sha <HEAD>`. Mounted mode uses `--meshy-mounted-review true`; benchmark mode uses `--meshy-calibrated-benchmark true --meshy-user-motion-benchmark true`. Do not run the generic Quaternius intake blindly on all fourteen exports.

Public distribution follows the [Meshy license audit](IMPLEMENTATION_14C_MESHY_LICENSE_AUDIT.txt), rechecked live on 7 October. Raw source remains local/ignored; a standalone general-purpose motion-library license is not assumed. No character or second export is requested.
