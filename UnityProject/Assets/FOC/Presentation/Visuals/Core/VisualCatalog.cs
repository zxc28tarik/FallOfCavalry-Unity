#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;

namespace FOC.Visuals.Core
{
    public sealed class VisualCatalogValidation
    {
        internal VisualCatalogValidation(IEnumerable<string> errors,IEnumerable<string> warnings){Errors=errors.ToArray();Warnings=warnings.ToArray();}public IReadOnlyList<string> Errors{get;}public IReadOnlyList<string> Warnings{get;}public bool IsValid=>Errors.Count==0;
    }

    public sealed class VisualCatalog
    {
        private readonly SortedDictionary<VisualAssetId,VisualAssetRecord> _assets=new SortedDictionary<VisualAssetId,VisualAssetRecord>();private readonly SortedDictionary<string,VisualProfile> _profiles=new SortedDictionary<string,VisualProfile>(StringComparer.Ordinal);private readonly SortedDictionary<string,EquipmentVisualMapping> _equipment=new SortedDictionary<string,EquipmentVisualMapping>(StringComparer.Ordinal);
        public IReadOnlyCollection<VisualAssetRecord> OrderedAssets=>_assets.Values;public IReadOnlyCollection<VisualProfile> OrderedProfiles=>_profiles.Values;public IReadOnlyCollection<EquipmentVisualMapping> OrderedEquipment=>_equipment.Values;
        public void Add(VisualAssetRecord record){if(record==null)throw new ArgumentNullException(nameof(record));if(_assets.ContainsKey(record.Id))throw new InvalidOperationException("Duplicate visual asset ID.");_assets.Add(record.Id,record);}public void Add(VisualProfile profile){if(profile==null)throw new ArgumentNullException(nameof(profile));if(_profiles.ContainsKey(profile.Id.Value))throw new InvalidOperationException("Duplicate visual profile ID.");_profiles.Add(profile.Id.Value,profile);}public void Add(EquipmentVisualMapping mapping){if(mapping==null)throw new ArgumentNullException(nameof(mapping));var key=Key(mapping.Definition);if(_equipment.ContainsKey(key))throw new InvalidOperationException("Duplicate equipment visual mapping.");_equipment.Add(key,mapping);}
        public VisualAssetRecord GetAsset(VisualAssetId id){if(!_assets.TryGetValue(id,out var result))throw new KeyNotFoundException("Visual asset mapping is missing: "+id.Value);return result;}public VisualProfile GetProfile(VisualProfileId id){if(!_profiles.TryGetValue(id.Value,out var result))throw new KeyNotFoundException("Visual profile mapping is missing: "+id.Value);return result;}public EquipmentVisualMapping GetEquipment(EquipmentDefinitionRef definition){if(!_equipment.TryGetValue(Key(definition),out var result))throw new KeyNotFoundException("Equipment visual mapping is missing: "+definition.Kind+":"+definition.Id);return result;}public bool TryGetEquipment(EquipmentDefinitionRef definition,out EquipmentVisualMapping? mapping){var ok=_equipment.TryGetValue(Key(definition),out var x);mapping=x;return ok;}
        public VisualCatalogValidation Validate(){var errors=new List<string>();var warnings=new List<string>();foreach(var profile in _profiles.Values){Require(profile.Body,profile.Id.Value,errors);Require(profile.Head,profile.Id.Value,errors);Require(profile.Clothing,profile.Id.Value,errors);if(profile.Headgear.HasValue)Require(profile.Headgear.Value,profile.Id.Value,errors);}foreach(var mapping in _equipment.Values){Require(mapping.AssetId,mapping.Definition.Id,errors);if(_assets.TryGetValue(mapping.AssetId,out var asset)&&asset.LodCount<3&&asset.Category!=VisualAssetCategory.Animation)warnings.Add("Asset has fewer than three LODs: "+asset.Id.Value);}return new VisualCatalogValidation(errors,warnings);}private void Require(VisualAssetId id,string owner,List<string> errors){if(!_assets.ContainsKey(id))errors.Add("Missing visual asset '"+id.Value+"' for '"+owner+"'.");}private static string Key(EquipmentDefinitionRef d)=>((int)d.Kind).ToString()+":"+d.Id;
    }
}
