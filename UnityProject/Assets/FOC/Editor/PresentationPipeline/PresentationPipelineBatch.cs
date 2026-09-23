#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Editor.Presentation
{
    public static class PresentationPipelineBatch
    {
        private const string ShellPath = "Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml";
        private static readonly string[] Styles =
        {
            "Assets/FOC/Presentation/Resources/FOC/Presentation/Styles/PresentationTokens.uss",
            "Assets/FOC/Presentation/Resources/FOC/Presentation/Styles/PresentationBase.uss",
            "Assets/FOC/Presentation/Resources/FOC/Presentation/Styles/PresentationComponents.uss",
            "Assets/FOC/Presentation/Resources/FOC/Presentation/Styles/PresentationScreens.uss"
        };

        public static void Run()
        {
            var exitCode = 0;
            var summary = new StringBuilder();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShellPath) ?? throw new InvalidOperationException("Presentation shell UXML is missing.");
                foreach (var path in Styles) if (AssetDatabase.LoadAssetAtPath<StyleSheet>(path) == null) throw new InvalidOperationException("Presentation stylesheet is missing: " + path);
                ValidateSourceBoundary();
                var source = new ProofScreenSource(4);
                var root = template.Instantiate();
                using (var navigator = new PresentationNavigator())
                using (var viewModel = new PresentationShellViewModel(navigator, source))
                using (var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer()))
                {
                    foreach (PresentationScreenId screen in Enum.GetValues(typeof(PresentationScreenId))) controller.Open(new PresentationRoute(screen));
                    controller.ApplyLayout(1366, 768);
                    controller.ApplyLayout(1920, 1080);
                    controller.ApplyLayout(2560, 1440);
                    controller.ApplyLayout(3440, 1440);
                    if (controller.BoundNavigationCount != 12) throw new InvalidOperationException("Navigation shell binding count is invalid.");
                    RequireNamed(root, "presentation-shell", "screen-title", "current-content", "why-content", "risk-content", "opportunity-content", "action-content", "details-content");
                    summary.AppendLine("SCREEN_SMOKE=PASS screens=" + Enum.GetValues(typeof(PresentationScreenId)).Length);
                    summary.AppendLine("NAVIGATION_SMOKE=PASS bindings=" + controller.BoundNavigationCount);
                    summary.AppendLine("RESPONSIVE=PASS sizes=1366x768,1920x1080,2560x1440,3440x1440");
                }
                RunBenchmarks(template, summary);
                summary.AppendLine("INFORMATION_BOUNDARY=PASS source=PresentationReadModel");
                summary.AppendLine("UXML_USS=PASS");
                summary.AppendLine("SCREENSHOTS=NOT_RUN reason=batchmode-nographics");
                summary.AppendLine("UNITY=" + Application.unityVersion);
                summary.AppendLine("RESULT=PASS");
            }
            catch (Exception error)
            {
                exitCode = 1;
                summary.AppendLine("RESULT=FAIL");
                summary.AppendLine(error.ToString());
                UnityEngine.Debug.LogException(error);
            }
            finally
            {
                var resultRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults"));
                Directory.CreateDirectory(resultRoot);
                File.WriteAllText(Path.Combine(resultRoot, "presentation-pipeline-result.txt"), summary.ToString());
                UnityEngine.Debug.Log(summary.ToString());
                EditorApplication.Exit(exitCode);
            }
        }

        private static void RunBenchmarks(VisualTreeAsset template, StringBuilder summary)
        {
            foreach (var count in new[] { 100, 500, 1000, 5000 })
            {
                var source = new ProofScreenSource(count);
                var before = GC.GetTotalMemory(true);
                var watch = Stopwatch.StartNew();
                var root = template.Instantiate();
                using (var navigator = new PresentationNavigator())
                using (var viewModel = new PresentationShellViewModel(navigator, source))
                using (var controller = new PresentationShellController(root, viewModel, new KeyFallbackLocalizer()))
                {
                    controller.Open(new PresentationRoute(PresentationScreenId.Character));
                    watch.Stop();
                    var list = root.Query<ListView>().First();
                    if (list == null || list.virtualizationMethod != CollectionVirtualizationMethod.FixedHeight || list.itemsSource.Count != count)
                        throw new InvalidOperationException("Virtualized ListView did not bind the expected proof rows.");
                    var allocated = Math.Max(0, GC.GetTotalMemory(false) - before);
                    summary.AppendLine("LIST_BENCHMARK rows=" + count + " buildMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " sourceCount=" + list.itemsSource.Count + " liveVisualRows=" + list.childCount + " allocationBytes=" + allocated);
                }
            }
            var items = Enumerable.Range(0, 5000).ToList();
            var window = new VirtualizedListWindow<int>(items, 24, 4);
            if (window.GetWindow(2500).Count > 32) throw new InvalidOperationException("Virtualized window exceeded its bounded live-row contract.");
            summary.AppendLine("VIRTUALIZATION_WINDOW=PASS maxLiveRows=" + window.MaximumLiveRows);
            MeasureReadModel("characters", 100, summary);
            MeasureReadModel("soldiers", 500, summary);
            MeasureReadModel("reports-contracts", 1000, summary);
        }

        private static void MeasureReadModel(string name, int count, StringBuilder summary)
        {
            var before = GC.GetTotalMemory(true); var watch = Stopwatch.StartNew();
            var fields = Enumerable.Range(0, count).Select(index => new PresentationField("proof." + name + "." + index, index.ToString(), PresentationKnowledge.ExactSelf)).ToList();
            var state = new ScreenPresentationState(PresentationScreenId.Archive, "proof." + name, null,
                new PresentationSection("presentation.section.current", PresentationAvailability.Available, fields), PresentationTrend.InsufficientHistory,
                PresentationAvailability.Unavailable, null, "presentation.why.not-exposed-by-gameplay", null, null, null);
            watch.Stop(); var allocated = Math.Max(0, GC.GetTotalMemory(false) - before);
            if (state.Current.Fields.Count != count) throw new InvalidOperationException("Read-model benchmark count changed.");
            summary.AppendLine("READMODEL_BENCHMARK kind=" + name + " rows=" + count + " buildMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " allocationBytes=" + allocated);
        }

        private static void ValidateSourceBoundary()
        {
            var runtimeRoot = Path.Combine(Application.dataPath, "FOC", "Presentation", "Runtime");
            foreach (var path in Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                if (text.Contains("FOC.Domain", StringComparison.Ordinal) || text.Contains("CampaignRuntimeState", StringComparison.Ordinal) || text.Contains("SeededRandomSource", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unity view source crossed the Presentation information boundary: " + path);
            }
        }

        private static void RequireNamed(VisualElement root, params string[] names)
        {
            foreach (var name in names) if (root.Q(name) == null) throw new InvalidOperationException("UXML named element is missing: " + name);
        }

        private sealed class ProofScreenSource : IPresentationScreenSource
        {
            private readonly int _fieldCount;
            public ProofScreenSource(int fieldCount) { _fieldCount = fieldCount; }
            public ScreenPresentationState Get(PresentationRoute route)
            {
                var fields = Enumerable.Range(0, _fieldCount).Select(index => new PresentationField("proof.label." + index, index.ToString(), PresentationKnowledge.ExactSelf)).ToList();
                return new ScreenPresentationState(route.Screen, "presentation.screen." + route.Screen.ToString().ToLowerInvariant(), route.Subject,
                    new PresentationSection("presentation.section.current", PresentationAvailability.Available, fields), PresentationTrend.InsufficientHistory,
                    PresentationAvailability.Unavailable, null, "presentation.why.not-exposed-by-gameplay", null, null, null,
                    null, new[] { new PresentationSection("presentation.section.details", PresentationAvailability.Available, fields.Take(12)) });
            }
        }
    }
}
