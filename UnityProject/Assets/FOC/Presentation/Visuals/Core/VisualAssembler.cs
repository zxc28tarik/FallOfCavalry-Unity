#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;

namespace FOC.Visuals.Core
{
    public enum VisualRepresentationKind { Modular, Consolidated, Crowd }

    public sealed class VisualModule
    {
        public VisualModule(VisualAssetId assetId,VisualAssetCategory category,VisualSocket? socket,string semanticSource){AssetId=assetId;Category=category;Socket=socket;SemanticSource=semanticSource??string.Empty;}public VisualAssetId AssetId{get;}public VisualAssetCategory Category{get;}public VisualSocket? Socket{get;}public string SemanticSource{get;}
    }

    public readonly struct VisualSoldierSignature:IEquatable<VisualSoldierSignature>
    {
        public VisualSoldierSignature(string value){if(string.IsNullOrWhiteSpace(value))throw new ArgumentException(nameof(value));Value=value;}public string Value{get;}public bool Equals(VisualSoldierSignature other)=>StringComparer.Ordinal.Equals(Value,other.Value);public override bool Equals(object? obj)=>obj is VisualSoldierSignature other&&Equals(other);public override int GetHashCode()=>StringComparer.Ordinal.GetHashCode(Value??string.Empty);public override string ToString()=>Value;
    }

    public sealed class VisualAssemblyPlan
    {
        internal VisualAssemblyPlan(SoldierId soldierId,VisualProfile profile,IEnumerable<VisualModule> modules,VisualSoldierSignature signature,uint appearanceVariant,VisualRepresentationKind representationKind){SoldierId=soldierId;Profile=profile;Modules=modules.ToArray();Signature=signature;AppearanceVariant=appearanceVariant;RepresentationKind=representationKind;}public SoldierId SoldierId{get;}public VisualProfile Profile{get;}public IReadOnlyList<VisualModule> Modules{get;}public VisualSoldierSignature Signature{get;}public uint AppearanceVariant{get;}public VisualRepresentationKind RepresentationKind{get;}
    }

