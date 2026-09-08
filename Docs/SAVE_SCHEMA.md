# Save Schema

## Current version

`CampaignSaveData.CurrentSaveVersion = 1`.

The Implementation 0 format is a deterministic UTF-8 text envelope. Its first line is `FOC_CAMPAIGN_SAVE`; fields then appear in a fixed order as `key=value`. Text values are base64-encoded UTF-8. The format is deliberately small and dependency-free; it is an infrastructure detail, not a Domain contract.

## Version 1 root metadata

| Field | Meaning |
|---|---|
| `SaveVersion` | Persistent schema version. |
| `CampaignId` | Stable campaign identity. |
| `GameVersion` | Producer game build/version. |
| `ContentDataVersion` | Static definition/content revision. |
| `WorldSeed` | Authoritative campaign generation seed. |
| `WorldGenRevision` | Deterministic generation/rules revision. |
| `WorldTime` | Authoritative WorldClock tick. |
| `RngState` | Current deterministic RNG internal state. |
| `RngDrawCount` | Number of RNG draws represented by the state. |

## Runtime separation

`CampaignSaveData` is a transport DTO. `CampaignRuntimeState`, `WorldClock`, and `SeededRandomSource` are runtime objects and are never serialized directly. `CampaignSaveMapper` is the explicit boundary in both directions.

## Migration policy

Each `ISaveMigration` advances exactly one integer version. `SaveMigrationPipeline` applies a continuous ascending chain. Downgrades, gaps, duplicate starting versions, and a migration returning the wrong version fail explicitly. A persistent schema change must increment the version and include its migration and old fixture in the same package.

Version 1 is the first schema, so no production migration is required yet.

## Atomic storage policy

1. Write `<slot>.focsave.tmp` in UTF-8 and flush it to disk.
2. Read and validate the temporary content.
3. Replace the current file only after validation.
4. Preserve the previous current file as `<slot>.focsave.bak`.
5. On read, use the current file when valid; otherwise return a valid backup and mark recovery.

Slots are restricted to letters, digits, `_`, and `-` to prevent path traversal. Invalid state is reported, not silently repaired.

