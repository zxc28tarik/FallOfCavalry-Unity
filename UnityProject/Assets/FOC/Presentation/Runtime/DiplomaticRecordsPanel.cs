#nullable enable
using System;
using System.Linq;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    internal sealed class DiplomaticRecordsPanel : IDisposable
    {
        private readonly VisualElement _root;
        private readonly DiplomaticRecordsSession _session;
        private readonly Foldout _foldout = new Foldout { name = "diplomatic-records-foldout", text = "İLİŞKİ VE ANLAŞMA DEFTERİ · SALT OKUNUR", value = false };
        private readonly TextField _search = new TextField("Kayıt ara") { name = "diplomatic-search", maxLength = 80 };
        private readonly DropdownField _type = new DropdownField("Kayıt türü") { name = "diplomatic-type" };
        private readonly Label _summary = new Label { name = "diplomatic-summary" }, _empty = new Label { name = "diplomatic-empty" },
            _title = new Label { name = "diplomatic-detail-title" }, _metadata = new Label { name = "diplomatic-detail-metadata" };
        private readonly ListView _rows = new ListView { name = "diplomatic-rows", fixedItemHeight = 42, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private readonly ListView _entries = new ListView { name = "diplomatic-entries", fixedItemHeight = 58, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private DiplomaticRecordsSnapshot? _snapshot;
        private bool _disposed;
        private sealed class Row : Button
        {
            public PresentationEntityRef? Subject;
            private readonly Action _click;
            public Row(Action<PresentationEntityRef> open) { _click = () => { if (Subject.HasValue) open(Subject.Value); }; clicked += _click; }
            public void Release() { clicked -= _click; Subject = null; }
        }
        public DiplomaticRecordsPanel(VisualElement root, DiplomaticRecordsSession session, Action<PresentationEntityRef> navigate)
        {
            _root = root; _session = session; root.Add(_foldout);
            var note = new Label("Yalnız kendi aktörünüzün taraf kayıtları. Yabancı-yabancı kayıtlar ve canlı dünya teyidi açılmaz.");
            _foldout.Add(note);
            var selectors = new VisualElement(); selectors.AddToClassList("foc-trade-selectors"); selectors.Add(_search); selectors.Add(_type); _foldout.Add(selectors);
            _type.choices = DiplomaticRecordsText.FilterLabels.ToList();
            _foldout.Add(_summary); _foldout.Add(_empty); _foldout.Add(_rows); _foldout.Add(_title); _foldout.Add(_metadata); _foldout.Add(_entries);
            _title.AddToClassList("foc-city-heading");
            _rows.makeItem = () => { var row = new Row(subject => { if (!_disposed) navigate(subject); }); row.AddToClassList("foc-diplomatic-row"); return row; };
            _rows.bindItem = (element, index) => {
                var row = (Row)element; var data = (DiplomaticRecordRow)_rows.itemsSource[index];
                row.Subject = data.Subject; row.text = data.Text; row.tooltip = data.Subject.Id + "\n" + data.Text;
                row.EnableInClassList("foc-diplomatic-selected", data.Subject.Equals(_snapshot?.Selected));
            };
            _rows.unbindItem = (element, _) => { var row = (Row)element; row.Subject = null; row.text = ""; row.tooltip = ""; };
            _rows.destroyItem = element => ((Row)element).Release();
            _entries.makeItem = () => { var label = new Label(); label.AddToClassList("foc-diplomatic-entry"); return label; };
            _entries.bindItem = (element, index) => { var label = (Label)element; label.text = (string)_entries.itemsSource[index]; label.tooltip = label.text; };
            _entries.unbindItem = (element, _) => { ((Label)element).text = ""; element.tooltip = ""; };
            _search.RegisterValueChangedCallback(OnSearch); _type.RegisterValueChangedCallback(OnType);
        }
        public void Render(PresentationEntityRef? selected = null)
        {
            if (_disposed) return;
            _session.OpenRecord(selected); var snapshot = _session.Read(); _snapshot = snapshot;
            if (selected.HasValue) _foldout.value = true;
            _search.SetValueWithoutNotify(snapshot.Search); _type.SetValueWithoutNotify(_type.choices[snapshot.FilterIndex]);
            _summary.text = snapshot.Rows.Count + " / " + snapshot.TotalKnown + " bilinen taraf kaydı · En yeni kayıt önce";
            _empty.text = snapshot.EmptyMessage; _empty.style.display = snapshot.Rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _rows.itemsSource = snapshot.Rows.ToList(); _rows.RefreshItems(); _rows.style.height = Math.Min(snapshot.Rows.Count, 3) * 42;
            _rows.style.display = snapshot.Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = snapshot.Detail != null ? (snapshot.Detail.Subject.Kind == PresentationEntityKind.DiplomaticRelation ? "İlişki" : "Anlaşma") + " · " + snapshot.Detail.Subject.Id
                : (snapshot.Selected.HasValue ? "Bu kayıt filtre dışında veya erişilemiyor." : "Kayıt ayrıntısı");
            _metadata.text = snapshot.Detail?.Metadata ?? "Bilinmeyen kayıt yerine başka bir kayıt gösterilmez. Teslim edilmiş dış gözlemler Raporlar ekranındadır.";
            _entries.itemsSource = snapshot.Detail?.Entries.ToList(); _entries.RefreshItems();
            _entries.style.height = Math.Min(snapshot.Detail?.Entries.Count ?? 0, 3) * 58;
            _entries.style.display = snapshot.Detail != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private void OnSearch(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(evt.newValue, Math.Max(0, _type.index)); Render(); } }
        private void OnType(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(_search.value, _type.choices.IndexOf(evt.newValue)); Render(); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _search.UnregisterValueChangedCallback(OnSearch); _type.UnregisterValueChangedCallback(OnType);
            ReleaseList(_rows); ReleaseList(_entries); _snapshot = null; _root.Clear();
        }
        private static void ReleaseList(ListView list)
        { list.itemsSource = null; list.Rebuild(); list.makeItem = null; list.bindItem = null; list.unbindItem = null; list.destroyItem = null; }
    }
}
