using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Time;

namespace FOC.Domain.Geography
{
    public enum WorldLocationKind { Capital, City, Town, Waystation, Port, Crossing }
    public enum RouteMode { Road, Sea, Crossing }
    public enum HistoricalConfidence { High, Medium, Interpretative }
    public enum GeographyContentStatus { ProductionCandidate, SliceTuning }
    public enum TravelActorKind { Character, Army, Caravan, Envoy, Messenger }
    public enum TravelLifecycle { Active, Arrived, Cancelled }

    public readonly struct GeoCoordinateE6 : IEquatable<GeoCoordinateE6>
    {
        public GeoCoordinateE6(int latitudeE6, int longitudeE6)
        {
            if (latitudeE6 < -90000000 || latitudeE6 > 90000000 || longitudeE6 < -180000000 || longitudeE6 > 180000000) throw new ArgumentOutOfRangeException(nameof(latitudeE6));
            LatitudeE6 = latitudeE6; LongitudeE6 = longitudeE6;
        }
        public int LatitudeE6 { get; }
        public int LongitudeE6 { get; }
        public bool Equals(GeoCoordinateE6 other) => LatitudeE6 == other.LatitudeE6 && LongitudeE6 == other.LongitudeE6;
        public override bool Equals(object? obj) => obj is GeoCoordinateE6 other && Equals(other);
        public override int GetHashCode() => unchecked((LatitudeE6 * 397) ^ LongitudeE6);
    }

