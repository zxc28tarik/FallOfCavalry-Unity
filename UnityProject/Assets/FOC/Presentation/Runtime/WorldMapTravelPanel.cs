#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Presentation.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    /// <summary>Retained UI only. Selection navigates; commands dispatch to the existing application binding.</summary>
    internal sealed class WorldMapTravelPanel : IDisposable
    {
        private readonly VisualElement _map;
        private readonly VisualElement _toolbar;
        private readonly IPresentationLocalizer _localizer;
        private readonly Action<PresentationEntityRef> _select;
        private readonly Action<PresentationActionDescriptor> _dispatch;
        private readonly DropdownField _destination;
        private readonly Label _route = new Label { name = "travel-route" };
        private readonly Label _summary = new Label { name = "travel-summary" };
        private readonly Label _stops = new Label { name = "travel-stops" };
        private readonly Label _status = new Label { name = "travel-status" };
        private readonly ProgressBar _progress = new ProgressBar { name = "travel-progress", lowValue = 0, highValue = 100 };
        private readonly Button _start, _advance, _city;
        private readonly Dictionary<PresentationEntityRef, Button> _markers = new Dictionary<PresentationEntityRef, Button>();
        private MapPresentationSnapshot? _snapshot;
        private List<MapMarkerPresentation> _locations = new List<MapMarkerPresentation>();
        private PresentationActionDescriptor? _startAction, _advanceAction;
        private bool _disposed, _rendering;

        public WorldMapTravelPanel(VisualElement map, VisualElement toolbar, IPresentationLocalizer localizer,
            Action<PresentationEntityRef> select, Action<PresentationActionDescriptor> dispatch)
        {
            _map = map; _toolbar = toolbar; _localizer = localizer; _select = select; _dispatch = dispatch;
            var top = new VisualElement(); top.AddToClassList("foc-travel-top");
            _destination = new DropdownField("Hedef") { name = "travel-destination", focusable = true };
            _destination.RegisterValueChangedCallback(OnDestination);
            top.Add(_destination);
            var summary = new VisualElement(); summary.AddToClassList("foc-travel-summary");
            _route.AddToClassList("foc-travel-route"); summary.Add(_route); summary.Add(_summary); top.Add(summary);
            var actions = new VisualElement(); actions.AddToClassList("foc-travel-buttons");
            _start = new Button(Start) { name = "travel-start", text = "Yola çık", focusable = true };
            _advance = new Button(Advance) { name = "travel-advance", text = "1 saat ilerlet", focusable = true };
            _city = new Button(OpenCity) { name = "travel-open-city", text = "Şehri incele", focusable = true };
            actions.Add(_start); actions.Add(_advance); actions.Add(_city); top.Add(actions);
            _toolbar.Add(top); _toolbar.Add(_stops); _toolbar.Add(_progress); _toolbar.Add(_status);
            _toolbar.Add(new Label { name = "travel-feedback" });
            var help = new Label("Yer seç: haritaya tıkla veya hedef listesini kullan. Tab / Shift+Tab ile ilerle, Enter ile onayla. Esc: geri.")
                { name = "travel-help" }; help.AddToClassList("foc-muted"); _toolbar.Add(help);
            var legend = new Label("Şematik yol haritası · Altın: seçili rota · Kırmızı: etkin yolculuk · Noktalar: konumlar")
                { name = "map-legend", pickingMode = PickingMode.Ignore };
            legend.AddToClassList("foc-map-progress"); _map.Add(legend);
            // The decorative Marmara painting is not georeferenced. Never imply that its
            // coastline is authoritative underneath projected campaign coordinates.
            _map.style.backgroundImage = StyleKeyword.None;
            _map.generateVisualContent += DrawRoutes;
            _map.RegisterCallback<GeometryChangedEvent>(OnMapGeometry);
        }

        public void Render(ScreenPresentationState state)
        {
            if (_disposed || state.Map?.Travel == null) return;
            _snapshot = state.Map;
            var travel = _snapshot.Travel;
            _locations = _snapshot.Markers.Where(x => x.Entity.Kind == PresentationEntityKind.WorldLocation).ToList();
            _rendering = true;
            _destination.choices = _locations.Select(x => _localizer.Get(x.LabelKey)).ToList();
            var chosen = _locations.FindIndex(x => travel.Selected.HasValue && x.Entity.Equals(travel.Selected.Value));
            _destination.SetValueWithoutNotify(chosen < 0 ? string.Empty : _destination.choices[chosen]);
            _rendering = false;
            _route.text = travel.ActorLabel + "  ·  " + travel.OriginLabel + " → " + travel.DestinationLabel;
            _summary.text = travel.TotalTicks > 0
                ? (travel.DistanceMeters / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + " km  ·  "
                    + (travel.Active ? "Kalan " : "Süre ") + TravelPresentationText.Duration(travel.RemainingTicks)
                    + "  ·  Tahmini varış: " + TravelPresentationText.Clock(checked(travel.ClockTicks + travel.RemainingTicks))
                : "Rota ve süre için farklı bir hedef seç.";
            _stops.text = travel.Stops.Count > 0 ? string.Join(" → ", travel.Stops) : string.Empty;
            _status.text = _localizer.Get(travel.ReasonKey);
            _status.AddToClassList("foc-travel-status");
            _progress.style.display = travel.Active ? DisplayStyle.Flex : DisplayStyle.None;
            _progress.value = 100 * travel.Progress;
            _progress.title = "Yolculuk · %" + Mathf.FloorToInt(_progress.value);
            _startAction = state.Actions.FirstOrDefault(x => x.ActionId == "travel.start:" + travel.Selected?.Id);
            _advanceAction = state.Actions.FirstOrDefault(x => x.ActionId == "travel.advance-one-hour");
            _start.SetEnabled(_startAction?.IsEnabled == true);
            _start.tooltip = _startAction == null ? _localizer.Get(travel.ReasonKey) : _localizer.Get(_startAction.DisabledReasonKey);
            _advance.SetEnabled(_advanceAction?.IsEnabled == true);
            _advance.tooltip = _advanceAction == null ? string.Empty : _localizer.Get(_advanceAction.DisabledReasonKey);
            _city.SetEnabled(travel.City.HasValue);
            var clock = _toolbar.panel?.visualTree.Q<Label>("campaign-clock");
            if (clock != null) clock.text = TravelPresentationText.Clock(travel.ClockTicks);
            foreach (var key in _markers.Keys.Where(x => !_snapshot.Markers.Any(m => m.Entity.Equals(x))).ToArray())
            { _markers[key].RemoveFromHierarchy(); _markers.Remove(key); }
            foreach (var marker in _snapshot.Markers)
            {
                if (!_markers.TryGetValue(marker.Entity, out var button))
                {
                    var key = marker.Entity;
                    button = new Button(() => { if (!_disposed) SelectMarker(key); })
                        { name = "map-marker-" + key.Id, focusable = true };
                    button.AddToClassList("foc-map-marker"); _markers.Add(key, button); _map.Add(button);
                }
                button.text = _localizer.Get(marker.LabelKey);
                button.tooltip = _localizer.Get(marker.Knowledge.SourceKey);
                button.style.position = Position.Absolute;
                button.EnableInClassList("foc-map-marker--selected", travel.Selected.HasValue && marker.Entity.Equals(travel.Selected.Value));
            }
            LayoutMarkers();
            _map.MarkDirtyRepaint();
        }

        private void DrawRoutes(MeshGenerationContext context)
        {
            if (_disposed || _snapshot == null) return;
            var painter = context.painter2D;
            var size = _map.contentRect.size;
            painter.lineWidth = 1; painter.strokeColor = new Color(.55f,.65f,.62f,.12f);
            for (var column = 1; column < 12; column++)
            {
                var x = column * size.x / 12;
                painter.BeginPath(); painter.MoveTo(new Vector2(x,0)); painter.LineTo(new Vector2(x,size.y)); painter.Stroke();
            }
            for (var row = 1; row < 6; row++)
            {
                var y = row * size.y / 6;
                painter.BeginPath(); painter.MoveTo(new Vector2(0,y)); painter.LineTo(new Vector2(size.x,y)); painter.Stroke();
            }
            foreach (var route in _snapshot.Routes)
            {
                var selected = _snapshot.Travel?.RouteIds.Contains(route.RouteId) == true;
                painter.lineWidth = selected || route.Active ? 3 : 1.5f;
                painter.strokeColor = route.Active ? new Color(.9f,.25f,.12f) : selected ? new Color(1,.77f,.25f) : new Color(.60f,.65f,.57f,.72f);
                painter.BeginPath(); painter.MoveTo(new Vector2(route.FirstX * size.x, route.FirstY * size.y));
                painter.LineTo(new Vector2(route.SecondX * size.x, route.SecondY * size.y)); painter.Stroke();
            }
            foreach (var marker in _snapshot.Markers)
            {
                var point = new Vector2(marker.NormalizedX * size.x, marker.NormalizedY * size.y);
                if (_markers.TryGetValue(marker.Entity, out var button) && button.userData is Vector2 label)
                {
                    painter.lineWidth = 1; painter.strokeColor = new Color(.95f,.85f,.6f,.65f);
                    painter.BeginPath(); painter.MoveTo(point); painter.LineTo(new Vector2(label.x * size.x, label.y * size.y)); painter.Stroke();
                }
                painter.fillColor = marker.Entity.Kind == PresentationEntityKind.WorldLocation ? new Color(1,.83f,.4f) : new Color(.85f,.2f,.15f);
                painter.BeginPath(); painter.MoveTo(point + new Vector2(0,-4)); painter.LineTo(point + new Vector2(4,0));
                painter.LineTo(point + new Vector2(0,4)); painter.LineTo(point + new Vector2(-4,0)); painter.ClosePath(); painter.Fill();
            }
        }
        private void SelectMarker(PresentationEntityRef entity)
        {
            var marker = _snapshot?.Markers.FirstOrDefault(x => x.Entity.Equals(entity));
            if (marker == null) return;
            if (entity.Kind == PresentationEntityKind.WorldLocation) _select(entity);
            else if (marker.NavigationTarget.HasValue) _select(marker.NavigationTarget.Value);
        }
        private void OnDestination(ChangeEvent<string> evt)
        {
            if (_disposed || _rendering) return;
            var index = _destination.choices.IndexOf(evt.newValue);
            if (index >= 0 && index < _locations.Count) _select(_locations[index].Entity);
        }
        private void Start() { if (!_disposed && _startAction?.IsEnabled == true) _dispatch(_startAction); }
        private void Advance() { if (!_disposed && _advanceAction?.IsEnabled == true) _dispatch(_advanceAction); }
        private void OpenCity() { if (!_disposed && _snapshot?.Travel?.City != null) _select(_snapshot.Travel.City.Value); }
        private void OnMapGeometry(GeometryChangedEvent evt) { if (!_disposed) LayoutMarkers(); }
        private void LayoutMarkers()
        {
            if (_snapshot == null) return;
            var size = _map.contentRect.size;
            if (size.x < 150 || size.y < 100) size = new Vector2(900,380);
            var occupied = new List<Rect>();
            foreach (var marker in _snapshot.Markers)
            {
                var point = FindLabelPosition(marker.NormalizedX, marker.NormalizedY, size, occupied);
                occupied.Add(new Rect(point.x*size.x-59,point.y*size.y-18,118,36));
                var button = _markers[marker.Entity];
                button.style.left = Length.Percent(100*point.x); button.style.top = Length.Percent(100*point.y);
                button.userData = point;
            }
            _map.MarkDirtyRepaint();
        }
        internal static Vector2 FindLabelPosition(float x, float y, Vector2 size, IReadOnlyList<Rect> occupied)
        {
            // Label displacement never changes the authoritative anchor or route geometry.
            var best = Vector2.zero; var bestDistance = float.PositiveInfinity;
            for (var dx=-10;dx<=10;dx++)
                for (var dy=-10;dy<=10;dy++)
                {
                    var candidate = new Vector2(Mathf.Clamp(x*size.x+dx*120,62,size.x-62),
                        Mathf.Clamp(y*size.y-22+dy*38,22,size.y-58));
                    var box = new Rect(candidate.x-59,candidate.y-18,118,36);
                    var distance = (candidate-new Vector2(x*size.x,y*size.y-22)).sqrMagnitude;
                    if (distance >= bestDistance || occupied.Any(r => r.Overlaps(box))) continue;
                    best=candidate;bestDistance=distance;
                }
            if (float.IsPositiveInfinity(bestDistance)) best=new Vector2(Mathf.Clamp(x*size.x,62,size.x-62),Mathf.Clamp(y*size.y,22,size.y-58));
            return new Vector2(best.x/size.x,best.y/size.y);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _map.generateVisualContent -= DrawRoutes; _destination.UnregisterValueChangedCallback(OnDestination);
            _map.UnregisterCallback<GeometryChangedEvent>(OnMapGeometry);
            _start.clicked -= Start; _advance.clicked -= Advance; _city.clicked -= OpenCity;
            _snapshot = null; _startAction = null; _advanceAction = null; _locations.Clear();
            _markers.Clear(); _map.Clear(); _toolbar.Clear();
        }
    }
}
