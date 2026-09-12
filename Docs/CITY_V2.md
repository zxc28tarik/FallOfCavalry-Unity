# City V2 Foundation

A City is a persistent functional-area aggregate, not a UI scene or a list of thirty independent building slots. `CityDefinition` owns immutable content; `CityState` owns campaign state; `CityRegistry` owns deterministic `CityId` ordering.

Every City definition contains exactly nine areas: InnerCastle, Trade, InnCaravan, Housing, Military, Health, ProductionCraft, FoodSupply and SquareCulture. InnerCastle starts and remains Full. Other areas use Empty, Low, Half and Full. Empty areas cannot contain active buildings; active and locked buildings must exist in their compatible pool, and removed content cannot activate.

Building kinds encode the approved content vocabulary. RopeWorkshop does not exist. Butcher and Fishery map only to FoodSupply. Definitions may carry typed future effect tags, but Implementation 4 calculates no economic, health, administrative, military or combat modifier.

Infrastructure is a separate invisible service-state collection covering wells, fountains, cisterns, water channels, roads, crossings, drainage and waste channels. It is neither an area nor a building and has no invented cost/benefit formula.

Population is a non-negative count. Wealth, Order, Health and Security are typed but deliberately `Unassessed` until scales and source-based rules receive authority.

Kethüda is a `CityOfficialReference` to an active Organization assignment with the controlled `city-kethuda` role and a target matching the real City. The assigned Character must exist, live and not be captive. The assignment can be remote-capable and does not mutate location, loyalty, membership, House, Clique or Religion state. No maximum simultaneous Kethüda count is invented.

City religion profiles stay in `ReligionCampaignState`. `CityReligionIntegrationRules` resolves a City profile against a real City registry. Religion difference retains its zero automatic unrest, order, rebellion, war and combat semantics.
