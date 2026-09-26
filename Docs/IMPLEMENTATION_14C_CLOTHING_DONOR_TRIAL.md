# 14C — selected zero-cost clothing donor trial

Date: 2026-09-26. **Implementation 14C: NOT READY.**

The operator-selected source intake and first comparative tests are complete.
The requested historical transformation / production clothing package is NOT complete.
No alternative-source search, paid Source purchase, new procedural tunic, body A/B,
gameplay change, production catalog activation, or Implementation 15 was performed.

## Repository and persistence

- Branch: `codex/impl-14c-production-character-art`.
- Starting / unchanged HEAD: `de12391009194c215dd02a994b75fa7c64ae6009`.
- Worktree was clean before this trial. Trial changes are local, uncommitted.
- No push or new CI run for these changes. The pre-existing HEAD's CI run
  36187220358 is now verified completed/success; it does NOT validate this trial.
- Save schema remains v14. Domain/runtime contracts and the production catalog
  were not edited. Original source files, fitting tools, draft assets and evidence
  are retained locally in this repository; downloaded ZIPs remain in ignored Artifacts.
- Superseded first-pass `.001` donor outputs were moved (not deleted) to
  `Artifacts/ClothingDonorSuperseded`. Original rejected-art stash was not touched.

## Sources and transformation authority

See `ArtSource/HistoricalSlice/Upstream/ClothingDonors/PROVENANCE.md` for exact
upstream URLs, four archive SHA256 values, selected filenames, and embedded licenses.
The free Standard pack contained all seven requested Quaternius files. Each of the
four requested MakeHuman garments also independently declares CC0 in its binding file.

These are topology/deformation donors, never historical evidence. The monk cape and
rope, Viking trim/cut, fantasy accessories, modern-looking buckles and source styling
must not survive as unsupported Ottoman claims. Historical direction remains in
`IMPLEMENTATION_14C_ZERO_BUDGET_SPEC.md`; its earlier procedural authoring method is
superseded by the operator's donor-first instruction.

## Work actually performed

1. Downloaded only the four selected free archives; audited licenses and pinned hashes.
2. Imported all seven FBX files in Blender 4.5.9 LTS; checked mesh/UV/rig/weights.
3. Fitted all four MakeHuman `.mhclo` garments to the existing morphed hm08 anatomy.
   Their original meshes/UVs were retained; no upstream garment rig weights were
   supplied, so weights were transferred from the existing anatomical body's rig.
4. Remapped Quaternius source weights and bind limbs to the unchanged canonical rig.
   This first fitting algorithm is visibly insufficient; see failures below.
5. Exported all eleven as clearly named `CLTH_Donor_*` DRAFT sources with three LODs.
6. Created six RAW comparison assemblies using existing body/head plus donor clothes.
   Body masking was intentionally NOT used to hide fit errors. Monochrome test cloth
   is diagnostic, not new production material/texture work.
7. Imported in Unity 6000.3.16f1, validated humanoid avatars, baked three LOD meshes
   for bind/standing/mounted poses, and sampled the existing shared animation fixtures.
8. Built a real Windows player and captured six combinations standing and mounted.
   All 12 image captures returned exit 0; they are actual render output, not mockups.
9. Added 15 regression tests for exact selection, draft status, rig preservation,
   correct Ranger main-mesh selection, and no experiment activation in production.

An intake-tool bug initially selected an arbitrary mesh from multi-mesh Ranger FBXs.
It was fixed to require the exact requested mesh name; fantasy bracer and optional belt
meshes are intentionally excluded from fitting, while preserved in original FBX sources.
Two exact triangle-count regressions cover this error. Old evidence is superseded.

## Per-donor outcome

All rows below passed numeric fit, UV/weight, Unity import/avatar/mesh-bake tests.
None is visually production accepted. Counts are actual LOD0 / LOD1 / LOD2 triangles.

| Donor | Triangles | Visual assessment / next work |
|---|---|---|
| Male_Peasant_Body | 3856 / 1927 / 848 | Severe torso penetration in first transfer. Correct depth/shoulder fitting before historical reshaping. |
| Male_Peasant_Arms | 5338 / 2669 / 1173 | Source bare arms/hands overlap the selected FOC body. Separate sleeve geometry from donor anatomy and fix fit. |
| Male_Peasant_Legs | 1112 / 556 / 244 | Hip/thigh penetration; not rejected as a donor, but current adaptation fails. |
| Male_Ranger_Body | 2998 / 1499 / 658 | Main garment now selected correctly; severe torso penetration remains. |
| Male_Ranger_Arms | 4928 / 2464 / 1084 | Main arms retained, auxiliary fantasy bracer excluded; overlap/double hands need cleanup. |
| Male_Ranger_Legs | 1128 / 563 / 247 | Hip/thigh fit fails current body. |
| Male_Ranger_Feet_Boots | 9172 / 4586 / 2016 | Useful boot topology; shin/pants intersection and fantasy buckle styling need adaptation. |
| toigo_harem_pants | 10912 / 5455 / 2400 | Useful loose silhouette, better fit than initial Quaternius legs; waistband and boot tuck intersect. |
| culturalibre_male_boots | 30768 / 15384 / 6768 | Useful foot/shaft form; excessive initial triangle cost and pants intersections need work. |
| donitz_monk_robe | 19820 / 9909 / 4359 | Promising Hasan topology, but cape/rope/long closed skirt remain explicitly unacceptable. Boot penetration and mounted skirt need correction. |
| rehmanpolanski_viking_tunic | 6568 / 3283 / 1443 | Best initial short-garment fit in these images; still generic/Viking styling, not an Ottoman final outfit. |

