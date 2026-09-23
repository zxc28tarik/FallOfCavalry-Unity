using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FOC.Application.AI;
using FOC.Application.Save;
using FOC.Application.Characters;
using FOC.Domain.AI;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.Cities;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class AIDecisionMakingTests
    {
        [Test]
        public void TypedOwnerAndTargetReferencesRemainDistinctFromGameplayOwnership()
        {
            var character=AIDecisionOwnerRef.Character(CharacterId.Create("character-ai"));
            var faction=AIDecisionOwnerRef.Faction(FactionId.Create("faction-ai"));
            Assert.That(character.Kind,Is.EqualTo(AIDecisionOwnerKind.Character));
            Assert.That(faction.Kind,Is.EqualTo(AIDecisionOwnerKind.Faction));
            Assert.That(AITargetRef.Army(ArmyId.Create("army-target")).Kind,Is.EqualTo(AITargetKind.Army));
            Assert.That(typeof(AIDecisionOwnerRef),Is.Not.EqualTo(typeof(FOC.Domain.Military.ArmyOwnerRef)));
        }

        [Test]
        public void DecisionContextCannotContainCampaignRuntimeTruth()
        {
            Assert.That(typeof(AIDecisionContext).GetProperties().Any(x=>x.PropertyType==typeof(CampaignRuntimeState)),Is.False);
            Assert.That(typeof(AIDecisionContext).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Any(x=>x.FieldType==typeof(CampaignRuntimeState)),Is.False);
        }

        [Test]
        public void UtilityIsDeterministicExplainableAndUsesStableCandidateTieBreak()
        {
            var context=Context("character-a");var engine=new AIDecisionEngine();var candidates=new[]{Candidate("candidate-b",ProofAIContent.MilitaryFactorId),Candidate("candidate-a",ProofAIContent.MilitaryFactorId)};
            var catalog=ProofAIContent.CreateCatalog();var before=new SeededRandomSource(99).CaptureState();
            var first=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(2,2)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));
            var second=engine.Decide(context,candidates.Reverse(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(2,2)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));
            Assert.That(first.Proposal!.Candidate.Id.Value,Is.EqualTo("candidate-a"));
            Assert.That(second.Proposal!.Candidate.Id,Is.EqualTo(first.Proposal.Candidate.Id));
            Assert.That(first.Trace.TieBreakReason,Is.EqualTo("stable-candidate-id"));
            Assert.That(first.Proposal.Contributions,Has.Count.EqualTo(1));
            Assert.That(first.Proposal.Contributions[0].ReasonKey,Is.EqualTo("proof.utility"));
            Assert.That(before.State,Is.EqualTo(new SeededRandomSource(99).CaptureState().State));
        }

        [Test]
        public void HardInvalidCandidateIsRejectedBeforeUtilityEvaluation()
        {
            var utility=new CountingUtilityPolicy();var catalog=ProofAIContent.CreateCatalog();var result=new AIDecisionEngine().Decide(Context("character-a"),new[]{Candidate("candidate-denied",ProofAIContent.MilitaryFactorId)},new[]{new RejectPolicy()},new[]{utility},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));
            Assert.That(result.Proposal,Is.Null);Assert.That(result.Trace.Rejected.Single().Reason,Is.EqualTo("hard-rule"));Assert.That(utility.Calls,Is.Zero);
        }

        [Test]
        public void FactionPriorityAndCharacterProfileCanReorderOnlyLegalCandidates()
        {
            var catalog=ProofAIContent.CreateCatalog();var candidates=new[]{Candidate("military",ProofAIContent.MilitaryFactorId),Candidate("caution",ProofAIContent.CautionFactorId)};var utility=new CandidateUtilityPolicy(0,3);var engine=new AIDecisionEngine();var context=Context("character-a");
            var neutral=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{utility},catalog.Priority(ProofAIContent.NeutralPriorityId),null,catalog.Quality(ProofAIContent.AdvancedQualityId));
            var strategic=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{utility},catalog.Priority(ProofAIContent.MilitaryPriorityId),null,catalog.Quality(ProofAIContent.AdvancedQualityId));
            var personal=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{utility},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.BoldCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));
            Assert.That(neutral.Proposal!.Candidate.Id.Value,Is.EqualTo("caution"));Assert.That(strategic.Proposal!.Candidate.Id.Value,Is.EqualTo("military"));Assert.That(personal.Proposal!.Candidate.Id.Value,Is.EqualTo("military"));
            Assert.That(neutral.Trace.Rejected,Is.Empty);Assert.That(strategic.Trace.Rejected,Is.Empty);Assert.That(personal.Trace.Rejected,Is.Empty);
        }

        [Test]
        public void DifficultyChangesEvaluationBreadthButNotInformationResourcesOrLegality()
        {
            var catalog=ProofAIContent.CreateCatalog();var context=new AIDecisionContext(AIDecisionOwnerRef.Character(CharacterId.Create("character-a")),new WorldTimestamp(10),new[]{new AIOwnFact("money",25,"real-resource")},new[]{Known("report-1",5,7)});var candidates=Enumerable.Range(0,4).Select(x=>Candidate("candidate-"+x,ProofAIContent.MilitaryFactorId)).ToList();var engine=new AIDecisionEngine();
            var basic=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.BasicQualityId));
            var advanced=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));
            Assert.That(basic.Trace.Evaluated,Has.Count.EqualTo(1));Assert.That(advanced.Trace.Evaluated,Has.Count.EqualTo(4));
            Assert.That(basic.Trace.Information.Select(x=>x.ReportId),Is.EqualTo(advanced.Trace.Information.Select(x=>x.ReportId)));
            Assert.That(context.TryGetOwnFact("money",out var money)&&money.Value==25,Is.True);
            Assert.That(basic.Trace.Rejected.All(x=>x.Reason=="decision-quality-candidate-budget"),Is.True);
        }

        [Test]
        public void HiddenForeignTruthMutationDoesNotChangePerceptionUntilDeliveredReport()
        {
            var campaign=PerceptionCampaign();var own=FactionId.Create("faction-own");var enemy=FactionId.Create("faction-enemy");var builder=new AIPerceptionContextBuilder(campaign);
            var before=builder.BuildFaction(own);campaign.Diplomacy.Actors.GetRequired(enemy).Dissolve();var afterHiddenMutation=builder.BuildFaction(own);
            Assert.That(afterHiddenMutation.OrderedKnownForeignInformation,Is.EqualTo(before.OrderedKnownForeignInformation));
            var report=new ReportState(ReportId.Create("report-enemy"),ReportType.Diplomatic,ReportSourceRef.Character(ReportSourceKind.Envoy,CharacterId.Create("character-observer")),own,ReportQuality.Medium,ReportDetailLevel.Standard,new WorldTimestamp(2),new[]{new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Faction,enemy.Value),ReportObservationKind.DiplomaticDisposition,ObservationPrecision.Qualitative,qualitative:QualitativeObservation.Worsening)});
            campaign.Diplomacy.Reports.Add(report);report.Dispatch(new WorldTimestamp(3));campaign.Diplomacy.Information.GetRequired(own).Receive(report,new WorldTimestamp(4));var delivered=builder.BuildFaction(own);
            Assert.That(delivered.OrderedKnownForeignInformation,Has.Count.EqualTo(1));Assert.That(delivered.OrderedKnownForeignInformation[0].Quality,Is.EqualTo(ReportQuality.Medium));Assert.That(delivered.OrderedKnownForeignInformation[0].StalenessAt(campaign.Clock.Now),Is.EqualTo(8));
        }

        [Test]
        public void UndeliveredReportIsNotAvailableToDiplomacyAI()
        {
            var campaign=PerceptionCampaign();var own=FactionId.Create("faction-own");var report=new ReportState(ReportId.Create("report-pending"),ReportType.Military,ReportSourceRef.Character(ReportSourceKind.Envoy,CharacterId.Create("character-observer")),own,ReportQuality.High,ReportDetailLevel.Detailed,new WorldTimestamp(1),new[]{new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Faction,"faction-enemy"),ReportObservationKind.ArmyEstimatedStrength,ObservationPrecision.Approximate,10)});campaign.Diplomacy.Reports.Add(report);report.Dispatch(new WorldTimestamp(2));
            Assert.That(new AIPerceptionContextBuilder(campaign).BuildFaction(own).OrderedKnownForeignInformation,Is.Empty);
        }

        [Test]
        public void SchedulerUsesCanonicalOwnerOrderAndAggregationWithoutMergingIdentity()
        {
            var catalog=ProofAIContent.CreateCatalog();var state=Controllers(250,true);var source=new ProofScheduledSource(true);var run=new AIDecisionScheduler().Run(new WorldTimestamp(10),state,catalog,source,new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});
            Assert.That(run.Results,Has.Count.EqualTo(250));Assert.That(run.Results.Select(x=>x.Trace.Owner.Id).Distinct().Count(),Is.EqualTo(250));Assert.That(run.Results.Select(x=>x.Trace.Owner).ToList(),Is.Ordered);
            Assert.That(run.Metrics.ContextBuilds,Is.EqualTo(3));Assert.That(run.Metrics.CandidateGenerations,Is.EqualTo(3));Assert.That(run.Metrics.SharedOrAggregatedEvaluations,Is.EqualTo(250));Assert.That(run.Metrics.SelectedActions,Is.EqualTo(250));
        }

        [Test]
        public void ImportantActorsAlwaysRemainIndividuallyPlanned()
        {
            var catalog=ProofAIContent.CreateCatalog();var state=Controllers(10,false);var run=new AIDecisionScheduler().Run(new WorldTimestamp(10),state,catalog,new ProofScheduledSource(false),new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});
            Assert.That(run.Metrics.ContextBuilds,Is.EqualTo(10));Assert.That(run.Metrics.SharedOrAggregatedEvaluations,Is.Zero);
        }

        [Test]
        public void ImportantActorActionsExecuteInCanonicalSequenceAndLaterContextSeesMutation()
        {
            var source=new SequentialSource();var run=new AIDecisionScheduler().RunImportantSequential(new WorldTimestamp(10),Controllers(2,false),ProofAIContent.CreateCatalog(),source,new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new OwnFactUtilityPolicy()},proposal=>source.WorldValue++);
            Assert.That(run.Results.Select(x=>x.Trace.Owner.Id),Is.Ordered);Assert.That(run.Results[0].Proposal!.Utility,Is.EqualTo(0));Assert.That(run.Results[1].Proposal!.Utility,Is.EqualTo(1));Assert.That(source.WorldValue,Is.EqualTo(2));
        }

        [Test]
        public void AggregatedAndIndividualEquivalentPoliciesSelectSameActions()
        {
            var catalog=ProofAIContent.CreateCatalog();var aggregated=new AIDecisionScheduler().Run(new WorldTimestamp(10),Controllers(100,true),catalog,new ProofScheduledSource(true),new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});var individual=new AIDecisionScheduler().Run(new WorldTimestamp(10),Controllers(100,false),catalog,new ProofScheduledSource(false),new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});
            Assert.That(aggregated.Results.Select(x=>x.Proposal!.Candidate.Id),Is.EqualTo(individual.Results.Select(x=>x.Proposal!.Candidate.Id)));Assert.That(aggregated.Metrics.ContextBuilds,Is.LessThan(individual.Metrics.ContextBuilds));
        }

        [Test]
        public void SchedulerQueryDoesNotConsumeControllerRandomState()
        {
            var state=Controllers(1,false);var controller=state.Controllers.OrderedControllers.Single();var before=controller.RandomState;new AIDecisionScheduler().Run(new WorldTimestamp(10),state,ProofAIContent.CreateCatalog(),new ProofScheduledSource(false),new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});
            Assert.That(controller.RandomState.State,Is.EqualTo(before.State));Assert.That(controller.RandomState.DrawCount,Is.EqualTo(before.DrawCount));
        }

        [Test]
        public void TacticalAIExecutesExistingTypedBattleOrderAndCannotCreateCasualty()
        {
            var fixture=BattleTestFactory.Create();var proposal=Proposal(AIDecisionOwnerRef.Character(CharacterId.Create("commander-a")),AIDecisionDomain.BattleTactical,AITargetRef.Battle(fixture.Battle.Id));var order=new BattleOrder(BattleOrderId.Create("ai-order-hold"),0,BattleOrderKind.Hold,fixture.SideA,CharacterId.Create("commander-a"),fixture.DeployA);var events=fixture.Battle.OrderedEvents.Count;
            new AITacticalOrderActionExecutor(fixture.Campaign).Execute(proposal,fixture.Battle.Id,order);
            Assert.That(fixture.Battle.OrderedOrders.Single(),Is.SameAs(order));Assert.That(fixture.Battle.OrderedEvents.Count,Is.EqualTo(events));
        }

        [Test]
        public void TacticalAIReusesCommanderAndAdjacencyValidation()
        {
            var fixture=BattleTestFactory.Create();var wrong=Proposal(AIDecisionOwnerRef.Character(CharacterId.Create("commander-b")),AIDecisionDomain.BattleTactical,AITargetRef.Battle(fixture.Battle.Id));var order=new BattleOrder(BattleOrderId.Create("ai-wrong"),0,BattleOrderKind.Hold,fixture.SideA,CharacterId.Create("commander-b"),fixture.DeployA);Assert.Throws<InvalidOperationException>(()=>new AITacticalOrderActionExecutor(fixture.Campaign).Execute(wrong,fixture.Battle.Id,order));
            var owner=Proposal(AIDecisionOwnerRef.Character(CharacterId.Create("commander-a")),AIDecisionDomain.BattleTactical,AITargetRef.Battle(fixture.Battle.Id));var nonAdjacent=new BattleOrder(BattleOrderId.Create("ai-non-adjacent"),1,BattleOrderKind.MoveSector,fixture.SideA,CharacterId.Create("commander-a"),fixture.DeployA,fixture.SectorB);Assert.Throws<InvalidOperationException>(()=>new AITacticalOrderActionExecutor(fixture.Campaign).Execute(owner,fixture.Battle.Id,nonAdjacent));
        }

        [Test]
        public void RecruitmentAIUsesFiniteExistingSourceAndCannotCreateFreeSoldiers()
        {
            var fixture=BattleTestFactory.Create();var proposal=Proposal(AIDecisionOwnerRef.Character(CharacterId.Create("commander-a")),AIDecisionDomain.MilitaryLogistics,AITargetRef.Army(fixture.ArmyA));var before=fixture.Campaign.Military.RecruitmentSources.GetRequired(RecruitmentSourceId.Create("source-a")).AvailableHeadcount;
            var command=new AIRecruitmentCommand(RecruitmentRecordId.Create("ai-record"),RecruitmentSourceId.Create("source-a"),fixture.ArmyA,UnitGroupId.Create("ai-unit"),"troop-melee",before+1);
            Assert.Throws<InvalidOperationException>(()=>new AIRecruitmentActionExecutor(fixture.Campaign).Execute(proposal,command));Assert.That(fixture.Campaign.Military.RecruitmentSources.GetRequired(RecruitmentSourceId.Create("source-a")).AvailableHeadcount,Is.EqualTo(before));Assert.That(fixture.Campaign.Military.Armies.GetRequired(fixture.ArmyA).OrderedUnits.Any(x=>x.Id.Equals(command.UnitGroupId)),Is.False);
        }

        [Test]
        public void TradeAIUsesRealStockCashCapacityAndAtomicApplicationService()
        {
            var campaign=TradeCampaign();var caravan=campaign.Economy.Caravans.GetRequired(CaravanId.Create("caravan-ai"));var market=campaign.Economy.GetRequiredMarket(CityId.Create("city-origin"));var good=campaign.Economy.Goods.GetRequired(TradeGoodId.Create("grain"));var proposal=Proposal(AIDecisionOwnerRef.Character(caravan.ManagerCharacterId),AIDecisionDomain.EconomyTrade,AITargetRef.Caravan(caravan.Id));var quote=TradePriceRules.FormQuote(good,Array.Empty<PriceAdjustment>());var beforeStock=market.Stock.QuantityOf(good.Id);var beforeMoney=caravan.CashBalance.Value;
            Assert.Throws<InvalidOperationException>(()=>new AITradeActionExecutor(campaign).Execute(proposal,new AITradePurchaseCommand(caravan.Id,market.CityId,good.Id,beforeStock+1,quote)));
            Assert.That(market.Stock.QuantityOf(good.Id),Is.EqualTo(beforeStock));Assert.That(caravan.Cargo.QuantityOf(good.Id),Is.Zero);Assert.That(caravan.CashBalance.Value,Is.EqualTo(beforeMoney));
        }

        [Test]
        public void SupplyAIHasNoFreeSupplyPath()
        {
            var fixture=BattleTestFactory.Create();var good=fixture.Campaign.Economy.Goods.OrderedGoods.First().Id;var proposal=Proposal(AIDecisionOwnerRef.Character(CharacterId.Create("commander-a")),AIDecisionDomain.MilitaryLogistics,AITargetRef.Army(fixture.ArmyA));var before=fixture.Campaign.Military.Armies.GetRequired(fixture.ArmyA).Supply.QuantityOf(good);
            Assert.Throws<KeyNotFoundException>(()=>new AISupplyActionExecutor(fixture.Campaign).Execute(proposal,new AISupplyCommand(CityId.Create("missing-city"),fixture.ArmyA,good,5)));Assert.That(fixture.Campaign.Military.Armies.GetRequired(fixture.ArmyA).Supply.QuantityOf(good),Is.EqualTo(before));
        }

        [Test]
        public void DiplomacyPlannerCannotBypassSourceFactionAuthority()
        {
            var campaign=PerceptionCampaign();var mission=new EnvoyMissionState(EnvoyMissionId.Create("mission-ai"),CharacterId.Create("character-observer"),FOC.Domain.Common.OrganizationId.Create("org-ai"),FOC.Domain.Common.AssignmentId.Create("assignment-ai"),FactionId.Create("faction-enemy"),FactionId.Create("faction-own"),EnvoyMissionType.NegotiateTrade,new DiplomaticMandate(FOC.Domain.Diplomacy.DiplomaticAuthorityScope.Conclude,new[]{DiplomaticActionKind.TradeAgreement}),campaign.Clock.Now);var message=new DiplomaticMessageState(DiplomaticMessageId.Create("message-ai"),FactionId.Create("faction-enemy"),FactionId.Create("faction-own"),MessageKind.Proposal,MessageCarrierKind.EnvoyMission,CharacterId.Create("character-observer"),campaign.Clock.Now,mission.Id);var action=new DiplomaticActionState(DiplomaticActionId.Create("action-ai"),FactionId.Create("faction-enemy"),FactionId.Create("faction-own"),DiplomaticActionKind.TradeAgreement,mission.Id,message.Id,campaign.Clock.Now);var proposal=Proposal(AIDecisionOwnerRef.Faction(FactionId.Create("faction-own")),AIDecisionDomain.Diplomacy,AITargetRef.Faction(FactionId.Create("faction-own")));
            Assert.Throws<InvalidOperationException>(()=>new AIDiplomacyActionExecutor(campaign).Execute(proposal,mission,message,action));Assert.That(campaign.Diplomacy.Actions.OrderedActions,Is.Empty);
        }

        [Test]
        public void EncounterAIRejectsUnknownOrNonParticipantEncounterBeforeResolution()
        {
            var campaign=PerceptionCampaign();var proposal=Proposal(AIDecisionOwnerRef.Character(CharacterId.Create("character-observer")),AIDecisionDomain.EncounterContract,AITargetRef.Encounter(EncounterId.Create("encounter-hidden")));var executor=new AIEncounterActionExecutor(campaign,new FOC.Domain.EncountersContracts.EncounterDefinitionCatalog());
            Assert.Throws<InvalidOperationException>(()=>executor.Resolve(proposal,EncounterId.Create("encounter-hidden"),FOC.Domain.Common.EncounterChoiceId.Create("choice"),Array.Empty<EncounterId>(),null!,null!,null!));
        }

        [Test]
        public void DecisionProposalDoesNotMutateCampaignState()
        {
            var campaign=PerceptionCampaign();var serializer=new CampaignSaveTextSerializer();var before=serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign));var catalog=ProofAIContent.CreateCatalog();var result=new AIDecisionEngine().Decide(Context("character-observer"),new[]{Candidate("candidate",ProofAIContent.MilitaryFactorId)},new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));var after=serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign));
            Assert.That(result.Proposal,Is.Not.Null);Assert.That(after,Is.EqualTo(before));
        }

        [Test]
        public void ReligionDifferenceAndSecretBetrayalAreNotImplicitAIInputs()
        {
            var names=typeof(AIDecisionContext).Assembly.GetTypes().Where(x=>x.Namespace=="FOC.Domain.AI").SelectMany(x=>x.GetMembers()).Select(x=>x.Name).ToList();Assert.That(names.Any(x=>x.Contains("Religion",StringComparison.OrdinalIgnoreCase)||x.Contains("Betray",StringComparison.OrdinalIgnoreCase)||x.Contains("Hostility",StringComparison.OrdinalIgnoreCase)),Is.False);
        }

        [Test]
        public void PromotedGeneratedCharacterKeepsAIControllerIdentity()
        {
            var generated=CharacterTestFactory.Generated("generated-ai");var before=AIDecisionOwnerRef.Character(generated.Id);CharacterPromotionRules.PromoteGeneratedToNamed(generated,new WorldTimestamp(2));var after=AIDecisionOwnerRef.Character(generated.Id);Assert.That(generated.Definition.IdentityKind,Is.EqualTo(CharacterIdentityKind.Named));Assert.That(after,Is.EqualTo(before));
        }

        [Test]
        public void HiddenCampaignBattleMutationCannotChangeUnchangedObservationDecision()
        {
            var fixture=BattleTestFactory.Create();var context=new AIDecisionContext(AIDecisionOwnerRef.Character(CharacterId.Create("commander-a")),fixture.Campaign.Clock.Now,new[]{new AIOwnFact("battle.observed-sector",1,"explicit-battle-observation")},Array.Empty<AIKnownObservation>());var catalog=ProofAIContent.CreateCatalog();var engine=new AIDecisionEngine();var candidates=new[]{new AIActionCandidate(AICandidateId.Create("hold"),AIDecisionDomain.BattleTactical,AIActionPolicyId.Create("hold-policy"),"hold",AITargetRef.Battle(fixture.Battle.Id))};var first=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));fixture.Campaign.Military.Armies.GetRequired(fixture.ArmyB).Morale.Set(FOC.Domain.Military.MoraleAssessment.Wavering);var second=engine.Decide(context,candidates,new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)},catalog.Priority(ProofAIContent.NeutralPriorityId),catalog.Character(ProofAIContent.CautiousCharacterId),catalog.Quality(ProofAIContent.AdvancedQualityId));Assert.That(second.Proposal!.Candidate.Id,Is.EqualTo(first.Proposal!.Candidate.Id));Assert.That(second.Proposal.Utility,Is.EqualTo(first.Proposal.Utility));
        }

        [TestCase(10,false)]
        [TestCase(100,true)]
        [TestCase(500,true)]
        [TestCase(1000,true)]
        public void RepresentativeSchedulerBenchmarkUsesActualMeasuredWork(int count,bool aggregate)
        {
            var state=Controllers(count,aggregate);var beforeBytes=GC.GetTotalMemory(true);var timer=Stopwatch.StartNew();var run=new AIDecisionScheduler().Run(new WorldTimestamp(10),state,ProofAIContent.CreateCatalog(),new ProofScheduledSource(aggregate),new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});timer.Stop();var allocated=Math.Max(0,GC.GetTotalMemory(false)-beforeBytes);TestContext.Progress.WriteLine("AI_PERFORMANCE owners={0} aggregate={1} elapsed_ms={2:F3} context_builds={3} candidate_generations={4} utility_evaluations={5} shared={6} selected={7} managed_delta_bytes={8}",count,aggregate,timer.Elapsed.TotalMilliseconds,run.Metrics.ContextBuilds,run.Metrics.CandidateGenerations,run.Metrics.UtilityEvaluations,run.Metrics.SharedOrAggregatedEvaluations,run.Metrics.SelectedActions,allocated);Assert.That(run.Results,Has.Count.EqualTo(count));Assert.That(run.Metrics.SelectedActions,Is.EqualTo(count));
        }

        [Test]
        public void TravelBoundaryCannotTeleportWithoutARealProvider()
        {
            Assert.That(typeof(IAITravelActionProvider).IsInterface,Is.True);Assert.That(typeof(IAITravelActionProvider).Assembly.GetTypes().Any(x=>x.IsClass&&typeof(IAITravelActionProvider).IsAssignableFrom(x)),Is.False);
        }

        [Test]
        public void ActivePlanAndControllerStateRoundTripThroughSaveV12()
        {
            var campaign=PerceptionCampaign();var owner=AIDecisionOwnerRef.Character(CharacterId.Create("character-observer"));var plan=new AIPlanState(AIPlanId.Create("plan-1"),owner,AIGoalId.Create("goal-1"),AICandidateId.Create("candidate-1"),AIDecisionDomain.Diplomacy,AIActionPolicyId.Create("policy-1"),new WorldTimestamp(5),AITargetRef.Faction(FactionId.Create("faction-enemy")),new WorldTimestamp(20));var controller=new AIControllerState(AIControllerId.Create("controller-1"),owner,ProofAIContent.MilitaryPriorityId,ProofAIContent.AdvancedQualityId,ProofAIContent.ImportantScheduleId,new RandomState(91,7),ProofAIContent.BoldCharacterId,new WorldTimestamp(4),new WorldTimestamp(12),plan);campaign.AI.Controllers.Add(controller);
            var restored=CampaignSaveMapper.ToRuntimeState(CampaignSaveMapper.ToSaveData(campaign)).AI.Controllers.GetRequired(controller.Id);
            Assert.That(restored.Owner,Is.EqualTo(owner));Assert.That(restored.PriorityProfileId,Is.EqualTo(controller.PriorityProfileId));Assert.That(restored.CharacterProfileId,Is.EqualTo(controller.CharacterProfileId));Assert.That(restored.QualityProfileId,Is.EqualTo(controller.QualityProfileId));Assert.That(restored.SchedulingProfileId,Is.EqualTo(controller.SchedulingProfileId));Assert.That(restored.RandomState.State,Is.EqualTo(91));Assert.That(restored.RandomState.DrawCount,Is.EqualTo(7));Assert.That(restored.CurrentPlan!.Target,Is.EqualTo(plan.Target));Assert.That(restored.NextDecisionAt,Is.EqualTo(controller.NextDecisionAt));
        }

        [Test]
        public void V11MigrationCreatesNoFakeAIPlanInformationOrResources()
        {
            var v11=new CampaignSaveData{SaveVersion=11,CampaignId="hardcoded-v11",GameVersion="0.10",ContentDataVersion="v11",WorldSeed=5,WorldGenRevision=1,WorldTime=9,RngState=7,RngDrawCount=2};v11.Encounters.Add(new EncounterSaveData{EncounterId="preserved-encounter"});v11.Contracts.Add(new ContractSaveData{ContractId="preserved-contract"});
            var migrated=new CampaignSaveV11ToV12Migration().Apply(v11);
            Assert.That(migrated.SaveVersion,Is.EqualTo(12));Assert.That(migrated.AIControllers,Is.Empty);Assert.That(migrated.ActorInformation,Is.Empty);Assert.That(migrated.CityMarkets,Is.Empty);Assert.That(migrated.Encounters.Single().EncounterId,Is.EqualTo("preserved-encounter"));Assert.That(migrated.Contracts.Single().ContractId,Is.EqualTo("preserved-contract"));
        }

        [Test]
        public void HardcodedSchemaElevenFixtureMigratesToTwelveWithoutInventedState()
        {
            const string fixture="FOC_CAMPAIGN_SAVE\nSaveVersion=11\nCampaignId=Y2FtcGFpZ24tbGVnYWN5\nGameVersion=MC4wLjEx\nContentDataVersion=ZW5jb3VudGVyLTE=\nWorldSeed=1648\nWorldGenRevision=1\nWorldTime=9\nRngState=123\nRngDrawCount=2\nCharacterCount=0\nCharacterRelationCount=0\nSocialState=AAAAAAAAAAAAAAAA\nReligionState=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nCityState=AAAAAA==\nEconomyState=AAAAAAAAAAAAAAAAAAAAAA==\nDiplomacyState=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\nMilitaryState=AAAAAAAAAAAAAAAA\nSoldierState=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\nBattleState=AAAAAA==\nEncounterContractState=AAAAAAAAAAA=\n";var read=new CampaignSaveTextSerializer().Deserialize(fixture);Assert.That(read.Success,Is.True,read.Error);var migrated=new CampaignSaveV11ToV12Migration().Apply(read.Data!);Assert.That(migrated.SaveVersion,Is.EqualTo(12));Assert.That(migrated.AIControllers,Is.Empty);Assert.That(migrated.ActorInformation,Is.Empty);Assert.That(migrated.Encounters,Is.Empty);Assert.That(migrated.Contracts,Is.Empty);
        }

        [Test]
        public void SaveValidatorRejectsDuplicateOwnersAndDanglingPlanTargets()
        {
            var campaign=PerceptionCampaign();var owner=AIDecisionOwnerRef.Character(CharacterId.Create("character-observer"));var plan=new AIPlanState(AIPlanId.Create("plan-validate"),owner,AIGoalId.Create("goal-validate"),AICandidateId.Create("candidate-validate"),AIDecisionDomain.InternalCityManagement,AIActionPolicyId.Create("policy-validate"),new WorldTimestamp(5),AITargetRef.Faction(FactionId.Create("faction-enemy")));campaign.AI.Controllers.Add(new AIControllerState(AIControllerId.Create("controller-validate"),owner,ProofAIContent.NeutralPriorityId,ProofAIContent.BasicQualityId,ProofAIContent.ImportantScheduleId,new RandomState(8,0),ProofAIContent.CautiousCharacterId,currentPlan:plan));var save=CampaignSaveMapper.ToSaveData(campaign);Assert.That(new CampaignSaveValidator().Validate(save).IsValid,Is.True);save.AIControllers[0].CurrentPlan!.TargetKind=(int)AITargetKind.City;save.AIControllers[0].CurrentPlan!.TargetId="missing-city";Assert.That(new CampaignSaveValidator().Validate(save).Issues.Any(x=>x.Code=="AI_PLAN_INVALID"),Is.True);save.AIControllers[0].CurrentPlan=null;save.AIControllers.Add(new AIControllerSaveData{ControllerId="duplicate-controller",OwnerKind=save.AIControllers[0].OwnerKind,OwnerId=save.AIControllers[0].OwnerId,PriorityProfileId="p",CharacterProfileId="c",QualityProfileId="q",SchedulingProfileId="s",RngState=1,Lifecycle=(int)AIControllerLifecycle.Active});Assert.That(new CampaignSaveValidator().Validate(save).Issues.Any(x=>x.Code=="AI_CONTROLLER_INVALID"),Is.True);
        }

        [Test]
        public void AIControllerSerializationIsCanonicalAcrossInsertionOrder()
        {
            var first=PerceptionCampaign();var second=PerceptionCampaign();foreach(var x in new[]{Controller("z",false),Controller("a",false)})first.AI.Controllers.Add(x);foreach(var x in new[]{Controller("a",false),Controller("z",false)})second.AI.Controllers.Add(x);var serializer=new CampaignSaveTextSerializer();
            Assert.That(serializer.Serialize(CampaignSaveMapper.ToSaveData(first)),Is.EqualTo(serializer.Serialize(CampaignSaveMapper.ToSaveData(second))));
        }

        [Test]
        public void SaveLoadBeforeDecisionKeepsSameProposalTraceAndRngContinuation()
        {
            var campaign=PerceptionCampaign();var controller=Controller("observer",false,CharacterId.Create("character-observer"));campaign.AI.Controllers.Add(controller);var restored=CampaignSaveMapper.ToRuntimeState(CampaignSaveMapper.ToSaveData(campaign));var source=new ProofScheduledSource(false);var originalRun=new AIDecisionScheduler().Run(campaign.Clock.Now,campaign.AI,ProofAIContent.CreateCatalog(),source,new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});var restoredRun=new AIDecisionScheduler().Run(restored.Clock.Now,restored.AI,ProofAIContent.CreateCatalog(),source,new SingleCandidateProvider(),new[]{new AllowPolicy()},new[]{new CandidateUtilityPolicy(1,1)});
            Assert.That(restoredRun.Results.Single().Proposal!.Candidate.Id,Is.EqualTo(originalRun.Results.Single().Proposal!.Candidate.Id));Assert.That(restoredRun.Results.Single().Trace.TieBreakReason,Is.EqualTo(originalRun.Results.Single().Trace.TieBreakReason));Assert.That(restored.AI.Controllers.OrderedControllers.Single().RandomState.State,Is.EqualTo(campaign.AI.Controllers.OrderedControllers.Single().RandomState.State));
        }

        [Test]
        public void AIInvariantValidatorRejectsMissingDefinitionsAndUnavailableCharacter()
        {
            var campaign=PerceptionCampaign();campaign.AI.Controllers.Add(Controller("observer",false,CharacterId.Create("character-observer")));Assert.Throws<KeyNotFoundException>(()=>new AIInvariantValidator().ValidateOrThrow(campaign,new AIDefinitionCatalog()));new AIInvariantValidator().ValidateOrThrow(campaign,ProofAIContent.CreateCatalog());campaign.Characters.GetRequired(CharacterId.Create("character-observer")).Capture(new CaptivityState(CharacterId.Create("captor"),CaptivitySite.At(new WorldPosition(1,1))),campaign.Clock.Now);Assert.Throws<InvalidOperationException>(()=>new AIInvariantValidator().ValidateOrThrow(campaign,ProofAIContent.CreateCatalog()));
        }

        [Test]
        public void AISourceAuditForbidsHiddenRandomFrameTimeAndHistoricalHardcodes()
        {
            var root=FindRepositoryRoot();var files=Directory.GetFiles(Path.Combine(root,"UnityProject","Assets","FOC"),"*.cs",SearchOption.AllDirectories).Where(x=>x.Contains(Path.DirectorySeparatorChar+"AI"+Path.DirectorySeparatorChar,StringComparison.Ordinal)).ToList();var source=string.Join("\n",files.Select(File.ReadAllText));
            Assert.That(source,Does.Not.Contain("UnityEngine.Random").And.Not.Contain("System.Random").And.Not.Contain("DateTime.Now").And.Not.Contain("void Update(").And.Not.Contain("Ottoman").And.Not.Contain("Safavid"));
        }

        [Test]
        public void AIDomainAndApplicationRemainUnityAndPresentationFree()
        {
            var types=typeof(AIDecisionContext).Assembly.GetTypes().Where(x=>x.Namespace!=null&&x.Namespace.StartsWith("FOC.Domain.AI",StringComparison.Ordinal)).Concat(typeof(AIDecisionEngine).Assembly.GetTypes().Where(x=>x.Namespace!=null&&x.Namespace.StartsWith("FOC.Application.AI",StringComparison.Ordinal)));var signatures=types.SelectMany(x=>x.GetMembers()).Select(x=>x.ToString()).ToList();Assert.That(signatures.Any(x=>x!=null&&(x.Contains("UnityEngine",StringComparison.Ordinal)||x.Contains("Presentation",StringComparison.Ordinal)||x.Contains("GameObject",StringComparison.Ordinal)||x.Contains("MonoBehaviour",StringComparison.Ordinal))),Is.False);
        }

        private static CampaignRuntimeState PerceptionCampaign()
        {
            var characters=new CharacterRoster();characters.Add(CharacterTestFactory.Named("character-observer"));var actors=new DiplomaticActorRegistry();actors.Add(new DiplomaticActorState(FactionId.Create("faction-own"),"Own"));actors.Add(new DiplomaticActorState(FactionId.Create("faction-enemy"),"Enemy"));var information=new ActorInformationRegistry();information.Add(new ActorInformationState(FactionId.Create("faction-own")));information.Add(new ActorInformationState(FactionId.Create("faction-enemy")));var diplomacy=new DiplomacyState(actors,information:information);return new CampaignRuntimeState(StableId<CampaignTag>.Create("campaign-ai"),"0.11","ai-proof",5,1,new WorldClock(new WorldTimestamp(10)),new SeededRandomSource(5),characters,diplomacy:diplomacy);
        }
        private static CampaignRuntimeState TradeCampaign()
        {
            var characters=new CharacterRoster();characters.Add(CharacterTestFactory.Named("trade-manager"));var cities=new CityRegistry();var origin=CityTestFactory.State("city-origin");var destination=CityTestFactory.State("city-destination");cities.Add(origin);cities.Add(destination);var goods=new TradeGoodRegistry();var good=new TradeGoodDefinition(TradeGoodId.Create("grain"),"Grain",TradeGoodCategory.Food,1,true,false,false,10);goods.Add(good);var economy=new EconomyState(goods);var stock=new CityStockState();stock.AddInitial(new TradeGoodStock(good.Id,10));economy.AddMarket(new CityMarketState(origin.Id,0,stock));economy.AddMarket(new CityMarketState(destination.Id,100));economy.Caravans.Add(new CaravanState(CaravanId.Create("caravan-ai"),EconomicOwnerRef.Character(CharacterId.Create("trade-manager")),CharacterId.Create("trade-manager"),origin.Id,destination.Id,10,50));return new CampaignRuntimeState(StableId<CampaignTag>.Create("campaign-trade-ai"),"0.11","ai-proof",7,1,new WorldClock(new WorldTimestamp(10)),new SeededRandomSource(7),characters,cities:cities,economy:economy);
        }
        private static AIDecisionContext Context(string character)=>new AIDecisionContext(AIDecisionOwnerRef.Character(CharacterId.Create(character)),new WorldTimestamp(10),Array.Empty<AIOwnFact>(),Array.Empty<AIKnownObservation>());
        private static AIKnownObservation Known(string id,long observed,long arrived)=>new AIKnownObservation(ReportId.Create(id),new ReportSubjectRef(ReportSubjectKind.Faction,"faction-enemy"),ReportObservationKind.DiplomaticDisposition,ObservationPrecision.Qualitative,ReportQuality.Low,ReportDetailLevel.Summary,new WorldTimestamp(observed),new WorldTimestamp(arrived),qualitative:QualitativeObservation.Worsening);
        private static AIActionCandidate Candidate(string id,AIUtilityFactorId factor)=>new AIActionCandidate(AICandidateId.Create(id),AIDecisionDomain.Diplomacy,AIActionPolicyId.Create("policy-"+factor.Value),id);
        private static AIActionProposal Proposal(AIDecisionOwnerRef owner,AIDecisionDomain domain,AITargetRef target)=>new AIActionProposal(owner,new AIActionCandidate(AICandidateId.Create("proposal-candidate"),domain,AIActionPolicyId.Create("proposal-policy"),"proof",target),new WorldTimestamp(10),Array.Empty<AIUtilityContribution>(),0);
        private static AICampaignState Controllers(int count,bool aggregated){var registry=new AIDecisionRegistry();for(var i=count-1;i>=0;i--)registry.Add(Controller(i.ToString("D4"),aggregated));return new AICampaignState(registry);}
        private static AIControllerState Controller(string suffix,bool aggregated,CharacterId? owner=null)=>new AIControllerState(AIControllerId.Create("controller-"+suffix),AIDecisionOwnerRef.Character(owner??CharacterId.Create("character-"+suffix)),ProofAIContent.NeutralPriorityId,ProofAIContent.AdvancedQualityId,aggregated?ProofAIContent.LowImportanceScheduleId:ProofAIContent.ImportantScheduleId,new RandomState(17,0),ProofAIContent.CautiousCharacterId);
        private static string FindRepositoryRoot(){var current=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);while(current!=null){if(File.Exists(Path.Combine(current.FullName,"FallOfCavalry.sln")))return current.FullName;current=current.Parent;}throw new DirectoryNotFoundException();}

        private sealed class AllowPolicy:IAIEligibilityPolicy{public AIEligibilityResult Evaluate(AIDecisionContext context,AIActionCandidate candidate)=>AIEligibilityResult.Allow();}
        private sealed class RejectPolicy:IAIEligibilityPolicy{public AIEligibilityResult Evaluate(AIDecisionContext context,AIActionCandidate candidate)=>AIEligibilityResult.Reject("hard-rule");}
        private sealed class CountingUtilityPolicy:IAIUtilityPolicy{public int Calls{get;private set;}public IEnumerable<AIUtilityContribution> Evaluate(AIDecisionContext context,AIActionCandidate candidate){Calls++;yield return new AIUtilityContribution(ProofAIContent.MilitaryFactorId,1,"proof.utility");}}
        private sealed class CandidateUtilityPolicy:IAIUtilityPolicy
        {private readonly long _military;private readonly long _caution;public CandidateUtilityPolicy(long military,long caution){_military=military;_caution=caution;}public IEnumerable<AIUtilityContribution> Evaluate(AIDecisionContext context,AIActionCandidate candidate){var military=candidate.PolicyId.Value.Contains(ProofAIContent.MilitaryFactorId.Value,StringComparison.Ordinal);yield return new AIUtilityContribution(military?ProofAIContent.MilitaryFactorId:ProofAIContent.CautionFactorId,military?_military:_caution,"proof.utility");}}
        private sealed class SingleCandidateProvider:IAIDecisionCandidateProvider{public IEnumerable<AIActionCandidate> Generate(AIDecisionContext context){yield return Candidate("candidate-shared",ProofAIContent.MilitaryFactorId);}}
        private sealed class ProofScheduledSource:IAIScheduledDecisionSource
        {private readonly bool _aggregate;public ProofScheduledSource(bool aggregate){_aggregate=aggregate;}public bool CanAggregate(AIControllerState controller,AISchedulingProfile profile)=>_aggregate&&profile.Id.Equals(ProofAIContent.LowImportanceScheduleId);public string AggregationKey(AIControllerState controller)=>"equivalent-proof-context";public AIDecisionContext BuildIndividual(AIControllerState controller,WorldTimestamp now)=>new AIDecisionContext(controller.Owner,now,new[]{new AIOwnFact("resource",1,"actor-owned")},Array.Empty<AIKnownObservation>());public AIDecisionContext BuildShared(IReadOnlyList<AIControllerState> controllers,WorldTimestamp now)=>new AIDecisionContext(controllers[0].Owner,now,new[]{new AIOwnFact("resource",1,"equivalent-proof-batch")},Array.Empty<AIKnownObservation>());public AIDecisionContext MaterializeShared(AIDecisionContext shared,AIDecisionOwnerRef owner)=>new AIDecisionContext(owner,shared.Now,shared.OrderedOwnFacts,shared.OrderedKnownForeignInformation);}
        private sealed class SequentialSource:IAIScheduledDecisionSource
        {public long WorldValue{get;set;}public bool CanAggregate(AIControllerState controller,AISchedulingProfile profile)=>false;public string AggregationKey(AIControllerState controller)=>throw new InvalidOperationException();public AIDecisionContext BuildIndividual(AIControllerState controller,WorldTimestamp now)=>new AIDecisionContext(controller.Owner,now,new[]{new AIOwnFact("world",WorldValue,"updated-authoritative-state")},Array.Empty<AIKnownObservation>());public AIDecisionContext BuildShared(IReadOnlyList<AIControllerState> controllers,WorldTimestamp now)=>throw new InvalidOperationException();public AIDecisionContext MaterializeShared(AIDecisionContext shared,AIDecisionOwnerRef owner)=>throw new InvalidOperationException();}
        private sealed class OwnFactUtilityPolicy:IAIUtilityPolicy
        {public IEnumerable<AIUtilityContribution> Evaluate(AIDecisionContext context,AIActionCandidate candidate){context.TryGetOwnFact("world",out var fact);yield return new AIUtilityContribution(ProofAIContent.MilitaryFactorId,fact.Value,"world-state");}}
    }
}
