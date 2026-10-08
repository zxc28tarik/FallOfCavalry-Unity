# Implementation 14C: accepted sword grip and mounted seat fitting

Updated 8 October 2026. The user accepted the R4 hand corrective ("bu okey bence") and then explicitly closed further sword iteration ("kılıç tamam atı da hallet"). The subsequent priority is the **saddle and seated rider**, not additional rein polish. Preserve the accepted hand shape and weapon alignment.

This is the isolated Meshy Hasan presentation pilot. Production catalog activation, full 14C art acceptance and Implementation 15 remain separate. Save schema stays v14. No gameplay, character source FBX, skeleton, skin weights, clothing or texture changes are included.

## Accepted grip

`MeshyKilicGripCorrectivePipeline` creates three derived mesh assets and an explicitly selected candidate prefab. Original base vertices, topology, bind poses and bone weights remain intact; `Grip_Kilic_R` is an additive right-hand-only blend shape. `MeshyRightHandGripVisual` closes it only while the review kilic is equipped and resets it before pool return. The user's R4 visual acceptance supersedes further finger sculpting or weapon-direction experiments in this pass. It is not a claim of anatomically perfect close-up fingers.

`MeshyKilicGripAttachment` aligns the retained weapon's handle anchor to the actual RightHand descendant socket. Immediate review reuse now disables and detaches the old weapon before deferred player destruction, preventing duplicate weapon paths during calibrated Avatar restoration. EditMode cleanup uses immediate destruction; the player retains deferred destruction.

Protected original source hashes:

- `Hasan_Meshy.fbx`: `356dd92317ec1787f48aabe4e85157d31dfec2dd029168c856c5ffd6a125fadf`
- `CHR_HasanAga_MeshyPilot.prefab`: `3720d6f8921f6a239e33c1a927a0571fbada0a180de3a3d4e20aee00a271eb0d`

## Saddle fitting

The previous fitter placed the **internal Hips joint** exactly at `Socket_Rider`, treating the joint as the seated skin contact. The saddle rim then crossed Hasan's belt/haunch region. Simply lifting the joint by 12 cm made the original long stirrups unreachable and straightened the knees; that trial was rejected.

The selected fitting uses a 10 cm Hips-to-seat reference offset and shortens the retained stirrup leathers by 14 cm. The existing two stirrup irons move upward by the same amount. Foot targets use those actual adjusted anchors, allowing the knees to bend while the feet reach the irons. These are explicit presentation fitting values for this character/horse pair, not campaign position or character-height changes. Measurements report both the nonzero Hips-to-seat distance and error from the intended Hips target; a zero joint-to-seat distance is no longer presented as successful seating.

`MeshyMountedTackBinding` makes disposable copies of the existing three tack meshes. It identifies the two stirrup leathers as connected components, retaining original topology, UVs and authored normals. Saddle, fitted blanket and girth vertices stay in place. Only the leather straps and stirrup iron positions change. Original horse and tack assets stay unchanged.

The saddle follows `MountSpine`. The obsolete rigid reins are removed only from the leather submesh (96 triangles per retained LOD); the blanket and iron submeshes are preserved. Hand-to-bit reins and head straps use the retained leather material and follow the hands/horse head. These accessory connections are subordinate to the seated-fit work; they do not add a finger rig or change the horse skeleton.

The actual Windows review uses the retained horse Idle, Walk and Gallop clips, the calibrated Meshy Avatar and the existing seated Humanoid clip. Presentation limb fitting follows real Mecanim sampling. It checks exactly one active saddle, pelvis-target error below 1 mm, stirrup-anchor errors below 1 cm and stability across the render boundary. Numerical checks support visual review and do not alone establish art acceptance.

## Camera-Aware Visual Acceptance

The user's tactical-camera addendum is authoritative: normal tactical camera first, reasonable Hasan inspection second, extreme close-up diagnostic only. The review uses the repository battle camera position `(0,18,-28)`, rotation `(28,0,0)`, FOV 60. It is an isolated real Windows render scene using that camera, not the campaign battle UI.

- Sword grip: accepted by the user; preserve R4. Minor stable finger/handle overlap is `ACCEPTABLE_MICRO_CLIPPING`.
- Mounted seat: assess side and three-quarter captures for saddle rim below belt, seated haunches, bent knees and feet near the adjusted irons. Reject visible hovering, a buried pelvis or duplicate saddles.
- Small coat/saddle and boot/iron overlap: `ACCEPTABLE_MICRO_CLIPPING` when stable and unobtrusive in the normal view.
- Open individual fingers and coarse glove/hand detail in extreme close-up: `DEBUG_ONLY_ARTIFACT` for this pilot. No final narrative hand-animation claim.
- Any floating saddle, torso/horse burial or detached rider visible at normal distance remains `BLOCKER_AT_TACTICAL_DISTANCE`; a gross defect visible on Hasan inspection remains `BLOCKER_AT_CHARACTER_INSPECTION_DISTANCE`.

## Reproduction and evidence discipline

Build with Unity 6000.3.16f1, `-batchmode -nographics -projectPath <UnityProject> -executeMethod FOC.Editor.Visuals.MeshyHasanPilotBuild.RunUserMotion -logFile <fresh log> -quit`.

Run `Artifacts/MeshyUserMotionPlayer/FallOfCavalry-MeshyUserMotion.exe` with:

```text
--meshy-mounted-review true --meshy-grip-candidate true --meshy-camera-aware true
--meshy-output <fresh output directory> --meshy-sha <actual HEAD>
--meshy-worktree <actual clean/dirty state> -logFile <fresh player log>
```

Mounted output includes `mounted-evidence.json`, normal tactical idle/motion/gallop, side and three-quarter inspection and diagnostic saddle captures. Grip review uses `--meshy-grip-acceptance true --meshy-grip-candidate true`. Equipped 1/12/100 benchmarks use `--meshy-calibrated-benchmark true --meshy-user-motion-benchmark true --meshy-grip-candidate true` through the real assembler/cache/pool path.

Development trials under `TestResults/MeshyGripCorrective/` are explicitly dirty-worktree evidence and must not be relabeled as final-SHA evidence. `MountedSeat12` is the rejected long-stirrup trial. `MountedFitFinalCandidate` demonstrates the revised seating and stirrup positions; its subsequent normal-preservation fix must be included in final captures.

The closure manifest and final captures are written under `TestResults/MeshyGripCorrective/Final/<commit>/`. Record actual .NET, Unity, pipeline, player, benchmark and GitHub results there. Do not infer full 14C READY from accepted grip/seating or a green .NET-only GitHub workflow. ProductionArt proof dependency checks stay unchanged.
