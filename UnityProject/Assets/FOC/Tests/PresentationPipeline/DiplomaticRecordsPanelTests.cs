#nullable enable
using System;
using System.Linq;
using FOC.Application.Geography;
using FOC.Application.Save;
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
    public sealed class DiplomaticRecordsPanelTests
    {
        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign()
        {
            var c = VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
                Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
            c.Diplomacy.Agreements.Add(new DiplomaticAgreementState(DiplomaticAgreementId.Create("s3-ui-trade"),
                new DiplomaticActorPair(FactionId.Create("faction-ottoman-state"), FactionId.Create("faction-republic-of-venice")), DiplomaticAgreementKind.TradeAgreement,
                new WorldTimestamp(0), new WorldTimestamp(10), new[] { new DiplomaticAgreementTerm(DiplomaticAgreementTermKind.MarketAccess) }));
            return c;
        }
        private static PresentationViewerContext Viewer() => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"), new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"));
        private static VisualElement Root() => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
        [Test] public void AttachedFilterRetainsControlsAndDisposalRemovesActualCallbacks()
        {
            var c = Campaign(); var viewer = Viewer(); var root = Root(); using var attached = new AttachedPanel(root);
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new DiplomaticRecordsSession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), diplomatic: session);
            var before = new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); var search = root.Q<TextField>("diplomatic-search"); var type = root.Q<DropdownField>("diplomatic-type");
            root.Q<Foldout>("diplomatic-records-foldout").value = true;
            Assert.That(root.Q<Label>("diplomatic-summary").text, Does.StartWith("2 / 2"));
            for (var i = 0; i < 50; i++) vm.Refresh(); Assert.That(root.Q<TextField>("diplomatic-search"), Is.SameAs(search));
            search.value = "TİCARET"; type.value = "Anlaşmalar"; Assert.That(root.Q<ListView>("diplomatic-rows").itemsSource.Count, Is.EqualTo(1));
            Assert.That(root.Q<Label>("diplomatic-detail-metadata").text, Does.Contain("Şu anda yürürlükte: Hayır"));
            search.value = "missing"; Assert.That(root.Q<ListView>("diplomatic-entries").itemsSource, Is.Null);
            controller.Open(new PresentationRoute(PresentationScreenId.Map)); Assert.That(root.Q<VisualElement>("diplomatic-records-panel").childCount, Is.Zero);
            session.Filter("", 0); root.Add(search); root.Add(type); search.value = "DETACHED"; type.value = "İlişkiler";
            Assert.That(session.Read().Search, Is.Empty); Assert.That(session.Read().FilterIndex, Is.Zero); search.RemoveFromHierarchy(); type.RemoveFromHierarchy();
            for (var i = 0; i < 20; i++) { controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); controller.Open(new PresentationRoute(PresentationScreenId.Map)); }
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); Assert.That(root.Query<TextField>("diplomatic-search").ToList().Count, Is.EqualTo(1));
            Assert.That(new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c)), Is.EqualTo(before));
            Assert.That(controller.BoundNavigationCount, Is.EqualTo(12)); controller.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.Read());
        }
        [Test] public void TypedHistoryAndUnavailableLinkClearOldAgreementDetail()
        {
            var c = Campaign(); var viewer = Viewer(); var root = Root();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var s = new DiplomaticRecordsSession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), diplomatic: s);
            var target = new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, "s3-ui-trade");
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy, target));
            Assert.That(root.Q<Foldout>("diplomatic-records-foldout").value, Is.True); Assert.That(vm.Current!.Subject, Is.EqualTo(target));
            Assert.That(root.Q<ListView>("diplomatic-entries").itemsSource.Count, Is.EqualTo(1));
            controller.Open(new PresentationRoute(PresentationScreenId.Diplomacy, new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, "missing")));
            Assert.That(root.Q<Label>("diplomatic-detail-title").text, Does.Contain("erişilemiyor")); Assert.That(root.Q<ListView>("diplomatic-entries").itemsSource, Is.Null);
            vm.Back(); Assert.That(root.Q<Label>("diplomatic-detail-title").text, Does.Contain("s3-ui-trade"));
        }
        [Test] public void LoadReconfigurationDisposesOldSessionAndReadsNewState()
        {
            var go = new GameObject("Diplomatic host test"); var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FOC/Presentation/UnityDefaultRuntimeTheme"); go.AddComponent<UIDocument>().panelSettings = settings;
            try
            {
                var c = Campaign(); var viewer = Viewer(); var host = go.AddComponent<PresentationRuntimeHost>(); var old = new DiplomaticRecordsSession(c, viewer);
                host.Configure(new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)), diplomatic: old);
                var serializer = new CampaignSaveTextSerializer(); var loaded = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(serializer.Serialize(CampaignSaveMapper.ToSaveData(c))).Data!);
                host.Configure(new CampaignPresentationScreenSource(loaded, viewer, new WorldMapPresentationDataProvider(loaded)), diplomatic: new DiplomaticRecordsSession(loaded, viewer));
                Assert.Throws<ObjectDisposedException>(() => old.Read()); host.Open(new PresentationRoute(PresentationScreenId.Diplomacy));
                Assert.That(go.GetComponent<UIDocument>().rootVisualElement.Q<Label>("diplomatic-summary").text, Does.StartWith("2 / 2"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(settings); }
        }
        private sealed class AttachedPanel : IDisposable
        {
            private readonly GameObject _go = new GameObject("Diplomatic attached UI test");
            private readonly PanelSettings _settings = ScriptableObject.CreateInstance<PanelSettings>();
            public AttachedPanel(VisualElement root)
            {
                _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FOC/Presentation/UnityDefaultRuntimeTheme"); var document = _go.AddComponent<UIDocument>();
                document.panelSettings = _settings; document.rootVisualElement.Add(root); Assert.That(root.panel, Is.Not.Null);
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(_go); UnityEngine.Object.DestroyImmediate(_settings); }
        }
    }
}
