using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Cities;

namespace FOC.Tests
{
    internal static class CityTestFactory
    {
        public static CityDefinition Definition(string id="city-main",bool reverse=false)
        {
            var areas=new List<CityAreaDefinition>();var types=(CityAreaType[])Enum.GetValues(typeof(CityAreaType));if(reverse)Array.Reverse(types);foreach(var type in types){var buildings=new List<CityBuildingDefinition>();if(type==CityAreaType.InnerCastle)buildings.Add(Building("inner-walls",CityBuildingKind.InnerCastleWalls));if(type==CityAreaType.Trade){var market=Building("market",CityBuildingKind.Market);var removed=new CityBuildingDefinition(CityBuildingId.Create("removed-bedesten"),"Removed Bedesten",CityBuildingKind.Bedesten,CityAreaType.Trade,CityBuildingContentStatus.Removed);if(reverse){buildings.Add(removed);buildings.Add(market);}else{buildings.Add(market);buildings.Add(removed);}}if(type==CityAreaType.FoodSupply){buildings.Add(Building("butcher",CityBuildingKind.Butcher));buildings.Add(Building("fishery",CityBuildingKind.Fishery));}areas.Add(new CityAreaDefinition(type,buildings,type==CityAreaType.Housing?"future-housing-variant":string.Empty));}return new CityDefinition(CityId.Create(id),"Test City",areas);
        }
        public static CityState State(string id="city-main",bool reverse=false)=>new CityState(Definition(id,reverse),new CityMetricsState(1250));
        public static CityBuildingDefinition Building(string id,CityBuildingKind kind,CityBuildingContentStatus status=CityBuildingContentStatus.Active)=>new CityBuildingDefinition(CityBuildingId.Create(id),kind.ToString(),kind,CityBuildingRules.RequiredArea(kind),status);
    }
}
