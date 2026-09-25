using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.AI;
using FOC.Domain.AI;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Geography;
using FOC.Domain.Military;
using FOC.Domain.Time;

namespace FOC.Application.Geography
{
    public interface IExternalTravelActorPort
    {
        TravelActorKind Kind { get; }
        bool IsAt(string actorId, WorldLocationDefinition location);
        void Begin(string actorId, WorldLocationDefinition origin);
        void Move(string actorId, MapPoint position);
        void Arrive(string actorId, WorldLocationDefinition destination);
    }

    public sealed class TravelSegmentContext
    {
        public TravelSegmentContext(JourneyId journeyId,TravelActorRef actor,WorldLocationId from,WorldLocationId to,TravelRouteId routeId,RegionId regionId,MapPoint position){JourneyId=journeyId;Actor=actor;From=from;To=to;RouteId=routeId;RegionId=regionId;Position=position;}
        public JourneyId JourneyId{get;}public TravelActorRef Actor{get;}public WorldLocationId From{get;}public WorldLocationId To{get;}public TravelRouteId RouteId{get;}public RegionId RegionId{get;}public MapPoint Position{get;}
    }
    /// <summary>Boundary for encounter checks and battle-location creation. Implementations may observe; they cannot teleport or mutate journey progress.</summary>
    public interface ITravelProgressHook { void SegmentEntered(TravelSegmentContext context); void Arrived(JourneyId journeyId,TravelActorRef actor,WorldLocationDefinition destination); }

