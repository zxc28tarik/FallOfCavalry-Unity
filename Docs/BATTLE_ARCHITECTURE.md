# Battle Architecture

Implementation 9 introduces the first tactical Battle foundation without making Unity animation, physics, or presentation authoritative.

## Runtime boundary

`CampaignRuntimeState.Battles` owns persistent `BattleState` records. Battle creation copies explicit campaign truth into controlled snapshots: Army and Unit Group IDs, real commanders, aggregate headcount, active persistent Soldier IDs, exact equipment-instance IDs, qualitative morale/fatigue/discipline, Army supply, WorldTimestamp, and RandomState. It does not mutate Army, Unit Group, Soldier, Character, equipment, supply, or campaign time.

Aggregate headcount and instantiated Soldiers remain distinct. `BattleUnitSnapshot.AggregateNonInstantiatedStrength` is count-based and cannot create a SoldierId. Persistent Soldiers are included only when already present and active in the campaign roster.

## Lifecycle and time

The controlled lifecycle is Preparing → Deployment → Active → Resolving → Completed. Aborted is terminal. Invalid transitions, post-completion orders, deployment outside Deployment, and combat outside Active are rejected. `BattleState.Step`, event sequence, and explicit WorldTimestamp values replace wall-clock and frame timing.

Each Battle owns a serialized deterministic RandomState. Combat code receives `IRandomSource`; UnityEngine.Random, `System.Random`, wall-clock seeds, and frame delta are forbidden.

## Combat boundary

`BattleAttackIntent` names attacker group, persistent attacker/target Soldier IDs, persistent weapon EquipmentInstanceId, AttackMode, and DamageType. `BattleCombatService` proves that the attacker belongs to the declared deployed group, the target belongs to a hostile deployed group, the equipment belongs to the snapshot, and the declared attack option exists on its WeaponDefinition. Ranged weapons require explicit ammunition policy/ledger consumption. Persistent target ArmorDefinitions are passed to the optional armor-policy boundary without inventing a protection formula.

`IBattleCombatResolver`, `IBattleArmorPolicy`, and `IBattleAmmunitionPolicy` are policy boundaries. Implementation 9 intentionally defines no production damage, penetration, armor, hit, accuracy, reach, speed, terrain, formation, charge, morale, rout, or casualty coefficient.

Morale, fatigue, and discipline remain distinct typed assessments. Formations and terrain tags are organizational/descriptive state only.

## Information and presentation

Battle truth is not automatically actor knowledge. No Battle service writes `ActorInformationState`; later report delivery must reuse Implementation 6 observation/report contracts.

`BattleVisualBinding` verifies exact persistent identity and equipment before `BattleCombatantViewBinding` delegates to the existing `VisualSoldier3DAssembler`. Visual despawn only releases a view. Sector anchors and stable layout are presentation-only and never affect simulation results.
