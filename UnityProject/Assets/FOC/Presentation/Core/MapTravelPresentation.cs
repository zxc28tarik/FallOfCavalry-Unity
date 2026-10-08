using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Application.Geography;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Geography;
using FOC.Domain.Time;

namespace FOC.Presentation.Core
{
    public sealed class MapTravelPresentation
    {
        public MapTravelPresentation(string actorLabel, string originLabel, string destinationLabel,
            string reasonKey, bool active, bool canAdvance, long clockTicks, long distanceMeters,
            long totalTicks, long remainingTicks, IEnumerable<string> routeIds, IEnumerable<string> stops,
            PresentationEntityRef? selected, PresentationEntityRef? city)
        {
            ActorLabel = actorLabel; OriginLabel = originLabel; DestinationLabel = destinationLabel;
            ReasonKey = reasonKey; Active = active; CanAdvance = canAdvance; ClockTicks = clockTicks;
            DistanceMeters = distanceMeters; TotalTicks = totalTicks; RemainingTicks = remainingTicks;
            RouteIds = routeIds.ToList().AsReadOnly(); Stops = stops.ToList().AsReadOnly(); Selected = selected; City = city;
        }
        public string ActorLabel { get; }
        public string OriginLabel { get; }
        public string DestinationLabel { get; }
        public string ReasonKey { get; }
        public bool Active { get; }
        public bool CanAdvance { get; }
        public long ClockTicks { get; }
        public long DistanceMeters { get; }
        public long TotalTicks { get; }
        public long RemainingTicks { get; }
        public IReadOnlyList<string> RouteIds { get; }
        public IReadOnlyList<string> Stops { get; }
        public PresentationEntityRef? Selected { get; }
        public PresentationEntityRef? City { get; }
        public float Progress => TotalTicks <= 0 ? 0 : (float)((TotalTicks - RemainingTicks) / (double)TotalTicks);
    }

