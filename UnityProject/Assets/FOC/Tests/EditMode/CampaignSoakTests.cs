#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FOC.Application.AI;
using FOC.Application.Battle;
using FOC.Application.Diplomacy;
using FOC.Application.Economy;
using FOC.Application.Military;
using FOC.Application.Save;
using FOC.Domain.AI;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    // Bounded, test-only command replay. Not a new campaign scheduler or balance model.
    public sealed class CampaignSoakTests
    {
        private static readonly CityId Home = CityId.Create("city-home");
        private static readonly CityId Destination = CityId.Create("city-other");
        private static readonly CaravanId Caravan = CaravanId.Create("caravan-proof");
        private static readonly TradeGoodId Grain = TradeGoodId.Create("grain-proof");
        private static readonly TradeGoodId Flour = TradeGoodId.Create("flour-proof");
        private static readonly ArmyId SupplyArmy = ArmyId.Create("army-b");
        private static readonly FactionId Recipient = FactionId.Create("faction-proof");
        private string _directory = string.Empty;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "foc-campaign-soak", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [TestCase(100, 1)]
        [TestCase(1000, 37)]
        [TestCase(5000, 113)]
        public void EvolvingCampaign_DiskReloadMatchesUninterruptedReplayAndConservesResources(int steps, int saveEvery)
        {
            var direct = CreateCampaign(false);
            var resumed = CreateCampaign(true);
            var directLedger = new ResourceLedger(direct);
            var resumedLedger = new ResourceLedger(resumed);
            var serializer = new CampaignSaveTextSerializer();
            var service = new CampaignSaveService(serializer, new AtomicFileSaveStore(_directory), new CampaignSaveValidator(), CampaignSaveDefaults.CreateMigrationPipeline());
            var definitions = CreateTestAIProfiles();
            var cycles = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            AssertSamePayload(direct, resumed, serializer, 0);
            resumed = Reload(service, resumed);
            cycles++;

            for (var step = 1; step <= steps; step++)
            {
                var firstTrace = ExecuteStep(direct, directLedger, definitions, step, steps);
                var secondTrace = ExecuteStep(resumed, resumedLedger, definitions, step, steps);
                Assert.That(secondTrace, Is.EqualTo(firstTrace), "Command/RNG trace drift at step " + step);
                directLedger.Verify(direct, step);
                resumedLedger.Verify(resumed, step);
                if (step % saveEvery == 0 || step == steps)
                {
                    AssertSamePayload(direct, resumed, serializer, step);
                    resumed = Reload(service, resumed);
                    cycles++;
                    resumedLedger.Verify(resumed, step);
                    AssertSamePayload(direct, resumed, serializer, step);
                }
            }

            watch.Stop();
            Assert.That(cycles, Is.EqualTo(1 + steps / saveEvery + (steps % saveEvery == 0 ? 0 : 1)));
            Assert.That(directLedger.Productions, Is.EqualTo(steps / 7));
            Assert.That(directLedger.Purchases, Is.GreaterThan(0));
            Assert.That(directLedger.Sales, Is.GreaterThan(0));
            Assert.That(directLedger.SupplyTransfers, Is.GreaterThan(0));
            Assert.That(directLedger.Consumed, Is.GreaterThan(0));
            Assert.That(directLedger.Decisions, Is.EqualTo((steps - 10) / 13 + 1));
            Assert.That(directLedger.Reports, Is.EqualTo(steps / 37));
            Assert.That(File.Exists(Path.Combine(_directory, "soak.focsave.tmp")), Is.False);
            Assert.That(service.Load("soak").RecoveryStatus, Is.EqualTo(SaveRecoveryStatus.LoadedCurrent));
            TestContext.Out.WriteLine($"EVOLVING_SOAK steps={steps} seed=130013 save_every={saveEvery} disk_reload_cycles={cycles} production={directLedger.Productions} purchases={directLedger.Purchases} sales={directLedger.Sales} supply_transfers={directLedger.SupplyTransfers} consumed={directLedger.Consumed} ai_decisions={directLedger.Decisions} reports={directLedger.Reports} delivered={directLedger.Delivered} battle_orders={steps / 101} blocked_trades={directLedger.BlockedTrades} elapsed_ms={watch.Elapsed.TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - before} fingerprint={SavePayloadFingerprint.Compute(serializer, CampaignSaveMapper.ToSaveData(resumed))}");
        }

        [Test]
        public void ResourceOracle_DetectsUncommandedGoodsAndMoneyCreation()
        {
            var campaign = CreateCampaign(false);
            var ledger = new ResourceLedger(campaign);
            ledger.Verify(campaign, 0);
            campaign.Economy.GetRequiredMarket(Home).Stock.Add(Grain, 1);
            Assert.Throws<AssertionException>(() => ledger.Verify(campaign, 0));
            campaign.Economy.GetRequiredMarket(Home).Stock.Remove(Grain, 1);
            campaign.Economy.GetRequiredMarket(Home).Credit(1);
            Assert.Throws<AssertionException>(() => ledger.Verify(campaign, 0));
        }

        private static CampaignRuntimeState CreateCampaign(bool reverse)
        {
            var campaign = IntegratedCampaignTestFixture.Create(reverse);
            // Test fixture budget only: no production definitions, prices or population are edited.
            campaign.Economy.GetRequiredMarket(Home).Stock.Add(Grain, 10000);
            campaign.Economy.GetRequiredMarket(Destination).Stock.Add(Grain, 2000);
            campaign.Economy.Caravans.GetRequired(Caravan).Credit(5000);
            return campaign;
        }

        private static CampaignRuntimeState Reload(CampaignSaveService service, CampaignRuntimeState campaign)
        {
            var saved = service.Save("soak", campaign);
            Assert.That(saved.Success, Is.True, saved.Error);
            var loaded = service.Load("soak");
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.RecoveryStatus, Is.EqualTo(SaveRecoveryStatus.LoadedCurrent));
            Assert.That(loaded.Data!.SaveVersion, Is.EqualTo(14));
            return CampaignSaveMapper.ToRuntimeState(loaded.Data);
        }

        private static void AssertSamePayload(CampaignRuntimeState direct, CampaignRuntimeState resumed, CampaignSaveTextSerializer serializer, int step)
        {
            var expected = CampaignSaveMapper.ToSaveData(direct);
            var actual = CampaignSaveMapper.ToSaveData(resumed);
            var validator = new CampaignSaveValidator();
            foreach (var data in new[] { expected, actual })
            {
                var validation = validator.Validate(data);
                Assert.That(validation.IsValid, Is.True, "Save graph invalid at step " + step + ": " + string.Join(" | ", validation.Issues.Select(x => x.Code + ":" + x.Message)));
            }
            Assert.That(serializer.Serialize(actual), Is.EqualTo(serializer.Serialize(expected)), "Canonical save drift at step " + step);
        }

        private static string ExecuteStep(CampaignRuntimeState campaign, ResourceLedger ledger, AIDefinitionCatalog definitions, int step, int steps)
        {
            campaign.Clock.Advance(new WorldDuration(1));
            var draw = campaign.Random.NextUInt64();
            if (step % 7 == 0)
            {
                new ProductionService(campaign.Cities, campaign.Economy).Execute(Home, ProductionRecipeId.Create("mill-proof"));
                ledger.Productions++;
            }

            var caravan = campaign.Economy.Caravans.GetRequired(Caravan);
            // Existing location-state transitions only, not a claim of geography/travel simulation.
            if (step == steps / 2) caravan.MarkInTransit();
            if (step == steps / 2 + 2) caravan.MarkAtDestination();
            if (step % 11 == 0)
            {
                var good = campaign.Economy.Goods.GetRequired(Grain);
                var quote = TradePriceRules.FormQuote(good, Array.Empty<PriceAdjustment>());
                var trade = new TradeTransactionService(campaign.Cities, campaign.Economy);
                if (caravan.LocationStage == CaravanLocationStage.AtOrigin && caravan.Cargo.UsedWeight(campaign.Economy.Goods) < caravan.WeightCapacity && caravan.CashBalance.Value >= 10)
                {
                    trade.PurchaseAndLoad(Caravan, Home, Grain, 1, quote);
                    ledger.Purchases++;
                }
                else if (caravan.LocationStage == CaravanLocationStage.AtDestination && caravan.Cargo.QuantityOf(Grain) > 0 && campaign.Economy.GetRequiredMarket(Destination).CashBalance.Value >= 10)
                {
                    trade.SellAndUnload(Caravan, Destination, Grain, 1, quote);
                    ledger.Sales++;
                }
                else ledger.BlockedTrades++;
            }

            if (step % 37 == 0)
            {
                var report = new ReportState(ReportId.Create("soak-report-" + step), ReportType.Military,
                    ReportSourceRef.Character(ReportSourceKind.Official, CharacterId.Create("commander-a")), Recipient,
                    ReportQuality.High, ReportDetailLevel.Detailed, campaign.Clock.Now,
                    new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Army, "army-b"), ReportObservationKind.ArmyEstimatedStrength, ObservationPrecision.Approximate, 5) });
                campaign.Diplomacy.Reports.Add(report);
                new ReportDeliveryService(campaign).Dispatch(report.Id, campaign.Clock.Now);
                ledger.Reports++;
            }
            if (step > 1 && (step - 1) % 37 == 0)
            {
                new ReportDeliveryService(campaign).Deliver(ReportId.Create("soak-report-" + (step - 1)), Recipient, campaign.Clock.Now);
                ledger.Delivered++;
            }

            var run = new AIDecisionScheduler().RunImportantSequential(campaign.Clock.Now, campaign.AI, definitions,
                new SupplySource(campaign), new SupplyCandidate(), new[] { new AllowSupply() }, new[] { new SupplyUtility() },
                proposal =>
                {
                    new AISupplyActionExecutor(campaign).Execute(proposal, new AISupplyCommand(Destination, SupplyArmy, Grain, 1));
                    ledger.SupplyTransfers++;
                });
            ledger.Decisions += run.Metrics.Controllers;
            if (step % 5 == 0) ledger.Consumed += new ArmySupplyService(campaign).Consume(SupplyArmy, Grain, 1).Consumed;
            if (step % 101 == 0)
            {
                var battle = campaign.Battles.Battles.GetRequired(BattleId.Create("battle-main"));
                new BattleOrderCommandService(campaign).Issue(battle.Id, new BattleOrder(BattleOrderId.Create("soak-hold-" + step),
                    battle.OrderedOrders.Count, BattleOrderKind.Hold, BattleSideId.Create("side-a"), CharacterId.Create("commander-a"), DeploymentGroupId.Create("deploy-a")));
            }
            return $"{draw}:{ledger.Productions}:{ledger.Purchases}:{ledger.Sales}:{ledger.BlockedTrades}:{ledger.Decisions}:{ledger.SupplyTransfers}:{ledger.Consumed}:{ledger.Reports}:{ledger.Delivered}";
        }

        private sealed class ResourceLedger
        {
            private readonly long _grain;
            private readonly long _flour;
            private readonly long _cash;
            private readonly string _entities;
            public ResourceLedger(CampaignRuntimeState campaign)
            {
                _grain = TotalGoods(campaign, Grain);
                _flour = TotalGoods(campaign, Flour);
                _cash = TotalCash(campaign);
                _entities = EntityCounts(campaign);
            }
            public int Productions { get; set; }
            public int Purchases { get; set; }
            public int Sales { get; set; }
            public int BlockedTrades { get; set; }
            public int Decisions { get; set; }
            public int SupplyTransfers { get; set; }
            public long Consumed { get; set; }
            public int Reports { get; set; }
            public int Delivered { get; set; }
            public void Verify(CampaignRuntimeState campaign, int step)
            {
                Assert.That(TotalGoods(campaign, Grain) + 2L * Productions + Consumed, Is.EqualTo(_grain), "Grain conservation at step " + step);
                Assert.That(TotalGoods(campaign, Flour), Is.EqualTo(_flour + Productions), "Production outputs at step " + step);
                Assert.That(TotalCash(campaign), Is.EqualTo(_cash), "Money conservation at step " + step);
                Assert.That(EntityCounts(campaign), Is.EqualTo(_entities), "Unexpected entity growth at step " + step);
                var caravan = campaign.Economy.Caravans.GetRequired(Caravan);
                Assert.That(caravan.Accounting.PurchaseCost.Value, Is.EqualTo(10L * Purchases));
                Assert.That(caravan.Accounting.SaleRevenue.Value, Is.EqualTo(10L * Sales));
                Assert.That(caravan.Cargo.UsedWeight(campaign.Economy.Goods), Is.LessThanOrEqualTo(caravan.WeightCapacity));
                Assert.That(campaign.Diplomacy.Reports.OrderedReports.Count, Is.EqualTo(1 + Reports));
                Assert.That(campaign.Diplomacy.Information.GetRequired(Recipient).OrderedAvailableReports.Count, Is.EqualTo(1 + Delivered));
                Assert.That(campaign.Battles.Battles.GetRequired(BattleId.Create("battle-main")).OrderedOrders.Count, Is.EqualTo(step / 101));
                Assert.That(campaign.Clock.Now.Ticks, Is.EqualTo(10 + step));
                Assert.That(campaign.Soldiers.Soldiers.GetRequired(SoldierId.Create("soldier-a")).Loadout.Mount!.Value.Value, Is.EqualTo("equipment-horse-a"));
                Assert.That(campaign.AI.Controllers.GetRequired(AIControllerId.Create("controller-proof")).Owner.Id, Is.EqualTo("commander-b"));
                foreach (var market in campaign.Economy.OrderedMarkets)
                {
                    Assert.That(market.CashBalance.Value, Is.GreaterThanOrEqualTo(0));
                    foreach (var good in campaign.Economy.Goods.OrderedGoods) Assert.That(market.Stock.QuantityOf(good.Id), Is.GreaterThanOrEqualTo(0));
                }
            }
            private static long TotalGoods(CampaignRuntimeState campaign, TradeGoodId good) =>
                campaign.Economy.OrderedMarkets.Sum(x => x.Stock.QuantityOf(good)) + campaign.Economy.Caravans.OrderedCaravans.Sum(x => x.Cargo.QuantityOf(good)) + campaign.Military.Armies.OrderedArmies.Sum(x => x.Supply.QuantityOf(good));
            private static long TotalCash(CampaignRuntimeState campaign) => campaign.Economy.OrderedMarkets.Sum(x => x.CashBalance.Value) + campaign.Economy.Caravans.OrderedCaravans.Sum(x => x.CashBalance.Value);
            private static string EntityCounts(CampaignRuntimeState c) => $"{c.Characters.Count}:{c.Cities.OrderedCities.Count}:{c.Military.Armies.OrderedArmies.Count}:{c.Soldiers.Soldiers.OrderedSoldiers.Count}:{c.Soldiers.Equipment.OrderedEquipment.Count}:{c.Organizations.OrderedOrganizations.Count}:{c.Houses.OrderedHouses.Count}:{c.Cliques.OrderedCliques.Count}:{c.EncounterContracts.Encounters.OrderedEncounters.Count}:{c.EncounterContracts.Contracts.OrderedContracts.Count}:{c.AI.Controllers.OrderedControllers.Count}";
        }

        private static AIDefinitionCatalog CreateTestAIProfiles()
        {
            var catalog = new AIDefinitionCatalog();
            const string marker = "TEST_ONLY-campaign-soak";
            catalog.Add(new AIPriorityProfile(AIPriorityProfileId.Create("priority-proof"), Array.Empty<AIProfileWeight>(), marker));
            catalog.Add(new CharacterAIDecisionProfile(CharacterAIProfileId.Create("character-profile-proof"), Array.Empty<AIProfileWeight>(), marker));
            catalog.Add(new AIDecisionQualityProfile(AIDecisionQualityProfileId.Create("quality-proof"), 1, 1, 1, marker));
            catalog.Add(new AISchedulingProfile(AISchedulingProfileId.Create("schedule-proof"), new WorldDuration(13), 1, Array.Empty<CharacterImportance>(), marker));
            return catalog;
        }

        private sealed class SupplySource : IAIScheduledDecisionSource
        {
            private readonly CampaignRuntimeState _campaign;
            public SupplySource(CampaignRuntimeState campaign) => _campaign = campaign;
            public bool CanAggregate(AIControllerState controller, AISchedulingProfile profile) => false;
            public string AggregationKey(AIControllerState controller) => throw new NotSupportedException();
            public AIDecisionContext BuildIndividual(AIControllerState controller, WorldTimestamp now) =>
                new AIPerceptionContextBuilder(_campaign).BuildCharacter(CharacterId.Create(controller.Owner.Id), Recipient,
                    new[] { new AIOwnFact("supply.available", _campaign.Economy.GetRequiredMarket(Destination).Stock.QuantityOf(Grain), "TEST_ONLY-authorized-supply-stock") });
            public AIDecisionContext BuildShared(IReadOnlyList<AIControllerState> controllers, WorldTimestamp now) => throw new NotSupportedException();
            public AIDecisionContext MaterializeShared(AIDecisionContext shared, AIDecisionOwnerRef owner) => throw new NotSupportedException();
        }
        private sealed class SupplyCandidate : IAIDecisionCandidateProvider
        {
            public IEnumerable<AIActionCandidate> Generate(AIDecisionContext context)
            {
                if (context.TryGetOwnFact("supply.available", out var fact) && fact.Value > 0)
                    yield return new AIActionCandidate(AICandidateId.Create("soak-supply"), AIDecisionDomain.MilitaryLogistics,
                        AIActionPolicyId.Create("soak-supply-policy"), "TEST_ONLY-supply", AITargetRef.Army(SupplyArmy));
            }
        }
        private sealed class AllowSupply : IAIEligibilityPolicy
        {
            public AIEligibilityResult Evaluate(AIDecisionContext context, AIActionCandidate candidate) => AIEligibilityResult.Allow();
        }
        private sealed class SupplyUtility : IAIUtilityPolicy
        {
            public IEnumerable<AIUtilityContribution> Evaluate(AIDecisionContext context, AIActionCandidate candidate)
            {
                yield return new AIUtilityContribution(AIUtilityFactorId.Create("soak-supply-factor"), 1, "TEST_ONLY-supply-utility");
            }
        }
    }
}
