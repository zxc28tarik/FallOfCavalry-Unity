#nullable enable
using System;
using FOC.Presentation.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    [DisallowMultipleComponent]
    public sealed class PresentationRuntimeHost : MonoBehaviour
    {
        private PresentationNavigator? _navigator;
        private PresentationShellViewModel? _viewModel;
        private PresentationShellController? _controller;

        public void Configure(IPresentationScreenSource source, IPresentationLocalizer? localizer = null, IPresentationActionDispatcher? dispatcher = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Release();
            var document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            var template = Resources.Load<VisualTreeAsset>("FOC/Presentation/PresentationShell");
            if (template == null) throw new InvalidOperationException("PresentationShell UXML resource is missing.");
            document.visualTreeAsset = template;
            _navigator = new PresentationNavigator();
            _viewModel = new PresentationShellViewModel(_navigator, source);
            _controller = new PresentationShellController(document.rootVisualElement, _viewModel, localizer ?? new KeyFallbackLocalizer(), dispatcher);
            // Unity reports a zero-sized screen while a headless player/editor is
            // initializing. Start from the supported compact desktop baseline and
            // let the normal geometry callback apply the real dimensions later.
            var width = Screen.width >= 1024 ? Screen.width : 1366;
            var height = Screen.height >= 600 ? Screen.height : 768;
            _controller.ApplyLayout(width, height);
            _controller.Open(new PresentationRoute(PresentationScreenId.Map));
        }

        private void OnDisable() { Release(); }
        private void Release() { _controller?.Dispose(); _viewModel?.Dispose(); _navigator?.Dispose(); _controller = null; _viewModel = null; _navigator = null; }
    }
}
