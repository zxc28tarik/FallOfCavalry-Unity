# AI Difficulty and Fairness

`AIDecisionQualityProfile` controls technical decision work: candidate breadth, contribution breadth, and planning depth. Production values remain content work. `ProofBasic` and `ProofAdvanced` demonstrate that the same legal candidates, own resources, and known information can be evaluated with different breadth.

Difficulty does not change money, stock, supply, recruitment capacity, travel speed, report access/quality, trade price, diplomatic cost, damage, armor, morale, ammunition, or any gameplay modifier.

Representative executors prove symmetry: recruitment consumes a finite source through `ArmyCommandService`; supply uses `ArmySupplyService`; trade uses atomic stock/cash/cargo through `TradeTransactionService`; diplomacy uses real envoy/message/action contracts; Contract and Encounter actions use existing services; tactical orders use `BattleState.Issue` and its commander, adjacency, and hostile-target validation.

No travel executor ships because no complete authoritative world travel service exists. `IAITravelActionProvider` is a boundary, not a teleport fallback.
