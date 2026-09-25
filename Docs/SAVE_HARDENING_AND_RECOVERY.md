# Save hardening and recovery

Schema authority is `CampaignSaveData.CurrentSaveVersion = 14`. `CampaignSaveDefaults` owns the exact ordered v1→v14 migration chain; no runtime or document may maintain an independent chain. The v12→v13 step initializes empty geography/travel collections and invents no journey; v13→v14 atomically replaces temporary content identities while preserving geography and journey progress.

## Commit protocol

For each validated slot, `AtomicFileSaveStore` serializes writers and readers with a per-path gate, creates a same-directory `.tmp`, writes UTF-8, flushes the writer, performs a durable filesystem flush, rereads and validates the temp payload, preserves at most one `.bak`, atomically replaces the current file where the platform supports it, and rereads/validates the committed current generation. A stale `.tmp` is never authoritative.

Fault injection is available at `TempCreate`, `TempWrite`, `DurableFlush`, `TempValidation`, `Backup`, `Replace` and `FinalRead`. Tests prove that each interruption leaves at least one valid current or backup generation. Same-slot writers serialize; a load racing a save observes a committed generation only.

## Read and recovery policy

The current generation is authoritative when it parses and validates. If it is missing, truncated, corrupt or invalid and the single backup validates, load returns `RecoveredFromBackup` plus a player-safe reason. Recovery is explicit and does not silently rewrite either file. If neither generation validates, load fails. Future save versions, oversized payload/count fields, malformed/duplicate fields and invalid slots—including absolute paths, traversal, separators and extensions—fail explicitly.

`CampaignSaveCoordinator` reconstructs runtime state only after migration and validation. It additionally rejects mismatched content-data versions and world-generation revisions rather than silently loading incompatible state. Canonical serialization and `SavePayloadFingerprint` provide culture-independent deterministic evidence; the fingerprint is diagnostic, not gameplay truth.
