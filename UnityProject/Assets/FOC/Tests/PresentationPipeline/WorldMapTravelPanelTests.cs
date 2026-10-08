#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Tests
{
    public sealed class WorldMapTravelPanelTests
    {
        [Test]
        public void CoincidentMapLabelsStaySeparatedWithinCompactCanvas()
        {
            var size=new Vector2(700,380);var occupied=new List<Rect>();
            var placement=typeof(PresentationShellController).Assembly.GetType("FOC.Presentation.Unity.WorldMapTravelPanel")!
                .GetMethod("FindLabelPosition",BindingFlags.Static|BindingFlags.NonPublic)!;
            for(var i=0;i<12;i++)
            {
                var p=(Vector2)placement.Invoke(null,new object[]{.8f,.9f,size,occupied});
                var box=new Rect(p.x*size.x-59,p.y*size.y-18,118,36);
                Assert.That(occupied.Any(r=>r.Overlaps(box)),Is.False);
                Assert.That(box.xMin,Is.GreaterThanOrEqualTo(0));Assert.That(box.xMax,Is.LessThanOrEqualTo(size.x));
                Assert.That(box.yMin,Is.GreaterThanOrEqualTo(0));Assert.That(box.yMax,Is.LessThanOrEqualTo(size.y-40));
                occupied.Add(box);
            }
        }
        [Test]
        public void MarkerSelectsDestinationAndDropdownReflectsSelectionBeforeAnyCommand()
        {
            var root = Shell(); using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new Source());
            var dispatcher = new Dispatcher();
            using var controller = new PresentationShellController(root, viewModel, new SlicePresentationLocalizer(), dispatcher);
            controller.Open(new PresentationRoute(PresentationScreenId.Map));
            Click(root.Q<Button>("map-marker-edirne"));
            Assert.That(navigator.Current!.Value.Subject!.Value.Id, Is.EqualTo("edirne"));
            Assert.That(dispatcher.Count, Is.Zero);
            Assert.That(root.Q<DropdownField>("travel-destination").value, Is.EqualTo("Edirne"));
            Assert.That(root.Q<DropdownField>("travel-destination").choices, Does.Contain("İstanbul"));
            // This detached EditMode tree has no runtime panel to dispatch DropdownField change events.
            // Real attached dropdown interaction is covered by the Windows player acceptance harness.
            Click(root.Q<Button>("map-marker-istanbul"));
            Assert.That(navigator.Current!.Value.Subject!.Value.Id, Is.EqualTo("istanbul"));
            Assert.That(root.Q<Button>("travel-start").enabledSelf, Is.False);
        }

        [Test]
        public void RepeatedRefreshRetainsControlsAndDispatchesExactlyOnce()
        {
            var root = Shell(); using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new Source());
            var dispatcher = new Dispatcher();
            using var controller = new PresentationShellController(root, viewModel, new SlicePresentationLocalizer(), dispatcher);
            controller.Open(Route("edirne"));
            var button = root.Q<Button>("travel-start");
            var dropdown = root.Q<DropdownField>("travel-destination");
            var marker = root.Q<Button>("map-marker-edirne");
            for (var i=0;i<50;i++) viewModel.Refresh();
            Assert.That(root.Q<Button>("travel-start"), Is.SameAs(button));
            Assert.That(root.Q<DropdownField>("travel-destination"), Is.SameAs(dropdown));
            Assert.That(root.Q<Button>("map-marker-edirne"), Is.SameAs(marker));
            Assert.That(root.Query<Button>().ToList().Count(x => x.name == "travel-start"), Is.EqualTo(1));
            Click(button);
            Assert.That(dispatcher.Count, Is.EqualTo(1));
            Assert.That(dispatcher.LastAction, Is.EqualTo("travel.start:edirne"));
        }

        [Test]
        public void LeavingMapReleasesOldButtonsAndDoesNotAccumulatePanels()
        {
            var root = Shell(); using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new Source());
            var dispatcher = new Dispatcher();
            var controller = new PresentationShellController(root, viewModel, new SlicePresentationLocalizer(), dispatcher);
            try
            {
                controller.Open(Route("edirne"));
                var old = root.Q<Button>("travel-start");
                var oldMarker = root.Q<Button>("map-marker-edirne");
                for (var i=0;i<20;i++)
                {
                    controller.Open(new PresentationRoute(PresentationScreenId.City));
                    Assert.That(root.Q<VisualElement>("world-map-travel").childCount, Is.Zero);
                    controller.Open(Route("edirne"));
                    Assert.That(root.Query<Button>().ToList().Count(x => x.name == "travel-start"), Is.EqualTo(1));
                }
                Click(old); Click(oldMarker);
                Assert.That(dispatcher.Count, Is.Zero);
                var button = root.Q<Button>("travel-start");
                controller.Dispose(); Click(button);
                Assert.That(dispatcher.Count, Is.Zero);
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public void DisabledStartDoesNotReachApplicationDispatcher()
        {
            var root=Shell();using var navigator=new PresentationNavigator();
            using var viewModel=new PresentationShellViewModel(navigator,new Source());
            var dispatcher=new Dispatcher();
            using var controller=new PresentationShellController(root,viewModel,new SlicePresentationLocalizer(),dispatcher);
            controller.Open(Route("istanbul"));
            Assert.That(root.Q<Button>("travel-start").enabledSelf,Is.False);
            // A stale/direct callback must also carry the disabled descriptor to a guarded dispatch path.
            Click(root.Q<Button>("travel-start"));
            Assert.That(dispatcher.EnabledCount,Is.Zero);
        }

        private static VisualElement Shell() => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();
        private static PresentationRoute Route(string id) => new PresentationRoute(PresentationScreenId.Map,new PresentationEntityRef(PresentationEntityKind.WorldLocation,id));
        private static void Click(Button button)
        {
            var field=typeof(Clickable).GetField("clicked",BindingFlags.NonPublic|BindingFlags.Instance)!;
            ((Action?)field.GetValue(button.clickable))?.Invoke();
        }
        private sealed class Dispatcher : IPresentationActionDispatcher
        {
            public int Count,EnabledCount;public string LastAction="";
            public PresentationActionResult Dispatch(PresentationActionDescriptor action)
            { Count++;if(action.IsEnabled)EnabledCount++;LastAction=action.ActionId;return PresentationActionResult.Rejected("test-only"); }
        }
        private sealed class Source : IPresentationScreenSource
        {
            public ScreenPresentationState Get(PresentationRoute route)
            {
                var target=route.Subject?.Id??"istanbul";var selected=new PresentationEntityRef(PresentationEntityKind.WorldLocation,target);
                var actions=new[]{new PresentationActionDescriptor("travel.start:"+target,"start",target!="istanbul","already-here",selected,PresentationConfirmationPolicy.None,"travel"),
                    new PresentationActionDescriptor("travel.advance-one-hour","advance",false,"not-travelling",selected,PresentationConfirmationPolicy.None,"travel")};
                var markers=new[]{new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.WorldLocation,"istanbul"),"İstanbul",.5f,.5f,PresentationKnowledge.ExactSelf,false),
                    new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.WorldLocation,"edirne"),"Edirne",.1f,.1f,PresentationKnowledge.ExactSelf,false)};
                var travel=new MapTravelPresentation("Hasan Ağa","İstanbul",target,"status",false,false,0,5000,3600,3600,
                    Array.Empty<string>(),new[]{"İstanbul","Edirne"},selected,null);
                return new ScreenPresentationState(route.Screen,"title",selected,new PresentationSection("current",PresentationAvailability.Available),
                    PresentationTrend.InsufficientHistory,PresentationAvailability.Unavailable,null,"unknown",null,null,actions,
                    map:route.Screen==PresentationScreenId.Map?new MapPresentationSnapshot(markers,Array.Empty<MapRoutePresentation>(),Array.Empty<MapJourneyPresentation>(),travel):null);
            }
        }
    }
}
