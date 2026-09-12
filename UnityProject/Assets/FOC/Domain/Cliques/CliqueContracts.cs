using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Time;

namespace FOC.Domain.Cliques
{
    public enum CliqueType { Merchant, Military, Religious, GuildCraft, UrbanNotables, PatronageHouseholdCircle, TribalNomadic }
    public enum CliqueLifecycle { Active, Inactive, Dissolved }
    public enum CliqueAttitude { Hostile, Opposed, Neutral, Supportive, Loyal }
    public enum CliquePressure { None, Protest, CreditWithdrawal, TradeSlowdown, TaxResistance, SupplyRefusal, AppointmentResistance, LegitimacyCriticism, RivalSupport, MilitaryCooperationRefusal }
    public enum InfluenceSourceKind { MemberWealth, MemberOffice, MemberReputation, MemberNetwork }
    public sealed class CliqueDefinition
    {
        public CliqueDefinition(CliqueId id, string name, CliqueType type) { if (!id.IsValid) throw new ArgumentException("CliqueId is invalid.", nameof(id)); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name)); if (!Enum.IsDefined(typeof(CliqueType), type)) throw new ArgumentOutOfRangeException(nameof(type)); Id = id; Name = name; Type = type; }
        public CliqueId Id { get; }
        public string Name { get; }
        public CliqueType Type { get; }
    }
    public sealed class CliqueMembership
    {
        public CliqueMembership(CharacterId characterId, string roleCode, WorldTimestamp joinedAt, bool isActive = true) { if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId)); if (string.IsNullOrWhiteSpace(roleCode)) throw new ArgumentException("Role code is required.", nameof(roleCode)); CharacterId = characterId; RoleCode = roleCode; JoinedAt = joinedAt; IsActive = isActive; }
        public CharacterId CharacterId { get; }
        public string RoleCode { get; }
        public WorldTimestamp JoinedAt { get; }
        public bool IsActive { get; }
    }
    public sealed class CliqueInfluenceSourceState
    {
        public CliqueInfluenceSourceState(CharacterId characterId, InfluenceSourceKind kind, int contribution) { if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId)); if (!Enum.IsDefined(typeof(InfluenceSourceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind)); if (contribution < 0 || contribution > 100) throw new ArgumentOutOfRangeException(nameof(contribution)); CharacterId = characterId; Kind = kind; Contribution = contribution; }
        public CharacterId CharacterId { get; }
        public InfluenceSourceKind Kind { get; }
        public int Contribution { get; }
    }
    public sealed class RebellionReadiness
    {
        public RebellionReadiness(bool hasPower, bool hasCause) { HasPower = hasPower; HasCause = hasCause; }
        public bool HasPower { get; }
        public bool HasCause { get; }
        public bool IsReady => HasPower && HasCause;
    }
    public sealed class CliqueState
    {
        private readonly SortedDictionary<CharacterId, CliqueMembership> _members = new SortedDictionary<CharacterId, CliqueMembership>();
        private readonly List<CliqueInfluenceSourceState> _sources = new List<CliqueInfluenceSourceState>();
        public CliqueState(CliqueDefinition definition, CliqueLifecycle lifecycle = CliqueLifecycle.Active, CliqueAttitude attitude = CliqueAttitude.Neutral, CliqueId? parentId = null) { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); if (!Enum.IsDefined(typeof(CliqueLifecycle), lifecycle)) throw new ArgumentOutOfRangeException(nameof(lifecycle)); if (!Enum.IsDefined(typeof(CliqueAttitude), attitude)) throw new ArgumentOutOfRangeException(nameof(attitude)); if (parentId.HasValue && (!parentId.Value.IsValid || parentId.Value.Equals(definition.Id))) throw new ArgumentException("Parent CliqueId is invalid.", nameof(parentId)); Lifecycle = lifecycle; Attitude = attitude; ParentId = parentId; }
        public CliqueDefinition Definition { get; }
        public CliqueId Id => Definition.Id;
        public CliqueLifecycle Lifecycle { get; private set; }
        public CliqueAttitude Attitude { get; }
        public CliqueId? ParentId { get; }
        public CharacterId? LeaderId { get; private set; }
        public IReadOnlyCollection<CliqueMembership> OrderedMemberships => _members.Values;
        public IReadOnlyList<CliqueInfluenceSourceState> InfluenceSources => _sources;
        public int Influence { get { var total = 0; foreach (var source in _sources) total += source.Contribution; return total > 100 ? 100 : total; } }
        public bool CanOwnGoods => false;
        public bool CanOwnSoldiers => false;
        public bool CanOwnArmy => false;
        public int CombatBonus => 0;
        public void AddMembership(CliqueMembership membership) { if (membership == null) throw new ArgumentNullException(nameof(membership)); if (_members.ContainsKey(membership.CharacterId)) throw new InvalidOperationException("Duplicate Clique membership."); _members.Add(membership.CharacterId, membership); }
        public void SetLeader(CharacterId? leaderId) { if (leaderId.HasValue && (!_members.TryGetValue(leaderId.Value, out var member) || !member.IsActive)) throw new InvalidOperationException("Clique leader must be an active member."); LeaderId = leaderId; }
        public void AddInfluenceSource(CliqueInfluenceSourceState source) { if (source == null) throw new ArgumentNullException(nameof(source)); if (!_members.ContainsKey(source.CharacterId)) throw new InvalidOperationException("Influence source must belong to a Clique member."); _sources.Add(source); _sources.Sort((a, b) => { var compared = a.CharacterId.CompareTo(b.CharacterId); return compared != 0 ? compared : a.Kind.CompareTo(b.Kind); }); }
        public void SetLifecycle(CliqueLifecycle lifecycle) { if (!Enum.IsDefined(typeof(CliqueLifecycle), lifecycle)) throw new ArgumentOutOfRangeException(nameof(lifecycle)); Lifecycle = lifecycle; }
    }
    public sealed class CliqueRegistry
    {
        private readonly SortedDictionary<CliqueId, CliqueState> _items = new SortedDictionary<CliqueId, CliqueState>();
        public IReadOnlyCollection<CliqueState> OrderedCliques => _items.Values;
        public int Count => _items.Count;
        public void Add(CliqueState item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item)); if (_items.ContainsKey(item.Id)) throw new InvalidOperationException("Duplicate CliqueId.");
            CliqueState? parent = null;
            if (item.ParentId.HasValue && !_items.TryGetValue(item.ParentId.Value, out parent)) throw new InvalidOperationException("Parent Clique must already exist.");
            if (parent != null && parent.ParentId.HasValue) throw new InvalidOperationException("Clique hierarchy cannot exceed parent-to-subclique depth.");
            _items.Add(item.Id, item);
        }
        public CliqueState GetRequired(CliqueId id) { if (!_items.TryGetValue(id, out var item)) throw new KeyNotFoundException("Clique was not found."); return item; }
    }
    public sealed class PoliticalReactionProjection
    {
        public PoliticalReactionProjection(CliqueId cliqueId, int support, CliquePressure pressure, RebellionReadiness rebellion) { if (!cliqueId.IsValid) throw new ArgumentException("CliqueId is invalid.", nameof(cliqueId)); if (support < -100 || support > 100) throw new ArgumentOutOfRangeException(nameof(support)); CliqueId = cliqueId; Support = support; Pressure = pressure; Rebellion = rebellion ?? throw new ArgumentNullException(nameof(rebellion)); }
        public CliqueId CliqueId { get; }
        public int Support { get; }
        public CliquePressure Pressure { get; }
        public RebellionReadiness Rebellion { get; }
    }
}
