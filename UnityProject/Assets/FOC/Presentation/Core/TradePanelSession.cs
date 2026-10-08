#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Application.Economy;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;

namespace FOC.Presentation.Core
{
    public sealed class TradePanelChoice
    {
        public TradePanelChoice(string id, string label) { Id = id; Label = label; }
        public string Id { get; }
        public string Label { get; }
    }
    public sealed class TradePanelSnapshot
    {
        public string MarketId { get; internal set; } = "";
        public string GoodId { get; internal set; } = "";
        public string CaravanId { get; internal set; } = "";
        public string Quantity { get; internal set; } = "1";
        public IReadOnlyList<TradePanelChoice> Markets { get; internal set; } = Array.Empty<TradePanelChoice>();
        public IReadOnlyList<TradePanelChoice> Goods { get; internal set; } = Array.Empty<TradePanelChoice>();
        public IReadOnlyList<TradePanelChoice> Caravans { get; internal set; } = Array.Empty<TradePanelChoice>();
        public long Stock { get; internal set; }
        public long Demand { get; internal set; }
        public long MarketCash { get; internal set; }
        public long? UnitValue { get; internal set; }
        public long? Total { get; internal set; }
        public long? Cargo { get; internal set; }
        public long? CaravanCash { get; internal set; }
        public long? UsedWeight { get; internal set; }
        public long? Capacity { get; internal set; }
        public string Route { get; internal set; } = "";
        public string StageKey { get; internal set; } = "";
        public string PurchaseReason { get; internal set; } = "presentation.trade.reason.unauthorized";
        public string SaleReason { get; internal set; } = "presentation.trade.reason.unauthorized";
        public TradeOrderPreview? Confirmation { get; internal set; }
        public string Feedback { get; internal set; } = "";
    }

