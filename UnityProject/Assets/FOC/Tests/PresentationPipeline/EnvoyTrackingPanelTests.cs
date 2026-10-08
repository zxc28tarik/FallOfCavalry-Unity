#nullable enable
using System;
using System.Linq;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Tests
{
    public sealed class EnvoyTrackingPanelTests
    {
        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign()
        {
            var c = VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
                Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
            var org = new FOC.Domain.Organizations.OrganizationState(OrganizationId.Create("s2-test-org"), "Isolated test office");
            var assignment = AssignmentId.Create("s2-test-assignment");
            org.AddAssignment(new FOC.Domain.Organizations.AssignmentState(assignment, CharacterId.Create("yusuf-katip"), FOC.Domain.Organizations.OrganizationBranch.Diplomacy, "test-envoy",
                FOC.Domain.Organizations.AssignmentAuthority.Responsible, FOC.Domain.Organizations.AssignmentTarget.Organization(org.Id), FOC.Domain.Organizations.AssignmentPresence.RemoteCapable, new WorldTimestamp(0)));
            c.Organizations.Add(org);
            var mission = new EnvoyMissionState(EnvoyMissionId.Create("s2-test-mission"), CharacterId.Create("yusuf-katip"), org.Id, assignment,
                FactionId.Create("faction-ottoman-state"), FactionId.Create("faction-republic-of-venice"), EnvoyMissionType.DeliverDemand,
                new DiplomaticMandate(DiplomaticAuthorityScope.Negotiate, new[] { DiplomaticActionKind.Demand }), new WorldTimestamp(0));
            c.Diplomacy.Missions.Add(mission);
            return c;
        }
        private static PresentationViewerContext Viewer() => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"), new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"));
        [Test]
        public void RetainedControlsFilterDeliveredInformationWithoutChangingSave()
        {
            var c = Campaign(); var viewer = Viewer();
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
            using var attached = new AttachedPanel(root);
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new EnvoyTrackingSession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), envoys: session);
            var before = new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); var search = root.Q<TextField>("envoy-search"); var type = root.Q<DropdownField>("envoy-type");
            Assert.That(root.Q<Label>("envoy-inbox-summary").text, Does.StartWith("1 / 1"));
            Assert.That(root.Q<Label>("envoy-detail-metadata").text, Does.Contain("yusuf-katip"));
            for (var i = 0; i < 50; i++) vm.Refresh(); Assert.That(root.Q<TextField>("envoy-search"), Is.SameAs(search));
            search.value = "YUSUF"; Assert.That(root.Q<ListView>("envoy-inbox-rows").itemsSource.Count, Is.EqualTo(1));
            type.value = "Yolda"; Assert.That(root.Q<Label>("envoy-inbox-empty").text, Does.Contain("filtre"));
            Assert.That(root.Q<ListView>("envoy-messages").itemsSource, Is.Null);
            type.value = "Tüm bilinen durumlar"; Assert.That(root.Q<Label>("envoy-inbox-summary").text, Does.StartWith("1 / 1"));
            controller.Open(new PresentationRoute(PresentationScreenId.Map)); Assert.That(root.Q<VisualElement>("envoy-tracking-panel").childCount, Is.Zero);
            // Reattach released controls so actual UI events prove callbacks were removed,
            // rather than passing because a detached tree cannot dispatch them.
            session.Filter("", 0); root.Add(search); root.Add(type);
            search.value = "DETACHED"; type.value = "Tamamlandı"; Assert.That(session.Read().Search, Is.Empty); Assert.That(session.Read().FilterIndex, Is.Zero);
            search.RemoveFromHierarchy(); type.RemoveFromHierarchy();
            for (var i = 0; i < 20; i++) { controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); controller.Open(new PresentationRoute(PresentationScreenId.Map)); }
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); Assert.That(root.Query<TextField>("envoy-search").ToList().Count, Is.EqualTo(1));
            Assert.That(new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c)), Is.EqualTo(before));
            controller.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.Read());
        }
        [Test]
        public void UnavailableDeepLinkHidesPreviouslyDisplayedDetailAndHistoryRestoresValidMission()
        {
            var c = Campaign(); var viewer = Viewer(); var id = c.Diplomacy.Missions.OrderedMissions.Single().Id.Value;
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new EnvoyTrackingSession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), envoys: session);
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy, new PresentationEntityRef(PresentationEntityKind.EnvoyMission, id)));
            Assert.That(root.Q<Label>("envoy-detail-metadata").text, Does.Contain("Oluşturma"));
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy, new PresentationEntityRef(PresentationEntityKind.EnvoyMission, "missing")));
            Assert.That(root.Q<Label>("envoy-detail-title").text, Does.Contain("erişilemiyor"));
            Assert.That(root.Q<Label>("envoy-detail-metadata").text, Does.Not.Contain("yusuf-katip")); Assert.That(root.Q<ListView>("envoy-messages").itemsSource, Is.Null);
            vm.Back(); Assert.That(root.Q<Label>("envoy-detail-title").text, Does.Contain(id));
        }
        [Test]
        public void HostReconfigurationDisposesOldSessionAndBindsOnlyNewRuntime()
        {
            var gameObject = new GameObject("Envoy host test");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FOC/Presentation/UnityDefaultRuntimeTheme");
            gameObject.AddComponent<UIDocument>().panelSettings = settings;
            try
            {
                var c = Campaign(); var viewer = Viewer(); var old = new EnvoyTrackingSession(c, viewer); var host = gameObject.AddComponent<PresentationRuntimeHost>();
                host.Configure(new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)), envoys: old);
                host.Open(new PresentationRoute(PresentationScreenId.Diplomacy));
                var serializer = new CampaignSaveTextSerializer(); var restored = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(c))).Data!);
                var next = new EnvoyTrackingSession(restored, viewer);
                host.Configure(new CampaignPresentationScreenSource(restored, viewer, new WorldMapPresentationDataProvider(restored)), envoys: next);
                Assert.Throws<ObjectDisposedException>(() => old.Read()); host.Open(new PresentationRoute(PresentationScreenId.Diplomacy));
                Assert.That(gameObject.GetComponent<UIDocument>().rootVisualElement.Q<Label>("envoy-inbox-summary").text, Does.StartWith("1 / 1"));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); UnityEngine.Object.DestroyImmediate(settings); }
        }
        private sealed class AttachedPanel : IDisposable
        {
            private readonly GameObject _object = new GameObject("Envoy attached UI test");
            private readonly PanelSettings _settings = ScriptableObject.CreateInstance<PanelSettings>();
            public AttachedPanel(VisualElement root)
            {
                _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FOC/Presentation/UnityDefaultRuntimeTheme");
                var document = _object.AddComponent<UIDocument>(); document.panelSettings = _settings;
                document.rootVisualElement.Add(root);
                Assert.That(root.panel, Is.Not.Null, "UI change callbacks require an attached panel.");
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(_object); UnityEngine.Object.DestroyImmediate(_settings); }
        }
    }
}