    /// <summary>Deterministic equirectangular map-space point in millionths. This, not floating point latitude/longitude, is simulation/UI authority.</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public const int Scale = 1000000;
        public MapPoint(int x, int y) { if (x < 0 || x > Scale || y < 0 || y > Scale) throw new ArgumentOutOfRangeException(nameof(x)); X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
        public bool Equals(MapPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is MapPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
    }

    public sealed class WorldLocationDefinition
    {
        private readonly IReadOnlyList<string> _aliases;
        private readonly IReadOnlyList<string> _sourceIds;
        public WorldLocationDefinition(WorldLocationId id, string displayName, WorldLocationKind kind, RegionId regionId, MapPoint mapPoint, GeoCoordinateE6 coordinate, IEnumerable<string> aliases, IEnumerable<string> sourceIds, HistoricalConfidence confidence, GeographyContentStatus status, CityId? cityId = null)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(displayName) || !regionId.IsValid || !Enum.IsDefined(typeof(WorldLocationKind), kind) || !Enum.IsDefined(typeof(HistoricalConfidence), confidence) || !Enum.IsDefined(typeof(GeographyContentStatus), status)) throw new ArgumentException("World location is invalid.");
            if (cityId.HasValue && !cityId.Value.IsValid) throw new ArgumentException("City reference is invalid.", nameof(cityId));
            Id = id; DisplayName = displayName; Kind = kind; RegionId = regionId; MapPoint = mapPoint; Coordinate = coordinate; CityId = cityId; Confidence = confidence; Status = status;
            _aliases = OrderedText(aliases, true); _sourceIds = OrderedText(sourceIds, false);
        }
        public WorldLocationId Id { get; }
        public string DisplayName { get; }
        public WorldLocationKind Kind { get; }
        public RegionId RegionId { get; }
        public CityId? CityId { get; }
        public MapPoint MapPoint { get; }
        public GeoCoordinateE6 Coordinate { get; }
        public HistoricalConfidence Confidence { get; }
        public GeographyContentStatus Status { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<string> SourceIds => _sourceIds;
        private static IReadOnlyList<string> OrderedText(IEnumerable<string> values, bool allowEmpty)
        {
            var list = (values ?? throw new ArgumentNullException(nameof(values))).Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!allowEmpty && list.Count == 0) throw new ArgumentException("At least one source identifier is required.");
            return list.AsReadOnly();
        }
    }

    public sealed class TravelRouteDefinition
    {
        private readonly IReadOnlyList<string> _sourceIds;
        public TravelRouteDefinition(TravelRouteId id, WorldLocationId first, WorldLocationId second, RouteMode mode, long distanceMeters, IEnumerable<string> sourceIds, HistoricalConfidence confidence, GeographyContentStatus status, bool bidirectional = true)
        {
            if (!id.IsValid || !first.IsValid || !second.IsValid || first.Equals(second) || distanceMeters <= 0 || !Enum.IsDefined(typeof(RouteMode), mode) || !Enum.IsDefined(typeof(HistoricalConfidence), confidence) || !Enum.IsDefined(typeof(GeographyContentStatus), status)) throw new ArgumentException("Travel route is invalid.");
            Id = id; First = first; Second = second; Mode = mode; DistanceMeters = distanceMeters; Confidence = confidence; Status = status; Bidirectional = bidirectional;
            _sourceIds = (sourceIds ?? throw new ArgumentNullException(nameof(sourceIds))).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList().AsReadOnly();
            if (_sourceIds.Count == 0) throw new ArgumentException("At least one source identifier is required.", nameof(sourceIds));
        }
        public TravelRouteId Id { get; }
        public WorldLocationId First { get; }
        public WorldLocationId Second { get; }
        public RouteMode Mode { get; }
        public long DistanceMeters { get; }
        public HistoricalConfidence Confidence { get; }
        public GeographyContentStatus Status { get; }
        public bool Bidirectional { get; }
        public IReadOnlyList<string> SourceIds => _sourceIds;
        public bool Connects(WorldLocationId id) => First.Equals(id) || Second.Equals(id);
        public WorldLocationId Other(WorldLocationId id) { if (First.Equals(id)) return Second; if (Second.Equals(id)) return First; throw new InvalidOperationException("Location is not on this route."); }
        public bool Allows(WorldLocationId from, WorldLocationId to) => First.Equals(from) && Second.Equals(to) || Bidirectional && Second.Equals(from) && First.Equals(to);
    }

    public sealed class WorldGeography
    {
        private readonly SortedDictionary<WorldLocationId, WorldLocationDefinition> _locations = new SortedDictionary<WorldLocationId, WorldLocationDefinition>();
        private readonly SortedDictionary<TravelRouteId, TravelRouteDefinition> _routes = new SortedDictionary<TravelRouteId, TravelRouteDefinition>();
        public IReadOnlyCollection<WorldLocationDefinition> OrderedLocations => _locations.Values;
        public IReadOnlyCollection<TravelRouteDefinition> OrderedRoutes => _routes.Values;
        public int LocationCount => _locations.Count;
        public int RouteCount => _routes.Count;
        public void Add(WorldLocationDefinition value) { if (value == null || _locations.ContainsKey(value.Id)) throw new InvalidOperationException("World location is null or duplicated."); _locations.Add(value.Id, value); }
        public void Add(TravelRouteDefinition value) { if (value == null || _routes.ContainsKey(value.Id)) throw new InvalidOperationException("Travel route is null or duplicated."); if (!_locations.ContainsKey(value.First) || !_locations.ContainsKey(value.Second)) throw new InvalidOperationException("Travel route endpoint is missing."); _routes.Add(value.Id, value); }
        public WorldLocationDefinition Location(WorldLocationId id) => _locations.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException("World location was not found.");
        public TravelRouteDefinition Route(TravelRouteId id) => _routes.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException("Travel route was not found.");
        public IReadOnlyList<TravelRouteDefinition> RoutesFrom(WorldLocationId id) => _routes.Values.Where(x => x.First.Equals(id) || x.Bidirectional && x.Second.Equals(id)).OrderBy(x => x.Id).ToList().AsReadOnly();
        public WorldLocationDefinition LocationForCity(CityId id) => _locations.Values.Single(x => x.CityId.HasValue && x.CityId.Value.Equals(id));
    }

    public readonly struct TravelActorRef : IEquatable<TravelActorRef>, IComparable<TravelActorRef>
    {
        private TravelActorRef(TravelActorKind kind, string id) { if (!Enum.IsDefined(typeof(TravelActorKind), kind) || string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Travel actor is invalid."); Kind = kind; Id = id; }
        public TravelActorKind Kind { get; }
        public string Id { get; }
        public static TravelActorRef Character(CharacterId id) => New(TravelActorKind.Character, id.IsValid, id.Value);
        public static TravelActorRef Army(ArmyId id) => New(TravelActorKind.Army, id.IsValid, id.Value);
        public static TravelActorRef Caravan(CaravanId id) => New(TravelActorKind.Caravan, id.IsValid, id.Value);
        public static TravelActorRef Envoy(string id) => new TravelActorRef(TravelActorKind.Envoy, id);
        public static TravelActorRef Messenger(string id) => new TravelActorRef(TravelActorKind.Messenger, id);
        public static TravelActorRef Restore(TravelActorKind kind, string id) => new TravelActorRef(kind, id);
        private static TravelActorRef New(TravelActorKind kind, bool valid, string id) { if (!valid) throw new ArgumentException("Typed travel actor identifier is invalid."); return new TravelActorRef(kind, id); }
        public int CompareTo(TravelActorRef other) { var c = Kind.CompareTo(other.Kind); return c != 0 ? c : StringComparer.Ordinal.Compare(Id, other.Id); }
        public bool Equals(TravelActorRef other) => Kind == other.Kind && StringComparer.Ordinal.Equals(Id, other.Id);
        public override bool Equals(object? obj) => obj is TravelActorRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id);
    }

    public sealed class TravelJourneyState
    {
        private readonly IReadOnlyList<TravelRouteId> _path;
        public TravelJourneyState(JourneyId id, TravelActorRef actor, WorldLocationId origin, WorldLocationId destination, IEnumerable<TravelRouteId> path, WorldTimestamp departedAt, WorldTimestamp lastAdvancedAt, int segmentIndex = 0, long segmentElapsedTicks = 0, TravelLifecycle lifecycle = TravelLifecycle.Active, WorldTimestamp? arrivedAt = null)
        {
            if (!id.IsValid || !origin.IsValid || !destination.IsValid || origin.Equals(destination) || lastAdvancedAt.Ticks < departedAt.Ticks || segmentIndex < 0 || segmentElapsedTicks < 0 || !Enum.IsDefined(typeof(TravelLifecycle), lifecycle)) throw new ArgumentException("Travel journey is invalid.");
            _path = (path ?? throw new ArgumentNullException(nameof(path))).ToList().AsReadOnly(); if (_path.Count == 0 || _path.Any(x => !x.IsValid) || _path.Distinct().Count() != _path.Count) throw new ArgumentException("Travel path is invalid.", nameof(path));
            if (segmentIndex > _path.Count || lifecycle == TravelLifecycle.Active && segmentIndex >= _path.Count || lifecycle == TravelLifecycle.Arrived && (!arrivedAt.HasValue || segmentIndex != _path.Count)) throw new ArgumentException("Travel lifecycle conflicts with progress.");
            Id = id; Actor = actor; Origin = origin; Destination = destination; DepartedAt = departedAt; LastAdvancedAt = lastAdvancedAt; SegmentIndex = segmentIndex; SegmentElapsedTicks = segmentElapsedTicks; Lifecycle = lifecycle; ArrivedAt = arrivedAt;
        }
        public JourneyId Id { get; }
        public TravelActorRef Actor { get; }
        public WorldLocationId Origin { get; }
        public WorldLocationId Destination { get; }
        public IReadOnlyList<TravelRouteId> Path => _path;
        public WorldTimestamp DepartedAt { get; }
        public WorldTimestamp LastAdvancedAt { get; private set; }
        public int SegmentIndex { get; private set; }
        public long SegmentElapsedTicks { get; private set; }
        public TravelLifecycle Lifecycle { get; private set; }
        public WorldTimestamp? ArrivedAt { get; private set; }
        public void RestoreProgress(WorldTimestamp at, int segmentIndex, long elapsedTicks, TravelLifecycle lifecycle, WorldTimestamp? arrivedAt) { if (at.Ticks < LastAdvancedAt.Ticks || segmentIndex < SegmentIndex || elapsedTicks < 0) throw new InvalidOperationException("Travel progress cannot move backwards."); LastAdvancedAt = at; SegmentIndex = segmentIndex; SegmentElapsedTicks = elapsedTicks; Lifecycle = lifecycle; ArrivedAt = arrivedAt; }
        public void SetProgress(WorldTimestamp at, int segmentIndex, long elapsedTicks) { if (Lifecycle != TravelLifecycle.Active || at.Ticks < LastAdvancedAt.Ticks || segmentIndex < SegmentIndex || segmentIndex >= _path.Count || elapsedTicks < 0) throw new InvalidOperationException("Travel progress is invalid."); LastAdvancedAt = at; SegmentIndex = segmentIndex; SegmentElapsedTicks = elapsedTicks; }
        public void Arrive(WorldTimestamp at) { if (Lifecycle != TravelLifecycle.Active || at.Ticks < LastAdvancedAt.Ticks) throw new InvalidOperationException("Journey cannot arrive."); LastAdvancedAt = at; SegmentIndex = _path.Count; SegmentElapsedTicks = 0; Lifecycle = TravelLifecycle.Arrived; ArrivedAt = at; }
        public void Cancel(WorldTimestamp at) { if (Lifecycle != TravelLifecycle.Active || at.Ticks < LastAdvancedAt.Ticks) throw new InvalidOperationException("Journey cannot be cancelled."); LastAdvancedAt = at; Lifecycle = TravelLifecycle.Cancelled; }
    }

    public sealed class TravelState
    {
        private readonly SortedDictionary<JourneyId, TravelJourneyState> _journeys = new SortedDictionary<JourneyId, TravelJourneyState>();
        public IReadOnlyCollection<TravelJourneyState> OrderedJourneys => _journeys.Values;
        public bool Contains(JourneyId id) => _journeys.ContainsKey(id);
        public void Add(TravelJourneyState value) { if (value == null || _journeys.ContainsKey(value.Id) || _journeys.Values.Any(x => x.Lifecycle == TravelLifecycle.Active && x.Actor.Equals(value.Actor))) throw new InvalidOperationException("Journey is null, duplicated, or actor is already travelling."); _journeys.Add(value.Id, value); }
        public TravelJourneyState GetRequired(JourneyId id) => _journeys.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException("Journey was not found.");
        public TravelJourneyState? ActiveFor(TravelActorRef actor) => _journeys.Values.SingleOrDefault(x => x.Lifecycle == TravelLifecycle.Active && x.Actor.Equals(actor));
    }

    public sealed class GeographyCampaignState
    {
        public GeographyCampaignState(WorldGeography? world = null, TravelState? travel = null) { World = world ?? new WorldGeography(); Travel = travel ?? new TravelState(); }
        public WorldGeography World { get; }
        public TravelState Travel { get; }
    }
}
