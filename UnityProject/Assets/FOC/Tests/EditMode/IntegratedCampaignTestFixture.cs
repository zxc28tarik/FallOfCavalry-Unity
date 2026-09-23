#nullable enable
using System;
using System.Collections.Generic;
using FOC.Application.Proof;
using FOC.Domain.AI;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Houses;
using FOC.Domain.Organizations;
using FOC.Domain.Random;
using FOC.Domain.Religion;
using FOC.Domain.Time;

namespace FOC.Tests
{
    internal static class IntegratedCampaignTestFixture
    {
        public const string ContentVersion = "PROOF_ONLY-integrated-1";
        public static CampaignRuntimeState Create(bool reverse = false) => IntegratedProofCampaignFactory.Create(reverse);

        private static void AddSocial(CampaignRuntimeState campaign)
        {
            var commander = CharacterId.Create("commander-a");
            var second = CharacterId.Create("unit-commander-a");
            var organization = campaign.Organizations.GetRequired(OrganizationId.Create("org-a"));
            organization.AddMembership(new OrganizationMembershipState(commander, OrganizationBranch.Party, OrganizationMembershipType.Full, new WorldTimestamp(1)));
            organization.AddMembership(new OrganizationMembershipState(second, OrganizationBranch.Household, OrganizationMembershipType.Full, new WorldTimestamp(1)));
            organization.AddAssignment(new AssignmentState(AssignmentId.Create("kethuda-proof"), second, OrganizationBranch.Settlements, CityOfficialRoles.KethudaAssignmentRoleCode, AssignmentAuthority.Responsible, AssignmentTarget.City(CityId.Create("city-home")), AssignmentPresence.RemoteCapable, new WorldTimestamp(2)));

            var house = new HouseState(new HouseDefinition(HouseId.Create("house-proof"), "Proof House"), 12, 900);
            house.AddMember(new HouseMember(commander, new WorldTimestamp(1)));
            house.AddMember(new HouseMember(second, new WorldTimestamp(1)));
            house.SetHead(commander, campaign.Characters);
            campaign.Houses.Add(house);

            var clique = new CliqueState(new CliqueDefinition(CliqueId.Create("clique-proof"), "Proof Circle", CliqueType.Merchant), attitude: CliqueAttitude.Supportive);
            clique.AddMembership(new CliqueMembership(second, "member", new WorldTimestamp(3)));
            clique.SetLeader(second);
            clique.AddInfluenceSource(new CliqueInfluenceSourceState(second, InfluenceSourceKind.MemberReputation, 20));
            campaign.Cliques.Add(clique);
            campaign.Characters.SetRelation(commander, second, 18);
        }

        private static void AddReligion(CampaignRuntimeState campaign)
        {
            var faith = new ReligionDefinition(ReligionId.Create("faith-proof"), "Proof Faith");
            campaign.Religion.Definitions.AddReligion(faith);
            var sect = new SectDefinition(SectId.Create("sect-proof"), faith.Id, "Proof Sect");
            campaign.Religion.Definitions.AddSect(sect);
            campaign.Religion.Characters.Add(new CharacterReligionState(CharacterId.Create("commander-a"), faith.Id, sect.Id));
            var cityProfile = new ReligionProfile(ReligionProfileTarget.City(CityId.Create("city-home")));
            cityProfile.Add(new ReligionProfileEntry(faith.Id, sect.Id, 1));
            campaign.Religion.AddProfile(cityProfile);
        }

        private static void AddCitiesAndEconomy(CampaignRuntimeState campaign)
        {
            var home = City("city-home", "Proof Home");
            home.GetRequiredArea(CityAreaType.Trade).SetFullness(CityAreaFullness.Low);
            home.GetRequiredArea(CityAreaType.Trade).ActivateBuilding(CityBuildingId.Create("market-city-home"));
            home.GetRequiredArea(CityAreaType.Military).SetFullness(CityAreaFullness.Low);
            home.GetRequiredArea(CityAreaType.Military).ActivateBuilding(CityBuildingId.Create("barracks-city-home"));
            home.GetRequiredArea(CityAreaType.FoodSupply).SetFullness(CityAreaFullness.Low);
            home.GetRequiredArea(CityAreaType.FoodSupply).ActivateBuilding(CityBuildingId.Create("mill-city-home"));
            home.SetInfrastructure(new CityInfrastructureState(CityInfrastructureType.Cistern, true, CityInfrastructureCondition.Serviceable));
            home.AddOfficial(new CityOfficialReference(CityOfficialRole.Kethuda, OrganizationId.Create("org-a"), AssignmentId.Create("kethuda-proof")));
            var other = City("city-other", "Proof Other");
            campaign.Cities.Add(home); campaign.Cities.Add(other);

            var grain = new TradeGoodDefinition(TradeGoodId.Create("grain-proof"), "Proof Grain", TradeGoodCategory.Food, 1, true, false, false, 10);
            var flour = new TradeGoodDefinition(TradeGoodId.Create("flour-proof"), "Proof Flour", TradeGoodCategory.Food, 1, true, false, false, 14);
            campaign.Economy.Goods.Add(grain); campaign.Economy.Goods.Add(flour);
            campaign.Economy.Recipes.Add(new ProductionRecipeDefinition(ProductionRecipeId.Create("mill-proof"), "Proof Mill", CityBuildingKind.Mill, new[] { new RecipeGoodsLine(grain.Id, 2) }, new[] { new RecipeGoodsLine(flour.Id, 1) }));
            var stock = new CityStockState(); stock.AddInitial(new TradeGoodStock(grain.Id, 100)); stock.AddInitial(new TradeGoodStock(flour.Id, 20));
            var demand = new CityDemandState(); demand.AddSource(new DemandSourceState("proof-population", DemandSourceKind.Population, grain.Id, 5));
            campaign.Economy.AddMarket(new CityMarketState(home.Id, 1000, stock, demand));
            campaign.Economy.AddMarket(new CityMarketState(other.Id, 800));
            var caravan = new CaravanState(CaravanId.Create("caravan-proof"), EconomicOwnerRef.Character(CharacterId.Create("commander-a")), CharacterId.Create("commander-a"), home.Id, other.Id, 200, 300);
            caravan.Cargo.Add(grain.Id, 10, caravan.WeightCapacity, campaign.Economy.Goods);
            campaign.Economy.Caravans.Add(caravan);
        }

