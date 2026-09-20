#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    [CreateAssetMenu(menuName="FOC/Visuals/Visual Catalog",fileName="FOC_VisualCatalog")]
    public sealed class VisualCatalogAsset:ScriptableObject
    {
        [Serializable]public sealed class AssetEntry{public string id=string.Empty;public VisualAssetCategory category;public GameObject prefab=null!;public RigKind rigKind;public int lodCount=3;public int rendererCount=1;public int materialCount=1;public VisualSocket[] sockets=Array.Empty<VisualSocket>();}
        [Serializable]public sealed class ProfileEntry{public string visualProfileId=string.Empty;public string bodyFamily="standard-human";public string bodyAssetId=string.Empty;public string headAssetId=string.Empty;public string clothingAssetId=string.Empty;public string headgearAssetId=string.Empty;public VisualQualityTier qualityTier=VisualQualityTier.Standard;public string cultureFamily="generic";}
        [Serializable]public sealed class EquipmentEntry{public EquipmentKind definitionKind;public string definitionId=string.Empty;public string visualAssetId=string.Empty;public VisualSocket socket;public string bodyFamily="any";}
        [SerializeField]private List<AssetEntry> assets=new List<AssetEntry>();[SerializeField]private List<ProfileEntry> profiles=new List<ProfileEntry>();[SerializeField]private List<EquipmentEntry> equipment=new List<EquipmentEntry>();[SerializeField]private string consolidatedCharacterAssetId="CHR_Consolidated_Proof";[SerializeField]private string crowdAssetId="CRWD_Far_Proof";[SerializeField]private int catalogRevision=1;private Dictionary<string,GameObject>? prefabLookup;
        public IReadOnlyList<AssetEntry> Assets=>assets;public IReadOnlyList<ProfileEntry> Profiles=>profiles;public IReadOnlyList<EquipmentEntry> Equipment=>equipment;public VisualAssetId ConsolidatedCharacterAssetId=>VisualAssetId.Create(consolidatedCharacterAssetId);public VisualAssetId CrowdAssetId=>VisualAssetId.Create(crowdAssetId);public int CatalogRevision=>catalogRevision;
        public void ReplaceContent(IEnumerable<AssetEntry> newAssets,IEnumerable<ProfileEntry> newProfiles,IEnumerable<EquipmentEntry> newEquipment,string consolidatedId="CHR_Consolidated_Proof",string crowdId="CRWD_Far_Proof",int revision=1){if(string.IsNullOrWhiteSpace(consolidatedId)||string.IsNullOrWhiteSpace(crowdId)||revision<1)throw new ArgumentException("Visual runtime catalog configuration is invalid.");assets=new List<AssetEntry>(newAssets??throw new ArgumentNullException(nameof(newAssets)));profiles=new List<ProfileEntry>(newProfiles??throw new ArgumentNullException(nameof(newProfiles)));equipment=new List<EquipmentEntry>(newEquipment??throw new ArgumentNullException(nameof(newEquipment)));consolidatedCharacterAssetId=consolidatedId;crowdAssetId=crowdId;catalogRevision=revision;prefabLookup=null;}
        public VisualCatalog BuildCoreCatalog(){var result=new VisualCatalog();foreach(var x in assets)result.Add(new VisualAssetRecord(VisualAssetId.Create(x.id),x.category,x.prefab==null?"missing":x.prefab.name,x.rigKind,x.lodCount,x.rendererCount,x.materialCount,x.sockets));foreach(var x in profiles)result.Add(new VisualProfile(VisualProfileId.Create(x.visualProfileId),x.bodyFamily,VisualAssetId.Create(x.bodyAssetId),VisualAssetId.Create(x.headAssetId),VisualAssetId.Create(x.clothingAssetId),string.IsNullOrEmpty(x.headgearAssetId)?(VisualAssetId?)null:VisualAssetId.Create(x.headgearAssetId),x.qualityTier,x.cultureFamily));foreach(var x in equipment)result.Add(new EquipmentVisualMapping(Definition(x.definitionKind,x.definitionId),VisualAssetId.Create(x.visualAssetId),x.socket,x.bodyFamily));return result;}
        public GameObject GetPrefab(VisualAssetId id){EnsureLookup();if(!prefabLookup!.TryGetValue(id.Value,out var value)||value==null)throw new KeyNotFoundException("Visual prefab is missing: "+id.Value);return value;}
        public bool TryGetFirstAssetId(VisualAssetCategory category,out VisualAssetId id){foreach(var entry in assets)if(entry.category==category&&!string.IsNullOrWhiteSpace(entry.id)){id=VisualAssetId.Create(entry.id);return true;}id=default;return false;}
        private void OnValidate(){prefabLookup=null;}
        private void EnsureLookup(){if(prefabLookup!=null)return;prefabLookup=new Dictionary<string,GameObject>(StringComparer.Ordinal);foreach(var x in assets){if(string.IsNullOrWhiteSpace(x.id)||x.prefab==null||prefabLookup.ContainsKey(x.id))continue;prefabLookup.Add(x.id,x.prefab);}}
        private static EquipmentDefinitionRef Definition(EquipmentKind kind,string id){switch(kind){case EquipmentKind.Weapon:return EquipmentDefinitionRef.Weapon(WeaponDefinitionId.Create(id));case EquipmentKind.Armor:return EquipmentDefinitionRef.Armor(ArmorDefinitionId.Create(id));case EquipmentKind.Shield:return EquipmentDefinitionRef.Shield(ShieldDefinitionId.Create(id));case EquipmentKind.Mount:return EquipmentDefinitionRef.Mount(MountDefinitionId.Create(id));case EquipmentKind.Auxiliary:return EquipmentDefinitionRef.Auxiliary(AuxiliaryEquipmentDefinitionId.Create(id));default:throw new ArgumentOutOfRangeException(nameof(kind));}}
    }

}
