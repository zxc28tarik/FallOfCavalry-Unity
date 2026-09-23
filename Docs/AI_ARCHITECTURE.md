# AI Architecture

The dependency flow is:

`Campaign state + delivered ActorInformation → AIDecisionContext → candidate generation → hard eligibility → utility contributions → priority/personality composition → decision-quality budget → canonical ranking → AIActionProposal → existing Application service`

`AIControllerState` binds a typed Character or Faction decision owner to stable profile IDs, deterministic continuation state, scheduling, and an optional plan. It is not the Character/Faction and does not confer gameplay ownership. Faction planning can express intent, but an executor must still present a real Character, assignment, commander, manager, envoy mandate, or other authority expected by the existing service.

The Domain assembly owns definitions and semantic runtime state. Application owns perception construction, decision evaluation, scheduling, invariants, and adapters to existing gameplay services. There is no AI economy, military state, diplomacy state, battle resolver, or recruitment system.

`AIActionProposal` is inert. Recruitment, supply, trade, diplomacy, encounter, contract, and tactical executors validate proposal ownership and then call the already-authoritative Application/Domain path. A changed world can therefore reject a previously legal plan normally.

Tactical AI proposes existing typed `BattleOrder` values. It never creates combat outcomes, damage, casualties, ammo, morale, or hidden visibility facts. A future observation provider may supply explicit tactical observations without widening access to campaign truth.
