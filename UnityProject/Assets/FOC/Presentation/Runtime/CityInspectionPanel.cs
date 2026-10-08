#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Cities;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    internal sealed class CityInspectionPanel : IDisposable
    {
        private readonly VisualElement _root, _content = new VisualElement(), _areaGrid = new VisualElement();
        private readonly CityInspectionSession _session;
        private readonly Action<string> _navigate, _market;
        private readonly DropdownField _city = new DropdownField("Şehir") { name = "city-selector" }, _recipe = new DropdownField("Reçete") { name = "city-recipe" };
        private readonly TextField _search = new TextField("Mal ara") { name = "city-stock-search", maxLength = 80 };
        private readonly Toggle _shortages = new Toggle("Yalnız eksikler / hesaplanamayanlar") { name = "city-stock-shortages" };
        private readonly Label _summary = new Label { name = "city-summary" }, _heading = new Label { name = "city-detail-heading" },
            _detail = new Label { name = "city-production-detail" }, _reason = new Label { name = "city-production-reason" }, _note = new Label { name = "city-inspection-note" };
        private readonly ListView _rows = new ListView { name = "city-inspection-rows", fixedItemHeight = 34, selectionType = SelectionType.None, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight };
        private readonly List<Tuple<Button, Action>> _handlers = new List<Tuple<Button, Action>>();
        private readonly List<Button> _tabs = new List<Button>(), _areas = new List<Button>();
        private readonly Button _marketButton;
        private CityInspectionSnapshot? _snapshot;
        private bool _disposed;
        public CityInspectionPanel(VisualElement root, CityInspectionSession session, Action<string> navigate, Action<string> market)
        {
            _root = root; _session = session; _navigate = navigate; _market = market;
            var title = new Label("ŞEHİR DEFTERİ · ALANLAR VE KAYNAKLAR"); title.AddToClassList("foc-card-title"); root.Add(title);
            var top = new VisualElement(); top.AddToClassList("foc-trade-selectors"); top.Add(_city);
            _marketButton = Button("city-open-market", "Bu şehrin pazarını aç", () => { if (_snapshot?.Available == true) _market(_snapshot.CityId); }); top.Add(_marketButton); root.Add(top); root.Add(_summary);
            var tabs = new VisualElement(); tabs.AddToClassList("foc-trade-buttons");
            var names = new[] { "Alanlar", "Üretim incelemesi", "Stok ve talep", "Altyapı ve görevler" };
            for (var i = 0; i < names.Length; i++) { var tab = (CityInspectionTab)i; var b = Button("city-tab-" + i, names[i], () => { _session.SelectTab(tab); Render(); }); _tabs.Add(b); tabs.Add(b); }
            root.Add(tabs); root.Add(_content); _content.Add(_areaGrid); _areaGrid.AddToClassList("foc-city-areas");
            foreach (CityAreaType type in Enum.GetValues(typeof(CityAreaType)))
            { var area = type; var b = Button("city-area-" + (int)type, CityInspectionText.Area(type), () => { _session.SelectArea(area); Render(); }); b.AddToClassList("foc-city-area"); _areas.Add(b); _areaGrid.Add(b); }
            _content.Add(_recipe); _content.Add(_search); _content.Add(_shortages); _content.Add(_heading); _heading.AddToClassList("foc-city-heading");
            _content.Add(_detail); _content.Add(_reason); _content.Add(_rows); _content.Add(_note);
            _rows.makeItem = () => { var l = new Label(); l.AddToClassList("foc-city-row"); return l; };
            _rows.bindItem = (element, i) => { var l = (Label)element; l.text = (string)_rows.itemsSource[i]; l.tooltip = l.text; };
            _rows.unbindItem = (element, _) => { ((Label)element).text = ""; element.tooltip = ""; };
            _city.RegisterValueChangedCallback(OnCity); _recipe.RegisterValueChangedCallback(OnRecipe);
            _search.RegisterValueChangedCallback(OnSearch); _shortages.RegisterValueChangedCallback(OnShortages);
        }
        private Button Button(string name, string text, Action action)
        { Action guarded = () => { if (!_disposed) action(); }; var b = new Button(guarded) { name = name, text = text }; _handlers.Add(Tuple.Create(b, guarded)); return b; }
        public void Render(string? city = null)
        {
            if (_disposed) return;
            _session.OpenCity(city); var s = _session.Read(); _snapshot = s;
            Choices(_city, s.Cities, s.CityId); _summary.text = s.Summary; _content.style.display = s.Available ? DisplayStyle.Flex : DisplayStyle.None; _marketButton.SetEnabled(s.Available);
            for (var i = 0; i < _tabs.Count; i++) { _tabs[i].SetEnabled(s.Available); _tabs[i].EnableInClassList("foc-city-selected", (int)s.Tab == i); }
            if (!s.Available) { _rows.itemsSource = null; return; }
            _areaGrid.style.display = s.Tab == CityInspectionTab.Areas ? DisplayStyle.Flex : DisplayStyle.None;
            _recipe.style.display = s.Tab == CityInspectionTab.Production ? DisplayStyle.Flex : DisplayStyle.None;
            _search.style.display = _shortages.style.display = s.Tab == CityInspectionTab.Stocks ? DisplayStyle.Flex : DisplayStyle.None;
            _detail.style.display = _reason.style.display = s.Tab == CityInspectionTab.Production ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < _areas.Count; i++) { _areas[i].text = s.Areas[i]; _areas[i].EnableInClassList("foc-city-selected", (int)s.Area == i); }
            Choices(_recipe, s.Recipes, s.RecipeId); _search.SetValueWithoutNotify(s.Search); _shortages.SetValueWithoutNotify(s.ShortagesOnly);
            _detail.text = s.ProductionDetail; _reason.text = CityInspectionText.ProductionReason(s.ProductionResult);
            _reason.EnableInClassList("foc-status--success", s.ProductionResult == ProductionInspectionResult.MechanicallyReady);
            var rows = new List<string>();
            switch (s.Tab)
            {
                case CityInspectionTab.Areas:
                    _heading.text = CityInspectionText.Area(s.Area) + " · Bina havuzu"; rows.AddRange(s.Buildings);
                    _note.text = "İç Kale sabit tam doludur. Alan doluluğu, etkin bina ve bina kilidi ayrı kayıtlardır. İnşa emri verilmez."; break;
                case CityInspectionTab.Production:
                    _heading.text = "TEK REÇETE UYGULAMASI · SALT OKUNUR";
                    _note.text = "Bu uygunluk kontrolü üretim emri değildir. Süre, günlük verim, maliyet veya yönetim yetkisi tanımlamaz. Hiçbir mal tüketilmez ya da üretilmez."; break;
                case CityInspectionTab.Stocks:
                    _heading.text = s.Stocks.Count + " mal gösteriliyor"; rows.AddRange(s.Stocks.Select(x => x.Text));
                    if (rows.Count == 0) rows.Add("Filtreye uygun mal / eksik kaydı yok.");
                    _note.text = "Talep mevcut kayıtların toplamıdır; günlük tüketim veya otomatik fiyat etkisi değildir. Eksik = en çok(talep − stok, 0)."; break;
                case CityInspectionTab.Infrastructure:
                    _heading.text = "Görünmez altyapı · Kurulum ve durum"; rows.AddRange(s.Infrastructure);
                    _note.text = s.Officials; break;
            }
            _rows.itemsSource = rows; _rows.style.display = rows.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex; _rows.style.height = Math.Min(rows.Count, 6) * 34;
        }
        private static void Choices(DropdownField field, IReadOnlyList<CityInspectionChoice> choices, string id)
        { field.choices = choices.Select(x => x.Label).ToList(); field.SetValueWithoutNotify(choices.FirstOrDefault(x => x.Id == id)?.Label ?? "—"); field.SetEnabled(choices.Count > 0); }
        private void OnCity(ChangeEvent<string> evt) { if (!_disposed && _snapshot != null) { var c = _snapshot.Cities.ElementAtOrDefault(_city.choices.IndexOf(evt.newValue)); if (c != null) _navigate(c.Id); } }
        private void OnRecipe(ChangeEvent<string> evt) { if (!_disposed && _snapshot != null) { var r = _snapshot.Recipes.ElementAtOrDefault(_recipe.choices.IndexOf(evt.newValue)); if (r != null) { _session.SelectRecipe(r.Id); Render(); } } }
        private void OnSearch(ChangeEvent<string> evt) { if (!_disposed) { _session.FilterStocks(evt.newValue, _shortages.value); Render(); } }
        private void OnShortages(ChangeEvent<bool> evt) { if (!_disposed) { _session.FilterStocks(_search.value, evt.newValue); Render(); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _city.UnregisterValueChangedCallback(OnCity); _recipe.UnregisterValueChangedCallback(OnRecipe); _search.UnregisterValueChangedCallback(OnSearch); _shortages.UnregisterValueChangedCallback(OnShortages);
            foreach (var h in _handlers) h.Item1.clicked -= h.Item2; _handlers.Clear();
            _rows.itemsSource = null; _rows.Rebuild(); _rows.makeItem = null; _rows.bindItem = null; _rows.unbindItem = null;
            _snapshot = null; _root.Clear();
        }
    }
}
