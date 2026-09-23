using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Time;

namespace FOC.Domain.Soldiers
{
    public enum EquipmentOwnerKind { Soldier, Character, ArmyInventory, Institution, Caravan }
    public enum EquipmentAcquisitionKind { CityMarket, RecruitmentProvided }
    public enum SoldierLifecycle { Active, Unavailable, Retired, Wounded, Killed, Captured }
    public enum SoldierExperienceAssessment { Unassessed, Inexperienced, Experienced, Veteran }
    public enum SoldierTrainingAssessment { Unassessed, Untrained, Trained, Drilled }

    public readonly struct EquipmentDefinitionRef:IEquatable<EquipmentDefinitionRef>,IComparable<EquipmentDefinitionRef>
    {
        private EquipmentDefinitionRef(EquipmentKind kind,string id){Kind=kind;Id=id;}public EquipmentKind Kind{get;}public string Id{get;}
        public static EquipmentDefinitionRef Weapon(WeaponDefinitionId id){Require(id.IsValid);return new EquipmentDefinitionRef(EquipmentKind.Weapon,id.Value);}public static EquipmentDefinitionRef Armor(ArmorDefinitionId id){Require(id.IsValid);return new EquipmentDefinitionRef(EquipmentKind.Armor,id.Value);}public static EquipmentDefinitionRef Shield(ShieldDefinitionId id){Require(id.IsValid);return new EquipmentDefinitionRef(EquipmentKind.Shield,id.Value);}public static EquipmentDefinitionRef Mount(MountDefinitionId id){Require(id.IsValid);return new EquipmentDefinitionRef(EquipmentKind.Mount,id.Value);}public static EquipmentDefinitionRef Auxiliary(AuxiliaryEquipmentDefinitionId id){Require(id.IsValid);return new EquipmentDefinitionRef(EquipmentKind.Auxiliary,id.Value);}
        public int CompareTo(EquipmentDefinitionRef other){var c=Kind.CompareTo(other.Kind);return c!=0?c:StringComparer.Ordinal.Compare(Id,other.Id);}public bool Equals(EquipmentDefinitionRef other)=>Kind==other.Kind&&StringComparer.Ordinal.Equals(Id,other.Id);public override bool Equals(object? obj)=>obj is EquipmentDefinitionRef other&&Equals(other);public override int GetHashCode()=>((int)Kind*397)^StringComparer.Ordinal.GetHashCode(Id??string.Empty);private static void Require(bool valid){if(!valid)throw new ArgumentException("Equipment definition ID is invalid.");}
    }

    public readonly struct EquipmentOwnerRef:IEquatable<EquipmentOwnerRef>
    {
        private EquipmentOwnerRef(EquipmentOwnerKind kind,string id){Kind=kind;Id=id;}public EquipmentOwnerKind Kind{get;}public string Id{get;}
        public static EquipmentOwnerRef Soldier(SoldierId id){Require(id.IsValid);return new EquipmentOwnerRef(EquipmentOwnerKind.Soldier,id.Value);}public static EquipmentOwnerRef Character(CharacterId id){Require(id.IsValid);return new EquipmentOwnerRef(EquipmentOwnerKind.Character,id.Value);}public static EquipmentOwnerRef ArmyInventory(ArmyId id){Require(id.IsValid);return new EquipmentOwnerRef(EquipmentOwnerKind.ArmyInventory,id.Value);}public static EquipmentOwnerRef Institution(CityBuildingId id){Require(id.IsValid);return new EquipmentOwnerRef(EquipmentOwnerKind.Institution,id.Value);}public static EquipmentOwnerRef Caravan(CaravanId id){Require(id.IsValid);return new EquipmentOwnerRef(EquipmentOwnerKind.Caravan,id.Value);}
        public bool Equals(EquipmentOwnerRef other)=>Kind==other.Kind&&StringComparer.Ordinal.Equals(Id,other.Id);public override bool Equals(object? obj)=>obj is EquipmentOwnerRef other&&Equals(other);public override int GetHashCode()=>((int)Kind*397)^StringComparer.Ordinal.GetHashCode(Id??string.Empty);private static void Require(bool valid){if(!valid)throw new ArgumentException("Equipment owner ID is invalid.");}
    }

