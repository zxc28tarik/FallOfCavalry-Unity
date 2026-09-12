# Implementation 5 — Trade / Production / Caravan Scope Lock

## Authority and base

- Binding authority: latest Implementation 5 instruction, then Parts 1–11 (especially Part 6/P10 economy decisions), then current repository docs.
- Authoritative base: `ede38665b935c285600b29e20ed460ac23f0b7b2` (`Implementation 4: READY`).
- Branch: `codex/impl-5-trade-production-caravan`.
- Part 6 confirms real production/stock/consumption/demand/price/transfer flow, integral/auditable money, real Caravan cargo, owner != manager, food-derived future supply bundles, and conservation-safe atomic transactions.

## IN SCOPE

- Typed `TradeGoodId`, immutable content-driven definitions, five authoritative categories and deterministic registry.
- Integral quantity, unit-weight and money representations; no floating-point economy state.
- Per-City authoritative stock, source-driven demand, partial consumption with explicit shortage, availability projection and market cash counterparty.
- Content-driven recipes with explicit positive input/output quantities and compatible active City V2 building kinds.
- Atomic production that consumes all real inputs before adding outputs; insufficient input makes no mutation.
- Deterministic price quote contract: content reference value plus explicit, auditable signed source adjustments. No hidden coefficients or automatic global formula.
- Persistent Caravan with typed identity, owner reference, real manager Character, optional Trade-branch representative assignment, origin/destination, real cargo, weight capacity, cash, lifecycle/location stage, optional route reference and route/risk input hooks.
- Atomic purchase/load and sale/unload transactions moving goods and money exactly once between City market and Caravan; auditable Caravan P&L.
- Merchant Clique and Trade Network invariants: neither owns stock/cargo; membership != assignment != ownership.
- Save schema v6, v5-to-v6 migration, continuous v1-to-v6 chain, hardcoded v5 fixture and full deterministic semantic roundtrip.

## OUT OF SCOPE

- Implementation 6 Diplomacy/Envoy/Report and later Army, Recruitment, Soldier, Equipment, Battle, World Map pathfinding, Encounter, bandit/war/AI simulation, UI/art and historical economy database.
- Population consumption rates, production cadence/yields, base prices, target stocks, price coefficients, transport/risk/tax/customs/spoilage percentages, caravan speed, profit targets or other unauthorized balance constants.
- Character wallet funding/withdrawal, House/State treasury transfer rules, guards/personnel, SupplyBundle execution, market-knowledge delay, naval trade, insurance or monopoly gameplay.
- Clique-owned goods, stock, warehouse, treasury or Caravan inventory; new Caravan Organization branch or Character subtype.

## AUTHORITATIVE ECONOMY RULES

- Production -> real City stock -> consumption/demand -> price inputs -> trade -> real Caravan cargo -> destination stock/sale.
- Definition != runtime stock. No infinite inventory, UI truth, random price noise or wall-clock authority.
- Stock/cargo/market cash cannot become negative; capacity is a real weight constraint.
- Production requires an active, compatible, non-removed building and sufficient real inputs.
- A transaction is all-or-nothing across goods, cargo, cash and accounting; no duplication or partial money-only result.
- Price is deterministic and explainable. With no authorized coefficient set, every non-base adjustment must be an explicit input tagged by source.
- Owner and manager are separate; Clique is not a permitted owner kind.
- Organization `Trade` branch is reused; no top-level Caravan branch is added.

## PUBLIC CONTRACTS

- `TradeGoodId`, `ProductionRecipeId`, `TradeRouteId`
- `TradeGoodDefinition`, `TradeGoodCategory`, `TradeGoodRegistry`
- `GoodsQuantity`, `MoneyAmount`, `TradeGoodStock`, `CityStockState`
- `DemandSourceState`, `CityDemandState`, `ConsumptionRequest/Result`, availability contracts
- `ProductionRecipeDefinition`, recipe line contracts, `ProductionService`
- `PriceAdjustment`, `PriceQuote`, `TradePriceRules`
- `CityMarketState`, `EconomyState`
- reused typed `CaravanId`; `EconomicOwnerRef`, `CaravanState`, `CaravanCargoState`, `CaravanRegistry`
- Caravan lifecycle/location/route/risk/accounting/representative contracts
- `TradeTransactionService`, `EconomyInvariantValidator`
- Economy save DTOs and `CampaignSaveV5ToV6Migration`

## CITY / CHARACTER / TRADE NETWORK INTEGRATION

- Markets and production resolve a real Implementation 4 `CityId`.
- Recipes resolve active City building definitions; RopeWorkshop remains absent and Butcher/Fishery remain FoodSupply.
- Caravan manager and representative are real Characters. A representative reference resolves an active Organization assignment in `OrganizationBranch.Trade`; it does not imply membership, ownership, location or loyalty.
- Caravan owner can be a typed Character, House or Organization reference. No Clique owner constructor exists.

## SAVE IMPACT

- Schema v5 -> v6.
- v6 persists good/recipe definitions, markets, stock, demand, Caravan identity/owner/manager/representative/origin/destination/cargo/capacity/cash/accounting/route/risk/lifecycle.
- v5-to-v6 preserves Character, Organization, House, Clique, Religion and City state and initializes Economy empty; it invents no goods, market or Caravan.

## DETERMINISM

- All registries and goods/source/cargo/adjustment collections use stable-ID or explicit enum/source ordering.
- Integral quantities/money avoid floating-point rounding. Checked arithmetic rejects overflow.
- No global random, wall clock, dictionary iteration dependency or implicit equal-score choice.

## TEST GATES

- Typed identities, duplicate rejection and canonical registry order.
- Non-negative stock/cargo/cash, zero floor and explicit shortage.
- Real recipe input consumption/output creation; insufficient/inactive/removed/incompatible paths make no mutation.
- Deterministic explicit price components and no hidden modifiers.
- Atomic City<->Caravan goods/money transfers, capacity and conservation/no-duplication.
- Valid owner/manager/representative/City references; Trade membership/assignment/ownership separation; Clique prohibitions.
- v1-to-v6, hardcoded v5, v6 semantic roundtrip, old-state preservation and deterministic serialization.
- Implementation 0–4 regression, Unity 6000.3.16f1, final-SHA CI, clean worktree and matching remote.

## OPEN DESIGN-AUTHORITY TOPICS

- Exact base prices, good unit weights/volumes for production content, production ratios/cadence and population consumption rates.
- Price/stock/demand/scarcity coefficients and stock targets.
- Transport, security, route risk, tax/customs, war/diplomacy, competition and wealth adjustment values.
- Caravan speed, operating costs, animal/cart capacity, guards, losses and SupplyBundle conversion.
- Character/House/State capital funding and settlement of Caravan profits.

They do not block a conservation-safe vertical slice because fixture values enter through content/explicit inputs rather than hidden production constants.

## Legacy classification

- KEEP: none of the runtime economy code unchanged.
- ADAPT: authoritative good vocabulary and the general stock/market concept only.
- REWRITE: typed goods, recipes, City markets, atomic transfers, cargo, ownership and persistence.
- REMOVE: legacy base prices/coefficients as authority, random drift, infinite/fake inventory, global mutable/localStorage/UI truth, string aliases, fixed Caravan rewards and automatic war/religion price multipliers.
- REFERENCE_ONLY: old goods lists, merchant/caravan encounters, world constants and UI for later content/balance research.
