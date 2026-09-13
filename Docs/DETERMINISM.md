# Determinism

## Randomness

All gameplay randomness must enter through `IRandomSource`. `SeededRandomSource` uses a fixed xorshift64* transition and SplitMix64 seed normalization. A save stores both the internal non-zero state and draw count. Restoring `RandomState` resumes the exact next result.

Changing the algorithm or draw order is a deterministic-compatibility change. Such a change must increment the appropriate world-generation revision and add fixtures/migration policy before merge.

`System.Random`, `UnityEngine.Random`, cryptographic randomness, and implicit random selection are not campaign authorities.

## Time

`WorldClock` is the only campaign clock. Time advances only by explicit `WorldDuration`; pausing prevents advance and negative advance fails without mutation. Wall clock, `DateTime.Now`, frame time, and Unity `Time` values cannot decide campaign outcomes.

One world tick is an abstract foundation unit. Mapping ticks to calendar semantics is deferred to the package that owns that design decision.

## Ordering

- Stable IDs compare with ordinal, case-sensitive string ordering.
- Definition registries use sorted stable IDs.
- Domain messages receive monotonic sequence numbers and dispatch FIFO.
- Character rosters sort by `CharacterId`; relation keys canonicalize their two endpoints and use stable ordinal ordering.
- Character histories use monotonic sequence numbers and bounded insertion order.
- Organization, House and Clique registries sort by their typed stable IDs; assignment and membership views use stable typed-ID ordering.
- Religion and Sect definitions, Character affiliations, typed profiles, policy rules and Religious Clique associations use canonical typed-ID/target ordering.
- Cities sort by `CityId`; areas, building pools, active/locked buildings, infrastructure and official assignment references use canonical enum or stable-ID ordering.
- Trade Goods, recipes, recipe lines, City markets, stock records, demand sources, Caravans, cargo and route-risk inputs use typed stable IDs or explicit enum/source keys and canonical ordering.
- Diplomatic actors, canonical actor pairs, relation factors, actions, Envoy missions, messages, reports, observations, information records and agreements use typed IDs and stable source/subject keys.
- Armies, Unit Groups, recruitment sources/records, command relationships, Army goods, supply requirements, payroll obligations and payments use sorted typed IDs and canonical keys.
- Soldier/Troop/equipment definitions, Equipment Instances, Soldier rosters and typed loadout slots use sorted typed IDs or canonical enum order.
- Rules must never depend on the natural iteration order of dictionaries or hash sets.
- Future equal-score decisions require an explicit stable-ID tie-break.

## Verification

Tests lock same-seed sequences, RNG continuation, explicit time, stable ordering and full schema serialization. Diplomacy delivery, recruitment records, payroll payments and equipment acquisition accept explicit campaign time; future timestamps cannot bypass `WorldClock`. Report quality and Army readiness are source-preserving projections and never invoke randomness or replace underlying truth. Repeating the same Soldier acquisition command from the same state produces the same serialized result; loadout reads consume no RNG.
