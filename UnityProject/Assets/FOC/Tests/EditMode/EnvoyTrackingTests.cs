#nullable enable
using System;
using System.Linq;
using FOC.Application.Diplomacy;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Organizations;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class EnvoyTrackingTests
    {
        private static readonly FactionId Own = FactionId.Create("faction-ottoman-state"), Foreign = FactionId.Create("faction-republic-of-venice");
        private static WorldTimestamp T(long ticks) => new WorldTimestamp(ticks);
        private static CampaignRuntimeState Campaign() => PlayableTradeTests.Campaign();
        private static string State(CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
        private static PresentationViewerContext Viewer(string? exact = null, bool debug = false) => new PresentationViewerContext(Own,
            new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"), exact == null ? null : new[] { new PresentationEntityRef(PresentationEntityKind.EnvoyMission, exact) }, debug);
        private static EnvoyMissionState Add(CampaignRuntimeState c, string id = "s2-mission", FactionId? source = null, long created = 0)
        {
            var org = new OrganizationState(OrganizationId.Create("org-" + id), "Isolated test envoy office");
            var assignment = AssignmentId.Create("assignment-" + id);
            org.AddAssignment(new AssignmentState(assignment, CharacterId.Create("yusuf-katip"), OrganizationBranch.Diplomacy, "test-envoy",
                AssignmentAuthority.Responsible, AssignmentTarget.Organization(org.Id), AssignmentPresence.RemoteCapable, T(created)));
            c.Organizations.Add(org);
            var sender = source ?? Own; var target = sender.Equals(Own) ? Foreign : Own;
            var mission = new EnvoyMissionState(EnvoyMissionId.Create(id), CharacterId.Create("yusuf-katip"), org.Id, assignment, sender, target,
                EnvoyMissionType.DeliverDemand, new DiplomaticMandate(DiplomaticAuthorityScope.Negotiate, new[] { DiplomaticActionKind.Demand }), T(created));
            var message = new DiplomaticMessageState(DiplomaticMessageId.Create("message-" + id), sender, target, MessageKind.Demand,
                MessageCarrierKind.EnvoyMission, mission.CharacterId, T(created), mission.Id);
            var action = new DiplomaticActionState(DiplomaticActionId.Create("action-" + id), sender, target, DiplomaticActionKind.Demand, mission.Id, message.Id, T(created));
            new DiplomacyCommandService(c).RegisterOrder(mission, message, action); return mission;
        }
        private static void Move(CampaignRuntimeState c, long at) { if (c.Clock.Now.Ticks < at) c.Clock.Advance(new WorldDuration(at - c.Clock.Now.Ticks)); }
        private static void Depart(CampaignRuntimeState c, EnvoyMissionState m) { Move(c, 1); new DiplomacyCommandService(c).Dispatch(DiplomaticActionId.Create("action-" + m.Id.Value), c.Clock.Now); }
        private static DiplomaticMessageState Reply(CampaignRuntimeState c, string id = "reply", string? response = "message-s2-mission", FactionId? sender = null, FactionId? recipient = null, bool delivered = true, long arrived = 4)
        {
            var message = new DiplomaticMessageState(DiplomaticMessageId.Create(id), sender ?? Foreign, recipient ?? Own, MessageKind.Response,
                MessageCarrierKind.MessengerCharacter, CharacterId.Create("mehmed-celebi-tacir"), T(0), responseTo: response == null ? null : DiplomaticMessageId.Create(response));
            c.Diplomacy.Messages.Add(message); message.Dispatch(T(2)); if (delivered) message.Deliver(recipient ?? Own, T(arrived)); Move(c, arrived); return message;
        }
        private static string Visible(EnvoyTrackingSession s)
        {
            var r = s.Read(); return string.Join("\n", r.Rows.Select(x => x.Id + x.Text)) + "\n" + r.Detail?.Metadata + "\n" + string.Join("\n", r.Detail?.Messages ?? Array.Empty<string>());
        }
        [Test]
        public void NormalStartupIsTrulyEmptyAndReadOnly()
        {
            var c = Campaign(); var before = State(c); using var s = new EnvoyTrackingSession(c, Viewer());
            Assert.That(s.Read().TotalKnown, Is.Zero); Assert.That(s.Read().Detail, Is.Null); Assert.That(s.Read().EmptyMessage, Does.Contain("Henüz")); Assert.That(State(c), Is.EqualTo(before));
        }
        [TestCase(false)][TestCase(true)]
        public void ForeignMissionsAreUnavailableEvenWithExactReferenceOrDebug(bool debug)
        {
            var c = Campaign(); Add(c); Add(c, "secret", Foreign); using var s = new EnvoyTrackingSession(c, Viewer("secret", debug)); s.OpenMission("secret");
            Assert.That(s.Read().Rows.Single().Id, Is.EqualTo("s2-mission")); Assert.That(s.Read().Detail, Is.Null); s.Filter("secret", 0); Assert.That(s.Read().Rows, Is.Empty);
        }
        [TestCase("missing")][TestCase("secret")]
        public void UnavailableSelectionNeverFallsBackToAnotherMission(string id)
        {
            var c = Campaign(); Add(c); using var s = new EnvoyTrackingSession(c, Viewer()); s.OpenMission(id);
            Assert.That(s.Read().SelectedId, Is.EqualTo(id)); Assert.That(s.Read().Detail, Is.Null);
        }
        [Test]
        public void RemoteArrivalResolutionAndReturnCannotChangeOwnKnowledge()
        {
            var c = Campaign(); var m = Add(c); Depart(c, m); Move(c, 10); using var s = new EnvoyTrackingSession(c, Viewer(debug: true)); var before = Visible(s);
            var service = new DiplomacyCommandService(c); service.Arrive(m.Id, c.Clock.Now);
            service.DeliverAndResolve(DiplomaticActionId.Create("action-s2-mission"), DiplomaticActionOutcome.Accepted, c.Clock.Now); m.AwaitAudience(); m.BeginReturn(); Move(c, 11); m.Complete(c.Clock.Now);
            Assert.That(Visible(s), Is.EqualTo(before)); Assert.That(s.Read().Rows.Single().StateIndex, Is.EqualTo(8));
            s.Filter("Tamamlandı", 0); Assert.That(s.Read().Rows, Is.Empty); s.Filter("", 6); Assert.That(s.Read().Rows, Is.Empty);
            var fields = new CampaignPresentationQueries(c).BuildDiplomacy(Viewer()).State.Details.SelectMany(x => x.Fields).Select(x => x.DisplayValue).ToArray();
            Assert.That(fields, Does.Contain("Sevk edildi; uzak sonuç bilinmiyor")); Assert.That(fields.Any(x => x.Contains("resolved")), Is.False);
        }
        [TestCase(EnvoyMissionPhase.PendingDeparture, 1)][TestCase(EnvoyMissionPhase.Travelling, 2)][TestCase(EnvoyMissionPhase.Arrived, 3)]
        [TestCase(EnvoyMissionPhase.AwaitingAudience, 4)][TestCase(EnvoyMissionPhase.Returning, 5)][TestCase(EnvoyMissionPhase.Completed, 6)][TestCase(EnvoyMissionPhase.Failed, 7)]
        public void ExplicitOwnMissionAccessSupportsEveryLifecycleWithoutGrantingNewAuthority(EnvoyMissionPhase phase, int index)
        {
            var c = Campaign(); var m = Add(c); if (phase != EnvoyMissionPhase.PendingDeparture && phase != EnvoyMissionPhase.Failed) Depart(c, m);
            Move(c, 5);
            if (phase == EnvoyMissionPhase.Failed) m.Fail(c.Clock.Now);
            if (phase >= EnvoyMissionPhase.Arrived && phase != EnvoyMissionPhase.Failed) m.Arrive(c.Clock.Now);
            if (phase == EnvoyMissionPhase.AwaitingAudience) m.AwaitAudience();
            if (phase == EnvoyMissionPhase.Returning || phase == EnvoyMissionPhase.Completed) m.BeginReturn();
            if (phase == EnvoyMissionPhase.Completed) { Move(c, 6); m.Complete(c.Clock.Now); }
            var before = State(c); using var s = new EnvoyTrackingSession(c, Viewer(m.Id.Value)); s.Filter("", index);
            Assert.That(s.Read().Rows.Single().StateIndex, Is.EqualTo(index)); Assert.That(s.Read().Detail!.Metadata, Does.Contain("Yetkili görev kaydı")); Assert.That(State(c), Is.EqualTo(before));
        }
        [Test]
        public void OnlyDeliveredOwnResponseFromExpectedCounterpartyWithExactMessageLinkIsVisible()
        {
            var c = Campaign(); var m = Add(c); Depart(c, m); Reply(c); Reply(c, "unlinked", null); Reply(c, "wrong-link", "missing");
            Reply(c, "foreign-inbox", sender: Own, recipient: Foreign); Reply(c, "wrong-sender", sender: FactionId.Create("third")); Reply(c, "in-transit", delivered: false);
            using var s = new EnvoyTrackingSession(c, Viewer()); var messages = s.Read().Detail!.Messages;
            Assert.That(messages.Count, Is.EqualTo(2)); Assert.That(messages[0], Does.Contain("teslim teyidi: bilinmiyor")); Assert.That(messages[1], Does.Contain("yanıt: reply"));
            foreach (var hidden in new[] { "unlinked", "wrong-link", "foreign-inbox", "wrong-sender", "in-transit" }) Assert.That(Visible(s), Does.Not.Contain(hidden));
        }
        [Test]
        public void FutureResponseAndMissionDoNotAppearBeforeWorldClock()
        {
            var c = Campaign(); Add(c); Add(c, "future", created: 100); var reply = Reply(c, arrived: 50); // restore clock to earlier runtime via save
            var data = CampaignSaveMapper.ToSaveData(c); data.WorldTime = 10; var restored = CampaignSaveMapper.ToRuntimeState(data);
            using var s = new EnvoyTrackingSession(restored, Viewer()); Assert.That(s.Read().TotalKnown, Is.EqualTo(1)); Assert.That(Visible(s), Does.Not.Contain(reply.Id.Value));
        }
        [TestCase("ELÇİ", 0)][TestCase("talep", 0)][TestCase("YUSUF", 1)][TestCase("yusuf", 0)]
        public void TurkishSearchUsesOnlyProjectedKnownFields(string search, int filter)
        {
            var c = Campaign(); Add(c); using var s = new EnvoyTrackingSession(c, Viewer()); s.Filter(search == "ELÇİ" ? "İLETME" : search, filter);
            Assert.That(s.Read().Rows.Single().Id, Is.EqualTo("s2-mission"));
        }
        [Test]
        public void SortIsCreationDescendingThenOrdinalIdAndDoesNotUseSecretPhase()
        {
            var c = Campaign(); Add(c, "z"); Add(c, "a"); Add(c, "latest", created: 2); Move(c, 5);
            using var s = new EnvoyTrackingSession(c, Viewer()); Assert.That(s.Read().Rows.Select(x => x.Id), Is.EqualTo(new[] { "latest", "a", "z" }));
        }
        [Test]
        public void FilterClearsOldDetailAndRouteRefreshCannotRestoreFilteredMission()
        {
            var c = Campaign(); Add(c); using var s = new EnvoyTrackingSession(c, Viewer()); s.Read(); s.Filter("absent", 0); s.OpenMission("s2-mission");
            Assert.That(s.Read().Detail, Is.Null); Assert.That(s.Read().EmptyMessage, Does.Contain("filtre")); s.Filter("", 0); Assert.That(s.Read().Detail, Is.Not.Null);
        }
        [Test]
        public void MissionMetadataDoesNotResolveLiveCharacterOrReportRegistry()
        {
            var c = Campaign(); Add(c); using var s = new EnvoyTrackingSession(c, Viewer()); var text = Visible(s);
            Assert.That(text, Does.Contain("yusuf-katip")); Assert.That(text, Does.Not.Contain("city-istanbul")); Assert.That(text, Does.Not.Contain("report-istanbul"));
            Assert.That(text, Does.Contain("İzinli işlemler: Talep")); Assert.That(text, Does.Contain("Müzakere"));
        }
        [Test]
        public void SnapshotsAreImmutableDetachedFromLiveMissionAndMessages()
        {
            var c = Campaign(); var m = Add(c); using var s = new EnvoyTrackingSession(c, Viewer()); var old = s.Read(); Depart(c, m); Reply(c);
            Assert.That(old.Rows.Single().StateIndex, Is.EqualTo(1)); Assert.That(old.Detail!.Messages.Count, Is.EqualTo(1)); Assert.That(s.Read().Rows.Single().StateIndex, Is.EqualTo(8));
            Assert.That(old.Rows.GetType().Name, Does.StartWith("ReadOnlyCollection"));
        }
        [Test]
        public void SaveRoundtripPreservesKnowledgeAndInspectionNeverChangesFullPayload()
        {
            var c = Campaign(); var m = Add(c); Depart(c, m); Reply(c); var before = State(c); var data = CampaignSaveMapper.ToSaveData(c);
            Assert.That(new CampaignSaveValidator().Validate(data).IsValid, Is.True); Assert.That(data.SaveVersion, Is.EqualTo(14));
            using var s = new EnvoyTrackingSession(c, Viewer()); var expected = Visible(s);
            for (var i = 0; i < 100; i++) { s.Filter("YUSUF", 8); s.Read(); s.Filter("", 0); s.Read(); }
            Assert.That(State(c), Is.EqualTo(before)); var restored = CampaignSaveMapper.ToRuntimeState(new CampaignSaveTextSerializer().Deserialize(before).Data!);
            using var next = new EnvoyTrackingSession(restored, Viewer()); Assert.That(Visible(next), Is.EqualTo(expected)); Assert.That(State(restored), Is.EqualTo(before));
        }
        [TestCase(-1)][TestCase(9)]
        public void InvalidFilterRejectedWithoutChangingSelection(int index)
        {
            var c = Campaign(); Add(c); using var s = new EnvoyTrackingSession(c, Viewer()); s.Read(); Assert.Throws<ArgumentOutOfRangeException>(() => s.Filter("x", index)); Assert.That(s.Read().Detail, Is.Not.Null);
        }
        [Test]
        public void DisposeRejectsAllOperationsAndMissingDispatchAdapterIsNeverEnabled()
        {
            var c = Campaign(); Add(c); var s = new EnvoyTrackingSession(c, Viewer()); s.Dispose(); s.Dispose();
            Assert.Throws<ObjectDisposedException>(() => s.Read()); Assert.Throws<ObjectDisposedException>(() => s.Filter("", 0)); Assert.Throws<ObjectDisposedException>(() => s.OpenMission("x"));
            Assert.That(new CampaignPresentationQueries(c).BuildDiplomacy(Viewer()).State.Actions.All(x => !x.IsEnabled), Is.True);
        }
    }
}
