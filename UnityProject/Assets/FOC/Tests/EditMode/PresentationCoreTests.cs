using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.Battle;
using FOC.Application.Diplomacy;
using FOC.Application.EncountersContracts;
using FOC.Application.Save;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Military;
using FOC.Domain.Organizations;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class PresentationCoreTests
    {
        [Test] public void TypedReferences_KeepEntityKindsDistinct() { var city = new PresentationEntityRef(PresentationEntityKind.City, "same"); var army = new PresentationEntityRef(PresentationEntityKind.Army, "same"); Assert.That(city, Is.Not.EqualTo(army)); }
        [Test] public void DisabledAction_RequiresReason() { Assert.Throws<ArgumentException>(() => new PresentationActionDescriptor("a", "k", false, "", new PresentationEntityRef(PresentationEntityKind.City, "c"), PresentationConfirmationPolicy.None, "adapter")); }
        [Test] public void UnavailableSection_RequiresReason() { Assert.Throws<ArgumentException>(() => new PresentationSection("k", PresentationAvailability.Unknown)); }
        [Test] public void DefaultTrend_IsExplicitlyInsufficientHistory() { Assert.That(PresentationTrend.InsufficientHistory.Availability, Is.EqualTo(PresentationAvailability.InsufficientHistory)); }

        [Test]
        public void Navigator_BackForwardAndDuplicateRouteAreDeterministic()
        {
            using var navigator = new PresentationNavigator(); var changes = 0; navigator.Changed += _ => changes++;
            navigator.Navigate(new PresentationRoute(PresentationScreenId.Map)); navigator.Navigate(new PresentationRoute(PresentationScreenId.City)); navigator.Navigate(new PresentationRoute(PresentationScreenId.City));
            Assert.That(changes, Is.EqualTo(2)); Assert.That(navigator.Back(), Is.True); Assert.That(navigator.Current!.Value.Screen, Is.EqualTo(PresentationScreenId.Map)); Assert.That(navigator.Forward(), Is.True);
        }

        [Test]
        public void ViewModel_DisposeRemovesNavigationSubscription()
        {
            using var navigator = new PresentationNavigator(); var source = new CountingSource();
            var view = new PresentationShellViewModel(navigator, source); Assert.That(navigator.SubscriptionCount, Is.EqualTo(1)); view.Dispose(); Assert.That(navigator.SubscriptionCount, Is.Zero);
        }

        [Test]
        public void StableList_SearchAndEqualSortPreserveInsertionOrder()
        {
            var list = new StablePresentationList<string>(new[] { "beta-1", "alpha", "beta-2" }, x => x, (a, b) => a.StartsWith("beta", StringComparison.Ordinal).CompareTo(b.StartsWith("beta", StringComparison.Ordinal)));
            Assert.That(list.Filter("beta"), Is.EqualTo(new[] { "beta-1", "beta-2" }));
        }

        [TestCase(100)] [TestCase(500)] [TestCase(1000)] [TestCase(5000)]
        public void VirtualizedWindow_BoundsLiveRows(int count)
        {
            var data = Enumerable.Range(0, count).ToList(); var window = new VirtualizedListWindow<int>(data, 24, 4);
            Assert.That(window.GetWindow(count / 2).Count, Is.LessThanOrEqualTo(32)); Assert.That(window.MaximumLiveRows, Is.LessThanOrEqualTo(32));
        }

        [TestCase(1366, 768, PresentationLayoutClass.CompactDesktop)]
        [TestCase(1920, 1080, PresentationLayoutClass.StandardDesktop)]
        [TestCase(2560, 1440, PresentationLayoutClass.WideDesktop)]
        [TestCase(3440, 1440, PresentationLayoutClass.UltraWideDesktop)]
        public void ResponsivePolicy_CoversDesktopTargets(int width, int height, PresentationLayoutClass expected) => Assert.That(PresentationLayoutPolicy.Resolve(width, height).Kind, Is.EqualTo(expected));

        [Test] public void CityAndOrganizationAuthorityCountsRemainExact() { Assert.That(Enum.GetValues(typeof(CityAreaType)).Length, Is.EqualTo(9)); Assert.That(Enum.GetValues(typeof(OrganizationBranch)).Cast<OrganizationBranch>(), Is.EqualTo(new[] { OrganizationBranch.Party, OrganizationBranch.Household, OrganizationBranch.Army, OrganizationBranch.Settlements, OrganizationBranch.Estates, OrganizationBranch.Production, OrganizationBranch.Trade, OrganizationBranch.Diplomacy })); }
        [Test] public void ContractCategoriesRemainExact() => Assert.That(Enum.GetValues(typeof(ContractCategory)).Length, Is.EqualTo(4));

        [Test]
        public void SaveSchema_RemainsFourteenAndHasNoPresentationState()
        {
            Assert.That(CampaignSaveData.CurrentSaveVersion, Is.EqualTo(14));
            Assert.That(typeof(CampaignSaveData).GetProperties().Any(x => x.Name.IndexOf("Presentation", StringComparison.OrdinalIgnoreCase) >= 0 || x.Name.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void DomainAssembly_DoesNotReferencePresentation()
        {
            Assert.That(typeof(CampaignRuntimeState).Assembly.GetReferencedAssemblies().Any(x => x.Name != null && x.Name.StartsWith("FOC.Presentation", StringComparison.Ordinal)), Is.False);
            Assert.That(typeof(ScreenPresentationState).Assembly.GetReferencedAssemblies().Any(x => StringComparer.Ordinal.Equals(x.Name, "UnityEngine")), Is.False);
        }

        [Test]
        public void ProofMapProvider_RejectsUnmarkedCoordinates()
        {
            var marker = new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.City, "c"), "city", .5f, .5f, PresentationKnowledge.ExactSelf, false);
            Assert.Throws<InvalidOperationException>(() => new ProofOnlyMapPresentationDataProvider(new[] { marker }));
        }

        [Test]
        public void ForeignArmyTruth_DoesNotLeakUntilDeliveredReport()
        {
            var actor = FactionId.Create("player"); var campaign = Runtime(actor); var armyId = ArmyId.Create("foreign-army");
            var owner = ArmyOwnerRef.MercenaryCompany("foreign-company"); var army = new ArmyState(armyId, "Hidden Army", owner, owner, ArmyLocation.AtCamp(new WorldPosition(1, 2)));
            campaign.Military.Armies.Add(army);
            var viewer = new PresentationViewerContext(actor, new PresentationEntityRef(PresentationEntityKind.Faction, actor.Value));
            var queries = new CampaignPresentationQueries(campaign);
            var before = queries.BuildArmy(viewer, armyId).State;
            army.SetLifecycle(ArmyLifecycle.Active);
            var afterHiddenMutation = queries.BuildArmy(viewer, armyId).State;
            Assert.That(before.Current.Availability, Is.EqualTo(PresentationAvailability.Unknown));
            Assert.That(afterHiddenMutation.Current.Availability, Is.EqualTo(PresentationAvailability.Unknown));

            var report = DeliveredArmyReport(actor, armyId, 42);
            campaign.Diplomacy.Reports.Add(report);
            campaign.Diplomacy.Information.GetRequired(actor).RestoreAvailable(report);
            var afterReport = queries.BuildArmy(viewer, armyId).State;
            Assert.That(afterReport.Current.Availability, Is.EqualTo(PresentationAvailability.Available));
            Assert.That(afterReport.Current.Fields.Single().DisplayValue, Is.EqualTo("~42"));
            Assert.That(afterReport.Current.Fields.Single().Knowledge.Precision, Is.EqualTo(PresentationPrecision.Approximate));
            Assert.That(afterReport.Current.Fields.Single().Knowledge.Availability, Is.EqualTo(PresentationAvailability.Stale));
        }

        [Test]
        public void ReportsScreen_UsesActorInformationNotGlobalUndeliveredRegistry()
        {
            var actor = FactionId.Create("player"); var campaign = Runtime(actor); var armyId = ArmyId.Create("foreign-army");
            var undelivered = new ReportState(ReportId.Create("pending"), ReportType.Military, ReportSourceRef.Character(ReportSourceKind.Official, FOC.Domain.Characters.CharacterId.Create("source")), actor, ReportQuality.Low, ReportDetailLevel.Summary, new WorldTimestamp(15), new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Army, armyId.Value), ReportObservationKind.ArmyEstimatedStrength, ObservationPrecision.Unknown) });
            campaign.Diplomacy.Reports.Add(undelivered);
            var viewer = new PresentationViewerContext(actor, new PresentationEntityRef(PresentationEntityKind.Faction, actor.Value));
            Assert.That(new CampaignPresentationQueries(campaign).BuildReports(viewer).Reports, Is.Empty);
        }

        [Test]
        public void InvalidRepresentativeCommands_AreRejectedThroughApplicationBoundaries()
        {
            var actor = FactionId.Create("player"); var campaign = Runtime(actor);
            var battle = new BattleOrderPresentationCommandAdapter(new BattleOrderCommandService(campaign));
            var battleOrder = new FOC.Domain.Battle.BattleOrder(BattleOrderId.Create("order"), 0, FOC.Domain.Battle.BattleOrderKind.Hold, BattleSideId.Create("side"), FOC.Domain.Characters.CharacterId.Create("issuer"), DeploymentGroupId.Create("group"));
            Assert.That(battle.Issue(BattleId.Create("missing"), battleOrder).MessageKey, Is.EqualTo("presentation.action.target-unavailable"));
            var encounter = new EncounterPresentationCommandAdapter(new EncounterService(campaign, ProofEncounterContractContent.Encounters()));
            Assert.That(encounter.Engage(EncounterId.Create("missing")).MessageKey, Is.EqualTo("presentation.action.target-unavailable"));
            var contract = new ContractPresentationCommandAdapter(new ContractService(campaign, ProofEncounterContractContent.Contracts()));
            Assert.That(contract.Accept(ContractId.Create("missing")).MessageKey, Is.EqualTo("presentation.action.target-unavailable"));
        }

        [Test]
        public void CommandBindingRegistry_DispatchesExactlyOneRegisteredApplicationAction()
        {
            using var registry = new PresentationCommandBindingRegistry(); var executions = 0;
            registry.Register("contract.accept:c", () => { executions++; return PresentationActionResult.Success(); });
            var action = new PresentationActionDescriptor("contract.accept:c", "accept", true, string.Empty, new PresentationEntityRef(PresentationEntityKind.Contract, "c"), PresentationConfirmationPolicy.None, "contract-command-adapter");
            Assert.That(registry.Dispatch(action).Succeeded, Is.True); Assert.That(executions, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => registry.Register("contract.accept:c", () => PresentationActionResult.Success()));
        }

        [Test]
        public void MajorCampaignQueries_BuildAllAuthoritativePresentationModules()
        {
            var actor = FactionId.Create("player"); var campaign = Runtime(actor);
            var city = CityTestFactory.State("city"); campaign.Cities.Add(city); campaign.Economy.AddMarket(new CityMarketState(city.Id, 10));
            var character = CharacterTestFactory.Named("character"); campaign.Characters.Add(character);
            var organization = new OrganizationState(OrganizationId.Create("organization"), "Organization"); campaign.Organizations.Add(organization);
            var owner = ArmyOwnerRef.MercenaryCompany("company"); var army = new ArmyState(ArmyId.Create("army"), "Army", owner, owner, ArmyLocation.AtCamp(new WorldPosition(0, 0))); campaign.Military.Armies.Add(army);
            var sideA = new BattleSideState(BattleSideId.Create("a"), CharacterId.Create("commander-a"), new[] { new BattleParticipantSnapshot(ArmyId.Create("army-a"), CharacterId.Create("commander-a"), Array.Empty<BattleUnitSnapshot>(), Array.Empty<KeyValuePair<TradeGoodId, long>>()) });
            var sideB = new BattleSideState(BattleSideId.Create("b"), CharacterId.Create("commander-b"), new[] { new BattleParticipantSnapshot(ArmyId.Create("army-b"), CharacterId.Create("commander-b"), Array.Empty<BattleUnitSnapshot>(), Array.Empty<KeyValuePair<TradeGoodId, long>>()) });
            var sectors = new BattleSectorGraph(); sectors.Add(new BattleSector(BattleSectorId.Create("center"), null, new[] { sideA.Id, sideB.Id }));
            var battle = new BattleState(BattleId.Create("battle"), campaign.Clock.Now, new RandomState(1, 0), new[] { sideA, sideB }, sectors); campaign.Battles.Battles.Add(battle);
            var exact = new[] { new PresentationEntityRef(PresentationEntityKind.City, city.Id.Value), new PresentationEntityRef(PresentationEntityKind.Character, character.Id.Value), new PresentationEntityRef(PresentationEntityKind.Organization, organization.Id.Value), new PresentationEntityRef(PresentationEntityKind.Army, army.Id.Value), new PresentationEntityRef(PresentationEntityKind.Battle, battle.Id.Value) };
            var viewer = new PresentationViewerContext(actor, new PresentationEntityRef(PresentationEntityKind.Faction, actor.Value), exact);
            var queries = new CampaignPresentationQueries(campaign);
            var markers = new ProofOnlyMapPresentationDataProvider(new[] { new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.City, city.Id.Value), "city", .5f, .5f, PresentationKnowledge.ExactSelf, true) });
            var models = new PresentationReadModel[] { queries.BuildMap(viewer, markers), queries.BuildCity(viewer, city.Id), queries.BuildCharacter(viewer, character.Id), queries.BuildOrganization(viewer, organization.Id), queries.BuildTrade(viewer, city.Id), queries.BuildArmy(viewer, army.Id), queries.BuildDiplomacy(viewer), queries.BuildBattle(viewer, battle.Id), queries.BuildReports(viewer), queries.BuildEncounterContracts(viewer) };
            Assert.That(models.Select(x => x.State.ScreenId), Is.EqualTo(new[] { PresentationScreenId.Map, PresentationScreenId.City, PresentationScreenId.Character, PresentationScreenId.Organization, PresentationScreenId.Trade, PresentationScreenId.Army, PresentationScreenId.Diplomacy, PresentationScreenId.Battle, PresentationScreenId.Reports, PresentationScreenId.EncounterContract }));
            Assert.That(models.All(x => x.State.Trend.Availability == PresentationAvailability.InsufficientHistory && x.State.WhyAvailability != PresentationAvailability.Available), Is.True);
            Assert.That(((CityReadModel)models[1]).State.Current.Fields.Count(x => x.LabelKey.StartsWith("presentation.city.area.", StringComparison.Ordinal)), Is.EqualTo(9));
        }

        private static CampaignRuntimeState Runtime(FactionId actor)
        {
            var campaign = new CampaignRuntimeState(StableId<CampaignTag>.Create("presentation-campaign"), "0.12", "presentation-1", 12, 1, new WorldClock(new WorldTimestamp(20)), new SeededRandomSource(12));
            campaign.Diplomacy.Actors.Add(new DiplomaticActorState(actor, "Player"));
            campaign.Diplomacy.Information.Add(new ActorInformationState(actor));
            return campaign;
        }

        private static ReportState DeliveredArmyReport(FactionId actor, ArmyId armyId, long estimate) => new ReportState(
            ReportId.Create("delivered-army"), ReportType.Military,
            ReportSourceRef.Character(ReportSourceKind.Official, FOC.Domain.Characters.CharacterId.Create("source")), actor,
            ReportQuality.Medium, ReportDetailLevel.Standard, new WorldTimestamp(5),
            new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Army, armyId.Value), ReportObservationKind.ArmyEstimatedStrength, ObservationPrecision.Approximate, estimate) },
            CommunicationStatus.Delivered, new WorldTimestamp(7), new WorldTimestamp(10));

        private sealed class CountingSource : IPresentationScreenSource
        {
            public ScreenPresentationState Get(PresentationRoute route) => new ScreenPresentationState(route.Screen, "title", route.Subject, new PresentationSection("current", PresentationAvailability.Available), PresentationTrend.InsufficientHistory, PresentationAvailability.Unavailable, null, "why.unavailable", null, null, null);
        }
    }
}
