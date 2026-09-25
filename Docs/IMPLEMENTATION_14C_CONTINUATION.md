# Implementation 14C continuation — NOT READY

## Authority and starting state

Same repository `zxc28tarik/FallOfCavalry-Unity`, same branch
`codex/impl-14c-production-character-art`, continuation base
`9bcae4638a36d1cb6484eec3de363c18c9c9b372`. Fetch, branch, HEAD, clean worktree,
remote branch and Foundation CI 36168217398 were independently verified before
editing. The latest user continuation prompt is authoritative. No Implementation
15, gameplay/balance changes, architecture rewrite, save change or migration.

This is a **repair checkpoint**, not closure of the requested acceptance package.
All new art retains Draft status. Previous failed visual findings are not waived.

## Repairs and defects actually found

- Replaced independently lofted shoulder/sleeve pieces with a connected anatomical
  cloth surface. The skirt extends the actual waist boundary. Tests check open
  shoulder edges, welded waist edges and accidental long cuff-to-hem bridges.
- Replaced offset bare-foot anatomy with closed-toe boots: sole, toe box, instep,
  ankle and shaft. Trousers tuck into boots and under the coat instead of cutting
  through the waistband. This does not establish final historical footwear quality.
- Added UV-compatible skin and fitted hair-card sources from the official
  MakeHuman CC0 package. Original short facial fibers replace the inflated beard
  shell. See `ArtSource/HistoricalSlice/Upstream/SystemAssets/PROVENANCE.md`.
- The standing deformation diagnostic exposed an actual skinning bug: source
  **metacarpal** weights fell through to the Head mapping. Corrected to Hand and
  added a specific regression; normalized weights alone had not detected it.
- Added metric mail UVs, deterministic original albedo/normal surface studies and
  distinct surface responses. These are studies, not completed production materials.
- Reworked the matchlock from flat block stock to faceted stock, long barrel,
  retaining bands, ramrod, pan, serpentine, match, trigger lever and rear sight.
  Added shield rear grip/forearm strap. Period-reference limits remain documented.
- The Rider socket and old tack heights were inside the licensed horse's actual
  back. Moved the socket and ray-fitted the blanket/girth to that surface. Added
  real-player mounted diagnostic poses. Final rider/boot/stirrup/bridle/gait fit
  is still NOT accepted; a static IK study is not a runtime animation system.
- Fixed review-build snapshot restoration after Windows ERROR_USER_MAPPED_FILE
  (1224). Restore by atomic replacement rather than truncating a mapped settings
  file; an actual memory-mapping regression test covers it.
- Fixed nondeterministic proof-prefab internal fileID churn caused by three
  identically named Silhouette children. Children now have unique LOD-qualified
  names. Fixture geometry remains proof geometry, not production art. Repeated
  regeneration is tested; the runtime catalog remains untouched.
- Batch Battle Pipeline now builds its diagnostic scene in memory, without
  rewriting the versioned BattleProof fixture. The explicit authoring menu still
  saves it. A regression checks byte preservation; no battle semantics changed.
- Blender invocation now uses `--python-exit-code 1`: Blender otherwise returned
  zero even when the Python authoring script raised an exception. A failed blanket
  ray fit was detected and corrected, not reported as a successful export.

## Evidence and honest visual review

Nine real, unretouched Windows review-player images are versioned under
`Docs/Evidence/Implementation14C/Drafts/Continuation/`. They include a standing
deformation study and mounted side/three-quarter fit studies in addition to the
six existing categories. These are **draft/diagnostic images**, not any of the ten
mandatory Accepted images. There is deliberately no Accepted directory.

The studies use an isolated review scene, not the actual slice's persistent-loadout
assembler. Static diagnostic IK does not prove Walk/Run/mounted attack compatibility.
Production profile/equipment mappings must not be switched on the strength of these
images or the geometry tests. The final user-visible report must retain NOT READY.

Remaining visual findings include unfinished cloth drape and collar/hem treatment,
rough facial fibers, insufficiently distinct historical outfits, missing assembled
weapons/hand poses and headgear fit, incomplete saddle contact/stirrup/rein/bridle
acceptance, one horse coat, and unverified natural gait/contact phases. The current
human remains a generic clothing study, not an accepted Hasan/Sipahi/Cebeli/Tufekci.

## Runtime and performance status

- Production runtime catalog unchanged: **23 proof asset entries**, proof runtime
  consolidated/crowd fallbacks, no distinct activated `hasan-aga` profile. Existing
  historical loadouts still resolve to proof visuals. Visible proof primitive
  dependencies are still present. The dependency gate must continue to fail.
- No hardcoded production equipment assignment, gameplay/presentation conflation,
  new save state or gameplay change has been introduced.
- Three LODs remain. Current Hasan draft triangles: 39,170 / 15,059 / 6,042;
  horse: 14,986 / 7,788 / 3,440. Facial fibers explicitly thin at distance rather
  than retaining disconnected tiny triangles at every LOD.
- One SkinnedMeshRenderer per active human/mount LOD is retained. Material count
  and atlas work are not closed. This is not a claim of acceptable 500-actor cost.
- The existing draft-prefab benchmark remains a diagnostic only. Full
  SoldierInstance/loadout/profile -> assembler -> cache/pool benchmarks at
  100/250/500 and a mixed cavalry scenario remain NOT RUN.

## Verification protocol

Baseline .NET: 486. Baseline Unity: 522. This continuation adds 19 Unity test cases.
Do not infer passing counts from those numbers. Post-commit evidence is produced by:

```powershell
.\Tools\Test-14CExistingPipelines.ps1 -UnityEditor '<verified Unity 6000.3.16f1 path>'
.\Tools\Test-HistoricalArtDrafts.ps1 -UnityEditor '<verified Unity 6000.3.16f1 path>'
.\Tools\Test-ProductionArt.ps1 -UnityEditor '<verified Unity 6000.3.16f1 path>'
```

The first invokes every existing pipeline and the actual Windows Development
build, preserving script filters/exit codes, with hidden helpers. The second runs
restore/Release build/all .NET tests, all Unity EditMode tests, review build,
draft benchmark and nine actual player captures, then the production gate. It
requires a clean starting and ending worktree. Its expected NOT READY is not
converted to PASS because its other steps succeed.

Exact final SHA/commands/return codes are recorded after committing in local
`TestResults/14c-existing-pipelines.json`, `14c-final-validation.json`, test XML/TRX
and the delivery report. A new commit requires rerunning evidence. CI must be
checked against that same final SHA; the base run above is not final evidence.

## Open acceptance gates

1. Finish visual quality of the four distinct historical profiles and equipment.
2. Finish mounted fit, coat variants, shared human animations and natural horse gaits.
3. Only then activate production-only mappings and validate real persistent loadouts.
4. Produce ten accepted committed runtime images, both mounted fit angles and at
   least one real slice/gameplay-context image.
5. Benchmark full pooled/cached actors and mixed cavalry at 100/250/500.
6. Produce an actual slice Windows build with activated production art, not only
   a review-player build or a baseline build still using proof.
7. ProductionArt must return zero issues, visual QA must also pass, all final-SHA
   tests/CI must pass with no critical skip, and worktree must be clean.

Save v14 unchanged. Next package: **continue Implementation 14C**. No green light
for Implementation 15 — Story / Publisher Flow.
