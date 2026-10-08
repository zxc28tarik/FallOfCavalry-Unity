#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    internal sealed class EnvoyTrackingPanel : IDisposable
    {
        private readonly VisualElement _root;
        private readonly EnvoyTrackingSession _session;
        private readonly Action<string> _navigate;
        private readonly TextField _search = new TextField("Görev ara") { name = "envoy-search", maxLength = 80 };
        private readonly DropdownField _type = new DropdownField("Bilinen durum") { name = "envoy-type" };
        private readonly Label _summary = new Label { name = "envoy-inbox-summary" }, _empty = new Label { name = "envoy-inbox-empty" },
            _title = new Label { name = "envoy-detail-title" }, _metadata = new Label { name = "envoy-detail-metadata" };
        private readonly ListView _rows = new ListView { name = "envoy-inbox-rows", fixedItemHeight = 48, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private readonly ListView _messages = new ListView { name = "envoy-messages", fixedItemHeight = 60, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private EnvoyTrackingSnapshot? _snapshot;
        private bool _disposed;
        private sealed class Row : Button
        {
            public string MissionId = "";
            private readonly Action _click;
            public Row(Action<string> open) { _click = () => { if (MissionId.Length > 0) open(MissionId); }; clicked += _click; }
            public void Release() { clicked -= _click; MissionId = ""; }
        }
        public EnvoyTrackingPanel(VisualElement root, EnvoyTrackingSession session, Action<string> navigate)
        {
            _root = root; _session = session; _navigate = navigate;
            var heading = new Label("ELÇİ DEFTERİ · BİLGİ SINIRI KORUNUR"); heading.AddToClassList("foc-card-title"); root.Add(heading);
            var selectors = new VisualElement(); selectors.AddToClassList("foc-trade-selectors"); selectors.Add(_search); selectors.Add(_type); root.Add(selectors);
            _type.choices = EnvoyTrackingText.FilterLabels.ToList();
            root.Add(_summary); root.Add(_empty); root.Add(_rows); root.Add(_title); root.Add(_metadata); root.Add(_messages);
            _title.AddToClassList("foc-city-heading");
            _rows.makeItem = () => { var row = new Row(id => { if (!_disposed) _navigate(id); }); row.AddToClassList("foc-envoy-row"); return row; };
            _rows.bindItem = (element, index) => {
                var row = (Row)element; var data = (EnvoyTrackingRow)_rows.itemsSource[index];
                row.MissionId = data.Id; row.text = data.Text; row.tooltip = data.Id + " · " + data.TargetReference + "\n" + data.Text;
                row.EnableInClassList("foc-envoy-selected", data.Id == _snapshot?.SelectedId);
            };
            _rows.unbindItem = (element, _) => { var row = (Row)element; row.MissionId = ""; row.text = ""; row.tooltip = ""; };
            _rows.destroyItem = element => ((Row)element).Release();
            _messages.makeItem = () => { var label = new Label(); label.AddToClassList("foc-envoy-observation"); return label; };
            _messages.bindItem = (element, index) => { var label = (Label)element; label.text = (string)_messages.itemsSource[index]; label.tooltip = label.text; };
            _messages.unbindItem = (element, _) => { ((Label)element).text = ""; element.tooltip = ""; };
            _search.RegisterValueChangedCallback(OnSearch); _type.RegisterValueChangedCallback(OnType);
        }
        public void Render(string? selected = null)
        {
            if (_disposed) return;
            _session.OpenMission(selected); var snapshot = _session.Read(); _snapshot = snapshot;
            _search.SetValueWithoutNotify(snapshot.Search); _type.SetValueWithoutNotify(_type.choices[snapshot.FilterIndex]);
            _summary.text = snapshot.Rows.Count + " / " + snapshot.TotalKnown + " bilinen görev · En yeni emir önce";
            _empty.text = snapshot.EmptyMessage; _empty.style.display = snapshot.Rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _rows.itemsSource = snapshot.Rows.ToList(); _rows.RefreshItems(); _rows.style.height = Math.Min(snapshot.Rows.Count, 4) * 48;
            _rows.style.display = snapshot.Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = snapshot.Detail != null ? "Görev · " + snapshot.Detail.Id : (snapshot.SelectedId.Length > 0 ? "Bu görev filtre dışında veya erişilemiyor." : "Görev ayrıntısı");
            _metadata.text = snapshot.Detail?.Metadata ?? "Uzak görev durumu bilinmiyor kalır. Bu ekran emir vermiyor veya zamanı ilerletmiyor.";
            _messages.itemsSource = snapshot.Detail?.Messages.ToList(); _messages.RefreshItems();
            _messages.style.height = Math.Min(snapshot.Detail?.Messages.Count ?? 0, 4) * 60;
            _messages.style.display = snapshot.Detail != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private void OnSearch(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(evt.newValue, Math.Max(0, _type.index)); Render(); } }
        private void OnType(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(_search.value, _type.choices.IndexOf(evt.newValue)); Render(); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _search.UnregisterValueChangedCallback(OnSearch); _type.UnregisterValueChangedCallback(OnType);
            ReleaseList(_rows); ReleaseList(_messages); _snapshot = null; _root.Clear();
        }
        private static void ReleaseList(ListView list)
        { list.itemsSource = null; list.Rebuild(); list.makeItem = null; list.bindItem = null; list.unbindItem = null; list.destroyItem = null; }
    }
}
