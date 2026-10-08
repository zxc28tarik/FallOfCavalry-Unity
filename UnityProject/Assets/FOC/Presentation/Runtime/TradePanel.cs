#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Application.Economy;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    /// <summary>Retained controls. All authoritative validation and transfer is outside the view.</summary>
    internal sealed class TradePanel : IDisposable
    {
        private readonly VisualElement _root;
        private readonly TradePanelSession _session;
        private readonly IPresentationLocalizer _text;
        private readonly Action<string> _selectMarket;
        private readonly Action<PresentationActionResult> _completed;
        private readonly DropdownField _market = new DropdownField("Pazar") { name = "trade-market" };
        private readonly DropdownField _good = new DropdownField("Mal") { name = "trade-good" };
        private readonly DropdownField _caravan = new DropdownField("Kervan") { name = "trade-caravan" };
        private readonly TextField _quantity = new TextField("Miktar") { name = "trade-quantity", value = "1", maxLength = 20 };
        private readonly Label _stock = new Label { name = "trade-stock" }, _price = new Label { name = "trade-price" },
            _funds = new Label { name = "trade-funds" }, _location = new Label { name = "trade-location" },
            _reason = new Label { name = "trade-reasons" }, _confirmation = new Label { name = "trade-confirmation" },
            _feedback = new Label { name = "trade-feedback" };
        private readonly Button _purchase, _sale, _confirm, _cancel;
        private TradePanelSnapshot? _snapshot;
        private bool _disposed;

        public TradePanel(VisualElement root, TradePanelSession session, IPresentationLocalizer text,
            Action<string> selectMarket, Action<PresentationActionResult> completed)
        {
            _root = root; _session = session; _text = text; _selectMarket = selectMarket; _completed = completed;
            var title = new Label("PAZAR VE KERVAN") { name = "trade-heading" }; title.AddToClassList("foc-card-title"); root.Add(title);
            var selectors = new VisualElement(); selectors.AddToClassList("foc-trade-selectors");
            selectors.Add(_market); selectors.Add(_good); selectors.Add(_quantity); root.Add(selectors); root.Add(_caravan);
            var data = new VisualElement(); data.AddToClassList("foc-trade-data");
            data.Add(_stock); data.Add(_price); data.Add(_funds); data.Add(_location); root.Add(data);
            var buttons = new VisualElement(); buttons.AddToClassList("foc-trade-buttons");
            _purchase = new Button(Purchase) { name = "trade-purchase", text = "Alımı incele" };
            _sale = new Button(Sale) { name = "trade-sale", text = "Satışı incele" };
            buttons.Add(_purchase); buttons.Add(_sale); root.Add(buttons); root.Add(_reason);
            root.Add(_confirmation);
            var confirmRow = new VisualElement(); confirmRow.AddToClassList("foc-trade-buttons");
            _confirm = new Button(Confirm) { name = "trade-confirm", text = "İşlemi onayla" };
            _cancel = new Button(Cancel) { name = "trade-cancel", text = "Vazgeç" };
            confirmRow.Add(_confirm); confirmRow.Add(_cancel); root.Add(confirmRow); root.Add(_feedback);
            var note = new Label("Fiyat: içerikte tanımlı referans değer; ek fiyat düzeltmesi yok. Talep bilgi içindir, otomatik fiyat katsayısı değildir. Kervan işletme parası kişisel servetten ayrıdır.");
            note.AddToClassList("foc-muted"); root.Add(note);
            _market.RegisterValueChangedCallback(OnMarket);
            _good.RegisterValueChangedCallback(OnSelection); _caravan.RegisterValueChangedCallback(OnSelection);
            _quantity.RegisterValueChangedCallback(OnSelection);
        }
        public void Render(string? city = null)
        {
            if (_disposed) return;
            _session.OpenMarket(city); var state = _session.Read(); _snapshot = state;
            Choices(_market, state.Markets, state.MarketId); Choices(_good, state.Goods, state.GoodId); Choices(_caravan, state.Caravans, state.CaravanId);
            _quantity.SetValueWithoutNotify(state.Quantity);
            _stock.text = state.Markets.Count == 0 ? "Bu pazar için kesin bilgi yok." : "Şehir stoku: " + N(state.Stock) + "  ·  Talep: " + N(state.Demand) + "  ·  Kervanda: " + N(state.Cargo);
            _price.text = "Birim fiyat: " + N(state.UnitValue) + "  ·  Toplam: " + N(state.Total);
            _funds.text = state.Markets.Count == 0 ? "" : "Pazar parası: " + N(state.MarketCash) + "  ·  Kervan parası: " + N(state.CaravanCash)
                + "  ·  Yük: " + N(state.UsedWeight) + " / " + N(state.Capacity);
            _location.text = state.Caravans.Count == 0 ? "Kontrol edilebilir / kesin bilgisi olan kervan yok. NPC kervanı oyuncuya devredilmez." : state.Route + "  ·  " + _text.Get(state.StageKey);
            var ready = TradePanelSession.Reason(TradeOrderFailure.None);
            _purchase.SetEnabled(state.PurchaseReason == ready && state.Confirmation == null);
            _sale.SetEnabled(state.SaleReason == ready && state.Confirmation == null);
            _purchase.tooltip = _text.Get(state.PurchaseReason); _sale.tooltip = _text.Get(state.SaleReason);
            _reason.text = "Alım: " + _text.Get(state.PurchaseReason) + "\nSatış: " + _text.Get(state.SaleReason);
            var quote = state.Confirmation;
            _confirmation.text = quote == null ? "" : (quote.Side == TradeOrderSide.Purchase ? "ALIM" : "SATIŞ") + " ONAYI · "
                + state.Goods.Single(x => x.Id == quote.Good.Value).Label + " × " + N(quote.Quantity) + " · Birim " + N(quote.UnitValue)
                + " · Toplam " + N(quote.Total) + "\n" + _market.value + " · " + _caravan.value;
            _confirm.style.display = _cancel.style.display = quote == null ? DisplayStyle.None : DisplayStyle.Flex;
            _confirm.SetEnabled(quote != null); _cancel.SetEnabled(quote != null);
            _feedback.text = _text.Get(state.Feedback);
        }
        private static string N(long? value) => value?.ToString("N0", CultureInfo.GetCultureInfo("tr-TR")) ?? "—";
        private static void Choices(DropdownField field, IReadOnlyList<TradePanelChoice> choices, string selected)
        {
            field.choices = choices.Select(x => x.Label).ToList();
            field.SetValueWithoutNotify(choices.FirstOrDefault(x => x.Id == selected)?.Label ?? "—");
            field.SetEnabled(choices.Count > 0);
        }
        private static string Id(DropdownField field, IReadOnlyList<TradePanelChoice> choices) =>
            choices.ElementAtOrDefault(field.choices.IndexOf(field.value))?.Id ?? "";
        private void OnMarket(ChangeEvent<string> evt)
        {
            if (_disposed || _snapshot == null) return;
            var id = Id(_market, _snapshot.Markets); if (id.Length > 0) _selectMarket(id);
        }
        private void OnSelection(ChangeEvent<string> evt)
        {
            if (_disposed || _snapshot == null) return;
            _session.Select(_snapshot.MarketId, Id(_good, _snapshot.Goods), Id(_caravan, _snapshot.Caravans), _quantity.value); Render();
        }
        private void Purchase() { if (!_disposed) { _session.Prepare(TradeOrderSide.Purchase); Render(); } }
        private void Sale() { if (!_disposed) { _session.Prepare(TradeOrderSide.Sale); Render(); } }
        private void Confirm() { if (!_disposed) { var result = _session.Confirm(); _completed(result); Render(); } }
        private void Cancel() { if (!_disposed) { _session.Cancel(); Render(); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _session.Cancel();
            _market.UnregisterValueChangedCallback(OnMarket); _good.UnregisterValueChangedCallback(OnSelection);
            _caravan.UnregisterValueChangedCallback(OnSelection); _quantity.UnregisterValueChangedCallback(OnSelection);
            _purchase.clicked -= Purchase; _sale.clicked -= Sale; _confirm.clicked -= Confirm; _cancel.clicked -= Cancel;
            _snapshot = null; _root.Clear();
        }
    }
}
