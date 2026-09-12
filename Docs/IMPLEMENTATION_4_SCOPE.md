# Implementation 4 — City V2 Scope Lock

## Authority and base

- Binding authority: the latest Implementation 4 instruction, then Parts 1–11.
- Authoritative base: `5537d46e830da8a003c75a916fc1c35e21dffc3c` (`Implementation 3: READY`).
- Feature branch: `codex/impl-4-city-v2`.
- The latest four-level fullness decision (`Empty`, `Low`, `Half`, `Full`) supersedes the older optional five-level proposal.
- Part 6 explicitly closes the Military-area pool as Barracks, Training Ground, Arsenal, Stable/Cavalry Facilities, Guard Facilities, and a culture-specific military training institution. No recruitment, training, garrison or combat effects are included.

## IN SCOPE

- Reuse the existing typed `CityId`; add typed `CityBuildingId` and separate immutable City/area/building definitions from mutable runtime state.
- One deterministically ordered `CityRegistry` of persistent cities.
- Exactly nine top-level areas: InnerCastle, Trade, InnCaravan, Housing, Military, Health, ProductionCraft, FoodSupply, SquareCulture.
- InnerCastle always Full; other areas explicitly support Empty, Low, Half and Full.
- Per-area building pool, active/locked building state, removed-content guards and a future visual-variant reference.
- Authority-approved content definitions and future effect tags without effects or numeric bonuses.
- Invisible, separate infrastructure state for Well, Fountain, Cistern, WaterChannel, MainPavedRoad, SecondaryRoad, BridgeCrossing, DrainageLine and WasteSewageChannel.
- City metrics foundation: non-negative population count and typed, deliberately unassessed Wealth/Order/Health/Security hooks; no invented scale.
- Kethüda as a reference to an existing Organization assignment targeting the real City; no Character subtype, ownership or new Organization branch.
- Explicit City/Religion profile integration validation against a real `CityId`, preserving multi-religion and zero automatic religious effects.
- Schema v5, v4-to-v5 migration, continuous v1-to-v2-to-v3-to-v4-to-v5 chain, hardcoded v4 fixture, deterministic semantic roundtrip and regression.

## OUT OF SCOPE

- Implementation 5 Trade/Production/Caravan behavior: goods, stocks, demand, prices, inputs/outputs, cargo, route and profit.
- Tax/customs formulas, recruitment, training, garrison, Army, equipment, Battle, siege/fortification effects, Diplomacy, Envoy, AI management, final UI/art or historical city database.
- Construction costs/times, infrastructure need/cost/benefit formulas, population growth, city metric scales, building bonuses, country/city hardcoding or exact historical population.
- Owner-faction/territory/world-map systems beyond future typed-reference capability.
- A single-active-Kethüda rule: no newer authority defines the permitted maximum, so the foundation supports explicit assignment references without inventing a cardinality rule.

## AUTHORITATIVE CITY V2 RULES

- City is a functional-area persistent system, not a thirty-slot building list or Presentation state.
- City != Area != Building != Infrastructure; all remain separate from Character, Organization, Faction, House, Clique and Religion profile.
- InnerCastle is the fixed administrative/visual core and cannot receive an automatic combat modifier.
- Divan, TaxOffice, Diplomacy, Port/Sea, RegionalSpecial and Infrastructure are not top-level areas.
- Infrastructure is invisible City service state, never a visible building pool.
- RopeWorkshop remains removed. Butcher and Fishery belong only to FoodSupply, never ProductionCraft.
- Kethüda is an existing Character's Organization assignment and not the total City manager.
- City religion difference alone never changes order, loyalty, rebellion, war or combat state.

## PUBLIC CONTRACTS

- `CityBuildingId`, `CityDefinition`, `CityState`, `CityRegistry`
- `CityAreaType`, `CityAreaFullness`, `CityAreaDefinition`, `CityAreaState`
- `CityBuildingDefinition`, content status and future effect-tag contracts
- `CityInfrastructureType`, `CityInfrastructureState`, installed/condition foundation
- `CityMetricsState`, `CityMetricKind`, unassessed metric hook
- `CityOfficialReference`, `CityOfficialRole`, Kethüda assignment role contract
- `CityInvariantValidator`, `CityCommandService`, City/Religion integration rule
- City save DTOs and `CampaignSaveV4ToV5Migration`

## SAVE IMPACT

- Persistent schema changes from v4 to v5.
- v5 stores City definitions, area definitions/states, building definitions/status, infrastructure, metrics and official assignment references in canonical order.
- v4-to-v5 preserves Character, Organization, House, Clique and Religion data and initializes City collections empty; it invents no city.

## INTEGRATION POINTS

- `CampaignRuntimeState.Cities` owns City runtime state.
- Organization assignments remain the authority for Kethüda Character, role, authority, activity and City target.
- Religion profiles remain in `ReligionCampaignState`; a City integration rule resolves their typed target against `CityRegistry`.
- Future Faction/Region, Economy, Trade, Production, Caravan, Army, Battle, Diplomacy and Presentation packages consume contracts without being implemented here.

## TEST GATES

- Identity, duplicate rejection and deterministic ordering.
- Exact area enum and all fullness rules; InnerCastle mutation rejection.
- Building pool/active/duplicate/removed and Production/Food prohibitions.
- Infrastructure separation and exact supported types.
- Real Kethüda Character/assignment/City validation and mutation isolation.
- Real City Religion profile association, multi-profile continuity and zero automatic order effect.
- v1-to-v5, hardcoded v4, v5 full semantic roundtrip, deterministic serialization and old-state preservation.
- Implementation 0–3 regression, Domain UnityEngine independence, fresh .NET/Unity/final-SHA CI, clean worktree and matching remote SHA.

## OPEN AUTHORITY TOPICS

- Exact City metric scales and balance formulas.
- Building/infrastructure costs, benefits, condition/need formulas and construction times.
- Population growth and burden formulas.
- City tax, fortification and siege effects.
- Maximum simultaneous active Kethüda assignments.
- Historical city templates/content values.

These topics do not block the structural foundation and remain unimplemented.

## Legacy classification

Audited `origin/main` and `origin/333` City/settlement/building sources and documentation.

- KEEP: visual reference assets only as repository history; no runtime contract qualifies unchanged.
- ADAPT: four-level fullness vocabulary and functional-area/content-pool intent.
- REWRITE: City identity, area/building/infrastructure state, officials and persistence behind typed deterministic C# contracts.
- REMOVE: global mutable JS state, arbitrary string IDs, DOM/localStorage coupling, inferred hash/jitter city state, Port area, automatic Istanbul special casing, numeric bonuses/costs and thirty-slot/parcel truth.
- REFERENCE_ONLY: legacy city art, manifests, names, geography and historical planning documents for later content/art packages.
