#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FOC.Application.Battle;
using FOC.Application.Economy;
using FOC.Application.Save;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Random;
using FOC.Domain.Soldiers;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using FOC.Visuals.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class IntegrationHardeningTests
    {
        [Test]
        public void IntegratedProofCampaign_ContainsEveryFoundationSubsystemAndValidSaveGraph()
        {
            var campaign = IntegratedCampaignTestFixture.Create();
            var data = CampaignSaveMapper.ToSaveData(campaign);
            var validation = new CampaignSaveValidator().Validate(data);
            Assert.That(validation.IsValid, Is.True, string.Join(" | ", validation.Issues.Select(x => x.Code + ":" + x.Message)));
            Assert.That(data.Characters.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(data.Organizations, Is.Not.Empty); Assert.That(data.Houses, Is.Not.Empty); Assert.That(data.Cliques, Is.Not.Empty);
            Assert.That(data.Religions, Is.Not.Empty); Assert.That(data.Sects, Is.Not.Empty); Assert.That(data.Cities, Has.Count.EqualTo(2));
            Assert.That(data.TradeGoods, Is.Not.Empty); Assert.That(data.Caravans, Is.Not.Empty); Assert.That(data.Reports, Is.Not.Empty);
            Assert.That(data.Armies, Has.Count.EqualTo(2)); Assert.That(data.Soldiers, Has.Count.EqualTo(2)); Assert.That(data.EquipmentInstances, Is.Not.Empty);
            Assert.That(data.Battles, Has.Count.EqualTo(1)); Assert.That(data.Encounters, Has.Count.EqualTo(1)); Assert.That(data.Contracts, Has.Count.EqualTo(1)); Assert.That(data.AIControllers, Has.Count.EqualTo(1));
        }

        [Test]
        public void FullCampaignRoundTrip_IsCanonicalAndPreservesCrossDomainReferences()
        {
            var serializer = new CampaignSaveTextSerializer();
            var source = IntegratedCampaignTestFixture.Create();
            var first = serializer.Serialize(CampaignSaveMapper.ToSaveData(source));
            var read = serializer.Deserialize(first);
            Assert.That(read.Success, Is.True, read.Error);
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!);
            var second = serializer.Serialize(CampaignSaveMapper.ToSaveData(restored));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(SavePayloadFingerprint.Compute(second), Is.EqualTo(SavePayloadFingerprint.Compute(first)));
            Assert.That(restored.CampaignId, Is.EqualTo(source.CampaignId));
            Assert.That(restored.Cities.GetRequired(CityId.Create("city-home")).OrderedOfficials.Single().AssignmentId.Value, Is.EqualTo("kethuda-org-a"));
            Assert.That(restored.Economy.Caravans.GetRequired(CaravanId.Create("caravan-proof")).ManagerCharacterId.Value, Is.EqualTo("commander-a"));
            Assert.That(restored.Military.Armies.GetRequired(ArmyId.Create("army-a")).Commander!.CharacterId.Value, Is.EqualTo("commander-a"));
            Assert.That(restored.Soldiers.Soldiers.GetRequired(SoldierId.Create("soldier-a")).Loadout.Mount!.Value.Value, Is.EqualTo("equipment-horse-a"));
            Assert.That(restored.EncounterContracts.Encounters.GetRequired(EncounterId.Create("encounter-proof")).Context.Source.Id, Is.EqualTo("commander-a"));
            Assert.That(restored.AI.Controllers.GetRequired(AIControllerId.Create("controller-proof")).Owner.Id, Is.EqualTo("commander-b"));
        }

        [Test]
        public void SaveLoad_PreservesWorldClockRngBattleEncounterContractDiplomacyAndAIContinuation()
        {
            var serializer = new CampaignSaveTextSerializer();
            var direct = IntegratedCampaignTestFixture.Create();
            var loaded = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(direct))).Data!);
            Assert.That(loaded.Clock.Now, Is.EqualTo(direct.Clock.Now));
            Assert.That(loaded.Random.NextUInt64(), Is.EqualTo(direct.Random.NextUInt64()));
            direct.Clock.Advance(new WorldDuration(17)); loaded.Clock.Advance(new WorldDuration(17));
            Assert.That(loaded.Clock.Now, Is.EqualTo(direct.Clock.Now));
            Assert.That(loaded.Battles.Battles.GetRequired(BattleId.Create("battle-main")).Lifecycle, Is.EqualTo(direct.Battles.Battles.GetRequired(BattleId.Create("battle-main")).Lifecycle));
            Assert.That(loaded.EncounterContracts.Encounters.GetRequired(EncounterId.Create("encounter-proof")).RandomState, Is.EqualTo(direct.EncounterContracts.Encounters.GetRequired(EncounterId.Create("encounter-proof")).RandomState));
            Assert.That(loaded.EncounterContracts.Contracts.GetRequired(ContractId.Create("contract-proof")).Lifecycle, Is.EqualTo(ContractLifecycle.Active));
            Assert.That(loaded.Diplomacy.Information.GetRequired(FactionId.Create("faction-proof")).OrderedAvailableReports.Single().ArrivedAt, Is.EqualTo(new WorldTimestamp(5)));
            Assert.That(loaded.AI.Controllers.GetRequired(AIControllerId.Create("controller-proof")).RandomState, Is.EqualTo(direct.AI.Controllers.GetRequired(AIControllerId.Create("controller-proof")).RandomState));
        }

        [Test]
        public void Presentation_RebuildsAllMajorReadModelsAfterFullReloadWithoutSavedUiState()
        {
            var serializer = new CampaignSaveTextSerializer();
            var data = serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(IntegratedCampaignTestFixture.Create()))).Data!;
            var campaign = CampaignSaveMapper.ToRuntimeState(data);
            var refs = new[]
            {
                Ref(PresentationEntityKind.Faction,"faction-proof"), Ref(PresentationEntityKind.City,"city-home"), Ref(PresentationEntityKind.Character,"commander-a"),
                Ref(PresentationEntityKind.Organization,"org-a"), Ref(PresentationEntityKind.Army,"army-a"), Ref(PresentationEntityKind.Battle,"battle-main")
            };
            var viewer = new PresentationViewerContext(FactionId.Create("faction-proof"), refs[0], refs);
            var queries = new CampaignPresentationQueries(campaign);
            Assert.That(queries.BuildMap(viewer, new ProofOnlyMapPresentationDataProvider(Array.Empty<MapMarkerPresentation>())), Is.Not.Null);
            Assert.That(queries.BuildCity(viewer, CityId.Create("city-home")), Is.Not.Null);
            Assert.That(queries.BuildCharacter(viewer, CharacterId.Create("commander-a")), Is.Not.Null);
            Assert.That(queries.BuildOrganization(viewer, OrganizationId.Create("org-a")), Is.Not.Null);
            Assert.That(queries.BuildTrade(viewer, CityId.Create("city-home")), Is.Not.Null);
            Assert.That(queries.BuildArmy(viewer, ArmyId.Create("army-a")), Is.Not.Null);
            Assert.That(queries.BuildDiplomacy(viewer), Is.Not.Null);
            Assert.That(queries.BuildBattle(viewer, BattleId.Create("battle-main")), Is.Not.Null);
            Assert.That(queries.BuildReports(viewer).Reports, Has.Count.EqualTo(1));
            Assert.That(queries.BuildEncounterContracts(viewer).State.Current.Fields, Is.Not.Empty);
            Assert.That(typeof(CampaignSaveData).GetProperties().Select(x => x.Name), Does.Not.Contain("SelectedScreen").And.Not.Contain("NavigationHistory").And.Not.Contain("VisualCache"));
        }

        [Test]
        public void VisualSoldierPlan_RebuildsAfterFullReloadFromPersistentIdentityAndLoadout()
        {
            var serializer = new CampaignSaveTextSerializer();
            var source = IntegratedCampaignTestFixture.Create();
            var restored = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(source))).Data!);
            var before = PlanVisual(source);
            var after = PlanVisual(restored);
            Assert.That(after.SoldierId, Is.EqualTo(before.SoldierId));
            Assert.That(after.Signature, Is.EqualTo(before.Signature));
            Assert.That(after.AppearanceVariant, Is.EqualTo(before.AppearanceVariant));
            Assert.That(after.Modules.Select(x => x.AssetId.Value), Does.Contain("WPN_Sword_Proof").And.Contain("MNT_Horse_Proof"));
            Assert.That(typeof(CampaignSaveData).GetProperties().Select(x => x.Name), Does.Not.Contain("VisualCache"));
        }

        [Test]
        public void DeterministicReplay_DirectAndSaveBoundaryProduceSameTraceAndFinalFingerprint()
        {
            var direct = DeterministicReplayHarness.Run(IntegratedCampaignTestFixture.Create(), null);
            var split = DeterministicReplayHarness.Run(IntegratedCampaignTestFixture.Create(), 2);
            Assert.That(split.Trace, Is.EqualTo(direct.Trace));
            Assert.That(split.Fingerprint, Is.EqualTo(direct.Fingerprint));
        }

        [TestCase(100, 10)]
        [TestCase(500, 25)]
        [TestCase(2000, 50)]
        public void LongRun_SameSeedIsDeterministicBoundedAndSurvivesPeriodicReload(int steps, int saveEvery)
        {
            var first = LongRunCampaignHarness.Run(steps, saveEvery);
            var second = LongRunCampaignHarness.Run(steps, saveEvery);
            Assert.That(second.FinalFingerprint, Is.EqualTo(first.FinalFingerprint));
            Assert.That(second.SaveLoadCycles, Is.EqualTo(first.SaveLoadCycles));
            Assert.That(first.ValidationFailures, Is.Zero);
            Assert.That(first.StartCounts, Is.EqualTo(first.EndCounts));
            TestContext.Out.WriteLine($"LONG_RUN steps={steps} seed=130013 save_load_cycles={first.SaveLoadCycles} elapsed_ms={first.ElapsedMilliseconds:F3} allocated_bytes={first.AllocatedBytes} fingerprint={first.FinalFingerprint}");
        }

        [TestCase(10, "Small")]
        [TestCase(100, "Medium")]
        [TestCase(500, "Large")]
        public void ProductionFoundationBenchmark_RecordsRealMapSerializeValidateAndLoadWork(int characterCount, string tier)
        {
            var campaign = BenchmarkCampaign(characterCount);
            var serializer = new CampaignSaveTextSerializer();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            var data = CampaignSaveMapper.ToSaveData(campaign); var mapMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var validation = new CampaignSaveValidator().Validate(data); var validationMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var payload = serializer.Serialize(data); var serializeMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var read = serializer.Deserialize(payload); var deserializeMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var runtime = CampaignSaveMapper.ToRuntimeState(read.Data!); var reconstructMs = watch.Elapsed.TotalMilliseconds;
            Assert.That(validation.IsValid, Is.True); Assert.That(read.Success, Is.True); Assert.That(runtime.Characters.Count, Is.EqualTo(characterCount));
            TestContext.Out.WriteLine($"FOUNDATION_BENCHMARK tier={tier} characters={characterCount} bytes={System.Text.Encoding.UTF8.GetByteCount(payload)} map_ms={mapMs:F3} validate_ms={validationMs:F3} serialize_ms={serializeMs:F3} deserialize_ms={deserializeMs:F3} reconstruct_ms={reconstructMs:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread()-before}");
        }

        private static CampaignRuntimeState BenchmarkCampaign(int count)
        {
            var roster = new CharacterRoster();
            for (var index = 0; index < count; index++) roster.Add(CharacterTestFactory.Generated("benchmark-" + index, "Benchmark " + index));
            return new CampaignRuntimeState(StableId<CampaignTag>.Create("benchmark"), "0.13-dev", "PROOF_ONLY-benchmark", 13, 1, new WorldClock(new WorldTimestamp(1)), new SeededRandomSource(13), roster);
        }
        private static PresentationEntityRef Ref(PresentationEntityKind kind, string id) => new PresentationEntityRef(kind, id);

        private static VisualAssemblyPlan PlanVisual(CampaignRuntimeState campaign)
        {
            var soldier = campaign.Soldiers.Soldiers.GetRequired(SoldierId.Create("soldier-a"));
            var troop = campaign.Soldiers.Definitions.GetRequired(soldier.TroopDefinitionId);
            var equipment = campaign.Soldiers.Equipment.OrderedEquipment.ToDictionary(x => x.Id);
            var catalog = new VisualCatalog();
            catalog.Add(VisualAsset("BODY_Human_Proof", VisualAssetCategory.Body));
            catalog.Add(VisualAsset("HEAD_A_Proof", VisualAssetCategory.Head));
            catalog.Add(VisualAsset("CLTH_Light_Proof", VisualAssetCategory.Clothing));
            catalog.Add(VisualAsset("WPN_Sword_Proof", VisualAssetCategory.Weapon));
            catalog.Add(VisualAsset("MNT_Horse_Proof", VisualAssetCategory.Mount, RigKind.GenericMount));
            catalog.Add(new VisualProfile(VisualProfileId.Create("proof"), "standard-human", VisualAssetId.Create("BODY_Human_Proof"), VisualAssetId.Create("HEAD_A_Proof"), VisualAssetId.Create("CLTH_Light_Proof"), null, VisualQualityTier.Standard, "proof-only"));
            catalog.Add(new EquipmentVisualMapping(EquipmentDefinitionRef.Weapon(WeaponDefinitionId.Create("sword")), VisualAssetId.Create("WPN_Sword_Proof"), VisualSocket.RightHand, "standard-human"));
            catalog.Add(new EquipmentVisualMapping(EquipmentDefinitionRef.Mount(MountDefinitionId.Create("horse")), VisualAssetId.Create("MNT_Horse_Proof"), VisualSocket.Rider, "standard-human"));
            return new VisualSoldierPlanner(catalog).Plan(soldier, troop, equipment);
        }

        private static VisualAssetRecord VisualAsset(string id, VisualAssetCategory category, RigKind rig = RigKind.Humanoid) =>
            new VisualAssetRecord(VisualAssetId.Create(id), category, "Assets/FOC/Generated/" + id + ".prefab", rig, 3, 1, 1);
    }

    internal sealed class ReplayResult
    {
        public ReplayResult(IReadOnlyList<string> trace, string fingerprint) { Trace = trace; Fingerprint = fingerprint; }
        public IReadOnlyList<string> Trace { get; }
        public string Fingerprint { get; }
    }

    internal static class DeterministicReplayHarness
    {
        public static ReplayResult Run(CampaignRuntimeState campaign, int? saveBoundary)
        {
            var trace = new List<string>(); var serializer = new CampaignSaveTextSerializer();
            for (var index = 0; index < 4; index++)
            {
                if (saveBoundary == index) campaign = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign))).Data!);
                switch (index)
                {
                    case 0:
                        new ProductionService(campaign.Cities, campaign.Economy).Execute(CityId.Create("city-home"), ProductionRecipeId.Create("mill-proof")); trace.Add("0:production:success"); break;
                    case 1:
                        var good = campaign.Economy.Goods.GetRequired(TradeGoodId.Create("grain-proof"));
                        new TradeTransactionService(campaign.Cities, campaign.Economy).PurchaseAndLoad(CaravanId.Create("caravan-proof"), CityId.Create("city-home"), good.Id, 3, TradePriceRules.FormQuote(good, Array.Empty<PriceAdjustment>())); trace.Add("1:trade:success"); break;
                    case 2:
                        new BattleOrderCommandService(campaign).Issue(BattleId.Create("battle-main"), new BattleOrder(BattleOrderId.Create("replay-hold"), 0, BattleOrderKind.Hold, BattleSideId.Create("side-a"), CharacterId.Create("commander-a"), DeploymentGroupId.Create("deploy-a"))); trace.Add("2:battle-order:success"); break;
                    default:
                        campaign.Clock.Advance(new WorldDuration(7)); var draw = campaign.Random.NextUInt64(); trace.Add("3:clock-rng:" + campaign.Clock.Now.Ticks + ":" + draw); break;
                }
            }
            return new ReplayResult(trace.AsReadOnly(), SavePayloadFingerprint.Compute(serializer, CampaignSaveMapper.ToSaveData(campaign)));
        }
    }

    internal sealed class LongRunResult
    {
        public LongRunResult(string fingerprint, int cycles, int failures, string counts, double elapsed, long allocated) { FinalFingerprint=fingerprint; SaveLoadCycles=cycles; ValidationFailures=failures; StartCounts=counts; EndCounts=counts; ElapsedMilliseconds=elapsed; AllocatedBytes=allocated; }
        public string FinalFingerprint { get; } public int SaveLoadCycles { get; } public int ValidationFailures { get; } public string StartCounts { get; } public string EndCounts { get; } public double ElapsedMilliseconds { get; } public long AllocatedBytes { get; }
    }

    internal static class LongRunCampaignHarness
    {
        public static LongRunResult Run(int steps, int saveEvery)
        {
            var campaign = IntegratedCampaignTestFixture.Create(); var serializer = new CampaignSaveTextSerializer(); var validator = new CampaignSaveValidator(); var cycles=0; var failures=0;
            var startCounts = Counts(campaign); var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            for (var step = 1; step <= steps; step++)
            {
                campaign.Clock.Advance(new WorldDuration(1)); campaign.Random.NextUInt64();
                if (step % 10 == 0)
                {
                    var viewer = new PresentationViewerContext(FactionId.Create("faction-proof"), new PresentationEntityRef(PresentationEntityKind.Faction, "faction-proof"));
                    new CampaignPresentationQueries(campaign).BuildReports(viewer);
                }
                if (step % saveEvery == 0)
                {
                    var data = CampaignSaveMapper.ToSaveData(campaign); if (!validator.Validate(data).IsValid) failures++;
                    campaign = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(data)).Data!); cycles++;
                }
            }
            watch.Stop(); var final = CampaignSaveMapper.ToSaveData(campaign); if (!validator.Validate(final).IsValid) failures++;
            var result = new LongRunResult(SavePayloadFingerprint.Compute(serializer, final), cycles, failures, startCounts, watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread()-before);
            if (!StringComparer.Ordinal.Equals(startCounts, Counts(campaign))) throw new InvalidOperationException("Long-run entity-count drift detected.");
            return result;
        }
        private static string Counts(CampaignRuntimeState c) => $"characters={c.Characters.Count};cities={c.Cities.OrderedCities.Count};armies={c.Military.Armies.OrderedArmies.Count};soldiers={c.Soldiers.Soldiers.OrderedSoldiers.Count};reports={c.Diplomacy.Reports.OrderedReports.Count};encounters={c.EncounterContracts.Encounters.OrderedEncounters.Count};contracts={c.EncounterContracts.Contracts.OrderedContracts.Count};ai={c.AI.Controllers.OrderedControllers.Count}";
    }
}
