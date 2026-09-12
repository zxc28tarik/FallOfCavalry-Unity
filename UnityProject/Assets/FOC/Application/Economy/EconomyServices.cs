using System;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Economy;

namespace FOC.Application.Economy
{
    public sealed class ProductionService
    {
        private readonly CityRegistry _cities;private readonly EconomyState _economy;public ProductionService(CityRegistry cities,EconomyState economy){_cities=cities??throw new ArgumentNullException(nameof(cities));_economy=economy??throw new ArgumentNullException(nameof(economy));}
        public void Execute(CityId cityId,ProductionRecipeId recipeId)
        {
            var city=_cities.GetRequired(cityId);var market=_economy.GetRequiredMarket(cityId);var recipe=_economy.Recipes.GetRequired(recipeId);var areaType=CityBuildingRules.RequiredArea(recipe.BuildingKind);var area=city.GetRequiredArea(areaType);if(area.Fullness==CityAreaFullness.Empty)throw new InvalidOperationException("An Empty City area cannot produce.");var active=false;foreach(var building in area.OrderedActiveBuildings)if(building.Kind==recipe.BuildingKind&&building.Status==CityBuildingContentStatus.Active){active=true;break;}if(!active)throw new InvalidOperationException("Production requires an active compatible building.");foreach(var input in recipe.OrderedInputs){_economy.Goods.GetRequired(input.GoodId);if(market.Stock.QuantityOf(input.GoodId)<input.Quantity.Value)throw new InvalidOperationException("Insufficient production input.");}foreach(var output in recipe.OrderedOutputs){_economy.Goods.GetRequired(output.GoodId);if(!market.Stock.CanAdd(output.GoodId,output.Quantity.Value))throw new OverflowException("Production output would overflow stock.");}foreach(var input in recipe.OrderedInputs)market.Stock.Remove(input.GoodId,input.Quantity.Value);foreach(var output in recipe.OrderedOutputs)market.Stock.Add(output.GoodId,output.Quantity.Value);
        }
    }
    public sealed class TradeTransactionService
    {
        private readonly CityRegistry _cities;private readonly EconomyState _economy;public TradeTransactionService(CityRegistry cities,EconomyState economy){_cities=cities??throw new ArgumentNullException(nameof(cities));_economy=economy??throw new ArgumentNullException(nameof(economy));}
        public void PurchaseAndLoad(CaravanId caravanId,CityId cityId,TradeGoodId goodId,long quantity,PriceQuote quote)
        {
            if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));var caravan=_economy.Caravans.GetRequired(caravanId);var market=_economy.GetRequiredMarket(cityId);_cities.GetRequired(cityId);_economy.Goods.GetRequired(goodId);RequireQuote(quote,goodId);if(!caravan.OriginCityId.Equals(cityId)||caravan.LocationStage!=CaravanLocationStage.AtOrigin)throw new InvalidOperationException("Caravan is not available at its origin market.");var total=checked(quantity*quote.FinalUnitValue);if(market.Stock.QuantityOf(goodId)<quantity||!caravan.Cargo.CanAdd(goodId,quantity,caravan.WeightCapacity,_economy.Goods)||caravan.CashBalance.Value<total||!market.CanCredit(total)||!caravan.Accounting.CanRecordPurchase(total))throw new InvalidOperationException("Purchase cannot complete atomically.");market.Stock.Remove(goodId,quantity);caravan.Cargo.Add(goodId,quantity,caravan.WeightCapacity,_economy.Goods);caravan.Debit(total);market.Credit(total);caravan.Accounting.RecordPurchase(total);
        }
        public void SellAndUnload(CaravanId caravanId,CityId cityId,TradeGoodId goodId,long quantity,PriceQuote quote)
        {
            if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));var caravan=_economy.Caravans.GetRequired(caravanId);var market=_economy.GetRequiredMarket(cityId);_cities.GetRequired(cityId);_economy.Goods.GetRequired(goodId);RequireQuote(quote,goodId);if(!caravan.DestinationCityId.Equals(cityId)||caravan.LocationStage!=CaravanLocationStage.AtDestination)throw new InvalidOperationException("Caravan is not available at its destination market.");var total=checked(quantity*quote.FinalUnitValue);if(caravan.Cargo.QuantityOf(goodId)<quantity||!market.Stock.CanAdd(goodId,quantity)||market.CashBalance.Value<total||!caravan.CanCredit(total)||!caravan.Accounting.CanRecordSale(total))throw new InvalidOperationException("Sale cannot complete atomically.");caravan.Cargo.Remove(goodId,quantity);market.Stock.Add(goodId,quantity);market.Debit(total);caravan.Credit(total);caravan.Accounting.RecordSale(total);
        }
        private static void RequireQuote(PriceQuote quote,TradeGoodId id){if(quote==null||!quote.GoodId.Equals(id))throw new ArgumentException("Price quote does not match Trade Good.",nameof(quote));}
    }
}
