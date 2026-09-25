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
            LocalizeTemplate();
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

        private void LocalizeTemplate()
        {
            foreach (var label in _root.Query<Label>().ToList())
                if (!string.IsNullOrWhiteSpace(label.text) && label.text.StartsWith("presentation.", StringComparison.Ordinal))
                    label.text = _localizer.Get(label.text);
            foreach (var button in _root.Query<Button>().ToList())
            {
                if (!string.IsNullOrWhiteSpace(button.text) && button.text.StartsWith("presentation.", StringComparison.Ordinal))
                    button.text = _localizer.Get(button.text);
                if (!string.IsNullOrWhiteSpace(button.tooltip) && button.tooltip.StartsWith("presentation.", StringComparison.Ordinal))
                    button.tooltip = _localizer.Get(button.tooltip);
            }
        }

        private void Render(ScreenPresentationState state)
        {
            SetText("screen-title", state.TitleKey);
            SetText("breadcrumb", state.Subject.HasValue ? EntityLabel(state.Subject.Value) : "presentation.navigation.root");
            RenderSection(_root.Q<VisualElement>("current-content"), state.Current);
            SetText("trend-value", state.Trend.Availability == PresentationAvailability.Available ? "presentation.trend." + state.Trend.Direction.ToString().ToLowerInvariant() : state.Trend.ExplanationKey);
            RenderFactors(_root.Q<VisualElement>("why-content"), state.WhyAvailability, state.Why, state.WhyUnavailableReasonKey);
            RenderAttention(_root.Q<VisualElement>("risk-content"), state.Risks, "presentation.empty.risks");
            RenderAttention(_root.Q<VisualElement>("opportunity-content"), state.Opportunities, "presentation.empty.opportunities");
            RenderActions(_root.Q<VisualElement>("action-content"), state.Actions);
            RenderDetails(_root.Q<VisualElement>("details-content"), state.Details);
            SetText("alert-count", state.Alerts.Count.ToString());
            RenderMap(state);
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
                    var value = new Button { name = "field-value" }; value.AddToClassList("foc-field-value"); value.clicked += () => { if (value.userData is PresentationEntityRef target) OpenEntity(target); };
                    var quality = new Label { name = "field-quality" }; quality.AddToClassList("foc-quality-chip");
                    row.Add(label); row.Add(value); row.Add(quality); return row;
                },
                bindItem = (element, index) =>
                {
                    var field = fields[index];
                    element.Q<Label>("field-label").text = _localizer.Get(field.LabelKey);
                    var value=element.Q<Button>("field-value"); value.text = field.Link.HasValue ? EntityLabel(field.Link.Value) : _localizer.Get(field.DisplayValue); value.userData=field.Link.HasValue?(object)field.Link.Value:null; value.SetEnabled(field.Link.HasValue);
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
        private void RenderMap(ScreenPresentationState state)
        {
            var panel=_root.Q<VisualElement>("world-map-panel");if(panel==null)return;panel.style.display=state.ScreenId==PresentationScreenId.Map?DisplayStyle.Flex:DisplayStyle.None;panel.Clear();if(state.ScreenId!=PresentationScreenId.Map||state.Map==null)return;
            var texture=Resources.Load<Texture2D>("FOC/Geography/MarmaraStrategyMap");if(texture!=null)panel.style.backgroundImage=new StyleBackground(texture);
            var routes=new VisualElement();routes.style.position=Position.Absolute;routes.style.left=0;routes.style.right=0;routes.style.top=0;routes.style.bottom=0;routes.pickingMode=PickingMode.Ignore;var routeSnapshot=state.Map.Routes;routes.generateVisualContent+=context=>{var painter=context.painter2D;painter.lineWidth=2f;foreach(var route in routeSnapshot){painter.strokeColor=route.Active?new Color(0.9f,0.25f,0.12f,0.95f):new Color(0.26f,0.19f,0.11f,0.72f);painter.BeginPath();painter.MoveTo(new Vector2(route.FirstX*routes.contentRect.width,route.FirstY*routes.contentRect.height));painter.LineTo(new Vector2(route.SecondX*routes.contentRect.width,route.SecondY*routes.contentRect.height));painter.Stroke();}};panel.Add(routes);
            var occupiedLabels=new List<Vector2>();foreach(var marker in state.Map.Markers){var labelPoint=FindMarkerLabelPosition(marker.NormalizedX,marker.NormalizedY,occupiedLabels);occupiedLabels.Add(labelPoint);var button=new Button{ text=_localizer.Get(marker.LabelKey),tooltip=marker.Knowledge.SourceKey};button.AddToClassList("foc-map-marker");button.style.position=Position.Absolute;button.style.left=Length.Percent(labelPoint.x*92f+2f);button.style.top=Length.Percent(labelPoint.y*86f+4f);if(marker.NavigationTarget.HasValue){var target=marker.NavigationTarget.Value;button.clicked+=()=>OpenEntity(target);}panel.Add(button);}
            if(state.Map.Journeys.Count>0){var journey=state.Map.Journeys[0];var label=new Label(journey.ActorLabel+"  "+journey.OriginLabel+" → "+journey.DestinationLabel+"  "+journey.SegmentIndex+"/"+journey.SegmentCount);label.AddToClassList("foc-map-progress");panel.Add(label);}
        }
        private static Vector2 FindMarkerLabelPosition(float x,float y,IReadOnlyList<Vector2> occupied)
        {
            var offsets=new[]{new Vector2(0f,0f),new Vector2(0f,-0.075f),new Vector2(0f,0.075f),new Vector2(-0.08f,-0.04f),new Vector2(0.08f,0.04f),new Vector2(-0.08f,0.075f),new Vector2(0.08f,-0.075f),new Vector2(0f,-0.15f),new Vector2(0f,0.15f),new Vector2(-0.16f,0f),new Vector2(0.16f,0f)};
            foreach(var offset in offsets){var candidate=new Vector2(Mathf.Clamp(x+offset.x,0.05f,0.95f),Mathf.Clamp(y+offset.y,0.06f,0.94f));if(!occupied.Any(other=>Mathf.Abs(candidate.x-other.x)<0.08f&&Mathf.Abs(candidate.y-other.y)<0.065f))return candidate;}
            return new Vector2(Mathf.Clamp(x,0.05f,0.95f),Mathf.Clamp(y,0.06f,0.94f));
        }
        private void OpenEntity(PresentationEntityRef target)
        {
            switch(target.Kind){case PresentationEntityKind.City:_viewModel.Open(new PresentationRoute(PresentationScreenId.City,target));break;case PresentationEntityKind.Character:_viewModel.Open(new PresentationRoute(PresentationScreenId.Character,target));break;case PresentationEntityKind.Army:_viewModel.Open(new PresentationRoute(PresentationScreenId.Army,target));break;case PresentationEntityKind.WorldLocation:_viewModel.Open(new PresentationRoute(PresentationScreenId.Map,target));break;}
        }
        private void SetText(string name, string key) { var label = _root.Q<Label>(name); if (label != null) label.text = _localizer.Get(key); }
        private string EntityLabel(PresentationEntityRef entity) => _localizer.Get("presentation.entity." + entity.Id);
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
