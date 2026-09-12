using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Common;

namespace FOC.Domain.Cities
{
    public readonly struct CityBuildingId : IStableId, IEquatable<CityBuildingId>, IComparable<CityBuildingId>
    {
        private readonly StableId<CityBuildingTag> _value; private CityBuildingId(StableId<CityBuildingTag> value){_value=value;}
        public bool IsValid=>_value.IsValid; public string Value=>_value.Value; public static CityBuildingId Create(string value)=>new CityBuildingId(StableId<CityBuildingTag>.Create(value));
        public int CompareTo(CityBuildingId other)=>_value.CompareTo(other._value); public bool Equals(CityBuildingId other)=>_value.Equals(other._value); public override bool Equals(object? obj)=>obj is CityBuildingId other&&Equals(other); public override int GetHashCode()=>_value.GetHashCode(); public override string ToString()=>_value.ToString();
    }

    public enum CityAreaType { InnerCastle, Trade, InnCaravan, Housing, Military, Health, ProductionCraft, FoodSupply, SquareCulture }
    public enum CityAreaFullness { Empty, Low, Half, Full }
    public enum CityBuildingContentStatus { Active, Removed }
    public enum CityBuildingKind
    {
        InnerCastleWalls, Palace, CourtKadiOffice, Mosque, Medrese, ImperialRegistry, ChiefScribeOffice, ImperialGuardHeadquarters,
        Market, Bedesten, GuildHall, CustomsOffice, Inn, MerchantInn, Caravanserai,
        PoorHousing, LowerMiddleHousing, UpperMiddleHousing, EliteMerchantMansions,
        Barracks, TrainingGround, Arsenal, CavalryStables, GuardFacilities, CultureSpecificMilitaryTraining,
        Hospital, GreatBath, Pharmacy, Tannery, Blacksmith, Carpenter, TextileWorkshop, Dyehouse, Soapworks, PaperMill, Candlemaker, ConstructionStoneWorkshop, Farrier,
        Bakery, Mill, Granary, Butcher, Fishery, HorseSquare, FairGround, MonumentalPublicStructure
    }
    public enum CityInstitutionEffectTag { Order, PublicTrust, TraderAttraction, PopulationAttraction, OfficialQuality, AdministrativeSupport, ReportQuality, RecordOrder, SmugglingDetection, RecommendationQuality, DiplomacyOptions, DiplomacySuccess, ExternalInformation, Peace, PalaceSecurity, CitySecurity, TaxLossReduction }
    public enum CityInfrastructureType { Well, Fountain, Cistern, WaterChannel, MainPavedRoad, SecondaryRoad, BridgeCrossing, DrainageLine, WasteSewageChannel }
    public enum CityInfrastructureCondition { Unassessed, Serviceable, Degraded, Failed }
    public enum CityMetricKind { Wealth, Order, Health, Security }
    public enum CityMetricAssessment { Unassessed }
    public enum CityOfficialRole { Kethuda }

    public sealed class CityBuildingDefinition
    {
        private readonly List<CityInstitutionEffectTag> _effectTags=new List<CityInstitutionEffectTag>();
        public CityBuildingDefinition(CityBuildingId id,string name,CityBuildingKind kind,CityAreaType areaType,CityBuildingContentStatus status=CityBuildingContentStatus.Active,IEnumerable<CityInstitutionEffectTag>? effectTags=null)
        {
            if(!id.IsValid)throw new ArgumentException("CityBuildingId is invalid.",nameof(id));if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Name is required.",nameof(name));RequireEnum(kind,nameof(kind));RequireEnum(areaType,nameof(areaType));RequireEnum(status,nameof(status));if(CityBuildingRules.RequiredArea(kind)!=areaType)throw new InvalidOperationException("Building kind is not compatible with City area.");Id=id;Name=name;Kind=kind;AreaType=areaType;Status=status;
            if(effectTags!=null){var seen=new HashSet<CityInstitutionEffectTag>();foreach(var tag in effectTags){RequireEnum(tag,nameof(effectTags));if(!seen.Add(tag))throw new InvalidOperationException("Duplicate City institution effect tag.");_effectTags.Add(tag);}_effectTags.Sort();}
        }
        public CityBuildingId Id{get;} public string Name{get;} public CityBuildingKind Kind{get;} public CityAreaType AreaType{get;} public CityBuildingContentStatus Status{get;} public IReadOnlyList<CityInstitutionEffectTag> EffectTags=>_effectTags;
        private static void RequireEnum<T>(T value,string name)where T:struct{if(!Enum.IsDefined(typeof(T),value))throw new ArgumentOutOfRangeException(name);}
    }