These observations evaluate OUR current fitting outputs, not an assertion that the
upstream donors are defective or unusable. No donor was replaced with an unselected source.

## Evidence and exact executed commands

Durable draft evidence: `Docs/Evidence/Implementation14C/Drafts/ClothingDonors/`:
12 PNGs, source mesh audit, fit results, Unity diagnostics and capture return-code report.
Detailed logs/XML/TRX: `TestResults/ClothingDonors/` (local ignored evidence).

From repository root, executed:

```powershell
& Artifacts/DccTools/blender-4.5.9-windows-x64/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Art/audit_clothing_donors.py
& Artifacts/DccTools/blender-4.5.9-windows-x64/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools/Art/test_clothing_donors.py
pwsh -NoProfile -File Tools/Art/Test-ClothingDonors.ps1
dotnet restore FallOfCavalry.sln
dotnet build FallOfCavalry.sln -c Release --no-restore
dotnet test FallOfCavalry.sln -c Release --no-build --logger 'trx;LogFileName=clothing-donors.trx' --results-directory TestResults/ClothingDonors/dotnet
```

The wrapper initially had a verified local Editor default; it now requires explicit
`-UnityEditor 'C:\Users\zxc28\AppData\Local\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'`
for portability. It executed these actual Unity argument sequences through hidden processes:

```text
-batchmode -nographics -projectPath "C:\Users\zxc28\Documents\ChatGPT\FOC/UnityProject" -executeMethod FOC.Editor.Visuals.ClothingDonorValidation.Run -logFile "C:\Users\zxc28\Documents\ChatGPT\FOC\TestResults\ClothingDonors\unity-donor.log"
-batchmode -nographics -projectPath "C:\Users\zxc28\Documents\ChatGPT\FOC/UnityProject" -executeMethod FOC.Editor.Visuals.HistoricalArtReviewBuild.RunDonors -logFile "C:\Users\zxc28\Documents\ChatGPT\FOC\TestResults\ClothingDonors\unity-donor-build.log"
```

Full EditMode suite was separately executed with that same Unity.exe:

```text
-batchmode -nographics -projectPath "C:\Users\zxc28\Documents\ChatGPT\FOC/UnityProject" -runTests -testPlatform EditMode -testResults "C:\Users\zxc28\Documents\ChatGPT\FOC/TestResults/ClothingDonors/editmode.xml" -logFile "C:\Users\zxc28\Documents\ChatGPT\FOC/TestResults/ClothingDonors/editmode.log"
```

Results on the local trial worktree:

- .NET restore and Release build PASS, 0 warnings / 0 errors; tests 486 passed / 0 failed / 0 skipped.
- Unity EditMode: **560 passed / 0 failed / 0 skipped**, process exit 0.
- Unity donor import / diagnostic batch: exit 0; 11 assets, 3 LODs each, 3 baked poses
  and 60 shared-clip samples per donor (660 samples total).
- Windows diagnostic build: exit 0; 12/12 capture processes exit 0.
- Visual QA: FAIL for production use. All six combinations were inspected.
- No new final-SHA CI, full production-actor benchmark, or production art gate closure.

CRITICAL: Existing shared human clips are simple pelvis-motion architecture fixtures.
Sampling them does NOT prove real locomotion deformation. Mounted images use diagnostic
IK, not full mounted animation. Numeric PASS is not visual PASS.

## Remaining required work / handoff

Continue with THESE donors, no alternative shopping or new procedural tunic shortcut.
First correct Quaternius bind/body depth and duplicate anatomy. Adapt Hasan's donor
robe by removing cape/rope, making researched front closure and riding opening, and
fitting trousers/boots. Preserve useful source topology/UVs/weights. Then create
appropriate material/texture differentiation and inspect in actual player again.
Propagate only after visual improvement is demonstrated. Full motion, loadout,
production catalog, clipping, performance and final-SHA acceptance remain pending.

No READY claim; no Implementation 15.
