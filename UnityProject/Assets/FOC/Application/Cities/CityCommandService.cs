using System;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Organizations;
using FOC.Domain.Religion;

namespace FOC.Application.Cities
{
    public sealed class CityCommandService
    {
        private readonly CityRegistry _cities;private readonly CharacterRoster _characters;private readonly OrganizationRegistry _organizations;
        public CityCommandService(CityRegistry cities,CharacterRoster characters,OrganizationRegistry organizations){_cities=cities??throw new ArgumentNullException(nameof(cities));_characters=characters??throw new ArgumentNullException(nameof(characters));_organizations=organizations??throw new ArgumentNullException(nameof(organizations));}
        public void SetAreaFullness(CityId cityId,CityAreaType type,CityAreaFullness fullness)=>_cities.GetRequired(cityId).GetRequiredArea(type).SetFullness(fullness);
        public void ActivateBuilding(CityId cityId,CityAreaType type,CityBuildingId buildingId)=>_cities.GetRequired(cityId).GetRequiredArea(type).ActivateBuilding(buildingId);
        public void SetInfrastructure(CityId cityId,CityInfrastructureState infrastructure)=>_cities.GetRequired(cityId).SetInfrastructure(infrastructure);
        public void AddKethuda(CityId cityId,OrganizationId organizationId,AssignmentId assignmentId)
        {
            var city=_cities.GetRequired(cityId);var organization=_organizations.GetRequired(organizationId);AssignmentState? assignment=null;foreach(var candidate in organization.OrderedAssignments)if(candidate.Id.Equals(assignmentId)){assignment=candidate;break;}if(assignment==null)throw new InvalidOperationException("Kethuda assignment was not found.");
            var character=_characters.GetRequired(assignment.CharacterId);if(character.IsDead||character.Location.Kind==CharacterLocationKind.Captivity)throw new InvalidOperationException("Kethuda must be a living, non-captive Character.");if(!assignment.IsActive||assignment.Target.Kind!=AssignmentTargetKind.City||!StringComparer.Ordinal.Equals(assignment.Target.TargetId,cityId.Value)||!StringComparer.Ordinal.Equals(assignment.RoleCode,CityOfficialRoles.KethudaAssignmentRoleCode))throw new InvalidOperationException("Assignment is not an active Kethuda assignment for this City.");city.AddOfficial(new CityOfficialReference(CityOfficialRole.Kethuda,organizationId,assignmentId));
        }
    }

    public static class CityReligionIntegrationRules
    {
        public static void RequireRealCityProfile(ReligionProfile profile,CityRegistry cities){if(profile==null)throw new ArgumentNullException(nameof(profile));if(cities==null)throw new ArgumentNullException(nameof(cities));if(profile.Target.Kind!=ReligionProfileTargetKind.City)throw new InvalidOperationException("Religion profile is not a City profile.");cities.GetRequired(CityId.Create(profile.Target.Id));}
    }
}
