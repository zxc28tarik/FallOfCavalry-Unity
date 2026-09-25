using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Geography;

namespace FOC.Presentation.Core
{
    public sealed class WorldMapPresentationDataProvider : IMapPresentationDataProvider
    {
        private readonly CampaignRuntimeState _campaign;
        public WorldMapPresentationDataProvider(CampaignRuntimeState campaign){_campaign=campaign??throw new ArgumentNullException(nameof(campaign));}
        public IReadOnlyList<MapMarkerPresentation> GetKnownMarkers(PresentationViewerContext viewer)
        {
            if(viewer==null)throw new ArgumentNullException(nameof(viewer));var markers=_campaign.Geography.World.OrderedLocations.Select(x=>new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.WorldLocation,x.Id.Value),x.DisplayName,x.MapPoint.X/(float)MapPoint.Scale,x.MapPoint.Y/(float)MapPoint.Scale,PresentationKnowledge.ExactSelf,false,x.CityId.HasValue?new PresentationEntityRef(PresentationEntityKind.City,x.CityId.Value.Value):(PresentationEntityRef?)null)).ToList();
            foreach(var character in _campaign.Characters.OrderedCharacters){var entity=new PresentationEntityRef(PresentationEntityKind.Character,character.Id.Value);if(viewer.CanReadExact(entity)&&TryCharacterPoint(character,out var point))markers.Add(new MapMarkerPresentation(entity,"● "+character.Definition.DisplayName,point.X/(float)MapPoint.Scale,point.Y/(float)MapPoint.Scale,PresentationKnowledge.ExactSelf,false,entity));}
            foreach(var army in _campaign.Military.Armies.OrderedArmies){var entity=new PresentationEntityRef(PresentationEntityKind.Army,army.Id.Value);if(viewer.CanReadExact(entity)&&TryArmyPoint(army,out var point))markers.Add(new MapMarkerPresentation(entity,"⚑ "+army.Name,point.X/(float)MapPoint.Scale,point.Y/(float)MapPoint.Scale,PresentationKnowledge.ExactSelf,false,entity));}
            return markers.OrderBy(x=>x.Entity).ToList().AsReadOnly();
        }
        public IReadOnlyList<MapRoutePresentation> GetKnownRoutes(PresentationViewerContext viewer)
        {
            if(viewer==null)throw new ArgumentNullException(nameof(viewer));var active=new HashSet<TravelRouteId>(_campaign.Geography.Travel.OrderedJourneys.Where(x=>x.Lifecycle==TravelLifecycle.Active&&CanSeeActor(viewer,x.Actor)).SelectMany(x=>x.Path));return _campaign.Geography.World.OrderedRoutes.Select(x=>{var a=_campaign.Geography.World.Location(x.First).MapPoint;var b=_campaign.Geography.World.Location(x.Second).MapPoint;return new MapRoutePresentation(x.Id.Value,a.X/(float)MapPoint.Scale,a.Y/(float)MapPoint.Scale,b.X/(float)MapPoint.Scale,b.Y/(float)MapPoint.Scale,"presentation.route-mode."+x.Mode.ToString().ToLowerInvariant(),active.Contains(x.Id));}).ToList().AsReadOnly();
        }
        public IReadOnlyList<MapJourneyPresentation> GetKnownJourneys(PresentationViewerContext viewer)
        {
            if(viewer==null)throw new ArgumentNullException(nameof(viewer));return _campaign.Geography.Travel.OrderedJourneys.Where(x=>x.Lifecycle==TravelLifecycle.Active&&CanSeeActor(viewer,x.Actor)).Select(x=>new MapJourneyPresentation(x.Id.Value,x.Actor.Kind+":"+x.Actor.Id,_campaign.Geography.World.Location(x.Origin).DisplayName,_campaign.Geography.World.Location(x.Destination).DisplayName,x.SegmentIndex+1,x.Path.Count,x.SegmentElapsedTicks)).ToList().AsReadOnly();
        }
        private static bool CanSeeActor(PresentationViewerContext viewer,TravelActorRef actor){PresentationEntityKind kind;switch(actor.Kind){case TravelActorKind.Character:kind=PresentationEntityKind.Character;break;case TravelActorKind.Army:kind=PresentationEntityKind.Army;break;case TravelActorKind.Caravan:kind=PresentationEntityKind.Caravan;break;default:return viewer.DevelopmentDebug;}return viewer.CanReadExact(new PresentationEntityRef(kind,actor.Id));}
        private bool TryCharacterPoint(FOC.Domain.Characters.CharacterState character,out MapPoint point){if(character.Location.Kind==FOC.Domain.Characters.CharacterLocationKind.City&&character.Location.CityId.HasValue){point=_campaign.Geography.World.LocationForCity(character.Location.CityId.Value).MapPoint;return true;}if(character.Location.Kind==FOC.Domain.Characters.CharacterLocationKind.Travelling||character.Location.Kind==FOC.Domain.Characters.CharacterLocationKind.WorldPosition){point=new MapPoint((int)character.Location.Position.X,(int)character.Location.Position.Y);return true;}point=default;return false;}
        private bool TryArmyPoint(FOC.Domain.Military.ArmyState army,out MapPoint point){if(army.Location.Kind==FOC.Domain.Military.ArmyLocationKind.City&&army.Location.CityId.HasValue){point=_campaign.Geography.World.LocationForCity(army.Location.CityId.Value).MapPoint;return true;}if(army.Location.Kind!=FOC.Domain.Military.ArmyLocationKind.City){point=new MapPoint((int)army.Location.Position.X,(int)army.Location.Position.Y);return true;}point=default;return false;}
    }
}
