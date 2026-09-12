using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Time;

namespace FOC.Domain.Organizations
{
    public enum OrganizationBranch { Party, Household, Army, Settlements, Estates, Production, Trade, Diplomacy }
    public enum OrganizationMembershipType { Full, Temporary, Salaried, Contact, Protected, Contract, Volunteer }
    public enum AssignmentAuthority { LowOfficial, Responsible, Manager, Deputy, FullAuthority }
    public enum AssignmentStatus { Active, Completed, Cancelled }
    public enum AssignmentPresence { PhysicalPresenceRequired, RemoteCapable }
    public enum AssignmentTargetKind { Organization, City, Army, Caravan, House, Clique, WorldPosition }

    public sealed class AssignmentTarget : IEquatable<AssignmentTarget>
    {
        private AssignmentTarget(AssignmentTargetKind kind, string id, WorldPosition position)
        { Kind = kind; TargetId = id; Position = position; }
        public AssignmentTargetKind Kind { get; }
        public string TargetId { get; }
        public WorldPosition Position { get; }
        public static AssignmentTarget Organization(OrganizationId id) { Require(id.IsValid); return new AssignmentTarget(AssignmentTargetKind.Organization, id.Value, default); }
        public static AssignmentTarget City(CityId id) { Require(id.IsValid); return new AssignmentTarget(AssignmentTargetKind.City, id.Value, default); }
        public static AssignmentTarget Army(ArmyId id) { Require(id.IsValid); return new AssignmentTarget(AssignmentTargetKind.Army, id.Value, default); }
        public static AssignmentTarget Caravan(CaravanId id) { Require(id.IsValid); return new AssignmentTarget(AssignmentTargetKind.Caravan, id.Value, default); }
        public static AssignmentTarget House(HouseId id) { Require(id.IsValid); return new AssignmentTarget(AssignmentTargetKind.House, id.Value, default); }
        public static AssignmentTarget Clique(CliqueId id) { Require(id.IsValid); return new AssignmentTarget(AssignmentTargetKind.Clique, id.Value, default); }
        public static AssignmentTarget At(WorldPosition position) => new AssignmentTarget(AssignmentTargetKind.WorldPosition, string.Empty, position);
        public bool Equals(AssignmentTarget? other) => other != null && Kind == other.Kind && StringComparer.Ordinal.Equals(TargetId, other.TargetId) && Position.Equals(other.Position);
        public override bool Equals(object? obj) => Equals(obj as AssignmentTarget);
        public override int GetHashCode() => unchecked((((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(TargetId)) * 397 ^ Position.GetHashCode());
        private static void Require(bool valid) { if (!valid) throw new ArgumentException("Assignment target identifier is invalid."); }
    }

    public sealed class OrganizationMembershipState
    {
        public OrganizationMembershipState(CharacterId characterId, OrganizationBranch branch, OrganizationMembershipType membershipType, WorldTimestamp startedAt, bool isActive = true)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId));
            if (!Enum.IsDefined(typeof(OrganizationBranch), branch)) throw new ArgumentOutOfRangeException(nameof(branch));
            if (!Enum.IsDefined(typeof(OrganizationMembershipType), membershipType)) throw new ArgumentOutOfRangeException(nameof(membershipType));
            CharacterId = characterId; Branch = branch; MembershipType = membershipType; StartedAt = startedAt; IsActive = isActive;
        }
        public CharacterId CharacterId { get; }
        public OrganizationBranch Branch { get; }
        public OrganizationMembershipType MembershipType { get; }
        public WorldTimestamp StartedAt { get; }
        public bool IsActive { get; private set; }
        public void Deactivate() => IsActive = false;
    }

    public sealed class AssignmentState
    {
        public AssignmentState(AssignmentId id, CharacterId characterId, OrganizationBranch branch, string roleCode, AssignmentAuthority authority, AssignmentTarget target, AssignmentPresence presence, WorldTimestamp startedAt, AssignmentStatus status = AssignmentStatus.Active)
        {
            if (!id.IsValid) throw new ArgumentException("AssignmentId is invalid.", nameof(id));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is invalid.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(roleCode)) throw new ArgumentException("Controlled role code is required.", nameof(roleCode));
            if (!Enum.IsDefined(typeof(OrganizationBranch), branch)) throw new ArgumentOutOfRangeException(nameof(branch));
            if (!Enum.IsDefined(typeof(AssignmentAuthority), authority)) throw new ArgumentOutOfRangeException(nameof(authority));
            if (!Enum.IsDefined(typeof(AssignmentPresence), presence)) throw new ArgumentOutOfRangeException(nameof(presence));
            if (!Enum.IsDefined(typeof(AssignmentStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            Id = id; CharacterId = characterId; Branch = branch; RoleCode = roleCode; Authority = authority; Target = target ?? throw new ArgumentNullException(nameof(target)); Presence = presence; StartedAt = startedAt; Status = status;
        }
        public AssignmentId Id { get; }
        public CharacterId CharacterId { get; }
        public OrganizationBranch Branch { get; }
        public string RoleCode { get; }
        public AssignmentAuthority Authority { get; }
        public AssignmentTarget Target { get; }
        public AssignmentPresence Presence { get; }
        public WorldTimestamp StartedAt { get; }
        public AssignmentStatus Status { get; private set; }
        public bool IsActive => Status == AssignmentStatus.Active;
        public void Complete() => Status = AssignmentStatus.Completed;
        public void Cancel() => Status = AssignmentStatus.Cancelled;
    }

    public sealed class OrganizationState
    {
        private readonly List<OrganizationMembershipState> _memberships = new List<OrganizationMembershipState>();
        private readonly SortedDictionary<AssignmentId, AssignmentState> _assignments = new SortedDictionary<AssignmentId, AssignmentState>();
        public OrganizationState(OrganizationId id, string name) { if (!id.IsValid) throw new ArgumentException("OrganizationId is invalid.", nameof(id)); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name)); Id = id; Name = name; }
        public OrganizationId Id { get; }
        public string Name { get; }
        public IReadOnlyList<OrganizationMembershipState> Memberships => _memberships;
        public IReadOnlyCollection<AssignmentState> OrderedAssignments => _assignments.Values;
        public void AddMembership(OrganizationMembershipState membership)
        {
            if (membership == null) throw new ArgumentNullException(nameof(membership));
            foreach (var existing in _memberships) if (existing.CharacterId == membership.CharacterId && existing.Branch == membership.Branch && existing.IsActive && membership.IsActive) throw new InvalidOperationException("Duplicate active membership for branch.");
            _memberships.Add(membership);
            _memberships.Sort((left, right) => { var compared = left.CharacterId.CompareTo(right.CharacterId); if (compared != 0) return compared; compared = left.Branch.CompareTo(right.Branch); if (compared != 0) return compared; return left.StartedAt.Ticks.CompareTo(right.StartedAt.Ticks); });
        }
        public void AddAssignment(AssignmentState assignment)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            if (_assignments.ContainsKey(assignment.Id)) throw new InvalidOperationException("Duplicate AssignmentId.");
            if (assignment.IsActive && assignment.Presence == AssignmentPresence.PhysicalPresenceRequired)
                foreach (var existing in _assignments.Values) if (existing.IsActive && existing.CharacterId == assignment.CharacterId && existing.Presence == AssignmentPresence.PhysicalPresenceRequired && !existing.Target.Equals(assignment.Target)) throw new InvalidOperationException("Character cannot hold active physical-presence assignments at different targets.");
            _assignments.Add(assignment.Id, assignment);
        }
    }

    public sealed class OrganizationRegistry
    {
        private readonly SortedDictionary<OrganizationId, OrganizationState> _items = new SortedDictionary<OrganizationId, OrganizationState>();
        public IReadOnlyCollection<OrganizationState> OrderedOrganizations => _items.Values;
        public int Count => _items.Count;
        public void Add(OrganizationState item) { if (item == null) throw new ArgumentNullException(nameof(item)); if (_items.ContainsKey(item.Id)) throw new InvalidOperationException("Duplicate OrganizationId."); _items.Add(item.Id, item); }
        public OrganizationState GetRequired(OrganizationId id) { if (!_items.TryGetValue(id, out var item)) throw new KeyNotFoundException("Organization was not found."); return item; }
    }

    public static class AssignmentFeasibilityRules
    {
        public static bool IsPhysicalTargetSatisfied(CharacterLocation location, AssignmentTarget target)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (target == null) throw new ArgumentNullException(nameof(target));
            switch (target.Kind)
            {
                case AssignmentTargetKind.City: return location.Kind == CharacterLocationKind.City && location.CityId!.Value.Value == target.TargetId;
                case AssignmentTargetKind.Army: return location.Kind == CharacterLocationKind.Army && location.ArmyId!.Value.Value == target.TargetId;
                case AssignmentTargetKind.Caravan: return location.Kind == CharacterLocationKind.Caravan && location.CaravanId!.Value.Value == target.TargetId;
                case AssignmentTargetKind.WorldPosition: return (location.Kind == CharacterLocationKind.WorldPosition || location.Kind == CharacterLocationKind.Travelling) && location.Position.Equals(target.Position);
                default: return true;
            }
        }
    }
}
