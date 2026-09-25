# Save Schema

## v13 — Historical geography and world travel

Schema v13 adds immutable world-location and explicit route-graph snapshots plus persistent travel journeys. A journey stores actor kind/ID, origin, destination, ordered route IDs, departure and last-advanced timestamps, current segment, integer elapsed segment ticks, lifecycle and optional arrival time. Character/Army/Caravan authoritative location or lifecycle remains in its existing aggregate and must agree with the journey.

Migration v12→v13 initializes empty geography, routes and journeys. It invents no location, route, actor or active trip. Authored slice geography is content bootstrap responsibility. Mid-travel saves resume from exact integer progress; no float position is save authority.

## v12 — AI semantic continuation

Schema v12 adds canonical AI controller records: typed owner, stable priority/Character/decision-quality/scheduling profile IDs, lifecycle, last/next decision timestamps, deterministic RNG continuation, and optional active-plan semantics (goal, proposal identity, domain/policy, target, timing, lifecycle).

Migration v11→v12 preserves all earlier state and initializes an empty AI collection. It creates no actor, plan, report/ActorInformation, resource, or candidate cache. Perception contexts, utility candidates, traces, foreign truth copies, Unity objects, and presentation state are never persisted. Profile/bootstrap content remains an explicit post-load/content responsibility.

## Current version

`CampaignSaveData.CurrentSaveVersion = 13`.

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

## Version 8 Army / Recruitment / Logistics

Version 8 persists typed Armies and Unit Groups, owner/controller and real-Character command references, physical location/lifecycle, finite recruitment sources and records, command relationships, real Trade Good supply inventory and requirements, separate morale/fatigue/discipline assessments, and payroll obligations/payments/arrears. It contains no SoldierInstance, equipment, Battle, casualty or pathfinding state.

## Version 9 Soldier / Equipment / Weapon / Armor

Version 9 adds immutable Troop, Weapon, Armor, Shield, Mount and Auxiliary Equipment definition snapshots; persistent Equipment Instances; and persistent Soldier Instances. A Soldier stores stable identity, Unit Group and recruitment provenance, qualitative experience/training/lifecycle values, and exact typed loadout assignments. Definition, instance, Soldier, Unit Group and economic-good identities remain separate. The schema stores no Battle, deployment, casualty, animation or pathfinding state.

Version 10 adds persistent active/completed Battle state: typed sides, participant snapshots, aggregate and instantiated strength, exact persistent Soldier/equipment references, sectors and adjacency, deployment/formations, typed orders/events, explicit battle ammunition, lifecycle/step, deterministic RNG continuation, optional BattleResult, and one-time reconciliation state. It serializes no GameObject, Transform, Animator, mesh, visual pool, cache, or scene coordinate. The v9→v10 migration creates an empty valid Battle registry and invents no Battle, Soldier, casualty, or result.

## Version 11 Encounter / Contract

Version 11 persists Encounter instances and Contract instances, not their immutable content definitions. Encounter records preserve typed definition/family/type identity, timestamps, typed source/participants, lifecycle, selected choice, deterministic RNG continuation, resolution, optional Battle/Contract links and one-time application state. Contract records preserve category, typed issuer/assignee/target bindings, objective evidence, lifecycle, optional explicit deadline and Encounter/Battle links. The v10→v11 migration initializes both registries empty and invents no gameplay state.

## Migration policy

Each `ISaveMigration` advances exactly one integer version. `SaveMigrationPipeline` applies a continuous ascending chain. Downgrades, gaps, duplicate starting versions, and a migration returning the wrong version fail explicitly. A persistent schema change must increment the version and include its migration and old fixture in the same package.

`CampaignSaveV1ToV2Migration` initializes empty Character collections. `CampaignSaveV2ToV3Migration` initializes empty social collections. `CampaignSaveV3ToV4Migration` initializes empty Religion/Sect collections. `CampaignSaveV4ToV5Migration` initializes Cities empty. `CampaignSaveV5ToV6Migration` initializes Economy empty. `CampaignSaveV6ToV7Migration` initializes Diplomacy/communication/information. `CampaignSaveV7ToV8Migration` initializes military state. `CampaignSaveV8ToV9Migration` initializes Soldier definitions/equipment/roster without inventing Soldiers. `CampaignSaveV9ToV10Migration` initializes Battle state without inventing battles. `CampaignSaveV10ToV11Migration` initializes Encounter/Contract state without inventing instances. `CampaignSaveV11ToV12Migration` initializes AI state without inventing controllers or plans. `CampaignSaveV12ToV13Migration` initializes geography/travel collections without inventing a world or journey. Hardcoded old-schema fixtures exercise the continuous chain.

## Atomic storage policy

1. Write `<slot>.focsave.tmp` in UTF-8 and flush it to disk.
2. Read and validate the temporary content.
3. Replace the current file only after validation.
4. Preserve the previous current file as `<slot>.focsave.bak`.
5. On read, use the current file when valid; otherwise return a valid backup and mark recovery.

Slots are restricted to letters, digits, `_`, and `-` to prevent path traversal. Invalid state is reported, not silently repaired.
