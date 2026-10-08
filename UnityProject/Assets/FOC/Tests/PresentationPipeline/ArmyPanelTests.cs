#nullable enable
using System;
using System.Linq;
using System.Reflection;
using FOC.Application.Geography;
using FOC.Application.Military;
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
    public sealed class ArmyPanelTests
    {
        [Test]
        public void RetainedControlsRecruitOnceAndDisplayFiniteSource()
        {
            var c = Campaign(); var viewer = Viewer(); var root = Shell();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new ArmyPanelSession(c, viewer);
            session.Select("army-hasan-retinue", ArmyOrderKind.Recruit, "recruitment-hasan-retinue", "troop-cebeli", "2");
            using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), army: session);
            controller.Open(Route()); var prepare = root.Q<Button>("army-prepare");
            for (var i = 0; i < 50; i++) vm.Refresh();
            Assert.That(root.Q<Button>("army-prepare"), Is.SameAs(prepare));
            Click(prepare); var confirm = root.Q<Button>("army-confirm");
            Click(confirm); Click(confirm);
            Assert.That(c.Military.Armies.GetRequired(ArmyId.Create("army-hasan-retinue")).Headcount, Is.EqualTo(14));
            Assert.That(root.Q<Label>("army-summary").text, Does.Contain("Kalıcı Soldier: 6"));
            Assert.That(root.Q<DropdownField>("army-source").value, Does.Contain("kalan 18"));
        }
        [Test]
        public void PayrollAndNpcSupplyHaveExplicitBlocksAndLeavingReleasesCallbacks()
        {
            var c = Campaign(); var viewer = Viewer(); var root = Shell();
            using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new ArmyPanelSession(c, viewer);
            using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), army: session);
            controller.Open(Route()); Click(root.Q<Button>("army-prepare")); var old = root.Q<Button>("army-confirm");
            for (var i = 0; i < 20; i++) { controller.Open(new PresentationRoute(PresentationScreenId.Map)); controller.Open(Route()); }
            Click(old); Assert.That(c.Military.Armies.GetRequired(ArmyId.Create("army-hasan-retinue")).Headcount, Is.EqualTo(12));
            Assert.That(root.Query<Button>().ToList().Count(x => x.name == "army-confirm"), Is.EqualTo(1));
            // Detached template: value setters do not dispatch panel change events.
            // Actual attached control input is exercised by DevelopmentArmyAcceptance.
            session.Select("army-hasan-retinue", ArmyOrderKind.Payroll, "", "", "1"); vm.Refresh();
            Assert.That(root.Q<Button>("army-prepare").enabledSelf, Is.False); Assert.That(root.Q<Label>("army-reason").text, Does.Contain("yükümlülüğü yok"));
            session.Select("army-hasan-retinue", ArmyOrderKind.CaravanSupply, "", "", "1"); vm.Refresh();
            Assert.That(root.Q<DropdownField>("army-source").choices, Is.Empty); Assert.That(root.Q<Button>("army-prepare").enabledSelf, Is.False);
            controller.Dispose(); Click(old); Assert.That(session.Confirm().Succeeded, Is.False);
        }
        private static VisualElement Shell() => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
        private static PresentationRoute Route() => new PresentationRoute(PresentationScreenId.Army, new PresentationEntityRef(PresentationEntityKind.Army, "army-hasan-retinue"));
        private static void Click(Button b) => ((Action?)typeof(Clickable).GetField("clicked", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b.clickable))?.Invoke();
        private static CampaignRuntimeState Campaign() => VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
            Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
        private static PresentationViewerContext Viewer() => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"),
            new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"), new[] { new PresentationEntityRef(PresentationEntityKind.Army, "army-hasan-retinue"), new PresentationEntityRef(PresentationEntityKind.City, "city-istanbul") });
    }
}
