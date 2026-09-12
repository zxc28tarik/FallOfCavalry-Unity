using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;

namespace FOC.Application.Save
{
    public static partial class CampaignSaveMapper
    {
        private static void AddCitySaveData(FOC.Domain.Campaign.CampaignRuntimeState state,CampaignSaveData data)
        {
            foreach(var city in state.Cities.OrderedCities){var dto=new CitySaveData{CityId=city.Id.Value,Name=city.Definition.Name,PopulationCount=city.Metrics.PopulationCount,Wealth=(int)city.Metrics.Wealth,Order=(int)city.Metrics.Order,Health=(int)city.Metrics.Health,Security=(int)city.Metrics.Security};foreach(var area in city.OrderedAreas){var a=new CityAreaSaveData{Type=(int)area.Type,Fullness=(int)area.Fullness,VisualVariantHook=area.Definition.VisualVariantHook};foreach(var b in area.Definition.OrderedBuildingPool){var bd=new CityBuildingSaveData{CityBuildingId=b.Id.Value,Name=b.Name,Kind=(int)b.Kind,AreaType=(int)b.AreaType,Status=(int)b.Status};foreach(var tag in b.EffectTags)bd.EffectTags.Add((int)tag);a.BuildingPool.Add(bd);}foreach(var b in area.OrderedActiveBuildings)a.ActiveBuildingIds.Add(b.Id.Value);foreach(var id in area.OrderedLockedBuildingIds)a.LockedBuildingIds.Add(id.Value);dto.Areas.Add(a);}foreach(var x in city.OrderedInfrastructure)dto.Infrastructure.Add(new CityInfrastructureSaveData{Type=(int)x.Type,Installed=x.Installed,Condition=(int)x.Condition});foreach(var x in city.OrderedOfficials)dto.Officials.Add(new CityOfficialSaveData{Role=(int)x.Role,OrganizationId=x.OrganizationId.Value,AssignmentId=x.AssignmentId.Value});data.Cities.Add(dto);}
        }
        private static CityRegistry RestoreCities(CampaignSaveData data)
        {
            var registry=new CityRegistry();foreach(var dto in data.Cities){var definitions=new List<CityAreaDefinition>();foreach(var area in dto.Areas){var buildings=new List<CityBuildingDefinition>();foreach(var b in area.BuildingPool){var tags=new List<CityInstitutionEffectTag>();foreach(var tag in b.EffectTags)tags.Add((CityInstitutionEffectTag)tag);buildings.Add(new CityBuildingDefinition(CityBuildingId.Create(b.CityBuildingId),b.Name,(CityBuildingKind)b.Kind,(CityAreaType)b.AreaType,(CityBuildingContentStatus)b.Status,tags));}definitions.Add(new CityAreaDefinition((CityAreaType)area.Type,buildings,area.VisualVariantHook));}var city=new CityState(new CityDefinition(CityId.Create(dto.CityId),dto.Name,definitions),new CityMetricsState(dto.PopulationCount,(CityMetricAssessment)dto.Wealth,(CityMetricAssessment)dto.Order,(CityMetricAssessment)dto.Health,(CityMetricAssessment)dto.Security));foreach(var area in dto.Areas){var state=city.GetRequiredArea((CityAreaType)area.Type);state.SetFullness((CityAreaFullness)area.Fullness);foreach(var id in area.ActiveBuildingIds)state.ActivateBuilding(CityBuildingId.Create(id));foreach(var id in area.LockedBuildingIds)state.LockBuilding(CityBuildingId.Create(id));}foreach(var x in dto.Infrastructure)city.SetInfrastructure(new CityInfrastructureState((CityInfrastructureType)x.Type,x.Installed,(CityInfrastructureCondition)x.Condition));foreach(var x in dto.Officials)city.AddOfficial(new CityOfficialReference((CityOfficialRole)x.Role,OrganizationId.Create(x.OrganizationId),AssignmentId.Create(x.AssignmentId)));registry.Add(city);}return registry;
        }
    }
}