    public sealed class EquipmentAcquisitionProvenance
    {
        private EquipmentAcquisitionProvenance(EquipmentAcquisitionKind kind,string sourceId,TradeGoodId economicGoodId,WorldTimestamp acquiredAt){if(string.IsNullOrWhiteSpace(sourceId)||!economicGoodId.IsValid)throw new ArgumentException("Equipment acquisition provenance is invalid.");Kind=kind;SourceId=sourceId;EconomicGoodId=economicGoodId;AcquiredAt=acquiredAt;}
        public EquipmentAcquisitionKind Kind{get;}public string SourceId{get;}public TradeGoodId EconomicGoodId{get;}public WorldTimestamp AcquiredAt{get;}
        public static EquipmentAcquisitionProvenance CityMarket(CityId cityId,TradeGoodId goodId,WorldTimestamp at){if(!cityId.IsValid)throw new ArgumentException(nameof(cityId));return new EquipmentAcquisitionProvenance(EquipmentAcquisitionKind.CityMarket,cityId.Value,goodId,at);}public static EquipmentAcquisitionProvenance RecruitmentProvided(RecruitmentRecordId recordId,TradeGoodId goodId,WorldTimestamp at){if(!recordId.IsValid)throw new ArgumentException(nameof(recordId));return new EquipmentAcquisitionProvenance(EquipmentAcquisitionKind.RecruitmentProvided,recordId.Value,goodId,at);}
    }

    public sealed class EquipmentInstance
    {
        public EquipmentInstance(EquipmentInstanceId id,EquipmentDefinitionRef definition,EquipmentOwnerRef owner,EquipmentAcquisitionProvenance provenance,string qualityCode="unspecified")
        {if(!id.IsValid||string.IsNullOrWhiteSpace(definition.Id)||string.IsNullOrWhiteSpace(owner.Id)||provenance==null||string.IsNullOrWhiteSpace(qualityCode))throw new ArgumentException("Equipment instance is invalid.");Id=id;Definition=definition;Owner=owner;Provenance=provenance;QualityCode=qualityCode;}
        public EquipmentInstanceId Id{get;}public EquipmentDefinitionRef Definition{get;}public EquipmentOwnerRef Owner{get;private set;}public EquipmentAcquisitionProvenance Provenance{get;}public string QualityCode{get;private set;}
        internal void TransferTo(EquipmentOwnerRef owner){if(string.IsNullOrWhiteSpace(owner.Id))throw new ArgumentException(nameof(owner));Owner=owner;}internal void SetQuality(string qualityCode){if(string.IsNullOrWhiteSpace(qualityCode))throw new ArgumentException(nameof(qualityCode));QualityCode=qualityCode;}
    }

    public sealed class WeaponSlotAssignment{public WeaponSlotAssignment(WeaponSlot slot,EquipmentInstanceId equipmentId){if(!Enum.IsDefined(typeof(WeaponSlot),slot)||!equipmentId.IsValid)throw new ArgumentException("Weapon slot assignment is invalid.");Slot=slot;EquipmentId=equipmentId;}public WeaponSlot Slot{get;}public EquipmentInstanceId EquipmentId{get;}}
    public sealed class ArmorSlotAssignment{public ArmorSlotAssignment(ArmorSlot slot,EquipmentInstanceId equipmentId){if(!Enum.IsDefined(typeof(ArmorSlot),slot)||!equipmentId.IsValid)throw new ArgumentException("Armor slot assignment is invalid.");Slot=slot;EquipmentId=equipmentId;}public ArmorSlot Slot{get;}public EquipmentInstanceId EquipmentId{get;}}

