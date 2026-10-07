# Save hardening and recovery

Schema authority is `CampaignSaveData.CurrentSaveVersion = 14`. `CampaignSaveDefaults` owns the exact ordered v1→v14 migration chain; no runtime or document may maintain an independent chain. The v12→v13 step initializes empty geography/travel collections and invents no journey; v13→v14 atomically replaces temporary content identities while preserving geography and journey progress.

## Commit protocol

For each validated slot, `AtomicFileSaveStore` serializes writers and readers with a per-path gate, creates a same-directory `.tmp`, writes UTF-8, flushes the writer, performs a durable filesystem flush, rereads and validates the temp payload, preserves at most one `.bak`, atomically replaces the current file where the platform supports it, and rereads/validates the committed current generation. A stale `.tmp` is never authoritative.

Fault injection is available at `TempCreate`, `TempWrite`, `DurableFlush`, `TempValidation`, `Backup`, `Replace` and `FinalRead`. Tests prove that each interruption leaves at least one valid current or backup generation. Same-slot writers serialize; a load racing a save observes a committed generation only.

## Read and recovery policy

The current generation is authoritative when it parses and validates. If it is missing, truncated, corrupt or invalid and the single backup validates, load returns `RecoveredFromBackup` plus a player-safe reason. Recovery is explicit and does not silently rewrite either file. If neither generation validates, load fails. Future save versions, oversized payload/count fields, malformed/duplicate fields and invalid slots—including absolute paths, traversal, separators and extensions—fail explicitly.

An individual candidate that cannot be decoded as strict UTF-8, or cannot be read because of an I/O/access error, is unavailable; it must not abort examination of the backup. Validation callback exceptions are not swallowed by this candidate-read handling. Replacing an invalid/unavailable current generation must preserve the existing backup rather than copying corruption over it. A valid current generation remains eligible to become the next backup; the existing backup is no longer pre-deleted before filesystem replacement.

The nonvisual hardening regressions first reproduced four failures in eleven new cases: invalid UTF-8 current recovery, exclusively locked current recovery, corrupt-current overwrite poisoning a valid backup, and interruption at `Replace` after deleting the only valid backup. They use full v14 integrated campaign saves, verify canonical payload recovery and unchanged recovery files, and test all seven fault-injection boundaries starting with a corrupt current plus a valid backup. A valid stale `.tmp` remains non-authoritative even when both committed generations contain invalid UTF-8.

These checks concern managed exceptions and injected stage interruptions on the tested filesystem. They do not establish power-loss durability on every storage device, cross-process writer arbitration, or atomic replacement on platforms that use the existing copy fallback.

`CampaignSaveCoordinator` reconstructs runtime state only after migration and validation. It additionally rejects mismatched content-data versions and world-generation revisions rather than silently loading incompatible state. Canonical serialization and `SavePayloadFingerprint` provide culture-independent deterministic evidence; the fingerprint is diagnostic, not gameplay truth.

## Isolated Windows save/load smoke

The previous Development smoke called both UI handlers and printed `PASS` unconditionally, including after a failed save or load. Three focused Unity regression cases initially failed; they require failure propagation/early stop and a successful round-trip fingerprint. A further case rejects a successful load of the wrong committed generation.

`-focSmokeTest` now always selects a fresh GUID-named directory directly below the OS temporary `foc-windows-smoke` parent. It never defaults to the normal player save directory. `DevelopmentSmokeSaveDirectory` rejects existing files/directories, relative paths, other parents, non-GUID names, player-save overlap and a linked/junction smoke parent. An optional `-focSmokeSaveRoot` must meet the same rules; overrides outside smoke mode are rejected. Ordinary interactive player save location, save schema and gameplay definitions are unchanged.

The smoke exercises the same save/load handlers as the UI. It verifies the save result, makes a one-tick/RNG probe mutation only in this temporary test campaign, loads/reconstructs the fresh current generation, and compares the full canonical payload fingerprint against the pre-save state. Save failure stops before load. Load failure, backup recovery, payload mismatch or startup failure produce `FOC_DEVELOPMENT_SMOKE_FAIL` and exit 1. Only a matching successful round-trip produces `FOC_DEVELOPMENT_SMOKE_PASS ... fingerprint=...` and exit 0. Screenshot and smoke modes cannot be combined.

After `Tools/Build-Windows-Development.ps1`, run `Tools/Test-WindowsSaveSmoke.ps1`. It runs three real Windows-player cases: successful round-trip (expected exit 0), invalid slot (expected exit 1, no load/no files), and deliberate current-file corruption in the isolated root (expected exit 1, no PASS marker). Diagnostic flags `-focSmokeSlot` and `-focSmokeCorruptAfterSave` are Development smoke-only, not gameplay features. The script checks bootstrap DLL freshness, records commands/logs/roots and start/end SHA, and compares normal player-save paths, bytes/hashes, lengths and modification times before/after. It does not repair or overwrite player saves.

Results and generated test saves are retained locally under ignored `TestResults/WindowsSaveSmoke/<SHA>/<run-id>/` and temporary GUID roots. No broad cleanup or player-save deletion is performed. A clean, unchanged SHA/worktree is required for final `PASS`; `-AllowDirty` labels successful development diagnostics `DEVELOPMENT_PASS`, never final evidence. Positive and expected-negative cases must all execute. These are automated UI-handler tests, not manual mouse interaction; they do not establish crash/power-loss durability or protection against adversarial cross-process filesystem tampering.
