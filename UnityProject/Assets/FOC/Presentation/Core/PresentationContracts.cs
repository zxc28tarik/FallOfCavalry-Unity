#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FOC.Presentation.Core
{
    public enum PresentationScreenId
    {
        Map, City, Character, Organization, Trade, Army, Diplomacy, Battle,
        Reports, EncounterContract, Ledger, Archive
    }

    public enum PresentationEntityKind
    {
        None, City, Character, Organization, House, Clique, Caravan, Army,
        UnitGroup, Soldier, Faction, Battle, Report, Encounter, Contract, WorldLocation
    }

    public readonly struct PresentationEntityRef : IEquatable<PresentationEntityRef>, IComparable<PresentationEntityRef>
    {
        public PresentationEntityRef(PresentationEntityKind kind, string id)
        {
            if (kind == PresentationEntityKind.None || string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A typed presentation entity reference is required.");
            Kind = kind;
            Id = id;
        }

        public PresentationEntityKind Kind { get; }
        public string Id { get; }
        public int CompareTo(PresentationEntityRef other)
        {
            var kind = Kind.CompareTo(other.Kind);
            return kind != 0 ? kind : StringComparer.Ordinal.Compare(Id, other.Id);
        }
        public bool Equals(PresentationEntityRef other) => Kind == other.Kind && StringComparer.Ordinal.Equals(Id, other.Id);
        public override bool Equals(object? obj) => obj is PresentationEntityRef other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id ?? string.Empty));
        public override string ToString() => Kind + ":" + Id;
    }

    public enum PresentationAvailability
    {
        Available, Unknown, Unavailable, InsufficientHistory, NoComparablePeriod, Stale
    }

    public enum PresentationPrecision { Exact, Approximate, Range, Qualitative, Unknown }
    public enum PresentationTrendDirection { Rising, Falling, Stable, Unknown, Unavailable }
    public enum PresentationSeverity { Information, Caution, Warning, Critical }
    public enum PresentationConfirmationPolicy { None, ConfirmDestructive, ConfirmIrreversible }

    public sealed class PresentationKnowledge
    {
        public PresentationKnowledge(
            PresentationAvailability availability,
            PresentationPrecision precision,
            string sourceKey,
            long? observedAtTicks = null,
            long? arrivedAtTicks = null,
            long? stalenessTicks = null)
        {
            if (string.IsNullOrWhiteSpace(sourceKey)) throw new ArgumentException("Knowledge source key is required.", nameof(sourceKey));
            Availability = availability;
            Precision = precision;
            SourceKey = sourceKey;
            ObservedAtTicks = observedAtTicks;
            ArrivedAtTicks = arrivedAtTicks;
            StalenessTicks = stalenessTicks;
        }

        public PresentationAvailability Availability { get; }
        public PresentationPrecision Precision { get; }
        public string SourceKey { get; }
        public long? ObservedAtTicks { get; }
        public long? ArrivedAtTicks { get; }
        public long? StalenessTicks { get; }

        public static PresentationKnowledge ExactSelf { get; } =
            new PresentationKnowledge(PresentationAvailability.Available, PresentationPrecision.Exact, "presentation.knowledge.self");

        public static PresentationKnowledge Unknown { get; } =
            new PresentationKnowledge(PresentationAvailability.Unknown, PresentationPrecision.Unknown, "presentation.knowledge.unknown");
    }

    public sealed class PresentationField
    {
        public PresentationField(string labelKey, string displayValue, PresentationKnowledge knowledge, PresentationEntityRef? link = null, string? tooltipKey = null)
        {
            if (string.IsNullOrWhiteSpace(labelKey)) throw new ArgumentException("Field label key is required.", nameof(labelKey));
            LabelKey = labelKey;
            DisplayValue = displayValue ?? string.Empty;
            Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
            Link = link;
            TooltipKey = tooltipKey ?? string.Empty;
        }

        public string LabelKey { get; }
        public string DisplayValue { get; }
        public PresentationKnowledge Knowledge { get; }
        public PresentationEntityRef? Link { get; }
        public string TooltipKey { get; }
    }

    public sealed class PresentationFactor
    {
        public PresentationFactor(string labelKey, string displayValue, string sourceKey, PresentationKnowledge knowledge, string explanationKey = "")
        {
            if (string.IsNullOrWhiteSpace(labelKey) || string.IsNullOrWhiteSpace(sourceKey)) throw new ArgumentException("Factor keys are required.");
            LabelKey = labelKey;
            DisplayValue = displayValue ?? string.Empty;
            SourceKey = sourceKey;
            Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
            ExplanationKey = explanationKey ?? string.Empty;
        }
        public string LabelKey { get; }
        public string DisplayValue { get; }
        public string SourceKey { get; }
        public PresentationKnowledge Knowledge { get; }
        public string ExplanationKey { get; }
    }

    public sealed class PresentationTrend
    {
        public PresentationTrend(PresentationTrendDirection direction, PresentationAvailability availability, string explanationKey)
        {
            if (string.IsNullOrWhiteSpace(explanationKey)) throw new ArgumentException("Trend explanation key is required.", nameof(explanationKey));
            Direction = direction;
            Availability = availability;
            ExplanationKey = explanationKey;
        }
        public PresentationTrendDirection Direction { get; }
        public PresentationAvailability Availability { get; }
        public string ExplanationKey { get; }
        public static PresentationTrend InsufficientHistory { get; } =
            new PresentationTrend(PresentationTrendDirection.Unavailable, PresentationAvailability.InsufficientHistory, "presentation.trend.insufficient-history");
    }

    public sealed class PresentationAttentionItem
    {
        public PresentationAttentionItem(string labelKey, PresentationSeverity severity, string reasonKey, PresentationEntityRef affectedEntity, string? actionId = null)
        {
            if (string.IsNullOrWhiteSpace(labelKey) || string.IsNullOrWhiteSpace(reasonKey)) throw new ArgumentException("Attention item keys are required.");
            LabelKey = labelKey;
            Severity = severity;
            ReasonKey = reasonKey;
            AffectedEntity = affectedEntity;
            ActionId = actionId ?? string.Empty;
        }
        public string LabelKey { get; }
        public PresentationSeverity Severity { get; }
        public string ReasonKey { get; }
        public PresentationEntityRef AffectedEntity { get; }
        public string ActionId { get; }
    }

    public sealed class PresentationActionDescriptor
    {
        public PresentationActionDescriptor(
            string actionId,
            string labelKey,
            bool isEnabled,
            string disabledReasonKey,
            PresentationEntityRef target,
            PresentationConfirmationPolicy confirmationPolicy,
            string commandAdapterId)
        {
            if (string.IsNullOrWhiteSpace(actionId) || string.IsNullOrWhiteSpace(labelKey) || string.IsNullOrWhiteSpace(commandAdapterId))
                throw new ArgumentException("Presentation action identity is required.");
            if (!isEnabled && string.IsNullOrWhiteSpace(disabledReasonKey))
                throw new ArgumentException("A disabled action must explain why it is unavailable.", nameof(disabledReasonKey));
            ActionId = actionId;
            LabelKey = labelKey;
            IsEnabled = isEnabled;
            DisabledReasonKey = disabledReasonKey ?? string.Empty;
            Target = target;
            ConfirmationPolicy = confirmationPolicy;
            CommandAdapterId = commandAdapterId;
        }
        public string ActionId { get; }
        public string LabelKey { get; }
        public bool IsEnabled { get; }
        public string DisabledReasonKey { get; }
        public PresentationEntityRef Target { get; }
        public PresentationConfirmationPolicy ConfirmationPolicy { get; }
        public string CommandAdapterId { get; }
    }

    public sealed class PresentationSection
    {
        private readonly IReadOnlyList<PresentationField> _fields;
        public PresentationSection(string headingKey, PresentationAvailability availability, IEnumerable<PresentationField>? fields = null, string unavailableReasonKey = "")
        {
            if (string.IsNullOrWhiteSpace(headingKey)) throw new ArgumentException("Section heading key is required.", nameof(headingKey));
            if (availability != PresentationAvailability.Available && string.IsNullOrWhiteSpace(unavailableReasonKey))
                throw new ArgumentException("Unavailable sections need an explicit reason key.", nameof(unavailableReasonKey));
            HeadingKey = headingKey;
            Availability = availability;
            UnavailableReasonKey = unavailableReasonKey ?? string.Empty;
            _fields = (fields ?? Array.Empty<PresentationField>()).ToList().AsReadOnly();
        }
        public string HeadingKey { get; }
        public PresentationAvailability Availability { get; }
        public string UnavailableReasonKey { get; }
        public IReadOnlyList<PresentationField> Fields => _fields;

        public static PresentationSection Unavailable(string headingKey, string reasonKey, PresentationAvailability availability = PresentationAvailability.Unavailable) =>
            new PresentationSection(headingKey, availability, null, reasonKey);
    }

    public sealed class ScreenPresentationState
    {
        private readonly IReadOnlyList<PresentationSection> _details;
        private readonly IReadOnlyList<PresentationFactor> _why;
        private readonly IReadOnlyList<PresentationAttentionItem> _risks;
        private readonly IReadOnlyList<PresentationAttentionItem> _opportunities;
        private readonly IReadOnlyList<PresentationActionDescriptor> _actions;
        private readonly IReadOnlyList<PresentationAttentionItem> _alerts;

        public ScreenPresentationState(
            PresentationScreenId screenId,
            string titleKey,
            PresentationEntityRef? subject,
            PresentationSection current,
            PresentationTrend trend,
            PresentationAvailability whyAvailability,
            IEnumerable<PresentationFactor>? why,
            string whyUnavailableReasonKey,
            IEnumerable<PresentationAttentionItem>? risks,
            IEnumerable<PresentationAttentionItem>? opportunities,
            IEnumerable<PresentationActionDescriptor>? actions,
            IEnumerable<PresentationAttentionItem>? alerts = null,
            IEnumerable<PresentationSection>? details = null,
            MapPresentationSnapshot? map = null)
        {
            if (string.IsNullOrWhiteSpace(titleKey)) throw new ArgumentException("Screen title key is required.", nameof(titleKey));
            if (whyAvailability != PresentationAvailability.Available && string.IsNullOrWhiteSpace(whyUnavailableReasonKey))
                throw new ArgumentException("Unavailable Why requires an explicit reason.", nameof(whyUnavailableReasonKey));
            ScreenId = screenId;
            TitleKey = titleKey;
            Subject = subject;
            Current = current ?? throw new ArgumentNullException(nameof(current));
            Trend = trend ?? throw new ArgumentNullException(nameof(trend));
            WhyAvailability = whyAvailability;
            WhyUnavailableReasonKey = whyUnavailableReasonKey ?? string.Empty;
            _why = (why ?? Array.Empty<PresentationFactor>()).ToList().AsReadOnly();
            _risks = (risks ?? Array.Empty<PresentationAttentionItem>()).ToList().AsReadOnly();
            _opportunities = (opportunities ?? Array.Empty<PresentationAttentionItem>()).ToList().AsReadOnly();
            _actions = (actions ?? Array.Empty<PresentationActionDescriptor>()).ToList().AsReadOnly();
            _alerts = (alerts ?? Array.Empty<PresentationAttentionItem>()).ToList().AsReadOnly();
            _details = (details ?? Array.Empty<PresentationSection>()).ToList().AsReadOnly();
            Map = map;
        }

        public PresentationScreenId ScreenId { get; }
        public string TitleKey { get; }
        public PresentationEntityRef? Subject { get; }
        public PresentationSection Current { get; }
        public PresentationTrend Trend { get; }
        public PresentationAvailability WhyAvailability { get; }
        public string WhyUnavailableReasonKey { get; }
        public IReadOnlyList<PresentationFactor> Why => _why;
        public IReadOnlyList<PresentationAttentionItem> Risks => _risks;
        public IReadOnlyList<PresentationAttentionItem> Opportunities => _opportunities;
        public IReadOnlyList<PresentationActionDescriptor> Actions => _actions;
        public IReadOnlyList<PresentationAttentionItem> Alerts => _alerts;
        public IReadOnlyList<PresentationSection> Details => _details;
        public MapPresentationSnapshot? Map { get; }
    }

    public abstract class PresentationReadModel
    {
        protected PresentationReadModel(ScreenPresentationState state) { State = state ?? throw new ArgumentNullException(nameof(state)); }
        public ScreenPresentationState State { get; }
    }

    public sealed class MapReadModel : PresentationReadModel { public MapReadModel(ScreenPresentationState state, MapPresentationSnapshot map) : base(state) { Map = map ?? throw new ArgumentNullException(nameof(map)); } public MapPresentationSnapshot Map { get; } public IReadOnlyList<MapMarkerPresentation> Markers => Map.Markers; }
    public sealed class CityReadModel : PresentationReadModel { public CityReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class CharacterReadModel : PresentationReadModel { public CharacterReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class OrganizationReadModel : PresentationReadModel { public OrganizationReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class TradeReadModel : PresentationReadModel { public TradeReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class ArmyReadModel : PresentationReadModel { public ArmyReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class DiplomacyReadModel : PresentationReadModel { public DiplomacyReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class BattleReadModel : PresentationReadModel { public BattleReadModel(ScreenPresentationState state) : base(state) { } }
    public sealed class ReportsReadModel : PresentationReadModel { public ReportsReadModel(ScreenPresentationState state, IReadOnlyList<ReportRowPresentation> reports) : base(state) { Reports = reports ?? throw new ArgumentNullException(nameof(reports)); } public IReadOnlyList<ReportRowPresentation> Reports { get; } }
    public sealed class EncounterContractReadModel : PresentationReadModel { public EncounterContractReadModel(ScreenPresentationState state) : base(state) { } }

    public sealed class MapMarkerPresentation
    {
        public MapMarkerPresentation(PresentationEntityRef entity, string labelKey, float normalizedX, float normalizedY, PresentationKnowledge knowledge, bool proofOnly, PresentationEntityRef? navigationTarget = null)
        {
            if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1) throw new ArgumentOutOfRangeException(nameof(normalizedX));
            Entity = entity; LabelKey = labelKey ?? throw new ArgumentNullException(nameof(labelKey)); NormalizedX = normalizedX; NormalizedY = normalizedY; Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge)); ProofOnly = proofOnly; NavigationTarget = navigationTarget;
        }
        public PresentationEntityRef Entity { get; }
        public string LabelKey { get; }
        public float NormalizedX { get; }
        public float NormalizedY { get; }
        public PresentationKnowledge Knowledge { get; }
        public bool ProofOnly { get; }
        public PresentationEntityRef? NavigationTarget { get; }
    }

    public sealed class MapRoutePresentation
    {
        public MapRoutePresentation(string routeId,float firstX,float firstY,float secondX,float secondY,string modeKey,bool active){RouteId=routeId??throw new ArgumentNullException(nameof(routeId));FirstX=firstX;FirstY=firstY;SecondX=secondX;SecondY=secondY;ModeKey=modeKey??throw new ArgumentNullException(nameof(modeKey));Active=active;}
        public string RouteId{get;}public float FirstX{get;}public float FirstY{get;}public float SecondX{get;}public float SecondY{get;}public string ModeKey{get;}public bool Active{get;}
    }
    public sealed class MapJourneyPresentation
    {
        public MapJourneyPresentation(string journeyId,string actorLabel,string originLabel,string destinationLabel,int segmentIndex,int segmentCount,long segmentElapsedTicks){JourneyId=journeyId;ActorLabel=actorLabel;OriginLabel=originLabel;DestinationLabel=destinationLabel;SegmentIndex=segmentIndex;SegmentCount=segmentCount;SegmentElapsedTicks=segmentElapsedTicks;}
        public string JourneyId{get;}public string ActorLabel{get;}public string OriginLabel{get;}public string DestinationLabel{get;}public int SegmentIndex{get;}public int SegmentCount{get;}public long SegmentElapsedTicks{get;}
    }
    public sealed class MapPresentationSnapshot
    {
        public MapPresentationSnapshot(IReadOnlyList<MapMarkerPresentation> markers,IReadOnlyList<MapRoutePresentation> routes,IReadOnlyList<MapJourneyPresentation> journeys){Markers=markers??throw new ArgumentNullException(nameof(markers));Routes=routes??throw new ArgumentNullException(nameof(routes));Journeys=journeys??throw new ArgumentNullException(nameof(journeys));}
        public IReadOnlyList<MapMarkerPresentation> Markers{get;}public IReadOnlyList<MapRoutePresentation> Routes{get;}public IReadOnlyList<MapJourneyPresentation> Journeys{get;}
    }

    public sealed class ReportRowPresentation
    {
        public ReportRowPresentation(PresentationEntityRef report, string typeKey, string sourceKey, long observedAt, long? arrivedAt, long staleness, string qualityKey, string precisionKey, string detailKey)
        { Report = report; TypeKey = typeKey; SourceKey = sourceKey; ObservedAt = observedAt; ArrivedAt = arrivedAt; Staleness = staleness; QualityKey = qualityKey; PrecisionKey = precisionKey; DetailKey = detailKey; }
        public PresentationEntityRef Report { get; }
        public string TypeKey { get; }
        public string SourceKey { get; }
        public long ObservedAt { get; }
        public long? ArrivedAt { get; }
        public long Staleness { get; }
        public string QualityKey { get; }
        public string PrecisionKey { get; }
        public string DetailKey { get; }
    }
}
