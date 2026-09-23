# Battle Reconciliation

`BattleResult` is an immutable outcome artifact, not campaign state. It carries the complete ordered participating-Side set, typed end reason, optional winning Side, completion timestamp, elapsed deterministic steps, Unit Group outcomes, persistent Soldier outcomes, optional Character outcomes, and terminal explicit-ammunition state.

`BattleReconciliationService` first validates every reference and proposed mutation. It rejects missing participants, excess losses, changed/unavailable Soldiers, terminal identity outcomes larger than Unit Group losses, persistent roster overflow after losses, invalid Character captivity, death-policy rejection, clock mismatch, and duplicate application. Validation failure occurs before campaign mutation.

After preflight, reconciliation applies Character rules, persistent Soldier lifecycle outcomes, Unit Group aggregate losses, and distinct morale/fatigue/discipline aftermath, then marks the Battle reconciled. A result can be applied once only.

Survivors keep the same SoldierId, SoldierInstance, SoldierLoadout, and EquipmentInstance IDs. Wounded Soldiers remain identified in their Unit Group. Killed/captured Soldiers remain historically addressable and retain equipment identity, but no longer count as active Unit Group roster. No equipment reroll, loot conversion, automatic deletion, or TradeGood conversion occurs.

Character wounds use `InjuryState`; captivity uses existing `CaptivityState`; death is preflighted through the injected existing `ICharacterDeathPolicy` and applied through the existing Character death state/history contract. Captured or killed commanders vacate Army/UnitGroup command, complete the corresponding active Army assignment, and deactivate their recruitment authority source. Ransom, exchange, prison economy, equipment loss, and loot are deferred.

Campaign time is not guessed. The caller explicitly advances WorldClock to `BattleResult.CompletedAt`; reconciliation requires equality and adds no hidden duration.
