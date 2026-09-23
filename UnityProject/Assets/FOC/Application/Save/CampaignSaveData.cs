using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveData
    {
        public const int CurrentSaveVersion = 11;

        public int SaveVersion { get; set; }

        public string CampaignId { get; set; } = string.Empty;

        public string GameVersion { get; set; } = string.Empty;

        public string ContentDataVersion { get; set; } = string.Empty;

        public ulong WorldSeed { get; set; }

        public int WorldGenRevision { get; set; }

        public long WorldTime { get; set; }

        public ulong RngState { get; set; }

        public ulong RngDrawCount { get; set; }

        public List<CharacterSaveData> Characters { get; set; } = new List<CharacterSaveData>();

        public List<CharacterRelationSaveData> CharacterRelations { get; set; } = new List<CharacterRelationSaveData>();

        public List<OrganizationSaveData> Organizations { get; set; } = new List<OrganizationSaveData>();
        public List<HouseSaveData> Houses { get; set; } = new List<HouseSaveData>();
        public List<CliqueSaveData> Cliques { get; set; } = new List<CliqueSaveData>();
        public List<ReligionDefinitionSaveData> Religions { get; set; } = new List<ReligionDefinitionSaveData>();
        public List<SectDefinitionSaveData> Sects { get; set; } = new List<SectDefinitionSaveData>();
        public List<CharacterReligionSaveData> CharacterReligions { get; set; } = new List<CharacterReligionSaveData>();
        public List<ReligionProfileSaveData> ReligionProfiles { get; set; } = new List<ReligionProfileSaveData>();
        public List<ReligionPolicySaveData> ReligionPolicies { get; set; } = new List<ReligionPolicySaveData>();
        public List<ReligiousCliqueAssociationSaveData> ReligiousCliqueAssociations { get; set; } = new List<ReligiousCliqueAssociationSaveData>();
        public List<CitySaveData> Cities { get; set; } = new List<CitySaveData>();
        public List<TradeGoodSaveData> TradeGoods{get;set;}=new List<TradeGoodSaveData>();
        public List<ProductionRecipeSaveData> ProductionRecipes{get;set;}=new List<ProductionRecipeSaveData>();
        public List<CityMarketSaveData> CityMarkets{get;set;}=new List<CityMarketSaveData>();
        public List<CaravanSaveData> Caravans{get;set;}=new List<CaravanSaveData>();
        public List<DiplomaticActorSaveData> DiplomaticActors{get;set;}=new List<DiplomaticActorSaveData>();
        public List<DiplomaticRelationSaveData> DiplomaticRelations{get;set;}=new List<DiplomaticRelationSaveData>();
        public List<EnvoyMissionSaveData> EnvoyMissions{get;set;}=new List<EnvoyMissionSaveData>();
        public List<DiplomaticMessageSaveData> DiplomaticMessages{get;set;}=new List<DiplomaticMessageSaveData>();
        public List<DiplomaticActionSaveData> DiplomaticActions{get;set;}=new List<DiplomaticActionSaveData>();
        public List<ReportSaveData> Reports{get;set;}=new List<ReportSaveData>();
        public List<ActorInformationSaveData> ActorInformation{get;set;}=new List<ActorInformationSaveData>();
        public List<DiplomaticAgreementSaveData> DiplomaticAgreements{get;set;}=new List<DiplomaticAgreementSaveData>();
        public List<ArmySaveData> Armies{get;set;}=new List<ArmySaveData>();
        public List<RecruitmentSourceSaveData> RecruitmentSources{get;set;}=new List<RecruitmentSourceSaveData>();
        public List<RecruitmentRecordSaveData> RecruitmentRecords{get;set;}=new List<RecruitmentRecordSaveData>();
        public List<TroopDefinitionSaveData> TroopDefinitions{get;set;}=new List<TroopDefinitionSaveData>();
        public List<WeaponDefinitionSaveData> WeaponDefinitions{get;set;}=new List<WeaponDefinitionSaveData>();
        public List<ArmorDefinitionSaveData> ArmorDefinitions{get;set;}=new List<ArmorDefinitionSaveData>();
        public List<ShieldDefinitionSaveData> ShieldDefinitions{get;set;}=new List<ShieldDefinitionSaveData>();
        public List<MountDefinitionSaveData> MountDefinitions{get;set;}=new List<MountDefinitionSaveData>();
        public List<AuxiliaryEquipmentDefinitionSaveData> AuxiliaryEquipmentDefinitions{get;set;}=new List<AuxiliaryEquipmentDefinitionSaveData>();
        public List<EquipmentInstanceSaveData> EquipmentInstances{get;set;}=new List<EquipmentInstanceSaveData>();
        public List<SoldierSaveData> Soldiers{get;set;}=new List<SoldierSaveData>();
        public List<BattleSaveData> Battles{get;set;}=new List<BattleSaveData>();
        public List<EncounterSaveData> Encounters{get;set;}=new List<EncounterSaveData>();
        public List<ContractSaveData> Contracts{get;set;}=new List<ContractSaveData>();
    }

    public sealed class CharacterSaveData
    {
        public string CharacterId { get; set; } = string.Empty;
        public int IdentityKind { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int Provenance { get; set; }
        public int? Importance { get; set; }
        public int Intelligence { get; set; }
        public int Observation { get; set; }
        public int Persuasion { get; set; }
        public int Leadership { get; set; }
        public int Command { get; set; }
        public int Trade { get; set; }
        public int Administration { get; set; }
        public int Courage { get; set; }
        public int Experience { get; set; }
        public int BaseLoyalty { get; set; }
        public int CurrentLoyalty { get; set; }
        public int Satisfaction { get; set; }
        public int BaseReputation { get; set; }
        public int CurrentStanding { get; set; }
        public CharacterLocationSaveData Location { get; set; } = new CharacterLocationSaveData();
        public InjurySaveData? Injury { get; set; }
        public DeathSaveData? Death { get; set; }
        public int HistoryCapacity { get; set; }
        public List<CharacterHistorySaveData> History { get; set; } = new List<CharacterHistorySaveData>();
    }

    public sealed class CharacterLocationSaveData
    {
        public int Kind { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public long X { get; set; }
        public long Y { get; set; }
        public string CaptorId { get; set; } = string.Empty;
        public int CaptivityStatus { get; set; }
        public int CaptivitySiteKind { get; set; }
        public string CaptivitySiteTargetId { get; set; } = string.Empty;
        public long CaptivitySiteX { get; set; }
        public long CaptivitySiteY { get; set; }
    }

    public sealed class InjurySaveData
    {
        public int Severity { get; set; }
        public long OccurredAt { get; set; }
        public long? ExpectedRecoveryAt { get; set; }
    }

    public sealed class DeathSaveData
    {
        public int Cause { get; set; }
        public long OccurredAt { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class CharacterHistorySaveData
    {
        public long Sequence { get; set; }
        public long OccurredAt { get; set; }
        public int Kind { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class CharacterRelationSaveData
    {
        public string FirstCharacterId { get; set; } = string.Empty;
        public string SecondCharacterId { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public sealed class OrganizationSaveData
    {
        public string OrganizationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<OrganizationMembershipSaveData> Memberships { get; set; } = new List<OrganizationMembershipSaveData>();
        public List<AssignmentSaveData> Assignments { get; set; } = new List<AssignmentSaveData>();
    }
    public sealed class OrganizationMembershipSaveData
    {
        public string CharacterId { get; set; } = string.Empty;
        public int Branch { get; set; }
        public int MembershipType { get; set; }
        public long StartedAt { get; set; }
        public bool IsActive { get; set; }
    }
    public sealed class AssignmentSaveData
    {
        public string AssignmentId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public int Branch { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public int Authority { get; set; }
        public int TargetKind { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public long TargetX { get; set; }
        public long TargetY { get; set; }
        public int Presence { get; set; }
        public long StartedAt { get; set; }
        public int Status { get; set; }
    }
    public sealed class HouseSaveData
    {
        public string HouseId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string HeadCharacterId { get; set; } = string.Empty;
        public bool SuccessionPending { get; set; }
        public int Prestige { get; set; }
        public long Wealth { get; set; }
        public int Lifecycle { get; set; }
        public List<HouseMemberSaveData> Members { get; set; } = new List<HouseMemberSaveData>();
        public List<MarriageSaveData> Marriages { get; set; } = new List<MarriageSaveData>();
        public List<FamilyLinkSaveData> FamilyLinks { get; set; } = new List<FamilyLinkSaveData>();
        public List<HousePropertySaveData> Properties { get; set; } = new List<HousePropertySaveData>();
        public List<InheritanceSaveData> Inheritances { get; set; } = new List<InheritanceSaveData>();
    }
    public sealed class HouseMemberSaveData { public string CharacterId { get; set; } = string.Empty; public long JoinedAt { get; set; } public bool IsActive { get; set; } }
    public sealed class MarriageSaveData { public string FirstCharacterId { get; set; } = string.Empty; public string SecondCharacterId { get; set; } = string.Empty; public long StartedAt { get; set; } public bool IsActive { get; set; } }
    public sealed class FamilyLinkSaveData { public string FirstCharacterId { get; set; } = string.Empty; public string SecondCharacterId { get; set; } = string.Empty; public int Kind { get; set; } }
    public sealed class HousePropertySaveData { public string AssetId { get; set; } = string.Empty; public int Kind { get; set; } }
    public sealed class InheritanceSaveData { public string AssetId { get; set; } = string.Empty; public int Kind { get; set; } public string HeirCharacterId { get; set; } = string.Empty; public int Status { get; set; } }
    public sealed class CliqueSaveData
    {
        public string CliqueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
        public int Lifecycle { get; set; }
        public int Attitude { get; set; }
        public string ParentCliqueId { get; set; } = string.Empty;
        public string LeaderCharacterId { get; set; } = string.Empty;
        public List<CliqueMembershipSaveData> Memberships { get; set; } = new List<CliqueMembershipSaveData>();
        public List<CliqueInfluenceSourceSaveData> InfluenceSources { get; set; } = new List<CliqueInfluenceSourceSaveData>();
    }
    public sealed class CliqueMembershipSaveData { public string CharacterId { get; set; } = string.Empty; public string RoleCode { get; set; } = string.Empty; public long JoinedAt { get; set; } public bool IsActive { get; set; } }
    public sealed class CliqueInfluenceSourceSaveData { public string CharacterId { get; set; } = string.Empty; public int Kind { get; set; } public int Contribution { get; set; } }
    public sealed class ReligionDefinitionSaveData { public string ReligionId { get; set; }=string.Empty; public string Name { get; set; }=string.Empty; public int Status { get; set; } }
    public sealed class SectDefinitionSaveData { public string SectId { get; set; }=string.Empty; public string ParentReligionId { get; set; }=string.Empty; public string Name { get; set; }=string.Empty; public int Status { get; set; } }
    public sealed class CharacterReligionSaveData { public string CharacterId { get; set; }=string.Empty; public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; }
    public sealed class ReligionProfileEntrySaveData { public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; public int RelativePresence { get; set; } }
    public sealed class ReligionProfileSaveData { public int TargetKind { get; set; } public string TargetId { get; set; }=string.Empty; public List<ReligionProfileEntrySaveData> Entries { get; set; }=new List<ReligionProfileEntrySaveData>(); }
    public sealed class ReligionPolicyRuleSaveData { public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; public int Recognition { get; set; } public int Treatment { get; set; } public int Enforcement { get; set; } }
    public sealed class ReligionPolicySaveData { public int TargetKind { get; set; } public string TargetId { get; set; }=string.Empty; public List<ReligionPolicyRuleSaveData> Rules { get; set; }=new List<ReligionPolicyRuleSaveData>(); }
    public sealed class ReligiousCliqueAssociationSaveData { public string CliqueId { get; set; }=string.Empty; public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; }
    public sealed class CitySaveData
    {
        public string CityId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public long PopulationCount{get;set;}public int Wealth{get;set;}public int Order{get;set;}public int Health{get;set;}public int Security{get;set;}
        public List<CityAreaSaveData> Areas{get;set;}=new List<CityAreaSaveData>();public List<CityInfrastructureSaveData> Infrastructure{get;set;}=new List<CityInfrastructureSaveData>();public List<CityOfficialSaveData> Officials{get;set;}=new List<CityOfficialSaveData>();
    }
    public sealed class CityAreaSaveData { public int Type{get;set;}public int Fullness{get;set;}public string VisualVariantHook{get;set;}=string.Empty;public List<CityBuildingSaveData> BuildingPool{get;set;}=new List<CityBuildingSaveData>();public List<string> ActiveBuildingIds{get;set;}=new List<string>();public List<string> LockedBuildingIds{get;set;}=new List<string>(); }
    public sealed class CityBuildingSaveData { public string CityBuildingId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int Kind{get;set;}public int AreaType{get;set;}public int Status{get;set;}public List<int> EffectTags{get;set;}=new List<int>(); }
    public sealed class CityInfrastructureSaveData { public int Type{get;set;}public bool Installed{get;set;}public int Condition{get;set;} }
    public sealed class CityOfficialSaveData { public int Role{get;set;}public string OrganizationId{get;set;}=string.Empty;public string AssignmentId{get;set;}=string.Empty; }
    public sealed class TradeGoodSaveData{public string TradeGoodId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int Category{get;set;}public long UnitWeight{get;set;}public bool IsFood{get;set;}public bool IsMilitaryGood{get;set;}public bool IsLuxury{get;set;}public long? ReferenceUnitValue{get;set;}}
    public sealed class RecipeGoodsLineSaveData{public string TradeGoodId{get;set;}=string.Empty;public long Quantity{get;set;}}
    public sealed class ProductionRecipeSaveData{public string ProductionRecipeId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int BuildingKind{get;set;}public List<RecipeGoodsLineSaveData> Inputs{get;set;}=new List<RecipeGoodsLineSaveData>();public List<RecipeGoodsLineSaveData> Outputs{get;set;}=new List<RecipeGoodsLineSaveData>();}
    public sealed class TradeGoodStockSaveData{public string TradeGoodId{get;set;}=string.Empty;public long Quantity{get;set;}}
    public sealed class DemandSourceSaveData{public string SourceId{get;set;}=string.Empty;public int Kind{get;set;}public string TradeGoodId{get;set;}=string.Empty;public long Quantity{get;set;}}
    public sealed class CityMarketSaveData{public string CityId{get;set;}=string.Empty;public long CashBalance{get;set;}public List<TradeGoodStockSaveData> Stocks{get;set;}=new List<TradeGoodStockSaveData>();public List<DemandSourceSaveData> DemandSources{get;set;}=new List<DemandSourceSaveData>();}
    public sealed class RouteRiskSaveData{public string SourceId{get;set;}=string.Empty;public int Source{get;set;}}
    public sealed class CaravanSaveData{public string CaravanId{get;set;}=string.Empty;public int OwnerKind{get;set;}public string OwnerId{get;set;}=string.Empty;public string ManagerCharacterId{get;set;}=string.Empty;public string RepresentativeCharacterId{get;set;}=string.Empty;public string RepresentativeOrganizationId{get;set;}=string.Empty;public string RepresentativeAssignmentId{get;set;}=string.Empty;public string OriginCityId{get;set;}=string.Empty;public string DestinationCityId{get;set;}=string.Empty;public string RouteId{get;set;}=string.Empty;public long WeightCapacity{get;set;}public long CashBalance{get;set;}public int Lifecycle{get;set;}public int LocationStage{get;set;}public long PurchaseCost{get;set;}public long SaleRevenue{get;set;}public long OperatingCost{get;set;}public long Tariffs{get;set;}public long Losses{get;set;}public List<TradeGoodStockSaveData> Cargo{get;set;}=new List<TradeGoodStockSaveData>();public List<RouteRiskSaveData> RiskInputs{get;set;}=new List<RouteRiskSaveData>();}
    public sealed class DiplomaticActorSaveData{public string FactionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int Lifecycle{get;set;}}
    public sealed class DiplomaticFactorSaveData{public string SourceId{get;set;}=string.Empty;public int Source{get;set;}public int Direction{get;set;}public long OccurredAt{get;set;}}
    public sealed class DiplomaticRelationSaveData{public string FirstActorId{get;set;}=string.Empty;public string SecondActorId{get;set;}=string.Empty;public int Disposition{get;set;}public long UpdatedAt{get;set;}public List<DiplomaticFactorSaveData> Factors{get;set;}=new List<DiplomaticFactorSaveData>();}
    public sealed class EnvoyMissionSaveData{public string EnvoyMissionId{get;set;}=string.Empty;public string CharacterId{get;set;}=string.Empty;public string OrganizationId{get;set;}=string.Empty;public string AssignmentId{get;set;}=string.Empty;public string SourceActorId{get;set;}=string.Empty;public string TargetActorId{get;set;}=string.Empty;public int MissionType{get;set;}public int AuthorityScope{get;set;}public List<int> AllowedActions{get;set;}=new List<int>();public long CreatedAt{get;set;}public long? DepartedAt{get;set;}public long? ArrivedAt{get;set;}public long? CompletedAt{get;set;}public int Phase{get;set;}}
    public sealed class DiplomaticMessageSaveData{public string DiplomaticMessageId{get;set;}=string.Empty;public string SenderActorId{get;set;}=string.Empty;public string RecipientActorId{get;set;}=string.Empty;public int Kind{get;set;}public int CarrierKind{get;set;}public string CarrierCharacterId{get;set;}=string.Empty;public string EnvoyMissionId{get;set;}=string.Empty;public string ResponseToId{get;set;}=string.Empty;public long CreatedAt{get;set;}public long? DispatchedAt{get;set;}public long? DeliveredAt{get;set;}public int Status{get;set;}}
    public sealed class DiplomaticActionSaveData{public string DiplomaticActionId{get;set;}=string.Empty;public string SourceActorId{get;set;}=string.Empty;public string TargetActorId{get;set;}=string.Empty;public int Kind{get;set;}public string EnvoyMissionId{get;set;}=string.Empty;public string DiplomaticMessageId{get;set;}=string.Empty;public long OrderedAt{get;set;}public int Status{get;set;}public int? Outcome{get;set;}public long? ResolvedAt{get;set;}}
    public sealed class ReportObservationSaveData{public int SubjectKind{get;set;}public string SubjectId{get;set;}=string.Empty;public int Kind{get;set;}public int Precision{get;set;}public long? Lower{get;set;}public long? Upper{get;set;}public int Qualitative{get;set;}}
    public sealed class ReportSaveData{public string ReportId{get;set;}=string.Empty;public int Type{get;set;}public int SourceKind{get;set;}public string SourceId{get;set;}=string.Empty;public string SourceCityId{get;set;}=string.Empty;public string SourceBuildingId{get;set;}=string.Empty;public string RecipientActorId{get;set;}=string.Empty;public int Quality{get;set;}public int DetailLevel{get;set;}public long ObservedAt{get;set;}public long? DispatchedAt{get;set;}public long? ArrivedAt{get;set;}public int Status{get;set;}public List<ReportObservationSaveData> Observations{get;set;}=new List<ReportObservationSaveData>();}
    public sealed class ActorInformationSaveData{public string ActorId{get;set;}=string.Empty;public List<string> AvailableReportIds{get;set;}=new List<string>();}
    public sealed class DiplomaticAgreementSaveData{public string AgreementId{get;set;}=string.Empty;public string FirstActorId{get;set;}=string.Empty;public string SecondActorId{get;set;}=string.Empty;public int Kind{get;set;}public long SignedAt{get;set;}public long EffectiveAt{get;set;}public long? ExpiresAt{get;set;}public int Status{get;set;}public List<int> Terms{get;set;}=new List<int>();}
    public sealed class ArmySaveData{public string ArmyId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int OwnerKind{get;set;}public string OwnerId{get;set;}=string.Empty;public int ControllerKind{get;set;}public string ControllerId{get;set;}=string.Empty;public int LocationKind{get;set;}public string CityId{get;set;}=string.Empty;public long LocationX{get;set;}public long LocationY{get;set;}public string CommanderCharacterId{get;set;}=string.Empty;public string CommanderOrganizationId{get;set;}=string.Empty;public string CommanderAssignmentId{get;set;}=string.Empty;public int Lifecycle{get;set;}public int Morale{get;set;}public int Fatigue{get;set;}public int Discipline{get;set;}public List<UnitGroupSaveData> Units{get;set;}=new List<UnitGroupSaveData>();public List<CommandRelationshipSaveData> CommandRelationships{get;set;}=new List<CommandRelationshipSaveData>();public List<ArmySupplySaveData> Supply{get;set;}=new List<ArmySupplySaveData>();public List<SupplyRequirementSaveData> SupplyRequirements{get;set;}=new List<SupplyRequirementSaveData>();public List<PayrollObligationSaveData> PayrollObligations{get;set;}=new List<PayrollObligationSaveData>();public List<PayrollPaymentSaveData> PayrollPayments{get;set;}=new List<PayrollPaymentSaveData>();}
    public sealed class UnitGroupSaveData{public string UnitGroupId{get;set;}=string.Empty;public string RecruitmentSourceId{get;set;}=string.Empty;public string TroopDefinitionId{get;set;}=string.Empty;public long Headcount{get;set;}public string CommanderCharacterId{get;set;}=string.Empty;public int Morale{get;set;}public int Fatigue{get;set;}public int Discipline{get;set;}}
    public sealed class CommandRelationshipSaveData{public int ParentKind{get;set;}public string ParentId{get;set;}=string.Empty;public int ChildKind{get;set;}public string ChildId{get;set;}=string.Empty;}
    public sealed class ArmySupplySaveData{public string TradeGoodId{get;set;}=string.Empty;public long Quantity{get;set;}}

    public sealed class BattleSaveData{public string BattleId{get;set;}=string.Empty;public long StartedAt{get;set;}public int Lifecycle{get;set;}public long Step{get;set;}public ulong RngState{get;set;}public ulong RngDrawCount{get;set;}public bool IsReconciled{get;set;}public List<BattleSideSaveData>Sides{get;set;}=new List<BattleSideSaveData>();public List<BattleSectorSaveData>Sectors{get;set;}=new List<BattleSectorSaveData>();public List<BattleDeploymentSaveData>Deployments{get;set;}=new List<BattleDeploymentSaveData>();public List<BattleOrderSaveData>Orders{get;set;}=new List<BattleOrderSaveData>();public List<BattleEventSaveData>Events{get;set;}=new List<BattleEventSaveData>();public List<BattleAmmoSaveData>Ammunition{get;set;}=new List<BattleAmmoSaveData>();public BattleResultSaveData? Result{get;set;}}
    public sealed class BattleSideSaveData{public string SideId{get;set;}=string.Empty;public string CommanderCharacterId{get;set;}=string.Empty;public List<BattleParticipantSaveData>Participants{get;set;}=new List<BattleParticipantSaveData>();}
    public sealed class BattleParticipantSaveData{public string ArmyId{get;set;}=string.Empty;public string CommanderCharacterId{get;set;}=string.Empty;public List<BattleUnitSnapshotSaveData>Units{get;set;}=new List<BattleUnitSnapshotSaveData>();public List<ArmySupplySaveData>Supply{get;set;}=new List<ArmySupplySaveData>();}
    public sealed class BattleUnitSnapshotSaveData{public string ArmyId{get;set;}=string.Empty;public string UnitGroupId{get;set;}=string.Empty;public string CommanderCharacterId{get;set;}=string.Empty;public long CampaignHeadcount{get;set;}public int Morale{get;set;}public int Fatigue{get;set;}public int Discipline{get;set;}public List<BattleCombatantSaveData>Combatants{get;set;}=new List<BattleCombatantSaveData>();}
    public sealed class BattleCombatantSaveData{public string SoldierId{get;set;}=string.Empty;public string UnitGroupId{get;set;}=string.Empty;public string TroopDefinitionId{get;set;}=string.Empty;public string CombatRoleId{get;set;}=string.Empty;public bool IsMounted{get;set;}public List<string>EquipmentInstanceIds{get;set;}=new List<string>();}
    public sealed class BattleSectorSaveData{public string SectorId{get;set;}=string.Empty;public List<int>Terrain{get;set;}=new List<int>();public List<string>EligibleSideIds{get;set;}=new List<string>();public List<string>AdjacentSectorIds{get;set;}=new List<string>();}
    public sealed class BattleDeploymentSaveData{public string DeploymentGroupId{get;set;}=string.Empty;public string SideId{get;set;}=string.Empty;public string ArmyId{get;set;}=string.Empty;public string UnitGroupId{get;set;}=string.Empty;public string SectorId{get;set;}=string.Empty;public int Formation{get;set;}public bool IsReserve{get;set;}}
    public sealed class BattleOrderSaveData{public string BattleOrderId{get;set;}=string.Empty;public long Sequence{get;set;}public int Kind{get;set;}public string SideId{get;set;}=string.Empty;public string IssuerCharacterId{get;set;}=string.Empty;public string DeploymentGroupId{get;set;}=string.Empty;public string TargetSectorId{get;set;}=string.Empty;public string TargetGroupId{get;set;}=string.Empty;public int? Formation{get;set;}}
    public sealed class BattleEventSaveData{public string BattleEventId{get;set;}=string.Empty;public long Sequence{get;set;}public string TargetSoldierId{get;set;}=string.Empty;public int Outcome{get;set;}}
    public sealed class BattleAmmoSaveData{public string SoldierId{get;set;}=string.Empty;public string AmmoFamilyId{get;set;}=string.Empty;public long Quantity{get;set;}}
    public sealed class BattleResultSaveData{public int EndReason{get;set;}public long CompletedAt{get;set;}public long ElapsedSteps{get;set;}public string WinningSideId{get;set;}=string.Empty;public List<string>ParticipatingSideIds{get;set;}=new List<string>();public List<BattleUnitOutcomeSaveData>Units{get;set;}=new List<BattleUnitOutcomeSaveData>();public List<BattleSoldierOutcomeSaveData>Soldiers{get;set;}=new List<BattleSoldierOutcomeSaveData>();public List<BattleCharacterOutcomeSaveData>Characters{get;set;}=new List<BattleCharacterOutcomeSaveData>();public List<BattleAmmoOutcomeSaveData>Ammunition{get;set;}=new List<BattleAmmoOutcomeSaveData>();}
    public sealed class BattleUnitOutcomeSaveData{public string UnitGroupId{get;set;}=string.Empty;public long AggregateLosses{get;set;}public int Morale{get;set;}public int Fatigue{get;set;}public int Discipline{get;set;}}
    public sealed class BattleSoldierOutcomeSaveData{public string SoldierId{get;set;}=string.Empty;public int Outcome{get;set;}}
    public sealed class BattleCharacterOutcomeSaveData{public string CharacterId{get;set;}=string.Empty;public int Outcome{get;set;}public int? InjurySeverity{get;set;}public string CaptorCharacterId{get;set;}=string.Empty;public string CaptorArmyId{get;set;}=string.Empty;}
    public sealed class BattleAmmoOutcomeSaveData{public string SoldierId{get;set;}=string.Empty;public string AmmoFamilyId{get;set;}=string.Empty;public long RemainingQuantity{get;set;}}
    public sealed class EncounterEntityRefSaveData{public int Kind{get;set;}public string Id{get;set;}=string.Empty;}
    public sealed class EncounterResolutionSaveData{public string OutcomeId{get;set;}=string.Empty;public string SelectedChoiceId{get;set;}=string.Empty;public string PolicyId{get;set;}=string.Empty;public long ResolvedAt{get;set;}public ulong RngState{get;set;}public ulong RngDrawCount{get;set;}}
    public sealed class EncounterSaveData{public string EncounterId{get;set;}=string.Empty;public string DefinitionId{get;set;}=string.Empty;public int Family{get;set;}public int Type{get;set;}public long CreatedAt{get;set;}public int SourceKind{get;set;}public string SourceId{get;set;}=string.Empty;public List<EncounterEntityRefSaveData>Participants{get;set;}=new List<EncounterEntityRefSaveData>();public ulong RngState{get;set;}public ulong RngDrawCount{get;set;}public int Lifecycle{get;set;}public long? EngagedAt{get;set;}public string SelectedChoiceId{get;set;}=string.Empty;public EncounterResolutionSaveData? Resolution{get;set;}public string LinkedContractId{get;set;}=string.Empty;public string LinkedBattleId{get;set;}=string.Empty;public bool OutcomeApplied{get;set;}}
    public sealed class ContractTargetSaveData{public string SlotId{get;set;}=string.Empty;public int Kind{get;set;}public string TargetId{get;set;}=string.Empty;}
    public sealed class ContractEvidenceSaveData{public string EvidenceId{get;set;}=string.Empty;public int Kind{get;set;}public int TargetKind{get;set;}public string TargetId{get;set;}=string.Empty;public long OccurredAt{get;set;}}
    public sealed class ContractObjectiveSaveData{public string ObjectiveId{get;set;}=string.Empty;public int EvidenceKind{get;set;}public string TargetSlotId{get;set;}=string.Empty;public int RequiredEvidenceCount{get;set;}public List<ContractEvidenceSaveData>Evidence{get;set;}=new List<ContractEvidenceSaveData>();}
    public sealed class ContractSaveData{public string ContractId{get;set;}=string.Empty;public string DefinitionId{get;set;}=string.Empty;public int Category{get;set;}public int IssuerKind{get;set;}public string IssuerId{get;set;}=string.Empty;public List<ContractTargetSaveData>Targets{get;set;}=new List<ContractTargetSaveData>();public List<ContractObjectiveSaveData>Objectives{get;set;}=new List<ContractObjectiveSaveData>();public long OfferedAt{get;set;}public string AssigneeCharacterId{get;set;}=string.Empty;public long? AcceptedAt{get;set;}public long? Deadline{get;set;}public long? TerminalAt{get;set;}public string LinkedEncounterId{get;set;}=string.Empty;public string LinkedBattleId{get;set;}=string.Empty;public int Lifecycle{get;set;}public bool OutcomeApplied{get;set;}}
    public sealed class SupplyRequirementSaveData{public string TradeGoodId{get;set;}=string.Empty;public int Purpose{get;set;}public long RequiredQuantity{get;set;}}
    public sealed class PayrollObligationSaveData{public string PayrollObligationId{get;set;}=string.Empty;public long AmountOwed{get;set;}public long AmountPaid{get;set;}public long DueAt{get;set;}public int FundingSourceKind{get;set;}public string FundingSourceId{get;set;}=string.Empty;}
    public sealed class PayrollPaymentSaveData{public string PayrollPaymentId{get;set;}=string.Empty;public string PayrollObligationId{get;set;}=string.Empty;public long Amount{get;set;}public long PaidAt{get;set;}public int FundingSourceKind{get;set;}public string FundingSourceId{get;set;}=string.Empty;}
    public sealed class RecruitmentSourceSaveData{public string RecruitmentSourceId{get;set;}=string.Empty;public int Type{get;set;}public long AvailableHeadcount{get;set;}public string AuthorityCharacterId{get;set;}=string.Empty;public string AuthorityOrganizationId{get;set;}=string.Empty;public string AuthorityAssignmentId{get;set;}=string.Empty;public string CityId{get;set;}=string.Empty;public string InstitutionId{get;set;}=string.Empty;public string ObligationOrContractId{get;set;}=string.Empty;public bool IsActive{get;set;}}
    public sealed class RecruitmentRecordSaveData{public string RecruitmentRecordId{get;set;}=string.Empty;public string RecruitmentSourceId{get;set;}=string.Empty;public string ArmyId{get;set;}=string.Empty;public string UnitGroupId{get;set;}=string.Empty;public long Headcount{get;set;}public long OccurredAt{get;set;}}
    public sealed class TroopDefinitionSaveData{public string TroopDefinitionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public string UnitClassId{get;set;}=string.Empty;public string DefaultCombatRoleId{get;set;}=string.Empty;public int MountContext{get;set;}public string VisualProfileId{get;set;}=string.Empty;public List<int> AllowedWeaponFamilies{get;set;}=new List<int>();public bool AllowsArmor{get;set;}public bool AllowsShield{get;set;}}
    public sealed class WeaponAttackOptionSaveData{public int Mode{get;set;}public int DamageType{get;set;}}
    public sealed class WeaponDefinitionSaveData{public string WeaponDefinitionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int Family{get;set;}public List<int> AllowedSlots{get;set;}=new List<int>();public List<WeaponAttackOptionSaveData> AttackOptions{get;set;}=new List<WeaponAttackOptionSaveData>();public int MountContext{get;set;}public string EconomicGoodId{get;set;}=string.Empty;public string VisualProfileId{get;set;}=string.Empty;public bool IsRanged{get;set;}public string AmmoFamilyId{get;set;}=string.Empty;}
    public sealed class ArmorDefinitionSaveData{public string ArmorDefinitionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int Slot{get;set;}public string EconomicGoodId{get;set;}=string.Empty;public string VisualProfileId{get;set;}=string.Empty;public string QualityCode{get;set;}=string.Empty;}
    public sealed class ShieldDefinitionSaveData{public string ShieldDefinitionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public string EconomicGoodId{get;set;}=string.Empty;public string VisualProfileId{get;set;}=string.Empty;public string Classification{get;set;}=string.Empty;}
    public sealed class MountDefinitionSaveData{public string MountDefinitionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public string EconomicGoodId{get;set;}=string.Empty;public string VisualProfileId{get;set;}=string.Empty;public string MobilityClass{get;set;}=string.Empty;}
    public sealed class AuxiliaryEquipmentDefinitionSaveData{public string AuxiliaryEquipmentDefinitionId{get;set;}=string.Empty;public string Name{get;set;}=string.Empty;public int Kind{get;set;}public string EconomicGoodId{get;set;}=string.Empty;public string VisualProfileId{get;set;}=string.Empty;public string AmmoFamilyId{get;set;}=string.Empty;}
    public sealed class EquipmentInstanceSaveData{public string EquipmentInstanceId{get;set;}=string.Empty;public int DefinitionKind{get;set;}public string DefinitionId{get;set;}=string.Empty;public int OwnerKind{get;set;}public string OwnerId{get;set;}=string.Empty;public int AcquisitionKind{get;set;}public string AcquisitionSourceId{get;set;}=string.Empty;public string EconomicGoodId{get;set;}=string.Empty;public long AcquiredAt{get;set;}public string QualityCode{get;set;}=string.Empty;}
    public sealed class WeaponSlotAssignmentSaveData{public int Slot{get;set;}public string EquipmentInstanceId{get;set;}=string.Empty;}
    public sealed class ArmorSlotAssignmentSaveData{public int Slot{get;set;}public string EquipmentInstanceId{get;set;}=string.Empty;}
    public sealed class SoldierSaveData{public string SoldierId{get;set;}=string.Empty;public string UnitGroupId{get;set;}=string.Empty;public string TroopDefinitionId{get;set;}=string.Empty;public string RecruitmentSourceId{get;set;}=string.Empty;public string RecruitmentRecordId{get;set;}=string.Empty;public string CombatRoleId{get;set;}=string.Empty;public int Experience{get;set;}public int Training{get;set;}public int Lifecycle{get;set;}public List<WeaponSlotAssignmentSaveData> Weapons{get;set;}=new List<WeaponSlotAssignmentSaveData>();public List<ArmorSlotAssignmentSaveData> Armor{get;set;}=new List<ArmorSlotAssignmentSaveData>();public string ShieldEquipmentId{get;set;}=string.Empty;public string MountEquipmentId{get;set;}=string.Empty;}
}