    public static class CityBuildingRules
    {
        public static CityAreaType RequiredArea(CityBuildingKind kind)
        {
            if(!Enum.IsDefined(typeof(CityBuildingKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            switch(kind)
            {
                case CityBuildingKind.InnerCastleWalls:case CityBuildingKind.Palace:case CityBuildingKind.CourtKadiOffice:case CityBuildingKind.Mosque:case CityBuildingKind.Medrese:case CityBuildingKind.ImperialRegistry:case CityBuildingKind.ChiefScribeOffice:case CityBuildingKind.ImperialGuardHeadquarters:return CityAreaType.InnerCastle;
                case CityBuildingKind.Market:case CityBuildingKind.Bedesten:case CityBuildingKind.GuildHall:case CityBuildingKind.CustomsOffice:return CityAreaType.Trade;
                case CityBuildingKind.Inn:case CityBuildingKind.MerchantInn:case CityBuildingKind.Caravanserai:return CityAreaType.InnCaravan;
                case CityBuildingKind.PoorHousing:case CityBuildingKind.LowerMiddleHousing:case CityBuildingKind.UpperMiddleHousing:case CityBuildingKind.EliteMerchantMansions:return CityAreaType.Housing;
                case CityBuildingKind.Barracks:case CityBuildingKind.TrainingGround:case CityBuildingKind.Arsenal:case CityBuildingKind.CavalryStables:case CityBuildingKind.GuardFacilities:case CityBuildingKind.CultureSpecificMilitaryTraining:return CityAreaType.Military;
                case CityBuildingKind.Hospital:case CityBuildingKind.GreatBath:case CityBuildingKind.Pharmacy:return CityAreaType.Health;
                case CityBuildingKind.Tannery:case CityBuildingKind.Blacksmith:case CityBuildingKind.Carpenter:case CityBuildingKind.TextileWorkshop:case CityBuildingKind.Dyehouse:case CityBuildingKind.Soapworks:case CityBuildingKind.PaperMill:case CityBuildingKind.Candlemaker:case CityBuildingKind.ConstructionStoneWorkshop:case CityBuildingKind.Farrier:return CityAreaType.ProductionCraft;
                case CityBuildingKind.Bakery:case CityBuildingKind.Mill:case CityBuildingKind.Granary:case CityBuildingKind.Butcher:case CityBuildingKind.Fishery:return CityAreaType.FoodSupply;
                case CityBuildingKind.HorseSquare:case CityBuildingKind.FairGround:case CityBuildingKind.MonumentalPublicStructure:return CityAreaType.SquareCulture;
                default:throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    public sealed class CityAreaDefinition
    {
        private readonly SortedDictionary<CityBuildingId,CityBuildingDefinition> _pool=new SortedDictionary<CityBuildingId,CityBuildingDefinition>();
        public CityAreaDefinition(CityAreaType type,IEnumerable<CityBuildingDefinition> buildingPool,string visualVariantHook="")
        {
            if(!Enum.IsDefined(typeof(CityAreaType),type))throw new ArgumentOutOfRangeException(nameof(type));Type=type;VisualVariantHook=visualVariantHook??string.Empty;if(buildingPool==null)throw new ArgumentNullException(nameof(buildingPool));foreach(var item in buildingPool){if(item==null||item.AreaType!=type)throw new InvalidOperationException("Building pool contains an incompatible definition.");if(_pool.ContainsKey(item.Id))throw new InvalidOperationException("Duplicate building in City area pool.");_pool.Add(item.Id,item);}
        }
        public CityAreaType Type{get;} public string VisualVariantHook{get;} public IReadOnlyCollection<CityBuildingDefinition> OrderedBuildingPool=>_pool.Values;
        public CityBuildingDefinition GetRequired(CityBuildingId id){if(!_pool.TryGetValue(id,out var item))throw new KeyNotFoundException("Building is outside the City area pool.");return item;}
    }

    public sealed class CityDefinition
    {
        private readonly SortedDictionary<CityAreaType,CityAreaDefinition> _areas=new SortedDictionary<CityAreaType,CityAreaDefinition>();
        public CityDefinition(CityId id,string name,IEnumerable<CityAreaDefinition> areas){if(!id.IsValid)throw new ArgumentException("CityId is invalid.",nameof(id));if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Name is required.",nameof(name));if(areas==null)throw new ArgumentNullException(nameof(areas));Id=id;Name=name;var buildingIds=new HashSet<CityBuildingId>();foreach(var area in areas){if(area==null||_areas.ContainsKey(area.Type))throw new InvalidOperationException("Duplicate City area type.");foreach(var building in area.OrderedBuildingPool)if(!buildingIds.Add(building.Id))throw new InvalidOperationException("City building IDs must be unique across all area pools.");_areas.Add(area.Type,area);}foreach(CityAreaType type in Enum.GetValues(typeof(CityAreaType)))if(!_areas.ContainsKey(type))throw new InvalidOperationException("Every authoritative City area is required.");}
        public CityId Id{get;} public string Name{get;} public IReadOnlyCollection<CityAreaDefinition> OrderedAreas=>_areas.Values; public CityAreaDefinition GetRequired(CityAreaType type){if(!_areas.TryGetValue(type,out var item))throw new KeyNotFoundException("City area was not found.");return item;}
    }

    public sealed class CityAreaState
    {
        private readonly SortedDictionary<CityBuildingId,CityBuildingDefinition> _active=new SortedDictionary<CityBuildingId,CityBuildingDefinition>();private readonly SortedSet<CityBuildingId> _locked=new SortedSet<CityBuildingId>();
        public CityAreaState(CityAreaDefinition definition,CityAreaFullness fullness){Definition=definition??throw new ArgumentNullException(nameof(definition));SetFullness(fullness);}
        public CityAreaDefinition Definition{get;} public CityAreaType Type=>Definition.Type; public CityAreaFullness Fullness{get;private set;} public IReadOnlyCollection<CityBuildingDefinition> OrderedActiveBuildings=>_active.Values; public IReadOnlyCollection<CityBuildingId> OrderedLockedBuildingIds=>_locked;
        public void SetFullness(CityAreaFullness fullness){if(!Enum.IsDefined(typeof(CityAreaFullness),fullness))throw new ArgumentOutOfRangeException(nameof(fullness));if(Type==CityAreaType.InnerCastle&&fullness!=CityAreaFullness.Full)throw new InvalidOperationException("Inner Castle is always Full.");if(fullness==CityAreaFullness.Empty&&_active.Count>0)throw new InvalidOperationException("An area with active buildings cannot become Empty.");Fullness=fullness;}
        public void ActivateBuilding(CityBuildingId id){var item=Definition.GetRequired(id);if(Fullness==CityAreaFullness.Empty)throw new InvalidOperationException("An Empty area cannot activate a building.");if(item.Status==CityBuildingContentStatus.Removed)throw new InvalidOperationException("A removed building cannot be active.");if(_locked.Contains(id))throw new InvalidOperationException("A locked building cannot be active.");if(_active.ContainsKey(id))throw new InvalidOperationException("Duplicate active building.");_active.Add(id,item);}
        public void LockBuilding(CityBuildingId id){Definition.GetRequired(id);if(_active.ContainsKey(id)||!_locked.Add(id))throw new InvalidOperationException("Building cannot be locked in its current state.");}
    }

    public sealed class CityInfrastructureState
    {
        public CityInfrastructureState(CityInfrastructureType type,bool installed,CityInfrastructureCondition condition=CityInfrastructureCondition.Unassessed){if(!Enum.IsDefined(typeof(CityInfrastructureType),type))throw new ArgumentOutOfRangeException(nameof(type));if(!Enum.IsDefined(typeof(CityInfrastructureCondition),condition))throw new ArgumentOutOfRangeException(nameof(condition));if(!installed&&condition!=CityInfrastructureCondition.Unassessed)throw new InvalidOperationException("Uninstalled infrastructure cannot carry a condition.");Type=type;Installed=installed;Condition=condition;}
        public CityInfrastructureType Type{get;} public bool Installed{get;} public CityInfrastructureCondition Condition{get;}
    }
    public sealed class CityMetricsState
    {
        public CityMetricsState(long populationCount,CityMetricAssessment wealth=CityMetricAssessment.Unassessed,CityMetricAssessment order=CityMetricAssessment.Unassessed,CityMetricAssessment health=CityMetricAssessment.Unassessed,CityMetricAssessment security=CityMetricAssessment.Unassessed){if(populationCount<0)throw new ArgumentOutOfRangeException(nameof(populationCount));PopulationCount=populationCount;Wealth=Require(wealth);Order=Require(order);Health=Require(health);Security=Require(security);}
        public long PopulationCount{get;} public CityMetricAssessment Wealth{get;} public CityMetricAssessment Order{get;} public CityMetricAssessment Health{get;} public CityMetricAssessment Security{get;} private static CityMetricAssessment Require(CityMetricAssessment value){if(!Enum.IsDefined(typeof(CityMetricAssessment),value))throw new ArgumentOutOfRangeException(nameof(value));return value;}
    }
    public static class CityOfficialRoles { public const string KethudaAssignmentRoleCode="city-kethuda"; }
    public sealed class CityOfficialReference
    {
        public CityOfficialReference(CityOfficialRole role,OrganizationId organizationId,AssignmentId assignmentId){if(!Enum.IsDefined(typeof(CityOfficialRole),role))throw new ArgumentOutOfRangeException(nameof(role));if(!organizationId.IsValid)throw new ArgumentException("OrganizationId is invalid.",nameof(organizationId));if(!assignmentId.IsValid)throw new ArgumentException("AssignmentId is invalid.",nameof(assignmentId));Role=role;OrganizationId=organizationId;AssignmentId=assignmentId;}
        public CityOfficialRole Role{get;} public OrganizationId OrganizationId{get;} public AssignmentId AssignmentId{get;}
    }

    public sealed class CityState
    {
        private readonly SortedDictionary<CityAreaType,CityAreaState> _areas=new SortedDictionary<CityAreaType,CityAreaState>();private readonly SortedDictionary<CityInfrastructureType,CityInfrastructureState> _infrastructure=new SortedDictionary<CityInfrastructureType,CityInfrastructureState>();private readonly SortedDictionary<AssignmentId,CityOfficialReference> _officials=new SortedDictionary<AssignmentId,CityOfficialReference>();
        public CityState(CityDefinition definition,CityMetricsState metrics){Definition=definition??throw new ArgumentNullException(nameof(definition));Metrics=metrics??throw new ArgumentNullException(nameof(metrics));foreach(var area in definition.OrderedAreas)_areas.Add(area.Type,new CityAreaState(area,area.Type==CityAreaType.InnerCastle?CityAreaFullness.Full:CityAreaFullness.Empty));}
        public CityDefinition Definition{get;} public CityId Id=>Definition.Id; public CityMetricsState Metrics{get;} public IReadOnlyCollection<CityAreaState> OrderedAreas=>_areas.Values; public IReadOnlyCollection<CityInfrastructureState> OrderedInfrastructure=>_infrastructure.Values; public IReadOnlyCollection<CityOfficialReference> OrderedOfficials=>_officials.Values;
        public CityAreaState GetRequiredArea(CityAreaType type){if(!_areas.TryGetValue(type,out var item))throw new KeyNotFoundException("City area was not found.");return item;} public void SetInfrastructure(CityInfrastructureState state){if(state==null)throw new ArgumentNullException(nameof(state));_infrastructure[state.Type]=state;} public void AddOfficial(CityOfficialReference official){if(official==null)throw new ArgumentNullException(nameof(official));if(_officials.ContainsKey(official.AssignmentId))throw new InvalidOperationException("Duplicate City official assignment reference.");_officials.Add(official.AssignmentId,official);}
    }
    public sealed class CityRegistry
    {
        private readonly SortedDictionary<CityId,CityState> _items=new SortedDictionary<CityId,CityState>();public int Count=>_items.Count;public IReadOnlyCollection<CityState> OrderedCities=>_items.Values;public void Add(CityState city){if(city==null)throw new ArgumentNullException(nameof(city));if(_items.ContainsKey(city.Id))throw new InvalidOperationException("Duplicate CityId.");_items.Add(city.Id,city);}public CityState GetRequired(CityId id){if(!_items.TryGetValue(id,out var item))throw new KeyNotFoundException("City was not found.");return item;}
    }
}
