using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Application.Battle;
using FOC.Application.EncountersContracts;
using FOC.Application.Save;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Military;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class EncounterContractTests
    {
        private static readonly CharacterId Issuer = CharacterId.Create("issuer");
        private static readonly CharacterId Target = CharacterId.Create("target");
        private static readonly ContractTargetSlotId TargetSlot = ContractTargetSlotId.Create("target");
        private static readonly ContractObjectiveId Objective = ContractObjectiveId.Create("objective");

        [Test]
        public void AuthorityEnums_AreExactAndRemovedEncounterTypesCannotReappear()
        {
            Assert.That(Enum.GetNames(typeof(EncounterFamily)), Is.EqualTo(new[] { "Roaming", "SurpriseEvent" }));
            Assert.That(Enum.GetNames(typeof(EncounterType)), Is.EqualTo(new[] { "Bandit", "Caravan", "Noble", "EnemyPatrol", "Refugee", "AbandonedCaravan", "Epidemic", "RoadCollapse", "Storm", "WoundedSoldier" }));
            Assert.That(Enum.GetNames(typeof(ContractCategory)), Is.EqualTo(new[] { "Military", "Diplomatic", "Economic", "InternalManagement" }));
            var forbidden = new[] { "Agent", "Informant", "VillageDelegation", "Deserter", "Duel", "Night" };
            Assert.That(Enum.GetNames(typeof(EncounterType)).Intersect(forbidden), Is.Empty);
        }

        [Test]
        public void ProofCatalog_CoversAllTenTypesWithAuthoritativeFamilies()
        {
            var definitions = ProofEncounterContractContent.Encounters().OrderedDefinitions;
            Assert.That(definitions.Count, Is.EqualTo(10));
            Assert.That(definitions.Select(x => x.Type).Distinct().Count(), Is.EqualTo(10));
            Assert.That(definitions.All(x => EncounterAuthority.Matches(x.Family, x.Type)), Is.True);
        }

        [Test]
        public void EveryAuthoritativeEncounterType_ResolvesThroughTheGenericEngine()
        {
            var campaign = SimpleCampaign();
            var catalog = ProofEncounterContractContent.Encounters();
            var service = new EncounterService(campaign, catalog);
            foreach (var definition in catalog.OrderedDefinitions)
            {
                var state = service.Trigger(EncounterId.Create("generic-" + definition.Id.Value), definition.Id, new EncounterContext(EncounterEntityRef.Character(Issuer), new[] { EncounterEntityRef.Character(Target) }), Rng(), new AlwaysTrigger());
                service.Engage(state.Id);
                var choice = definition.OrderedChoices.First();
                service.Resolve(state.Id, choice.Id, new AlwaysRequirements(), new DrawResolver(), new RecordingEncounterEffects());
                Assert.That(state.Lifecycle, Is.EqualTo(EncounterLifecycle.Resolved), definition.Type.ToString());
            }
        }

        [Test]
        public void BanditAndCaravan_ExposeCapabilitiesWithoutInventedNumbers()
        {
            var definitions = ProofEncounterContractContent.Encounters().OrderedDefinitions;
            var bandit = definitions.Single(x => x.Type == EncounterType.Bandit);
            var caravan = definitions.Single(x => x.Type == EncounterType.Caravan);
            Assert.That((bandit.Capabilities & EncounterCapability.Recruitment) != 0, Is.True);
            Assert.That((bandit.Capabilities & EncounterCapability.Battle) != 0, Is.True);
            Assert.That((caravan.Capabilities & EncounterCapability.Toll) != 0, Is.True);
            Assert.That((caravan.Capabilities & EncounterCapability.Bribe) != 0, Is.True);
            var forbiddenPropertyNames = new[] { "Probability", "Chance", "Rate", "Amount", "Reward", "Penalty" };
            Assert.That(typeof(EncounterDefinition).GetProperties().Any(x => forbiddenPropertyNames.Any(y => x.Name.Contains(y, StringComparison.OrdinalIgnoreCase))), Is.False);
        }

        [Test]
        public void EncounterDefinitionAndInstance_AreSeparateTypes()
        {
            Assert.That(typeof(EncounterDefinition).IsAssignableFrom(typeof(EncounterState)), Is.False);
            Assert.That(typeof(EncounterState).IsAssignableFrom(typeof(EncounterDefinition)), Is.False);
        }

        [Test]
        public void EncounterLifecycle_RejectsInvalidTransitionAndSecondResolution()
        {
            var setup = Trigger("encounter-life", "proof-bandit");
            Assert.Throws<InvalidOperationException>(() => Resolve(setup, "join"));
            setup.Service.Engage(setup.State.Id);
            Resolve(setup, "join");
            Assert.That(setup.State.Lifecycle, Is.EqualTo(EncounterLifecycle.Resolved));
            Assert.That(setup.State.IsOutcomeApplied, Is.True);
            Assert.Throws<InvalidOperationException>(() => Resolve(setup, "join"));
            Assert.Throws<InvalidOperationException>(() => setup.State.Cancel());
        }

        [Test]
        public void EncounterResolution_IsDeterministicAndPersistsRngContinuation()
        {
            var a = Trigger("encounter-a", "proof-bandit");
            var b = Trigger("encounter-b", "proof-bandit");
            a.Service.Engage(a.State.Id);
            b.Service.Engage(b.State.Id);
            var first = Resolve(a, "fight");
            var second = Resolve(b, "fight");
            Assert.That(second.OutcomeId, Is.EqualTo(first.OutcomeId));
            Assert.That(second.RandomState, Is.EqualTo(first.RandomState));
            Assert.That(a.State.RandomState, Is.EqualTo(first.RandomState));
        }

        [Test]
        public void EncounterResolution_IsAtomicWhenPreflightFails()
        {
            var setup = Trigger("encounter-atomic", "proof-bandit");
            setup.Service.Engage(setup.State.Id);
            var effects = new RecordingEncounterEffects(failPreflight: true);
            Assert.Throws<InvalidOperationException>(() => setup.Service.Resolve(setup.State.Id, EncounterChoiceId.Create("join"), new AlwaysRequirements(), new DrawResolver(), effects));
            Assert.That(setup.State.Lifecycle, Is.EqualTo(EncounterLifecycle.Engaged));
            Assert.That(setup.State.Resolution, Is.Null);
            Assert.That(effects.ApplyCount, Is.Zero);
        }

        [Test]
        public void EncounterTrigger_UsesExplicitPolicyAndTypedContext()
        {
            var campaign = SimpleCampaign();
            var service = new EncounterService(campaign, ProofEncounterContractContent.Encounters());
            var context = new EncounterContext(EncounterEntityRef.Character(Issuer), new[] { EncounterEntityRef.Character(Target), EncounterEntityRef.Region(RegionId.Create("region")) });
            Assert.Throws<InvalidOperationException>(() => service.Trigger(EncounterId.Create("blocked"), EncounterDefinitionId.Create("proof-storm"), context, Rng(), new NeverTrigger()));
            var state = service.Trigger(EncounterId.Create("allowed"), EncounterDefinitionId.Create("proof-storm"), context, Rng(), new AlwaysTrigger());
            Assert.That(state.Context.OrderedParticipants.Select(x => x.Kind), Is.EqualTo(new[] { EncounterEntityKind.Character, EncounterEntityKind.Region }));
        }

        [Test]
        public void UnknownEncounterChoice_IsRejectedWithoutMutation()
        {
            var setup = Trigger("encounter-choice", "proof-noble");
            setup.Service.Engage(setup.State.Id);
            Assert.Throws<KeyNotFoundException>(() => setup.Service.Resolve(setup.State.Id, EncounterChoiceId.Create("missing"), new AlwaysRequirements(), new DrawResolver(), new RecordingEncounterEffects()));
            Assert.That(setup.State.Lifecycle, Is.EqualTo(EncounterLifecycle.Engaged));
        }

        [Test]
        public void ContractDefinitionAndInstance_AreSeparateAndAllCategoriesHaveProofContent()
        {
            Assert.That(typeof(ContractDefinition).IsAssignableFrom(typeof(ContractState)), Is.False);
            Assert.That(ProofEncounterContractContent.Contracts().OrderedDefinitions.Select(x => x.Category).Distinct(), Is.EquivalentTo(Enum.GetValues(typeof(ContractCategory))));
        }

        [Test]
        public void ContractDefinition_BindsDifferentActualTypedTargets()
        {
            var campaign = SimpleCampaign();
            var catalog = CharacterContractCatalog();
            var service = new ContractService(campaign, catalog);
            var first = service.Offer(ContractId.Create("contract-a"), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Character(TargetSlot, Issuer) });
            var second = service.Offer(ContractId.Create("contract-b"), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Character(TargetSlot, Target) });
            Assert.That(first.DefinitionId, Is.EqualTo(second.DefinitionId));
            Assert.That(first.GetRequiredTarget(TargetSlot).TargetId, Is.Not.EqualTo(second.GetRequiredTarget(TargetSlot).TargetId));
        }

        [Test]
        public void ContractOffer_RejectsWrongTargetTypeMissingTargetAndUnknownAssignee()
        {
            var campaign = SimpleCampaign();
            var service = new ContractService(campaign, CharacterContractCatalog());
            Assert.Throws<InvalidOperationException>(() => service.Offer(ContractId.Create("wrong"), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.City(TargetSlot, CityId.Create("city-home")) }));
            Assert.Throws<InvalidOperationException>(() => service.Offer(ContractId.Create("missing"), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(Issuer), Array.Empty<ContractTargetBinding>()));
            Assert.Throws<KeyNotFoundException>(() => service.Offer(ContractId.Create("assignee"), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Character(TargetSlot, Target) }, CharacterId.Create("absent")));
            Assert.Throws<KeyNotFoundException>(() => service.Offer(ContractId.Create("issuer"), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(CharacterId.Create("absent")), new[] { ContractTargetBinding.Character(TargetSlot, Target) }));
        }

        [Test]
        public void ContractLifecycle_DistinguishesCancellationFailureAndNoAutomaticDeadline()
        {
            var campaign = SimpleCampaign();
            var service = new ContractService(campaign, CharacterContractCatalog());
            var cancelled = OfferCharacter(service, "cancelled", new WorldTimestamp(11));
            var failed = OfferCharacter(service, "failed", new WorldTimestamp(11));
            service.Accept(failed.Id);
            campaign.Clock.Advance(new WorldDuration(5));
            Assert.That(failed.Lifecycle, Is.EqualTo(ContractLifecycle.Active));
            service.Cancel(cancelled.Id);
            service.Fail(failed.Id, new RecordingContractEffects());
            Assert.That(cancelled.Lifecycle, Is.EqualTo(ContractLifecycle.Cancelled));
            Assert.That(failed.Lifecycle, Is.EqualTo(ContractLifecycle.Failed));
            Assert.That(failed.IsOutcomeApplied, Is.True);
        }

        [Test]
        public void ContractEvidence_ProgressesFromRealCharacterStateAndRejectsDuplicate()
        {
            var campaign = SimpleCampaign();
            var service = new ContractService(campaign, CharacterContractCatalog());
            var state = OfferCharacter(service, "evidence");
            service.Accept(state.Id);
            var evidence = ContractEvidence.CharacterAssigned(ContractEvidenceId.Create("evidence-a"), Target, campaign.Clock.Now);
            service.SubmitEvidence(state.Id, Objective, evidence);
            Assert.That(state.AllObjectivesSatisfied, Is.True);
            Assert.Throws<InvalidOperationException>(() => service.SubmitEvidence(state.Id, Objective, evidence));
        }

        [Test]
        public void ContractCompletion_IsAtomicAndOneTime()
        {
            var campaign = SimpleCampaign();
            var service = new ContractService(campaign, CharacterContractCatalog());
            var state = OfferCharacter(service, "complete");
            service.Accept(state.Id);
            service.SubmitEvidence(state.Id, Objective, ContractEvidence.CharacterAssigned(ContractEvidenceId.Create("proof"), Target, campaign.Clock.Now));
            var failing = new RecordingContractEffects(failPreflight: true);
            Assert.Throws<InvalidOperationException>(() => service.Complete(state.Id, failing));
            Assert.That(state.Lifecycle, Is.EqualTo(ContractLifecycle.Active));
            Assert.That(failing.ApplyCount, Is.Zero);
            service.Complete(state.Id, new RecordingContractEffects());
            Assert.That(state.Lifecycle, Is.EqualTo(ContractLifecycle.Completed));
            Assert.That(state.IsOutcomeApplied, Is.True);
            Assert.Throws<InvalidOperationException>(() => service.Complete(state.Id, new RecordingContractEffects()));
        }

        [Test]
        public void ContractResolutionProfiles_AreExplicitAndNotUniversal()
        {
            var stats = new CharacterStats(10, 20, 30, 40, 50, 60, 70, 80, 90);
            var diplomacy = new ContractResolutionProfile(ContractResolutionProfileId.Create("diplomacy"), new[] { new ContractStatWeight(CharacterStatKind.Persuasion, 2) });
            var economy = new ContractResolutionProfile(ContractResolutionProfileId.Create("economy"), new[] { new ContractStatWeight(CharacterStatKind.Trade, 3), new ContractStatWeight(CharacterStatKind.Administration, 1) });
            Assert.That(diplomacy.Evaluate(stats), Is.EqualTo(60));
            Assert.That(economy.Evaluate(stats), Is.EqualTo(250));
            Assert.That(typeof(ContractState).GetProperties().Any(x => x.Name.Contains("Universal", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void ResolvedEncounter_CreatesLinkedContractAndProvidesRealEvidence()
        {
            var setup = Trigger("encounter-contract", "proof-bandit");
            setup.Service.Engage(setup.State.Id);
            Resolve(setup, "join");
            var contracts = new ContractService(setup.Campaign, ProofEncounterContractContent.Contracts());
            var contract = setup.Service.CreateLinkedContract(setup.State.Id, contracts, ContractId.Create("linked-contract"), ContractDefinitionId.Create("proof-encounter"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Encounter(TargetSlot, setup.State.Id) }, Target);
            contracts.Accept(contract.Id);
            contracts.SubmitEvidence(contract.Id, Objective, ContractEvidence.EncounterResolved(ContractEvidenceId.Create("encounter-proof"), setup.State.Id, setup.Campaign.Clock.Now));
            Assert.That(setup.State.LinkedContractId, Is.EqualTo(contract.Id));
            Assert.That(contract.LinkedEncounterId, Is.EqualTo(setup.State.Id));
            Assert.That(contract.AllObjectivesSatisfied, Is.True);
        }

        [Test]
        public void CompletedBattle_ProvidesContractEvidence()
        {
            var fixture = BattleTestFactory.Create();
            fixture.Battle.BeginResolving();
            fixture.Campaign.Clock.Advance(new WorldDuration(2));
            fixture.Battle.Complete(BattleResultFor(fixture));
            var service = new ContractService(fixture.Campaign, ProofEncounterContractContent.Contracts());
            var contract = service.Offer(ContractId.Create("battle-contract"), ContractDefinitionId.Create("proof-military"), ContractIssuerRef.Character(CharacterId.Create("commander-a")), new[] { ContractTargetBinding.Battle(TargetSlot, fixture.Battle.Id) }, linkedBattle: fixture.Battle.Id);
            service.Accept(contract.Id);
            service.SubmitEvidence(contract.Id, Objective, ContractEvidence.BattleCompleted(ContractEvidenceId.Create("battle-proof"), fixture.Battle.Id, fixture.Campaign.Clock.Now));
            Assert.That(contract.AllObjectivesSatisfied, Is.True);
        }

        [Test]
        public void ResolvedEnemyPatrol_CreatesBattleThroughImplementationNineService()
        {
            var fixture = BattleTestFactory.Create();
            var encounters = ProofEncounterContractContent.Encounters();
            var service = new EncounterService(fixture.Campaign, encounters);
            var state = service.Trigger(EncounterId.Create("enemy-patrol"), EncounterDefinitionId.Create("proof-enemy-patrol"), new EncounterContext(EncounterEntityRef.Character(CharacterId.Create("commander-a")), new[] { EncounterEntityRef.Character(CharacterId.Create("commander-b")) }), Rng(), new AlwaysTrigger());
            service.Engage(state.Id);
            service.Resolve(state.Id, EncounterChoiceId.Create("engage"), new AlwaysRequirements(), new DrawResolver(), new RecordingEncounterEffects());
            var graph = new BattleSectorGraph();
            var sectorA = new BattleSector(BattleSectorId.Create("linked-sector-a"), eligibleSides: new[] { fixture.SideA });
            var sectorB = new BattleSector(BattleSectorId.Create("linked-sector-b"), eligibleSides: new[] { fixture.SideB });
            graph.Add(sectorA);
            graph.Add(sectorB);
            graph.Connect(sectorA.Id, sectorB.Id);
            var request = new BattleCreationRequest(BattleId.Create("linked-battle"), fixture.Campaign.Clock.Now, Rng(), new[]
            {
                new BattleSidePlan(fixture.SideA, CharacterId.Create("commander-a"), new[] { fixture.ArmyA }),
                new BattleSidePlan(fixture.SideB, CharacterId.Create("commander-b"), new[] { fixture.ArmyB })
            }, graph);
            var battle = service.CreateLinkedBattle(state.Id, request);
            Assert.That(battle.Id, Is.EqualTo(BattleId.Create("linked-battle")));
            Assert.That(state.LinkedBattleId, Is.EqualTo(battle.Id));
            Assert.That(fixture.Campaign.Battles.Battles.GetRequired(battle.Id), Is.SameAs(battle));
        }

        [Test]
        public void TradeTransaction_ProvidesContractEvidence()
        {
            var campaign = SimpleCampaign();
            var caravan = new CaravanState(CaravanId.Create("caravan"), EconomicOwnerRef.Character(Issuer), Issuer, CityId.Create("city-home"), CityId.Create("city-other"), 10, 10);
            caravan.Accounting.RecordPurchase(4);
            campaign.Economy.Caravans.Add(caravan);
            var service = new ContractService(campaign, ProofEncounterContractContent.Contracts());
            var contract = service.Offer(ContractId.Create("trade-contract"), ContractDefinitionId.Create("proof-economic"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Caravan(TargetSlot, caravan.Id) });
            service.Accept(contract.Id);
            service.SubmitEvidence(contract.Id, Objective, ContractEvidence.TradeCompleted(ContractEvidenceId.Create("trade-proof"), caravan.Id, campaign.Clock.Now));
            Assert.That(contract.AllObjectivesSatisfied, Is.True);
        }

        [Test]
        public void DeliveredDiplomaticReport_ProvidesContractEvidence()
        {
            var campaign = SimpleCampaign();
            var faction = FactionId.Create("faction");
            campaign.Diplomacy.Actors.Add(new DiplomaticActorState(faction, "Faction"));
            var information = new ActorInformationState(faction);
            campaign.Diplomacy.Information.Add(information);
            var observation = new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Faction, faction.Value), ReportObservationKind.DiplomaticDisposition, ObservationPrecision.Qualitative, qualitative: QualitativeObservation.Stable);
            var report = new ReportState(ReportId.Create("report"), ReportType.Diplomatic, ReportSourceRef.Character(ReportSourceKind.Official, Issuer), faction, ReportQuality.High, ReportDetailLevel.Summary, campaign.Clock.Now, new[] { observation });
            report.Dispatch(campaign.Clock.Now);
            campaign.Clock.Advance(new WorldDuration(1));
            information.Receive(report, campaign.Clock.Now);
            var service = new ContractService(campaign, ProofEncounterContractContent.Contracts());
            var contract = service.Offer(ContractId.Create("diplomatic-contract"), ContractDefinitionId.Create("proof-diplomatic"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Faction(TargetSlot, faction) });
            service.Accept(contract.Id);
            service.SubmitEvidence(contract.Id, Objective, ContractEvidence.ReportDelivered(ContractEvidenceId.Create("report-proof"), faction, campaign.Clock.Now));
            Assert.That(contract.AllObjectivesSatisfied, Is.True);
        }

        [Test]
        public void CityState_ProvidesInternalManagementEvidence()
        {
            var campaign = SimpleCampaign();
            var service = new ContractService(campaign, ProofEncounterContractContent.Contracts());
            var contract = service.Offer(ContractId.Create("city-contract"), ContractDefinitionId.Create("proof-internal"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.City(TargetSlot, CityId.Create("city-home")) });
            service.Accept(contract.Id);
            service.SubmitEvidence(contract.Id, Objective, ContractEvidence.CityChanged(ContractEvidenceId.Create("city-proof"), CityId.Create("city-home"), campaign.Clock.Now));
            Assert.That(contract.AllObjectivesSatisfied, Is.True);
        }

        [Test]
        public void SaveV11_RoundTripsActiveAndResolvedStatesWithTypedReferences()
        {
            var setup = Trigger("encounter-save", "proof-bandit");
            setup.Service.Engage(setup.State.Id);
            Resolve(setup, "fight");
            var contracts = new ContractService(setup.Campaign, CharacterContractCatalog());
            var contract = OfferCharacter(contracts, "save-contract", new WorldTimestamp(20));
            contracts.Accept(contract.Id);
            var serializer = new CampaignSaveTextSerializer();
            var text = serializer.Serialize(CampaignSaveMapper.ToSaveData(setup.Campaign));
            var read = serializer.Deserialize(text);
            Assert.That(read.Success, Is.True, read.Error);
            Assert.That(new CampaignSaveValidator().Validate(read.Data!).IsValid, Is.True);
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!);
            var encounter = restored.EncounterContracts.Encounters.GetRequired(setup.State.Id);
            var restoredContract = restored.EncounterContracts.Contracts.GetRequired(contract.Id);
            Assert.That(encounter.RandomState, Is.EqualTo(setup.State.RandomState));
            Assert.That(encounter.IsOutcomeApplied, Is.True);
            Assert.That(restoredContract.Lifecycle, Is.EqualTo(ContractLifecycle.Active));
            Assert.That(restoredContract.GetRequiredTarget(TargetSlot).Kind, Is.EqualTo(ContractTargetKind.Character));
        }

        [Test]
        public void SaveV11_RoundTripsCompletedContractAndOneTimeOutcomeMarker()
        {
            var campaign = SimpleCampaign();
            var service = new ContractService(campaign, CharacterContractCatalog());
            var contract = OfferCharacter(service, "completed-save");
            service.Accept(contract.Id);
            service.SubmitEvidence(contract.Id, Objective, ContractEvidence.CharacterAssigned(ContractEvidenceId.Create("completed-proof"), Target, campaign.Clock.Now));
            service.Complete(contract.Id, new RecordingContractEffects());
            var serializer = new CampaignSaveTextSerializer();
            var read = serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign)));
            Assert.That(read.Success, Is.True, read.Error);
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!).EncounterContracts.Contracts.GetRequired(contract.Id);
            Assert.That(restored.Lifecycle, Is.EqualTo(ContractLifecycle.Completed));
            Assert.That(restored.IsOutcomeApplied, Is.True);
            Assert.That(restored.AllObjectivesSatisfied, Is.True);
            Assert.Throws<InvalidOperationException>(() => restored.MarkOutcomeApplied());
        }

        [Test]
        public void SaveV11_IsCanonicalAcrossEncounterAndContractInsertionOrder()
        {
            var first = CanonicalCampaign(reverse: false);
            var second = CanonicalCampaign(reverse: true);
            var serializer = new CampaignSaveTextSerializer();
            Assert.That(serializer.Serialize(CampaignSaveMapper.ToSaveData(second)), Is.EqualTo(serializer.Serialize(CampaignSaveMapper.ToSaveData(first))));
        }

        [Test]
        public void MigrationTenToEleven_PreservesBattleAndInventsNoEncounterOrContract()
        {
            var source = CampaignSaveMapper.ToSaveData(BattleTestFactory.Create().Campaign);
            source.SaveVersion = 10;
            var battleCount = source.Battles.Count;
            var migrated = new CampaignSaveV10ToV11Migration().Apply(source);
            Assert.That(migrated.SaveVersion, Is.EqualTo(11));
            Assert.That(migrated.Battles.Count, Is.EqualTo(battleCount));
            Assert.That(migrated.Encounters, Is.Empty);
            Assert.That(migrated.Contracts, Is.Empty);
            Assert.That(source.SaveVersion, Is.EqualTo(10));
        }

        [Test]
        public void HardcodedSchemaTenFixture_MigratesWithoutInventedState()
        {
            const string fixture = "FOC_CAMPAIGN_SAVE\nSaveVersion=10\nCampaignId=Y2FtcGFpZ24tbGVnYWN5\nGameVersion=MC4wLjEw\nContentDataVersion=YmF0dGxlLTE=\nWorldSeed=1648\nWorldGenRevision=1\nWorldTime=9\nRngState=123\nRngDrawCount=2\nCharacterCount=0\nCharacterRelationCount=0\nSocialState=AAAAAAAAAAAAAAAA\nReligionState=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nCityState=AAAAAA==\nEconomyState=AAAAAAAAAAAAAAAAAAAAAA==\nDiplomacyState=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\nMilitaryState=AAAAAAAAAAAAAAAA\nSoldierState=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\nBattleState=AAAAAA==\n";
            var read = new CampaignSaveTextSerializer().Deserialize(fixture);
            Assert.That(read.Success, Is.True, read.Error);
            var migrated = new CampaignSaveV10ToV11Migration().Apply(read.Data!);
            Assert.That(migrated.SaveVersion, Is.EqualTo(11));
            Assert.That(migrated.Encounters, Is.Empty);
            Assert.That(migrated.Contracts, Is.Empty);
        }

        [Test]
        public void RuntimeInvariantValidation_RejectsMissingDefinition()
        {
            var setup = Trigger("encounter-missing-definition", "proof-bandit");
            Assert.Throws<KeyNotFoundException>(() => new EncounterContractInvariantValidator().ValidateOrThrow(setup.Campaign, new EncounterDefinitionCatalog(), ProofEncounterContractContent.Contracts()));
        }

        [Test]
        public void EncounterContractSources_AreUnityPresentationAndGlobalRandomFree()
        {
            var root = FindRepoRoot();
            var paths = Directory.GetFiles(Path.Combine(root, "UnityProject", "Assets", "FOC", "Domain", "EncountersContracts"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(root, "UnityProject", "Assets", "FOC", "Application", "EncountersContracts"), "*.cs", SearchOption.AllDirectories));
            foreach (var path in paths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("UnityEngine").And.Not.Contain("FOC.Presentation").And.Not.Contain("System.Random").And.Not.Contain("UnityEngine.Random"), path);
                Assert.That(source, Does.Not.Contain("Istanbul"), path);
            }
        }

        private static EncounterSetup Trigger(string id, string definition)
        {
            var campaign = SimpleCampaign();
            var service = new EncounterService(campaign, ProofEncounterContractContent.Encounters());
            var context = new EncounterContext(EncounterEntityRef.Character(Issuer), new[] { EncounterEntityRef.Character(Target) });
            var state = service.Trigger(EncounterId.Create(id), EncounterDefinitionId.Create(definition), context, Rng(), new AlwaysTrigger());
            return new EncounterSetup(campaign, service, state);
        }

        private static EncounterResolution Resolve(EncounterSetup setup, string choice) => setup.Service.Resolve(setup.State.Id, EncounterChoiceId.Create(choice), new AlwaysRequirements(), new DrawResolver(), new RecordingEncounterEffects());
        private static RandomState Rng() => new SeededRandomSource(123).CaptureState();

        private static CampaignRuntimeState SimpleCampaign()
        {
            var roster = new CharacterRoster();
            roster.Add(CharacterTestFactory.Named(Issuer.Value));
            roster.Add(CharacterTestFactory.Named(Target.Value));
            var cities = new FOC.Domain.Cities.CityRegistry();
            cities.Add(CityTestFactory.State("city-home"));
            cities.Add(CityTestFactory.State("city-other"));
            return new CampaignRuntimeState(StableId<CampaignTag>.Create("campaign"), "0.10", "encounter-contract-1", 123, 1, new WorldClock(new WorldTimestamp(10)), new SeededRandomSource(123), roster, cities: cities);
        }

        private static ContractDefinitionCatalog CharacterContractCatalog()
        {
            var catalog = new ContractDefinitionCatalog();
            catalog.Add(new ContractDefinition(ContractDefinitionId.Create("character-contract"), ContractCategory.InternalManagement, new[] { new ContractTargetSlotDefinition(TargetSlot, ContractTargetKind.Character) }, new[] { new ContractObjectiveDefinition(Objective, ContractEvidenceKind.CharacterAssignmentChanged, TargetSlot) }, "proof.contract.character"));
            return catalog;
        }

        private static ContractState OfferCharacter(ContractService service, string id, WorldTimestamp? deadline = null) => service.Offer(ContractId.Create(id), ContractDefinitionId.Create("character-contract"), ContractIssuerRef.Character(Issuer), new[] { ContractTargetBinding.Character(TargetSlot, Target) }, Target, deadline);

        private static CampaignRuntimeState CanonicalCampaign(bool reverse)
        {
            var campaign = SimpleCampaign();
            var service = new EncounterService(campaign, ProofEncounterContractContent.Encounters());
            var ids = reverse ? new[] { "encounter-z", "encounter-a" } : new[] { "encounter-a", "encounter-z" };
            foreach (var id in ids) service.Trigger(EncounterId.Create(id), EncounterDefinitionId.Create("proof-storm"), new EncounterContext(EncounterEntityRef.Character(Issuer), new[] { EncounterEntityRef.Character(Target) }), Rng(), new AlwaysTrigger());
            var contracts = new ContractService(campaign, CharacterContractCatalog());
            var contractIds = reverse ? new[] { "contract-z", "contract-a" } : new[] { "contract-a", "contract-z" };
            foreach (var id in contractIds) OfferCharacter(contracts, id);
            return campaign;
        }

        private static BattleResult BattleResultFor(BattleFixture f) => new BattleResult(
            f.Battle.Id, BattleEndReason.ObjectiveResolved, f.Campaign.Clock.Now, f.Battle.Step, f.SideA,
            new[]
            {
                new BattleUnitOutcome(f.UnitA1, 0, MoraleAssessment.Steady, FatigueAssessment.Fatigued, DisciplineAssessment.Ordered),
                new BattleUnitOutcome(f.UnitA2, 0, MoraleAssessment.Steady, FatigueAssessment.Rested, DisciplineAssessment.Ordered),
                new BattleUnitOutcome(f.UnitB1, 0, MoraleAssessment.Steady, FatigueAssessment.Rested, DisciplineAssessment.Ordered),
                new BattleUnitOutcome(f.UnitB2, 0, MoraleAssessment.Steady, FatigueAssessment.Rested, DisciplineAssessment.Ordered)
            },
            new[]
            {
                new BattleSoldierOutcome(f.SoldierA, BattleCasualtyOutcome.Survived),
                new BattleSoldierOutcome(f.SoldierB, BattleCasualtyOutcome.Survived),
                new BattleSoldierOutcome(f.SoldierMounted, BattleCasualtyOutcome.Survived)
            },
            Array.Empty<BattleCharacterOutcome>(), f.Battle.OrderedSides.Select(x => x.Id),
            f.Battle.Ammunition.OrderedAllocations.Select(x => new BattleAmmoOutcome(x.SoldierId, x.FamilyId, x.Quantity)));

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "FallOfCavalry.sln"))) current = current.Parent;
            return current?.FullName ?? throw new DirectoryNotFoundException();
        }

        private sealed class EncounterSetup
        {
            public EncounterSetup(CampaignRuntimeState campaign, EncounterService service, EncounterState state) { Campaign = campaign; Service = service; State = state; }
            public CampaignRuntimeState Campaign { get; }
            public EncounterService Service { get; }
            public EncounterState State { get; }
        }

        private sealed class AlwaysTrigger : IEncounterTriggerPolicy { public bool IsEligible(CampaignRuntimeState campaign, EncounterDefinition definition, EncounterContext context) => true; }
        private sealed class NeverTrigger : IEncounterTriggerPolicy { public bool IsEligible(CampaignRuntimeState campaign, EncounterDefinition definition, EncounterContext context) => false; }
        private sealed class AlwaysRequirements : IEncounterChoiceRequirementPolicy { public bool CanSelect(CampaignRuntimeState campaign, EncounterState encounter, EncounterChoiceDefinition choice) => true; }

        private sealed class DrawResolver : IEncounterChoiceResolver
        {
            public EncounterResolution Resolve(CampaignRuntimeState campaign, EncounterState encounter, EncounterChoiceDefinition choice, IRandomSource random, WorldTimestamp at)
            {
                var roll = random.NextInt(0, 100);
                return new EncounterResolution(EncounterOutcomeId.Create("outcome-" + roll), choice.Id, choice.PolicyId, at, random.CaptureState());
            }
        }

        private sealed class RecordingEncounterEffects : IAtomicEncounterEffectPolicy
        {
            private readonly bool _failPreflight;
            public RecordingEncounterEffects(bool failPreflight = false) { _failPreflight = failPreflight; }
            public int ApplyCount { get; private set; }
            public void Preflight(CampaignRuntimeState campaign, EncounterState encounter, EncounterResolution resolution) { if (_failPreflight) throw new InvalidOperationException("preflight"); }
            public void ApplyPreflighted(CampaignRuntimeState campaign, EncounterState encounter, EncounterResolution resolution) { ApplyCount++; }
        }

        private sealed class RecordingContractEffects : IAtomicContractOutcomePolicy
        {
            private readonly bool _failPreflight;
            public RecordingContractEffects(bool failPreflight = false) { _failPreflight = failPreflight; }
            public int ApplyCount { get; private set; }
            public void Preflight(CampaignRuntimeState campaign, ContractState contract) { if (_failPreflight) throw new InvalidOperationException("preflight"); }
            public void ApplyPreflighted(CampaignRuntimeState campaign, ContractState contract) { ApplyCount++; }
        }
    }
}
