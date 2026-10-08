#nullable enable
using System;
using System.IO;
using System.Linq;
using FOC.Application.Economy;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Geography;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class PlayableTradeTests
    {
        private static readonly CaravanId Caravan = CaravanId.Create("caravan-bursa-istanbul");
        private static readonly CityId Bursa = CityId.Create("city-bursa"), Istanbul = CityId.Create("city-istanbul");
        private static readonly TradeGoodId Silk = TradeGoodId.Create("silk-cloth");
        private static readonly CharacterId Manager = CharacterId.Create("mehmed-celebi-tacir");
        private static TradeOrderSession Orders(CampaignRuntimeState c, string? actor = null) => new TradeOrderSession(c, actor == null ? Manager : CharacterId.Create(actor));
        private static TradeOrderPreview Prepare(TradeOrderSession session, long quantity = 1, TradeOrderSide side = TradeOrderSide.Purchase) => session.Prepare(Caravan, side == TradeOrderSide.Purchase ? Bursa : Istanbul, Silk, quantity, side);

        [Test]
        public void PreviewAndCancelDoNotChangeCampaignOrGrantHasanNpcAuthority()
        {
            var c = Campaign(); var before = Fingerprint(c); var orders = Orders(c);
            var quote = Prepare(orders); Assert.That(quote.CanExecute, Is.True);
            Assert.That(quote.UnitValue, Is.EqualTo(c.Economy.Goods.GetRequired(Silk).ReferenceUnitValue));
            orders.Cancel(); Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.ConfirmationUnavailable));
            Assert.That(Prepare(Orders(c, "hasan-aga")).Failure, Is.EqualTo(TradeOrderFailure.Unauthorized));
            Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void PurchaseConservesMoneyAndGoodsAndConfirmationIsSingleUse()
        {
            var c = Campaign(); var orders = Orders(c); var caravan = c.Economy.Caravans.GetRequired(Caravan); var market = c.Economy.GetRequiredMarket(Bursa);
            var money = caravan.CashBalance.Value + market.CashBalance.Value; var goods = caravan.Cargo.QuantityOf(Silk) + market.Stock.QuantityOf(Silk);
            var cash = caravan.CashBalance.Value; var cargo = caravan.Cargo.QuantityOf(Silk);
            var quote = Prepare(orders, 2); Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.None));
            Assert.That(caravan.CashBalance.Value, Is.EqualTo(cash - quote.Total));
            Assert.That(caravan.Cargo.QuantityOf(Silk), Is.EqualTo(cargo + 2));
            Assert.That(caravan.Accounting.PurchaseCost.Value, Is.EqualTo(quote.Total));
            Assert.That(caravan.CashBalance.Value + market.CashBalance.Value, Is.EqualTo(money));
            Assert.That(caravan.Cargo.QuantityOf(Silk) + market.Stock.QuantityOf(Silk), Is.EqualTo(goods));
            var after = Fingerprint(c); Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.ConfirmationUnavailable));
            Assert.That(Fingerprint(c), Is.EqualTo(after));
        }

        [Test]
        public void SaleAtActualTravelDestinationConservesResourcesAndSurvivesSaveReload()
        {
            var c = Campaign(); var travel = new TravelCommandService(c);
            var journey = travel.Start(JourneyId.Create("y2-test-travel"), TravelActorRef.Caravan(Caravan), WorldLocationId.Create("bursa"), WorldLocationId.Create("istanbul"));
            travel.Advance(new WorldDuration(travel.TotalJourneyTicks(journey)));
            var restored = CampaignSaveMapper.ToRuntimeState(new CampaignSaveTextSerializer().Deserialize(Fingerprint(c)).Data!);
            foreach (var state in new[] { c, restored })
            {
                var caravan = state.Economy.Caravans.GetRequired(Caravan); var market = state.Economy.GetRequiredMarket(Istanbul);
                var cash = caravan.CashBalance.Value + market.CashBalance.Value; var goods = caravan.Cargo.QuantityOf(Silk) + market.Stock.QuantityOf(Silk);
                var orders = Orders(state); var quote = Prepare(orders, 2, TradeOrderSide.Sale);
                Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.None));
                Assert.That(caravan.Accounting.SaleRevenue.Value, Is.EqualTo(quote.Total));
                Assert.That(caravan.CashBalance.Value + market.CashBalance.Value, Is.EqualTo(cash));
                Assert.That(caravan.Cargo.QuantityOf(Silk) + market.Stock.QuantityOf(Silk), Is.EqualTo(goods));
            }
            Assert.That(Fingerprint(restored), Is.EqualTo(Fingerprint(c)));
            var data = CampaignSaveMapper.ToSaveData(c); Assert.That(data.SaveVersion, Is.EqualTo(14));
            Assert.That(new CampaignSaveValidator().Validate(data).IsValid, Is.True);
        }

        [TestCase(0, TradeOrderFailure.InvalidQuantity)]
        [TestCase(-1, TradeOrderFailure.InvalidQuantity)]
        [TestCase(long.MaxValue, TradeOrderFailure.Overflow)]
        [TestCase(10000, TradeOrderFailure.InsufficientStock)]
        public void InvalidAmountsAreAtomic(long amount, TradeOrderFailure expected)
        {
            var c = Campaign(); var before = Fingerprint(c); var orders = Orders(c);
            var quote = Prepare(orders, amount); Assert.That(quote.Failure, Is.EqualTo(expected));
            Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.ConfirmationUnavailable)); Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void StaleQuoteIsRejectedEvenIfNewBalancesCouldStillAffordIt()
        {
            var c = Campaign(); var orders = Orders(c); var quote = Prepare(orders);
            c.Economy.GetRequiredMarket(Bursa).Stock.Add(Silk, 1); var before = Fingerprint(c);
            Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.StaleQuote)); Assert.That(Fingerprint(c), Is.EqualTo(before));
            Assert.That(orders.Confirm(Prepare(orders)), Is.EqualTo(TradeOrderFailure.None));
        }

        [Test]
        public void ConfirmationCannotBeUsedAcrossSessionsOrAfterReprepare()
        {
            var c = Campaign(); var a = Orders(c); var b = Orders(c); var quote = Prepare(a); var before = Fingerprint(c);
            Assert.That(b.Confirm(quote), Is.EqualTo(TradeOrderFailure.ConfirmationUnavailable));
            Prepare(a, 2); Assert.That(a.Confirm(quote), Is.EqualTo(TradeOrderFailure.ConfirmationUnavailable));
            Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void PurchaseCannotUseDestinationAndSaleCannotUseOriginOrTravel()
        {
            var c = Campaign(); var orders = Orders(c); var before = Fingerprint(c);
            Assert.That(orders.Inspect(Caravan, Istanbul, Silk, 1, TradeOrderSide.Purchase).Failure, Is.EqualTo(TradeOrderFailure.WrongLocation));
            Assert.That(orders.Inspect(Caravan, Bursa, Silk, 1, TradeOrderSide.Sale).Failure, Is.EqualTo(TradeOrderFailure.WrongLocation));
            Assert.That(Fingerprint(c), Is.EqualTo(before));
            new TravelCommandService(c).Start(JourneyId.Create("transit"), TravelActorRef.Caravan(Caravan), WorldLocationId.Create("bursa"), WorldLocationId.Create("istanbul"));
            before = Fingerprint(c); Assert.That(Prepare(orders).Failure, Is.EqualTo(TradeOrderFailure.WrongLocation)); Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void DeadOrCaptiveManagerCannotConfirmAnOldQuote()
        {
            var c = Campaign(); var orders = Orders(c); var quote = Prepare(orders);
            c.Characters.GetRequired(Manager).Capture(new CaptivityState(CharacterId.Create("hasan-aga"), CaptivitySite.InCity(Bursa)), c.Clock.Now);
            var before = Fingerprint(c); Assert.That(orders.Confirm(quote), Is.EqualTo(TradeOrderFailure.Unavailable)); Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void FundsAndCapacityFailuresExplainWhyWithoutMutation()
        {
            var c = Campaign(); var caravan = c.Economy.Caravans.GetRequired(Caravan); var orders = Orders(c);
            caravan.Debit(caravan.CashBalance.Value); var before = Fingerprint(c);
            Assert.That(Prepare(orders).Failure, Is.EqualTo(TradeOrderFailure.CaravanFunds)); Assert.That(Fingerprint(c), Is.EqualTo(before));
            caravan.Credit(300); c.Economy.GetRequiredMarket(Bursa).Stock.Add(Silk, 500);
            before = Fingerprint(c); Assert.That(Prepare(orders, 201).Failure, Is.EqualTo(TradeOrderFailure.Capacity)); Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void SaleRejectsMissingCargoAndMarketFundsWithoutMutation()
        {
            var c = Campaign(); var caravan = c.Economy.Caravans.GetRequired(Caravan); caravan.MarkInTransit(); caravan.MarkAtDestination();
            var orders = Orders(c); var before = Fingerprint(c);
            Assert.That(Prepare(orders, 100, TradeOrderSide.Sale).Failure, Is.EqualTo(TradeOrderFailure.InsufficientCargo)); Assert.That(Fingerprint(c), Is.EqualTo(before));
            var market = c.Economy.GetRequiredMarket(Istanbul); market.Debit(market.CashBalance.Value); before = Fingerprint(c);
            Assert.That(Prepare(orders, 1, TradeOrderSide.Sale).Failure, Is.EqualTo(TradeOrderFailure.MarketFunds)); Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void ExactCityKnowledgeDoesNotDiscloseForeignCaravanCargoOrGrantCommands()
        {
            var c = Campaign(); var viewer = Viewer("hasan-aga", false); using var session = new TradePanelSession(c, viewer);
            var before = Fingerprint(c); var view = session.Read();
            Assert.That(view.Caravans, Is.Empty); Assert.That(view.CaravanCash, Is.Null); Assert.That(view.Cargo, Is.Null);
            session.Select("city-bursa", "silk-cloth", Caravan.Value, "1"); session.Prepare(TradeOrderSide.Purchase);
            Assert.That(session.Confirm().Succeeded, Is.False);
            var screen = new CampaignPresentationQueries(c).BuildTrade(viewer, Bursa).State;
            Assert.That(screen.Details.Single(x => x.HeadingKey == "presentation.trade.caravans").Fields, Is.Empty);
            Assert.That(Fingerprint(c), Is.EqualTo(before));
            using var informed = new TradePanelSession(c, Viewer("hasan-aga", true));
            Assert.That(informed.Read().CaravanCash, Is.Not.Null);
            informed.Prepare(TradeOrderSide.Purchase); Assert.That(informed.Confirm().Succeeded, Is.False);
        }

        [TestCase("")][TestCase("0")][TestCase("-1")][TestCase("1.5")][TestCase("1e3")][TestCase("999999999999999999999")]
        public void QuantityFieldRejectsMalformedInput(string value)
        {
            var c = Campaign(); using var session = new TradePanelSession(c, Viewer(Manager.Value, true));
            var before = Fingerprint(c); session.Select(Bursa.Value, Silk.Value, Caravan.Value, value);
            Assert.That(session.Read().PurchaseReason, Is.EqualTo(TradePanelSession.Reason(TradeOrderFailure.InvalidQuantity)));
            session.Prepare(TradeOrderSide.Purchase); Assert.That(session.Read().Confirmation, Is.Null); Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        [Test]
        public void ChangedSelectionNavigationAndDisposedSessionInvalidateConfirmation()
        {
            var c = Campaign(); using var session = new TradePanelSession(c, Viewer(Manager.Value, true));
            session.Select(Bursa.Value, Silk.Value, Caravan.Value, "1"); session.Prepare(TradeOrderSide.Purchase); Assert.That(session.Read().Confirmation, Is.Not.Null);
            var before = Fingerprint(c); session.Select(Bursa.Value, Silk.Value, Caravan.Value, "2"); Assert.That(session.Confirm().Succeeded, Is.False);
            session.Prepare(TradeOrderSide.Purchase); session.OpenMarket(Istanbul.Value); Assert.That(session.Confirm().Succeeded, Is.False);
            session.OpenMarket(Bursa.Value); session.Prepare(TradeOrderSide.Purchase); session.Dispose(); Assert.That(session.Confirm().Succeeded, Is.False);
            Assert.That(Fingerprint(c), Is.EqualTo(before));
        }

        public static PresentationViewerContext Viewer(string actor, bool caravan) => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"),
            new PresentationEntityRef(PresentationEntityKind.Character, actor), new[] { new PresentationEntityRef(PresentationEntityKind.City, Bursa.Value),
                new PresentationEntityRef(PresentationEntityKind.City, Istanbul.Value) }.Concat(caravan ? new[] { new PresentationEntityRef(PresentationEntityKind.Caravan, Caravan.Value) } : Array.Empty<PresentationEntityRef>()));
        private static string Fingerprint(CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
        public static CampaignRuntimeState Campaign()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FallOfCavalry.sln"))) directory = directory.Parent;
            var root = Path.Combine(directory!.FullName, "UnityProject/Assets/FOC/Content/Resources/FOC");
            return VerticalSliceCampaignFactory.Create(File.ReadAllText(Path.Combine(root, "Geography/vertical-slice-locations.txt")),
                File.ReadAllText(Path.Combine(root, "Geography/vertical-slice-routes.txt")), File.ReadAllText(Path.Combine(root, "HistoricalSlice/historical-slice-content.txt")));
        }
    }
}
