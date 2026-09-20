#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;

namespace FOC.Visuals.Core
{
    public enum VisualAssetCategory { Body, Head, Hair, Beard, Clothing, BodyArmor, AdditionalArmor, Headgear, Gloves, Boots, Cape, Weapon, Shield, Auxiliary, Mount, Harness, MountArmor, Animation, ConsolidatedCharacter, Crowd }
    public enum VisualQualityTier { Narrative, Standard, Crowd }
    public enum VisualAssetLifecycle { Draft, Validated, ProductionCandidate, Approved }
    public enum RigKind { Humanoid, GenericMount }
    public enum AnimationCapability { Unarmed, OneHanded, Shield, Spear, Lance, Polearm, Firearm, Throwing, Mounted }
    public enum VisualSocket { RightHand, LeftHand, Head, BackPrimary, BackSecondary, HipLeft, HipRight, ShieldBack, Rider }

    public readonly struct VisualAssetId:IEquatable<VisualAssetId>,IComparable<VisualAssetId>
    {
        private VisualAssetId(string value){Value=value;}public string Value{get;}public bool IsValid=>!string.IsNullOrWhiteSpace(Value);
        public static VisualAssetId Create(string value){if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("Visual asset ID is required.",nameof(value));return new VisualAssetId(value.Trim());}
        public int CompareTo(VisualAssetId other)=>StringComparer.Ordinal.Compare(Value,other.Value);public bool Equals(VisualAssetId other)=>StringComparer.Ordinal.Equals(Value,other.Value);public override bool Equals(object? obj)=>obj is VisualAssetId other&&Equals(other);public override int GetHashCode()=>StringComparer.Ordinal.GetHashCode(Value??string.Empty);public override string ToString()=>Value??string.Empty;
    }

    public sealed class VisualAssetProvenance
    {
        public VisualAssetProvenance(string tool,string source,string license,string generationDate,string revision){if(string.IsNullOrWhiteSpace(tool)||string.IsNullOrWhiteSpace(source)||string.IsNullOrWhiteSpace(license)||string.IsNullOrWhiteSpace(revision))throw new ArgumentException("Visual provenance is incomplete.");Tool=tool;Source=source;License=license;GenerationDate=generationDate??string.Empty;Revision=revision;}
        public string Tool{get;}public string Source{get;}public string License{get;}public string GenerationDate{get;}public string Revision{get;}
    }

    public sealed class VisualAssetSpec
    {
        private readonly List<VisualSocket> _sockets;private readonly List<string> _bodyFamilies;private readonly List<string> _styleTags;
        public VisualAssetSpec(VisualAssetId id,VisualAssetCategory category,string historicalPeriod,string culture,string visualRole,string bodyFamily,string materialDescription,RigKind requiredRig,IEnumerable<VisualSocket> sockets,IEnumerable<string> compatibleBodyFamilies,string lodProfile,string textureProfile,string scaleCode,string gameplayDefinitionKey,string historicalDescription,IEnumerable<string> styleTags,VisualAssetProvenance provenance,VisualAssetLifecycle lifecycle)
        {if(!id.IsValid||!Enum.IsDefined(typeof(VisualAssetCategory),category)||!Enum.IsDefined(typeof(RigKind),requiredRig)||!Enum.IsDefined(typeof(VisualAssetLifecycle),lifecycle))throw new ArgumentException("Visual asset specification identity is invalid.");if(string.IsNullOrWhiteSpace(historicalPeriod)||string.IsNullOrWhiteSpace(culture)||string.IsNullOrWhiteSpace(visualRole)||string.IsNullOrWhiteSpace(bodyFamily)||string.IsNullOrWhiteSpace(materialDescription)||string.IsNullOrWhiteSpace(lodProfile)||string.IsNullOrWhiteSpace(textureProfile)||string.IsNullOrWhiteSpace(scaleCode)||provenance==null)throw new ArgumentException("Visual asset specification is incomplete.");Id=id;Category=category;HistoricalPeriod=historicalPeriod;Culture=culture;VisualRole=visualRole;BodyFamily=bodyFamily;MaterialDescription=materialDescription;RequiredRig=requiredRig;LodProfile=lodProfile;TextureProfile=textureProfile;ScaleCode=scaleCode;GameplayDefinitionKey=gameplayDefinitionKey??string.Empty;HistoricalDescription=historicalDescription??string.Empty;Provenance=provenance;Lifecycle=lifecycle;_sockets=(sockets??Array.Empty<VisualSocket>()).Distinct().OrderBy(x=>x).ToList();_bodyFamilies=Canonical(compatibleBodyFamilies);_styleTags=Canonical(styleTags);}
        public VisualAssetId Id{get;}public VisualAssetCategory Category{get;}public string HistoricalPeriod{get;}public string Culture{get;}public string VisualRole{get;}public string BodyFamily{get;}public string MaterialDescription{get;}public RigKind RequiredRig{get;}public IReadOnlyList<VisualSocket> RequiredSockets=>_sockets;public IReadOnlyList<string> CompatibleBodyFamilies=>_bodyFamilies;public string LodProfile{get;}public string TextureProfile{get;}public string ScaleCode{get;}public string GameplayDefinitionKey{get;}public string HistoricalDescription{get;}public IReadOnlyList<string> StyleTags=>_styleTags;public VisualAssetProvenance Provenance{get;}public VisualAssetLifecycle Lifecycle{get;}
        private static List<string> Canonical(IEnumerable<string>? values)=> (values??Array.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList();
    }

