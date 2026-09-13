using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Economy;

namespace FOC.Domain.Soldiers
{
    public enum WeaponSlot { Main, Secondary, Sidearm, Throwing, Ammo, Special }
    public enum ArmorSlot { Inner, Body, Additional, Head, Limbs, MountArmor }
    public enum DamageType { Cut, Pierce, Blunt }
    public enum AttackMode { Thrust, Slash, Crush }
    public enum WeaponFamily { Sword, Sabre, Yatagan, Spear, Lance, Pike, Axe, Bow, TurkishBow, Firearm, Pistol, Javelin, Dagger, Polearm, Grenade }
    public enum MountContext { InfantryOnly, MountedOnly, Either }
    public enum AuxiliaryEquipmentKind { Ammunition, Special }
    public enum EquipmentKind { Weapon, Armor, Shield, Mount, Auxiliary }

    public sealed class WeaponAttackOption
    {
        public WeaponAttackOption(AttackMode mode,DamageType damageType){if(!Enum.IsDefined(typeof(AttackMode),mode)||!Enum.IsDefined(typeof(DamageType),damageType))throw new ArgumentOutOfRangeException(nameof(mode));Mode=mode;DamageType=damageType;}
        public AttackMode Mode{get;}public DamageType DamageType{get;}
    }

    public sealed class WeaponDefinition
    {
        private readonly List<WeaponSlot> _slots;private readonly List<WeaponAttackOption> _attacks;
        public WeaponDefinition(WeaponDefinitionId id,string name,WeaponFamily family,IEnumerable<WeaponSlot> allowedSlots,IEnumerable<WeaponAttackOption> attacks,MountContext mountContext,TradeGoodId economicGoodId,VisualProfileId visualProfileId,bool isRanged=false,AmmoFamilyId? ammoFamilyId=null)
        {if(!id.IsValid||!economicGoodId.IsValid||!visualProfileId.IsValid)throw new ArgumentException("Weapon references are invalid.");if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Weapon name is required.",nameof(name));if(!Enum.IsDefined(typeof(WeaponFamily),family)||!Enum.IsDefined(typeof(MountContext),mountContext))throw new ArgumentOutOfRangeException(nameof(family));_slots=CanonicalEnumSet(allowedSlots,"Weapon slot");if(_slots.Any(x=>x==WeaponSlot.Ammo))throw new ArgumentException("A weapon cannot occupy the ammunition slot.",nameof(allowedSlots));_attacks=(attacks??throw new ArgumentNullException(nameof(attacks))).ToList();if(_attacks.Count==0||_attacks.Any(x=>x==null))throw new ArgumentException("Weapon needs at least one attack option.",nameof(attacks));_attacks=_attacks.OrderBy(x=>x.Mode).ThenBy(x=>x.DamageType).ToList();if(isRanged!=ammoFamilyId.HasValue)throw new ArgumentException("Ranged weapons require exactly one ammunition family.");Id=id;Name=name;Family=family;MountContext=mountContext;EconomicGoodId=economicGoodId;VisualProfileId=visualProfileId;IsRanged=isRanged;AmmoFamilyId=ammoFamilyId;}
        public WeaponDefinitionId Id{get;}public string Name{get;}public WeaponFamily Family{get;}public IReadOnlyList<WeaponSlot> AllowedSlots=>_slots;public IReadOnlyList<WeaponAttackOption> AttackOptions=>_attacks;public MountContext MountContext{get;}public TradeGoodId EconomicGoodId{get;}public VisualProfileId VisualProfileId{get;}public bool IsRanged{get;}public AmmoFamilyId? AmmoFamilyId{get;}
        private static List<WeaponSlot> CanonicalEnumSet(IEnumerable<WeaponSlot> values,string label){if(values==null)throw new ArgumentNullException(nameof(values));var result=values.Distinct().OrderBy(x=>x).ToList();if(result.Count==0||result.Any(x=>!Enum.IsDefined(typeof(WeaponSlot),x)))throw new ArgumentException(label+" is invalid.");return result;}
    }

    public sealed class ArmorDefinition
    {
        public ArmorDefinition(ArmorDefinitionId id,string name,ArmorSlot slot,TradeGoodId economicGoodId,VisualProfileId visualProfileId,string qualityCode="unspecified")
        {if(!id.IsValid||!economicGoodId.IsValid||!visualProfileId.IsValid)throw new ArgumentException("Armor references are invalid.");if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(qualityCode))throw new ArgumentException("Armor content is incomplete.");if(!Enum.IsDefined(typeof(ArmorSlot),slot))throw new ArgumentOutOfRangeException(nameof(slot));Id=id;Name=name;Slot=slot;EconomicGoodId=economicGoodId;VisualProfileId=visualProfileId;QualityCode=qualityCode;}
        public ArmorDefinitionId Id{get;}public string Name{get;}public ArmorSlot Slot{get;}public TradeGoodId EconomicGoodId{get;}public VisualProfileId VisualProfileId{get;}public string QualityCode{get;}
    }

