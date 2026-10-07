# Full campaign integration proof

`IntegratedProofCampaignFactory` is production-located test/integration data marked `PROOF_ONLY`; it is not historical or balance authority and is not referenced by the executable Development bootstrap or `VerticalSliceCampaignFactory`. It remains a broad regression fixture containing every implemented aggregate, including Battle and Encounter/Contract state.

The executable `VerticalSliceCampaignFactory` independently composes `VERTICAL_SLICE_HISTORICAL-1648-09-01-r1`: eight researched/disclosed Characters, four City V2 states, economy/Caravan, diplomacy/report, a small retinue, persistent Soldiers/equipment, one AI controller and authored 14A geography. It intentionally creates no Battle, Encounter or Contract; those remain available systems but are outside 14B story scope.

The proof is consumed by regression tests only. The historical campaign is mapped to v14, validated by both all domain validators and `HistoricalCampaignValidator`, canonically serialized, deserialized, reconstructed and projected through the main Presentation read models. Cross-domain references are checked after reload. UI/navigation state is intentionally absent from persistence and is rebuilt from gameplay truth.

The replay harness executes real Application services for production, trade, battle orders, world time and deterministic RNG. A direct run and the same run interrupted by save/load must produce the same trace and final payload fingerprint. Continuation coverage separately checks world clock, RNG, Battle, Encounter, Contract, Diplomacy and AI state.

## Bounded evolving campaign soak

`CampaignSoakTests` runs 100/1, 1000/37 and 5000/113 step/save-interval cases. Each compares an uninterrupted campaign against a separately constructed campaign with reversed character/organization insertion order and real `CampaignSaveService` / `AtomicFileSaveStore` disk reloads. Every checkpoint validates both full save graphs and compares the exact canonical payload before and after reconstruction. Per-step RNG and command traces must agree.

The test executes existing production, trade purchase/sale, report dispatch/delivery, AI scheduling and authorized supply-action execution, Army supply consumption, and Battle Hold commands. A 37-step checkpoint can fall between report dispatch and delivery, testing pending-state continuation. Supply decisions use an explicitly `TEST_ONLY` 13-tick scheduling profile; no production AI policy, pricing, balance or definitions are changed. Added starting stock/cash is an explicit fixture budget, not newly generated gameplay resources.

Independent ledgers assert grain conservation including production input and consumed supply, flour output, total market/caravan cash, caravan accounting/capacity, stable aggregate counts, expected report/order history growth, current clock, and persistent mount/controller identity. A negative-oracle test deliberately creates one extra grain unit and one extra money unit and requires the conservation assertions to reject both. The older clock/RNG-only harness now records independently measured end counts as well as checking them.

Development results: 101/29/46 disk reloads in the respective cases; the 5000-step case executed 714 production cycles, 190 purchases, 80 sales, 384 AI supply decisions/transfers/consumptions, 135 report deliveries and 49 Battle Hold commands. Its 184 blocked trade attempts are precondition guards, not successful transactions or skipped tests. Replay fingerprints are expected to remain platform/culture-independent; timing and allocation figures are diagnostics without universal performance thresholds.

Run `Tools/Test-LongRunPipeline.ps1` for both the original three cases and these four new cases. Run the complete .NET and Unity EditMode suites as well; the pipeline alone is not full acceptance.

Scope limits: this is accelerated headless command replay on a small `PROOF_ONLY` graph, not a many-hour rendered-player soak or a claim that every campaign subsystem evolves. Caravan movement here tests its existing location-stage transitions, not geography/travel routing. Encounter, Contract, social and religious aggregates are preserved/validated but not actively advanced. Battle commands are replayed without combat/casualty resolution. Test fixture scheduling and budgets are not historical or balance authority. Save schema remains v14; no art, rig, texture, gameplay definition, or Implementation 15 work is included.
