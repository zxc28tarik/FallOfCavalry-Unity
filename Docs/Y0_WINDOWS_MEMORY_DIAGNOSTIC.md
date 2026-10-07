# Y0 Windows UI and save memory diagnosis

## Scope and status

Y0 is the first stability package in [the playable-screen plan](PLAYABLE_SCREEN_PROGRESSION_PLAN.md). It is not a new gameplay implementation or a character-art closure. Save remains v14; Implementation 15 has not started. Starting code commit: `537805b8556d5f20f2f097e8f42c427d63b76ac7`, branch `codex/impl-14c-production-character-art`.

This document records the change and development evidence, not final acceptance. A clean final-commit short flow and complete two-hour Windows soak are required. Development or collection-assisted diagnostics are not substitutes. The launcher's SHA-scoped manifests under `TestResults/WindowsPlayerSoak/<SHA>/<run-id>/` are the authority for each actual run; `RUNNING` is not acceptance.

## Observed failure

The original two-hour attempt on the starting commit stopped after 882.006 seconds and 152 save/load cycles with `MANAGED_GROWTH_OVER_64_MIB`. Managed growth was 68,341,760 bytes, Unity native growth 4,983,728 bytes and attached UI elements remained 244. Peak process working set was 303,783,936 bytes against a warm 274,661,376 bytes. Normal player-save snapshots were unchanged. The attempt failed; it did not complete two hours.

Evidence: `TestResults/WindowsPlayerSoak/537805b8556d5f20f2f097e8f42c427d63b76ac7/cd24175b7f8746a792a820cb6405eb78/manifest.json`. TestResults is local, ignored evidence, not a public repository screenshot or artifact.

## Read-only lifecycle review

`PresentationRuntimeHost.Configure` releases its old controller, view model and navigator before reconstructing the document. Controller disposal detaches navigation and root callbacks. Bootstrap save/load callbacks already unbind before rebinding. The save-store slot lock uses a single unchanged root/slot during this workload. These observations do not conclusively exclude retention in another object type.

