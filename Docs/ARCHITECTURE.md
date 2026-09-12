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
- Persistence representation: `CampaignSaveData`, never runtime objects.
- Message ordering: the explicit sequence in `DeterministicMessageQueue`.

## Public-contract policy

Typed IDs are generic by tag, so unrelated domain identities cannot be mixed. Future packages should introduce domain-specific tags or wrappers rather than a universal entity abstraction. State changes belong behind bounded rule/application services; Presentation must not mutate fields directly.

`CharacterCommandService` remains the Character orchestration boundary. `OrganizationCommandService`, `HouseCommandService`, `CliqueCommandService` and `ReligionCommandService` validate cross-aggregate references and lifecycle restrictions. None implements later City V2, Economy, Army, AI or UI behavior.