        private static CityState City(string id, string name)
        {
            var areas = new List<CityAreaDefinition>();
            foreach (CityAreaType type in Enum.GetValues(typeof(CityAreaType)))
            {
                var buildings = new List<CityBuildingDefinition>();
                if (type == CityAreaType.InnerCastle) buildings.Add(Building("inner-" + id, CityBuildingKind.InnerCastleWalls));
                if (type == CityAreaType.Trade) buildings.Add(Building("market-" + id, CityBuildingKind.Market));
                if (type == CityAreaType.Military) buildings.Add(Building("barracks-" + id, CityBuildingKind.Barracks));
                if (type == CityAreaType.FoodSupply) buildings.Add(Building("mill-" + id, CityBuildingKind.Mill));
                areas.Add(new CityAreaDefinition(type, buildings));
            }
            return new CityState(new CityDefinition(CityId.Create(id), name, areas), new CityMetricsState(1250));
        }

        private static CityBuildingDefinition Building(string id, CityBuildingKind kind) => new CityBuildingDefinition(CityBuildingId.Create(id), id, kind, CityBuildingRules.RequiredArea(kind));

        private static void AddDiplomacy(CampaignRuntimeState campaign)
        {
            var own = FactionId.Create("faction-proof"); var foreign = FactionId.Create("faction-foreign");
            campaign.Diplomacy.Actors.Add(new DiplomaticActorState(own, "Proof Faction"));
            campaign.Diplomacy.Actors.Add(new DiplomaticActorState(foreign, "Foreign Faction"));
            campaign.Diplomacy.Relations.Add(new DiplomaticRelationState(new DiplomaticActorPair(own, foreign), DiplomaticDisposition.Neutral, new WorldTimestamp(2)));
            var information = new ActorInformationState(own); campaign.Diplomacy.Information.Add(information);
            campaign.Diplomacy.Information.Add(new ActorInformationState(foreign));
            var report = new ReportState(ReportId.Create("report-proof"), ReportType.Military, ReportSourceRef.Character(ReportSourceKind.Official, CharacterId.Create("commander-a")), own, ReportQuality.High, ReportDetailLevel.Detailed, new WorldTimestamp(3), new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Army, "army-b"), ReportObservationKind.ArmyEstimatedStrength, ObservationPrecision.Approximate, 5) });
            campaign.Diplomacy.Reports.Add(report); report.Dispatch(new WorldTimestamp(4)); information.Receive(report, new WorldTimestamp(5));
        }

        private static void AddEncounterContract(CampaignRuntimeState campaign)
        {
            var encounter = new EncounterState(EncounterId.Create("encounter-proof"), EncounterDefinitionId.Create("proof-bandit"), EncounterFamily.Roaming, EncounterType.Bandit, new WorldTimestamp(6), new EncounterContext(EncounterEntityRef.Character(CharacterId.Create("commander-a")), new[] { EncounterEntityRef.Character(CharacterId.Create("commander-b")) }), new RandomState(77, 0));
            campaign.EncounterContracts.Encounters.Add(encounter);
            var slot = ContractTargetSlotId.Create("target");
            var objective = new ContractObjectiveState(ContractObjectiveId.Create("objective-proof"), ContractEvidenceKind.CharacterAssignmentChanged, slot, 1);
            var contract = new ContractState(ContractId.Create("contract-proof"), ContractDefinitionId.Create("proof-internal"), ContractCategory.InternalManagement, ContractIssuerRef.Character(CharacterId.Create("commander-a")), new[] { ContractTargetBinding.Character(slot, CharacterId.Create("unit-commander-a")) }, new[] { objective }, new WorldTimestamp(7), CharacterId.Create("unit-commander-a"));
            contract.Accept(new WorldTimestamp(8));
            campaign.EncounterContracts.Contracts.Add(contract);
        }

        private static void AddAI(CampaignRuntimeState campaign)
        {
            var owner = AIDecisionOwnerRef.Character(CharacterId.Create("commander-b"));
            campaign.AI.Controllers.Add(new AIControllerState(AIControllerId.Create("controller-proof"), owner, AIPriorityProfileId.Create("priority-proof"), AIDecisionQualityProfileId.Create("quality-proof"), AISchedulingProfileId.Create("schedule-proof"), new RandomState(88, 0), CharacterAIProfileId.Create("character-profile-proof"), new WorldTimestamp(4), new WorldTimestamp(20)));
        }
    }
}
