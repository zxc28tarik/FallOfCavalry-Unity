using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Domain.AI
{
    public enum AIDecisionOwnerKind { Character, Faction }
    public enum AIDecisionDomain { Diplomacy, EconomyTrade, MilitaryLogistics, InternalCityManagement, EncounterContract, BattleTactical }
    public enum AITargetKind { Character, Faction, City, Army, Caravan, Clique, Battle, Encounter, Contract, DeploymentGroup, BattleSector, Region }
    public enum AIPlanLifecycle { Active, Completed, Invalidated, Cancelled }
    public enum AIControllerLifecycle { Active, Suspended, Retired }
    public enum AIReconsiderationReason { Scheduled, Completed, Invalidated, PrerequisiteLost, InformationChanged }

    public readonly struct AIDecisionOwnerRef : IEquatable<AIDecisionOwnerRef>, IComparable<AIDecisionOwnerRef>
    {
        private AIDecisionOwnerRef(AIDecisionOwnerKind kind, string id)
        {
            if (!Enum.IsDefined(typeof(AIDecisionOwnerKind), kind) || string.IsNullOrWhiteSpace(id)) throw new ArgumentException("AI decision owner is invalid.");
            Kind = kind; Id = id;
        }
        public AIDecisionOwnerKind Kind { get; }
        public string Id { get; }
        public static AIDecisionOwnerRef Character(CharacterId id) => new AIDecisionOwnerRef(AIDecisionOwnerKind.Character, Need(id.IsValid, id.Value));
        public static AIDecisionOwnerRef Faction(FactionId id) => new AIDecisionOwnerRef(AIDecisionOwnerKind.Faction, Need(id.IsValid, id.Value));
        internal static AIDecisionOwnerRef Restore(AIDecisionOwnerKind kind, string id) => new AIDecisionOwnerRef(kind, id);
        public int CompareTo(AIDecisionOwnerRef other) { var c = Kind.CompareTo(other.Kind); return c != 0 ? c : StringComparer.Ordinal.Compare(Id, other.Id); }
        public bool Equals(AIDecisionOwnerRef other) => Kind == other.Kind && StringComparer.Ordinal.Equals(Id, other.Id);
        public override bool Equals(object? obj) => obj is AIDecisionOwnerRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id);
        private static string Need(bool valid, string value) { if (!valid) throw new ArgumentException("Typed owner ID is invalid."); return value; }
    }

    public readonly struct AITargetRef : IEquatable<AITargetRef>, IComparable<AITargetRef>
    {
        private AITargetRef(AITargetKind kind, string id)
        {
            if (!Enum.IsDefined(typeof(AITargetKind), kind) || string.IsNullOrWhiteSpace(id)) throw new ArgumentException("AI target is invalid.");
            Kind = kind; Id = id;
        }
        public AITargetKind Kind { get; }
        public string Id { get; }
        public static AITargetRef Character(CharacterId id) => New(AITargetKind.Character, id.IsValid, id.Value);
        public static AITargetRef Faction(FactionId id) => New(AITargetKind.Faction, id.IsValid, id.Value);
        public static AITargetRef City(CityId id) => New(AITargetKind.City, id.IsValid, id.Value);
        public static AITargetRef Army(ArmyId id) => New(AITargetKind.Army, id.IsValid, id.Value);
        public static AITargetRef Caravan(CaravanId id) => New(AITargetKind.Caravan, id.IsValid, id.Value);
        public static AITargetRef Clique(CliqueId id) => New(AITargetKind.Clique, id.IsValid, id.Value);
        public static AITargetRef Battle(BattleId id) => New(AITargetKind.Battle, id.IsValid, id.Value);
        public static AITargetRef Encounter(EncounterId id) => New(AITargetKind.Encounter, id.IsValid, id.Value);
        public static AITargetRef Contract(ContractId id) => New(AITargetKind.Contract, id.IsValid, id.Value);
        public static AITargetRef DeploymentGroup(DeploymentGroupId id) => New(AITargetKind.DeploymentGroup, id.IsValid, id.Value);
        public static AITargetRef BattleSector(BattleSectorId id) => New(AITargetKind.BattleSector, id.IsValid, id.Value);
        public static AITargetRef Region(RegionId id) => New(AITargetKind.Region, id.IsValid, id.Value);
        internal static AITargetRef Restore(AITargetKind kind, string id) => new AITargetRef(kind, id);
        private static AITargetRef New(AITargetKind kind, bool valid, string id) { if (!valid) throw new ArgumentException("Typed target ID is invalid."); return new AITargetRef(kind, id); }
        public int CompareTo(AITargetRef other) { var c = Kind.CompareTo(other.Kind); return c != 0 ? c : StringComparer.Ordinal.Compare(Id, other.Id); }
        public bool Equals(AITargetRef other) => Kind == other.Kind && StringComparer.Ordinal.Equals(Id, other.Id);
        public override bool Equals(object? obj) => obj is AITargetRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id);
    }

    public sealed class AIOwnFact
    {
        public AIOwnFact(string key, long value, string provenance)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(provenance)) throw new ArgumentException("AI own fact is invalid.");
            Key = key; Value = value; Provenance = provenance;
        }
        public string Key { get; }
        public long Value { get; }
        public string Provenance { get; }
    }

    public sealed class AIKnownObservation
    {
        public AIKnownObservation(ReportId reportId, ReportSubjectRef subject, ReportObservationKind kind, ObservationPrecision precision, ReportQuality quality, ReportDetailLevel detail, WorldTimestamp observedAt, WorldTimestamp arrivedAt, long? lower = null, long? upper = null, QualitativeObservation qualitative = QualitativeObservation.Unknown)
        {
            if (!reportId.IsValid || subject.Equals(default(ReportSubjectRef)) || !Enum.IsDefined(typeof(ReportObservationKind), kind) || !Enum.IsDefined(typeof(ObservationPrecision), precision) || !Enum.IsDefined(typeof(ReportQuality), quality) || !Enum.IsDefined(typeof(ReportDetailLevel), detail) || arrivedAt.Ticks < observedAt.Ticks) throw new ArgumentException("Known observation is invalid.");
            ReportId = reportId; Subject = subject; Kind = kind; Precision = precision; Quality = quality; Detail = detail; ObservedAt = observedAt; ArrivedAt = arrivedAt; Lower = lower; Upper = upper; Qualitative = qualitative;
        }
        public ReportId ReportId { get; }
        public ReportSubjectRef Subject { get; }
        public ReportObservationKind Kind { get; }
        public ObservationPrecision Precision { get; }
        public ReportQuality Quality { get; }
        public ReportDetailLevel Detail { get; }
        public WorldTimestamp ObservedAt { get; }
        public WorldTimestamp ArrivedAt { get; }
        public long? Lower { get; }
        public long? Upper { get; }
        public QualitativeObservation Qualitative { get; }
        public long StalenessAt(WorldTimestamp now) { if (now.Ticks < ObservedAt.Ticks) throw new ArgumentOutOfRangeException(nameof(now)); return now.Ticks - ObservedAt.Ticks; }
    }

    public sealed class AIDecisionContext
    {
        private readonly SortedDictionary<string, AIOwnFact> _ownFacts = new SortedDictionary<string, AIOwnFact>(StringComparer.Ordinal);
        private readonly List<AIKnownObservation> _known = new List<AIKnownObservation>();
        public AIDecisionContext(AIDecisionOwnerRef owner, WorldTimestamp now, IEnumerable<AIOwnFact> ownFacts, IEnumerable<AIKnownObservation> knownForeignInformation)
        {
            Owner = owner; Now = now;
            foreach (var fact in ownFacts ?? throw new ArgumentNullException(nameof(ownFacts))) { if (fact == null || _ownFacts.ContainsKey(fact.Key)) throw new InvalidOperationException("AI own fact is null or duplicated."); _ownFacts.Add(fact.Key, fact); }
            foreach (var observation in knownForeignInformation ?? throw new ArgumentNullException(nameof(knownForeignInformation))) { if (observation == null) throw new ArgumentException("Known observation is null."); _known.Add(observation); }
            _known.Sort(CompareObservation);
        }
        public AIDecisionOwnerRef Owner { get; }
        public WorldTimestamp Now { get; }
        public IReadOnlyCollection<AIOwnFact> OrderedOwnFacts => _ownFacts.Values;
        public IReadOnlyList<AIKnownObservation> OrderedKnownForeignInformation => _known;
        public bool TryGetOwnFact(string key, out AIOwnFact fact) => _ownFacts.TryGetValue(key, out fact!);
        private static int CompareObservation(AIKnownObservation a, AIKnownObservation b) { var c = a.ReportId.CompareTo(b.ReportId); if (c != 0) return c; c = a.Subject.Kind.CompareTo(b.Subject.Kind); if (c != 0) return c; c = StringComparer.Ordinal.Compare(a.Subject.Id, b.Subject.Id); return c != 0 ? c : a.Kind.CompareTo(b.Kind); }
    }

    public sealed class AIActionCandidate
    {
        public AIActionCandidate(AICandidateId id, AIDecisionDomain domain, AIActionPolicyId policyId, string contentKey, AITargetRef? target = null)
        {
            if (!id.IsValid || !Enum.IsDefined(typeof(AIDecisionDomain), domain) || !policyId.IsValid || string.IsNullOrWhiteSpace(contentKey)) throw new ArgumentException("AI candidate is invalid.");
            Id = id; Domain = domain; PolicyId = policyId; ContentKey = contentKey; Target = target;
        }
        public AICandidateId Id { get; }
        public AIDecisionDomain Domain { get; }
        public AIActionPolicyId PolicyId { get; }
        public string ContentKey { get; }
        public AITargetRef? Target { get; }
    }

    public sealed class AIUtilityContribution
    {
        public AIUtilityContribution(AIUtilityFactorId factorId, long value, string reasonKey, ReportId? evidenceReportId = null)
        {
            if (!factorId.IsValid || string.IsNullOrWhiteSpace(reasonKey)) throw new ArgumentException("AI utility contribution is invalid.");
            FactorId = factorId; Value = value; ReasonKey = reasonKey; EvidenceReportId = evidenceReportId;
        }
        public AIUtilityFactorId FactorId { get; }
        public long Value { get; }
        public string ReasonKey { get; }
        public ReportId? EvidenceReportId { get; }
    }

    public sealed class AIProfileWeight
    {
        public AIProfileWeight(AIUtilityFactorId factorId, long additiveValue)
        {
            if (!factorId.IsValid) throw new ArgumentException(nameof(factorId));
            FactorId = factorId; AdditiveValue = additiveValue;
        }
        public AIUtilityFactorId FactorId { get; }
        public long AdditiveValue { get; }
    }

    public sealed class AIPriorityProfile
    {
        private readonly SortedDictionary<AIUtilityFactorId, long> _weights = new SortedDictionary<AIUtilityFactorId, long>();
        public AIPriorityProfile(AIPriorityProfileId id, IEnumerable<AIProfileWeight> weights, string contentKey)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(contentKey)) throw new ArgumentException("AI priority profile is invalid."); Id = id; ContentKey = contentKey;
            Add(weights);
        }
        public AIPriorityProfileId Id { get; }
        public string ContentKey { get; }
        public IReadOnlyDictionary<AIUtilityFactorId, long> OrderedWeights => _weights;
        public long Adjustment(AIUtilityFactorId id) => _weights.TryGetValue(id, out var value) ? value : 0;
        private void Add(IEnumerable<AIProfileWeight> values) { foreach (var value in values ?? throw new ArgumentNullException(nameof(values))) if (value == null || _weights.ContainsKey(value.FactorId)) throw new InvalidOperationException("AI profile weight is null or duplicated."); else _weights.Add(value.FactorId, value.AdditiveValue); }
    }

    public sealed class CharacterAIDecisionProfile
    {
        private readonly SortedDictionary<AIUtilityFactorId, long> _weights = new SortedDictionary<AIUtilityFactorId, long>();
        public CharacterAIDecisionProfile(CharacterAIProfileId id, IEnumerable<AIProfileWeight> weights, string contentKey)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(contentKey)) throw new ArgumentException("Character AI profile is invalid."); Id = id; ContentKey = contentKey;
            foreach (var value in weights ?? throw new ArgumentNullException(nameof(weights))) if (value == null || _weights.ContainsKey(value.FactorId)) throw new InvalidOperationException("Character AI profile weight is null or duplicated."); else _weights.Add(value.FactorId, value.AdditiveValue);
        }
        public CharacterAIProfileId Id { get; }
        public string ContentKey { get; }
        public IReadOnlyDictionary<AIUtilityFactorId, long> OrderedWeights => _weights;
        public long Adjustment(AIUtilityFactorId id) => _weights.TryGetValue(id, out var value) ? value : 0;
    }

    public sealed class AIDecisionQualityProfile
    {
        public AIDecisionQualityProfile(AIDecisionQualityProfileId id, int candidateBreadth, int contributionBreadth, int planningDepth, string contentKey)
        {
            if (!id.IsValid || candidateBreadth <= 0 || contributionBreadth <= 0 || planningDepth <= 0 || string.IsNullOrWhiteSpace(contentKey)) throw new ArgumentException("AI decision-quality profile is invalid.");
            Id = id; CandidateBreadth = candidateBreadth; ContributionBreadth = contributionBreadth; PlanningDepth = planningDepth; ContentKey = contentKey;
        }
        public AIDecisionQualityProfileId Id { get; }
        public int CandidateBreadth { get; }
        public int ContributionBreadth { get; }
        public int PlanningDepth { get; }
        public string ContentKey { get; }
    }

    public sealed class AISchedulingProfile
    {
        private readonly SortedSet<CharacterImportance> _aggregatedImportance = new SortedSet<CharacterImportance>();
        public AISchedulingProfile(AISchedulingProfileId id, WorldDuration cadence, int aggregationBatchSize, IEnumerable<CharacterImportance> aggregatedImportance, string contentKey)
        {
            if (!id.IsValid || cadence.Ticks <= 0 || aggregationBatchSize <= 0 || string.IsNullOrWhiteSpace(contentKey)) throw new ArgumentException("AI scheduling profile is invalid.");
            Id = id; Cadence = cadence; AggregationBatchSize = aggregationBatchSize; ContentKey = contentKey;
            foreach (var value in aggregatedImportance ?? throw new ArgumentNullException(nameof(aggregatedImportance))) if (!Enum.IsDefined(typeof(CharacterImportance), value) || !_aggregatedImportance.Add(value)) throw new InvalidOperationException("Aggregated importance is invalid or duplicated.");
        }
        public AISchedulingProfileId Id { get; }
        public WorldDuration Cadence { get; }
        public int AggregationBatchSize { get; }
        public string ContentKey { get; }
        public IReadOnlyCollection<CharacterImportance> OrderedAggregatedImportance => _aggregatedImportance;
        public bool UsesAggregation(CharacterImportance importance) => _aggregatedImportance.Contains(importance);
    }

    public sealed class AIDefinitionCatalog
    {
        private readonly SortedDictionary<AIPriorityProfileId, AIPriorityProfile> _priorities = new SortedDictionary<AIPriorityProfileId, AIPriorityProfile>();
        private readonly SortedDictionary<CharacterAIProfileId, CharacterAIDecisionProfile> _characters = new SortedDictionary<CharacterAIProfileId, CharacterAIDecisionProfile>();
        private readonly SortedDictionary<AIDecisionQualityProfileId, AIDecisionQualityProfile> _quality = new SortedDictionary<AIDecisionQualityProfileId, AIDecisionQualityProfile>();
        private readonly SortedDictionary<AISchedulingProfileId, AISchedulingProfile> _scheduling = new SortedDictionary<AISchedulingProfileId, AISchedulingProfile>();
        public IReadOnlyCollection<AIPriorityProfile> OrderedPriorities => _priorities.Values;
        public IReadOnlyCollection<CharacterAIDecisionProfile> OrderedCharacterProfiles => _characters.Values;
        public IReadOnlyCollection<AIDecisionQualityProfile> OrderedQualityProfiles => _quality.Values;
        public IReadOnlyCollection<AISchedulingProfile> OrderedSchedulingProfiles => _scheduling.Values;
        public void Add(AIPriorityProfile value) { if (value == null || _priorities.ContainsKey(value.Id)) throw new InvalidOperationException("AI priority profile is null or duplicated."); _priorities.Add(value.Id, value); }
        public void Add(CharacterAIDecisionProfile value) { if (value == null || _characters.ContainsKey(value.Id)) throw new InvalidOperationException("Character AI profile is null or duplicated."); _characters.Add(value.Id, value); }
        public void Add(AIDecisionQualityProfile value) { if (value == null || _quality.ContainsKey(value.Id)) throw new InvalidOperationException("AI quality profile is null or duplicated."); _quality.Add(value.Id, value); }
        public void Add(AISchedulingProfile value) { if (value == null || _scheduling.ContainsKey(value.Id)) throw new InvalidOperationException("AI scheduling profile is null or duplicated."); _scheduling.Add(value.Id, value); }
        public AIPriorityProfile Priority(AIPriorityProfileId id) => Required(_priorities, id, "AI priority profile");
        public CharacterAIDecisionProfile Character(CharacterAIProfileId id) => Required(_characters, id, "Character AI profile");
        public AIDecisionQualityProfile Quality(AIDecisionQualityProfileId id) => Required(_quality, id, "AI quality profile");
        public AISchedulingProfile Scheduling(AISchedulingProfileId id) => Required(_scheduling, id, "AI scheduling profile");
        private static TValue Required<TKey, TValue>(SortedDictionary<TKey, TValue> values, TKey id, string name) where TKey : notnull { if (!values.TryGetValue(id, out var value)) throw new KeyNotFoundException(name + " was not found."); return value; }
    }

    public sealed class AIActionProposal
    {
        public AIActionProposal(AIDecisionOwnerRef owner, AIActionCandidate candidate, WorldTimestamp decidedAt, IEnumerable<AIUtilityContribution> contributions, long utility)
        {
            Owner = owner; Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate)); DecidedAt = decidedAt; Utility = utility;
            Contributions = (contributions ?? throw new ArgumentNullException(nameof(contributions))).OrderBy(x => x.FactorId).ThenBy(x => x.ReasonKey, StringComparer.Ordinal).ToList();
        }
        public AIDecisionOwnerRef Owner { get; }
        public AIActionCandidate Candidate { get; }
        public WorldTimestamp DecidedAt { get; }
        public IReadOnlyList<AIUtilityContribution> Contributions { get; }
        public long Utility { get; }
    }

    public sealed class AIPlanState
    {
        public AIPlanState(AIPlanId id, AIDecisionOwnerRef owner, AIGoalId goalId, AICandidateId candidateId, AIDecisionDomain domain, AIActionPolicyId policyId, WorldTimestamp createdAt, AITargetRef? target = null, WorldTimestamp? reconsiderAt = null, AIPlanLifecycle lifecycle = AIPlanLifecycle.Active, WorldTimestamp? terminalAt = null, AIReconsiderationReason? terminalReason = null)
        {
            if (!id.IsValid || !goalId.IsValid || !candidateId.IsValid || !Enum.IsDefined(typeof(AIDecisionDomain), domain) || !policyId.IsValid || !Enum.IsDefined(typeof(AIPlanLifecycle), lifecycle) || (reconsiderAt.HasValue && reconsiderAt.Value.Ticks <= createdAt.Ticks)) throw new ArgumentException("AI plan is invalid.");
            Id = id; Owner = owner; GoalId = goalId; CandidateId = candidateId; Domain = domain; PolicyId = policyId; CreatedAt = createdAt; Target = target; ReconsiderAt = reconsiderAt; Lifecycle = lifecycle; TerminalAt = terminalAt; TerminalReason = terminalReason; Validate();
        }
        public AIPlanId Id { get; }
        public AIDecisionOwnerRef Owner { get; }
        public AIGoalId GoalId { get; }
        public AICandidateId CandidateId { get; }
        public AIDecisionDomain Domain { get; }
        public AIActionPolicyId PolicyId { get; }
        public AITargetRef? Target { get; }
        public WorldTimestamp CreatedAt { get; }
        public WorldTimestamp? ReconsiderAt { get; }
        public AIPlanLifecycle Lifecycle { get; private set; }
        public WorldTimestamp? TerminalAt { get; private set; }
        public AIReconsiderationReason? TerminalReason { get; private set; }
        public void Complete(WorldTimestamp at) => End(AIPlanLifecycle.Completed, AIReconsiderationReason.Completed, at);
        public void Invalidate(AIReconsiderationReason reason, WorldTimestamp at) { if (reason == AIReconsiderationReason.Completed || reason == AIReconsiderationReason.Scheduled) throw new ArgumentException(nameof(reason)); End(AIPlanLifecycle.Invalidated, reason, at); }
        public void Cancel(WorldTimestamp at) => End(AIPlanLifecycle.Cancelled, AIReconsiderationReason.Invalidated, at);
        private void End(AIPlanLifecycle lifecycle, AIReconsiderationReason reason, WorldTimestamp at) { if (Lifecycle != AIPlanLifecycle.Active || at.Ticks < CreatedAt.Ticks) throw new InvalidOperationException("AI plan is terminal or timestamp is invalid."); Lifecycle = lifecycle; TerminalReason = reason; TerminalAt = at; }
        private void Validate() { if (Lifecycle == AIPlanLifecycle.Active && (TerminalAt.HasValue || TerminalReason.HasValue) || Lifecycle != AIPlanLifecycle.Active && (!TerminalAt.HasValue || !TerminalReason.HasValue || TerminalAt.Value.Ticks < CreatedAt.Ticks)) throw new ArgumentException("AI plan lifecycle is invalid."); }
    }

    public sealed class AIControllerState
    {
        public AIControllerState(AIControllerId id, AIDecisionOwnerRef owner, AIPriorityProfileId priorityProfileId, AIDecisionQualityProfileId qualityProfileId, AISchedulingProfileId schedulingProfileId, RandomState randomState, CharacterAIProfileId? characterProfileId = null, WorldTimestamp? lastDecisionAt = null, WorldTimestamp? nextDecisionAt = null, AIPlanState? currentPlan = null, AIControllerLifecycle lifecycle = AIControllerLifecycle.Active)
        {
            if (!id.IsValid || !priorityProfileId.IsValid || !qualityProfileId.IsValid || !schedulingProfileId.IsValid || randomState.State == 0 || !Enum.IsDefined(typeof(AIControllerLifecycle), lifecycle) || owner.Kind == AIDecisionOwnerKind.Character != characterProfileId.HasValue || (lastDecisionAt.HasValue && nextDecisionAt.HasValue && nextDecisionAt.Value.Ticks <= lastDecisionAt.Value.Ticks) || (currentPlan != null && !currentPlan.Owner.Equals(owner))) throw new ArgumentException("AI controller state is invalid.");
            Id = id; Owner = owner; PriorityProfileId = priorityProfileId; QualityProfileId = qualityProfileId; SchedulingProfileId = schedulingProfileId; CharacterProfileId = characterProfileId; RandomState = randomState; LastDecisionAt = lastDecisionAt; NextDecisionAt = nextDecisionAt; CurrentPlan = currentPlan; Lifecycle = lifecycle;
        }
        public AIControllerId Id { get; }
        public AIDecisionOwnerRef Owner { get; }
        public AIPriorityProfileId PriorityProfileId { get; }
        public CharacterAIProfileId? CharacterProfileId { get; }
        public AIDecisionQualityProfileId QualityProfileId { get; }
        public AISchedulingProfileId SchedulingProfileId { get; }
        public RandomState RandomState { get; private set; }
        public WorldTimestamp? LastDecisionAt { get; private set; }
        public WorldTimestamp? NextDecisionAt { get; private set; }
        public AIPlanState? CurrentPlan { get; private set; }
        public AIControllerLifecycle Lifecycle { get; private set; }
        public bool IsDue(WorldTimestamp now) => Lifecycle == AIControllerLifecycle.Active && (!NextDecisionAt.HasValue || NextDecisionAt.Value.Ticks <= now.Ticks);
        public void RecordDecision(WorldTimestamp at, WorldTimestamp nextAt, RandomState continuation) { if (Lifecycle != AIControllerLifecycle.Active || nextAt.Ticks <= at.Ticks || continuation.State == 0 || (LastDecisionAt.HasValue && at.Ticks < LastDecisionAt.Value.Ticks)) throw new InvalidOperationException("AI decision schedule update is invalid."); LastDecisionAt = at; NextDecisionAt = nextAt; RandomState = continuation; }
        public void SetPlan(AIPlanState plan) { if (plan == null || !plan.Owner.Equals(Owner) || plan.Lifecycle != AIPlanLifecycle.Active || CurrentPlan != null && CurrentPlan.Lifecycle == AIPlanLifecycle.Active) throw new InvalidOperationException("AI plan cannot be assigned."); CurrentPlan = plan; }
        public void Suspend() { if (Lifecycle != AIControllerLifecycle.Active) throw new InvalidOperationException(); Lifecycle = AIControllerLifecycle.Suspended; }
        public void Resume() { if (Lifecycle != AIControllerLifecycle.Suspended) throw new InvalidOperationException(); Lifecycle = AIControllerLifecycle.Active; }
        public void Retire() { if (Lifecycle == AIControllerLifecycle.Retired) throw new InvalidOperationException(); Lifecycle = AIControllerLifecycle.Retired; }
    }

    public sealed class AIDecisionRegistry
    {
        private readonly SortedDictionary<AIControllerId, AIControllerState> _controllers = new SortedDictionary<AIControllerId, AIControllerState>();
        private readonly SortedDictionary<AIDecisionOwnerRef, AIControllerId> _owners = new SortedDictionary<AIDecisionOwnerRef, AIControllerId>();
        public IReadOnlyCollection<AIControllerState> OrderedControllers => _controllers.Values;
        public void Add(AIControllerState value) { if (value == null || _controllers.ContainsKey(value.Id) || _owners.ContainsKey(value.Owner)) throw new InvalidOperationException("AI controller or owner is duplicated."); _controllers.Add(value.Id, value); _owners.Add(value.Owner, value.Id); }
        public AIControllerState GetRequired(AIControllerId id) { if (!_controllers.TryGetValue(id, out var value)) throw new KeyNotFoundException("AI controller was not found."); return value; }
        public AIControllerState GetByOwner(AIDecisionOwnerRef owner) { if (!_owners.TryGetValue(owner, out var id)) throw new KeyNotFoundException("AI decision owner was not found."); return _controllers[id]; }
        public bool ContainsOwner(AIDecisionOwnerRef owner) => _owners.ContainsKey(owner);
    }

    public sealed class AICampaignState
    {
        public AICampaignState(AIDecisionRegistry? controllers = null) { Controllers = controllers ?? new AIDecisionRegistry(); }
        public AIDecisionRegistry Controllers { get; }
    }
}