    public sealed class VisualProfile
    {
        public VisualProfile(VisualProfileId id,string bodyFamily,VisualAssetId body,VisualAssetId head,VisualAssetId clothing,VisualAssetId? headgear,VisualQualityTier qualityTier,string cultureFamily){if(!id.IsValid||!body.IsValid||!head.IsValid||!clothing.IsValid||string.IsNullOrWhiteSpace(bodyFamily)||string.IsNullOrWhiteSpace(cultureFamily)||!Enum.IsDefined(typeof(VisualQualityTier),qualityTier))throw new ArgumentException("Visual profile is invalid.");Id=id;BodyFamily=bodyFamily;Body=body;Head=head;Clothing=clothing;Headgear=headgear;QualityTier=qualityTier;CultureFamily=cultureFamily;}
        public VisualProfileId Id{get;}public string BodyFamily{get;}public VisualAssetId Body{get;}public VisualAssetId Head{get;}public VisualAssetId Clothing{get;}public VisualAssetId? Headgear{get;}public VisualQualityTier QualityTier{get;}public string CultureFamily{get;}
    }

    public sealed class EquipmentVisualMapping
    {
        public EquipmentVisualMapping(EquipmentDefinitionRef definition,VisualAssetId assetId,VisualSocket socket,string bodyFamily="standard-human"){if(string.IsNullOrWhiteSpace(definition.Id)||!assetId.IsValid||string.IsNullOrWhiteSpace(bodyFamily))throw new ArgumentException("Equipment visual mapping is invalid.");Definition=definition;AssetId=assetId;Socket=socket;BodyFamily=bodyFamily;}
        public EquipmentDefinitionRef Definition{get;}public VisualAssetId AssetId{get;}public VisualSocket Socket{get;}public string BodyFamily{get;}
    }

    public sealed class VisualAssetRecord
    {
        public VisualAssetRecord(VisualAssetId id,VisualAssetCategory category,string resourceKey,RigKind rigKind,int lodCount,int rendererCount,int materialCount,IEnumerable<VisualSocket>? sockets=null){if(!id.IsValid||string.IsNullOrWhiteSpace(resourceKey)||lodCount<1||rendererCount<1||materialCount<1)throw new ArgumentException("Visual asset record is invalid.");Id=id;Category=category;ResourceKey=resourceKey;RigKind=rigKind;LodCount=lodCount;RendererCount=rendererCount;MaterialCount=materialCount;Sockets=(sockets??Array.Empty<VisualSocket>()).Distinct().OrderBy(x=>x).ToArray();}
        public VisualAssetId Id{get;}public VisualAssetCategory Category{get;}public string ResourceKey{get;}public RigKind RigKind{get;}public int LodCount{get;}public int RendererCount{get;}public int MaterialCount{get;}public IReadOnlyList<VisualSocket> Sockets{get;}
    }
}
