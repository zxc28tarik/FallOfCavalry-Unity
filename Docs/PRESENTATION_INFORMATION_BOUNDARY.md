# Presentation information boundary

`PresentationViewerContext` explicitly identifies the viewing Faction, controlled identity, exact-readable self-owned entities and development-debug flag.

Exact state is projected only when the context grants exact access. Foreign City/Market/Army projections use `ActorInformationState.OrderedAvailableReports`; the global report registry and underlying foreign runtime objects are not consulted. Report rows expose observed/arrived ticks, quality, detail, precision and staleness. Unknown remains unknown; approximate values retain the approximation marker; stale values retain provenance.

Map providers receive the viewer context and return already knowledge-safe markers. The included proof provider accepts only markers flagged `PROOF_ONLY`; these coordinates are never persisted or represented as production geography.

AI traces are not included in normal screen read models. A host may expose a separate developer source only when `DevelopmentDebug` is explicit. This does not grant player knowledge.

Mandatory regression: changing a hidden foreign Army while player information is unchanged leaves its presentation unknown. Restoring a delivered approximate report changes only the visible observation and metadata, not world truth.

