# Full campaign integration proof

`IntegratedProofCampaignFactory` is production-located test/integration data marked `PROOF_ONLY`; it is not historical or balance authority and is not referenced by the executable Development bootstrap or `VerticalSliceCampaignFactory`. It remains a broad regression fixture containing every implemented aggregate, including Battle and Encounter/Contract state.

The executable `VerticalSliceCampaignFactory` independently composes `VERTICAL_SLICE_HISTORICAL-1648-09-01-r1`: eight researched/disclosed Characters, four City V2 states, economy/Caravan, diplomacy/report, a small retinue, persistent Soldiers/equipment, one AI controller and authored 14A geography. It intentionally creates no Battle, Encounter or Contract; those remain available systems but are outside 14B story scope.

The proof is consumed by regression tests only. The historical campaign is mapped to v14, validated by both all domain validators and `HistoricalCampaignValidator`, canonically serialized, deserialized, reconstructed and projected through the main Presentation read models. Cross-domain references are checked after reload. UI/navigation state is intentionally absent from persistence and is rebuilt from gameplay truth.

The replay harness executes real Application services for production, trade, battle orders, world time and deterministic RNG. A direct run and the same run interrupted by save/load must produce the same trace and final payload fingerprint. Continuation coverage separately checks world clock, RNG, Battle, Encounter, Contract, Diplomacy and AI state.
