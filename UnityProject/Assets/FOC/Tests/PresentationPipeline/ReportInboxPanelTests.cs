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
    public sealed class ReportInboxPanelTests
    {
        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign()
        {
            var c = VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
                Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
            var report = c.Diplomacy.Reports.OrderedReports.Single(); report.Dispatch(new WorldTimestamp(0));
            c.Clock.Advance(new WorldDuration(2)); c.Diplomacy.Information.GetRequired(report.RecipientActorId).Receive(report, c.Clock.Now);
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
            using var session = new ReportInboxSession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), reports: session);
            var before = new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
            controller.Open(new PresentationRoute(PresentationScreenId.Reports)); var search = root.Q<TextField>("report-search"); var type = root.Q<DropdownField>("report-type");
            Assert.That(root.Q<Label>("report-inbox-summary").text, Does.StartWith("1 / 1"));
            Assert.That(root.Q<Label>("report-detail-metadata").text, Does.Contain("yusuf-katip"));
            for (var i = 0; i < 50; i++) vm.Refresh(); Assert.That(root.Q<TextField>("report-search"), Is.SameAs(search));
            search.value = "ŞEHİR"; Assert.That(root.Q<ListView>("report-inbox-rows").itemsSource.Count, Is.EqualTo(1));
            type.value = "Askerî"; Assert.That(root.Q<Label>("report-inbox-empty").text, Does.Contain("filtre"));
            Assert.That(root.Q<ListView>("report-observations").itemsSource, Is.Null);
            type.value = "Tüm türler"; Assert.That(root.Q<Label>("report-inbox-summary").text, Does.StartWith("1 / 1"));
            controller.Open(new PresentationRoute(PresentationScreenId.Map)); Assert.That(root.Q<VisualElement>("report-inbox-panel").childCount, Is.Zero);
            // Reattach released controls so actual UI events prove callbacks were removed,
            // rather than passing because a detached tree cannot dispatch them.
            session.Filter("", 0); root.Add(search); root.Add(type);
            search.value = "DETACHED"; type.value = "Ticaret"; Assert.That(session.Read().Search, Is.Empty); Assert.That(session.Read().FilterIndex, Is.Zero);
            search.RemoveFromHierarchy(); type.RemoveFromHierarchy();
            for (var i = 0; i < 20; i++) { controller.Open(new PresentationRoute(PresentationScreenId.Reports)); controller.Open(new PresentationRoute(PresentationScreenId.Map)); }
            controller.Open(new PresentationRoute(PresentationScreenId.Reports)); Assert.That(root.Query<TextField>("report-search").ToList().Count, Is.EqualTo(1));
            Assert.That(new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c)), Is.EqualTo(before));
            controller.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.Read());
        }
        [Test]
        public void UnavailableDeepLinkHidesPreviouslyDisplayedDetailAndHistoryRestoresValidReport()
        {
            var c = Campaign(); var viewer = Viewer(); var id = c.Diplomacy.Reports.OrderedReports.Single().Id.Value;
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new ReportInboxSession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), reports: session);
            controller.Open(new PresentationRoute(PresentationScreenId.Reports, new PresentationEntityRef(PresentationEntityKind.Report, id)));
            Assert.That(root.Q<Label>("report-detail-metadata").text, Does.Contain("Gözlem"));
            controller.Open(new PresentationRoute(PresentationScreenId.Reports, new PresentationEntityRef(PresentationEntityKind.Report, "missing")));
            Assert.That(root.Q<Label>("report-detail-title").text, Does.Contain("erişilemiyor"));
            Assert.That(root.Q<Label>("report-detail-metadata").text, Does.Not.Contain("yusuf-katip")); Assert.That(root.Q<ListView>("report-observations").itemsSource, Is.Null);
            vm.Back(); Assert.That(root.Q<Label>("report-detail-title").text, Does.Contain(id));
        }
        [Test]
        public void HostReconfigurationDisposesOldSessionAndBindsOnlyNewRuntime()
        {
            var gameObject = new GameObject("Report host test");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FOC/Presentation/UnityDefaultRuntimeTheme");
            gameObject.AddComponent<UIDocument>().panelSettings = settings;
            try
            {
                var c = Campaign(); var viewer = Viewer(); var old = new ReportInboxSession(c, viewer); var host = gameObject.AddComponent<PresentationRuntimeHost>();
                host.Configure(new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)), reports: old);
                host.Open(new PresentationRoute(PresentationScreenId.Reports));
                var serializer = new CampaignSaveTextSerializer(); var restored = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(c))).Data!);
                var next = new ReportInboxSession(restored, viewer);
                host.Configure(new CampaignPresentationScreenSource(restored, viewer, new WorldMapPresentationDataProvider(restored)), reports: next);
                Assert.Throws<ObjectDisposedException>(() => old.Read()); host.Open(new PresentationRoute(PresentationScreenId.Reports));
                Assert.That(gameObject.GetComponent<UIDocument>().rootVisualElement.Q<Label>("report-inbox-summary").text, Does.StartWith("1 / 1"));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); UnityEngine.Object.DestroyImmediate(settings); }
        }
        private sealed class AttachedPanel : IDisposable
        {
            private readonly GameObject _object = new GameObject("Report attached UI test");
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
