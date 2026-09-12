# Army / Recruitment / Logistics

Implementation 7 introduces the persistent pre-battle military campaign aggregate.

## Identity and command

`ArmyState` is distinct from `OrganizationBranch.Army`, `UnitGroupState`, Character location and ownership. Owner and controller are typed references and cannot be a Clique. A commander is a live, non-captive Character referenced through an active Organization Army assignment and must be physically present. The command hierarchy has one active parent per node and rejects cycles.

## Recruitment

Recruitment sources carry one of the seven authoritative source kinds, finite available headcount, a real Organization Army authority assignment and optional City/Military-institution context. Timar/Service and Mercenary sources require explicit obligation/contract references. A successful recruitment consumes source availability and creates a persistent Unit Group plus an immutable recruitment record. Source kind and troop-definition reference remain separate.

## Logistics and payroll

Army supply is a sorted inventory of Implementation 5 Trade Goods. City and Caravan transfers preflight source quantity, destination overflow and co-location before applying both sides atomically. Food, fodder, ammunition and general requirements are explicit content inputs; shortage is derived from required versus present quantity without hidden percentages.

Payroll obligations preserve amount owed, paid amount, arrears, due time and a typed real-cash source. Payments debit an existing City market or Caravan and cannot repeat or exceed arrears. Salary does not mutate Character loyalty, Army morale or discipline.

## Readiness and information

Morale, fatigue and discipline are separate qualitative campaign states. `ArmyReadinessProjection` exposes those underlying states and supply conditions without collapsing them into an invented score. Army report subject/observation hooks extend the delayed report system; they do not expose foreign `ArmyState` as omniscient actor knowledge.

## Deferred authority

SoldierInstance, loadouts, equipment, Battle, casualties, pathfinding, consumption rates, salary rates, capacity formulas, morale thresholds, fatigue recovery and combat effects are deferred to their owning implementations or explicit authority decisions.
