#nullable enable
using System;
using System.Linq;
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
    }
}