    public sealed class SoldierLoadout
    {
        private readonly List<WeaponSlotAssignment> _weapons;private readonly List<ArmorSlotAssignment> _armor;
        public SoldierLoadout(IEnumerable<WeaponSlotAssignment>? weapons=null,IEnumerable<ArmorSlotAssignment>? armor=null,EquipmentInstanceId? shield=null,EquipmentInstanceId? mount=null)
        {_weapons=(weapons??Array.Empty<WeaponSlotAssignment>()).ToList();_armor=(armor??Array.Empty<ArmorSlotAssignment>()).ToList();if(_weapons.Any(x=>x==null)||_armor.Any(x=>x==null)||_weapons.Select(x=>x.Slot).Distinct().Count()!=_weapons.Count||_armor.Select(x=>x.Slot).Distinct().Count()!=_armor.Count)throw new InvalidOperationException("Loadout slots must be unique.");_weapons=_weapons.OrderBy(x=>x.Slot).ToList();_armor=_armor.OrderBy(x=>x.Slot).ToList();Shield=shield;Mount=mount;var ids=_weapons.Select(x=>x.EquipmentId).Concat(_armor.Select(x=>x.EquipmentId));if(shield.HasValue)ids=ids.Concat(new[]{shield.Value});if(mount.HasValue)ids=ids.Concat(new[]{mount.Value});if(ids.Distinct().Count()!=ids.Count())throw new InvalidOperationException("One equipment instance cannot occupy multiple slots.");}
        public IReadOnlyList<WeaponSlotAssignment> OrderedWeapons=>_weapons;public IReadOnlyList<ArmorSlotAssignment> OrderedArmor=>_armor;public EquipmentInstanceId? Shield{get;}public EquipmentInstanceId? Mount{get;}
        public EquipmentInstanceId? WeaponAt(WeaponSlot slot){var x=_weapons.FirstOrDefault(v=>v.Slot==slot);return x==null?(EquipmentInstanceId?)null:x.EquipmentId;}public EquipmentInstanceId? ArmorAt(ArmorSlot slot){var x=_armor.FirstOrDefault(v=>v.Slot==slot);return x==null?(EquipmentInstanceId?)null:x.EquipmentId;}
    }

    public sealed class SoldierRecruitmentProvenance
    {
        public SoldierRecruitmentProvenance(RecruitmentSourceId sourceId,RecruitmentRecordId recordId){if(!sourceId.IsValid||!recordId.IsValid)throw new ArgumentException("Recruitment provenance is invalid.");SourceId=sourceId;RecordId=recordId;}public RecruitmentSourceId SourceId{get;}public RecruitmentRecordId RecordId{get;}
    }

