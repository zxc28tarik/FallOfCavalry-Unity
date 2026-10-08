#nullable enable
using System;
using System.Linq;
using FOC.Application.Economy;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CityInspectionTests
    {
        private static CampaignRuntimeState Campaign() => PlayableTradeTests.Campaign();
        private static string State(CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
        private static CityInspectionSession Session(CampaignRuntimeState c, params string[] ids) => new CityInspectionSession(c,
            new PresentationViewerContext(FactionId.Create("faction-ottoman-state"), new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"),
                (ids.Length == 0 ? new[] { "city-istanbul", "city-bursa" } : ids).Select(x => new PresentationEntityRef(PresentationEntityKind.City, x))));
        [Test]
        public void AllSelectionsAndRepeatedReadsPreserveEntireCampaignAndSaveVersion()
        {
            var c = Campaign(); var before = State(c); using var s = Session(c);
            foreach (var city in s.Read().Cities)
            {
                s.OpenCity(city.Id);
                foreach (CityInspectionTab tab in Enum.GetValues(typeof(CityInspectionTab)))
                {
                    s.SelectTab(tab);
                    foreach (CityAreaType a in Enum.GetValues(typeof(CityAreaType))) { s.SelectArea(a); Assert.That(s.Read().Areas.Count, Is.EqualTo(9)); }
                    foreach (var recipe in s.Read().Recipes) { s.SelectRecipe(recipe.Id); s.Read(); }
                    s.FilterStocks("tahıl", false); s.Read(); s.FilterStocks("", true); s.Read(); s.FilterStocks("", false);
                }
            }
            Assert.That(State(c), Is.EqualTo(before)); Assert.That(CampaignSaveMapper.ToSaveData(c).SaveVersion, Is.EqualTo(14));
        }
        [TestCase("city-edirne")][TestCase("city-missing")]
        public void UnknownRouteNeverFallsBackToKnownCityOrLeaksStocks(string id)
        {
            using var s = Session(Campaign()); s.OpenCity(id); var r = s.Read();
            Assert.That(r.CityId, Is.EqualTo(id)); Assert.That(r.Available, Is.False); Assert.That(r.Stocks, Is.Empty);
            Assert.That(r.Areas, Is.Empty); Assert.That(r.Recipes, Is.Empty); Assert.That(r.Officials, Is.Empty);
            Assert.That(r.Cities.Select(x => x.Id), Is.EquivalentTo(new[] { "city-istanbul", "city-bursa" }));
        }
        [Test]
        public void UnassessedAndMissingInfrastructureAreNotConvertedToZeroOrUninstalled()
        {
            using var s = Session(Campaign()); s.OpenCity("city-istanbul"); var r = s.Read();
            Assert.That(r.Summary, Does.Contain("değerlendirilmemiş")); Assert.That(r.Areas[0], Does.Contain("Tam dolu"));
            Assert.That(r.Infrastructure.Count, Is.EqualTo(9)); Assert.That(r.Infrastructure[0], Does.Contain("Kurulu · Kullanılabilir"));
            Assert.That(r.Infrastructure.Last(), Does.Contain("Kayıt yok; kurulu değil anlamına gelmez"));
        }
        [Test]
        public void KnownCityDoesNotRevealHiddenOfficialsIdentityOrLiveStatus()
        {
            var c = Campaign(); using var s = Session(c); s.OpenCity("city-istanbul"); var before = s.Read().Officials;
            Assert.That(before, Does.Contain("Organization görev referansı")); Assert.That(before, Does.Not.Contain("ahmed-efendi"));
            c.Organizations.OrderedOrganizations.SelectMany(x => x.OrderedAssignments).First(x => x.RoleCode == CityOfficialRoles.KethudaAssignmentRoleCode).Cancel();
            Assert.That(s.Read().Officials, Is.EqualTo(before));
        }
        [Test]
        public void BuildingStatesDistinguishActiveLockedInactiveAndRemoved()
        {
            var c = Campaign(); var city = CityTestFactory.State(); c.Cities.Add(city); var trade = city.GetRequiredArea(CityAreaType.Trade);
            trade.SetFullness(CityAreaFullness.Low); trade.LockBuilding(CityBuildingId.Create("market"));
            using var s = Session(c, "city-main"); s.SelectArea(CityAreaType.Trade); var r = s.Read();
            Assert.That(r.Buildings, Has.Some.Contains("Kilitli")); Assert.That(r.Buildings, Has.Some.Contains("kaldırılmış"));
            s.SelectArea(CityAreaType.FoodSupply); Assert.That(s.Read().Buildings, Has.All.Contains("Etkin değil"));
            var food = city.GetRequiredArea(CityAreaType.FoodSupply); food.SetFullness(CityAreaFullness.Low); food.ActivateBuilding(CityBuildingId.Create("mill"));
            Assert.That(s.Read().Buildings, Has.Some.EqualTo("Değirmen — Etkin"));
        }
        [TestCase("city-istanbul", "recipe-mill-flour", ProductionInspectionResult.BuildingInactive)]
        [TestCase("city-istanbul", "recipe-tannery-leather", ProductionInspectionResult.InsufficientInput)]
        [TestCase("city-istanbul", "recipe-smith-weapons", ProductionInspectionResult.InsufficientInput)]
        [TestCase("city-istanbul", "recipe-paper", ProductionInspectionResult.InsufficientInput)]
        [TestCase("city-bursa", "recipe-bursa-silk", ProductionInspectionResult.MechanicallyReady)]
        [TestCase("city-bursa", "recipe-mill-flour", ProductionInspectionResult.MechanicallyReady)]
        public void PreviewAgreesWithActualProductionServiceWithoutExecutingIt(string city, string recipe, ProductionInspectionResult expected)
        {
            var c = Campaign(); var before = State(c); using var s = Session(c); s.OpenCity(city); s.SelectRecipe(recipe);
            Assert.That(s.Read().ProductionResult, Is.EqualTo(expected)); Assert.That(State(c), Is.EqualTo(before));
            var clone = CampaignSaveMapper.ToRuntimeState(new CampaignSaveTextSerializer().Deserialize(before).Data!);
            var service = new ProductionService(clone.Cities, clone.Economy);
            if (expected == ProductionInspectionResult.MechanicallyReady) Assert.DoesNotThrow(() => service.Execute(CityId.Create(city), ProductionRecipeId.Create(recipe)));
            else Assert.Throws<InvalidOperationException>(() => service.Execute(CityId.Create(city), ProductionRecipeId.Create(recipe)));
        }
        [Test]
        public void EmptyAreaAndOutputOverflowAreReportedAndNeverMutateStock()
        {
            var c = Campaign(); var city = CityTestFactory.State(); c.Cities.Add(city);
            c.Economy.AddMarket(new CityMarketState(city.Id, 0));
            using var s = Session(c, "city-main"); s.SelectRecipe("recipe-mill-flour");
            Assert.That(s.Read().ProductionResult, Is.EqualTo(ProductionInspectionResult.AreaEmpty));
            var area = city.GetRequiredArea(CityAreaType.FoodSupply); area.SetFullness(CityAreaFullness.Low); area.ActivateBuilding(CityBuildingId.Create("mill"));
            var stock = c.Economy.GetRequiredMarket(city.Id).Stock; stock.Add(TradeGoodId.Create("grain"), 2); stock.Add(TradeGoodId.Create("flour"), long.MaxValue);
            Assert.That(s.Read().ProductionResult, Is.EqualTo(ProductionInspectionResult.OutputOverflow));
            Assert.Throws<OverflowException>(() => new ProductionService(c.Cities, c.Economy).Execute(city.Id, ProductionRecipeId.Create("recipe-mill-flour")));
            Assert.That(stock.QuantityOf(TradeGoodId.Create("grain")), Is.EqualTo(2));
        }
        [Test]
        public void OutputOverflowMatchesPreRemovalGuardEvenForSameInputAndOutputGood()
        {
            var c = Campaign(); var city = CityId.Create("city-bursa"); var grain = TradeGoodId.Create("grain"); var stock = c.Economy.GetRequiredMarket(city).Stock;
            stock.Add(grain, long.MaxValue - stock.QuantityOf(grain));
            var id = ProductionRecipeId.Create("same-good-test"); c.Economy.Recipes.Add(new ProductionRecipeDefinition(id, "Fixture", CityBuildingKind.Mill, new[] { new RecipeGoodsLine(grain, 2) }, new[] { new RecipeGoodsLine(grain, 1) }));
            using var s = Session(c); s.OpenCity(city.Value); s.SelectRecipe(id.Value);
            Assert.That(s.Read().ProductionResult, Is.EqualTo(ProductionInspectionResult.OutputOverflow));
            Assert.Throws<OverflowException>(() => new ProductionService(c.Cities, c.Economy).Execute(city, id));
        }
        [TestCase("TAHIL")][TestCase("Tahıl")][TestCase("tahıl")]
        public void StockFiltersHandleTurkishSearchTrueShortageAndDemandOverflow(string query)
        {
            var c = Campaign(); using var s = Session(c); s.OpenCity("city-istanbul"); s.FilterStocks(query, false);
            var r = s.Read(); Assert.That(r.Stocks.Count, Is.EqualTo(1)); Assert.That(r.Stocks[0].Stock, Is.EqualTo(120));
            s.FilterStocks("", true); Assert.That(s.Read().Stocks, Is.Empty);
            var market = c.Economy.GetRequiredMarket(CityId.Create("city-istanbul")); var grain = TradeGoodId.Create("grain");
            market.Stock.Remove(grain, 100); Assert.That(s.Read().Stocks.Single().Shortage, Is.EqualTo(10));
            market.Demand.AddSource(new DemandSourceState("overflow-fixture", DemandSourceKind.EventFuture, grain, long.MaxValue));
            Assert.That(s.Read().Stocks.Single().Demand, Is.Null); Assert.That(s.Read().Stocks.Single().Text, Does.Contain("hesaplanamıyor"));
        }
        [Test]
        public void SnapshotsAreDetachedAndRefreshAfterRealStateChangesAndRoundtrip()
        {
            var c = Campaign(); using var s = Session(c); s.OpenCity("city-bursa"); s.SelectRecipe("recipe-mill-flour"); var before = s.Read();
            new ProductionService(c.Cities, c.Economy).Execute(CityId.Create("city-bursa"), ProductionRecipeId.Create("recipe-mill-flour"));
            Assert.That(before.Stocks.Single(x => x.Id == "grain").Stock, Is.EqualTo(55));
            Assert.That(s.Read().Stocks.Single(x => x.Id == "grain").Stock, Is.EqualTo(53));
            var restored = CampaignSaveMapper.ToRuntimeState(new CampaignSaveTextSerializer().Deserialize(State(c)).Data!);
            using var after = Session(restored); after.OpenCity("city-bursa"); after.SelectRecipe("recipe-mill-flour");
            Assert.That(after.Read().Stocks.Select(x => x.Text), Is.EqualTo(s.Read().Stocks.Select(x => x.Text)));
            Assert.That(after.Read().ProductionDetail, Is.EqualTo(s.Read().ProductionDetail));
        }
        [Test]
        public void InvalidSelectionAndDisposedSessionCannotExecuteAnything()
        {
            using var s = Session(Campaign()); s.SelectRecipe("missing"); Assert.That(s.Read().ProductionResult, Is.EqualTo(ProductionInspectionResult.Unavailable));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SelectTab((CityInspectionTab)99)); Assert.Throws<ArgumentOutOfRangeException>(() => s.SelectArea((CityAreaType)99));
            s.Dispose(); Assert.Throws<ObjectDisposedException>(() => s.Read()); Assert.Throws<ObjectDisposedException>(() => s.OpenCity("city-bursa"));
        }
        [Test]
        public void EveryCityEnumHasNonemptyTurkishPresentationText()
        {
            var localizer = new SlicePresentationLocalizer();
            foreach (CityBuildingKind v in Enum.GetValues(typeof(CityBuildingKind))) Assert.That(CityInspectionText.Building(v), Is.Not.Empty);
            foreach (CityAreaType v in Enum.GetValues(typeof(CityAreaType))) Assert.That(localizer.Get("presentation.city.area." + v.ToString().ToLowerInvariant()), Is.EqualTo(CityInspectionText.Area(v)));
            foreach (CityAreaFullness v in Enum.GetValues(typeof(CityAreaFullness))) Assert.That(localizer.Get("presentation.city.fullness." + v.ToString().ToLowerInvariant()), Is.EqualTo(CityInspectionText.Fullness(v)));
            foreach (CityInfrastructureType v in Enum.GetValues(typeof(CityInfrastructureType))) Assert.That(CityInspectionText.Infrastructure(v), Is.Not.Empty);
        }
    }
}
