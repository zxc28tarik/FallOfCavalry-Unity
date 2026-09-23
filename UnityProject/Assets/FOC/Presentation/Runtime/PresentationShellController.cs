#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Presentation.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    public sealed class PresentationShellController : IDisposable
    {
        private static readonly PresentationScreenId[] TopLevel =
        {
            PresentationScreenId.Map, PresentationScreenId.City, PresentationScreenId.Character,
            PresentationScreenId.Organization, PresentationScreenId.Trade, PresentationScreenId.Army,
            PresentationScreenId.Diplomacy, PresentationScreenId.Battle
        };

        private readonly VisualElement _root;
        private readonly PresentationShellViewModel _viewModel;
        private readonly IPresentationLocalizer _localizer;
        private readonly IPresentationActionDispatcher? _dispatcher;
        private readonly List<Tuple<Button, Action>> _buttonHandlers = new List<Tuple<Button, Action>>();
        private readonly EventCallback<KeyDownEvent> _keyHandler;
        private readonly EventCallback<GeometryChangedEvent> _geometryHandler;
        private bool _disposed;

        public PresentationShellController(VisualElement root, PresentationShellViewModel viewModel, IPresentationLocalizer localizer, IPresentationActionDispatcher? dispatcher = null)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _dispatcher = dispatcher;
            _keyHandler = OnKeyDown;
            _geometryHandler = OnGeometryChanged;
            BindNavigation();
            _viewModel.Changed += Render;
            _root.RegisterCallback(_keyHandler);
            _root.RegisterCallback(_geometryHandler);
            _root.focusable = true;
        }

        public int BoundNavigationCount => _buttonHandlers.Count;

        public void Open(PresentationRoute route) { ThrowIfDisposed(); _viewModel.Open(route); }

        public void ApplyLayout(int width, int height, float scale = 1f)
        {
            var layout = PresentationLayoutPolicy.Resolve(width, height, scale);
            _root.EnableInClassList("foc-layout--compact", layout.Kind == PresentationLayoutClass.CompactDesktop);
            _root.EnableInClassList("foc-layout--wide", layout.Kind == PresentationLayoutClass.WideDesktop || layout.Kind == PresentationLayoutClass.UltraWideDesktop);
            _root.style.fontSize = 14f * layout.Scale;
        }

        private void BindNavigation()
        {
            foreach (var screen in TopLevel)
            {
                var button = _root.Q<Button>("nav-" + screen.ToString().ToLowerInvariant());
                if (button == null) throw new InvalidOperationException("Navigation control is missing for " + screen + ".");
                button.text = _localizer.Get("presentation.screen." + screen.ToString().ToLowerInvariant());
                var captured = screen;
                Action action = () => _viewModel.Open(new PresentationRoute(captured));
                button.clicked += action;
                _buttonHandlers.Add(Tuple.Create(button, action));
            }
            BindButton("nav-reports", () => _viewModel.Open(new PresentationRoute(PresentationScreenId.Reports)));
            BindButton("nav-ledger", () => _viewModel.Open(new PresentationRoute(PresentationScreenId.Ledger)));
            BindButton("nav-back", () => _viewModel.Back());
            BindButton("nav-forward", () => _viewModel.Forward());
        }

        private void BindButton(string name, Action action)
        {
            var button = _root.Q<Button>(name) ?? throw new InvalidOperationException("Required shell control is missing: " + name);
            button.clicked += action;
            _buttonHandlers.Add(Tuple.Create(button, action));
        }

        private void Render(ScreenPresentationState state)
        {
            SetText("screen-title", state.TitleKey);
            SetText("breadcrumb", state.Subject.HasValue ? state.Subject.Value.ToString() : "presentation.navigation.root");
            RenderSection(_root.Q<VisualElement>("current-content"), state.Current);
            SetText("trend-value", state.Trend.Availability == PresentationAvailability.Available ? "presentation.trend." + state.Trend.Direction.ToString().ToLowerInvariant() : state.Trend.ExplanationKey);
            RenderFactors(_root.Q<VisualElement>("why-content"), state.WhyAvailability, state.Why, state.WhyUnavailableReasonKey);
            RenderAttention(_root.Q<VisualElement>("risk-content"), state.Risks, "presentation.empty.risks");
            RenderAttention(_root.Q<VisualElement>("opportunity-content"), state.Opportunities, "presentation.empty.opportunities");
            RenderActions(_root.Q<VisualElement>("action-content"), state.Actions);
            RenderDetails(_root.Q<VisualElement>("details-content"), state.Details);
            SetText("alert-count", state.Alerts.Count.ToString());
        }

        private void RenderSection(VisualElement? container, PresentationSection section)
        {
            if (container == null) return;
            container.Clear();
            if (section.Availability != PresentationAvailability.Available)
            {
                container.Add(StatusLabel(section.UnavailableReasonKey, "foc-status--unknown"));
                return;
            }
            container.Add(CreateFieldList(section.Fields));
        }

        private ListView CreateFieldList(IReadOnlyList<PresentationField> fields)
        {
            var list = new ListView
            {
                itemsSource = fields as System.Collections.IList ?? fields.ToList(),
                fixedItemHeight = 34,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                focusable = true,
                makeItem = () =>
                {
                    var row = new VisualElement(); row.AddToClassList("foc-field-row");
                    var label = new Label { name = "field-label" }; label.AddToClassList("foc-field-label");
                    var value = new Label { name = "field-value" }; value.AddToClassList("foc-field-value");
                    var quality = new Label { name = "field-quality" }; quality.AddToClassList("foc-quality-chip");
                    row.Add(label); row.Add(value); row.Add(quality); return row;
                },
                bindItem = (element, index) =>
                {
                    var field = fields[index];
                    element.Q<Label>("field-label").text = _localizer.Get(field.LabelKey);
                    element.Q<Label>("field-value").text = _localizer.Get(field.DisplayValue);
                    element.Q<Label>("field-quality").text = _localizer.Get("presentation.precision." + field.Knowledge.Precision.ToString().ToLowerInvariant());
                    element.tooltip = string.IsNullOrEmpty(field.TooltipKey) ? _localizer.Get(field.Knowledge.SourceKey) : _localizer.Get(field.TooltipKey);
                }
            };
            list.AddToClassList("foc-virtual-list");
            list.style.height = Mathf.Clamp(fields.Count * 34f, 42f, 272f);
            return list;
        }

        private void RenderFactors(VisualElement? container, PresentationAvailability availability, IReadOnlyList<PresentationFactor> factors, string reason)
        {
            if (container == null) return; container.Clear();
            if (availability != PresentationAvailability.Available) { container.Add(StatusLabel(reason, "foc-status--unknown")); return; }
            foreach (var factor in factors) container.Add(StatusLabel(_localizer.Get(factor.LabelKey) + "  " + _localizer.Get(factor.DisplayValue), "foc-factor"));
        }

        private void RenderAttention(VisualElement? container, IReadOnlyList<PresentationAttentionItem> items, string emptyKey)
        {
            if (container == null) return; container.Clear();
            if (items.Count == 0) { container.Add(StatusLabel(emptyKey, "foc-status--neutral")); return; }
            foreach (var item in items)
            {
                var row = StatusLabel(item.LabelKey + " · " + item.ReasonKey, "foc-attention");
                row.AddToClassList("foc-severity--" + item.Severity.ToString().ToLowerInvariant()); container.Add(row);
            }
        }

        private void RenderActions(VisualElement? container, IReadOnlyList<PresentationActionDescriptor> actions)
        {
            if (container == null) return; container.Clear();
            if (actions.Count == 0) { container.Add(StatusLabel("presentation.empty.actions", "foc-status--neutral")); return; }
            foreach (var action in actions)
            {
                var button = new Button { text = _localizer.Get(action.LabelKey), focusable = true };
                button.SetEnabled(action.IsEnabled);
                button.tooltip = action.IsEnabled ? _localizer.Get(action.CommandAdapterId) : _localizer.Get(action.DisabledReasonKey);
                if (action.IsEnabled)
                {
                    var captured = action;
                    button.clicked += () => ShowActionResult(_dispatcher == null ? PresentationActionResult.Rejected("presentation.action.binding-unavailable") : _dispatcher.Dispatch(captured));
                }
                button.AddToClassList("foc-action-button"); container.Add(button);
            }
        }

        private void ShowActionResult(PresentationActionResult result)
        {
            var feedback = _root.Q<Label>("action-feedback");
            if (feedback == null) return;
            feedback.text = _localizer.Get(result.MessageKey);
            feedback.EnableInClassList("foc-status--success", result.Succeeded);
            feedback.EnableInClassList("foc-status--error", !result.Succeeded);
            if (result.Succeeded) _viewModel.Refresh();
        }

        private void RenderDetails(VisualElement? container, IReadOnlyList<PresentationSection> details)
        {
            if (container == null) return; container.Clear();
            foreach (var detail in details)
            {
                var foldout = new Foldout { text = _localizer.Get(detail.HeadingKey), value = false };
                foldout.AddToClassList("foc-detail-foldout");
                if (detail.Availability == PresentationAvailability.Available) foldout.Add(CreateFieldList(detail.Fields));
                else foldout.Add(StatusLabel(detail.UnavailableReasonKey, "foc-status--unknown"));
                container.Add(foldout);
            }
        }

        private Label StatusLabel(string key, string className) { var label = new Label(_localizer.Get(key)); label.AddToClassList(className); return label; }
        private void SetText(string name, string key) { var label = _root.Q<Label>(name); if (label != null) label.text = _localizer.Get(key); }
        private void OnKeyDown(KeyDownEvent evt) { if (evt.keyCode == KeyCode.Escape && _viewModel.Back()) evt.StopPropagation(); }
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var width = (int)evt.newRect.width;
            var height = (int)evt.newRect.height;
            if (width >= 1024 && height >= 600) ApplyLayout(width, height);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _viewModel.Changed -= Render;
            _root.UnregisterCallback(_keyHandler);
            _root.UnregisterCallback(_geometryHandler);
            foreach (var binding in _buttonHandlers) binding.Item1.clicked -= binding.Item2;
            _buttonHandlers.Clear();
            _disposed = true;
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PresentationShellController)); }
    }
}
