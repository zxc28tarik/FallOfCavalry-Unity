#nullable enable
using System;
using System.Linq;
using System.Reflection;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Tests
{
    public sealed class PresentationUnityTests
    {
        private const string ShellPath = "Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml";

        [Test]
        public void PresentationShell_ConstructsEveryScreenWithoutException()
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new Source(8));
            using var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
            foreach (PresentationScreenId screen in Enum.GetValues(typeof(PresentationScreenId))) controller.Open(new PresentationRoute(screen));
            Assert.That(root.Q<Label>("screen-title"), Is.Not.Null);
            Assert.That(root.Query<ListView>().ToList(), Is.Not.Empty);
        }

        [Test]
        public void LargeRows_UseFixedHeightListViewVirtualization()
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new Source(5000));
            using var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
            controller.Open(new PresentationRoute(PresentationScreenId.Army));
            var list = root.Query<ListView>().First();
            Assert.That(list.virtualizationMethod, Is.EqualTo(CollectionVirtualizationMethod.FixedHeight));
            Assert.That(list.itemsSource.Count, Is.EqualTo(5000));
        }

        [TestCase(1366, 768, true, false)]
        [TestCase(1920, 1080, false, false)]
        [TestCase(2560, 1440, false, true)]
        public void ResponsiveDesktopClasses_AreApplied(int width, int height, bool compact, bool wide)
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new Source(1));
            using var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
            controller.ApplyLayout(width, height);
            Assert.That(root.ClassListContains("foc-layout--compact"), Is.EqualTo(compact));
            Assert.That(root.ClassListContains("foc-layout--wide"), Is.EqualTo(wide));
        }

        [Test]
        public void DisposeAndReopen_DoesNotDuplicateNavigationSubscriptions()
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            for (var i = 0; i < 50; i++)
            {
                using var viewModel = new PresentationShellViewModel(navigator, new Source(1));
                using var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
                Assert.That(navigator.SubscriptionCount, Is.EqualTo(1));
            }
            Assert.That(navigator.SubscriptionCount, Is.Zero);
        }

        [Test]
        public void FieldCollections_AreReusedAndBindTheCurrentRouteRatherThanOldFields()
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new ChangingSource());
            using var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
            controller.Open(new PresentationRoute(PresentationScreenId.Map));
            var list = root.Q<VisualElement>("current-content").Q<ListView>();
            var row = list.makeItem();
            list.bindItem(row, 0);
            Assert.That(row.Q<Label>("field-label").text, Is.EqualTo("field.Map"));
            controller.Open(new PresentationRoute(PresentationScreenId.Army));
            Assert.That(root.Q<VisualElement>("current-content").Q<ListView>(), Is.SameAs(list));
            list.bindItem(row, 0);
            Assert.That(row.Q<Label>("field-label").text, Is.EqualTo("field.Army"));
            Assert.That(row.Q<Button>("field-value").userData, Is.EqualTo(new PresentationEntityRef(PresentationEntityKind.City, "new-city")));
            list.destroyItem(row);
            Assert.That(row.Q<Button>("field-value").userData, Is.Null);
            Assert.That(row.GetType().GetField("_click", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(row), Is.Null,
                "Destroyed rows must release the delegate's captured controller, not only unsubscribe it.");
        }

        [Test]
        public void DetailSlots_AreBoundedAndInactiveCollectionsReleaseOldData()
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            using var viewModel = new PresentationShellViewModel(navigator, new ChangingSource());
            var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
            try
            {
                controller.Open(new PresentationRoute(PresentationScreenId.Map));
                var initial = root.Q<VisualElement>("details-content").Query<ListView>().ToList().ToArray();
                Assert.That(initial.Length, Is.EqualTo(3));
                for (var i = 0; i < 30; i++)
                {
                    controller.Open(new PresentationRoute(PresentationScreenId.Army));
                    Assert.That(initial[1].itemsSource, Is.Null);
                    Assert.That(initial[2].itemsSource, Is.Null);
                    controller.Open(new PresentationRoute(PresentationScreenId.City));
                    Assert.That(root.Q<VisualElement>("current-content").Q<ListView>(), Is.Null);
                    Assert.That(root.Q<VisualElement>("details-content").Query<ListView>().ToList(), Is.Empty);
                    controller.Open(new PresentationRoute(PresentationScreenId.Map));
                    Assert.That(root.Q<VisualElement>("details-content").Query<ListView>().ToList(), Is.EqualTo(initial));
                }
                controller.Dispose();
                foreach (var list in initial)
                {
                    Assert.That(list.itemsSource, Is.Null);
                    Assert.That(list.makeItem, Is.Null);
                    Assert.That(list.bindItem, Is.Null);
                    Assert.That(list.parent, Is.Null);
                }
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public void DisposedController_ReopeningTheSameTreeDoesNotAccumulateDetailContainers()
        {
            var root = LoadShell().Instantiate();
            for (var i = 0; i < 30; i++)
            {
                using var navigator = new PresentationNavigator();
                using var viewModel = new PresentationShellViewModel(navigator, new ChangingSource());
                using var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
                controller.Open(new PresentationRoute(PresentationScreenId.Map));
                Assert.That(root.Q<VisualElement>("details-content").childCount, Is.EqualTo(3));
                controller.Dispose();
                Assert.That(root.Q<VisualElement>("details-content").childCount, Is.Zero);
                Assert.That(root.Query<ListView>().ToList(), Is.Empty);
            }
        }

        [Test]
        public void RecycledFieldButton_OpensOnlyItsCurrentEntityAndDoesNotDispatchAfterDisposal()
        {
            var root = LoadShell().Instantiate();
            using var navigator = new PresentationNavigator();
            var source = new ChangingSource();
            using var viewModel = new PresentationShellViewModel(navigator, source);
            var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer());
            try
            {
                controller.Open(new PresentationRoute(PresentationScreenId.Map));
                var list = root.Q<VisualElement>("current-content").Q<ListView>();
                var row = list.makeItem();
                list.bindItem(row, 0);
                controller.Open(new PresentationRoute(PresentationScreenId.Army));
                list.bindItem(row, 0);
                var value = row.Q<Button>("field-value");
                var clicked = typeof(Clickable).GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(clicked, Is.Not.Null, "Unity callback backing field changed; do not skip this check.");
                var queries = source.Queries;
                ((Action?)clicked!.GetValue(value.clickable))?.Invoke();
                Assert.That(source.Queries, Is.EqualTo(queries + 1), "Row rebinding must not add duplicate click callbacks.");
                Assert.That(navigator.Current!.Value.Subject, Is.EqualTo(new PresentationEntityRef(PresentationEntityKind.City, "new-city")));
                controller.Dispose();
                queries = source.Queries;
                ((Action?)clicked.GetValue(value.clickable))?.Invoke();
                Assert.That(source.Queries, Is.EqualTo(queries), "Retained external rows cannot open a retired controller.");
            }
            finally { controller.Dispose(); }
        }

        [Test]
        public void ExistingVisualSoldierPipeline_IsThePreviewImplementation()
        {
            Assert.That(typeof(VisualSoldier3DAssembler).Assembly.GetName().Name, Is.EqualTo("FOC.Visuals.Unity"));
            Assert.That(typeof(PresentationShellController).Assembly.GetTypes().Count(x => x.Name.IndexOf("VisualSoldier", StringComparison.Ordinal) >= 0), Is.Zero);
        }

        [Test]
        public void ShellAssetsAndRequiredNavigationTargetsExist()
        {
            var root = LoadShell().Instantiate();
            foreach (var name in new[] { "nav-map", "nav-city", "nav-character", "nav-organization", "nav-trade", "nav-army", "nav-diplomacy", "nav-battle", "nav-reports", "nav-ledger", "nav-back", "nav-forward" })
                Assert.That(root.Q<Button>(name), Is.Not.Null, name);
            Assert.That(root.styleSheets.count, Is.EqualTo(4));
        }

        [Test]
        public void RuntimeHost_AutoCreatesDocumentAndConstructsShell()
        {
            var gameObject = new GameObject("PresentationRuntimeHostTest");
            try
            {
                var host = gameObject.AddComponent<PresentationRuntimeHost>();
                host.Configure(new Source(2));
                var document = gameObject.GetComponent<UIDocument>();
                Assert.That(document, Is.Not.Null);
                Assert.That(document.panelSettings, Is.Not.Null, "A runtime player needs a PanelSettings instance to render the shell.");
                Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null, "The runtime panel needs the default UI Toolkit theme for text and controls.");
                Assert.That(document.rootVisualElement.Q<Label>("screen-title"), Is.Not.Null);
                Assert.That(document.rootVisualElement.Query<ListView>().ToList(), Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static VisualTreeAsset LoadShell() => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShellPath) ?? throw new InvalidOperationException("Presentation shell UXML not imported.");

        private sealed class Source : IPresentationScreenSource
        {
            private readonly int _rows; public Source(int rows) { _rows = rows; }
            public ScreenPresentationState Get(PresentationRoute route)
            {
                var fields = Enumerable.Range(0, _rows).Select(x => new PresentationField("proof." + x, x.ToString(), PresentationKnowledge.ExactSelf));
                return new ScreenPresentationState(route.Screen, "presentation.screen." + route.Screen.ToString().ToLowerInvariant(), route.Subject,
                    new PresentationSection("presentation.section.current", PresentationAvailability.Available, fields), PresentationTrend.InsufficientHistory,
                    PresentationAvailability.Unavailable, null, "presentation.why.unavailable", null, null, null);
            }
        }

        private sealed class ChangingSource : IPresentationScreenSource
        {
            public int Queries { get; private set; }
            public ScreenPresentationState Get(PresentationRoute route)
            {
                Queries++;
                var field = new PresentationField("field." + route.Screen, "value." + route.Screen,
                    PresentationKnowledge.ExactSelf, new PresentationEntityRef(PresentationEntityKind.City,
                        route.Screen == PresentationScreenId.Map ? "old-city" : "new-city"));
                var current = route.Screen == PresentationScreenId.City
                    ? PresentationSection.Unavailable("current", "unknown.current")
                    : new PresentationSection("current", PresentationAvailability.Available, new[] { field });
                var count = route.Screen == PresentationScreenId.Map ? 3 : route.Screen == PresentationScreenId.Army ? 1 : 0;
                return new ScreenPresentationState(route.Screen, "screen." + route.Screen, route.Subject, current,
                    PresentationTrend.InsufficientHistory, PresentationAvailability.Unavailable, null, "why.unavailable",
                    null, null, null, details: Enumerable.Range(0, count).Select(i =>
                        new PresentationSection("detail." + i, PresentationAvailability.Available, new[] { field })));
            }
        }
    }
}
