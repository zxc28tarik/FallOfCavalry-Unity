#nullable enable
using System;
using System.Linq;
using FOC.Application.Geography;
using FOC.Domain.Common;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Tests
{
    public sealed class CharacterDirectoryPanelTests
    {
        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign() => VerticalSliceCampaignFactory.Create(Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations").text,
            Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes").text, Resources.Load<TextAsset>("FOC/HistoricalSlice/historical-slice-content").text);
        private static PresentationViewerContext Viewer() => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"), new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"),
            new[] { new PresentationEntityRef(PresentationEntityKind.Character, "ali-cavus"), new PresentationEntityRef(PresentationEntityKind.Character, "mehmed-celebi-tacir") });
        private static VisualElement Root() => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FOC/Presentation/Resources/FOC/Presentation/PresentationShell.uxml").Instantiate();

        [Test] public void SearchSelectionRefreshAndDisposalStayBounded()
        {
            var c = Campaign(); var viewer = Viewer(); var root = Root(); using var attached = new AttachedPanel(root); using var nav = new PresentationNavigator(); using var vm = new PresentationShellViewModel(nav, new CampaignPresentationScreenSource(c, viewer, new WorldMapPresentationDataProvider(c)));
            using var session = new CharacterDirectorySession(c, viewer); using var controller = new PresentationShellController(root, vm, new SlicePresentationLocalizer(), characters: session);
            controller.Open(new PresentationRoute(PresentationScreenId.Character)); var search = root.Q<TextField>("character-search");
            Assert.That(root.Q<Label>("character-directory-count").text, Is.EqualTo("3 / 3 bilinen karakter"));
            for (var i = 0; i < 50; i++) vm.Refresh(); Assert.That(root.Q<TextField>("character-search"), Is.SameAs(search));
            search.value = "ÇAVUŞ"; Assert.That(root.Q<ListView>("character-directory-rows").itemsSource.Count, Is.EqualTo(1));
            controller.Open(new PresentationRoute(PresentationScreenId.Character, new PresentationEntityRef(PresentationEntityKind.Character, "ali-cavus")));
            Assert.That(root.Q<Label>("character-directory-summary").text, Does.Contain("Ali Çavuş"));
            controller.Open(new PresentationRoute(PresentationScreenId.Map)); Assert.That(root.Q<VisualElement>("character-directory-panel").childCount, Is.Zero);
            for (var i = 0; i < 20; i++) { controller.Open(new PresentationRoute(PresentationScreenId.Character)); controller.Open(new PresentationRoute(PresentationScreenId.Map)); }
            controller.Open(new PresentationRoute(PresentationScreenId.Character)); Assert.That(root.Query<TextField>("character-search").ToList().Count, Is.EqualTo(1));
            controller.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.Read());
        }

        private sealed class AttachedPanel : IDisposable
        {
            private readonly GameObject _go = new GameObject("Character directory attached UI test");
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
