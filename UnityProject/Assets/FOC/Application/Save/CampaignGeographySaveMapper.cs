using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Geography;
using FOC.Domain.Time;

namespace FOC.Application.Save
{
    public static partial class CampaignSaveMapper
    {
        private static void AddGeographySaveData(FOC.Domain.Campaign.CampaignRuntimeState state, CampaignSaveData data)
        {
            foreach (var x in state.Geography.World.OrderedLocations) data.WorldLocations.Add(new WorldLocationSaveData { WorldLocationId=x.Id.Value, DisplayName=x.DisplayName, Kind=(int)x.Kind, RegionId=x.RegionId.Value, CityId=x.CityId?.Value??string.Empty, MapX=x.MapPoint.X, MapY=x.MapPoint.Y, LatitudeE6=x.Coordinate.LatitudeE6, LongitudeE6=x.Coordinate.LongitudeE6, Confidence=(int)x.Confidence, Status=(int)x.Status, Aliases=x.Aliases.ToList(), SourceIds=x.SourceIds.ToList() });
            foreach (var x in state.Geography.World.OrderedRoutes) data.TravelRoutes.Add(new TravelRouteSaveData { TravelRouteId=x.Id.Value, FirstLocationId=x.First.Value, SecondLocationId=x.Second.Value, Mode=(int)x.Mode, DistanceMeters=x.DistanceMeters, Confidence=(int)x.Confidence, Status=(int)x.Status, Bidirectional=x.Bidirectional, SourceIds=x.SourceIds.ToList() });
            foreach (var x in state.Geography.Travel.OrderedJourneys) data.TravelJourneys.Add(new TravelJourneySaveData { JourneyId=x.Id.Value, ActorKind=(int)x.Actor.Kind, ActorId=x.Actor.Id, OriginId=x.Origin.Value, DestinationId=x.Destination.Value, PathRouteIds=x.Path.Select(y=>y.Value).ToList(), DepartedAt=x.DepartedAt.Ticks, LastAdvancedAt=x.LastAdvancedAt.Ticks, SegmentIndex=x.SegmentIndex, SegmentElapsedTicks=x.SegmentElapsedTicks, Lifecycle=(int)x.Lifecycle, ArrivedAt=x.ArrivedAt?.Ticks });
        }

        private static GeographyCampaignState RestoreGeography(CampaignSaveData data)
        {
            var world = new WorldGeography();
            foreach (var x in data.WorldLocations) world.Add(new WorldLocationDefinition(WorldLocationId.Create(x.WorldLocationId),x.DisplayName,(WorldLocationKind)x.Kind,RegionId.Create(x.RegionId),new MapPoint(x.MapX,x.MapY),new GeoCoordinateE6(x.LatitudeE6,x.LongitudeE6),x.Aliases,x.SourceIds,(HistoricalConfidence)x.Confidence,(GeographyContentStatus)x.Status,string.IsNullOrEmpty(x.CityId)?(CityId?)null:CityId.Create(x.CityId)));
            foreach (var x in data.TravelRoutes) world.Add(new TravelRouteDefinition(TravelRouteId.Create(x.TravelRouteId),WorldLocationId.Create(x.FirstLocationId),WorldLocationId.Create(x.SecondLocationId),(RouteMode)x.Mode,x.DistanceMeters,x.SourceIds,(HistoricalConfidence)x.Confidence,(GeographyContentStatus)x.Status,x.Bidirectional));
            var travel = new TravelState();
            foreach (var x in data.TravelJourneys) travel.Add(new TravelJourneyState(JourneyId.Create(x.JourneyId),TravelActorRef.Restore((TravelActorKind)x.ActorKind,x.ActorId),WorldLocationId.Create(x.OriginId),WorldLocationId.Create(x.DestinationId),x.PathRouteIds.Select(TravelRouteId.Create),new WorldTimestamp(x.DepartedAt),new WorldTimestamp(x.LastAdvancedAt),x.SegmentIndex,x.SegmentElapsedTicks,(TravelLifecycle)x.Lifecycle,x.ArrivedAt.HasValue?new WorldTimestamp(x.ArrivedAt.Value):(WorldTimestamp?)null));
            return new GeographyCampaignState(world,travel);
        }
    }
}
