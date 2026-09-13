# Soldier and Equipment Foundation

Implementation 8 introduces persistent individual Soldier identity and persistent issued equipment without implementing Battle simulation.

## Aggregate boundary

- `CampaignMilitaryState` remains authoritative for Army and Unit Group aggregates. `UnitGroupState.Headcount` is the total group strength.
- `SoldierCampaignState` owns immutable content definitions, persistent `EquipmentInstance` records and the instantiated `SoldierInstance` roster.
- The instantiated roster may be a deliberate subset of a Unit Group, but it can never exceed that Unit Group's aggregate headcount. Migration does not expand aggregate headcount into millions of individual records.
- `SoldierCombatSnapshot` is a read-only handoff contract for the later Battle package. It does not simulate combat or own campaign truth.

## Soldier identity and provenance

Every Soldier has a stable `SoldierId`, a parent `UnitGroupId`, a `TroopDefinitionId`, recruitment source/record provenance, qualitative experience/training/lifecycle state, and one persistent `SoldierLoadout`. Soldier, Character, Unit Group and Troop Definition identities are distinct typed contracts.

## Equipment model

Definitions and instances are separate. Weapons, armor, shields, mounts and auxiliary equipment are immutable content definitions. An `EquipmentInstance` records its typed definition reference, owner, acquisition source/time, economic good and qualitative workmanship. Military Clique is not a valid owner.

Weapon slots are Main, Secondary, Sidearm, Throwing, Ammo and Special. Armor layers are Inner, Body, Additional, Head, Limbs and Mount Armor. Shield and Mount remain separate loadout references. Attack mode and damage type are separate, and one weapon may expose multiple attack options.

Troop capability is content-driven through Unit Class, Combat Role, mount context, allowed weapon families, armor/shield permissions and a visual-profile hook. Historical fixtures such as Deli, Humbaraci and Bostanci exercise generic contracts; their names never trigger special-case code.

## Acquisition and mutation

`SoldierEquipmentService.AcquireFromCity` validates every cross-aggregate reference and compatibility rule before mutation. A successful acquisition consumes one real unit of the mapped City stock per issued definition, creates owned equipment instances and creates the Soldier atomically. Failure leaves stock, equipment and roster unchanged.

Loadouts are generated only by an explicit acquisition or replacement command. Reading a loadout or creating a combat snapshot never rerolls it and never consumes campaign RNG. Equipment replacement is an explicit service operation using existing Soldier-owned equipment.

## Deferred work

Battle resolution, deployment, sectors, casualty reconciliation, animation, visuals, collision, detailed weapon statistics, durability, repair, manufacturing formulas and balance values remain outside Implementation 8.
