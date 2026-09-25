#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FOC.Application.Economy;
using FOC.Application.Geography;
using FOC.Application.HistoricalContent;
using FOC.Application.Save;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Geography;
using FOC.Domain.Military;
using FOC.Domain.Soldiers;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class HistoricalSliceContentTests
    {
        [Test]
        public void Manifest_SeparatesHistoricalClaimsFictionAndAllNumericTuning()
        {
            var content=Content();
            Assert.That(content.AnchorDate,Is.EqualTo("1648-09-01"));Assert.That(content.ContentVersion,Is.EqualTo(VerticalSliceCampaignFactory.ContentVersion));Assert.That(content.SourceIds.Count,Is.GreaterThanOrEqualTo(10));
            Assert.That(content.Characters.Count(x=>x.Truth==HistoricalContentTruth.HistoricalAttested),Is.EqualTo(3));Assert.That(content.Characters.Count(x=>x.Truth==HistoricalContentTruth.SliceFiction),Is.EqualTo(5));Assert.That(content.Characters.All(x=>x.StatsTruth==HistoricalContentTruth.SliceTuning),Is.True);
            Assert.That(content.Cities.All(x=>x.Truth==HistoricalContentTruth.HistoricalReconstruction&&x.MetricsTruth==HistoricalContentTruth.SliceTuning),Is.True);
            Assert.That(content.Goods.Any(x=>x.Id=="raw-silk"&&x.Truth==HistoricalContentTruth.HistoricalAttested),Is.True);Assert.That(content.Goods.All(x=>x.NumericTruth==HistoricalContentTruth.SliceTuning),Is.True);
            Assert.That(content.Recipes.All(x=>x.NumericTruth==HistoricalContentTruth.SliceTuning),Is.True);Assert.That(content.Stocks.All(x=>x.NumericTruth==HistoricalContentTruth.SliceTuning),Is.True);
        }

        [Test]
        public void ProductionFactory_HasNoProofFactoryDependencyAndNoTemporaryIdentityLeaks()
        {
            var source=File.ReadAllText(Path.Combine(Root(),"UnityProject","Assets","FOC","Application","Geography","VerticalSliceCampaignFactory.cs"));
            Assert.That(source,Does.Not.Contain("IntegratedProofCampaignFactory"));Assert.That(source,Does.Not.Contain("CreateProof"));
            var presentationSource=File.ReadAllText(Path.Combine(Root(),"UnityProject","Assets","FOC","Presentation","Core","CampaignPresentationQueries.cs"));
            foreach(var forbidden in new[]{"city-home","city-other","commander-a","commander-b","army-a","army-b","slice-player-sipahi"})Assert.That(presentationSource,Does.Not.Contain(forbidden),forbidden);
            var serialized=new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(Campaign()));
            foreach(var forbidden in new[]{"city-home","city-other","commander-a","commander-b","army-a","army-b","slice-player-sipahi","Proof Home","Proof Other","PROOF_ONLY"})Assert.That(serialized,Does.Not.Contain(forbidden),forbidden);
        }

        [Test]
        public void ProductionCampaign_PassesAllHistoricalAndDomainAcceptanceValidators()
        {
            var result=new HistoricalCampaignValidator().Validate(Campaign());
            Assert.That(result.IsValid,Is.True,string.Join("; ",result.Issues.Select(x=>x.Code+":"+x.Message)));
        }

        [Test]
        public void Istanbul_IsCityV2CompliantAndContainsAuthorityProductionFoodAndInfrastructure()
        {
            var city=Campaign().Cities.GetRequired(CityId.Create("city-istanbul"));
            Assert.That(city.OrderedAreas.Count,Is.EqualTo(9));Assert.That(city.GetRequiredArea(CityAreaType.InnerCastle).Fullness,Is.EqualTo(CityAreaFullness.Full));Assert.That(city.OrderedOfficials.Single().Role,Is.EqualTo(CityOfficialRole.Kethuda));
            var kinds=city.OrderedAreas.SelectMany(x=>x.OrderedActiveBuildings).Select(x=>x.Kind).ToArray();
            Assert.That(kinds,Does.Contain(CityBuildingKind.Palace));Assert.That(kinds,Does.Contain(CityBuildingKind.CourtKadiOffice));Assert.That(kinds,Does.Contain(CityBuildingKind.Market));Assert.That(kinds,Does.Contain(CityBuildingKind.Inn));Assert.That(kinds,Does.Contain(CityBuildingKind.Tannery));Assert.That(kinds,Does.Contain(CityBuildingKind.Blacksmith));Assert.That(kinds,Does.Contain(CityBuildingKind.PaperMill));Assert.That(kinds,Does.Contain(CityBuildingKind.Bakery));Assert.That(kinds,Does.Contain(CityBuildingKind.Granary));
            Assert.That(city.OrderedInfrastructure.Count(x=>x.Installed),Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void Economy_HasDifferentiatedMarketsExecutableProductionAndRealCaravanAccounting()
        {
            var campaign=Campaign();var istanbul=campaign.Economy.GetRequiredMarket(CityId.Create("city-istanbul"));var bursa=campaign.Economy.GetRequiredMarket(CityId.Create("city-bursa"));
            Assert.That(istanbul.Stock.QuantityOf(TradeGoodId.Create("raw-silk")),Is.Not.EqualTo(bursa.Stock.QuantityOf(TradeGoodId.Create("raw-silk"))));
            var rawBefore=bursa.Stock.QuantityOf(TradeGoodId.Create("raw-silk"));var clothBefore=bursa.Stock.QuantityOf(TradeGoodId.Create("silk-cloth"));new ProductionService(campaign.Cities,campaign.Economy).Execute(bursa.CityId,ProductionRecipeId.Create("recipe-bursa-silk"));Assert.That(bursa.Stock.QuantityOf(TradeGoodId.Create("raw-silk")),Is.EqualTo(rawBefore-2));Assert.That(bursa.Stock.QuantityOf(TradeGoodId.Create("silk-cloth")),Is.EqualTo(clothBefore+1));
            var caravan=campaign.Economy.Caravans.GetRequired(CaravanId.Create("caravan-bursa-istanbul"));Assert.That(caravan.Accounting.NetResult,Is.Zero);var quote=TradePriceRules.FormQuote(campaign.Economy.Goods.GetRequired(TradeGoodId.Create("silk-cloth")),Array.Empty<PriceAdjustment>());new TradeTransactionService(campaign.Cities,campaign.Economy).PurchaseAndLoad(caravan.Id,bursa.CityId,TradeGoodId.Create("silk-cloth"),1,quote);Assert.That(caravan.Accounting.PurchaseCost.Value,Is.EqualTo(quote.FinalUnitValue));
        }

        [Test]
        public void Military_UsesPersistentUnitsSoldiersAndHistoricallyProvenancedEquipment()
        {
            var campaign=Campaign();var army=campaign.Military.Armies.GetRequired(ArmyId.Create("army-hasan-retinue"));
            Assert.That(army.OrderedUnits.Select(x=>x.TroopDefinitionId),Is.EquivalentTo(new[]{"troop-timarli-sipahi","troop-cebeli","troop-tufekli-piyade"}));Assert.That(army.Headcount,Is.EqualTo(12));Assert.That(campaign.Soldiers.Soldiers.OrderedSoldiers.Count,Is.EqualTo(6));
            Assert.That(campaign.Soldiers.Definitions.OrderedWeapons.Select(x=>x.Id.Value),Is.EquivalentTo(new[]{"kilic","mizrak","fitilli-tufek"}));Assert.That(campaign.Soldiers.Definitions.OrderedArmor.Single().Id.Value,Is.EqualTo("zirh-gomlek"));Assert.That(campaign.Soldiers.Definitions.OrderedShields.Single().Id.Value,Is.EqualTo("kalkan"));Assert.That(campaign.Soldiers.Definitions.OrderedMounts.Single().Id.Value,Is.EqualTo("sipahi-ati"));
            Assert.That(campaign.Soldiers.Equipment.OrderedEquipment.All(x=>x.Provenance.EconomicGoodId.IsValid),Is.True);
        }

        [Test]
        public void ProductionSaveV14_RoundTripsWithoutIdentityOrDeterminismDrift()
        {
            var serializer=new CampaignSaveTextSerializer();var first=CampaignSaveMapper.ToSaveData(Campaign());var text=serializer.Serialize(first);var read=serializer.Deserialize(text);Assert.That(read.Success,Is.True,read.Error);var restored=CampaignSaveMapper.ToRuntimeState(read.Data!);var second=serializer.Serialize(CampaignSaveMapper.ToSaveData(restored));
            Assert.That(first.SaveVersion,Is.EqualTo(14));Assert.That(second,Is.EqualTo(text));Assert.That(restored.Characters.GetRequired(CharacterId.Create("hasan-aga")).Definition.DisplayName,Is.EqualTo("Hasan Ağa"));Assert.That(restored.Cities.GetRequired(CityId.Create("city-istanbul")).Definition.Name,Is.EqualTo("İstanbul"));
        }

        [Test]
        public void V13ToV14_HardCodedFixtureRemapsOldIdentityAndPreservesActiveJourneyProgress()
        {
            var source=new CampaignSaveData{SaveVersion=13,CampaignId="legacy",GameVersion="0.13",ContentDataVersion="HISTORICAL_GEOGRAPHY-1648-1650-r1",WorldSeed=1,WorldGenRevision=1,RngState=1};
            source.Characters.Add(new CharacterSaveData{CharacterId="slice-player-sipahi",DisplayName="Proof Player",Location=new CharacterLocationSaveData{Kind=(int)CharacterLocationKind.City,TargetId="city-home"}});source.Cities.Add(new CitySaveData{CityId="city-home",Name="Proof Home"});source.WorldLocations.Add(new WorldLocationSaveData{WorldLocationId="istanbul",DisplayName="İstanbul",CityId="city-home"});source.TravelJourneys.Add(new TravelJourneySaveData{JourneyId="journey-active",ActorKind=(int)TravelActorKind.Character,ActorId="slice-player-sipahi",OriginId="istanbul",DestinationId="bursa",PathRouteIds={"route-istanbul-uskudar","route-uskudar-izmit"},SegmentIndex=1,SegmentElapsedTicks=4321,Lifecycle=(int)TravelLifecycle.Active,DepartedAt=100,LastAdvancedAt=4421});
            var migrated=new CampaignSaveV13ToV14Migration().Apply(source);var journey=migrated.TravelJourneys.Single();
            Assert.That(migrated.SaveVersion,Is.EqualTo(14));Assert.That(migrated.ContentDataVersion,Is.EqualTo(VerticalSliceCampaignFactory.ContentVersion));Assert.That(migrated.Characters.Single().CharacterId,Is.EqualTo("hasan-aga"));Assert.That(migrated.Characters.Single().Location.TargetId,Is.EqualTo("city-istanbul"));Assert.That(migrated.Cities.Single().CityId,Is.EqualTo("city-istanbul"));Assert.That(migrated.WorldLocations.Single().CityId,Is.EqualTo("city-istanbul"));Assert.That(journey.ActorId,Is.EqualTo("hasan-aga"));Assert.That(journey.PathRouteIds,Is.EqualTo(new[]{"route-istanbul-uskudar","route-uskudar-izmit"}));Assert.That(journey.SegmentIndex,Is.EqualTo(1));Assert.That(journey.SegmentElapsedTicks,Is.EqualTo(4321));Assert.That(journey.Lifecycle,Is.EqualTo((int)TravelLifecycle.Active));
        }

        [Test]
        public void SameContentAndSeed_ProduceByteIdenticalInitialSave()
        {
            var serializer=new CampaignSaveTextSerializer();Assert.That(serializer.Serialize(CampaignSaveMapper.ToSaveData(Campaign())),Is.EqualTo(serializer.Serialize(CampaignSaveMapper.ToSaveData(Campaign()))));
        }

        [Test]
        public void SliceLocalizer_NeverLeaksInternalPresentationKeys()
        {
            var localizer=new SlicePresentationLocalizer();foreach(var key in new[]{"presentation.screen.city","presentation.good.raw-silk","presentation.city.area.inner-castle","presentation.morale.steady","presentation.reason.unknown-to-viewer"})Assert.That(localizer.Get(key),Does.Not.StartWith("presentation."),key);
        }

        [Test]
        public void HistoricalContentBenchmarks_RecordLoadValidationAndSaveProjection()
        {
            var watch=Stopwatch.StartNew();for(var i=0;i<100;i++)Content();watch.Stop();TestContext.Progress.WriteLine("HISTORICAL_CONTENT_PERFORMANCE manifest_loads=100 elapsed_ms="+watch.ElapsedMilliseconds);
            var campaign=Campaign();watch.Restart();for(var i=0;i<100;i++){var result=new HistoricalCampaignValidator().Validate(campaign);Assert.That(result.IsValid,Is.True);}watch.Stop();TestContext.Progress.WriteLine("HISTORICAL_CONTENT_PERFORMANCE validations=100 elapsed_ms="+watch.ElapsedMilliseconds);
            watch.Restart();for(var i=0;i<100;i++)CampaignSaveMapper.ToSaveData(campaign);watch.Stop();TestContext.Progress.WriteLine("HISTORICAL_CONTENT_PERFORMANCE save_projections=100 elapsed_ms="+watch.ElapsedMilliseconds);
        }

        private static HistoricalSliceContent Content()=>new HistoricalSliceContentLoader().Load(ReadHistorical());
        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign()=>VerticalSliceCampaignFactory.Create(ReadGeo("vertical-slice-locations.txt"),ReadGeo("vertical-slice-routes.txt"),ReadHistorical());
        private static string ReadHistorical()=>File.ReadAllText(Path.Combine(Root(),"UnityProject","Assets","FOC","Content","Resources","FOC","HistoricalSlice","historical-slice-content.txt"));
        private static string ReadGeo(string file)=>File.ReadAllText(Path.Combine(Root(),"UnityProject","Assets","FOC","Content","Resources","FOC","Geography",file));
        private static string Root(){var current=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);while(current!=null){if(File.Exists(Path.Combine(current.FullName,"FallOfCavalry.sln")))return current.FullName;current=current.Parent;}throw new DirectoryNotFoundException();}
    }
}