Weak lifetime instrumentation now tracks retired campaign graphs and visible field lists, capped at 512 weak entries per type. It is diagnostic and does not introduce a strong owner of old state. The raw allocated-memory samples include uncollected garbage as well as live objects, as explained in [Unity's official counter documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.Profiler.GetMonoUsedSizeLong.html).

## Development experiments

All experiments below use a dirty worktree based on the starting commit. They are not validation of a new final SHA.

| Run | Actual result | Observation |
| --- | --- | --- |
| `111d3cb37e364d1aa4edd66cd98177b1`, requested 180 seconds | `DEVELOPMENT_PASS`, exit 0; 182.27 seconds, 31 cycles | No forced collection. Four retired campaigns remained alive out of 31 tracked; 16 field lists out of 124. Last raw managed size 38,998,016 bytes. |
| `606060386b2443c7969e0d4645ea5d51`, requested 300 seconds, explicit memory audit | Overall launcher `FAIL`, exit 1; player reported `DIAGNOSTIC_COMPLETE`, 301.705 seconds, 51 cycles | The initial diagnostic completion exit-code bug caused launcher failure and was subsequently corrected. Raw managed size was 45,944,832 bytes. Explicitly collected size was 7,016,448 bytes after warmup and 9,306,112 bytes at shutdown. This run is not a PASS. |
| `6161e99c2dc64563bd8b6c32192c2b73`, requested 1200 seconds | `DEVELOPMENT_PASS`, exit 0; 1202.456 seconds, 208 cycles | Unchanged memory limits after the existing idle interval, without forced collection. Managed growth 38,633,472 bytes; native growth 4,912,416 bytes. Attached UI remained 244. Normal saves unchanged. |
| `88c7a7315c894415917bbeb50a6a5805`, requested 30 seconds, explicit memory audit | `DIAGNOSTIC_COMPLETE`, exit 0; 30.289 seconds, five cycles | Confirms the diagnostic completion exit-code repair and its separate non-PASS classification. Normal saves unchanged. One retired campaign and four tracked field lists remained alive after final collection. |

The five-minute collected-size difference supports the inference that much of the raw memory sample was transient garbage. It does not establish a zero-leak result or justify raising the acceptance threshold.

The twenty-minute run completed 2,704 navigation checks, 51 travel checks, two rejection checks and the disabled-bootstrap lifecycle check. Its sampled managed peak was 76,345,344 bytes; six of 208 retired campaigns and 20 of 512 tracked field lists remained alive at the last sample. Counts are bounded-window diagnostics, not proof about every historical object. The p95 wall-clock frame interval was 37 ms and the maximum 730.714 ms; these are workload measurements, not release or GPU performance certification. Some development test jobs ran concurrently, so this experiment is not an isolated performance benchmark.

## Instrumentation and sampling changes

The ordinary acceptance workload keeps its UI routes, save/load reconstruction, fingerprints, travel limits, five-second idle duration and resource guards. Sampling moves from immediately after reconstruction to the end of that same idle interval; warm and later measurements use the same phase. The guard still checks the current raw sample against the original 64 MiB growth limit, not a lower minimum or collection-assisted measurement. Busy-phase peaks remain observable.

Failure reports now retain current raw/Mono counters and GC counts before a guard throws. Retired-object counts and idle minima accompany the samples. A separate `-MemoryAudit` diagnostic explicitly collects garbage only after warmup and at shutdown, reports `DIAGNOSTIC_COMPLETE` instead of acceptance `PASS`, and cannot hide a guard failure. The launcher preserves the raw player report even on failure.

Regression tests cover diagnostic/acceptance outcome separation, failure preservation, bounded weak references and current memory measurements on a guard failure. Exact final-commit test results will be recorded only after they have actually run.

Development verification: .NET restore/build succeeded with zero build warnings/errors; 514/514 tests passed, zero failed/skipped (`Build/TestResults/y0-development.trx`). Unity 6000.3.16f1 full EditMode completed with 761/761 passed, zero failed/skipped (`TestResults/unity-editmode.xml`). After strengthening the raw-sample-versus-idle-minimum assertion, the focused Bootstrap suite completed again with 20/20 passed, zero failed/skipped (`TestResults/y0-bootstrap-diagnostic.xml`). These are pre-commit runs, not final SHA evidence. An earlier full Unity launch was rejected while the same project was being built; it ran no tests and is not counted as a PASS.

## Clean `119b56f` follow-up and allocation candidate — 7 October 2026

The normal two-hour attempt on clean `119b56f757b901ff8e2e37f26d1b8336295006b1` failed after **833.568 seconds / 145 cycles**, not two hours. The failure was `MANAGED_GROWTH_OVER_64_MIB`: raw managed growth 68,214,784 bytes, native growth 5,659,176 bytes and attached UI elements 244. Normal player saves were unchanged. Evidence: `TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/72028f2255924482af21e119c96186e4/manifest.json`. Its earlier short-flow, test-suite and CI passes do not override this failure. Y0 remains NOT READY.

A separate clean-SHA `-MemoryAudit` run requested 900 seconds and completed **905.227 seconds / 157 cycles**, exit 0, status **DIAGNOSTIC_COMPLETE**, not PASS. The warm collected managed size was 7,172,096 bytes; final collected size 11,837,440 bytes. The final raw sample was 47,218,688 bytes and raw peak 70,729,728 bytes. After the explicitly requested final collection, tracked retired campaigns and field lists were both zero. Native growth was 5,542,568 bytes and attached UI remained 244. Evidence: `TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/f975cc91aae641c29c8836da8062f884/manifest.json`. Normal saves were unchanged. Collection reclaimed the tracked retired graphs; the collected-size increase still prevents a zero-leak claim. Concurrent .NET diagnostic work also prevents treating its frame timings as an isolated benchmark.

### Minimum allocation change

`CampaignSaveService` now uses an operation-local, two-entry exact-content validation cache. Atomic storage still reads the temporary, previous and committed disk generations and invokes validation for each. Byte-identical strings share deserialization/invariant validation only within that single call; changed bytes are independently decoded. Each later load produces a fresh DTO. Migration and post-migration validation remain in place. There is no service-wide cache, hash-only equality, file-validation bypass, save-format/schema change, forced collection or raised acceptance limit.

Six new regression tests cover equal/changed generations, operation isolation, corrupt-current backup recovery, corrupt committed bytes, a store returning different content and bounded-cache eviction. A first development compile found a duplicate local name; it was corrected before the successful runs below. No test ran in that failed compile attempt.

A local .NET allocation probe used the real 38,615-character historical save, real isolated atomic save files and 25 measured operations per case. It did not instantiate Unity widgets or measure retained Unity heap:

| Path | Clean `119b56f` bytes/operation | Dirty candidate bytes/operation |
| --- | ---: | ---: |
| Full atomic save | 2,700,411.84 | 1,620,442.88 |
| Save service load | 1,244,937.28 | 849,024.32 |
| Load coordinator | 1,621,353.28 | 1,226,063.04 |

These represent approximately 40.0%, 31.8% and 24.4% less temporary allocation respectively, not proof of Windows memory acceptance. Probe source is local ignored `TestResults/Y0AllocationAudit/`; no ordinary player save was used.

Dirty-candidate development verification:

- Focused save tests: **48/48**, failed 0, skipped 0 (`Build/TestResults/y0-validation-cache-development.trx`).
- Full .NET restore / Release build / tests: **520/520**, failed 0, skipped 0; build errors/warnings 0 (`Build/TestResults/y0-cache-full-development.trx`).
- Unity **6000.3.16f1** full EditMode: **767/767**, failed 0, skipped 0 (`TestResults/unity-editmode.xml`, completed 7 October 23:18 TSI).
- Rebuilt Windows development player and save smoke: **3/3**, status **DEVELOPMENT_PASS**, not clean final-SHA acceptance (`TestResults/WindowsSaveSmoke/119b56f757b901ff8e2e37f26d1b8336295006b1/f15ffd0275c747c694717ae2adfc694e/manifest.json`).
- Normal no-forced-collection 1,200-second candidate run: **FAIL**, exit 1 after **848.881 seconds / 149 cycles**, `MANAGED_GROWTH_OVER_64_MIB` (`TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/d971b78d9eb942358ba13c64f1543afe/manifest.json`). Raw managed growth 68,870,144 bytes; native growth 5,679,496 bytes; UI elements 244. Normal player saves unchanged. Ten tracked campaigns and forty field lists were alive at failure; that uncollected sample is not proof of permanent retention. The allocation candidate alone does not close Y0.

All candidate runs above are on an uncommitted worktree based on `119b56f`; none is evidence for a new final SHA. The ordinary two-hour guard and final clean-SHA reruns remain mandatory.

### Windows allocation attribution — in progress

The failed twenty-minute candidate makes route/UI allocation attribution necessary; the .NET probe alone was insufficient. A first attempt to use `GC.GetAllocatedBytesForCurrentThread` was rejected by a real Unity regression: a known 4,096-byte allocation produced zero from this API. That suite finished **767 passed / 1 failed / 0 skipped (768 total)** at 23:39 TSI, and the chained Windows build/run did not execute. No zero-allocation result is claimed.

The replacement is a separate `-AllocationAudit` / `-focSoakAllocationAudit` mode using Unity `ProfilerRecorder`, following the [official 6000.3 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html) and [collection options](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.html). A second development suite correctly rejected a requirement that the `GC.Alloc` marker have byte units (**768 passed / 1 failed / 0 skipped**, 769 total, 23:43 TSI); again the chained player run did not execute. Marker sample values are not to be mislabeled as allocated bytes.

The revised diagnostic counts synchronous `GC.Alloc` EVENTS on the current thread for navigation, save, load and fingerprint callbacks, with a 65,536-sample capacity that fails explicitly on saturation. Byte values come separately from `GC Allocated In Frame`, with its byte units checked. Five diagnostic probe rounds isolate named operations in separate frame windows after warmup; those windows include engine/layout work, not exclusively the callback. An idle window is recorded for comparison, not subtracted to manufacture an exact value. Probe roundtrips also compare the real canonical campaign fingerprint. Native storage is disposed on finish/disable; a regression requires a real event, a positive frame-byte sample after a known allocation and disposal of both recorders.

Only the opt-in allocation diagnostic enables these recorders. Its success is **DIAGNOSTIC_COMPLETE**, never acceptance PASS, even on a clean SHA. It forces no GC; original resource guards still apply. Combining it with collected-memory diagnostics is rejected.

The replacement development Unity suite completed **769/769**, failed/skipped 0, at 23:49 TSI; preserved local result: `TestResults/Y0AllocationAudit/unity-attribution-769-development.xml`. The actual D3D11 Windows attribution run completed **180.192 seconds / 30 ordinary cycles**, exit 0, **DIAGNOSTIC_COMPLETE**, not acceptance. Five extra isolated probe roundtrips ran within it. `GC.Alloc` reported `TimeNanoseconds` units, so only its sample COUNT is used for allocation events; no timing value is relabeled as bytes. Source evidence: `TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/a270bd0cfb5a41be9fa5ee371f12f382/manifest.json` and `player-soak.json`. Normal player saves were unchanged.

Measured mean frame-window bytes (not retained heap or exact callback-exclusive bytes):

| Probe | Samples | Bytes / window |
| --- | ---: | ---: |
| Idle comparison | 5 | 107,636.8 |
| Navigation | 50 | 263,833.3 |
| Save | 5 | 1,724,917.8 |
| Load plus presentation reconstruction | 5 | 2,108,544.4 |
| Canonical fingerprint | 5 | 508,913.0 |

### Collection reuse candidate — pending verification

The Windows probe confirms that route rendering is a material source of transient allocation. `PresentationShellController` previously created new current/detail ListViews and their virtual rows for every render. The candidate reuses one current collection and detail slots bounded by the largest simultaneous detail set. Binding reads the current item source instead of closing over old route data. Inactive slots release their item sources; row destruction detaches its click callback; controller disposal clears collection bindings and sources. Unknown data stays unknown, localization and link navigation stay unchanged, fixed-height virtualization remains, and foldouts keep the prior per-render collapsed behavior. No gameplay or model change is included.

New Unity regressions cover object reuse with fresh fields, bounded detail slots and source release, current-entity single dispatch and retired-controller click rejection. The diagnostic failure path is also checked so final reporting cannot restart an unavailable recorder. Fresh development .NET restore/build/tests completed **520/520**, failed/skipped 0, build errors/warnings 0 (`Build/TestResults/y0-ui-reuse-development.trx`). Unity completed **773/773**, failed/skipped 0 (`TestResults/Y0AllocationAudit/unity-ui-reuse-773-development.xml`). Neither run is final-SHA evidence.

The rebuilt Windows comparison completed **180.630 seconds / 31 ordinary cycles**, exit 0, **DIAGNOSTIC_COMPLETE**, normal saves unchanged (`TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/0e4533a68aff4c698b460f2e6d37d22c/manifest.json`). Its five probe rounds used the same definitions, source campaign and resolutions as the prior diagnostic:

| Probe | Before bytes / window | Reuse candidate bytes / window |
| --- | ---: | ---: |
| Idle comparison | 107,636.8 | 107,639.2 |
| Navigation | 263,833.3 | 60,685.0 |
| Save | 1,724,917.8 | 1,716,221.2 |
| Load plus presentation reconstruction | 2,108,544.4 | 2,098,602.6 |
| Canonical fingerprint | 508,913.0 | 508,937.0 |

Navigation frame-window allocation is approximately 77.0% lower; this is a scoped diagnostic comparison, not live-heap, graphics-performance or long-run acceptance. The ordinary no-audit/no-forced-GC candidate completed **1,203.240 seconds / 212 roundtrips, exit 0, DEVELOPMENT_PASS**, normal saves unchanged: `TestResults/WindowsPlayerSoak/119b56f757b901ff8e2e37f26d1b8336295006b1/66b6dd064ce74b768f3fd439f76dea4a/manifest.json`. Last managed growth was 32,399,360 bytes, below the unchanged 64 MiB guard; the raw managed peak was 64,212,992 bytes. This development result predates the final lifecycle cleanup and is not final-SHA/two-hour acceptance.

On 8 October the user explicitly prioritized 3D character/motion work. Memory closure is paused as task scope, not accepted; do not launch another long soak automatically. Preserve these changes and resume only when requested. A subsequent scroll reset compiler error was corrected with the supported public ScrollView query; the stale 773-case XML was not treated as a fresh result.

## Remaining gate

1. Evaluate the no-forced-collection development experiment; do not call a sampling candidate a proven fix before its result.
2. If it fails, investigate the remaining memory behavior without raising guards or changing gameplay.
3. Commit validated changes, rebuild and rerun .NET, Unity, existing pipelines and Windows smoke on the final clean SHA.
4. Complete the normal two-hour Windows soak on that unchanged SHA, with normal saves unchanged.
5. Verify local, remote and CI SHA agreement. Only then close Y0 and start Y1 scope locking.

No managed-memory snapshot has been captured. No physical OS mouse/keyboard input, continuous economy/AI/combat soak, graphics-art acceptance or release-performance certification is claimed by this UI/reload workload.
