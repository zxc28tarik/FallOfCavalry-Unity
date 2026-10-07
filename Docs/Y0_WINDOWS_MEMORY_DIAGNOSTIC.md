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

## Remaining gate

1. Evaluate the no-forced-collection development experiment; do not call a sampling candidate a proven fix before its result.
2. If it fails, investigate the remaining memory behavior without raising guards or changing gameplay.
3. Commit validated changes, rebuild and rerun .NET, Unity, existing pipelines and Windows smoke on the final clean SHA.
4. Complete the normal two-hour Windows soak on that unchanged SHA, with normal saves unchanged.
5. Verify local, remote and CI SHA agreement. Only then close Y0 and start Y1 scope locking.

No managed-memory snapshot has been captured. No physical OS mouse/keyboard input, continuous economy/AI/combat soak, graphics-art acceptance or release-performance certification is claimed by this UI/reload workload.
