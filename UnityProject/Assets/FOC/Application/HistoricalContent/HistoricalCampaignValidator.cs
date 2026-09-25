#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Validation;

namespace FOC.Application.HistoricalContent
{
    /// <summary>One deterministic acceptance gate for the production-candidate 1648 vertical-slice composition.</summary>
    public sealed class HistoricalCampaignValidator
    {
        private static readonly string[] ForbiddenIdentityFragments={"city-home","city-other","commander-a","commander-b","army-a","army-b","slice-player-sipahi","proof home","proof other","PROOF_ONLY"};

        public ValidationResult Validate(CampaignRuntimeState campaign)
        {
            var result=new ValidationResult();
            if(campaign==null){result.AddError("HISTORICAL_CAMPAIGN_NULL","Historical campaign is required.");return result;}
            result.Merge(new SocialInvariantValidator().Validate(campaign.Characters,campaign.Organizations,campaign.Houses,campaign.Cliques));
            result.Merge(new ReligionInvariantValidator().Validate(campaign));
            result.Merge(new CityInvariantValidator().Validate(campaign));
            result.Merge(new EconomyInvariantValidator().Validate(campaign));
            result.Merge(new DiplomacyInvariantValidator().Validate(campaign));
            result.Merge(new MilitaryInvariantValidator().Validate(campaign));
            result.Merge(new SoldierInvariantValidator().Validate(campaign));
            result.Merge(new GeographyInvariantValidator().Validate(campaign));
            if(!StringComparer.Ordinal.Equals(campaign.ContentDataVersion,VerticalSliceCampaignFactory.ContentVersion))result.AddError("HISTORICAL_CONTENT_VERSION","Historical content data version does not match the production factory.");
            RequireCounts(campaign,result);
            ValidateLocations(campaign,result);
            ValidateSave(campaign,result);
            return result;
        }

        private static void RequireCounts(CampaignRuntimeState c,ValidationResult r)
        {
            var requiredCities=new[]{"city-istanbul","city-bursa","city-edirne","city-izmit"};
            foreach(var id in requiredCities)Try(r,"HISTORICAL_CITY_MISSING",()=>c.Cities.GetRequired(CityId.Create(id)));
            foreach(var id in new[]{"hasan-aga","sultan-mehmed-iv","kosem-sultan","sofu-mehmed-pasa","ahmed-efendi-kethuda","mehmed-celebi-tacir","ali-cavus","yusuf-katip"})Try(r,"HISTORICAL_CHARACTER_MISSING",()=>c.Characters.GetRequired(CharacterId.Create(id)));
            if(c.Economy.Goods.OrderedGoods.Count<15||c.Economy.Recipes.OrderedRecipes.Count<5||c.Economy.OrderedMarkets.Count!=4)r.AddError("HISTORICAL_ECONOMY_INCOMPLETE","Historical economy requires 15 goods, five recipes and four markets.");
            if(c.Economy.Caravans.OrderedCaravans.Count!=1)r.AddError("HISTORICAL_CARAVAN_COUNT","Historical slice requires exactly one initial real caravan.");
            if(c.Military.Armies.OrderedArmies.Count!=1||c.Military.Armies.OrderedArmies.Single().OrderedUnits.Count!=3)r.AddError("HISTORICAL_MILITARY_INCOMPLETE","Historical slice requires one army with three UnitGroups.");
            if(c.Soldiers.Soldiers.OrderedSoldiers.Count!=6)r.AddError("HISTORICAL_SOLDIERS_INCOMPLETE","Historical slice requires six persistent Soldiers.");
            if(c.Battles.Battles.OrderedBattles.Count!=0||c.EncounterContracts.Encounters.OrderedEncounters.Count!=0||c.EncounterContracts.Contracts.OrderedContracts.Count!=0)r.AddError("HISTORICAL_STORY_SCOPE","14B may not pre-script battles, encounters or contracts.");
        }

        private static void ValidateLocations(CampaignRuntimeState c,ValidationResult r)
        {
            foreach(var city in c.Cities.OrderedCities){try{c.Geography.World.LocationForCity(city.Id);}catch(Exception){r.AddError("HISTORICAL_CITY_MAP_LINK","Each slice City must resolve to one WorldLocation: "+city.Id.Value);}}
            foreach(var character in c.Characters.OrderedCharacters)
            {
                try
                {
                    switch(character.Location.Kind)
                    {
                        case CharacterLocationKind.City:c.Cities.GetRequired(character.Location.CityId!.Value);break;
                        case CharacterLocationKind.Army:c.Military.Armies.GetRequired(character.Location.ArmyId!.Value);break;
                        case CharacterLocationKind.Caravan:c.Economy.Caravans.GetRequired(character.Location.CaravanId!.Value);break;
                    }
                }
                catch(Exception){r.AddError("HISTORICAL_CHARACTER_LOCATION","Character location does not resolve: "+character.Id.Value);}
            }
        }

        private static void ValidateSave(CampaignRuntimeState c,ValidationResult r)
        {
            var save=CampaignSaveMapper.ToSaveData(c);r.Merge(new CampaignSaveValidator().Validate(save));
            if(save.SaveVersion!=CampaignSaveData.CurrentSaveVersion||save.SaveVersion!=14)r.AddError("HISTORICAL_SAVE_VERSION","Historical slice must emit save schema v14.");
            foreach(var value in Strings(save))foreach(var forbidden in ForbiddenIdentityFragments)if(value.IndexOf(forbidden,StringComparison.OrdinalIgnoreCase)>=0)r.AddError("HISTORICAL_PROOF_IDENTITY","Production campaign contains forbidden proof identity: "+forbidden);
        }

        private static IEnumerable<string> Strings(object root)
        {
            var pending=new Stack<object>();var seen=new HashSet<object>(ReferenceComparer.Instance);pending.Push(root);
            while(pending.Count>0)
            {
                var current=pending.Pop();if(current is string text){yield return text;continue;}var type=current.GetType();if(type.IsPrimitive||type.IsEnum||type.IsValueType||!seen.Add(current))continue;
                if(current is IEnumerable sequence){foreach(var item in sequence)if(item!=null)pending.Push(item);continue;}
                foreach(var property in type.GetProperties(BindingFlags.Instance|BindingFlags.Public))if(property.CanRead&&property.GetIndexParameters().Length==0){var value=property.GetValue(current,null);if(value!=null)pending.Push(value);}
            }
        }

        private static void Try(ValidationResult result,string code,Action action){try{action();}catch(Exception exception){result.AddError(code,exception.Message);}}
        private sealed class ReferenceComparer:IEqualityComparer<object>{public static readonly ReferenceComparer Instance=new ReferenceComparer();public new bool Equals(object? x,object? y)=>ReferenceEquals(x,y);public int GetHashCode(object obj)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);}
    }
}