    public static class TravelPresentationText
    {
        public static string Reason(TravelPreviewFailure failure) => "presentation.travel.blocked." + failure.ToString().ToLowerInvariant();
        public static string Duration(long seconds)
        {
            var minutes = (seconds + 59) / 60;
            return (minutes / 60).ToString(CultureInfo.InvariantCulture) + " sa " + (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + " dk";
        }
        public static string Clock(long ticks) => "Gün " + (ticks / 86400 + 1).ToString(CultureInfo.InvariantCulture)
            + " · " + (ticks / 3600 % 24).ToString("00", CultureInfo.InvariantCulture) + ":" + (ticks / 60 % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Player-only commands. Visibility of another actor never grants movement authority.</summary>
    public sealed class TravelPresentationCommands
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private readonly TravelCommandService _travel;
        public TravelPresentationCommands(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        { _campaign = campaign; _viewer = viewer; _travel = new TravelCommandService(campaign); }
        public PresentationActionResult Start(WorldLocationId destination)
        {
            if (_viewer.ControlledIdentity.Kind != PresentationEntityKind.Character)
                return PresentationActionResult.Rejected(TravelPresentationText.Reason(TravelPreviewFailure.ActorUnavailable));
            var actor = CharacterId.Create(_viewer.ControlledIdentity.Id);
            var plan = _travel.PreviewCharacter(actor, destination);
            if (!plan.CanStart) return PresentationActionResult.Rejected(TravelPresentationText.Reason(plan.Failure));
            // Revalidate at execution; stale enabled descriptors cannot create a second journey.
            _travel.StartCharacter(JourneyId.Create("journey-player-" + actor.Value + "-" + _campaign.Clock.Now.Ticks + "-" + destination.Value), actor, destination);
            return PresentationActionResult.Success("presentation.travel.started");
        }
        public PresentationActionResult AdvanceOneHour()
        {
            if (_viewer.ControlledIdentity.Kind != PresentationEntityKind.Character
                || _campaign.Geography.Travel.ActiveFor(TravelActorRef.Character(CharacterId.Create(_viewer.ControlledIdentity.Id))) == null)
                return PresentationActionResult.Rejected("presentation.action.no-active-journey");
            if (_campaign.Clock.IsPaused) return PresentationActionResult.Rejected("presentation.travel.paused");
            _travel.Advance(WorldDuration.FromMinutes(60));
            return PresentationActionResult.Success("presentation.travel.advanced");
        }
    }

    internal static class MapTravelQuery
    {
        public static MapTravelPresentation Build(CampaignRuntimeState campaign, PresentationViewerContext viewer, string? requestedDestination)
        {
            var world = campaign.Geography.World;
            var travel = new TravelCommandService(campaign);
            var player = viewer.ControlledIdentity.Kind == PresentationEntityKind.Character
                ? campaign.Characters.OrderedCharacters.FirstOrDefault(x => x.Id.Value == viewer.ControlledIdentity.Id) : null;
            var active = player == null ? null : campaign.Geography.Travel.ActiveFor(TravelActorRef.Character(player.Id));
            var origin = player == null ? null : travel.CharacterOrigin(player.Id);
            var destinationId = requestedDestination ?? active?.Destination.Value ?? origin?.Id.Value;
            var selected = destinationId == null ? (PresentationEntityRef?)null : new PresentationEntityRef(PresentationEntityKind.WorldLocation, destinationId);
            var destination = world.OrderedLocations.FirstOrDefault(x => x.Id.Value == destinationId);
            var city = destination?.CityId.HasValue == true ? new PresentationEntityRef(PresentationEntityKind.City, destination.CityId.Value.Value) : (PresentationEntityRef?)null;
            var reason = "presentation.travel.select-destination";
            IReadOnlyList<TravelRouteId> path = Array.Empty<TravelRouteId>();
            long total = 0, remaining = 0;
            if (active != null)
            {
                origin = world.Location(active.Origin);
                path = active.Path;
                total = travel.TotalJourneyTicks(active);
                var time = new SliceTravelTimePolicy();
                var elapsed = path.Take(active.SegmentIndex).Sum(x => time.SegmentTicks(active.Actor.Kind, world.Route(x).DistanceMeters)) + active.SegmentElapsedTicks;
                remaining = Math.Max(0, total - elapsed);
                reason = campaign.Clock.IsPaused ? "presentation.travel.paused" : "presentation.travel.in-progress";
            }
            else if (player == null) reason = TravelPresentationText.Reason(TravelPreviewFailure.ActorUnavailable);
            else if (destinationId != null)
            {
                var plan = travel.PreviewCharacter(player.Id, WorldLocationId.Create(destinationId));
                reason = plan.CanStart ? "presentation.travel.preview-ready" : TravelPresentationText.Reason(plan.Failure);
                if (plan.Path != null) { path = plan.Path.Routes; total = remaining = plan.DurationTicks; }
            }
            var stops = new List<string>();
            if (origin != null && path.Count > 0)
            {
                var cursor = origin.Id; stops.Add(origin.DisplayName);
                foreach (var id in path) { cursor = world.Route(id).Other(cursor); stops.Add(world.Location(cursor).DisplayName); }
            }
            // While travelling, the journey destination is authoritative; selecting another marker
            // remains possible for inspection but does not silently reroute or replace that journey.
            var destinationLabel = active != null ? world.Location(active.Destination).DisplayName : destination?.DisplayName ?? "—";
            return new MapTravelPresentation(player?.Definition.DisplayName ?? "—", origin?.DisplayName ?? "—",
                destinationLabel, reason, active != null, active != null && !campaign.Clock.IsPaused,
                campaign.Clock.Now.Ticks, path.Sum(x => world.Route(x).DistanceMeters), total, remaining,
                path.Select(x => x.Value), stops, selected, city);
        }
    }
}
