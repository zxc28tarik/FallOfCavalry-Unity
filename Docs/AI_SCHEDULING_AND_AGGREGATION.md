# AI Scheduling and Aggregation

AI work is driven by `WorldClock` timestamps and explicit scheduling profiles, never Unity frames. Due controllers are ordered by typed owner and controller ID. Cadence is profile data; no universal production interval is asserted.

Important actors remain individually planned. Low-importance aggregation is opt-in through `IAIScheduledDecisionSource.CanAggregate`, a stable equivalence key, and a configured batch size. A source must build shared candidate/context work only for actors it can prove equivalent, then materialize an owner-specific immutable context for every controller.

Aggregation shares computational work; it never merges Character IDs, controllers, relations, money, stock, supply, plans, or save records. Selected proposals retain their exact owner. Generated-person promotion preserves the same Character ID and therefore the same decision-owner identity.

Multiple due actors are evaluated in canonical order. `RunImportantSequential` executes each important actor's accepted proposal before constructing the next actor's context, so later legality/perception sees earlier authoritative mutation without races. Aggregated low-importance batches share only evaluation work and are not routed through that mutating execution loop.
