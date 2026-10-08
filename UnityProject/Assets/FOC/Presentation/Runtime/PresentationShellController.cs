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
        private ListView? _currentFieldList;
        private Label? _currentUnavailable;
        private readonly List<Foldout> _detailFoldouts = new List<Foldout>();
        private readonly List<ListView> _detailFieldLists = new List<ListView>();
        private readonly List<Label> _detailUnavailable = new List<Label>();
        private bool _disposed;
        private WorldMapTravelPanel? _travelPanel;
        private readonly TradePanelSession? _trade;
        private TradePanel? _tradePanel;

        public PresentationShellController(VisualElement root, PresentationShellViewModel viewModel, IPresentationLocalizer localizer, IPresentationActionDispatcher? dispatcher = null, TradePanelSession? trade = null)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _dispatcher = dispatcher;
            _trade = trade;
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
            var tradeRoot = _root.Q<VisualElement>("trade-panel");
            if (tradeRoot != null)
            {
                var visible = state.ScreenId == PresentationScreenId.Trade && state.Current.Availability == PresentationAvailability.Available && _trade != null;
                tradeRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                {
                    _tradePanel ??= new TradePanel(tradeRoot, _trade!, _localizer,
                        city => _viewModel.Open(new PresentationRoute(PresentationScreenId.Trade, new PresentationEntityRef(PresentationEntityKind.City, city))), ShowActionResult);
                    _tradePanel.Render(state.Subject?.Id);
                }
                else { _tradePanel?.Dispose(); _tradePanel = null; }
            }
        }

        private void RenderSection(VisualElement? container, PresentationSection section)
        {
            if (container == null) return;
            if (section.Availability != PresentationAvailability.Available)
            {
                if (_currentFieldList != null) _currentFieldList.itemsSource = null;
                _currentUnavailable ??= StatusLabel(section.UnavailableReasonKey, "foc-status--unknown");
                _currentUnavailable.text = _localizer.Get(section.UnavailableReasonKey);
                SetOnlyChild(container, _currentUnavailable);
                return;
            }
            _currentFieldList ??= CreateFieldList();
            UpdateFieldList(_currentFieldList, section.Fields);
            SetOnlyChild(container, _currentFieldList);
        }

        private ListView CreateFieldList()
        {
            var list = new ListView
            {
                fixedItemHeight = 34,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                focusable = true
            };
            list.makeItem = () => new FieldRow(this);
            list.bindItem = (element, index) =>
            {
                // Read the CURRENT item source. Never close over an old route's
                // fields when a virtual row or collection is reused.
                var field = (PresentationField)list.itemsSource[index];
                var row = (FieldRow)element;
                row.Label.text = _localizer.Get(field.LabelKey);
                row.Value.text = field.Link.HasValue ? EntityLabel(field.Link.Value) : _localizer.Get(field.DisplayValue);
                row.Value.userData = field.Link.HasValue ? (object)field.Link.Value : null;
                row.Value.SetEnabled(field.Link.HasValue);
                row.Quality.text = _localizer.Get("presentation.precision." + field.Knowledge.Precision.ToString().ToLowerInvariant());
                row.tooltip = string.IsNullOrEmpty(field.TooltipKey) ? _localizer.Get(field.Knowledge.SourceKey) : _localizer.Get(field.TooltipKey);
            };
            list.unbindItem = (element, _) => ((FieldRow)element).Value.userData = null;
            list.destroyItem = element => ((FieldRow)element).Release();
            list.AddToClassList("foc-virtual-list");
            return list;
        }

        private static void UpdateFieldList(ListView list, IReadOnlyList<PresentationField> fields)
        {
            list.itemsSource = fields as System.Collections.IList ?? fields.ToList();
            list.style.height = Mathf.Clamp(fields.Count * 34f, 42f, 272f);
            var scroll = list.Q<ScrollView>();
            if (scroll != null) scroll.scrollOffset = Vector2.zero;
        }

        private static void SetOnlyChild(VisualElement parent, VisualElement child)
        {
            if (parent.childCount == 1 && ReferenceEquals(parent[0], child)) return;
            parent.Clear();
            parent.Add(child);
        }

        private sealed class FieldRow : VisualElement
        {
            public readonly Label Label = new Label { name = "field-label" };
            public readonly Button Value = new Button { name = "field-value" };
            public readonly Label Quality = new Label { name = "field-quality" };
            private Action? _click;
            public FieldRow(PresentationShellController owner)
            {
                AddToClassList("foc-field-row"); Label.AddToClassList("foc-field-label");
                Value.AddToClassList("foc-field-value"); Quality.AddToClassList("foc-quality-chip");
                _click = () => { if (!owner._disposed && Value.userData is PresentationEntityRef target) owner.OpenEntity(target); };
                Value.clicked += _click;
                Add(Label); Add(Value); Add(Quality);
            }
            public void Release()
            {
                if (_click != null) { Value.clicked -= _click; _click = null; }
                Value.userData = null;
            }
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
            var travelFeedback = _root.Q<Label>("travel-feedback");
            if (travelFeedback != null) travelFeedback.text = _localizer.Get(result.MessageKey);
            var feedback = _root.Q<Label>("action-feedback");
            if (feedback == null) return;
            feedback.text = _localizer.Get(result.MessageKey);
            feedback.EnableInClassList("foc-status--success", result.Succeeded);
            feedback.EnableInClassList("foc-status--error", !result.Succeeded);
            if (result.Succeeded) _viewModel.Refresh();
        }

        private void RenderDetails(VisualElement? container, IReadOnlyList<PresentationSection> details)
        {
            if (container == null) return;
            // Slot reuse is bounded by the largest visible detail set, not by
            // route count. Inactive slots must release their old data sources.
            for (var i = details.Count; i < _detailFoldouts.Count; i++)
            {
                _detailFieldLists[i].itemsSource = null;
                _detailFoldouts[i].RemoveFromHierarchy();
            }
            for (var i = 0; i < details.Count; i++)
            {
                if (i == _detailFoldouts.Count)
                {
                    var created = new Foldout { value = false }; created.AddToClassList("foc-detail-foldout");
                    _detailFoldouts.Add(created); _detailFieldLists.Add(CreateFieldList());
                    _detailUnavailable.Add(StatusLabel("presentation.unknown", "foc-status--unknown"));
                }
                var detail = details[i];
                var foldout = _detailFoldouts[i];
                foldout.text = _localizer.Get(detail.HeadingKey);
                foldout.value = false; // Preserve the prior per-render collapsed behavior.
                if (detail.Availability == PresentationAvailability.Available)
                {
                    UpdateFieldList(_detailFieldLists[i], detail.Fields);
                    SetOnlyChild(foldout, _detailFieldLists[i]);
                }
                else
                {
                    _detailFieldLists[i].itemsSource = null;
                    _detailUnavailable[i].text = _localizer.Get(detail.UnavailableReasonKey);
                    SetOnlyChild(foldout, _detailUnavailable[i]);
                }
                if (!ReferenceEquals(foldout.parent, container)) container.Add(foldout);
            }
        }

        private Label StatusLabel(string key, string className) { var label = new Label(_localizer.Get(key)); label.AddToClassList(className); return label; }
        private void RenderMap(ScreenPresentationState state)
        {
            var toolbar = _root.Q<VisualElement>("world-map-travel");
            var map = _root.Q<VisualElement>("world-map-panel");
            if (toolbar != null && map != null && state.Map?.Travel != null)
            {
                toolbar.style.display = DisplayStyle.Flex; map.style.display = DisplayStyle.Flex;
                _travelPanel ??= new WorldMapTravelPanel(map, toolbar, _localizer, OpenEntity,
                    action => { if (!_disposed) ShowActionResult(_dispatcher?.Dispatch(action) ?? PresentationActionResult.Rejected("presentation.action.binding-unavailable")); });
                _travelPanel.Render(state);
                SetText("campaign-clock", TravelPresentationText.Clock(state.Map.Travel.ClockTicks));
                return;
            }
            if (toolbar != null) toolbar.style.display = DisplayStyle.None;
            _travelPanel?.Dispose(); _travelPanel = null;
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
            _travelPanel?.Dispose(); _travelPanel = null;
            _viewModel.Changed -= Render;
            _tradePanel?.Dispose(); _tradePanel = null; _trade?.Dispose();
            _root.UnregisterCallback(_keyHandler);
            _root.UnregisterCallback(_geometryHandler);
            foreach (var binding in _buttonHandlers) binding.Item1.clicked -= binding.Item2;
            _buttonHandlers.Clear();
            _disposed = true;
            if (_currentFieldList != null) ReleaseFieldList(_currentFieldList);
            foreach (var list in _detailFieldLists) ReleaseFieldList(list);
            _currentUnavailable?.RemoveFromHierarchy();
            foreach (var foldout in _detailFoldouts) foldout.RemoveFromHierarchy();
            _currentFieldList = null; _currentUnavailable = null;
            _detailFieldLists.Clear(); _detailFoldouts.Clear(); _detailUnavailable.Clear();
        }
        private static void ReleaseFieldList(ListView list)
        {
            list.RemoveFromHierarchy();
            list.itemsSource = null;
            list.Rebuild(); // Destroy owned rows while their release hook exists.
            list.makeItem = null; list.bindItem = null; list.unbindItem = null; list.destroyItem = null;
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PresentationShellController)); }
    }
}
