#nullable enable
using System;
using System.Collections.Generic;
using FOC.Domain.AI;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Houses;
using FOC.Domain.Military;
using FOC.Domain.Organizations;
using FOC.Domain.Random;
using FOC.Domain.Religion;
using FOC.Domain.Soldiers;
using FOC.Domain.Time;
using FOC.Application.Battle;

namespace FOC.Application.Proof
{
    /// <summary>Deterministic development-only campaign. This is proof data, not historical content.</summary>
    public static class IntegratedProofCampaignFactory
    {
        public const string ContentVersion = "PROOF_ONLY-integrated-1";

        public static CampaignRuntimeState Create(bool reverseInsertion = false)
        {
            var clock = new WorldClock(new WorldTimestamp(10));
            var characters = Characters(reverseInsertion);
            var organizations = Organizations(reverseInsertion);
            var houses = Houses(characters);
            var cliques = Cliques();
            var religion = Religion();
            var cities = Cities(organizations);
            var economy = Economy(characters, cities);
            var diplomacy = Diplomacy();
            var military = Military(characters, organizations);
            var soldiers = Soldiers(military, economy, clock);
            var encounters = EncounterContracts();
            var ai = AI();
            var campaign = new CampaignRuntimeState(
                StableId<CampaignTag>.Create("integrated-proof-campaign"), "0.13-dev", ContentVersion, 130013, 1,
                clock, new SeededRandomSource(130013), characters, organizations, houses, cliques, religion, cities,
                economy, diplomacy, military, soldiers, encounterContracts: encounters, ai: ai);
            CreateBattle(campaign);
            return campaign;
        }

        private static CharacterRoster Characters(bool reverse)
        {
            var a = Named("commander-a", CharacterImportance.A);
            var b = Named("commander-b", CharacterImportance.B);
            var roster = new CharacterRoster();
            if (reverse) { roster.Add(b); roster.Add(a); } else { roster.Add(a); roster.Add(b); }
            roster.SetRelation(a.Id, b.Id, 18);
            return roster;
        }

        private static CharacterState Named(string id, CharacterImportance importance) => new CharacterState(
            new NamedCharacter(CharacterId.Create(id), id, CharacterProvenance.Registered),
            new CharacterStats(55, 50, 52, 60, 62, 45, 50, 58, 54), CharacterLocation.InCity(CityId.Create("city-home")),
            55, 50, 45, 40, 35, importance);

        private static OrganizationRegistry Organizations(bool reverse)
        {
            var a = Organization("org-a", CharacterId.Create("commander-a"), ArmyId.Create("army-a"), CityId.Create("city-home"));
            var b = Organization("org-b", CharacterId.Create("commander-b"), ArmyId.Create("army-b"), CityId.Create("city-other"));
            var registry = new OrganizationRegistry(); if (reverse) { registry.Add(b); registry.Add(a); } else { registry.Add(a); registry.Add(b); } return registry;
        }

        private static OrganizationState Organization(string id, CharacterId commander, ArmyId army, CityId city)
        {
            var organization = new OrganizationState(OrganizationId.Create(id), id);
            organization.AddMembership(new OrganizationMembershipState(commander, OrganizationBranch.Party, OrganizationMembershipType.Full, new WorldTimestamp(1)));
            organization.AddAssignment(new AssignmentState(AssignmentId.Create("assignment-" + id), commander, OrganizationBranch.Army, "army-commander", AssignmentAuthority.FullAuthority, AssignmentTarget.Army(army), AssignmentPresence.PhysicalPresenceRequired, new WorldTimestamp(1)));
            organization.AddAssignment(new AssignmentState(AssignmentId.Create("kethuda-" + id), commander, OrganizationBranch.Settlements, CityOfficialRoles.KethudaAssignmentRoleCode, AssignmentAuthority.Responsible, AssignmentTarget.City(city), AssignmentPresence.RemoteCapable, new WorldTimestamp(2)));
            return organization;
        }

        private static HouseRegistry Houses(CharacterRoster characters)
        {
            var house = new HouseState(new HouseDefinition(HouseId.Create("house-proof"), "Proof House"), 12, 900);
            house.AddMember(new HouseMember(CharacterId.Create("commander-a"), new WorldTimestamp(1)));
            house.AddMember(new HouseMember(CharacterId.Create("commander-b"), new WorldTimestamp(1)));
            house.SetHead(CharacterId.Create("commander-a"), characters);
            var registry = new HouseRegistry(); registry.Add(house); return registry;
        }