    /// <summary>SLICE_TUNING: integer metres/hour. WorldClock ticks are seconds for this vertical slice.</summary>
    public sealed class SliceTravelTimePolicy
    {
        public long SpeedMetresPerHour(TravelActorKind kind)
        {
            switch (kind)
            {
                case TravelActorKind.Army: return 3000;
                case TravelActorKind.Caravan: return 3500;
                case TravelActorKind.Character: return 5000;
                case TravelActorKind.Envoy: return 6500;
                case TravelActorKind.Messenger: return 7500;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public long SegmentTicks(TravelActorKind kind, long distanceMeters)
        {
            if (distanceMeters <= 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            var speed = SpeedMetresPerHour(kind);
            return checked((distanceMeters * 3600L + speed - 1L) / speed);
        }
    }

    public sealed class TravelCommandService
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly DeterministicRoutePathfinder _pathfinder;
        private readonly SliceTravelTimePolicy _time;
        private readonly Dictionary<TravelActorKind, IExternalTravelActorPort> _external;
        private readonly IReadOnlyList<ITravelProgressHook> _hooks;
        public TravelCommandService(CampaignRuntimeState campaign, DeterministicRoutePathfinder? pathfinder = null, SliceTravelTimePolicy? time = null, IEnumerable<IExternalTravelActorPort>? external = null, IEnumerable<ITravelProgressHook>? hooks = null)
        {
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _pathfinder = pathfinder ?? new DeterministicRoutePathfinder(); _time = time ?? new SliceTravelTimePolicy();
            _external = (external ?? Array.Empty<IExternalTravelActorPort>()).ToDictionary(x => x.Kind);
            _hooks=(hooks??Array.Empty<ITravelProgressHook>()).ToList().AsReadOnly();
        }

        public TravelJourneyState Start(JourneyId journeyId, TravelActorRef actor, WorldLocationId originId, WorldLocationId destinationId)
        {
            if (_campaign.Geography.Travel.Contains(journeyId)) throw new InvalidOperationException("Journey identifier already exists.");
            if (_campaign.Geography.Travel.ActiveFor(actor) != null) throw new InvalidOperationException("Actor already has an active journey.");
            var origin = _campaign.Geography.World.Location(originId); var destination = _campaign.Geography.World.Location(destinationId);
            ValidateAtOrigin(actor, origin, destination);
            var path = _pathfinder.Find(_campaign.Geography.World, originId, destinationId);
            var journey = new TravelJourneyState(journeyId, actor, originId, destinationId, path.Routes, _campaign.Clock.Now, _campaign.Clock.Now);
            BeginActor(actor, origin);
            _campaign.Geography.Travel.Add(journey);
            NotifySegment(journey,0);
            return journey;
        }

        public void Advance(WorldDuration duration)
        {
            if (duration.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (_campaign.Clock.IsPaused || duration.Ticks == 0) return;
            _campaign.Clock.Advance(duration);
            var now = _campaign.Clock.Now;
            foreach (var journey in _campaign.Geography.Travel.OrderedJourneys.Where(x => x.Lifecycle == TravelLifecycle.Active).ToList()) AdvanceJourney(journey, now);
        }

        public long TotalJourneyTicks(TravelJourneyState journey) => journey.Path.Sum(x => _time.SegmentTicks(journey.Actor.Kind, _campaign.Geography.World.Route(x).DistanceMeters));

        private void AdvanceJourney(TravelJourneyState journey, WorldTimestamp now)
        {
            var available = checked(now.Ticks - journey.LastAdvancedAt.Ticks + journey.SegmentElapsedTicks);
            var segment = journey.SegmentIndex;
            while (segment < journey.Path.Count)
            {
                var route = _campaign.Geography.World.Route(journey.Path[segment]);
                var required = _time.SegmentTicks(journey.Actor.Kind, route.DistanceMeters);
                if (available < required)
                {
                    journey.SetProgress(now, segment, available);
                    MoveActor(journey.Actor, Interpolate(route, journey, segment, available, required));
                    return;
                }
                available -= required; segment++; if(segment<journey.Path.Count)NotifySegment(journey,segment);
            }
            journey.Arrive(now);
            ArriveActor(journey.Actor, _campaign.Geography.World.Location(journey.Destination));
            foreach(var hook in _hooks)hook.Arrived(journey.Id,journey.Actor,_campaign.Geography.World.Location(journey.Destination));
        }

        private MapPoint Interpolate(TravelRouteDefinition route, TravelJourneyState journey, int segment, long elapsed, long required)
        {
            var start = segment == 0 ? journey.Origin : _campaign.Geography.World.Route(journey.Path[segment - 1]).Other(RouteStart(journey, segment - 1));
            var from = RouteStart(journey, segment);
            if (!start.Equals(from)) start = from;
            var to = route.Other(from);
            var a = _campaign.Geography.World.Location(from).MapPoint; var b = _campaign.Geography.World.Location(to).MapPoint;
            return new MapPoint((int)(a.X + (b.X - (long)a.X) * elapsed / required), (int)(a.Y + (b.Y - (long)a.Y) * elapsed / required));
        }

        private WorldLocationId RouteStart(TravelJourneyState journey, int segment)
        {
            var cursor = journey.Origin;
            for (var i = 0; i < segment; i++) cursor = _campaign.Geography.World.Route(journey.Path[i]).Other(cursor);
            return cursor;
        }

        private void NotifySegment(TravelJourneyState journey,int segment){var from=RouteStart(journey,segment);var route=_campaign.Geography.World.Route(journey.Path[segment]);var to=route.Other(from);var location=_campaign.Geography.World.Location(from);var context=new TravelSegmentContext(journey.Id,journey.Actor,from,to,route.Id,location.RegionId,location.MapPoint);foreach(var hook in _hooks)hook.SegmentEntered(context);}

        private void ValidateAtOrigin(TravelActorRef actor, WorldLocationDefinition origin, WorldLocationDefinition destination)
        {
            switch (actor.Kind)
            {
                case TravelActorKind.Character:
                    var character = _campaign.Characters.GetRequired(CharacterId.Create(actor.Id));
                    if (character.IsDead || character.Captivity != null || !origin.CityId.HasValue || character.Location.Kind != CharacterLocationKind.City || !character.Location.CityId!.Value.Equals(origin.CityId.Value)) throw new InvalidOperationException("Character is unavailable or not at the journey origin.");
                    break;
                case TravelActorKind.Army:
                    var army = _campaign.Military.Armies.GetRequired(ArmyId.Create(actor.Id));
                    if (!origin.CityId.HasValue || army.Location.Kind != ArmyLocationKind.City || !army.Location.CityId!.Value.Equals(origin.CityId.Value)) throw new InvalidOperationException("Army is not at the journey origin.");
                    break;
                case TravelActorKind.Caravan:
                    var caravan = _campaign.Economy.Caravans.GetRequired(CaravanId.Create(actor.Id));
                    if (!origin.CityId.HasValue || !destination.CityId.HasValue || caravan.LocationStage != CaravanLocationStage.AtOrigin || !caravan.OriginCityId.Equals(origin.CityId.Value) || !caravan.DestinationCityId.Equals(destination.CityId.Value)) throw new InvalidOperationException("Caravan endpoints or lifecycle do not match the journey.");
                    break;
                case TravelActorKind.Envoy:
                case TravelActorKind.Messenger:
                    if (!_external.TryGetValue(actor.Kind, out var port) || !port.IsAt(actor.Id, origin)) throw new InvalidOperationException("External diplomatic travel actor is unavailable at the origin.");
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void BeginActor(TravelActorRef actor, WorldLocationDefinition origin)
        {
            var position = new WorldPosition(origin.MapPoint.X, origin.MapPoint.Y);
            switch (actor.Kind)
            {
                case TravelActorKind.Character: _campaign.Characters.GetRequired(CharacterId.Create(actor.Id)).MoveTo(CharacterLocation.TravellingAt(position), _campaign.Clock.Now); break;
                case TravelActorKind.Army: _campaign.Military.Armies.GetRequired(ArmyId.Create(actor.Id)).SetLocation(ArmyLocation.InTransit(position)); break;
                case TravelActorKind.Caravan: _campaign.Economy.Caravans.GetRequired(CaravanId.Create(actor.Id)).MarkInTransit(); break;
                default: _external[actor.Kind].Begin(actor.Id, origin); break;
            }
        }

        private void MoveActor(TravelActorRef actor, MapPoint point)
        {
            var position = new WorldPosition(point.X, point.Y);
            switch (actor.Kind)
            {
                case TravelActorKind.Character: _campaign.Characters.GetRequired(CharacterId.Create(actor.Id)).MoveTo(CharacterLocation.TravellingAt(position), _campaign.Clock.Now); break;
                case TravelActorKind.Army: _campaign.Military.Armies.GetRequired(ArmyId.Create(actor.Id)).SetLocation(ArmyLocation.InTransit(position)); break;
                case TravelActorKind.Caravan: break;
                default: _external[actor.Kind].Move(actor.Id, point); break;
            }
        }

        private void ArriveActor(TravelActorRef actor, WorldLocationDefinition destination)
        {
            var position = new WorldPosition(destination.MapPoint.X, destination.MapPoint.Y);
            switch (actor.Kind)
            {
                case TravelActorKind.Character: _campaign.Characters.GetRequired(CharacterId.Create(actor.Id)).MoveTo(destination.CityId.HasValue ? CharacterLocation.InCity(destination.CityId.Value) : CharacterLocation.At(position), _campaign.Clock.Now); break;
                case TravelActorKind.Army: _campaign.Military.Armies.GetRequired(ArmyId.Create(actor.Id)).SetLocation(destination.CityId.HasValue ? ArmyLocation.InCity(destination.CityId.Value) : ArmyLocation.AtCamp(position)); break;
                case TravelActorKind.Caravan: _campaign.Economy.Caravans.GetRequired(CaravanId.Create(actor.Id)).MarkAtDestination(); break;
                default: _external[actor.Kind].Arrive(actor.Id, destination); break;
            }
        }
    }

    public sealed class AITravelActionProvider : IAITravelActionProvider
    {
        private readonly CampaignRuntimeState _campaign; private readonly TravelCommandService _travel;
        public AITravelActionProvider(CampaignRuntimeState campaign, TravelCommandService travel) { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _travel = travel ?? throw new ArgumentNullException(nameof(travel)); }
        public bool CanExecute(AIDecisionOwnerRef owner, AITargetRef target, WorldTimestamp at)
        {
            if (!at.Equals(_campaign.Clock.Now) || owner.Kind != AIDecisionOwnerKind.Character || target.Kind != AITargetKind.WorldLocation) return false;
            try
            {
                var actor = TravelActorRef.Character(CharacterId.Create(owner.Id)); if (_campaign.Geography.Travel.ActiveFor(actor) != null) return false;
                var character = _campaign.Characters.GetRequired(CharacterId.Create(owner.Id));
                if (character.IsDead || character.Captivity != null || character.Location.Kind != CharacterLocationKind.City || !character.Location.CityId.HasValue) return false;
                var origin = _campaign.Geography.World.LocationForCity(character.Location.CityId.Value).Id; var destination = WorldLocationId.Create(target.Id);
                if (origin.Equals(destination)) return false;
                new DeterministicRoutePathfinder().Find(_campaign.Geography.World, origin, destination);
                return true;
            }
            catch { return false; }
        }
        public TravelJourneyState Execute(JourneyId id, AIDecisionOwnerRef owner, WorldLocationId origin, WorldLocationId destination) { if (!CanExecute(owner, AITargetRef.WorldLocation(destination), _campaign.Clock.Now)) throw new InvalidOperationException("AI travel action is not eligible."); return _travel.Start(id, TravelActorRef.Character(CharacterId.Create(owner.Id)), origin, destination); }
    }
}
