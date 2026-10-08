#nullable enable
using System;
using System.Linq;
using System.Reflection;
using FOC.Application.Geography;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Tests
{
    public sealed class TradePanelTests
    {
        [Test]
        public void DefaultPlayerSeesTheMarketButCannotUseTheNpcCaravan()
        {
            var c = Campaign(); var viewer = Viewer(false); var root = Shell();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), trade: new TradePanelSession(c, viewer));
            controller.Open(Route());
            Assert.That(root.Q<DropdownField>("trade-good").choices.Count, Is.GreaterThan(0));
            Assert.That(root.Q<Button>("trade-purchase").enabledSelf, Is.False);
            Assert.That(root.Q<DropdownField>("trade-caravan").choices, Is.Empty);
            Assert.That(root.Q<Label>("trade-reasons").text, Does.Contain("yöneticisi değilsin"));
        }

        [Test]
        public void RefreshRetainsControlsAndOneConfirmationExecutesExactlyOnce()
        {
            var c = Campaign(); var viewer = Viewer(true); var root = Shell();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            var session = new TradePanelSession(c, viewer); session.Select("city-bursa", "silk-cloth", "caravan-bursa-istanbul", "1");
            using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), trade: session);
            controller.Open(Route()); var purchase = root.Q<Button>("trade-purchase"); var field = root.Q<TextField>("trade-quantity");
            for (var i = 0; i < 50; i++) vm.Refresh();
            Assert.That(root.Q<Button>("trade-purchase"), Is.SameAs(purchase)); Assert.That(root.Q<TextField>("trade-quantity"), Is.SameAs(field));
            var cargo = c.Economy.Caravans.GetRequired(CaravanId.Create("caravan-bursa-istanbul")).Cargo;
            var before = cargo.QuantityOf(TradeGoodId.Create("silk-cloth"));
            Click(purchase); var confirm = root.Q<Button>("trade-confirm"); Assert.That(confirm.enabledSelf, Is.True);
            Click(confirm); Click(confirm);
            Assert.That(cargo.QuantityOf(TradeGoodId.Create("silk-cloth")), Is.EqualTo(before + 1));
            Assert.That(root.Q<Label>("trade-stock").text, Does.Contain("Kervanda: 5"));
        }

        [Test]
        public void NavigationAndDisposalReleaseCallbacksAndPendingOrders()
        {
            var c = Campaign(); var viewer = Viewer(true); var root = Shell();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            var session = new TradePanelSession(c, viewer); session.Select("city-bursa", "silk-cloth", "caravan-bursa-istanbul", "1");
            using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), trade: session);
            controller.Open(Route()); Click(root.Q<Button>("trade-purchase")); var oldConfirm = root.Q<Button>("trade-confirm");
            for (var i = 0; i < 20; i++) { controller.Open(new PresentationRoute(PresentationScreenId.Map)); controller.Open(Route()); }
            Assert.That(root.Query<Button>().ToList().Count(x => x.name == "trade-confirm"), Is.EqualTo(1));
            Click(oldConfirm); Assert.That(session.Read().Confirmation, Is.Null);
            var oldPurchase = root.Q<Button>("trade-purchase"); controller.Dispose(); Click(oldPurchase); Click(oldConfirm);
            Assert.That(c.Economy.Caravans.GetRequired(CaravanId.Create("caravan-bursa-istanbul")).Cargo.QuantityOf(TradeGoodId.Create("silk-cloth")), Is.EqualTo(4));
        }

        private static VisualElement Shell() => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
        private static PresentationRoute Route() => new PresentationRoute(PresentationScreenId.Trade, new PresentationEntityRef(PresentationEntityKind.City, "city-bursa"));
        private static void Click(Button b) => ((Action?)typeof(Clickable).GetField("clicked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b.clickable))?.Invoke();
        private static CampaignRuntimeState Campaign() => VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
            Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
        private static PresentationViewerContext Viewer(bool manager) => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"),
            new PresentationEntityRef(PresentationEntityKind.Character, manager ? "mehmed-celebi-tacir" : "hasan-aga"),
            new[] { new PresentationEntityRef(PresentationEntityKind.City, "city-bursa") }.Concat(manager
                ? new[] { new PresentationEntityRef(PresentationEntityKind.Caravan, "caravan-bursa-istanbul") } : Array.Empty<PresentationEntityRef>()));
    }
}