    public sealed class SoldierInstance
    {
        public SoldierInstance(SoldierId id,UnitGroupId unitGroupId,TroopDefinitionId troopDefinitionId,SoldierRecruitmentProvenance recruitment,SoldierLoadout loadout,CombatRoleId combatRoleId,SoldierExperienceAssessment experience=SoldierExperienceAssessment.Unassessed,SoldierTrainingAssessment training=SoldierTrainingAssessment.Unassessed,SoldierLifecycle lifecycle=SoldierLifecycle.Active)
        {if(!id.IsValid||!unitGroupId.IsValid||!troopDefinitionId.IsValid||!combatRoleId.IsValid||recruitment==null||loadout==null)throw new ArgumentException("Soldier references are invalid.");if(!Enum.IsDefined(typeof(SoldierExperienceAssessment),experience)||!Enum.IsDefined(typeof(SoldierTrainingAssessment),training)||!Enum.IsDefined(typeof(SoldierLifecycle),lifecycle))throw new ArgumentOutOfRangeException(nameof(experience));Id=id;UnitGroupId=unitGroupId;TroopDefinitionId=troopDefinitionId;Recruitment=recruitment;Loadout=loadout;CombatRoleId=combatRoleId;Experience=experience;Training=training;Lifecycle=lifecycle;}
        public SoldierId Id{get;}public UnitGroupId UnitGroupId{get;}public TroopDefinitionId TroopDefinitionId{get;}public SoldierRecruitmentProvenance Recruitment{get;}public SoldierLoadout Loadout{get;private set;}public CombatRoleId CombatRoleId{get;private set;}public SoldierExperienceAssessment Experience{get;private set;}public SoldierTrainingAssessment Training{get;private set;}public SoldierLifecycle Lifecycle{get;private set;}
        internal void ReplaceLoadout(SoldierLoadout loadout){Loadout=loadout??throw new ArgumentNullException(nameof(loadout));}public void ApplyBattleOutcome(SoldierLifecycle outcome){if(Lifecycle!=SoldierLifecycle.Active)throw new InvalidOperationException("Only an active Soldier can receive a battle outcome.");if(outcome!=SoldierLifecycle.Wounded&&outcome!=SoldierLifecycle.Killed&&outcome!=SoldierLifecycle.Captured)throw new ArgumentException("Battle outcome lifecycle is invalid.",nameof(outcome));Lifecycle=outcome;}
    }

    public sealed class EquipmentInstanceRegistry
    {
        private readonly SortedDictionary<EquipmentInstanceId,EquipmentInstance> _items=new SortedDictionary<EquipmentInstanceId,EquipmentInstance>();public IReadOnlyCollection<EquipmentInstance> OrderedEquipment=>_items.Values;public bool Contains(EquipmentInstanceId id)=>_items.ContainsKey(id);public EquipmentInstance GetRequired(EquipmentInstanceId id){if(!_items.TryGetValue(id,out var x))throw new KeyNotFoundException("Equipment instance was not found.");return x;}internal void Add(EquipmentInstance x){if(x==null)throw new ArgumentNullException(nameof(x));if(_items.ContainsKey(x.Id))throw new InvalidOperationException("Duplicate EquipmentInstanceId.");_items.Add(x.Id,x);}
    }
    public sealed class SoldierRegistry
    {
        private readonly SortedDictionary<SoldierId,SoldierInstance> _items=new SortedDictionary<SoldierId,SoldierInstance>();public IReadOnlyCollection<SoldierInstance> OrderedSoldiers=>_items.Values;public bool Contains(SoldierId id)=>_items.ContainsKey(id);public SoldierInstance GetRequired(SoldierId id){if(!_items.TryGetValue(id,out var x))throw new KeyNotFoundException("Soldier was not found.");return x;}public long CountFor(UnitGroupId id)=>_items.Values.LongCount(x=>x.UnitGroupId.Equals(id)&&x.Lifecycle!=SoldierLifecycle.Retired&&x.Lifecycle!=SoldierLifecycle.Killed&&x.Lifecycle!=SoldierLifecycle.Captured);internal void Add(SoldierInstance x){if(x==null)throw new ArgumentNullException(nameof(x));if(_items.ContainsKey(x.Id))throw new InvalidOperationException("Duplicate SoldierId.");_items.Add(x.Id,x);}
    }
    public sealed class SoldierCampaignState
    {
        public SoldierCampaignState(SoldierDefinitionRegistry? definitions=null,EquipmentInstanceRegistry? equipment=null,SoldierRegistry? soldiers=null){Definitions=definitions??new SoldierDefinitionRegistry();Equipment=equipment??new EquipmentInstanceRegistry();Soldiers=soldiers??new SoldierRegistry();}public SoldierDefinitionRegistry Definitions{get;}public EquipmentInstanceRegistry Equipment{get;}public SoldierRegistry Soldiers{get;}internal void RestoreEquipment(EquipmentInstance x)=>Equipment.Add(x);internal void RestoreSoldier(SoldierInstance x)=>Soldiers.Add(x);
    }
}