    public sealed class VisualSoldierPlanner
    {
        private readonly VisualCatalog _catalog;public VisualSoldierPlanner(VisualCatalog catalog){_catalog=catalog??throw new ArgumentNullException(nameof(catalog));}
        public VisualAssemblyPlan Plan(SoldierInstance soldier,TroopDefinition troop,IReadOnlyDictionary<EquipmentInstanceId,EquipmentInstance> equipment){if(soldier==null||troop==null||equipment==null)throw new ArgumentNullException();if(!soldier.TroopDefinitionId.Equals(troop.Id))throw new InvalidOperationException("Soldier and troop definition conflict.");var profile=_catalog.GetProfile(troop.VisualProfileId);var modules=new List<VisualModule>{Module(profile.Body,VisualAssetCategory.Body,null,"profile:body"),Module(profile.Head,VisualAssetCategory.Head,null,"profile:head"),Module(profile.Clothing,VisualAssetCategory.Clothing,null,"profile:clothing")};if(profile.Headgear.HasValue)modules.Add(Module(profile.Headgear.Value,VisualAssetCategory.Headgear,VisualSocket.Head,"profile:headgear"));foreach(var assignment in soldier.Loadout.OrderedWeapons)modules.Add(Resolve(equipment,assignment.EquipmentId,assignment.Slot,profile.BodyFamily));foreach(var assignment in soldier.Loadout.OrderedArmor)modules.Add(Resolve(equipment,assignment.EquipmentId,null,profile.BodyFamily));if(soldier.Loadout.Shield.HasValue)modules.Add(Resolve(equipment,soldier.Loadout.Shield.Value,null,profile.BodyFamily));if(soldier.Loadout.Mount.HasValue)modules.Add(Resolve(equipment,soldier.Loadout.Mount.Value,null,profile.BodyFamily));modules=modules.OrderBy(x=>x.Category).ThenBy(x=>x.AssetId).ThenBy(x=>x.SemanticSource,StringComparer.Ordinal).ToList();var variant=StableHash(soldier.Id.Value+"|"+troop.VisualProfileId.Value);var kind=profile.QualityTier==VisualQualityTier.Narrative?VisualRepresentationKind.Modular:profile.QualityTier==VisualQualityTier.Crowd?VisualRepresentationKind.Crowd:VisualRepresentationKind.Consolidated;var source=profile.Id.Value+"|"+kind+"|"+string.Join("|",modules.Select(x=>x.Category+":"+x.AssetId.Value+":"+(x.Socket?.ToString()??"none")));return new VisualAssemblyPlan(soldier.Id,profile,modules,new VisualSoldierSignature(StableHash64(source).ToString("x16")),variant,kind);}
        private VisualModule Resolve(IReadOnlyDictionary<EquipmentInstanceId,EquipmentInstance> equipment,EquipmentInstanceId id,WeaponSlot? weaponSlot,string bodyFamily){if(!equipment.TryGetValue(id,out var instance))throw new KeyNotFoundException("Loadout equipment instance is missing: "+id.Value);var mapping=_catalog.GetEquipment(instance.Definition);var asset=_catalog.GetAsset(mapping.AssetId);if(!StringComparer.Ordinal.Equals(mapping.BodyFamily,"any")&&!StringComparer.Ordinal.Equals(mapping.BodyFamily,bodyFamily))throw new InvalidOperationException("Visual equipment and body family are incompatible.");var socket=weaponSlot.HasValue?SocketFor(weaponSlot.Value):mapping.Socket;return Module(mapping.AssetId,asset.Category,socket,instance.Definition.Kind+":"+instance.Definition.Id);}
        private VisualModule Module(VisualAssetId id,VisualAssetCategory category,VisualSocket? socket,string source){_catalog.GetAsset(id);return new VisualModule(id,category,socket,source);}private static VisualSocket SocketFor(WeaponSlot slot){switch(slot){case WeaponSlot.Main:return VisualSocket.RightHand;case WeaponSlot.Secondary:return VisualSocket.BackSecondary;case WeaponSlot.Sidearm:return VisualSocket.HipLeft;case WeaponSlot.Throwing:return VisualSocket.HipRight;case WeaponSlot.Ammo:return VisualSocket.BackPrimary;case WeaponSlot.Special:return VisualSocket.HipRight;default:throw new ArgumentOutOfRangeException(nameof(slot));}}
        public static uint StableHash(string value){unchecked{uint hash=2166136261;foreach(var c in value){hash^=c;hash*=16777619;}return hash;}}private static ulong StableHash64(string value){unchecked{ulong hash=14695981039346656037UL;foreach(var c in value){hash^=c;hash*=1099511628211UL;}return hash;}}
    }

    public sealed class VisualSoldierBindingState
    {
        public SoldierId? SoldierId{get;private set;}public VisualSoldierSignature? Signature{get;private set;}public VisualQualityTier QualityTier{get;private set;}public bool IsBound=>SoldierId.HasValue;public void Bind(VisualAssemblyPlan plan){if(plan==null)throw new ArgumentNullException(nameof(plan));SoldierId=plan.SoldierId;Signature=plan.Signature;QualityTier=plan.Profile.QualityTier;}public void Clear(){SoldierId=null;Signature=null;QualityTier=default;}
    }

    public sealed class BoundedSignatureCache<T> where T:class
    {
        private readonly int _capacity;private readonly Dictionary<VisualSoldierSignature,T> _values=new Dictionary<VisualSoldierSignature,T>();private readonly Queue<VisualSoldierSignature> _order=new Queue<VisualSoldierSignature>();public BoundedSignatureCache(int capacity){if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));_capacity=capacity;}public int Count=>_values.Count;public bool TryGet(VisualSoldierSignature key,out T? value)=>_values.TryGetValue(key,out value);public void Put(VisualSoldierSignature key,T value){if(value==null)throw new ArgumentNullException(nameof(value));if(_values.ContainsKey(key)){_values[key]=value;return;}while(_values.Count>=_capacity){var old=_order.Dequeue();_values.Remove(old);}_values.Add(key,value);_order.Enqueue(key);}
    }
}
