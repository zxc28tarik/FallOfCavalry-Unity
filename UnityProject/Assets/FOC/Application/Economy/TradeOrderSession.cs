#nullable enable
using System;
using System.Globalization;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;

namespace FOC.Application.Economy
{
    public enum TradeOrderSide { Purchase, Sale }
    public enum TradeOrderFailure
    {
        None, Unauthorized, Unavailable, InvalidQuantity, PriceUnavailable, WrongLocation,
        InsufficientStock, InsufficientCargo, Capacity, CaravanFunds, MarketFunds, Overflow,
        StaleQuote, ConfirmationUnavailable
    }

    /// <summary>Ephemeral confirmation, never SaveData. Only its issuing session can consume it.</summary>
    public sealed class TradeOrderPreview
    {
        internal TradeOrderPreview(CaravanId caravan, CityId city, TradeGoodId good, long quantity,
            TradeOrderSide side, TradeOrderFailure failure, long unitValue, long total, string snapshot)
        { Caravan = caravan; City = city; Good = good; Quantity = quantity; Side = side;
          Failure = failure; UnitValue = unitValue; Total = total; Snapshot = snapshot; }
        public CaravanId Caravan { get; }
        public CityId City { get; }
        public TradeGoodId Good { get; }
        public long Quantity { get; }
        public TradeOrderSide Side { get; }
        public TradeOrderFailure Failure { get; }
        public bool CanExecute => Failure == TradeOrderFailure.None;
        public long UnitValue { get; }
        public long Total { get; }
        internal string Snapshot { get; }
    }

    /// <summary>
    /// Binds the existing atomic economy service to a real manager and one-use confirmation.
    /// Prices use authored reference values, with explicitly no additional price adjustments.
    /// This does not grant ownership, transfer funds, move caravans or advance campaign time.
    /// </summary>
    public sealed class TradeOrderSession
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly CharacterId _actor;
        private TradeOrderPreview? _pending;
        public TradeOrderSession(CampaignRuntimeState campaign, CharacterId actor)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _actor = actor; }

        public TradeOrderPreview Inspect(CaravanId caravanId, CityId cityId, TradeGoodId goodId,
            long quantity, TradeOrderSide side)
        {
            long unit = 0, total = 0; var snapshot = string.Empty;
            TradeOrderPreview Result(TradeOrderFailure failure) =>
                new TradeOrderPreview(caravanId, cityId, goodId, quantity, side, failure, unit, total, snapshot);
            var caravan = _campaign.Economy.Caravans.OrderedCaravans.FirstOrDefault(x => x.Id.Equals(caravanId));
            // Do not expose stock/funds/location through failure reasons to an unauthorized actor.
            if (caravan == null || !caravan.ManagerCharacterId.Equals(_actor)) return Result(TradeOrderFailure.Unauthorized);
            var actor = _campaign.Characters.OrderedCharacters.FirstOrDefault(x => x.Id.Equals(_actor));
            if (actor == null || actor.IsDead || actor.Captivity != null || caravan.Lifecycle != CaravanLifecycle.Active) return Result(TradeOrderFailure.Unavailable);
            if (quantity <= 0) return Result(TradeOrderFailure.InvalidQuantity);
            if (side != TradeOrderSide.Purchase && side != TradeOrderSide.Sale) return Result(TradeOrderFailure.Unavailable);
            var market = _campaign.Economy.OrderedMarkets.FirstOrDefault(x => x.CityId.Equals(cityId));
            var good = _campaign.Economy.Goods.OrderedGoods.FirstOrDefault(x => x.Id.Equals(goodId));
            if (market == null || good == null) return Result(TradeOrderFailure.Unavailable);
            if (!good.ReferenceUnitValue.HasValue) return Result(TradeOrderFailure.PriceUnavailable);
            try
            {
                var quote = TradePriceRules.FormQuote(good, Array.Empty<PriceAdjustment>());
                unit = quote.FinalUnitValue; total = checked(quantity * unit);
                var purchase = side == TradeOrderSide.Purchase;
                if (purchase ? !caravan.OriginCityId.Equals(cityId) || caravan.LocationStage != CaravanLocationStage.AtOrigin
                    : !caravan.DestinationCityId.Equals(cityId) || caravan.LocationStage != CaravanLocationStage.AtDestination)
                    return Result(TradeOrderFailure.WrongLocation);
                if (purchase)
                {
                    if (market.Stock.QuantityOf(goodId) < quantity) return Result(TradeOrderFailure.InsufficientStock);
                    if (!caravan.Cargo.CanAdd(goodId, quantity, caravan.WeightCapacity, _campaign.Economy.Goods)) return Result(TradeOrderFailure.Capacity);
                    if (caravan.CashBalance.Value < total) return Result(TradeOrderFailure.CaravanFunds);
                    if (!market.CanCredit(total) || !caravan.Accounting.CanRecordPurchase(total)) return Result(TradeOrderFailure.Overflow);
                }
                else
                {
                    if (caravan.Cargo.QuantityOf(goodId) < quantity) return Result(TradeOrderFailure.InsufficientCargo);
                    if (market.CashBalance.Value < total) return Result(TradeOrderFailure.MarketFunds);
                    if (!market.Stock.CanAdd(goodId, quantity) || !caravan.CanCredit(total) || !caravan.Accounting.CanRecordSale(total)) return Result(TradeOrderFailure.Overflow);
                }
                snapshot = string.Join("|", new[] { unit, market.CashBalance.Value, caravan.CashBalance.Value,
                    market.Stock.QuantityOf(goodId), caravan.Cargo.QuantityOf(goodId),
                    caravan.Cargo.UsedWeight(_campaign.Economy.Goods), caravan.Accounting.PurchaseCost.Value,
                    caravan.Accounting.SaleRevenue.Value }.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                return Result(TradeOrderFailure.None);
            }
            catch (OverflowException) { return Result(TradeOrderFailure.Overflow); }
        }

        public TradeOrderPreview Prepare(CaravanId caravan, CityId city, TradeGoodId good, long quantity, TradeOrderSide side)
        {
            _pending = null;
            var preview = Inspect(caravan, city, good, quantity, side);
            if (preview.CanExecute) _pending = preview;
            return preview;
        }

        public TradeOrderFailure Confirm(TradeOrderPreview preview)
        {
            if (preview == null || !ReferenceEquals(preview, _pending)) return TradeOrderFailure.ConfirmationUnavailable;
            _pending = null; // Consume before validation/execution: duplicate activation can never execute twice.
            var current = Inspect(preview.Caravan, preview.City, preview.Good, preview.Quantity, preview.Side);
            if (!current.CanExecute) return current.Failure;
            if (!StringComparer.Ordinal.Equals(current.Snapshot, preview.Snapshot)) return TradeOrderFailure.StaleQuote;
            var quote = TradePriceRules.FormQuote(_campaign.Economy.Goods.GetRequired(preview.Good), Array.Empty<PriceAdjustment>());
            var service = new TradeTransactionService(_campaign.Cities, _campaign.Economy);
            if (preview.Side == TradeOrderSide.Purchase) service.PurchaseAndLoad(preview.Caravan, preview.City, preview.Good, preview.Quantity, quote);
            else service.SellAndUnload(preview.Caravan, preview.City, preview.Good, preview.Quantity, quote);
            return TradeOrderFailure.None;
        }
        public void Cancel() => _pending = null;
    }
}
