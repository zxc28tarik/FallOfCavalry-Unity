using System;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Economy;
using FOC.Domain.Organizations;

namespace FOC.Domain.Validation
{
    public sealed class EconomyInvariantValidator:IInvariantValidator<CampaignRuntimeState>
    {
        public ValidationResult Validate(CampaignRuntimeState subject)
        {
            var result=new ValidationResult();if(subject==null){result.AddError("CAMPAIGN_NULL","Campaign is required.");return result;}foreach(var market in subject.Economy.OrderedMarkets)Try(result,"MARKET_INVALID",()=>{subject.Cities.GetRequired(market.CityId);foreach(var stock in market.Stock.OrderedStocks)subject.Economy.Goods.GetRequired(stock.GoodId);foreach(var demand in market.Demand.OrderedSources)subject.Economy.Goods.GetRequired(demand.GoodId);});foreach(var recipe in subject.Economy.Recipes.OrderedRecipes)Try(result,"RECIPE_INVALID",()=>{foreach(var line in recipe.OrderedInputs)subject.Economy.Goods.GetRequired(line.GoodId);foreach(var line in recipe.OrderedOutputs)subject.Economy.Goods.GetRequired(line.GoodId);});foreach(var caravan in subject.Economy.Caravans.OrderedCaravans)Try(result,"CARAVAN_INVALID",()=>ValidateCaravan(subject,caravan));return result;
        }
        private static void ValidateCaravan(CampaignRuntimeState campaign,CaravanState caravan)
        {
            campaign.Cities.GetRequired(caravan.OriginCityId);campaign.Cities.GetRequired(caravan.DestinationCityId);var manager=campaign.Characters.GetRequired(caravan.ManagerCharacterId);if(manager.IsDead||manager.Location.Kind==CharacterLocationKind.Captivity)throw new InvalidOperationException();switch(caravan.Owner.Kind){case EconomicOwnerKind.Character:campaign.Characters.GetRequired(CharacterId.Create(caravan.Owner.Id));break;case EconomicOwnerKind.House:campaign.Houses.GetRequired(HouseId.Create(caravan.Owner.Id));break;case EconomicOwnerKind.Organization:campaign.Organizations.GetRequired(OrganizationId.Create(caravan.Owner.Id));break;default:throw new InvalidOperationException();}if(caravan.Cargo.UsedWeight(campaign.Economy.Goods)>caravan.WeightCapacity)throw new InvalidOperationException();if(caravan.Representative!=null){var reference=caravan.Representative;campaign.Characters.GetRequired(reference.CharacterId);var organization=campaign.Organizations.GetRequired(reference.OrganizationId);AssignmentState? assignment=null;foreach(var item in organization.OrderedAssignments)if(item.Id.Equals(reference.AssignmentId)){assignment=item;break;}if(assignment==null||!assignment.IsActive||assignment.Branch!=OrganizationBranch.Trade||!assignment.CharacterId.Equals(reference.CharacterId))throw new InvalidOperationException();}}
        private static void Try(ValidationResult result,string code,Action action){try{action();}catch(Exception){result.AddError(code,"Economy invariant is invalid.");}}
    }
}
