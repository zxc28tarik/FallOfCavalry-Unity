# Save Schema

## Current version

`CampaignSaveData.CurrentSaveVersion = 7`.

The format is a deterministic UTF-8 text envelope. Its first line is `FOC_CAMPAIGN_SAVE`; fields then appear in a fixed order as `key=value`. Text and structured Character records are base64-encoded. The format is deliberately dependency-free; it is an infrastructure detail, not a Domain contract.

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

## Version 2 Character Core

Version 2 adds ordered `Characters` and canonical `CharacterRelations` collections. Each character persists identity kind/provenance, name, importance, all nine stats, separate loyalty/satisfaction/reputation/standing values, one typed physical location, injury, captivity, death and bounded history. Relations contain only explicitly created pairs. Definition objects, runtime objects and caches are reconstructed rather than serialized directly.

## Version 3 social/political foundation

Version 3 adds deterministically ordered Organization, House and Clique aggregates in one encoded social-state payload. It preserves Organization memberships and assignments (including typed target, authority, presence and status), House head/members/wealth/prestige/property/marriage/family/inheritance/lifecycle state, and Clique type/members/leader/influence sources/hierarchy/lifecycle/attitude. All Character references remain stable `CharacterId` values.

## Version 4 Religion/Sect foundation

Version 4 adds a separate deterministic religion-state payload. It preserves immutable Religion/Sect definition snapshots, optional Character sect affiliation, typed City/Faction/Region profiles, qualitative policy rules, and typed Religious Clique associations. Relative profile presence is an ordinal content value, not a claim of exact historical population percentage. Definitions and save DTOs remain separate from runtime state.

## Version 5 City V2 foundation

Version 5 adds a deterministic City payload. It persists City identity/name, all nine functional-area definitions and fullness states, building pools and active/locked IDs, future visual hooks, non-negative population plus unassessed metric hooks, invisible infrastructure state and references to real Organization assignments for officials. It contains no stock, price, production, recruitment, battle or construction simulation.

## Version 6 Trade / Production / Caravan

Version 6 adds deterministic Trade Good and production-recipe definitions, authoritative per-City stock/demand/market cash, and persistent Caravans. A Caravan stores a typed owner, real Character manager, optional Trade-branch representative assignment, origin/destination, weight capacity, real cargo, operational cash, lifecycle/location stage, accounting totals and source-tagged future route-risk inputs. Prices themselves are formed from content reference values and explicit inputs and are not persisted as hidden mutable truth.

## Version 7 Diplomacy / Envoy / Report

Version 7 persists the minimum Faction actor registry, canonical relation pairs and explicit factors, diplomatic actions, role-scoped Envoy missions, messages, typed reports/observations, delivered actor information and controlled agreement terms. Envoy and carrier references point to real Characters; mission assignments reuse the Organization Diplomacy branch. Observed, dispatched and arrived timestamps remain distinct. Only delivered reports enter actor information, and report observations never replace authoritative world state.

## Migration policy

Each `ISaveMigration` advances exactly one integer version. `SaveMigrationPipeline` applies a continuous ascending chain. Downgrades, gaps, duplicate starting versions, and a migration returning the wrong version fail explicitly. A persistent schema change must increment the version and include its migration and old fixture in the same package.

`CampaignSaveV1ToV2Migration` initializes empty Character collections. `CampaignSaveV2ToV3Migration` initializes empty social collections. `CampaignSaveV3ToV4Migration` initializes empty Religion/Sect collections. `CampaignSaveV4ToV5Migration` initializes Cities empty. `CampaignSaveV5ToV6Migration` initializes Economy empty. `CampaignSaveV6ToV7Migration` preserves all prior state and initializes Diplomacy/communication/information collections empty. No migration invents actors, relations, Envoys, messages or reports. Hardcoded old-schema fixtures exercise the continuous chain.

## Atomic storage policy

1. Write `<slot>.focsave.tmp` in UTF-8 and flush it to disk.
2. Read and validate the temporary content.
3. Replace the current file only after validation.
4. Preserve the previous current file as `<slot>.focsave.bak`.
5. On read, use the current file when valid; otherwise return a valid backup and mark recovery.

Slots are restricted to letters, digits, `_`, and `-` to prevent path traversal. Invalid state is reported, not silently repaired.
