#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    internal sealed class ReportInboxPanel : IDisposable
    {
        private readonly VisualElement _root;
        private readonly ReportInboxSession _session;
        private readonly Action<string> _navigate;
        private readonly TextField _search = new TextField("Rapor ara") { name = "report-search", maxLength = 80 };
        private readonly DropdownField _type = new DropdownField("Tür") { name = "report-type" };
        private readonly Label _summary = new Label { name = "report-inbox-summary" }, _empty = new Label { name = "report-inbox-empty" },
            _title = new Label { name = "report-detail-title" }, _metadata = new Label { name = "report-detail-metadata" };
        private readonly ListView _rows = new ListView { name = "report-inbox-rows", fixedItemHeight = 48, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private readonly ListView _observations = new ListView { name = "report-observations", fixedItemHeight = 60, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private ReportInboxSnapshot? _snapshot;
        private bool _disposed;
        private sealed class Row : Button
        {
            public string ReportId = "";
            private readonly Action _click;
            public Row(Action<string> open) { _click = () => { if (ReportId.Length > 0) open(ReportId); }; clicked += _click; }
            public void Release() { clicked -= _click; ReportId = ""; }
        }
        public ReportInboxPanel(VisualElement root, ReportInboxSession session, Action<string> navigate)
        {
            _root = root; _session = session; _navigate = navigate;
            var heading = new Label("RAPOR DEFTERİ · TESLİM EDİLMİŞ BİLGİ"); heading.AddToClassList("foc-card-title"); root.Add(heading);
            var selectors = new VisualElement(); selectors.AddToClassList("foc-trade-selectors"); selectors.Add(_search); selectors.Add(_type); root.Add(selectors);
            _type.choices = ReportInboxText.FilterLabels.ToList();
            root.Add(_summary); root.Add(_empty); root.Add(_rows); root.Add(_title); root.Add(_metadata); root.Add(_observations);
            _title.AddToClassList("foc-city-heading");
            _rows.makeItem = () => { var row = new Row(id => { if (!_disposed) _navigate(id); }); row.AddToClassList("foc-report-row"); return row; };
            _rows.bindItem = (element, index) => {
                var row = (Row)element; var data = (ReportInboxRow)_rows.itemsSource[index];
                row.ReportId = data.Id; row.text = data.Text; row.tooltip = data.Id + " · " + data.SourceReference + "\n" + data.Text;
                row.EnableInClassList("foc-report-selected", data.Id == _snapshot?.SelectedId);
            };
            _rows.unbindItem = (element, _) => { var row = (Row)element; row.ReportId = ""; row.text = ""; row.tooltip = ""; };
            _rows.destroyItem = element => ((Row)element).Release();
            _observations.makeItem = () => { var label = new Label(); label.AddToClassList("foc-report-observation"); return label; };
            _observations.bindItem = (element, index) => { var label = (Label)element; label.text = (string)_observations.itemsSource[index]; label.tooltip = label.text; };
            _observations.unbindItem = (element, _) => { ((Label)element).text = ""; element.tooltip = ""; };
            _search.RegisterValueChangedCallback(OnSearch); _type.RegisterValueChangedCallback(OnType);
        }
        public void Render(string? selected = null)
        {
            if (_disposed) return;
            _session.OpenReport(selected); var snapshot = _session.Read(); _snapshot = snapshot;
            _search.SetValueWithoutNotify(snapshot.Search); _type.SetValueWithoutNotify(_type.choices[snapshot.FilterIndex]);
            _summary.text = snapshot.Rows.Count + " / " + snapshot.TotalDelivered + " teslim edilmiş rapor · En son teslim edilen önce";
            _empty.text = snapshot.EmptyMessage; _empty.style.display = snapshot.Rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _rows.itemsSource = snapshot.Rows.ToList(); _rows.RefreshItems(); _rows.style.height = Math.Min(snapshot.Rows.Count, 4) * 48;
            _rows.style.display = snapshot.Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = snapshot.Detail?.Title ?? (snapshot.SelectedId.Length > 0 ? "Bu rapor filtre dışında veya erişilemiyor." : "Rapor ayrıntısı");
            _metadata.text = snapshot.Detail?.Metadata ?? "Yalnız teslim edilmiş raporlar okunabilir. Bu ekran rapor göndermiyor veya zamanı ilerletmiyor.";
            _observations.itemsSource = snapshot.Detail?.Observations.ToList(); _observations.RefreshItems();
            _observations.style.height = Math.Min(snapshot.Detail?.Observations.Count ?? 0, 4) * 60;
            _observations.style.display = snapshot.Detail != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private void OnSearch(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(evt.newValue, Math.Max(0, _type.index)); Render(); } }
        private void OnType(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(_search.value, _type.choices.IndexOf(evt.newValue)); Render(); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _search.UnregisterValueChangedCallback(OnSearch); _type.UnregisterValueChangedCallback(OnType);
            ReleaseList(_rows); ReleaseList(_observations); _snapshot = null; _root.Clear();
        }
        private static void ReleaseList(ListView list)
        { list.itemsSource = null; list.Rebuild(); list.makeItem = null; list.bindItem = null; list.unbindItem = null; list.destroyItem = null; }
    }
}
