using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;

namespace FOC.Domain.Religion
{
    public enum ContentStatus { Active, Inactive }
    public enum ReligionProfileTargetKind { City, Faction, Region }
    public enum ReligionRecognition { Unrecognized, Recognized, Official }
    public enum ReligionTreatment { Tolerated, Restricted, Privileged }
    public enum ReligionEnforcement { Nominal, LocalDiscretion, Enforced }

    public sealed class ReligionDefinition
    {
        public ReligionDefinition(ReligionId id, string name, ContentStatus status = ContentStatus.Active) { if (!id.IsValid) throw new ArgumentException("ReligionId is invalid.", nameof(id)); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name)); if (!Enum.IsDefined(typeof(ContentStatus), status)) throw new ArgumentOutOfRangeException(nameof(status)); Id = id; Name = name; Status = status; }
        public ReligionId Id { get; } public string Name { get; } public ContentStatus Status { get; }
    }
    public sealed class SectDefinition
    {
        public SectDefinition(SectId id, ReligionId parentReligionId, string name, ContentStatus status = ContentStatus.Active) { if (!id.IsValid) throw new ArgumentException("SectId is invalid.", nameof(id)); if (!parentReligionId.IsValid) throw new ArgumentException("Parent ReligionId is invalid.", nameof(parentReligionId)); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name)); if (!Enum.IsDefined(typeof(ContentStatus), status)) throw new ArgumentOutOfRangeException(nameof(status)); Id = id; ParentReligionId = parentReligionId; Name = name; Status = status; }
        public SectId Id { get; } public ReligionId ParentReligionId { get; } public string Name { get; } public ContentStatus Status { get; }
    }
    public sealed class ReligionRegistry
    {
        private readonly SortedDictionary<ReligionId, ReligionDefinition> _religions = new SortedDictionary<ReligionId, ReligionDefinition>();
        private readonly SortedDictionary<SectId, SectDefinition> _sects = new SortedDictionary<SectId, SectDefinition>();
        public IReadOnlyCollection<ReligionDefinition> OrderedReligions => _religions.Values; public IReadOnlyCollection<SectDefinition> OrderedSects => _sects.Values;
        public void AddReligion(ReligionDefinition item) { if (item == null) throw new ArgumentNullException(nameof(item)); if (_religions.ContainsKey(item.Id)) throw new InvalidOperationException("Duplicate ReligionId."); _religions.Add(item.Id, item); }
        public void AddSect(SectDefinition item) { if (item == null) throw new ArgumentNullException(nameof(item)); if (!_religions.ContainsKey(item.ParentReligionId)) throw new InvalidOperationException("Sect parent Religion does not exist."); if (_sects.ContainsKey(item.Id)) throw new InvalidOperationException("Duplicate SectId."); _sects.Add(item.Id, item); }
        public ReligionDefinition GetRequired(ReligionId id) { if (!_religions.TryGetValue(id, out var item)) throw new KeyNotFoundException("Religion was not found."); return item; }
        public SectDefinition GetRequired(SectId id) { if (!_sects.TryGetValue(id, out var item)) throw new KeyNotFoundException("Sect was not found."); return item; }
        public void RequireCompatible(ReligionId religionId, SectId? sectId) { GetRequired(religionId); if (sectId.HasValue && !GetRequired(sectId.Value).ParentReligionId.Equals(religionId)) throw new InvalidOperationException("Sect does not belong to Religion."); }
    }
    public sealed class CharacterReligionState
    {
        public CharacterReligionState(CharacterId characterId, ReligionId religionId, SectId? sectId = null) { if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId)); if (!religionId.IsValid) throw new ArgumentException("ReligionId is invalid.", nameof(religionId)); if (sectId.HasValue && !sectId.Value.IsValid) throw new ArgumentException("SectId is invalid.", nameof(sectId)); CharacterId = characterId; ReligionId = religionId; SectId = sectId; }
        public CharacterId CharacterId { get; } public ReligionId ReligionId { get; } public SectId? SectId { get; }
    }
    public sealed class CharacterReligionRegistry
    {
        private readonly SortedDictionary<CharacterId, CharacterReligionState> _items = new SortedDictionary<CharacterId, CharacterReligionState>();
        public IReadOnlyCollection<CharacterReligionState> OrderedStates => _items.Values;
        public void Add(CharacterReligionState state) { if (state == null) throw new ArgumentNullException(nameof(state)); if (_items.ContainsKey(state.CharacterId)) throw new InvalidOperationException("Duplicate Character religion state."); _items.Add(state.CharacterId, state); }
        public void Set(CharacterReligionState state) { if (state == null) throw new ArgumentNullException(nameof(state)); _items[state.CharacterId] = state; }
        public CharacterReligionState GetRequired(CharacterId id) { if (!_items.TryGetValue(id, out var item)) throw new KeyNotFoundException("Character religion state was not found."); return item; }
    }
    public readonly struct ReligionProfileTarget : IEquatable<ReligionProfileTarget>, IComparable<ReligionProfileTarget>
    {
        private ReligionProfileTarget(ReligionProfileTargetKind kind, string id) { Kind = kind; Id = id; }
        public ReligionProfileTargetKind Kind { get; } public string Id { get; }
        public static ReligionProfileTarget City(CityId id) { if (!id.IsValid) throw new ArgumentException("CityId is invalid.", nameof(id)); return new ReligionProfileTarget(ReligionProfileTargetKind.City, id.Value); }
        public static ReligionProfileTarget Faction(FactionId id) { if (!id.IsValid) throw new ArgumentException("FactionId is invalid.", nameof(id)); return new ReligionProfileTarget(ReligionProfileTargetKind.Faction, id.Value); }
        public static ReligionProfileTarget Region(RegionId id) { if (!id.IsValid) throw new ArgumentException("RegionId is invalid.", nameof(id)); return new ReligionProfileTarget(ReligionProfileTargetKind.Region, id.Value); }
        public int CompareTo(ReligionProfileTarget other) { var result = Kind.CompareTo(other.Kind); return result != 0 ? result : StringComparer.Ordinal.Compare(Id, other.Id); }
        public bool Equals(ReligionProfileTarget other) => Kind == other.Kind && StringComparer.Ordinal.Equals(Id, other.Id); public override bool Equals(object? obj) => obj is ReligionProfileTarget other && Equals(other); public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
    }
    public sealed class ReligionProfileEntry
    {
        public ReligionProfileEntry(ReligionId religionId, SectId? sectId, int relativePresence) { if (!religionId.IsValid) throw new ArgumentException("ReligionId is invalid.", nameof(religionId)); if (sectId.HasValue && !sectId.Value.IsValid) throw new ArgumentException("SectId is invalid.", nameof(sectId)); if (relativePresence <= 0) throw new ArgumentOutOfRangeException(nameof(relativePresence)); ReligionId = religionId; SectId = sectId; RelativePresence = relativePresence; }
        public ReligionId ReligionId { get; } public SectId? SectId { get; } public int RelativePresence { get; }
    }
    public sealed class ReligionProfile
    {
        private readonly List<ReligionProfileEntry> _entries = new List<ReligionProfileEntry>(); private readonly HashSet<string> _pairs = new HashSet<string>(StringComparer.Ordinal);
        public ReligionProfile(ReligionProfileTarget target) { if (string.IsNullOrWhiteSpace(target.Id) || !Enum.IsDefined(typeof(ReligionProfileTargetKind), target.Kind)) throw new ArgumentException("Target is invalid.", nameof(target)); Target = target; }
        public ReligionProfileTarget Target { get; } public IReadOnlyList<ReligionProfileEntry> Entries => _entries;
        public void Add(ReligionProfileEntry entry) { if (entry == null) throw new ArgumentNullException(nameof(entry)); var key = entry.ReligionId.Value + "\n" + (entry.SectId?.Value ?? string.Empty); if (!_pairs.Add(key)) throw new InvalidOperationException("Duplicate Religion profile entry."); _entries.Add(entry); _entries.Sort((a,b) => { var c=a.ReligionId.CompareTo(b.ReligionId); return c != 0 ? c : StringComparer.Ordinal.Compare(a.SectId?.Value ?? string.Empty, b.SectId?.Value ?? string.Empty); }); }
    }
    public sealed class ReligionPolicyRule
    {
        public ReligionPolicyRule(ReligionId religionId, SectId? sectId, ReligionRecognition recognition, ReligionTreatment treatment, ReligionEnforcement enforcement) { if (!religionId.IsValid) throw new ArgumentException("ReligionId is invalid.", nameof(religionId)); if (sectId.HasValue && !sectId.Value.IsValid) throw new ArgumentException("SectId is invalid.", nameof(sectId)); if (!Enum.IsDefined(typeof(ReligionRecognition), recognition) || !Enum.IsDefined(typeof(ReligionTreatment), treatment) || !Enum.IsDefined(typeof(ReligionEnforcement), enforcement)) throw new ArgumentOutOfRangeException(nameof(recognition)); ReligionId=religionId; SectId=sectId; Recognition=recognition; Treatment=treatment; Enforcement=enforcement; }
        public ReligionId ReligionId { get; } public SectId? SectId { get; } public ReligionRecognition Recognition { get; } public ReligionTreatment Treatment { get; } public ReligionEnforcement Enforcement { get; }
    }
    public sealed class ReligionPolicy
    {
        private readonly List<ReligionPolicyRule> _rules = new List<ReligionPolicyRule>(); private readonly HashSet<string> _pairs = new HashSet<string>(StringComparer.Ordinal);
        public ReligionPolicy(ReligionProfileTarget target) { if (string.IsNullOrWhiteSpace(target.Id)||!Enum.IsDefined(typeof(ReligionProfileTargetKind),target.Kind)) throw new ArgumentException("Target is invalid.", nameof(target)); Target=target; }
        public ReligionProfileTarget Target { get; } public IReadOnlyList<ReligionPolicyRule> Rules => _rules;
        public void Add(ReligionPolicyRule rule) { if (rule == null) throw new ArgumentNullException(nameof(rule)); var key=rule.ReligionId.Value+"\n"+(rule.SectId?.Value??string.Empty); if(!_pairs.Add(key)) throw new InvalidOperationException("Duplicate Religion policy rule."); _rules.Add(rule); _rules.Sort((a,b)=> { var c=a.ReligionId.CompareTo(b.ReligionId); return c!=0?c:StringComparer.Ordinal.Compare(a.SectId?.Value??string.Empty,b.SectId?.Value??string.Empty); }); }
    }
    public sealed class ReligiousCliqueAssociation
    {
        public ReligiousCliqueAssociation(CliqueId cliqueId, ReligionId religionId, SectId? sectId = null) { if(!cliqueId.IsValid) throw new ArgumentException("CliqueId is invalid.",nameof(cliqueId)); if(!religionId.IsValid) throw new ArgumentException("ReligionId is invalid.",nameof(religionId)); if(sectId.HasValue&&!sectId.Value.IsValid)throw new ArgumentException("SectId is invalid.",nameof(sectId)); CliqueId=cliqueId; ReligionId=religionId; SectId=sectId; }
        public CliqueId CliqueId { get; } public ReligionId ReligionId { get; } public SectId? SectId { get; }
    }
    public sealed class ReligiousCliqueAssociationRegistry
    {
        private readonly SortedDictionary<CliqueId,ReligiousCliqueAssociation> _items=new SortedDictionary<CliqueId,ReligiousCliqueAssociation>();
        public IReadOnlyCollection<ReligiousCliqueAssociation> OrderedAssociations=>_items.Values;
        public void Add(ReligiousCliqueAssociation association){if(association==null)throw new ArgumentNullException(nameof(association));if(_items.ContainsKey(association.CliqueId))throw new InvalidOperationException("Duplicate Religious Clique association.");_items.Add(association.CliqueId,association);}
    }
    public sealed class ReligionCampaignState
    {
        private readonly SortedDictionary<ReligionProfileTarget, ReligionProfile> _profiles=new SortedDictionary<ReligionProfileTarget, ReligionProfile>(); private readonly SortedDictionary<ReligionProfileTarget,ReligionPolicy> _policies=new SortedDictionary<ReligionProfileTarget,ReligionPolicy>();
        public ReligionCampaignState(ReligionRegistry? definitions=null, CharacterReligionRegistry? characters=null,ReligiousCliqueAssociationRegistry? cliqueAssociations=null) { Definitions=definitions??new ReligionRegistry(); Characters=characters??new CharacterReligionRegistry(); CliqueAssociations=cliqueAssociations??new ReligiousCliqueAssociationRegistry(); }
        public ReligionRegistry Definitions { get; } public CharacterReligionRegistry Characters { get; } public ReligiousCliqueAssociationRegistry CliqueAssociations{get;} public IReadOnlyCollection<ReligionProfile> OrderedProfiles=>_profiles.Values; public IReadOnlyCollection<ReligionPolicy> OrderedPolicies=>_policies.Values; public IReadOnlyCollection<ReligiousCliqueAssociation> OrderedCliqueAssociations=>CliqueAssociations.OrderedAssociations;
        public void AddProfile(ReligionProfile profile){if(profile==null)throw new ArgumentNullException(nameof(profile));if(_profiles.ContainsKey(profile.Target))throw new InvalidOperationException("Duplicate Religion profile target.");_profiles.Add(profile.Target,profile);} public void AddPolicy(ReligionPolicy policy){if(policy==null)throw new ArgumentNullException(nameof(policy));if(_policies.ContainsKey(policy.Target))throw new InvalidOperationException("Duplicate Religion policy target.");_policies.Add(policy.Target,policy);} public void AddCliqueAssociation(ReligiousCliqueAssociation association)=>CliqueAssociations.Add(association);
    }
    public sealed class ReligionDifferenceAssessment
    {
        public ReligionDifferenceAssessment(bool differs) { Differs=differs; } public bool Differs{get;} public bool AutomaticUnrest=>false; public int LoyaltyDelta=>0; public int RelationDelta=>0; public bool AutomaticRebellionCause=>false; public bool AutomaticWarCause=>false; public int CombatModifier=>0;
    }
}