    public sealed class ShieldDefinition
    {
        public ShieldDefinition(ShieldDefinitionId id,string name,TradeGoodId economicGoodId,VisualProfileId visualProfileId,string classification)
        {if(!id.IsValid||!economicGoodId.IsValid||!visualProfileId.IsValid)throw new ArgumentException("Shield references are invalid.");if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(classification))throw new ArgumentException("Shield content is incomplete.");Id=id;Name=name;EconomicGoodId=economicGoodId;VisualProfileId=visualProfileId;Classification=classification;}
        public ShieldDefinitionId Id{get;}public string Name{get;}public TradeGoodId EconomicGoodId{get;}public VisualProfileId VisualProfileId{get;}public string Classification{get;}
    }

    public sealed class MountDefinition
    {
        public MountDefinition(MountDefinitionId id,string name,TradeGoodId economicGoodId,VisualProfileId visualProfileId,string mobilityClass)
        {if(!id.IsValid||!economicGoodId.IsValid||!visualProfileId.IsValid)throw new ArgumentException("Mount references are invalid.");if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(mobilityClass))throw new ArgumentException("Mount content is incomplete.");Id=id;Name=name;EconomicGoodId=economicGoodId;VisualProfileId=visualProfileId;MobilityClass=mobilityClass;}
        public MountDefinitionId Id{get;}public string Name{get;}public TradeGoodId EconomicGoodId{get;}public VisualProfileId VisualProfileId{get;}public string MobilityClass{get;}
    }

    public sealed class AuxiliaryEquipmentDefinition
    {
        public AuxiliaryEquipmentDefinition(AuxiliaryEquipmentDefinitionId id,string name,AuxiliaryEquipmentKind kind,TradeGoodId economicGoodId,VisualProfileId visualProfileId,AmmoFamilyId? ammoFamilyId=null)
        {if(!id.IsValid||!economicGoodId.IsValid||!visualProfileId.IsValid)throw new ArgumentException("Auxiliary equipment references are invalid.");if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Equipment name is required.",nameof(name));if(!Enum.IsDefined(typeof(AuxiliaryEquipmentKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));if((kind==AuxiliaryEquipmentKind.Ammunition)!=ammoFamilyId.HasValue)throw new ArgumentException("Ammunition equipment requires exactly one ammunition family.");Id=id;Name=name;Kind=kind;EconomicGoodId=economicGoodId;VisualProfileId=visualProfileId;AmmoFamilyId=ammoFamilyId;}
        public AuxiliaryEquipmentDefinitionId Id{get;}public string Name{get;}public AuxiliaryEquipmentKind Kind{get;}public TradeGoodId EconomicGoodId{get;}public VisualProfileId VisualProfileId{get;}public AmmoFamilyId? AmmoFamilyId{get;}
    }

    public sealed class TroopDefinition
    {
        private readonly List<WeaponFamily> _families;
        public TroopDefinition(TroopDefinitionId id,string name,UnitClassId unitClassId,CombatRoleId defaultCombatRoleId,MountContext mountContext,VisualProfileId visualProfileId,IEnumerable<WeaponFamily>? allowedWeaponFamilies=null,bool allowsArmor=true,bool allowsShield=true)
        {if(!id.IsValid||!unitClassId.IsValid||!defaultCombatRoleId.IsValid||!visualProfileId.IsValid)throw new ArgumentException("Troop definition references are invalid.");if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Troop name is required.",nameof(name));if(!Enum.IsDefined(typeof(MountContext),mountContext))throw new ArgumentOutOfRangeException(nameof(mountContext));_families=(allowedWeaponFamilies??Array.Empty<WeaponFamily>()).Distinct().OrderBy(x=>x).ToList();if(_families.Any(x=>!Enum.IsDefined(typeof(WeaponFamily),x)))throw new ArgumentException("Allowed weapon family is invalid.");Id=id;Name=name;UnitClassId=unitClassId;DefaultCombatRoleId=defaultCombatRoleId;MountContext=mountContext;VisualProfileId=visualProfileId;AllowsArmor=allowsArmor;AllowsShield=allowsShield;}
        public TroopDefinitionId Id{get;}public string Name{get;}public UnitClassId UnitClassId{get;}public CombatRoleId DefaultCombatRoleId{get;}public MountContext MountContext{get;}public VisualProfileId VisualProfileId{get;}public IReadOnlyList<WeaponFamily> AllowedWeaponFamilies=>_families;public bool AllowsArmor{get;}public bool AllowsShield{get;}
    }

    public sealed class SoldierDefinitionRegistry
    {
        private readonly SortedDictionary<TroopDefinitionId,TroopDefinition> _troops=new SortedDictionary<TroopDefinitionId,TroopDefinition>();private readonly SortedDictionary<WeaponDefinitionId,WeaponDefinition> _weapons=new SortedDictionary<WeaponDefinitionId,WeaponDefinition>();private readonly SortedDictionary<ArmorDefinitionId,ArmorDefinition> _armor=new SortedDictionary<ArmorDefinitionId,ArmorDefinition>();private readonly SortedDictionary<ShieldDefinitionId,ShieldDefinition> _shields=new SortedDictionary<ShieldDefinitionId,ShieldDefinition>();private readonly SortedDictionary<MountDefinitionId,MountDefinition> _mounts=new SortedDictionary<MountDefinitionId,MountDefinition>();private readonly SortedDictionary<AuxiliaryEquipmentDefinitionId,AuxiliaryEquipmentDefinition> _auxiliary=new SortedDictionary<AuxiliaryEquipmentDefinitionId,AuxiliaryEquipmentDefinition>();
        public IReadOnlyCollection<TroopDefinition> OrderedTroops=>_troops.Values;public IReadOnlyCollection<WeaponDefinition> OrderedWeapons=>_weapons.Values;public IReadOnlyCollection<ArmorDefinition> OrderedArmor=>_armor.Values;public IReadOnlyCollection<ShieldDefinition> OrderedShields=>_shields.Values;public IReadOnlyCollection<MountDefinition> OrderedMounts=>_mounts.Values;public IReadOnlyCollection<AuxiliaryEquipmentDefinition> OrderedAuxiliary=>_auxiliary.Values;
        public void Add(TroopDefinition x)=>Add(_troops,x.Id,x,"TroopDefinitionId");public void Add(WeaponDefinition x)=>Add(_weapons,x.Id,x,"WeaponDefinitionId");public void Add(ArmorDefinition x)=>Add(_armor,x.Id,x,"ArmorDefinitionId");public void Add(ShieldDefinition x)=>Add(_shields,x.Id,x,"ShieldDefinitionId");public void Add(MountDefinition x)=>Add(_mounts,x.Id,x,"MountDefinitionId");public void Add(AuxiliaryEquipmentDefinition x)=>Add(_auxiliary,x.Id,x,"AuxiliaryEquipmentDefinitionId");
        public TroopDefinition GetRequired(TroopDefinitionId id)=>Get(_troops,id,"Troop definition");public WeaponDefinition GetRequired(WeaponDefinitionId id)=>Get(_weapons,id,"Weapon definition");public ArmorDefinition GetRequired(ArmorDefinitionId id)=>Get(_armor,id,"Armor definition");public ShieldDefinition GetRequired(ShieldDefinitionId id)=>Get(_shields,id,"Shield definition");public MountDefinition GetRequired(MountDefinitionId id)=>Get(_mounts,id,"Mount definition");public AuxiliaryEquipmentDefinition GetRequired(AuxiliaryEquipmentDefinitionId id)=>Get(_auxiliary,id,"Auxiliary equipment definition");
        public bool Contains(TroopDefinitionId id)=>_troops.ContainsKey(id);public bool Contains(WeaponDefinitionId id)=>_weapons.ContainsKey(id);public bool Contains(ArmorDefinitionId id)=>_armor.ContainsKey(id);public bool Contains(ShieldDefinitionId id)=>_shields.ContainsKey(id);public bool Contains(MountDefinitionId id)=>_mounts.ContainsKey(id);public bool Contains(AuxiliaryEquipmentDefinitionId id)=>_auxiliary.ContainsKey(id);
        private static void Add<TKey,TValue>(SortedDictionary<TKey,TValue> map,TKey key,TValue value,string label)where TKey:notnull where TValue:class{if(value==null)throw new ArgumentNullException(nameof(value));if(map.ContainsKey(key))throw new InvalidOperationException("Duplicate "+label+".");map.Add(key,value);}private static TValue Get<TKey,TValue>(SortedDictionary<TKey,TValue> map,TKey key,string label)where TKey:notnull{if(!map.TryGetValue(key,out var value))throw new KeyNotFoundException(label+" was not found.");return value;}
    }
}
