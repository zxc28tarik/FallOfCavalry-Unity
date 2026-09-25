using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.AI;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Geography;
using FOC.Domain.Military;
using FOC.Domain.Time;
using FOC.Domain.Validation;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class HistoricalGeographyTravelTests
    {
        [Test]
        public void AuthoredSliceLoadsStableLocationsRoutesSourcesAndProjection()
        {
            var campaign=Campaign();var world=campaign.Geography.World;
            Assert.That(world.LocationCount,Is.EqualTo(12));Assert.That(world.RouteCount,Is.EqualTo(11));
            var istanbul=world.Location(WorldLocationId.Create("istanbul"));Assert.That(istanbul.DisplayName,Is.EqualTo("İstanbul"));Assert.That(istanbul.CityId!.Value.Value,Is.EqualTo("city-home"));Assert.That(istanbul.SourceIds,Does.Contain("SRC_ALTUNAN_2006"));
            Assert.That(world.OrderedLocations.All(x=>x.Status==GeographyContentStatus.ProductionCandidate),Is.True);Assert.That(world.OrderedRoutes.All(x=>x.Status==GeographyContentStatus.SliceTuning),Is.True);
            var sourceCatalog=Read("geography-sources.txt");foreach(var sourceId in world.OrderedLocations.SelectMany(x=>x.SourceIds).Concat(world.OrderedRoutes.SelectMany(x=>x.SourceIds)).Distinct())Assert.That(sourceCatalog,Does.Contain(sourceId+"|"),sourceId);
            Assert.That(world.OrderedLocations.Select(x=>x.MapPoint).Distinct().Count(),Is.EqualTo(world.LocationCount));Assert.That(new DeterministicRoutePathfinder().Find(world,WorldLocationId.Create("edirne"),WorldLocationId.Create("bursa")).Routes,Is.Not.Empty);
            Assert.That(new GeographyInvariantValidator().Validate(campaign).IsValid,Is.True);
        }

        [Test]
        public void EqualCostPathUsesOrdinalRouteIdTieBreakAndIsInsertionIndependent()
        {
            var first=Diamond(false);var second=Diamond(true);var pathfinder=new DeterministicRoutePathfinder();
            var a=WorldLocationId.Create("a");var d=WorldLocationId.Create("d");
            Assert.That(pathfinder.Find(first,a,d).Routes.Select(x=>x.Value),Is.EqualTo(new[]{"route-a-b","route-b-d"}));
            Assert.That(pathfinder.Find(second,a,d).Routes,Is.EqualTo(pathfinder.Find(first,a,d).Routes));
        }

        [Test]
        public void CharacterMidJourneySaveRoundTripContinuesToExactArrival()
        {
            var campaign=Campaign();var service=new TravelCommandService(campaign);var actor=TravelActorRef.Character(CharacterId.Create("slice-player-sipahi"));
            var journey=service.Start(JourneyId.Create("journey-save"),actor,WorldLocationId.Create("istanbul"),WorldLocationId.Create("bursa"));
            var total=service.TotalJourneyTicks(journey);var half=total/2;var departure=campaign.Clock.Now;service.Advance(new WorldDuration(half));
            Assert.That(campaign.Clock.Now.Ticks,Is.EqualTo(departure.Ticks+half));
            Assert.That(journey.Lifecycle,Is.EqualTo(TravelLifecycle.Active));Assert.That(campaign.Characters.GetRequired(CharacterId.Create(actor.Id)).Location.Kind,Is.EqualTo(CharacterLocationKind.Travelling));
            var serializer=new CampaignSaveTextSerializer();var text=serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign));var read=serializer.Deserialize(text);Assert.That(read.Success,Is.True,read.Error);
            var restored=CampaignSaveMapper.ToRuntimeState(read.Data!);var restoredJourney=restored.Geography.Travel.GetRequired(journey.Id);Assert.That(restoredJourney.SegmentIndex,Is.EqualTo(journey.SegmentIndex));Assert.That(restoredJourney.SegmentElapsedTicks,Is.EqualTo(journey.SegmentElapsedTicks));
            var remaining=total-half;service.Advance(new WorldDuration(remaining));var continuation=new TravelCommandService(restored);continuation.Advance(new WorldDuration(remaining));
            Assert.That(restoredJourney.Lifecycle,Is.EqualTo(TravelLifecycle.Arrived));Assert.That(restored.Characters.GetRequired(CharacterId.Create(actor.Id)).Location.CityId!.Value.Value,Is.EqualTo("city-other"));
            Assert.That(restored.Clock.Now,Is.EqualTo(campaign.Clock.Now));Assert.That(restoredJourney.ArrivedAt,Is.EqualTo(journey.ArrivedAt));Assert.That(restored.Characters.GetRequired(CharacterId.Create(actor.Id)).Location.Kind,Is.EqualTo(campaign.Characters.GetRequired(CharacterId.Create(actor.Id)).Location.Kind));
        }

        [Test]
        public void InvalidUnavailableAndDisconnectedTravelIsRejectedWithoutMutation()
        {
            var deadCampaign=Campaign();var dead=deadCampaign.Characters.GetRequired(CharacterId.Create("slice-player-sipahi"));var decision=CharacterDeathRules.TryApply(dead,new CharacterDeathContext(CharacterDeathCause.Illness,deadCampaign.Clock.Now,dead.Location,"travel rejection proof"),new ImportanceStoryGuardDeathPolicy(),deadCampaign.Random);Assert.That(decision.Allowed,Is.True);
            Assert.Throws<InvalidOperationException>(()=>new TravelCommandService(deadCampaign).Start(JourneyId.Create("dead"),TravelActorRef.Character(dead.Id),WorldLocationId.Create("istanbul"),WorldLocationId.Create("bursa")));Assert.That(deadCampaign.Geography.Travel.OrderedJourneys,Is.Empty);
            var captiveCampaign=Campaign();var captive=captiveCampaign.Characters.GetRequired(CharacterId.Create("slice-player-sipahi"));captive.Capture(new CaptivityState(CharacterId.Create("commander-b"),CaptivitySite.InCity(CityId.Create("city-home"))),captiveCampaign.Clock.Now);
            Assert.Throws<InvalidOperationException>(()=>new TravelCommandService(captiveCampaign).Start(JourneyId.Create("captive"),TravelActorRef.Character(captive.Id),WorldLocationId.Create("istanbul"),WorldLocationId.Create("bursa")));Assert.That(captiveCampaign.Geography.Travel.OrderedJourneys,Is.Empty);
            var campaign=Campaign();var service=new TravelCommandService(campaign);campaign.Geography.World.Add(Location("isolated"));
            Assert.Throws<KeyNotFoundException>(()=>service.Start(JourneyId.Create("missing-actor"),TravelActorRef.Character(CharacterId.Create("missing")),WorldLocationId.Create("istanbul"),WorldLocationId.Create("bursa")));
            Assert.Throws<KeyNotFoundException>(()=>service.Start(JourneyId.Create("missing-destination"),TravelActorRef.Character(CharacterId.Create("slice-player-sipahi")),WorldLocationId.Create("istanbul"),WorldLocationId.Create("missing")));
            Assert.Throws<InvalidOperationException>(()=>service.Start(JourneyId.Create("disconnected"),TravelActorRef.Character(CharacterId.Create("slice-player-sipahi")),WorldLocationId.Create("istanbul"),WorldLocationId.Create("isolated")));Assert.That(campaign.Geography.Travel.OrderedJourneys,Is.Empty);
        }

        [Test]
        public void ArmyAndCaravanUseTheSameClockRoutesAndLifecycle()
        {
            var campaign=Campaign();var service=new TravelCommandService(campaign);
            var armyState=campaign.Military.Armies.GetRequired(ArmyId.Create("army-a"));var armyUnits=armyState.OrderedUnits.Select(x=>x.Id.Value+":"+x.Headcount).ToArray();var armySupply=armyState.Supply.OrderedGoods.Select(x=>x.GoodId.Value+":"+x.Quantity.Value).ToArray();var commander=armyState.Commander?.CharacterId;var caravanState=campaign.Economy.Caravans.GetRequired(CaravanId.Create("caravan-proof"));var cargo=caravanState.Cargo.OrderedCargo.Select(x=>x.GoodId.Value+":"+x.Quantity.Value).ToArray();var cash=caravanState.CashBalance;var capacity=caravanState.WeightCapacity;
            var army=service.Start(JourneyId.Create("journey-army"),TravelActorRef.Army(ArmyId.Create("army-a")),WorldLocationId.Create("istanbul"),WorldLocationId.Create("bursa"));
            var caravan=service.Start(JourneyId.Create("journey-caravan"),TravelActorRef.Caravan(CaravanId.Create("caravan-proof")),WorldLocationId.Create("istanbul"),WorldLocationId.Create("bursa"));
            Assert.That(campaign.Military.Armies.GetRequired(ArmyId.Create("army-a")).Location.Kind,Is.EqualTo(ArmyLocationKind.InTransit));Assert.That(campaign.Economy.Caravans.GetRequired(CaravanId.Create("caravan-proof")).LocationStage,Is.EqualTo(CaravanLocationStage.InTransit));
            service.Advance(new WorldDuration(Math.Max(service.TotalJourneyTicks(army),service.TotalJourneyTicks(caravan))+1));
            Assert.That(army.Lifecycle,Is.EqualTo(TravelLifecycle.Arrived));Assert.That(caravan.Lifecycle,Is.EqualTo(TravelLifecycle.Arrived));Assert.That(campaign.Economy.Caravans.GetRequired(CaravanId.Create("caravan-proof")).LocationStage,Is.EqualTo(CaravanLocationStage.AtDestination));
            Assert.That(campaign.Military.Armies.GetRequired(armyState.Id),Is.SameAs(armyState));Assert.That(armyState.OrderedUnits.Select(x=>x.Id.Value+":"+x.Headcount),Is.EqualTo(armyUnits));Assert.That(armyState.Supply.OrderedGoods.Select(x=>x.GoodId.Value+":"+x.Quantity.Value),Is.EqualTo(armySupply));Assert.That(armyState.Commander?.CharacterId,Is.EqualTo(commander));
            Assert.That(campaign.Economy.Caravans.GetRequired(caravanState.Id),Is.SameAs(caravanState));Assert.That(caravanState.Cargo.OrderedCargo.Select(x=>x.GoodId.Value+":"+x.Quantity.Value),Is.EqualTo(cargo));Assert.That(caravanState.CashBalance,Is.EqualTo(cash));Assert.That(caravanState.WeightCapacity,Is.EqualTo(capacity));
        }

        [Test]
        public void AIUsesRealTravelServiceAndDifficultyCannotChangeSpeed()
        {
            var campaign=Campaign();var travel=new TravelCommandService(campaign);var provider=new AITravelActionProvider(campaign,travel);var owner=AIDecisionOwnerRef.Character(CharacterId.Create("slice-player-sipahi"));var target=AITargetRef.WorldLocation(WorldLocationId.Create("edirne"));
            Assert.That(provider.CanExecute(owner,target,campaign.Clock.Now),Is.True);var journey=provider.Execute(JourneyId.Create("journey-ai"),owner,WorldLocationId.Create("istanbul"),WorldLocationId.Create("edirne"));Assert.That(journey.Lifecycle,Is.EqualTo(TravelLifecycle.Active));
            Assert.That(typeof(SliceTravelTimePolicy).GetMethods().SelectMany(x=>x.GetParameters()).Any(x=>x.Name!=null&&x.Name.IndexOf("difficulty",StringComparison.OrdinalIgnoreCase)>=0),Is.False);
            var blocked=Campaign();blocked.Geography.World.Add(Location("isolated"));var blockedProvider=new AITravelActionProvider(blocked,new TravelCommandService(blocked));Assert.That(blockedProvider.CanExecute(owner,AITargetRef.WorldLocation(WorldLocationId.Create("isolated")),blocked.Clock.Now),Is.False);
        }

        [Test]
        public void PresentationLinksCitiesAndDisplaysExactlyTheDomainJourneyPath()
        {
            var campaign=Campaign();var actor=TravelActorRef.Character(CharacterId.Create("slice-player-sipahi"));var journey=new TravelCommandService(campaign).Start(JourneyId.Create("journey-map"),actor,WorldLocationId.Create("istanbul"),WorldLocationId.Create("edirne"));var player=new PresentationEntityRef(PresentationEntityKind.Character,actor.Id);var viewer=new PresentationViewerContext(FactionId.Create("faction-proof"),player,new[]{player});var provider=new WorldMapPresentationDataProvider(campaign);
            var istanbul=provider.GetKnownMarkers(viewer).Single(x=>x.Entity.Equals(new PresentationEntityRef(PresentationEntityKind.WorldLocation,"istanbul")));Assert.That(istanbul.NavigationTarget,Is.EqualTo(new PresentationEntityRef(PresentationEntityKind.City,"city-home")));
            Assert.That(provider.GetKnownRoutes(viewer).Where(x=>x.Active).Select(x=>x.RouteId),Is.EquivalentTo(journey.Path.Select(x=>x.Value)));var shown=provider.GetKnownJourneys(viewer).Single();Assert.That(shown.SegmentCount,Is.EqualTo(journey.Path.Count));
        }

        [Test]
        public void GeographyKnowledgeDoesNotRevealForeignLiveJourney()
        {
            var campaign=Campaign();new TravelCommandService(campaign).Start(JourneyId.Create("foreign-army"),TravelActorRef.Army(ArmyId.Create("army-b")),WorldLocationId.Create("bursa"),WorldLocationId.Create("istanbul"));
            var player=new PresentationEntityRef(PresentationEntityKind.Character,"slice-player-sipahi");var viewer=new PresentationViewerContext(FactionId.Create("faction-proof"),player,new[]{player});var provider=new WorldMapPresentationDataProvider(campaign);
            Assert.That(provider.GetKnownMarkers(viewer).Count(x=>x.Entity.Kind==PresentationEntityKind.WorldLocation),Is.EqualTo(12));Assert.That(provider.GetKnownMarkers(viewer).Any(x=>x.Entity.Equals(player)),Is.True);Assert.That(provider.GetKnownJourneys(viewer),Is.Empty);
        }

        [Test]
        public void MigrationV12ToV13CreatesNoFakeGeographyOrJourney()
        {
            var source=new CampaignSaveData{SaveVersion=12,CampaignId="legacy",GameVersion="0.12",ContentDataVersion="legacy",WorldSeed=1,WorldGenRevision=1,RngState=1};var migrated=new CampaignSaveV12ToV13Migration().Apply(source);
            Assert.That(migrated.SaveVersion,Is.EqualTo(13));Assert.That(migrated.WorldLocations,Is.Empty);Assert.That(migrated.TravelRoutes,Is.Empty);Assert.That(migrated.TravelJourneys,Is.Empty);
        }

        [Test]
        public void WorldMapBenchmarksRecordGraphPathMarkerAndRouteProjectionWork()
        {
            var watch=Stopwatch.StartNew();var campaign=Campaign();watch.Stop();TestContext.Progress.WriteLine("WORLD_MAP_PERFORMANCE graph_loads=1 elapsed_ms="+watch.ElapsedMilliseconds);Assert.That(campaign.Geography.World.LocationCount,Is.EqualTo(12));
            var finder=new DeterministicRoutePathfinder();foreach(var count in new[]{1,100,1000}){watch.Restart();for(var i=0;i<count;i++)finder.Find(campaign.Geography.World,WorldLocationId.Create("istanbul"),i%2==0?WorldLocationId.Create("edirne"):WorldLocationId.Create("bursa"));watch.Stop();TestContext.Progress.WriteLine("WORLD_MAP_PERFORMANCE path_queries="+count+" elapsed_ms="+watch.ElapsedMilliseconds);}
            var player=new PresentationEntityRef(PresentationEntityKind.Character,"slice-player-sipahi");var viewer=new PresentationViewerContext(FactionId.Create("faction-proof"),player,new[]{player});var provider=new WorldMapPresentationDataProvider(campaign);watch.Restart();for(var i=0;i<1000;i++)provider.GetKnownMarkers(viewer);watch.Stop();TestContext.Progress.WriteLine("WORLD_MAP_PERFORMANCE marker_projections=1000 elapsed_ms="+watch.ElapsedMilliseconds);Assert.That(provider.GetKnownMarkers(viewer),Is.Not.Empty);
            watch.Restart();for(var i=0;i<1000;i++)provider.GetKnownRoutes(viewer);watch.Stop();TestContext.Progress.WriteLine("WORLD_MAP_PERFORMANCE route_projections=1000 elapsed_ms="+watch.ElapsedMilliseconds);Assert.That(provider.GetKnownRoutes(viewer).Count,Is.EqualTo(11));
        }

        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign()=>VerticalSliceCampaignFactory.Create(Read("vertical-slice-locations.txt"),Read("vertical-slice-routes.txt"));
        private static string Read(string name){var root=FindRoot();return File.ReadAllText(Path.Combine(root,"UnityProject","Assets","FOC","Content","Resources","FOC","Geography",name));}
        private static string FindRoot(){var current=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);while(current!=null){if(File.Exists(Path.Combine(current.FullName,"FallOfCavalry.sln")))return current.FullName;current=current.Parent;}throw new DirectoryNotFoundException();}
        private static WorldGeography Diamond(bool reverse)
        {
            var world=new WorldGeography();foreach(var id in new[]{"a","b","c","d"})world.Add(Location(id));var routes=new[]{Route("route-a-b","a","b"),Route("route-b-d","b","d"),Route("route-a-c","a","c"),Route("route-c-d","c","d")};foreach(var route in reverse?routes.Reverse():routes)world.Add(route);return world;
        }
        private static WorldLocationDefinition Location(string id)=>new WorldLocationDefinition(WorldLocationId.Create(id),id,WorldLocationKind.Waystation,RegionId.Create("region"),new MapPoint(1,1),new GeoCoordinateE6(0,0),Array.Empty<string>(),new[]{"source"},HistoricalConfidence.Interpretative,GeographyContentStatus.SliceTuning);
        private static TravelRouteDefinition Route(string id,string a,string b)=>new TravelRouteDefinition(TravelRouteId.Create(id),WorldLocationId.Create(a),WorldLocationId.Create(b),RouteMode.Road,10,new[]{"source"},HistoricalConfidence.Interpretative,GeographyContentStatus.SliceTuning);
    }
}
