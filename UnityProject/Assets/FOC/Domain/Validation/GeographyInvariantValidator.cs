using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Geography;

namespace FOC.Domain.Validation
{
    public sealed class GeographyInvariantValidator : IInvariantValidator<CampaignRuntimeState>
    {
        public ValidationResult Validate(CampaignRuntimeState subject)
        {
            var result=new ValidationResult();if(subject==null){result.AddError("CAMPAIGN_NULL","Campaign is required.");return result;}
            foreach(var location in subject.Geography.World.OrderedLocations)if(location.CityId.HasValue&&!subject.Cities.OrderedCities.Any(x=>x.Id.Equals(location.CityId.Value)))result.AddError("GEOGRAPHY_CITY_MISSING","World location references a missing City.");
            foreach(var route in subject.Geography.World.OrderedRoutes){try{subject.Geography.World.Location(route.First);subject.Geography.World.Location(route.Second);}catch(KeyNotFoundException){result.AddError("GEOGRAPHY_ROUTE_ENDPOINT_MISSING","Travel route references a missing WorldLocation.");}}
            var actors=new HashSet<TravelActorRef>();foreach(var journey in subject.Geography.Travel.OrderedJourneys){if(journey.Lifecycle==TravelLifecycle.Active&&!actors.Add(journey.Actor))result.AddError("GEOGRAPHY_ACTOR_DUAL_TRAVEL","Actor has more than one active journey.");try{var cursor=journey.Origin;foreach(var id in journey.Path){var route=subject.Geography.World.Route(id);if(!route.Connects(cursor)){result.AddError("GEOGRAPHY_PATH_DISCONNECTED","Journey path is disconnected.");break;}cursor=route.Other(cursor);}if(!cursor.Equals(journey.Destination))result.AddError("GEOGRAPHY_PATH_DESTINATION","Journey path does not reach its destination.");}catch(KeyNotFoundException){result.AddError("GEOGRAPHY_JOURNEY_REFERENCE_MISSING","Journey references missing geography.");}}
            return result;
        }
    }
}
