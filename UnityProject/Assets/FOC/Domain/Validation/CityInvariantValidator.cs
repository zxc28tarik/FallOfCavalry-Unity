using System;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Organizations;

namespace FOC.Domain.Validation
{
    public sealed class CityInvariantValidator:IInvariantValidator<CampaignRuntimeState>
    {
        public ValidationResult Validate(CampaignRuntimeState subject)
        {
            var result=new ValidationResult();if(subject==null){result.AddError("CAMPAIGN_NULL","Campaign is required.");return result;}foreach(var city in subject.Cities.OrderedCities){var inner=city.GetRequiredArea(CityAreaType.InnerCastle);if(inner.Fullness!=CityAreaFullness.Full)result.AddError("INNER_CASTLE_NOT_FULL","Inner Castle must remain Full.");foreach(var official in city.OrderedOfficials)ValidateOfficial(subject,city,official,result);}return result;
        }
        private static void ValidateOfficial(CampaignRuntimeState campaign,CityState city,CityOfficialReference official,ValidationResult result)
        {
            try{var organization=campaign.Organizations.GetRequired(official.OrganizationId);AssignmentState? assignment=null;foreach(var item in organization.OrderedAssignments)if(item.Id.Equals(official.AssignmentId)){assignment=item;break;}if(assignment==null)throw new InvalidOperationException();var character=campaign.Characters.GetRequired(assignment.CharacterId);if(character.IsDead||character.Location.Kind==CharacterLocationKind.Captivity||!assignment.IsActive||assignment.Target.Kind!=AssignmentTargetKind.City||!StringComparer.Ordinal.Equals(assignment.Target.TargetId,city.Id.Value)||!StringComparer.Ordinal.Equals(assignment.RoleCode,CityOfficialRoles.KethudaAssignmentRoleCode))throw new InvalidOperationException();}
            catch(Exception){result.AddError("CITY_OFFICIAL_INVALID","City official must resolve to a valid Character assignment for the City.");}
        }
    }
}
