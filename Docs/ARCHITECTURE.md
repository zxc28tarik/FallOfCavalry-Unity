# Architecture

## Boundary rule

Dependencies point inward:

```text
FOC.Presentation  ─┐
FOC.Infrastructure ├─> FOC.Application ─> FOC.Domain
                   └─────────────────────> FOC.Domain
```

`FOC.Domain` is pure C# and its assembly definition has no engine references. It owns stable IDs, deterministic random/time contracts, definitions, runtime-state markers, messages, and validation primitives. It may not reference Application, Infrastructure, Presentation, or UnityEngine.

`FOC.Application` orchestrates use cases and owns ports such as save serialization and atomic storage. It maps `CampaignRuntimeState` to and from `CampaignSaveData`; it does not perform filesystem I/O.

`FOC.Infrastructure` implements Application ports. The Implementation 0 adapter provides deterministic text serialization and an atomic file store with temp validation, replacement, backup, and backup read recovery.

`FOC.Presentation` is an outer layer. Implementation 0 contains no gameplay UI and no state mutation path.

`FOC.Tests` is an Editor test assembly. The same engine-independent test sources are compiled by the .NET mirror projects in `Build/`, allowing local and CI validation without duplicating production source.

## Source of truth

- Campaign time: `WorldClock` only.
- Gameplay random sequence: the injected `IRandomSource` state only.
- Static content: immutable `IDefinition<TTag>` implementations in a `DefinitionRegistry`.
- Mutable campaign foundation: `CampaignRuntimeState`.
- Persistence representation: `CampaignSaveData`, never runtime objects.
- Message ordering: the explicit sequence in `DeterministicMessageQueue`.

## Public-contract policy

Typed IDs are generic by tag, so unrelated domain identities cannot be mixed. Future packages should introduce domain-specific tags or wrappers rather than a universal entity abstraction. State changes belong behind bounded rule/application services; Presentation must not mutate fields directly.

No gameplay package is implemented in this baseline.

