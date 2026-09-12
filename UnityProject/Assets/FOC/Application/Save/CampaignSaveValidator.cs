using System;
using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Validation;
using FOC.Domain.Organizations;
using FOC.Domain.Houses;
using FOC.Domain.Cliques;
using FOC.Domain.Religion;
using FOC.Domain.Cities;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveValidator : IInvariantValidator<CampaignSaveData>
    {
        public ValidationResult Validate(CampaignSaveData subject)
        {
            var result = new ValidationResult();
            if (subject == null)
            {
                result.AddError("SAVE_NULL", "Campaign save data is required.");
                return result;
            }

            if (subject.SaveVersion <= 0)
            {
                result.AddError("SAVE_VERSION_INVALID", "SaveVersion must be positive.");
            }

            else if (subject.SaveVersion > CampaignSaveData.CurrentSaveVersion)
            {
                result.AddError("SAVE_VERSION_FUTURE", "SaveVersion is newer than this game can read.");
            }

            if (subject.Characters == null)
            {
                result.AddError("CHARACTERS_NULL", "Characters collection is required.");
            }

            if (subject.CharacterRelations == null)
            {
                result.AddError("CHARACTER_RELATIONS_NULL", "Character relations collection is required.");
            }

            if (subject.Organizations == null || subject.Houses == null || subject.Cliques == null)
            {
                result.AddError("SOCIAL_COLLECTION_NULL", "Organization, House and Clique collections are required.");
            }
            if (subject.Religions == null || subject.Sects == null || subject.CharacterReligions == null || subject.ReligionProfiles == null || subject.ReligionPolicies == null || subject.ReligiousCliqueAssociations == null)
            {
                result.AddError("RELIGION_COLLECTION_NULL", "Religion collections are required.");
            }
            if(subject.Cities==null)result.AddError("CITY_COLLECTION_NULL","City collection is required.");

            if (subject.Characters != null && subject.CharacterRelations != null)
            {
                ValidateCharacters(subject, result);
                if (subject.Organizations != null && subject.Houses != null && subject.Cliques != null) ValidateSocial(subject, result);
                if (subject.Religions != null && subject.Sects != null && subject.CharacterReligions != null && subject.ReligionProfiles != null && subject.ReligionPolicies != null && subject.ReligiousCliqueAssociations != null) ValidateReligion(subject, result);
                if(subject.Cities!=null&&subject.Organizations!=null)ValidateCities(subject,result);
            }

            if (string.IsNullOrWhiteSpace(subject.CampaignId))
            {
                result.AddError("CAMPAIGN_ID_INVALID", "CampaignId is required.");
            }

            if (string.IsNullOrWhiteSpace(subject.GameVersion))
            {
                result.AddError("GAME_VERSION_INVALID", "GameVersion is required.");
            }

            if (string.IsNullOrWhiteSpace(subject.ContentDataVersion))
            {
                result.AddError("CONTENT_VERSION_INVALID", "ContentDataVersion is required.");
            }

            if (subject.WorldGenRevision < 0)
            {
                result.AddError("WORLD_GEN_REVISION_INVALID", "WorldGenRevision cannot be negative.");
            }

            if (subject.WorldTime < 0)
            {
                result.AddError("WORLD_TIME_INVALID", "WorldTime cannot be negative.");
            }

            if (subject.RngState == 0)
            {
                result.AddError("RNG_STATE_INVALID", "RngState cannot be zero.");
            }

            return result;
        }

        private static void ValidateReligion(CampaignSaveData subject, ValidationResult result)
        {
            var religions=new HashSet<string>(StringComparer.Ordinal);foreach(var x in subject.Religions)if(x==null||string.IsNullOrWhiteSpace(x.ReligionId)||string.IsNullOrWhiteSpace(x.Name)||!religions.Add(x.ReligionId)||!Enum.IsDefined(typeof(ContentStatus),x.Status))result.AddError("RELIGION_INVALID","Religion definition is invalid.");
            var sects=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var x in subject.Sects)if(x==null||string.IsNullOrWhiteSpace(x.SectId)||string.IsNullOrWhiteSpace(x.Name)||!religions.Contains(x.ParentReligionId)||sects.ContainsKey(x.SectId)||!Enum.IsDefined(typeof(ContentStatus),x.Status))result.AddError("SECT_INVALID","Sect definition is invalid.");else sects.Add(x.SectId,x.ParentReligionId);
            var characters=new HashSet<string>(StringComparer.Ordinal);foreach(var x in subject.Characters)if(x!=null)characters.Add(x.CharacterId);var assigned=new HashSet<string>(StringComparer.Ordinal);foreach(var x in subject.CharacterReligions)if(x==null||!characters.Contains(x.CharacterId)||!assigned.Add(x.CharacterId)||!PairValid(x.ReligionId,x.SectId,religions,sects))result.AddError("CHARACTER_RELIGION_INVALID","Character religion is invalid.");
            var profileTargets=new HashSet<string>(StringComparer.Ordinal);foreach(var x in subject.ReligionProfiles){if(x==null||!TargetValid(x.TargetKind,x.TargetId)||!profileTargets.Add(x.TargetKind+"\n"+x.TargetId)||x.Entries==null){result.AddError("RELIGION_PROFILE_INVALID","Religion profile is invalid.");continue;}var pairs=new HashSet<string>(StringComparer.Ordinal);foreach(var e in x.Entries)if(e==null||e.RelativePresence<=0||!PairValid(e.ReligionId,e.SectId,religions,sects)||!pairs.Add(e.ReligionId+"\n"+e.SectId))result.AddError("RELIGION_PROFILE_ENTRY_INVALID","Religion profile entry is invalid.");}
            var policyTargets=new HashSet<string>(StringComparer.Ordinal);foreach(var x in subject.ReligionPolicies){if(x==null||!TargetValid(x.TargetKind,x.TargetId)||!policyTargets.Add(x.TargetKind+"\n"+x.TargetId)||x.Rules==null){result.AddError("RELIGION_POLICY_INVALID","Religion policy is invalid.");continue;}var pairs=new HashSet<string>(StringComparer.Ordinal);foreach(var e in x.Rules)if(e==null||!PairValid(e.ReligionId,e.SectId,religions,sects)||!pairs.Add(e.ReligionId+"\n"+e.SectId)||!Enum.IsDefined(typeof(ReligionRecognition),e.Recognition)||!Enum.IsDefined(typeof(ReligionTreatment),e.Treatment)||!Enum.IsDefined(typeof(ReligionEnforcement),e.Enforcement))result.AddError("RELIGION_POLICY_RULE_INVALID","Religion policy rule is invalid.");}
            var cliques=new Dictionary<string,int>(StringComparer.Ordinal);foreach(var x in subject.Cliques)if(x!=null)cliques[x.CliqueId]=x.Type;var linked=new HashSet<string>(StringComparer.Ordinal);foreach(var x in subject.ReligiousCliqueAssociations)if(x==null||!linked.Add(x.CliqueId)||!cliques.TryGetValue(x.CliqueId,out var type)||type!=(int)CliqueType.Religious||!PairValid(x.ReligionId,x.SectId,religions,sects))result.AddError("RELIGIOUS_CLIQUE_INVALID","Religious Clique association is invalid.");
        }
        private static bool PairValid(string religion,string sect,HashSet<string> religions,Dictionary<string,string> sects)=>religions.Contains(religion)&&(string.IsNullOrEmpty(sect)||(sects.TryGetValue(sect,out var parent)&&StringComparer.Ordinal.Equals(parent,religion)));
        private static bool TargetValid(int kind,string id)=>Enum.IsDefined(typeof(ReligionProfileTargetKind),kind)&&!string.IsNullOrWhiteSpace(id);

        private static void ValidateCities(CampaignSaveData subject,ValidationResult result)
        {
            var cityIds=new HashSet<string>(StringComparer.Ordinal);foreach(var city in subject.Cities){if(city==null||string.IsNullOrWhiteSpace(city.CityId)||string.IsNullOrWhiteSpace(city.Name)||!cityIds.Add(city.CityId)||city.PopulationCount<0||!Enum.IsDefined(typeof(CityMetricAssessment),city.Wealth)||!Enum.IsDefined(typeof(CityMetricAssessment),city.Order)||!Enum.IsDefined(typeof(CityMetricAssessment),city.Health)||!Enum.IsDefined(typeof(CityMetricAssessment),city.Security)){result.AddError("CITY_INVALID","City identity or metrics are invalid.");continue;}if(city.Areas==null||city.Infrastructure==null||city.Officials==null){result.AddError("CITY_COLLECTION_INVALID","City child collections are required.");continue;}var areaTypes=new HashSet<int>();var cityBuildingIds=new HashSet<string>(StringComparer.Ordinal);foreach(var area in city.Areas){if(area==null||!Enum.IsDefined(typeof(CityAreaType),area.Type)||!Enum.IsDefined(typeof(CityAreaFullness),area.Fullness)||!areaTypes.Add(area.Type)||area.BuildingPool==null||area.ActiveBuildingIds==null||area.LockedBuildingIds==null){result.AddError("CITY_AREA_INVALID","City area is invalid.");continue;}if(area.Type==(int)CityAreaType.InnerCastle&&area.Fullness!=(int)CityAreaFullness.Full)result.AddError("INNER_CASTLE_NOT_FULL","Inner Castle must remain Full.");var pool=new Dictionary<string,CityBuildingSaveData>(StringComparer.Ordinal);foreach(var b in area.BuildingPool){if(b==null||string.IsNullOrWhiteSpace(b.CityBuildingId)||string.IsNullOrWhiteSpace(b.Name)||!Enum.IsDefined(typeof(CityBuildingKind),b.Kind)||!Enum.IsDefined(typeof(CityAreaType),b.AreaType)||!Enum.IsDefined(typeof(CityBuildingContentStatus),b.Status)||b.AreaType!=area.Type||CityBuildingRules.RequiredArea((CityBuildingKind)b.Kind)!=(CityAreaType)area.Type||pool.ContainsKey(b.CityBuildingId)||cityBuildingIds.Contains(b.CityBuildingId)||b.EffectTags==null){result.AddError("CITY_BUILDING_INVALID","City building definition is invalid.");continue;}pool.Add(b.CityBuildingId,b);cityBuildingIds.Add(b.CityBuildingId);var tags=new HashSet<int>();foreach(var tag in b.EffectTags)if(!Enum.IsDefined(typeof(CityInstitutionEffectTag),tag)||!tags.Add(tag))result.AddError("CITY_BUILDING_EFFECT_TAG_INVALID","City building effect tags are invalid.");}var active=new HashSet<string>(StringComparer.Ordinal);foreach(var id in area.ActiveBuildingIds)if(!active.Add(id)||!pool.TryGetValue(id,out var b)||b.Status==(int)CityBuildingContentStatus.Removed)result.AddError("CITY_ACTIVE_BUILDING_INVALID","Active City building is invalid.");if(area.Fullness==(int)CityAreaFullness.Empty&&active.Count>0)result.AddError("EMPTY_CITY_AREA_ACTIVE_BUILDING","Empty City area cannot have active buildings.");var locked=new HashSet<string>(StringComparer.Ordinal);foreach(var id in area.LockedBuildingIds)if(!locked.Add(id)||!pool.ContainsKey(id)||active.Contains(id))result.AddError("CITY_LOCKED_BUILDING_INVALID","Locked City building is invalid.");}if(areaTypes.Count!=Enum.GetValues(typeof(CityAreaType)).Length)result.AddError("CITY_AREAS_INCOMPLETE","Every authoritative City area is required.");var infrastructure=new HashSet<int>();foreach(var x in city.Infrastructure)if(x==null||!Enum.IsDefined(typeof(CityInfrastructureType),x.Type)||!Enum.IsDefined(typeof(CityInfrastructureCondition),x.Condition)||!infrastructure.Add(x.Type)||(!x.Installed&&x.Condition!=(int)CityInfrastructureCondition.Unassessed))result.AddError("CITY_INFRASTRUCTURE_INVALID","City infrastructure is invalid.");var officialIds=new HashSet<string>(StringComparer.Ordinal);foreach(var x in city.Officials)if(x==null||!Enum.IsDefined(typeof(CityOfficialRole),x.Role)||string.IsNullOrWhiteSpace(x.OrganizationId)||string.IsNullOrWhiteSpace(x.AssignmentId)||!officialIds.Add(x.AssignmentId)||!OfficialValid(subject,city,x))result.AddError("CITY_OFFICIAL_INVALID","City official reference is invalid.");}
        }

        private static bool OfficialValid(CampaignSaveData subject,CitySaveData city,CityOfficialSaveData official)
        {
            OrganizationSaveData? organization=null;foreach(var candidate in subject.Organizations)if(candidate!=null&&StringComparer.Ordinal.Equals(candidate.OrganizationId,official.OrganizationId)){organization=candidate;break;}if(organization==null)return false;AssignmentSaveData? assignment=null;foreach(var candidate in organization.Assignments)if(candidate!=null&&StringComparer.Ordinal.Equals(candidate.AssignmentId,official.AssignmentId)){assignment=candidate;break;}if(assignment==null||assignment.Status!=(int)AssignmentStatus.Active||assignment.TargetKind!=(int)AssignmentTargetKind.City||!StringComparer.Ordinal.Equals(assignment.TargetId,city.CityId)||!StringComparer.Ordinal.Equals(assignment.RoleCode,CityOfficialRoles.KethudaAssignmentRoleCode))return false;foreach(var character in subject.Characters)if(character!=null&&StringComparer.Ordinal.Equals(character.CharacterId,assignment.CharacterId))return character.Death==null&&character.Location.Kind!=(int)CharacterLocationKind.Captivity;return false;
        }

        private static void ValidateSocial(CampaignSaveData subject, ValidationResult result)
        {
            var characterIds = new HashSet<string>(StringComparer.Ordinal);
            var charactersById = new Dictionary<string, CharacterSaveData>(StringComparer.Ordinal);
            foreach (var character in subject.Characters) if (character != null) { characterIds.Add(character.CharacterId); charactersById[character.CharacterId] = character; }
            var organizationIds = new HashSet<string>(StringComparer.Ordinal);
            var assignmentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var organization in subject.Organizations)
            {
                if (organization == null || string.IsNullOrWhiteSpace(organization.OrganizationId) || !organizationIds.Add(organization.OrganizationId) || string.IsNullOrWhiteSpace(organization.Name)) { result.AddError("ORGANIZATION_INVALID", "Organization identity must be unique and complete."); continue; }
                if (organization.Memberships == null || organization.Assignments == null) { result.AddError("ORGANIZATION_COLLECTION_NULL", "Organization collections are required."); continue; }
                var activeMemberships = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in organization.Memberships)
                {
                    if (member == null || !characterIds.Contains(member.CharacterId) || !Enum.IsDefined(typeof(OrganizationBranch), member.Branch) || !Enum.IsDefined(typeof(OrganizationMembershipType), member.MembershipType) || member.StartedAt < 0) result.AddError("ORGANIZATION_MEMBERSHIP_INVALID", "Organization membership is invalid.");
                    else if (member.IsActive && !activeMemberships.Add(member.CharacterId + "\n" + member.Branch)) result.AddError("ORGANIZATION_MEMBERSHIP_DUPLICATE", "Active branch membership is duplicated.");
                }
                var physical = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var assignment in organization.Assignments)
                {
                    if (assignment == null || string.IsNullOrWhiteSpace(assignment.AssignmentId) || !assignmentIds.Add(assignment.AssignmentId) || !characterIds.Contains(assignment.CharacterId) || string.IsNullOrWhiteSpace(assignment.RoleCode) || !Enum.IsDefined(typeof(OrganizationBranch), assignment.Branch) || !Enum.IsDefined(typeof(AssignmentAuthority), assignment.Authority) || !Enum.IsDefined(typeof(AssignmentTargetKind), assignment.TargetKind) || !Enum.IsDefined(typeof(AssignmentPresence), assignment.Presence) || !Enum.IsDefined(typeof(AssignmentStatus), assignment.Status) || assignment.StartedAt < 0) { result.AddError("ASSIGNMENT_INVALID", "Assignment is invalid."); continue; }
                    var world = assignment.TargetKind == (int)AssignmentTargetKind.WorldPosition;
                    if (world == !string.IsNullOrEmpty(assignment.TargetId)) result.AddError("ASSIGNMENT_TARGET_INVALID", "Assignment target payload conflicts with its typed kind.");
                    if (assignment.Status == (int)AssignmentStatus.Active && charactersById[assignment.CharacterId].Death != null) result.AddError("DEAD_ASSIGNMENT_ACTIVE", "Dead Character cannot retain an active assignment.");
                    if (assignment.Status == (int)AssignmentStatus.Active && assignment.Presence == (int)AssignmentPresence.PhysicalPresenceRequired)
                    {
                        var character = charactersById[assignment.CharacterId];
                        if (character.Location.Kind == (int)CharacterLocationKind.Captivity) result.AddError("CAPTIVE_PHYSICAL_ASSIGNMENT", "Captive Character cannot perform a physical assignment.");
                        var target = assignment.TargetKind + "\n" + assignment.TargetId + "\n" + assignment.TargetX + "\n" + assignment.TargetY;
                        if (physical.TryGetValue(assignment.CharacterId, out var existing) && !StringComparer.Ordinal.Equals(existing, target)) result.AddError("PHYSICAL_ASSIGNMENT_CONFLICT", "Character has physically impossible active assignments."); else physical[assignment.CharacterId] = target;
                    }
                }
            }
            var houseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var house in subject.Houses)
            {
                if (house == null || string.IsNullOrWhiteSpace(house.HouseId) || !houseIds.Add(house.HouseId) || string.IsNullOrWhiteSpace(house.Name) || house.Prestige < 0 || house.Wealth < 0 || !Enum.IsDefined(typeof(HouseLifecycle), house.Lifecycle)) { result.AddError("HOUSE_INVALID", "House identity or values are invalid."); continue; }
                if (house.Members == null || house.Marriages == null || house.FamilyLinks == null || house.Properties == null || house.Inheritances == null) { result.AddError("HOUSE_COLLECTION_NULL", "House collections are required."); continue; }
                var members = new HashSet<string>(StringComparer.Ordinal); var activeMembers = new HashSet<string>(StringComparer.Ordinal); foreach (var member in house.Members) if (member == null || !characterIds.Contains(member.CharacterId) || !members.Add(member.CharacterId) || member.JoinedAt < 0) result.AddError("HOUSE_MEMBER_INVALID", "House member is invalid."); else if (member.IsActive) activeMembers.Add(member.CharacterId);
                if (house.Lifecycle == (int)HouseLifecycle.Active && string.IsNullOrEmpty(house.HeadCharacterId) && !house.SuccessionPending) result.AddError("HOUSE_HEAD_OR_SUCCESSION_REQUIRED", "Active House needs a head or succession-pending state.");
                if (!string.IsNullOrEmpty(house.HeadCharacterId) && (!activeMembers.Contains(house.HeadCharacterId) || charactersById[house.HeadCharacterId].Death != null)) result.AddError("HOUSE_HEAD_INVALID", "House head must be a living active member.");
                foreach (var inheritance in house.Inheritances) if (inheritance == null || !Enum.IsDefined(typeof(HousePropertyKind), inheritance.Kind) || !Enum.IsDefined(typeof(InheritanceStatus), inheritance.Status) || ((inheritance.Kind == (int)HousePropertyKind.StateOffice || inheritance.Kind == (int)HousePropertyKind.TimarDirlikServiceGrant) && !string.IsNullOrEmpty(inheritance.HeirCharacterId))) result.AddError("INHERITANCE_SEMANTICS_INVALID", "Inheritance violates asset semantics.");
            }
            var cliqueIds = new HashSet<string>(StringComparer.Ordinal); foreach (var clique in subject.Cliques) if (clique != null && !string.IsNullOrWhiteSpace(clique.CliqueId) && !cliqueIds.Add(clique.CliqueId)) result.AddError("CLIQUE_ID_DUPLICATE", "Clique IDs must be unique.");
            foreach (var clique in subject.Cliques)
            {
                if (clique == null || string.IsNullOrWhiteSpace(clique.CliqueId) || string.IsNullOrWhiteSpace(clique.Name) || !Enum.IsDefined(typeof(CliqueType), clique.Type) || !Enum.IsDefined(typeof(CliqueLifecycle), clique.Lifecycle) || !Enum.IsDefined(typeof(CliqueAttitude), clique.Attitude)) { result.AddError("CLIQUE_INVALID", "Clique identity or type is invalid."); continue; }
                if (!string.IsNullOrEmpty(clique.ParentCliqueId) && !cliqueIds.Contains(clique.ParentCliqueId)) result.AddError("CLIQUE_PARENT_DANGLING", "Clique parent is missing.");
                var members = new HashSet<string>(StringComparer.Ordinal); var activeMembers = new HashSet<string>(StringComparer.Ordinal); foreach (var member in clique.Memberships) if (member == null || !characterIds.Contains(member.CharacterId) || string.IsNullOrWhiteSpace(member.RoleCode) || !members.Add(member.CharacterId)) result.AddError("CLIQUE_MEMBER_INVALID", "Clique member is invalid."); else if (member.IsActive) activeMembers.Add(member.CharacterId);
                if (!string.IsNullOrEmpty(clique.LeaderCharacterId) && (!activeMembers.Contains(clique.LeaderCharacterId) || charactersById[clique.LeaderCharacterId].Death != null)) result.AddError("CLIQUE_LEADER_INVALID", "Clique leader must be a living active member.");
                foreach (var source in clique.InfluenceSources) if (source == null || !members.Contains(source.CharacterId) || !Enum.IsDefined(typeof(InfluenceSourceKind), source.Kind) || source.Contribution < 0 || source.Contribution > 100) result.AddError("CLIQUE_INFLUENCE_SOURCE_INVALID", "Clique influence must come from a valid member source.");
            }
            foreach (var clique in subject.Cliques) if (clique != null && !string.IsNullOrEmpty(clique.ParentCliqueId)) { var parent = subject.Cliques.Find(x => x != null && x.CliqueId == clique.ParentCliqueId); if (parent != null && !string.IsNullOrEmpty(parent.ParentCliqueId)) result.AddError("CLIQUE_HIERARCHY_TOO_DEEP", "Clique hierarchy exceeds parent-to-subclique depth."); }
        }

        private static void ValidateCharacters(CampaignSaveData subject, ValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in subject.Characters)
            {
                if (character == null) { result.AddError("CHARACTER_SAVE_NULL", "Character save entry is required."); continue; }
                if (string.IsNullOrWhiteSpace(character.CharacterId) || !ids.Add(character.CharacterId))
                    result.AddError("CHARACTER_ID_DUPLICATE_OR_INVALID", "Character IDs must be non-empty and unique.");
                if (string.IsNullOrWhiteSpace(character.DisplayName)) result.AddError("CHARACTER_NAME_INVALID", "Character display name is required.");
                if (!Enum.IsDefined(typeof(CharacterIdentityKind), character.IdentityKind)) result.AddError("CHARACTER_KIND_INVALID", "Character identity kind is invalid.");
                if (!Enum.IsDefined(typeof(CharacterProvenance), character.Provenance)) result.AddError("CHARACTER_PROVENANCE_INVALID", "Character provenance is invalid.");
                var isNamed = character.IdentityKind == (int)CharacterIdentityKind.Named;
                if (isNamed != character.Importance.HasValue || (character.Importance.HasValue && !Enum.IsDefined(typeof(CharacterImportance), character.Importance.Value)))
                    result.AddError("CHARACTER_IMPORTANCE_INVALID", "Only named characters require a valid importance class.");
                if ((character.IdentityKind == (int)CharacterIdentityKind.Registered && character.Provenance != (int)CharacterProvenance.Registered) ||
                    (character.IdentityKind == (int)CharacterIdentityKind.Generated && character.Provenance != (int)CharacterProvenance.Generated) ||
                    (isNamed && character.Provenance != (int)CharacterProvenance.Historical && character.Provenance != (int)CharacterProvenance.Registered && character.Provenance != (int)CharacterProvenance.PromotedGenerated))
                    result.AddError("CHARACTER_IDENTITY_PROVENANCE_INVALID", "Character identity kind and provenance conflict.");
                ValidateRange(character.Intelligence, "INTELLIGENCE", result);
                ValidateRange(character.Observation, "OBSERVATION", result);
                ValidateRange(character.Persuasion, "PERSUASION", result);
                ValidateRange(character.Leadership, "LEADERSHIP", result);
                ValidateRange(character.Command, "COMMAND", result);
                ValidateRange(character.Trade, "TRADE", result);
                ValidateRange(character.Administration, "ADMINISTRATION", result);
                ValidateRange(character.Courage, "COURAGE", result);
                ValidateRange(character.Experience, "EXPERIENCE", result);
                ValidateRange(character.BaseLoyalty, "BASE_LOYALTY", result);
                ValidateRange(character.CurrentLoyalty, "CURRENT_LOYALTY", result);
                ValidateRange(character.Satisfaction, "SATISFACTION", result);
                ValidateRange(character.BaseReputation, "BASE_REPUTATION", result);
                ValidateRange(character.CurrentStanding, "CURRENT_STANDING", result);
                ValidateLocation(character.Location, character.Death != null, result);
                ValidateInjury(character.Injury, result);
                ValidateDeath(character.Death, result);
                ValidateHistory(character, result);
            }

            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var relation in subject.CharacterRelations)
            {
                if (relation == null) { result.AddError("CHARACTER_RELATION_NULL", "Character relation entry is required."); continue; }
                if (!ids.Contains(relation.FirstCharacterId) || !ids.Contains(relation.SecondCharacterId))
                    result.AddError("CHARACTER_RELATION_DANGLING", "Character relation endpoint does not exist.");
                if (StringComparer.Ordinal.Compare(relation.FirstCharacterId, relation.SecondCharacterId) >= 0)
                    result.AddError("CHARACTER_RELATION_ORDER_INVALID", "Character relation endpoints must use canonical order.");
                if (relation.Value < CharacterRelationState.MinimumValue || relation.Value > CharacterRelationState.MaximumValue)
                    result.AddError("CHARACTER_RELATION_VALUE_INVALID", "Character relation is outside its valid range.");
                if (!pairs.Add(relation.FirstCharacterId + "\n" + relation.SecondCharacterId))
                    result.AddError("CHARACTER_RELATION_DUPLICATE", "Character relation pair is duplicated.");
            }


            foreach (var character in subject.Characters)
            {
                if (character?.Location != null && !string.IsNullOrWhiteSpace(character.Location.CaptorId))
                {
                    if (string.Equals(character.CharacterId, character.Location.CaptorId, StringComparison.Ordinal))
                        result.AddError("CAPTOR_SELF_REFERENCE", "Character cannot be their own captor.");
                    if (!ids.Contains(character.Location.CaptorId))
                        result.AddError("CAPTOR_DANGLING", "Captor CharacterId does not exist in the roster.");
                }
            }
        }

        private static void ValidateRange(int value, string name, ValidationResult result)
        {
            if (value < CharacterValue.Minimum || value > CharacterValue.Maximum)
                result.AddError("CHARACTER_" + name + "_INVALID", name + " is outside 0-100.");
        }

        private static void ValidateLocation(CharacterLocationSaveData location, bool isDead, ValidationResult result)
        {
            if (location == null) { result.AddError("CHARACTER_LOCATION_NULL", "Character location is required."); return; }
            if (!Enum.IsDefined(typeof(CharacterLocationKind), location.Kind)) { result.AddError("CHARACTER_LOCATION_KIND_INVALID", "Location kind is invalid."); return; }
            var kind = (CharacterLocationKind)location.Kind;
            var hasTarget = !string.IsNullOrWhiteSpace(location.TargetId);
            var hasCaptor = !string.IsNullOrWhiteSpace(location.CaptorId);
            if ((kind == CharacterLocationKind.City || kind == CharacterLocationKind.Army || kind == CharacterLocationKind.Caravan) != hasTarget)
                result.AddError("CHARACTER_LOCATION_TARGET_INVALID", "Typed location target is missing or conflicts with location kind.");
            if ((kind == CharacterLocationKind.Captivity) != hasCaptor)
                result.AddError("CHARACTER_CAPTIVITY_PAYLOAD_INVALID", "Captivity payload is missing or attached to another location kind.");
            if (kind == CharacterLocationKind.Captivity)
            {
                if (!Enum.IsDefined(typeof(CaptivityStatus), location.CaptivityStatus)) result.AddError("CAPTIVITY_STATUS_INVALID", "Captivity status is invalid.");
                if (!Enum.IsDefined(typeof(CaptivitySiteKind), location.CaptivitySiteKind)) result.AddError("CAPTIVITY_SITE_KIND_INVALID", "Captivity site kind is invalid.");
                var siteUsesTarget = location.CaptivitySiteKind != (int)CaptivitySiteKind.WorldPosition;
                if (siteUsesTarget != !string.IsNullOrWhiteSpace(location.CaptivitySiteTargetId)) result.AddError("CAPTIVITY_SITE_TARGET_INVALID", "Captivity site payload conflicts with its kind.");
            }
            else if (!string.IsNullOrEmpty(location.CaptivitySiteTargetId))
            {
                result.AddError("CHARACTER_DUAL_LOCATION_PAYLOAD", "Non-captive location contains a captivity site.");
            }
            if (isDead && (kind == CharacterLocationKind.Travelling || kind == CharacterLocationKind.Captivity))
                result.AddError("DEAD_CHARACTER_ACTIVE", "Dead character cannot travel or remain captive.");
        }

        private static void ValidateInjury(InjurySaveData? injury, ValidationResult result)
        {
            if (injury == null) return;
            if (!Enum.IsDefined(typeof(CharacterInjurySeverity), injury.Severity)) result.AddError("CHARACTER_INJURY_INVALID", "Injury severity is invalid.");
            if (injury.OccurredAt < 0 || (injury.ExpectedRecoveryAt.HasValue && injury.ExpectedRecoveryAt.Value < injury.OccurredAt))
                result.AddError("CHARACTER_INJURY_TIME_INVALID", "Injury time is invalid.");
            if ((injury.Severity == (int)CharacterInjurySeverity.Permanent || injury.Severity == (int)CharacterInjurySeverity.UnfitForDuty) && injury.ExpectedRecoveryAt.HasValue)
                result.AddError("CHARACTER_PERMANENT_INJURY_RECOVERY_INVALID", "Permanent injury cannot have a recovery time.");
        }

        private static void ValidateDeath(DeathSaveData? death, ValidationResult result)
        {
            if (death == null) return;
            if (!Enum.IsDefined(typeof(CharacterDeathCause), death.Cause)) result.AddError("CHARACTER_DEATH_CAUSE_INVALID", "Death cause is invalid.");
            if (death.OccurredAt < 0 || string.IsNullOrWhiteSpace(death.Summary)) result.AddError("CHARACTER_DEATH_INVALID", "Death record is incomplete.");
        }

        private static void ValidateHistory(CharacterSaveData character, ValidationResult result)
        {
            if (character.History == null || character.HistoryCapacity <= 0 || character.History.Count > character.HistoryCapacity)
            {
                result.AddError("CHARACTER_HISTORY_INVALID", "Character history must be bounded by a positive capacity.");
                return;
            }
            long? previous = null;
            long? previousTime = null;
            foreach (var entry in character.History)
            {
                if (entry == null || entry.Sequence < 0 || entry.OccurredAt < 0 || string.IsNullOrWhiteSpace(entry.Summary) ||
                    !Enum.IsDefined(typeof(CharacterHistoryEventKind), entry.Kind) || (previous.HasValue && entry.Sequence <= previous.Value) ||
                    (previousTime.HasValue && entry.OccurredAt < previousTime.Value))
                {
                    result.AddError("CHARACTER_HISTORY_ENTRY_INVALID", "Character history entry is invalid or unstable.");
                    return;
                }
                previous = entry.Sequence;
                previousTime = entry.OccurredAt;
            }
        }
    }
}
