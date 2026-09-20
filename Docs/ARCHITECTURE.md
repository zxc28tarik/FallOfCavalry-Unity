# Architecture

## Boundary rule

Dependencies point inward:

```text
FOC.Presentation  ─┐
FOC.Infrastructure ├─> FOC.Application ─> FOC.Domain
                   └─────────────────────> FOC.Domain
```

`FOC.Domain` is pure C# and its assembly definition has no engine references. It owns stable IDs, deterministic random/time contracts, definitions, runtime state, Character Core rules, Organization/House/Clique state, messages, and validation primitives. It may not reference Application, Infrastructure, Presentation, or UnityEngine.

`FOC.Application` orchestrates use cases and owns ports such as save serialization and atomic storage. It maps `CampaignRuntimeState` to and from `CampaignSaveData`; it does not perform filesystem I/O.

`FOC.Infrastructure` implements Application ports. The Implementation 0 adapter provides deterministic text serialization and an atomic file store with temp validation, replacement, backup, and backup read recovery.

`FOC.Presentation` is an outer layer. Implementation 0 contains no gameplay UI and no state mutation path.

`FOC.Tests` is an Editor test assembly. The same engine-independent test sources are compiled by the .NET mirror projects in `Build/`, allowing local and CI validation without duplicating production source.

## Source of truth

- Campaign time: `WorldClock` only.
- Gameplay random sequence: the injected `IRandomSource` state only.
- Static content: immutable definitions in typed registries; Religion and Sect definitions are content-driven and Sect carries an explicit parent `ReligionId`.
- Mutable campaign foundation: `CampaignRuntimeState`.
- Persistent human identity: immutable `CharacterDefinition` subtypes referenced by mutable `CharacterState`.
- Character physical location: one `CharacterLocation` discriminated value; captivity is its typed payload rather than a second location field.
- Character relations: one canonical, sorted, explicitly populated sparse relation collection on `CharacterRoster`.
- Organization membership, assignment, authority and Character location: separate contracts; assignment feasibility reads but never mutates authoritative Character location.
- Family, Household, House, Clique and Faction: separate identity/state boundaries.
- Internal politics: projections derived from explicit Character, House, Clique, policy/event inputs; never a hidden stockpile.
- Religion identity: `ReligionId` and optional compatible `SectId`, separate from Character, Faction, House, Organization and Religious Clique identities.
- Religion context: typed City/Faction/Region profiles and policy rules; difference alone has no unrest, loyalty, relation, rebellion, war or combat effect.
- City state: persistent `CityState` aggregates immutable City/area/building definitions, explicit area fullness, invisible infrastructure, metrics hooks and references to real Organization assignments.
- City structure: City, Area, Building and Infrastructure are separate contracts. Presentation, Economy, Army and Battle are not City-domain dependencies.
- Economy state: Trade Good definitions, production recipes, City markets, demand sources and Caravans are typed deterministic aggregates. City stock and Caravan cargo are the only goods authorities in this package.
- Economic ownership: a Caravan owner may be a Character, House or Organization. Manager and optional Trade-branch representative are separate Character/assignment references; Merchant Clique and Trade Network never own goods.
- Trade mutation: production and buy/sell operations run through Application services that preflight every stock, capacity, cash and accounting condition before mutating state.
- Diplomacy state: typed Faction actors, canonical pair relations, explicit factors, actions, agreements, Envoy missions, messages, reports and actor information are separate aggregates.
- Information boundary: delivered reports are actor knowledge; report observations never mutate or masquerade as world truth. Future player/AI query layers must consume `ActorInformationState` for remote knowledge rather than an omniscient `CampaignRuntimeState` reference.
- Communication mutation: Application services validate real Characters, existing Organization Diplomacy assignments, mandate scope, WorldClock time and arrival before a remote action or report becomes available.
- Military state: `CampaignMilitaryState` owns typed Army, Unit Group, recruitment-source and recruitment-record registries. `OrganizationBranch.Army` remains an assignment network and is not the Army aggregate.
- Military mutation: Army creation/recruitment, City or Caravan supply transfer and payroll payment cross aggregate boundaries only through Application services. Goods and cash are removed from their real Economy owner before the Army receives them.
- Military information: authoritative foreign `ArmyState` is not actor knowledge. Remote strength/location/supply observations enter the existing report pipeline and actor information only after delivery.
- Soldier state: `SoldierCampaignState` owns immutable Troop/equipment definitions, persistent Equipment Instances and persistent Soldier Instances. `CampaignMilitaryState` remains authoritative for Army/Unit Group aggregate headcount; instantiated Soldiers are a bounded roster that cannot exceed it.
- Soldier loadout: exact typed slot assignments are persistent campaign truth. Acquisition and replacement run through `SoldierEquipmentService`; reads and the future-facing `SoldierCombatSnapshot` never reroll or mutate loadouts.
- Equipment economy boundary: acquisition consumes real City stock through an explicit definition-to-Trade-Good mapping before creating owned instances. Equipment definitions, instances, City stock and Army supply are distinct contracts.
- Visual Presentation: modular 3D is authoritative. Engine-independent `FOC.Visuals.Core` converts Soldier/Troop/loadout truth into deterministic visual plans; `FOC.Visuals.Unity` resolves those plans to Unity assets. Domain never references either assembly. Visual binding, pooling, caches, meshes, materials, Animator and LOD are rebuildable Presentation state and never save truth.
- Persistence representation: `CampaignSaveData`, never runtime objects.
- Message ordering: the explicit sequence in `DeterministicMessageQueue`.

## Public-contract policy

Typed IDs are generic by tag, so unrelated domain identities cannot be mixed. Future packages should introduce domain-specific tags or wrappers rather than a universal entity abstraction. State changes belong behind bounded rule/application services; Presentation must not mutate fields directly.

`CharacterCommandService` remains the Character orchestration boundary. Organization, House, Clique, Religion and City command services validate their bounded cross-aggregate references. `ProductionService` and `TradeTransactionService` own economy mutations. `DiplomacyCommandService` and `ReportDeliveryService` own communication transitions. `ArmyCommandService`, `ArmySupplyService` and `ArmyPayrollService` own military cross-aggregate mutations. `SoldierEquipmentService` owns Soldier acquisition and explicit loadout replacement. No package implements Battle resolution, deployment, pathfinding, AI strategy or gameplay UI behavior.
