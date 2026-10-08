#nullable enable
using System;
using System.IO;
using System.Linq;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.AI;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Geography;
using FOC.Domain.Military;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class PlayableMapTravelTests
    {
        private static readonly CharacterId Player = CharacterId.Create("hasan-aga");
        private static WorldLocationId Location(string id) => WorldLocationId.Create(id);
        private static PresentationViewerContext Viewer(params PresentationEntityRef[] extra) =>
            new PresentationViewerContext(FactionId.Create("faction-ottoman-state"), new PresentationEntityRef(PresentationEntityKind.Character, Player.Value), extra);

        [Test]
        public void DestinationSelectionAndPreviewAreReadOnlyAndUseTheRealRouteAndTimePolicy()
        {
            var campaign = Campaign(); var before = Fingerprint(campaign);
            var screen = Screen(campaign, "edirne"); var shown = screen.Map!.Travel!;
            var plan = new TravelCommandService(campaign).PreviewCharacter(Player, Location("edirne"));
            Assert.That(screen.Subject!.Value.Id, Is.EqualTo("edirne"));
            Assert.That(shown.Selected!.Value.Id, Is.EqualTo("edirne"));
            Assert.That(shown.RouteIds, Is.EqualTo(plan.Path!.Routes.Select(x => x.Value)));
            Assert.That(shown.TotalTicks, Is.EqualTo(182880));
            Assert.That(shown.RemainingTicks, Is.EqualTo(plan.DurationTicks));
            Assert.That(shown.DistanceMeters, Is.EqualTo(plan.Path.TotalDistanceMeters));
            Assert.That(shown.Stops.First(), Is.EqualTo("İstanbul"));
            Assert.That(shown.Stops.Last(), Is.EqualTo("Edirne"));
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        [TestCase("edirne")][TestCase("bursa")][TestCase("izmit")][TestCase("corlu")]
        public void OutwardAndReturnJourneysWorkWithoutTeleportOrNewState(string destination)
        {
            var campaign = Campaign(); var service = new TravelCommandService(campaign);
            var commands = new TravelPresentationCommands(campaign, Viewer());
            var armyBefore = campaign.Military.Armies.GetRequired(ArmyId.Create("army-hasan-retinue")).Location;
            Assert.That(commands.Start(Location(destination)).Succeeded, Is.True);
            var journey = campaign.Geography.Travel.ActiveFor(TravelActorRef.Character(Player))!;
            service.Advance(new WorldDuration(service.TotalJourneyTicks(journey)));
            Assert.That(service.CharacterOrigin(Player)!.Id, Is.EqualTo(Location(destination)));
            var returnAction = Screen(campaign, "istanbul").Actions.Single(x => x.ActionId == "travel.start:istanbul");
            Assert.That(returnAction.IsEnabled, Is.True);
            Assert.That(commands.Start(Location("istanbul")).Succeeded, Is.True);
            var back = campaign.Geography.Travel.ActiveFor(TravelActorRef.Character(Player))!;
            service.Advance(new WorldDuration(service.TotalJourneyTicks(back)));
            Assert.That(campaign.Characters.GetRequired(Player).Location.CityId!.Value.Value, Is.EqualTo("city-istanbul"));
            Assert.That(campaign.Military.Armies.GetRequired(ArmyId.Create("army-hasan-retinue")).Location, Is.SameAs(armyBefore));
            Assert.That(CampaignSaveMapper.ToSaveData(campaign).SaveVersion, Is.EqualTo(14));
        }

        [Test]
        public void DuplicateStartRejectsWithoutChangingIdentityClockRandomOrPayload()
        {
            var campaign = Campaign(); var commands = new TravelPresentationCommands(campaign, Viewer());
            Assert.That(commands.Start(Location("edirne")).Succeeded, Is.True);
            var before = Fingerprint(campaign);
            var rejected = commands.Start(Location("bursa"));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.MessageKey, Is.EqualTo(TravelPresentationText.Reason(TravelPreviewFailure.AlreadyTravelling)));
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
            Assert.That(campaign.Geography.Travel.OrderedJourneys.Count, Is.EqualTo(1));
        }

        [TestCase("istanbul", TravelPreviewFailure.AlreadyAtDestination)]
        [TestCase("missing", TravelPreviewFailure.DestinationUnavailable)]
        [TestCase("isolated", TravelPreviewFailure.NoRoute)]
        public void RejectedDestinationsExplainWhyAndDoNotMutate(string destination, TravelPreviewFailure expected)
        {
            var campaign = Campaign();
            campaign.Geography.World.Add(new WorldLocationDefinition(Location("isolated"), "Isolated", WorldLocationKind.Waystation,
                RegionId.Create("region"), new MapPoint(1,1), new GeoCoordinateE6(0,0), Array.Empty<string>(),
                new[] { "test-only" }, HistoricalConfidence.Interpretative, GeographyContentStatus.SliceTuning));
            var before = Fingerprint(campaign);
            var result = new TravelPresentationCommands(campaign, Viewer()).Start(Location(destination));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.MessageKey, Is.EqualTo(TravelPresentationText.Reason(expected)));
            var state = Screen(campaign, destination);
            Assert.That(state.Map!.Travel!.ReasonKey, Is.EqualTo(result.MessageKey));
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        [Test]
        public void CaptiveCannotUseStaleEnabledCommand()
        {
            var campaign = Campaign();
            var action = Screen(campaign, "edirne").Actions.Single(x => x.ActionId == "travel.start:edirne");
            Assert.That(action.IsEnabled, Is.True);
            campaign.Characters.GetRequired(Player).Capture(new CaptivityState(CharacterId.Create("ali-cavus"), CaptivitySite.InCity(CityId.Create("city-istanbul"))), campaign.Clock.Now);
            var before = Fingerprint(campaign);
            Assert.That(new TravelPresentationCommands(campaign, Viewer()).Start(Location(action.Target.Id)).MessageKey,
                Is.EqualTo(TravelPresentationText.Reason(TravelPreviewFailure.Captive)));
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        [Test]
        public void DeadCharacterAndMissingActorAreNotOfferedTravel()
        {
            var campaign = Campaign(); var character = campaign.Characters.GetRequired(Player);
            CharacterDeathRules.TryApply(character, new CharacterDeathContext(CharacterDeathCause.Illness, campaign.Clock.Now, character.Location, "Y1 rejection"), new ImportanceStoryGuardDeathPolicy(), campaign.Random);
            var before = Fingerprint(campaign);
            var service = new TravelCommandService(campaign);
            Assert.That(service.PreviewCharacter(Player, Location("edirne")).Failure, Is.EqualTo(TravelPreviewFailure.Dead));
            Assert.That(service.PreviewCharacter(CharacterId.Create("missing"), Location("edirne")).Failure, Is.EqualTo(TravelPreviewFailure.ActorUnavailable));
            Assert.That(Screen(campaign,"edirne").Actions.Single(x => x.ActionId == "travel.start:edirne").IsEnabled, Is.False);
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        [Test]
        public void ArbitraryWorldPointCannotSnapToWaystation()
        {
            var campaign = Campaign(); var corlu = campaign.Geography.World.Location(Location("corlu"));
            campaign.Characters.GetRequired(Player).MoveTo(CharacterLocation.At(new WorldPosition(corlu.MapPoint.X + 1, corlu.MapPoint.Y)), campaign.Clock.Now);
            var before = Fingerprint(campaign); var service = new TravelCommandService(campaign);
            Assert.That(service.PreviewCharacter(Player, Location("edirne")).Failure, Is.EqualTo(TravelPreviewFailure.OriginUnavailable));
            Assert.Throws<InvalidOperationException>(() => service.Start(JourneyId.Create("snap"), TravelActorRef.Character(Player), corlu.Id, Location("edirne")));
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        [Test]
        public void MidJourneySaveReloadPreservesPreviewAndContinuationExactly()
        {
            var campaign = Campaign(); var service = new TravelCommandService(campaign);
            new TravelPresentationCommands(campaign, Viewer()).Start(Location("edirne"));
            service.Advance(new WorldDuration(40001));
            var shown = Screen(campaign, null).Map!.Travel!;
            var serializer = new CampaignSaveTextSerializer();
            var data = serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign))).Data!;
            Assert.That(new CampaignSaveValidator().Validate(data).IsValid, Is.True);
            var restored = CampaignSaveMapper.ToRuntimeState(data);
            var loaded = Screen(restored, null).Map!.Travel!;
            Assert.That(loaded.RouteIds, Is.EqualTo(shown.RouteIds));
            Assert.That(loaded.RemainingTicks, Is.EqualTo(182880 - 40001));
            Assert.That(loaded.Selected, Is.EqualTo(shown.Selected));
            Assert.That(loaded.Progress, Is.EqualTo(shown.Progress));
            service.Advance(new WorldDuration(shown.RemainingTicks));
            new TravelCommandService(restored).Advance(new WorldDuration(loaded.RemainingTicks));
            Assert.That(Fingerprint(restored), Is.EqualTo(Fingerprint(campaign)));
        }

        [Test]
        public void ForeignKnownJourneyDoesNotBlockPlayerOrGrantAdvanceAuthority()
        {
            var campaign = Campaign();
            new TravelCommandService(campaign).Start(JourneyId.Create("other"), TravelActorRef.Army(ArmyId.Create("army-hasan-retinue")), Location("istanbul"), Location("bursa"));
            var viewer = Viewer(new PresentationEntityRef(PresentationEntityKind.Army,"army-hasan-retinue"));
            var screen = new CampaignPresentationQueries(campaign).BuildMap(viewer, new WorldMapPresentationDataProvider(campaign), "edirne").State;
            Assert.That(screen.Map!.Journeys.Count, Is.EqualTo(1));
            Assert.That(screen.Map.Travel!.Active, Is.False);
            Assert.That(screen.Actions.Single(x => x.ActionId == "travel.start:edirne").IsEnabled, Is.True);
            var before = Fingerprint(campaign);
            Assert.That(new TravelPresentationCommands(campaign, viewer).AdvanceOneHour().Succeeded, Is.False);
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
            Assert.That(Screen(campaign, "edirne").Map!.Journeys, Is.Empty, "No exact knowledge means no foreign journey disclosure.");
        }

        [Test]
        public void PausedClockDoesNotReportFalseAdvanceSuccess()
        {
            var campaign = Campaign(); var commands = new TravelPresentationCommands(campaign, Viewer());
            commands.Start(Location("edirne")); campaign.Clock.Pause();
            var before = Fingerprint(campaign);
            Assert.That(commands.AdvanceOneHour().Succeeded, Is.False);
            Assert.That(Screen(campaign, null).Map!.Travel!.CanAdvance, Is.False);
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        [Test]
        public void AIUsesTheSameAuthoredWaystationEligibility()
        {
            var campaign = Campaign(); var travel = new TravelCommandService(campaign);
            var j = travel.StartCharacter(JourneyId.Create("waypoint"), Player, Location("corlu"));
            travel.Advance(new WorldDuration(travel.TotalJourneyTicks(j)));
            var ai = new AITravelActionProvider(campaign, travel);
            var owner = AIDecisionOwnerRef.Character(Player);
            Assert.That(ai.CanExecute(owner, AITargetRef.WorldLocation(Location("istanbul")), campaign.Clock.Now), Is.True);
            Assert.That(ai.Execute(JourneyId.Create("return"), owner, Location("corlu"), Location("istanbul")).Origin, Is.EqualTo(Location("corlu")));
        }

        [Test]
        public void InspectingAnotherDestinationDuringTravelDoesNotChangeJourney()
        {
            var campaign = Campaign(); new TravelPresentationCommands(campaign, Viewer()).Start(Location("edirne"));
            var before = Fingerprint(campaign); var shown = Screen(campaign, "bursa").Map!.Travel!;
            Assert.That(shown.Selected!.Value.Id, Is.EqualTo("bursa"));
            Assert.That(shown.DestinationLabel, Is.EqualTo("Edirne"));
            Assert.That(shown.Stops.Last(), Is.EqualTo("Edirne"));
            Assert.That(Fingerprint(campaign), Is.EqualTo(before));
        }

        private static ScreenPresentationState Screen(CampaignRuntimeState campaign, string? destination) =>
            new CampaignPresentationScreenSource(campaign, Viewer(), new WorldMapPresentationDataProvider(campaign)).Get(
                new PresentationRoute(PresentationScreenId.Map, destination == null ? (PresentationEntityRef?)null : new PresentationEntityRef(PresentationEntityKind.WorldLocation,destination)));
        private static string Fingerprint(CampaignRuntimeState campaign) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(campaign));
        private static CampaignRuntimeState Campaign()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FallOfCavalry.sln"))) directory = directory.Parent;
            var root = Path.Combine(directory!.FullName,"UnityProject/Assets/FOC/Content/Resources/FOC");
            return VerticalSliceCampaignFactory.Create(File.ReadAllText(Path.Combine(root,"Geography/vertical-slice-locations.txt")),
                File.ReadAllText(Path.Combine(root,"Geography/vertical-slice-routes.txt")),File.ReadAllText(Path.Combine(root,"HistoricalSlice/historical-slice-content.txt")));
        }
    }
}
