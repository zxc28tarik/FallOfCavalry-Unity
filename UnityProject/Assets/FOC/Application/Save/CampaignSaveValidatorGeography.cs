using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Geography;
using FOC.Domain.Validation;

namespace FOC.Application.Save
{
    public sealed partial class CampaignSaveValidator
    {
        private static void ValidateGeography(CampaignSaveData data, ValidationResult result)
        {
            if (data.WorldLocations.Any(x=>x==null) || data.WorldLocations.GroupBy(x=>x.WorldLocationId,StringComparer.Ordinal).Any(x=>x.Count()!=1)) { result.AddError("WORLD_LOCATION_DUPLICATE","World locations must be non-null and uniquely identified."); return; }
            var locations=new HashSet<string>(data.WorldLocations.Select(x=>x.WorldLocationId),StringComparer.Ordinal);
            var cities=new HashSet<string>(data.Cities.Select(x=>x.CityId),StringComparer.Ordinal);
            foreach(var x in data.WorldLocations) if(string.IsNullOrWhiteSpace(x.WorldLocationId)||string.IsNullOrWhiteSpace(x.DisplayName)||string.IsNullOrWhiteSpace(x.RegionId)||x.MapX<0||x.MapX>MapPoint.Scale||x.MapY<0||x.MapY>MapPoint.Scale||x.SourceIds==null||x.SourceIds.Count==0||!Enum.IsDefined(typeof(WorldLocationKind),x.Kind)||!Enum.IsDefined(typeof(HistoricalConfidence),x.Confidence)||!Enum.IsDefined(typeof(GeographyContentStatus),x.Status)||!string.IsNullOrEmpty(x.CityId)&&!cities.Contains(x.CityId))result.AddError("WORLD_LOCATION_INVALID","World location payload or City reference is invalid.");
            if(data.TravelRoutes.Any(x=>x==null)||data.TravelRoutes.GroupBy(x=>x.TravelRouteId,StringComparer.Ordinal).Any(x=>x.Count()!=1)){result.AddError("TRAVEL_ROUTE_DUPLICATE","Travel routes must be non-null and uniquely identified.");return;}
            var routes=new HashSet<string>(data.TravelRoutes.Select(x=>x.TravelRouteId),StringComparer.Ordinal);
            foreach(var x in data.TravelRoutes)if(string.IsNullOrWhiteSpace(x.TravelRouteId)||!locations.Contains(x.FirstLocationId)||!locations.Contains(x.SecondLocationId)||x.FirstLocationId==x.SecondLocationId||x.DistanceMeters<=0||x.SourceIds==null||x.SourceIds.Count==0||!Enum.IsDefined(typeof(RouteMode),x.Mode))result.AddError("TRAVEL_ROUTE_INVALID","Travel route payload or endpoints are invalid.");
            var activeActors=new HashSet<string>(StringComparer.Ordinal);
            foreach(var x in data.TravelJourneys){if(x==null||string.IsNullOrWhiteSpace(x.JourneyId)||string.IsNullOrWhiteSpace(x.ActorId)||!locations.Contains(x.OriginId)||!locations.Contains(x.DestinationId)||x.PathRouteIds==null||x.PathRouteIds.Count==0||x.PathRouteIds.Any(y=>!routes.Contains(y))||x.DepartedAt<0||x.LastAdvancedAt<x.DepartedAt||x.SegmentIndex<0||x.SegmentIndex>x.PathRouteIds.Count||x.SegmentElapsedTicks<0||!Enum.IsDefined(typeof(TravelActorKind),x.ActorKind)||!Enum.IsDefined(typeof(TravelLifecycle),x.Lifecycle)){result.AddError("TRAVEL_JOURNEY_INVALID","Travel journey payload is invalid.");continue;}if(x.Lifecycle==(int)TravelLifecycle.Active&&!activeActors.Add(x.ActorKind+"\n"+x.ActorId))result.AddError("TRAVEL_ACTOR_DUPLICATE","An actor cannot have two active journeys.");}
        }
    }
}