        private static CliqueRegistry Cliques()
        {
            var clique = new CliqueState(new CliqueDefinition(CliqueId.Create("clique-proof"), "Proof Circle", CliqueType.Merchant), attitude: CliqueAttitude.Supportive);
            clique.AddMembership(new CliqueMembership(CharacterId.Create("commander-b"), "member", new WorldTimestamp(3)));
            clique.SetLeader(CharacterId.Create("commander-b"));
            clique.AddInfluenceSource(new CliqueInfluenceSourceState(CharacterId.Create("commander-b"), InfluenceSourceKind.MemberReputation, 20));
            var registry = new CliqueRegistry(); registry.Add(clique); return registry;
        }

        private static ReligionCampaignState Religion()
        {
            var definitions = new ReligionRegistry(); var faith = new ReligionDefinition(ReligionId.Create("faith-proof"), "Proof Faith"); definitions.AddReligion(faith);
            var sect = new SectDefinition(SectId.Create("sect-proof"), faith.Id, "Proof Sect"); definitions.AddSect(sect);
            var characters = new CharacterReligionRegistry(); characters.Add(new CharacterReligionState(CharacterId.Create("commander-a"), faith.Id, sect.Id));
            var state = new ReligionCampaignState(definitions, characters); var profile = new ReligionProfile(ReligionProfileTarget.City(CityId.Create("city-home"))); profile.Add(new ReligionProfileEntry(faith.Id, sect.Id, 1)); state.AddProfile(profile); return state;
        }

        private static CityRegistry Cities(OrganizationRegistry organizations)
        {
            var home = City("city-home", "Proof Home"); var other = City("city-other", "Proof Other");
            ConfigureCity(home, organizations.GetRequired(OrganizationId.Create("org-a")), CharacterId.Create("commander-a"));
            ConfigureCity(other, organizations.GetRequired(OrganizationId.Create("org-b")), CharacterId.Create("commander-b"));
            var registry = new CityRegistry(); registry.Add(home); registry.Add(other); return registry;
        }

