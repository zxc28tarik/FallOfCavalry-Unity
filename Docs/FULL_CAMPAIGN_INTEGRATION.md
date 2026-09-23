# Full campaign integration proof

`IntegratedProofCampaignFactory` is production-located development data marked `PROOF_ONLY`; it is not historical or balance authority. It constructs one deterministic campaign containing named Characters and relations, Organizations/assignments, House/Clique, Religion/Sect profiles, two complete City V2 aggregates, production/stock/demand/trade/caravan state, diplomatic relation/report/information, Armies/recruitment/supply, persistent Soldiers/equipment/mount/ammunition, an active multi-sector Battle, Encounter/Contract state and an AI controller.

The proof is consumed by tests and by the Development player bootstrap. It is mapped to v12, validated, canonically serialized, deserialized, reconstructed and projected through every major Presentation read model. Cross-domain references are checked after reload. UI/navigation state is intentionally absent from persistence and is rebuilt from gameplay truth.

The replay harness executes real Application services for production, trade, battle orders, world time and deterministic RNG. A direct run and the same run interrupted by save/load must produce the same trace and final payload fingerprint. Continuation coverage separately checks world clock, RNG, Battle, Encounter, Contract, Diplomacy and AI state.
