using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.AI;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Time;

namespace FOC.Application.AI
{
    public interface IAIDecisionCandidateProvider { IEnumerable<AIActionCandidate> Generate(AIDecisionContext context); }
    public interface IAIEligibilityPolicy { AIEligibilityResult Evaluate(AIDecisionContext context, AIActionCandidate candidate); }
    public interface IAIUtilityPolicy { IEnumerable<AIUtilityContribution> Evaluate(AIDecisionContext context, AIActionCandidate candidate); }
    public interface IAITravelActionProvider { bool CanExecute(AIDecisionOwnerRef owner, AITargetRef target, WorldTimestamp at); }

    public readonly struct AIEligibilityResult
    {
        private AIEligibilityResult(bool allowed, string reason) { Allowed = allowed; Reason = reason; }
        public bool Allowed { get; }
        public string Reason { get; }
        public static AIEligibilityResult Allow() => new AIEligibilityResult(true, "allowed");
        public static AIEligibilityResult Reject(string reason) { if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(nameof(reason)); return new AIEligibilityResult(false, reason); }
    }

    public sealed class AIRejectedCandidate
    {
        public AIRejectedCandidate(AIActionCandidate candidate, string reason) { Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate)); Reason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException(nameof(reason)) : reason; }
        public AIActionCandidate Candidate { get; }
        public string Reason { get; }
    }

    public sealed class AIEvaluatedCandidate
    {
        public AIEvaluatedCandidate(AIActionCandidate candidate, IEnumerable<AIUtilityContribution> contributions, long utility)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Contributions = (contributions ?? throw new ArgumentNullException(nameof(contributions))).OrderBy(x => x.FactorId).ThenBy(x => x.ReasonKey, StringComparer.Ordinal).ToList(); Utility = utility;
        }
        public AIActionCandidate Candidate { get; }
        public IReadOnlyList<AIUtilityContribution> Contributions { get; }
        public long Utility { get; }
    }

    public sealed class AIDecisionTrace
    {
        public AIDecisionTrace(AIDecisionOwnerRef owner, WorldTimestamp at, AIDecisionQualityProfileId qualityProfileId, IEnumerable<AIRejectedCandidate> rejected, IEnumerable<AIEvaluatedCandidate> evaluated, AICandidateId? selectedCandidateId, string tieBreakReason, IEnumerable<AIKnownObservation> information)
        {
            Owner = owner; At = at; QualityProfileId = qualityProfileId;
            Rejected = (rejected ?? throw new ArgumentNullException(nameof(rejected))).OrderBy(x => x.Candidate.Id).ToList();
            Evaluated = (evaluated ?? throw new ArgumentNullException(nameof(evaluated))).OrderBy(x => x.Candidate.Id).ToList();
            SelectedCandidateId = selectedCandidateId; TieBreakReason = tieBreakReason ?? string.Empty;
            Information = (information ?? throw new ArgumentNullException(nameof(information))).Select(x => new AIInformationTrace(x.ReportId, x.Subject, x.Quality, x.Precision, x.ObservedAt, x.ArrivedAt, x.StalenessAt(at))).ToList();
        }
        public AIDecisionOwnerRef Owner { get; }
        public WorldTimestamp At { get; }
        public AIDecisionQualityProfileId QualityProfileId { get; }
        public IReadOnlyList<AIRejectedCandidate> Rejected { get; }
        public IReadOnlyList<AIEvaluatedCandidate> Evaluated { get; }
        public AICandidateId? SelectedCandidateId { get; }
        public string TieBreakReason { get; }
        public IReadOnlyList<AIInformationTrace> Information { get; }
    }

    public sealed class AIInformationTrace
    {
        public AIInformationTrace(ReportId reportId, ReportSubjectRef subject, ReportQuality quality, ObservationPrecision precision, WorldTimestamp observedAt, WorldTimestamp arrivedAt, long staleness) { ReportId = reportId; Subject = subject; Quality = quality; Precision = precision; ObservedAt = observedAt; ArrivedAt = arrivedAt; Staleness = staleness; }
        public ReportId ReportId { get; }
        public ReportSubjectRef Subject { get; }
        public ReportQuality Quality { get; }
        public ObservationPrecision Precision { get; }
        public WorldTimestamp ObservedAt { get; }
        public WorldTimestamp ArrivedAt { get; }
        public long Staleness { get; }
    }

    public sealed class AIDecisionResult
    {
        public AIDecisionResult(AIActionProposal? proposal, AIDecisionTrace trace) { Proposal = proposal; Trace = trace ?? throw new ArgumentNullException(nameof(trace)); }
        public AIActionProposal? Proposal { get; }
        public AIDecisionTrace Trace { get; }
    }

    public sealed class AIDecisionEngine
    {
        public AIDecisionResult Decide(AIDecisionContext context, IEnumerable<AIActionCandidate> candidates, IEnumerable<IAIEligibilityPolicy> eligibilityPolicies, IEnumerable<IAIUtilityPolicy> utilityPolicies, AIPriorityProfile priority, CharacterAIDecisionProfile? personality, AIDecisionQualityProfile quality)
        {
            if (context == null || priority == null || quality == null) throw new ArgumentNullException();
            var eligibility = (eligibilityPolicies ?? throw new ArgumentNullException(nameof(eligibilityPolicies))).ToList();
            var utility = (utilityPolicies ?? throw new ArgumentNullException(nameof(utilityPolicies))).ToList();
            var ordered = (candidates ?? throw new ArgumentNullException(nameof(candidates))).OrderBy(x => x.Id).ToList();
            if (ordered.Any(x => x == null) || ordered.Select(x => x.Id).Distinct().Count() != ordered.Count) throw new InvalidOperationException("AI candidates are null or duplicated.");
            var rejected = new List<AIRejectedCandidate>(); var legal = new List<AIActionCandidate>();
            foreach (var candidate in ordered)
            {
                string? reason = null;
                foreach (var policy in eligibility) { var result = policy.Evaluate(context, candidate); if (!result.Allowed) { reason = result.Reason; break; } }
                if (reason == null) legal.Add(candidate); else rejected.Add(new AIRejectedCandidate(candidate, reason));
            }
            var considered = legal.Take(quality.CandidateBreadth).ToList();
            foreach (var omitted in legal.Skip(quality.CandidateBreadth)) rejected.Add(new AIRejectedCandidate(omitted, "decision-quality-candidate-budget"));
            var evaluated = new List<AIEvaluatedCandidate>();
            foreach (var candidate in considered)
            {
                var raw = utility.SelectMany(x => x.Evaluate(context, candidate) ?? throw new InvalidOperationException("AI utility policy returned null."))
                    .OrderBy(x => x.FactorId).ThenBy(x => x.ReasonKey, StringComparer.Ordinal).Take(quality.ContributionBreadth).ToList();
                var composed = new List<AIUtilityContribution>(); long score = 0;
                checked
                {
                    foreach (var value in raw)
                    {
                        var total = value.Value + priority.Adjustment(value.FactorId) + (personality?.Adjustment(value.FactorId) ?? 0);
                        score += total;
                        composed.Add(new AIUtilityContribution(value.FactorId, total, value.ReasonKey, value.EvidenceReportId));
                    }
                }
                evaluated.Add(new AIEvaluatedCandidate(candidate, composed, score));
            }
            var selected = evaluated.OrderByDescending(x => x.Utility).ThenBy(x => x.Candidate.Id).FirstOrDefault();
            var tied = selected != null && evaluated.Count(x => x.Utility == selected.Utility) > 1;
            var tieReason = selected == null ? "no-legal-evaluated-candidate" : tied ? "stable-candidate-id" : "highest-utility";
            var proposal = selected == null ? null : new AIActionProposal(context.Owner, selected.Candidate, context.Now, selected.Contributions, selected.Utility);
            return new AIDecisionResult(proposal, new AIDecisionTrace(context.Owner, context.Now, quality.Id, rejected, evaluated, proposal?.Candidate.Id, tieReason, context.OrderedKnownForeignInformation));
        }
    }

    public sealed class AIPerceptionContextBuilder
    {
        private readonly CampaignRuntimeState _campaign;
        public AIPerceptionContextBuilder(CampaignRuntimeState campaign) { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); }
        public AIDecisionContext BuildFaction(FactionId factionId, IEnumerable<AIOwnFact>? ownFacts = null)
        {
            _campaign.Diplomacy.Actors.GetRequired(factionId);
            return new AIDecisionContext(AIDecisionOwnerRef.Faction(factionId), _campaign.Clock.Now, ownFacts ?? Array.Empty<AIOwnFact>(), KnownFor(factionId));
        }
        public AIDecisionContext BuildCharacter(CharacterId characterId, FactionId? informationActorId = null, IEnumerable<AIOwnFact>? additionalOwnFacts = null)
        {
            var character = _campaign.Characters.GetRequired(characterId);
            var own = CharacterFacts(character).Concat(additionalOwnFacts ?? Array.Empty<AIOwnFact>());
            var known = informationActorId.HasValue ? KnownFor(informationActorId.Value) : Array.Empty<AIKnownObservation>();
            return new AIDecisionContext(AIDecisionOwnerRef.Character(characterId), _campaign.Clock.Now, own, known);
        }
        private IEnumerable<AIKnownObservation> KnownFor(FactionId factionId)
        {
            var information = _campaign.Diplomacy.Information.GetRequired(factionId);
            foreach (var report in information.OrderedAvailableReports)
                foreach (var observation in report.OrderedObservations)
                    yield return new AIKnownObservation(report.Id, observation.Subject, observation.Kind, observation.Precision, report.Quality, report.DetailLevel, report.ObservedAt, report.ArrivedAt!.Value, observation.Lower, observation.Upper, observation.Qualitative);
        }
        private static IEnumerable<AIOwnFact> CharacterFacts(CharacterState character)
        {
            yield return new AIOwnFact("character.intelligence", character.Stats.Intelligence.Value, "character-own-state");
            yield return new AIOwnFact("character.observation", character.Stats.Observation.Value, "character-own-state");
            yield return new AIOwnFact("character.persuasion", character.Stats.Persuasion.Value, "character-own-state");
            yield return new AIOwnFact("character.leadership", character.Stats.Leadership.Value, "character-own-state");
            yield return new AIOwnFact("character.command", character.Stats.Command.Value, "character-own-state");
            yield return new AIOwnFact("character.trade", character.Stats.Trade.Value, "character-own-state");
            yield return new AIOwnFact("character.administration", character.Stats.Administration.Value, "character-own-state");
            yield return new AIOwnFact("character.courage", character.Stats.Courage.Value, "character-own-state");
            yield return new AIOwnFact("character.experience", character.Stats.Experience.Value, "character-own-state");
            yield return new AIOwnFact("character.loyalty", character.CurrentLoyalty.Value, "character-own-state");
            yield return new AIOwnFact("character.satisfaction", character.Satisfaction.Value, "character-own-state");
        }
    }

    public interface IAIScheduledDecisionSource
    {
        bool CanAggregate(AIControllerState controller, AISchedulingProfile profile);
        string AggregationKey(AIControllerState controller);
        AIDecisionContext BuildIndividual(AIControllerState controller, WorldTimestamp now);
        AIDecisionContext BuildShared(IReadOnlyList<AIControllerState> controllers, WorldTimestamp now);
        AIDecisionContext MaterializeShared(AIDecisionContext shared, AIDecisionOwnerRef owner);
    }

    public sealed class AISchedulerMetrics
    {
        public int Controllers { get; internal set; }
        public int ContextBuilds { get; internal set; }
        public int CandidateGenerations { get; internal set; }
        public int UtilityEvaluations { get; internal set; }
        public int SharedOrAggregatedEvaluations { get; internal set; }
        public int SelectedActions { get; internal set; }
    }

    public sealed class AISchedulerRun
    {
        public AISchedulerRun(IEnumerable<AIDecisionResult> results, AISchedulerMetrics metrics) { Results = results.ToList(); Metrics = metrics; }
        public IReadOnlyList<AIDecisionResult> Results { get; }
        public AISchedulerMetrics Metrics { get; }
    }

    public sealed class AIDecisionScheduler
    {
        private readonly AIDecisionEngine _engine;
        public AIDecisionScheduler(AIDecisionEngine? engine = null) { _engine = engine ?? new AIDecisionEngine(); }
        public AISchedulerRun Run(WorldTimestamp now, AICampaignState state, AIDefinitionCatalog definitions, IAIScheduledDecisionSource source, IAIDecisionCandidateProvider candidates, IEnumerable<IAIEligibilityPolicy> eligibility, IEnumerable<IAIUtilityPolicy> utility)
        {
            if (state == null || definitions == null || source == null || candidates == null) throw new ArgumentNullException();
            var due = state.Controllers.OrderedControllers.Where(x => x.IsDue(now)).OrderBy(x => x.Owner).ThenBy(x => x.Id).ToList();
            var metrics = new AISchedulerMetrics { Controllers = due.Count }; var results = new List<AIDecisionResult>();
            var individual = due.Where(x => !source.CanAggregate(x, definitions.Scheduling(x.SchedulingProfileId))).ToList();
            foreach (var controller in individual)
            {
                var context = source.BuildIndividual(controller, now); metrics.ContextBuilds++;
                var generated = candidates.Generate(context).OrderBy(x => x.Id).ToList(); metrics.CandidateGenerations++;
                var result = Evaluate(controller, context, generated, definitions, eligibility, utility); metrics.UtilityEvaluations += result.Trace.Evaluated.Count; if (result.Proposal != null) metrics.SelectedActions++; results.Add(result);
                ScheduleNext(controller, definitions.Scheduling(controller.SchedulingProfileId), now);
            }
            var groups = due.Except(individual).GroupBy(x => x.SchedulingProfileId.Value + "\n" + source.AggregationKey(x), StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var profile = definitions.Scheduling(group.First().SchedulingProfileId); var ordered = group.OrderBy(x => x.Owner).ThenBy(x => x.Id).ToList();
                for (var offset = 0; offset < ordered.Count; offset += profile.AggregationBatchSize)
                {
                    var batch = ordered.Skip(offset).Take(profile.AggregationBatchSize).ToList();
                    var shared = source.BuildShared(batch, now); metrics.ContextBuilds++; var sharedCandidates = candidates.Generate(shared).OrderBy(x => x.Id).ToList(); metrics.CandidateGenerations++; metrics.SharedOrAggregatedEvaluations += batch.Count;
                    foreach (var controller in batch)
                    {
                        var context = source.MaterializeShared(shared, controller.Owner);
                        var result = Evaluate(controller, context, sharedCandidates, definitions, eligibility, utility); metrics.UtilityEvaluations += result.Trace.Evaluated.Count; if (result.Proposal != null) metrics.SelectedActions++; results.Add(result);
                        ScheduleNext(controller, profile, now);
                    }
                }
            }
            return new AISchedulerRun(results.OrderBy(x => x.Trace.Owner).ToList(), metrics);
        }
        public AISchedulerRun RunImportantSequential(WorldTimestamp now, AICampaignState state, AIDefinitionCatalog definitions, IAIScheduledDecisionSource source, IAIDecisionCandidateProvider candidates, IEnumerable<IAIEligibilityPolicy> eligibility, IEnumerable<IAIUtilityPolicy> utility, Action<AIActionProposal> execute)
        {
            if (state == null || definitions == null || source == null || candidates == null || execute == null) throw new ArgumentNullException();
            var due = state.Controllers.OrderedControllers.Where(x => x.IsDue(now)).OrderBy(x => x.Owner).ThenBy(x => x.Id).ToList();
            if (due.Any(x => source.CanAggregate(x, definitions.Scheduling(x.SchedulingProfileId)))) throw new InvalidOperationException("Sequential execution is reserved for individually planned important actors.");
            var metrics = new AISchedulerMetrics { Controllers = due.Count }; var results = new List<AIDecisionResult>();
            foreach (var controller in due)
            {
                var context = source.BuildIndividual(controller, now); metrics.ContextBuilds++;
                var generated = candidates.Generate(context).OrderBy(x => x.Id).ToList(); metrics.CandidateGenerations++;
                var result = Evaluate(controller, context, generated, definitions, eligibility, utility); metrics.UtilityEvaluations += result.Trace.Evaluated.Count;
                if (result.Proposal != null) { execute(result.Proposal); metrics.SelectedActions++; }
                results.Add(result); ScheduleNext(controller, definitions.Scheduling(controller.SchedulingProfileId), now);
            }
            return new AISchedulerRun(results, metrics);
        }
        private AIDecisionResult Evaluate(AIControllerState controller, AIDecisionContext context, IReadOnlyList<AIActionCandidate> candidates, AIDefinitionCatalog definitions, IEnumerable<IAIEligibilityPolicy> eligibility, IEnumerable<IAIUtilityPolicy> utility)
        {
            if (!context.Owner.Equals(controller.Owner)) throw new InvalidOperationException("AI decision context owner does not match controller.");
            var personality = controller.CharacterProfileId.HasValue ? definitions.Character(controller.CharacterProfileId.Value) : null;
            return _engine.Decide(context, candidates, eligibility, utility, definitions.Priority(controller.PriorityProfileId), personality, definitions.Quality(controller.QualityProfileId));
        }
        private static void ScheduleNext(AIControllerState controller, AISchedulingProfile profile, WorldTimestamp now) { controller.RecordDecision(now, new WorldTimestamp(checked(now.Ticks + profile.Cadence.Ticks)), controller.RandomState); }
    }
}
