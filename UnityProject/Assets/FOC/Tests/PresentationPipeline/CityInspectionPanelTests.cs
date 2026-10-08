#nullable enable
using System;
using System.Linq;
using System.Reflection;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Tests
{
    public sealed class CityInspectionPanelTests
    {
        [Test]
        public void RetainedCityControlsInspectAndNavigateWithoutMutatingCampaign()
        {
            var c = VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
                Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
            var viewer = new PresentationViewerContext(FactionId.Create("faction-ottoman-state"), new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"),
                new[] { new PresentationEntityRef(PresentationEntityKind.City, "city-istanbul"), new PresentationEntityRef(PresentationEntityKind.City, "city-bursa") });
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new CityInspectionSession(c, viewer);
            using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), city: session);
            var before = new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
            controller.Open(Route("city-bursa")); var tab = root.Q<Button>("city-tab-1");
            for (var i = 0; i < 50; i++) vm.Refresh();
            Assert.That(root.Q<Button>("city-tab-1"), Is.SameAs(tab));
            Click(tab); session.SelectRecipe("recipe-mill-flour"); vm.Refresh();
            Assert.That(root.Q<Label>("city-production-reason").text, Does.StartWith("UYGUN"));
            Click(root.Q<Button>("city-open-market"));
            Assert.That(root.Q<VisualElement>("city-inspection-panel").childCount, Is.Zero);
            // A detached old button must not re-enter the session after panel disposal.
            session.SelectTab(CityInspectionTab.Areas); Click(tab); Assert.That(session.Read().Tab, Is.EqualTo(CityInspectionTab.Areas));
            for (var i = 0; i < 20; i++) { controller.Open(Route("city-istanbul")); controller.Open(new PresentationRoute(PresentationScreenId.Map)); }
            controller.Open(Route("city-bursa"));
            Assert.That(root.Query<Button>().ToList().Count(x => x.name == "city-tab-1"), Is.EqualTo(1));
            Assert.That(new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c)), Is.EqualTo(before));
            controller.Open(Route("city-edirne"));
            Assert.That(root.Q<Label>("city-summary").text, Does.Contain("kesin bilgi yok"));
            Assert.That(root.Q<ListView>("city-inspection-rows").itemsSource, Is.Null);
            Assert.That(root.Q<Button>("city-open-market").enabledSelf, Is.False);
            controller.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.Read());
        }
        private static PresentationRoute Route(string id) => new PresentationRoute(PresentationScreenId.City, new PresentationEntityRef(PresentationEntityKind.City, id));
        private static void Click(Button b) => ((Action?)typeof(Clickable).GetField("clicked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b.clickable))?.Invoke();
    }
}