        private static void ConfigureCity(CityState city, OrganizationState organization, CharacterId official)
        {
            city.GetRequiredArea(CityAreaType.Trade).SetFullness(CityAreaFullness.Low); city.GetRequiredArea(CityAreaType.Trade).ActivateBuilding(CityBuildingId.Create("market-" + city.Id.Value));
            city.GetRequiredArea(CityAreaType.Military).SetFullness(CityAreaFullness.Low); city.GetRequiredArea(CityAreaType.Military).ActivateBuilding(CityBuildingId.Create("barracks-" + city.Id.Value));
            city.GetRequiredArea(CityAreaType.FoodSupply).SetFullness(CityAreaFullness.Low); city.GetRequiredArea(CityAreaType.FoodSupply).ActivateBuilding(CityBuildingId.Create("mill-" + city.Id.Value));
            city.SetInfrastructure(new CityInfrastructureState(CityInfrastructureType.Cistern, true, CityInfrastructureCondition.Serviceable));
            city.AddOfficial(new CityOfficialReference(CityOfficialRole.Kethuda, organization.Id, AssignmentId.Create("kethuda-" + organization.Id.Value)));
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

        private static EconomyState Economy(CharacterRoster characters, CityRegistry cities)
        {
            var goods = new TradeGoodRegistry();
            foreach (var good in new[] { Good("grain-proof", TradeGoodCategory.Food, 10), Good("flour-proof", TradeGoodCategory.Food, 14), Good("weapon-good", TradeGoodCategory.Military, 20), Good("armor-good", TradeGoodCategory.Military, 25), Good("ammo-good", TradeGoodCategory.Military, 3), Good("mount-good", TradeGoodCategory.RawMaterial, 30) }) goods.Add(good);
            var recipes = new ProductionRecipeRegistry(); recipes.Add(new ProductionRecipeDefinition(ProductionRecipeId.Create("mill-proof"), "Proof Mill", CityBuildingKind.Mill, new[] { new RecipeGoodsLine(TradeGoodId.Create("grain-proof"), 2) }, new[] { new RecipeGoodsLine(TradeGoodId.Create("flour-proof"), 1) }));
            var state = new EconomyState(goods, recipes);
            var stock = new CityStockState(); stock.AddInitial(new TradeGoodStock(TradeGoodId.Create("grain-proof"), 100)); stock.AddInitial(new TradeGoodStock(TradeGoodId.Create("flour-proof"), 20));
            var demand = new CityDemandState(); demand.AddSource(new DemandSourceState("proof-population", DemandSourceKind.Population, TradeGoodId.Create("grain-proof"), 5));
            state.AddMarket(new CityMarketState(CityId.Create("city-home"), 1000, stock, demand)); state.AddMarket(new CityMarketState(CityId.Create("city-other"), 800));
            var caravan = new CaravanState(CaravanId.Create("caravan-proof"), EconomicOwnerRef.Character(CharacterId.Create("commander-a")), CharacterId.Create("commander-a"), CityId.Create("city-home"), CityId.Create("city-other"), 200, 300);
            caravan.Cargo.Add(TradeGoodId.Create("grain-proof"), 10, caravan.WeightCapacity, goods); state.Caravans.Add(caravan); return state;
        }

        private static TradeGoodDefinition Good(string id, TradeGoodCategory category, long value) => new TradeGoodDefinition(TradeGoodId.Create(id), id, category, 1, category == TradeGoodCategory.Food, category == TradeGoodCategory.Military, category == TradeGoodCategory.Luxury, value);

        private static DiplomacyState Diplomacy()
        {
            var state = new DiplomacyState(); var own = FactionId.Create("faction-proof"); var foreign = FactionId.Create("faction-foreign");
            state.Actors.Add(new DiplomaticActorState(own, "Proof Faction")); state.Actors.Add(new DiplomaticActorState(foreign, "Foreign Faction"));
            state.Relations.Add(new DiplomaticRelationState(new DiplomaticActorPair(own, foreign), DiplomaticDisposition.Neutral, new WorldTimestamp(2)));
            var ownInfo = new ActorInformationState(own); state.Information.Add(ownInfo); state.Information.Add(new ActorInformationState(foreign));
            var report = new ReportState(ReportId.Create("report-proof"), ReportType.Military, ReportSourceRef.Character(ReportSourceKind.Official, CharacterId.Create("commander-a")), own, ReportQuality.High, ReportDetailLevel.Detailed, new WorldTimestamp(3), new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Army, "army-b"), ReportObservationKind.ArmyEstimatedStrength, ObservationPrecision.Approximate, 5) });
            state.Reports.Add(report); report.Dispatch(new WorldTimestamp(4)); ownInfo.Receive(report, new WorldTimestamp(5)); return state;
        }

        private static CampaignMilitaryState Military(CharacterRoster characters, OrganizationRegistry organizations)
        {
            var state = new CampaignMilitaryState();
            foreach (var suffix in new[] { "a", "b" })
            {
                var commander = CharacterId.Create("commander-" + suffix); var organization = OrganizationId.Create("org-" + suffix); var armyId = ArmyId.Create("army-" + suffix);
                var source = new RecruitmentSourceState(RecruitmentSourceId.Create("source-" + suffix), RecruitmentType.Retinue, 20, new RecruitmentAuthorityReference(commander, organization, AssignmentId.Create("assignment-org-" + suffix)));
                state.RecruitmentSources.Add(source);
                var army = new ArmyState(armyId, "Army " + suffix.ToUpperInvariant(), ArmyOwnerRef.Character(commander), ArmyOwnerRef.Character(commander), ArmyLocation.InCity(CityId.Create(suffix == "a" ? "city-home" : "city-other")), new ArmyCommanderReference(commander, organization, AssignmentId.Create("assignment-org-" + suffix)));
                army.SetLifecycle(ArmyLifecycle.Active); state.Armies.Add(army);
                var unit = new UnitGroupState(UnitGroupId.Create("unit-" + suffix), armyId, source.Id, suffix == "a" ? "troop-melee" : "troop-ranged", 2, commander, new MoraleState(MoraleAssessment.Steady), new FatigueState(FatigueAssessment.Rested), new DisciplineState(DisciplineAssessment.Ordered));
                state.RestoreUnit(unit); state.RestoreRecord(new RecruitmentRecord(RecruitmentRecordId.Create("record-" + suffix), source.Id, armyId, unit.Id, 2, new WorldTimestamp(1)));
            }
            return state;
        }

        private static SoldierCampaignState Soldiers(CampaignMilitaryState military, EconomyState economy, WorldClock clock)
        {
            var state = new SoldierCampaignState();
            state.Definitions.Add(new TroopDefinition(TroopDefinitionId.Create("troop-melee"), "Melee", UnitClassId.Create("infantry"), CombatRoleId.Create("melee"), MountContext.Either, VisualProfileId.Create("proof"), new[] { WeaponFamily.Sword }));
            state.Definitions.Add(new TroopDefinition(TroopDefinitionId.Create("troop-ranged"), "Ranged", UnitClassId.Create("archer"), CombatRoleId.Create("ranged"), MountContext.InfantryOnly, VisualProfileId.Create("proof"), new[] { WeaponFamily.Bow }));
            state.Definitions.Add(new WeaponDefinition(WeaponDefinitionId.Create("sword"), "Sword", WeaponFamily.Sword, new[] { WeaponSlot.Main }, new[] { new WeaponAttackOption(AttackMode.Slash, DamageType.Cut) }, MountContext.Either, TradeGoodId.Create("weapon-good"), VisualProfileId.Create("sword")));
            state.Definitions.Add(new WeaponDefinition(WeaponDefinitionId.Create("bow"), "Bow", WeaponFamily.Bow, new[] { WeaponSlot.Main }, new[] { new WeaponAttackOption(AttackMode.Thrust, DamageType.Pierce) }, MountContext.Either, TradeGoodId.Create("weapon-good"), VisualProfileId.Create("bow"), true, AmmoFamilyId.Create("arrow")));
            state.Definitions.Add(new AuxiliaryEquipmentDefinition(AuxiliaryEquipmentDefinitionId.Create("arrows"), "Arrows", AuxiliaryEquipmentKind.Ammunition, TradeGoodId.Create("ammo-good"), VisualProfileId.Create("arrows"), AmmoFamilyId.Create("arrow")));
            state.Definitions.Add(new MountDefinition(MountDefinitionId.Create("horse"), "Horse", TradeGoodId.Create("mount-good"), VisualProfileId.Create("horse"), "mounted"));
            var sword = Equipment("sword-a", EquipmentDefinitionRef.Weapon(WeaponDefinitionId.Create("sword")), "soldier-a", "record-a", TradeGoodId.Create("weapon-good"), clock.Now);
            var bow = Equipment("bow-b", EquipmentDefinitionRef.Weapon(WeaponDefinitionId.Create("bow")), "soldier-b", "record-b", TradeGoodId.Create("weapon-good"), clock.Now);
            var arrows = Equipment("arrows-b", EquipmentDefinitionRef.Auxiliary(AuxiliaryEquipmentDefinitionId.Create("arrows")), "soldier-b", "record-b", TradeGoodId.Create("ammo-good"), clock.Now);
            var horse = Equipment("horse-a", EquipmentDefinitionRef.Mount(MountDefinitionId.Create("horse")), "soldier-a", "record-a", TradeGoodId.Create("mount-good"), clock.Now);
            state.RestoreEquipment(sword); state.RestoreEquipment(bow); state.RestoreEquipment(arrows); state.RestoreEquipment(horse);
            state.RestoreSoldier(new SoldierInstance(SoldierId.Create("soldier-a"), UnitGroupId.Create("unit-a"), TroopDefinitionId.Create("troop-melee"), new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("source-a"), RecruitmentRecordId.Create("record-a")), new SoldierLoadout(new[] { new WeaponSlotAssignment(WeaponSlot.Main, sword.Id) }, mount: horse.Id), CombatRoleId.Create("melee")));
            state.RestoreSoldier(new SoldierInstance(SoldierId.Create("soldier-b"), UnitGroupId.Create("unit-b"), TroopDefinitionId.Create("troop-ranged"), new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("source-b"), RecruitmentRecordId.Create("record-b")), new SoldierLoadout(new[] { new WeaponSlotAssignment(WeaponSlot.Main, bow.Id), new WeaponSlotAssignment(WeaponSlot.Ammo, arrows.Id) }), CombatRoleId.Create("ranged")));
            return state;
        }

        private static EquipmentInstance Equipment(string id, EquipmentDefinitionRef definition, string soldier, string record, TradeGoodId good, WorldTimestamp at) => new EquipmentInstance(EquipmentInstanceId.Create("equipment-" + id), definition, EquipmentOwnerRef.Soldier(SoldierId.Create(soldier)), EquipmentAcquisitionProvenance.RecruitmentProvided(RecruitmentRecordId.Create(record), good, at));

        private static EncounterContractCampaignState EncounterContracts()
        {
            var state = new EncounterContractCampaignState();
            state.Encounters.Add(new EncounterState(EncounterId.Create("encounter-proof"), EncounterDefinitionId.Create("proof-bandit"), EncounterFamily.Roaming, EncounterType.Bandit, new WorldTimestamp(6), new EncounterContext(EncounterEntityRef.Character(CharacterId.Create("commander-a")), new[] { EncounterEntityRef.Character(CharacterId.Create("commander-b")) }), new RandomState(77, 0)));
            var slot = ContractTargetSlotId.Create("target"); var objective = new ContractObjectiveState(ContractObjectiveId.Create("objective-proof"), ContractEvidenceKind.CharacterAssignmentChanged, slot, 1);
            var contract = new ContractState(ContractId.Create("contract-proof"), ContractDefinitionId.Create("proof-internal"), ContractCategory.InternalManagement, ContractIssuerRef.Character(CharacterId.Create("commander-a")), new[] { ContractTargetBinding.Character(slot, CharacterId.Create("commander-b")) }, new[] { objective }, new WorldTimestamp(7), CharacterId.Create("commander-b")); contract.Accept(new WorldTimestamp(8)); state.Contracts.Add(contract); return state;
        }

        private static AICampaignState AI()
        {
            var state = new AICampaignState(); var owner = AIDecisionOwnerRef.Character(CharacterId.Create("commander-b"));
            state.Controllers.Add(new AIControllerState(AIControllerId.Create("controller-proof"), owner, AIPriorityProfileId.Create("priority-proof"), AIDecisionQualityProfileId.Create("quality-proof"), AISchedulingProfileId.Create("schedule-proof"), new RandomState(88, 0), CharacterAIProfileId.Create("character-profile-proof"), new WorldTimestamp(4), new WorldTimestamp(20))); return state;
        }

        private static void CreateBattle(CampaignRuntimeState campaign)
        {
            var sideA = BattleSideId.Create("side-a"); var sideB = BattleSideId.Create("side-b"); var graph = new BattleSectorGraph();
            var sectorA = BattleSectorId.Create("sector-a"); var middle = BattleSectorId.Create("sector-middle"); var sectorB = BattleSectorId.Create("sector-b");
            graph.Add(new BattleSector(sectorA, new[] { BattleTerrainTag.Open }, new[] { sideA })); graph.Add(new BattleSector(middle, new[] { BattleTerrainTag.Rough })); graph.Add(new BattleSector(sectorB, new[] { BattleTerrainTag.Open }, new[] { sideB })); graph.Connect(sectorA, middle); graph.Connect(middle, sectorB);
            var ammo = new BattleAmmunitionState(); ammo.Add(new BattleAmmoAllocation(SoldierId.Create("soldier-b"), AmmoFamilyId.Create("arrow"), 2));
            var battle = new BattleCreationService(campaign).Create(new BattleCreationRequest(BattleId.Create("battle-main"), campaign.Clock.Now, new SeededRandomSource(123).CaptureState(), new[] { new BattleSidePlan(sideA, CharacterId.Create("commander-a"), new[] { ArmyId.Create("army-a") }), new BattleSidePlan(sideB, CharacterId.Create("commander-b"), new[] { ArmyId.Create("army-b") }) }, graph, ammo));
            battle.BeginDeployment(); battle.Deploy(new BattleDeployment(DeploymentGroupId.Create("deploy-a"), sideA, ArmyId.Create("army-a"), UnitGroupId.Create("unit-a"), sectorA, BattleFormation.Line)); battle.Deploy(new BattleDeployment(DeploymentGroupId.Create("deploy-b"), sideB, ArmyId.Create("army-b"), UnitGroupId.Create("unit-b"), sectorB, BattleFormation.Line)); battle.BeginActive();
        }
    }
}
