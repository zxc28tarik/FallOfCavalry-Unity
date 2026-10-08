#nullable enable
using System;
using System.Linq;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    internal sealed class CharacterDirectoryPanel : IDisposable
    {
        private readonly VisualElement _root;
        private readonly CharacterDirectorySession _session;
        private readonly Action<string> _open;
        private readonly TextField _search = new TextField("Karakter ara") { name = "character-search", maxLength = 80 };
        private readonly Label _count = new Label { name = "character-directory-count" };
        private readonly Label _empty = new Label { name = "character-directory-empty" };
        private readonly Label _summary = new Label { name = "character-directory-summary" };
        private readonly ListView _rows = new ListView { name = "character-directory-rows", fixedItemHeight = 42, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private CharacterDirectorySnapshot? _snapshot;
        private bool _disposed;

        private sealed class Row : Button
        {
            public string? Id;
            private readonly Action _click;
            public Row(Action<string> open) { _click = () => { if (Id != null) open(Id); }; clicked += _click; }
            public void Release() { clicked -= _click; Id = null; }
        }

        public CharacterDirectoryPanel(VisualElement root, CharacterDirectorySession session, Action<string> open)
        {
            _root = root; _session = session; _open = open;
            root.Add(new Label("KARAKTER DEFTERİ · SALT OKUNUR") { name = "character-directory-title" });
            root.Add(new Label("Yalnız kesin erişim verilen karakterler listelenir. Portre, canlı takip veya bilinmeyen yabancı kişi verisi üretilmez."));
            root.Add(_search); root.Add(_count); root.Add(_empty); root.Add(_rows); root.Add(_summary);
            _summary.AddToClassList("foc-character-summary");
            _rows.makeItem = () => { var row = new Row(id => { if (!_disposed) _open(id); }); row.AddToClassList("foc-character-row"); return row; };
            _rows.bindItem = (element, index) => { var row = (Row)element; var data = (CharacterDirectoryRow)_rows.itemsSource[index]; row.Id = data.Id; row.text = data.Text; row.tooltip = data.Id + "\n" + data.Text; row.EnableInClassList("foc-character-selected", data.Id == _snapshot?.SelectedId); };
            _rows.unbindItem = (element, _) => { var row = (Row)element; row.Id = null; row.text = ""; row.tooltip = ""; };
            _rows.destroyItem = element => ((Row)element).Release();
            _search.RegisterValueChangedCallback(OnSearch);
        }

        public void Render(string? selected)
        {
            if (_disposed) return;
            _session.OpenCharacter(selected); var snapshot = _session.Read(); _snapshot = snapshot;
            _search.SetValueWithoutNotify(snapshot.Search);
            _count.text = snapshot.Rows.Count + " / " + snapshot.TotalKnown + " bilinen karakter";
            _empty.text = snapshot.EmptyMessage; _empty.style.display = snapshot.Rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _rows.itemsSource = snapshot.Rows.ToList(); _rows.RefreshItems(); _rows.style.height = Math.Min(snapshot.Rows.Count, 3) * 42;
            _rows.style.display = snapshot.Rows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _summary.text = snapshot.Summary;
        }

        private void OnSearch(ChangeEvent<string> evt) { if (!_disposed) { _session.Filter(evt.newValue); Render(null); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _search.UnregisterValueChangedCallback(OnSearch);
            _rows.itemsSource = null; _rows.Rebuild(); _rows.makeItem = null; _rows.bindItem = null; _rows.unbindItem = null; _rows.destroyItem = null;
            _snapshot = null; _root.Clear();
        }
    }
}
