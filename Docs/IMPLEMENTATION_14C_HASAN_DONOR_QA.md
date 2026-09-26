# Implementation 14C — Hasan MakeHuman donor review

**STATUS: NOT READY. Hasan not accepted. Do not advance to Sipahi or Implementation 15.**

2026-09-26. Donor trial checkpoint: `3889797a4af1b2a01dcf17da1c6a3e1792b239fc`.
The checkpoint was pushed, remote equality verified, and the worktree was clean
before adaptation. Old rejected-experiment stash was preserved.

## Work completed

Four donor adaptation revisions were evaluated with real Windows-player frames.
The final r4 uses the pinned Viking-tunic topology (not its belt/buckle/styling),
harem trousers and male boots, with the unchanged human base/rig and existing
licensed Hasan head/hair. The monk robe remains a tested alternative inside the
selected family, not the chosen r4 mesh. No additional source search or paid
purchase, no Quaternius adaptation, no new procedural tunic.

Historical construction research, limitations and revision details are in
`IMPLEMENTATION_14C_HASAN_DONOR_TRANSFORMATION.md`. Original upstream sources and
archive hashes are preserved in the provenance ledger.

The adapted character has 62,993 / 25,797 / 11,020 triangles across three LODs,
one consolidated skinned renderer per active LOD and the same canonical
Humanoid avatar. Counts are asset diagnostics, **not runtime performance**.

## Real motion and visual review

All 13 final review frames are under
`Docs/Evidence/Implementation14C/Drafts/HasanDonor/`.
They are actual Windows/D3D11 renders, not inspector shots, mockups or retouched
images. The BMP-to-PNG step is lossless encoding only. No Accepted folder.

| Frames | Action / actual observation | Acceptance |
| --- | --- | --- |
| 01–03 | Front/side/three-quarter idle. Better continuous shoulders; inner-sleeve clashes fixed. Garment still reads too generic; plain layered short-over-long sleeve and closure detail do not establish the required Ottoman identity. | FAIL |
| 04–05 | Two walking phases. Real leg/arm motion, no sphere/box body. Skirt rides with the thigh like a stiff split shell; hems/layers remain crude. | FAIL |
| 06 | Run. Shoulder/collar volume deforms unnaturally when the arm rises; drape and upper torso need corrective authoring. | FAIL |
| 07 | Turn. Full visible body; front overlap survives but does not resolve historical tailoring quality. | NOT ACCEPTED |
| 08–09 | One-handed attack phases with the existing kilic socket attachment. Hand remains open, hilt placement/grasp is not credible. | FAIL |
| 10 | Arm raise. No exploded mesh, but collar/upper-chest shape and shoulder transition are not production-quality deformation. | FAIL |
| 11 | Crouch. Severe r2 trouser-through-coat issue reduced by refitting edited-skirt weights. R4 still has rigid coat folds and weak neck/collar presentation. | FAIL |
| 12–13 | Mounted side/three-quarter on unchanged horse and tack. Rider remains separate. Near-straight leg, seat presentation, stirrup relationship and open hands/reins need a coordinated mounted-pose/tack pass. | FAIL |

The player runs eight actual Animator states for two seconds and requires
measured joint travel before capturing a repeatable phase. This is much more
than the previous static IK image, but these are **diagnostic transform-curve
clips**, not accepted production locomotion/combat animation. A temporary
Generic avatar plays them on the same skeleton; the saved prefab remains
Humanoid-compatible. Shared production-animation acceptance is still pending.

Initial runtime motion correctly failed with zero travel under Humanoid
retargeting. This was fixed in the review instance. Two new stance-foot tests
initially used the same invalid Humanoid sampling path; they now execute the
same Generic Animator path as the real player and retain the strict 5 mm foot
target assertion. Focused tests: 14 passed, 0 failed, 0 skipped (pre-final SHA).
Final-SHA suites must be rerun; precommit results do not substitute for them.

## Exact blocker classification

- **Historical transformation:** construction changed substantially, but the
  result still lacks convincing 1648 Ottoman tailoring, closure/layer detail
  and character-specific visual identity.
- **Weight transfer / rig deformation:** edited skirt movement is too rigid;
  shoulder/collar deformation and the grip require further authoring. The
  existing 18-bone rig has no articulated finger chain; a usable weapon-grasp
  solution has not been authored in this package.
- **Mounted fit:** pose, saddle/seat reading, stirrup and reins/grip are not
  accepted together. The horse was not rebuilt or falsely marked accepted.
- **Material quality / hair presentation:** current surfaces and facial hair
  remain draft quality. Per requested order, no material polish campaign was
  used to conceal failed geometry/fit.
- **Donor topology / human base:** not rejected as intrinsically unsuitable.
  The continuous donor shoulders are useful and the current base is unchanged.
  The failure is this adaptation/animation result, not a claim that MakeHuman
  can never work. No alternate strategy has been selected unilaterally.

## Gates deliberately left closed

Hasan has not passed, therefore no new Sipahi/Cebeli/Tufekci derivative, armor
refinement, horse rebuild, catalog activation, Accepted capture set, tactical
production screenshot, or 100/250/500 and mixed-cavalry benchmark was produced.
ProductionArt rules/catalog are unchanged; the known 130 proof-dependency issues
must still fail until actual accepted replacements exist.

Persistent loadout and gameplay/save contracts are untouched (save v14). Review
socket attachment is **not** evidence of the production loadout/assembler path.

## Reproducible commands

From repository root, discovered tools (not new installations):

```powershell
& 'Artifacts/DccTools/blender-4.5.9-windows-x64/blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Art/adapt_hasan_donors.py
# Authoring import (before freezing/committing the source state):
& 'C:/Users/zxc28/AppData/Local/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe' -batchmode -nographics -projectPath UnityProject -executeMethod FOC.Editor.Visuals.HasanDonorPipeline.Run -logFile TestResults/HasanDonor/import.log
# Clean final-SHA validation:
pwsh -NoProfile -File Tools/Test-HasanDonorClosure.ps1 -UnityEditor 'C:/Users/zxc28/AppData/Local/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe'
pwsh -NoProfile -File Tools/Test-14CExistingPipelines.ps1 -UnityEditor 'C:/Users/zxc28/AppData/Local/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe'
```

The final closure runner records exact commands, exit codes, SHA, EditMode XML,
fresh player motion captures and unchanged worktree in
`TestResults/HasanDonor/Final/validation.json`. An exit code 1 caused by the
unchanged ProductionArt rejection is expected and is **not** a successful art
closure. The previous-pipeline manifest is
`TestResults/14c-existing-pipelines.json`. Final report must separately verify
CI on the exact final SHA; Foundation CI does not run licensed Unity tests.

Next: continue 14C under the frozen MakeHuman pipeline. Do not start 15 or
propagate this unaccepted Hasan result to other profiles.
