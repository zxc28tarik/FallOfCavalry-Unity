#nullable enable
using System;
using System.Linq;
using FOC.Application.Military;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Military;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class PlayableArmyTests
    {
        public const string ArmyKey = "army-hasan-retinue", Source = "recruitment-hasan-retinue", Troop = "troop-cebeli";
        private static ArmyId Army => ArmyId.Create(ArmyKey);
        private static CharacterId Hasan => CharacterId.Create("hasan-aga");
        private static CityId Istanbul => CityId.Create("city-istanbul");
        private static TradeGoodId Grain => TradeGoodId.Create("grain");
        private static CampaignRuntimeState Campaign() => PlayableTradeTests.Campaign();
        private static ArmyOrderSession Orders(CampaignRuntimeState c) => new ArmyOrderSession(c, Hasan);
        private static ArmyOrderPreview Recruit(ArmyOrderSession s, long n = 2) => s.Prepare(Army, ArmyOrderKind.Recruit, Source, Troop, n);
        private static string State(CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
        public static PresentationViewerContext Viewer(bool exact = true, string actor = "hasan-aga") => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"),
            new PresentationEntityRef(PresentationEntityKind.Character, actor), exact ? new[] { new PresentationEntityRef(PresentationEntityKind.Army, ArmyKey),
                new PresentationEntityRef(PresentationEntityKind.City, "city-istanbul"), new PresentationEntityRef(PresentationEntityKind.City, "city-bursa") } : Array.Empty<PresentationEntityRef>());

        [Test]
        public void RecruitmentConservesFinitePersonnelAndExistingIdentitiesWithoutManufacturingSoldiersOrEquipment()
        {
            var c = Campaign(); var a = c.Military.Armies.GetRequired(Army); var s = Orders(c);
            var soldiers = c.Soldiers.Soldiers.OrderedSoldiers.ToArray(); var equipment = c.Soldiers.Equipment.OrderedEquipment.ToArray();
            var units = a.OrderedUnits.ToArray(); var commander = a.Commander; var time = c.Clock.Now;
            var p = Recruit(s); Assert.That(p.CanExecute, Is.True); Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.None));
            Assert.That(a.Headcount, Is.EqualTo(14)); Assert.That(c.Military.RecruitmentSources.GetRequired(RecruitmentSourceId.Create(Source)).AvailableHeadcount, Is.EqualTo(18));
            Assert.That(a.Commander, Is.SameAs(commander)); Assert.That(a.OrderedUnits, Does.Contain(units[0]));
            Assert.That(c.Soldiers.Soldiers.OrderedSoldiers, Is.EqualTo(soldiers)); Assert.That(c.Soldiers.Equipment.OrderedEquipment, Is.EqualTo(equipment));
            Assert.That(c.Clock.Now, Is.EqualTo(time)); var after = State(c);
            Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.ConfirmationUnavailable)); Assert.That(State(c), Is.EqualTo(after));
            Assert.That(new CampaignSaveValidator().Validate(CampaignSaveMapper.ToSaveData(c)).IsValid, Is.True);
        }
        [Test]
        public void PreviewCancelAndCrossSessionConfirmationAreReadOnly()
        {
            var c = Campaign(); var before = State(c); var s = Orders(c); var p = Recruit(s);
            Assert.That(Orders(c).Confirm(p), Is.EqualTo(ArmyOrderFailure.ConfirmationUnavailable)); s.Cancel();
            Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.ConfirmationUnavailable)); Assert.That(State(c), Is.EqualTo(before));
        }
        [TestCase(0, ArmyOrderFailure.InvalidQuantity)][TestCase(-1, ArmyOrderFailure.InvalidQuantity)]
        [TestCase(21, ArmyOrderFailure.InsufficientSource)][TestCase(long.MaxValue, ArmyOrderFailure.InsufficientSource)]
        public void RecruitmentRejectsInvalidAndExhaustedQuantities(long count, ArmyOrderFailure expected)
        { var c = Campaign(); var before = State(c); var s = Orders(c); Assert.That(Recruit(s, count).Failure, Is.EqualTo(expected)); Assert.That(State(c), Is.EqualTo(before)); }
        [Test]
        public void UnknownTroopCannotBeInventedByThePlayer()
        { var c = Campaign(); var before = State(c); Assert.That(Orders(c).Prepare(Army, ArmyOrderKind.Recruit, Source, "invented", 1).Failure, Is.EqualTo(ArmyOrderFailure.Unavailable)); Assert.That(State(c), Is.EqualTo(before)); }
        [TestCase("time", ArmyOrderFailure.StalePreview)][TestCase("source", ArmyOrderFailure.StalePreview)]
        [TestCase("away", ArmyOrderFailure.WrongLocation)][TestCase("assignment", ArmyOrderFailure.Unauthorized)]
        [TestCase("captive", ArmyOrderFailure.Unavailable)][TestCase("disband", ArmyOrderFailure.Unavailable)]
        [TestCase("inactive", ArmyOrderFailure.Unavailable)]
        public void ConfirmationRechecksAllLivePreconditions(string change, ArmyOrderFailure expected)
        {
            var c = Campaign(); var s = Orders(c); var p = Recruit(s);
            switch (change)
            {
                case "time": c.Clock.Advance(new WorldDuration(1)); break;
                case "source": var other = Orders(c); Assert.That(other.Confirm(Recruit(other, 1)), Is.EqualTo(ArmyOrderFailure.None)); break;
                case "away": c.Characters.GetRequired(Hasan).MoveTo(CharacterLocation.InCity(CityId.Create("city-bursa")), c.Clock.Now); break;
                case "assignment": c.Organizations.OrderedOrganizations.SelectMany(x => x.OrderedAssignments).Single(x => x.Id.Value == "assignment-hasan-army-command").Cancel(); break;
                case "captive": c.Characters.GetRequired(Hasan).Capture(new CaptivityState(CharacterId.Create("mehmed-celebi-tacir"), CaptivitySite.InCity(Istanbul)), c.Clock.Now); break;
                case "disband": c.Military.Armies.GetRequired(Army).SetLifecycle(ArmyLifecycle.Disbanding); break;
                case "inactive": c.Military.RecruitmentSources.GetRequired(RecruitmentSourceId.Create(Source)).Deactivate(); break;
            }
            var before = State(c); Assert.That(s.Confirm(p), Is.EqualTo(expected)); Assert.That(State(c), Is.EqualTo(before));
        }
        [Test]
        public void SupplyMovesActualGoodsOnceAndRejectsWrongCityAndOverflow()
        {
            var c = Campaign(); var s = Orders(c); var army = c.Military.Armies.GetRequired(Army); var stock = c.Economy.GetRequiredMarket(Istanbul).Stock;
            var total = stock.QuantityOf(Grain) + army.Supply.QuantityOf(Grain);
            var p = s.Prepare(Army, ArmyOrderKind.CitySupply, Istanbul.Value, Grain.Value, 3);
            Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.None)); Assert.That(army.Supply.QuantityOf(Grain), Is.EqualTo(19));
            Assert.That(stock.QuantityOf(Grain) + army.Supply.QuantityOf(Grain), Is.EqualTo(total));
            Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.ConfirmationUnavailable));
            Assert.That(s.Prepare(Army, ArmyOrderKind.CitySupply, "city-bursa", Grain.Value, 1).Failure, Is.EqualTo(ArmyOrderFailure.WrongLocation));
            var extra = c.Economy.Goods.OrderedGoods.First(x => army.Supply.QuantityOf(x.Id) == 0 && stock.QuantityOf(x.Id) > 0).Id;
            army.Supply.AddInitial(extra, long.MaxValue);
            var before = State(c); Assert.That(s.Prepare(Army, ArmyOrderKind.CitySupply, Istanbul.Value, extra.Value, 1).Failure, Is.EqualTo(ArmyOrderFailure.Overflow)); Assert.That(State(c), Is.EqualTo(before));
        }
        [Test]
        public void ChangedSupplyAndInsufficientStockRejectWithoutPartialTransfer()
        {
            var c = Campaign(); var s = Orders(c); var p = s.Prepare(Army, ArmyOrderKind.CitySupply, Istanbul.Value, Grain.Value, 1);
            c.Economy.GetRequiredMarket(Istanbul).Stock.Add(Grain, 1); var before = State(c);
            Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.StalePreview)); Assert.That(State(c), Is.EqualTo(before));
            Assert.That(s.Prepare(Army, ArmyOrderKind.CitySupply, Istanbul.Value, Grain.Value, long.MaxValue).Failure, Is.EqualTo(ArmyOrderFailure.InsufficientSource));
        }
        [Test]
        public void ArmyKnowledgeDoesNotGrantCommandOrNpcCargo()
        {
            var c = Campaign(); var before = State(c);
            Assert.That(new ArmyOrderSession(c, CharacterId.Create("mehmed-celebi-tacir")).Authority(Army), Is.EqualTo(ArmyOrderFailure.Unauthorized));
            Assert.That(Orders(c).Prepare(Army, ArmyOrderKind.CaravanSupply, "caravan-bursa-istanbul", "silk-cloth", 1).Failure, Is.EqualTo(ArmyOrderFailure.Unauthorized));
            using var s = new ArmyPanelSession(c, Viewer()); s.Select(ArmyKey, ArmyOrderKind.CaravanSupply, "caravan-bursa-istanbul", "silk-cloth", "1");
            Assert.That(s.Read().Sources, Is.Empty); s.Prepare(); Assert.That(s.Confirm().Succeeded, Is.False); Assert.That(State(c), Is.EqualTo(before));
        }
        [Test]
        public void HiddenArmyDoesNotLeakReadModelOrAcceptOrders()
        {
            var c = Campaign(); var before = State(c); using var s = new ArmyPanelSession(c, Viewer(false));
            s.Select(ArmyKey, ArmyOrderKind.Recruit, Source, Troop, "1"); var view = s.Read();
            Assert.That(view.Armies, Is.Empty); Assert.That(view.Sources, Is.Empty); Assert.That(view.Summary, Is.Empty);
            s.Prepare(); Assert.That(s.Confirm().Succeeded, Is.False); Assert.That(State(c), Is.EqualTo(before));
        }
        [Test]
        public void ActualStartingCampaignHasNoInventedPayroll()
        {
            var c = Campaign(); using var s = new ArmyPanelSession(c, Viewer()); s.Select(ArmyKey, ArmyOrderKind.Payroll, "", "", "1");
            var view = s.Read(); Assert.That(view.Sources, Is.Empty); Assert.That(view.Failure, Is.EqualTo(ArmyOrderFailure.NoObligation));
            Assert.That(view.Resources, Does.Contain("ücret oranı eklenmedi")); Assert.That(c.Military.Armies.GetRequired(Army).Payroll.OrderedObligations, Is.Empty);
        }
        [Test]
        public void IsolatedExistingPayrollFixtureDebitsAuthoredSourceAndCannotPayTwiceOrOverpay()
        {
            var c = Campaign(); var a = c.Military.Armies.GetRequired(Army); var id = PayrollObligationId.Create("test-existing-obligation");
            a.Payroll.AddObligation(new PayrollObligationState(id, 20, c.Clock.Now, PayrollFundingSourceRef.CityMarket(Istanbul)));
            var cash = c.Economy.GetRequiredMarket(Istanbul).CashBalance.Value;
            using var s = new ArmyPanelSession(c, Viewer()); s.Select(ArmyKey, ArmyOrderKind.Payroll, id.Value, "", "12");
            s.Prepare(); Assert.That(s.Read().Confirmation, Is.Not.Null); Assert.That(s.Confirm().Succeeded, Is.True); Assert.That(s.Confirm().Succeeded, Is.False);
            Assert.That(a.Payroll.GetRequired(id).Arrears, Is.EqualTo(8)); Assert.That(c.Economy.GetRequiredMarket(Istanbul).CashBalance.Value, Is.EqualTo(cash - 12));
            Assert.That(Orders(c).Prepare(Army, ArmyOrderKind.Payroll, id.Value, "", 9).Failure, Is.EqualTo(ArmyOrderFailure.ExcessPayment));
            Assert.That(new CampaignSaveValidator().Validate(CampaignSaveMapper.ToSaveData(c)).IsValid, Is.True);
        }
        [TestCase(false)][TestCase(true)]
        public void PayrollCannotDrainAnEmptyCityOrForeignCaravan(bool foreign)
        {
            var c = Campaign(); var a = c.Military.Armies.GetRequired(Army); var id = PayrollObligationId.Create("test-payroll");
            a.Payroll.AddObligation(new PayrollObligationState(id, 20, c.Clock.Now, foreign ? PayrollFundingSourceRef.Caravan(CaravanId.Create("caravan-bursa-istanbul")) : PayrollFundingSourceRef.CityMarket(Istanbul)));
            if (!foreign) { var m = c.Economy.GetRequiredMarket(Istanbul); m.Debit(m.CashBalance.Value); }
            var before = State(c); var s = Orders(c);
            Assert.That(s.Prepare(Army, ArmyOrderKind.Payroll, id.Value, "", 1).Failure, Is.EqualTo(foreign ? ArmyOrderFailure.Unauthorized : ArmyOrderFailure.InsufficientFunds)); Assert.That(State(c), Is.EqualTo(before));
        }
        [TestCase("")][TestCase("-1")][TestCase("1.5")][TestCase("1e3")][TestCase("999999999999999999999")]
        public void InvalidUiQuantityDoesNotMutateCampaign(string amount)
        {
            var c = Campaign(); var before = State(c); using var s = new ArmyPanelSession(c, Viewer()); s.Select(ArmyKey, ArmyOrderKind.Recruit, Source, Troop, amount);
            Assert.That(s.Read().Failure, Is.EqualTo(ArmyOrderFailure.InvalidQuantity)); s.Prepare(); Assert.That(s.Confirm().Succeeded, Is.False); Assert.That(State(c), Is.EqualTo(before));
        }
        [Test]
        public void SaveReloadPreservesIdentitiesAndDeterministicNextRecruitment()
        {
            var c = Campaign(); var s = Orders(c); Assert.That(s.Confirm(Recruit(s)), Is.EqualTo(ArmyOrderFailure.None));
            Assert.That(s.Confirm(s.Prepare(Army, ArmyOrderKind.CitySupply, Istanbul.Value, Grain.Value, 3)), Is.EqualTo(ArmyOrderFailure.None));
            var saved = State(c); var restored = CampaignSaveMapper.ToRuntimeState(new CampaignSaveTextSerializer().Deserialize(saved).Data!);
            Assert.That(State(restored), Is.EqualTo(saved));
            foreach (var state in new[] { c, restored }) { var orders = Orders(state); Assert.That(orders.Confirm(Recruit(orders)), Is.EqualTo(ArmyOrderFailure.None)); }
            Assert.That(State(restored), Is.EqualTo(State(c))); Assert.That(CampaignSaveMapper.ToSaveData(c).SaveVersion, Is.EqualTo(14));
        }
        [Test]
        public void ChangedSelectionAndDisposedSessionReleasePendingConfirmation()
        {
            var c = Campaign(); var before = State(c); using var s = new ArmyPanelSession(c, Viewer()); s.Select(ArmyKey, ArmyOrderKind.Recruit, Source, Troop, "1"); s.Prepare();
            s.Select(ArmyKey, ArmyOrderKind.Recruit, Source, Troop, "2"); Assert.That(s.Confirm().Succeeded, Is.False);
            s.Prepare(); s.Dispose(); Assert.That(s.Confirm().Succeeded, Is.False); Assert.That(State(c), Is.EqualTo(before));
        }

        [Test]
        public void IsolatedLegitimateManagerCommanderFixtureTransfersCaravanStockAndPaysExistingDebt()
        {
            var c = Campaign(); var s = Orders(c); var a = c.Military.Armies.GetRequired(Army);
            var caravan = new CaravanState(CaravanId.Create("test-authorized-caravan"), EconomicOwnerRef.Character(Hasan), Hasan,
                CityId.Create("city-bursa"), Istanbul, 100, 30);
            caravan.Cargo.Add(Grain, 5, caravan.WeightCapacity, c.Economy.Goods); c.Economy.Caravans.Add(caravan);
            Assert.That(s.Prepare(Army, ArmyOrderKind.CaravanSupply, caravan.Id.Value, Grain.Value, 2).Failure, Is.EqualTo(ArmyOrderFailure.WrongLocation));
            caravan.MarkInTransit(); caravan.MarkAtDestination();
            Assert.That(s.Confirm(s.Prepare(Army, ArmyOrderKind.CaravanSupply, caravan.Id.Value, Grain.Value, 2)), Is.EqualTo(ArmyOrderFailure.None));
            Assert.That(a.Supply.QuantityOf(Grain), Is.EqualTo(18)); Assert.That(caravan.Cargo.QuantityOf(Grain), Is.EqualTo(3));
            var obligation = new PayrollObligationState(PayrollObligationId.Create("test-funded-debt"), 20, c.Clock.Now, PayrollFundingSourceRef.Caravan(caravan.Id));
            a.Payroll.AddObligation(obligation);
            Assert.That(s.Confirm(s.Prepare(Army, ArmyOrderKind.Payroll, obligation.Id.Value, "", 10)), Is.EqualTo(ArmyOrderFailure.None));
            Assert.That(caravan.CashBalance.Value + obligation.AmountPaid, Is.EqualTo(30)); Assert.That(obligation.Arrears, Is.EqualTo(10));
            var saved = State(c); var restored = CampaignSaveMapper.ToRuntimeState(new CampaignSaveTextSerializer().Deserialize(saved).Data!);
            Assert.That(State(restored), Is.EqualTo(saved)); Assert.That(new CampaignSaveValidator().Validate(CampaignSaveMapper.ToSaveData(restored)).IsValid, Is.True);
        }

        [Test]
        public void ChangedFundingInvalidatesPayrollPreviewWithoutPartialPayment()
        {
            var c = Campaign(); var s = Orders(c); var a = c.Military.Armies.GetRequired(Army);
            var id = PayrollObligationId.Create("test-existing-debt"); a.Payroll.AddObligation(new PayrollObligationState(id, 20, c.Clock.Now, PayrollFundingSourceRef.CityMarket(Istanbul)));
            var p = s.Prepare(Army, ArmyOrderKind.Payroll, id.Value, "", 2); c.Economy.GetRequiredMarket(Istanbul).Debit(1);
            var before = State(c); Assert.That(s.Confirm(p), Is.EqualTo(ArmyOrderFailure.StalePreview)); Assert.That(State(c), Is.EqualTo(before));
        }
    }
}