    /// <summary>Ephemeral UI selection. Knowledge does not grant a command or reveal another caravan.</summary>
    public sealed class TradePanelSession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private readonly TradeOrderSession? _orders;
        private string _market = "", _good = "", _caravan = "", _quantity = "1", _feedback = "";
        private TradeOrderPreview? _confirmation;
        private bool _disposed;
        public TradePanelSession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        {
            _campaign = campaign; _viewer = viewer;
            if (viewer.ControlledIdentity.Kind == PresentationEntityKind.Character)
                _orders = new TradeOrderSession(campaign, CharacterId.Create(viewer.ControlledIdentity.Id));
        }
        public static string Reason(TradeOrderFailure failure) => "presentation.trade.reason." + failure.ToString().ToLowerInvariant();
        private bool Known(PresentationEntityKind kind, string id) => _viewer.CanReadExact(new PresentationEntityRef(kind, id));
        public void Select(string market, string good, string caravan, string quantity)
        {
            if (_disposed) return;
            if (_market == market && _good == good && _caravan == caravan && _quantity == quantity) return;
            Cancel(); _market = market; _good = good; _caravan = caravan; _quantity = quantity; _feedback = "";
        }
        public void OpenMarket(string? city)
        {
            if (city != null && city != _market) Select(city, _good, _caravan, _quantity);
        }
        public TradePanelSnapshot Read()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TradePanelSession));
            var markets = _campaign.Economy.OrderedMarkets.Where(x => Known(PresentationEntityKind.City, x.CityId.Value))
                .Select(x => new TradePanelChoice(x.CityId.Value, _campaign.Cities.GetRequired(x.CityId).Definition.Name)).ToArray();
            var goods = _campaign.Economy.Goods.OrderedGoods.Select(x => new TradePanelChoice(x.Id.Value, x.Name)).ToArray();
            var caravans = _campaign.Economy.Caravans.OrderedCaravans.Where(x => Known(PresentationEntityKind.Caravan, x.Id.Value))
                .Select(x => new TradePanelChoice(x.Id.Value, _campaign.Cities.GetRequired(x.OriginCityId).Definition.Name + " → "
                    + _campaign.Cities.GetRequired(x.DestinationCityId).Definition.Name + " · " + x.Id.Value)).ToArray();
            // Unknown or stale selections never fall back to an unseen entity.
            if (!markets.Any(x => x.Id == _market)) { Cancel(); _market = markets.FirstOrDefault()?.Id ?? ""; }
            if (!goods.Any(x => x.Id == _good)) { Cancel(); _good = goods.FirstOrDefault()?.Id ?? ""; }
            if (!caravans.Any(x => x.Id == _caravan)) { Cancel(); _caravan = caravans.FirstOrDefault()?.Id ?? ""; }
            var result = new TradePanelSnapshot { Markets = markets, Goods = goods, Caravans = caravans,
                MarketId = _market, GoodId = _good, CaravanId = _caravan, Quantity = _quantity, Feedback = _feedback, Confirmation = _confirmation };
            if (_market.Length == 0 || _good.Length == 0) return result;
            var good = _campaign.Economy.Goods.GetRequired(TradeGoodId.Create(_good));
            var market = _campaign.Economy.GetRequiredMarket(CityId.Create(_market));
            result.Stock = market.Stock.QuantityOf(good.Id); result.Demand = market.Demand.TotalFor(good.Id);
            result.MarketCash = market.CashBalance.Value; result.UnitValue = good.ReferenceUnitValue;
            var validQuantity = long.TryParse(_quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) && quantity > 0;
            if (validQuantity && result.UnitValue.HasValue)
            { try { result.Total = checked(quantity * result.UnitValue.Value); } catch (OverflowException) { } }
            if (_caravan.Length == 0 || _orders == null) return result;
            var caravan = _campaign.Economy.Caravans.GetRequired(CaravanId.Create(_caravan));
            result.Cargo = caravan.Cargo.QuantityOf(good.Id); result.CaravanCash = caravan.CashBalance.Value;
            result.UsedWeight = caravan.Cargo.UsedWeight(_campaign.Economy.Goods); result.Capacity = caravan.WeightCapacity;
            result.Route = caravans.Single(x => x.Id == _caravan).Label;
            result.StageKey = "presentation.trade.stage." + caravan.LocationStage.ToString().ToLowerInvariant();
            result.PurchaseReason = Reason(_orders.Inspect(caravan.Id, market.CityId, good.Id, validQuantity ? quantity : 0, TradeOrderSide.Purchase).Failure);
            result.SaleReason = Reason(_orders.Inspect(caravan.Id, market.CityId, good.Id, validQuantity ? quantity : 0, TradeOrderSide.Sale).Failure);
            return result;
        }
        public void Prepare(TradeOrderSide side)
        {
            if (_disposed) return;
            Cancel(); var view = Read();
            if (_orders == null || view.CaravanId.Length == 0 || view.MarketId.Length == 0 || view.GoodId.Length == 0)
            { _feedback = Reason(TradeOrderFailure.Unauthorized); return; }
            long.TryParse(view.Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity);
            var preview = _orders.Prepare(CaravanId.Create(view.CaravanId), CityId.Create(view.MarketId), TradeGoodId.Create(view.GoodId), quantity, side);
            _feedback = Reason(preview.Failure);
            if (preview.CanExecute) _confirmation = preview;
        }
        public PresentationActionResult Confirm()
        {
            if (_disposed || _orders == null || _confirmation == null)
                return PresentationActionResult.Rejected(Reason(TradeOrderFailure.ConfirmationUnavailable));
            var preview = _confirmation; _confirmation = null;
            var failure = _orders.Confirm(preview);
            _feedback = failure == TradeOrderFailure.None ? "presentation.trade.completed" : Reason(failure);
            return failure == TradeOrderFailure.None ? PresentationActionResult.Success(_feedback) : PresentationActionResult.Rejected(_feedback);
        }
        public void Cancel() { _confirmation = null; _orders?.Cancel(); }
        public void Dispose() { Cancel(); _disposed = true; }
    }
}
