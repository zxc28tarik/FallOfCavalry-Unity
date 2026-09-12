using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Time;

namespace FOC.Domain.Houses
{
    public enum HouseLifecycle { Active, Extinguished, Dissolved }
    public enum FamilyLinkKind { BiologicalParent, BiologicalChild, Marriage }
    public enum HousePropertyKind { PrivateProperty, StateOffice, TimarDirlikServiceGrant }
    public enum InheritanceStatus { Pending, Resolved, RegrantRequired }

    public readonly struct PersonalWealth { public PersonalWealth(long value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; } public long Value { get; } }
    public readonly struct HouseWealth { public HouseWealth(long value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; } public long Value { get; } }
    public readonly struct StateTreasuryBalance { public StateTreasuryBalance(long value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; } public long Value { get; } }
    public readonly struct HousePrestige { public HousePrestige(int value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; } public int Value { get; } }
    public readonly struct HouseHead { public HouseHead(CharacterId characterId) { if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId)); CharacterId = characterId; } public CharacterId CharacterId { get; } }

    public sealed class HouseDefinition
    {
        public HouseDefinition(HouseId id, string name) { if (!id.IsValid) throw new ArgumentException("HouseId is invalid.", nameof(id)); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name)); Id = id; Name = name; }
        public HouseId Id { get; }
        public string Name { get; }
    }
    public sealed class HouseMember
    {
        public HouseMember(CharacterId characterId, WorldTimestamp joinedAt, bool isActive = true) { if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId)); CharacterId = characterId; JoinedAt = joinedAt; IsActive = isActive; }
        public CharacterId CharacterId { get; }
        public WorldTimestamp JoinedAt { get; }
        public bool IsActive { get; private set; }
        public void Leave() => IsActive = false;
    }
    public sealed class MarriageLink
    {
        public MarriageLink(CharacterId first, CharacterId second, WorldTimestamp startedAt, bool isActive = true) { if (!first.IsValid || !second.IsValid || first == second) throw new ArgumentException("Marriage endpoints must be distinct valid characters."); if (second < first) { var swap = first; first = second; second = swap; } First = first; Second = second; StartedAt = startedAt; IsActive = isActive; }
        public CharacterId First { get; }
        public CharacterId Second { get; }
        public WorldTimestamp StartedAt { get; }
        public bool IsActive { get; }
        public bool CreatesAlliance => false;
    }
    public sealed class FamilyLink
    {
        public FamilyLink(CharacterId first, CharacterId second, FamilyLinkKind kind) { if (!first.IsValid || !second.IsValid || first == second) throw new ArgumentException("Family endpoints must be distinct valid characters."); if (!Enum.IsDefined(typeof(FamilyLinkKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind)); First = first; Second = second; Kind = kind; }
        public CharacterId First { get; }
        public CharacterId Second { get; }
        public FamilyLinkKind Kind { get; }
    }
    public sealed class HousePropertyRef
    {
        public HousePropertyRef(string assetId, HousePropertyKind kind) { if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset id is required.", nameof(assetId)); if (!Enum.IsDefined(typeof(HousePropertyKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind)); AssetId = assetId; Kind = kind; }
        public string AssetId { get; }
        public HousePropertyKind Kind { get; }
        public bool IsOrdinarilyInheritable => Kind == HousePropertyKind.PrivateProperty;
    }
    public sealed class InheritanceState
    {
        public InheritanceState(string assetId, HousePropertyKind kind, CharacterId? heir, InheritanceStatus status)
        {
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset id is required.", nameof(assetId));
            if (!Enum.IsDefined(typeof(HousePropertyKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(InheritanceStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (heir.HasValue && !heir.Value.IsValid) throw new ArgumentException("Heir is invalid.", nameof(heir));
            if ((kind == HousePropertyKind.StateOffice || kind == HousePropertyKind.TimarDirlikServiceGrant) && heir.HasValue) throw new InvalidOperationException("State office and Timar/Dirlik service grants cannot use ordinary automatic inheritance.");
            AssetId = assetId; Kind = kind; Heir = heir; Status = kind == HousePropertyKind.PrivateProperty ? status : InheritanceStatus.RegrantRequired;
        }
        public string AssetId { get; }
        public HousePropertyKind Kind { get; }
        public CharacterId? Heir { get; }
        public InheritanceStatus Status { get; }
    }
    public sealed class HouseState
    {
        private readonly SortedDictionary<CharacterId, HouseMember> _members = new SortedDictionary<CharacterId, HouseMember>();
        private readonly List<MarriageLink> _marriages = new List<MarriageLink>();
        private readonly List<FamilyLink> _familyLinks = new List<FamilyLink>();
        private readonly List<HousePropertyRef> _properties = new List<HousePropertyRef>();
        private readonly List<InheritanceState> _inheritances = new List<InheritanceState>();
        public HouseState(HouseDefinition definition, int prestige = 0, long wealth = 0, HouseLifecycle lifecycle = HouseLifecycle.Active) { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); if (!Enum.IsDefined(typeof(HouseLifecycle), lifecycle)) throw new ArgumentOutOfRangeException(nameof(lifecycle)); Prestige = new HousePrestige(prestige); Wealth = new HouseWealth(wealth); Lifecycle = lifecycle; }
        public HouseDefinition Definition { get; }
        public HouseId Id => Definition.Id;
        public CharacterId? HeadId { get; private set; }
        public bool SuccessionPending { get; private set; }
        public HousePrestige Prestige { get; private set; }
        public HouseWealth Wealth { get; private set; }
        public HouseLifecycle Lifecycle { get; private set; }
        public IReadOnlyCollection<HouseMember> OrderedMembers => _members.Values;
        public IReadOnlyList<MarriageLink> Marriages => _marriages;
        public IReadOnlyList<FamilyLink> FamilyLinks => _familyLinks;
        public IReadOnlyList<HousePropertyRef> Properties => _properties;
        public IReadOnlyList<InheritanceState> Inheritances => _inheritances;
        public void AddMember(HouseMember member) { if (member == null) throw new ArgumentNullException(nameof(member)); if (_members.ContainsKey(member.CharacterId)) throw new InvalidOperationException("Duplicate House member."); _members.Add(member.CharacterId, member); }
        public void SetHead(CharacterId characterId, CharacterRoster characters) { if (!_members.TryGetValue(characterId, out var member) || !member.IsActive) throw new InvalidOperationException("House head must be an active member."); if (characters.GetRequired(characterId).IsDead) throw new InvalidOperationException("House head must be living."); HeadId = characterId; SuccessionPending = false; }
        public void MarkSuccessionPending() { HeadId = null; SuccessionPending = true; }
        public void AddMarriage(MarriageLink link) { _marriages.Add(link ?? throw new ArgumentNullException(nameof(link))); _marriages.Sort((a, b) => { var compared = a.First.CompareTo(b.First); return compared != 0 ? compared : a.Second.CompareTo(b.Second); }); }
        public void AddFamilyLink(FamilyLink link) { _familyLinks.Add(link ?? throw new ArgumentNullException(nameof(link))); _familyLinks.Sort((a, b) => { var compared = a.First.CompareTo(b.First); if (compared != 0) return compared; compared = a.Second.CompareTo(b.Second); return compared != 0 ? compared : a.Kind.CompareTo(b.Kind); }); }
        public void AddProperty(HousePropertyRef property) { _properties.Add(property ?? throw new ArgumentNullException(nameof(property))); _properties.Sort((a, b) => StringComparer.Ordinal.Compare(a.AssetId, b.AssetId)); }
        public void AddInheritance(InheritanceState inheritance) { _inheritances.Add(inheritance ?? throw new ArgumentNullException(nameof(inheritance))); _inheritances.Sort((a, b) => StringComparer.Ordinal.Compare(a.AssetId, b.AssetId)); }
        public void SetLifecycle(HouseLifecycle lifecycle) { if (!Enum.IsDefined(typeof(HouseLifecycle), lifecycle)) throw new ArgumentOutOfRangeException(nameof(lifecycle)); Lifecycle = lifecycle; }
    }
    public sealed class HouseRegistry
    {
        private readonly SortedDictionary<HouseId, HouseState> _items = new SortedDictionary<HouseId, HouseState>();
        public IReadOnlyCollection<HouseState> OrderedHouses => _items.Values;
        public int Count => _items.Count;
        public void Add(HouseState item) { if (item == null) throw new ArgumentNullException(nameof(item)); if (_items.ContainsKey(item.Id)) throw new InvalidOperationException("Duplicate HouseId."); _items.Add(item.Id, item); }
        public HouseState GetRequired(HouseId id) { if (!_items.TryGetValue(id, out var item)) throw new KeyNotFoundException("House was not found."); return item; }
    }
}
