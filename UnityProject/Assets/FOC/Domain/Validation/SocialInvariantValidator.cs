using System.Collections.Generic;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Houses;
using FOC.Domain.Organizations;

namespace FOC.Domain.Validation
{
    public sealed class SocialInvariantValidator
    {
        public ValidationResult Validate(CharacterRoster characters, OrganizationRegistry organizations, HouseRegistry houses, CliqueRegistry cliques)
        {
            var result = new ValidationResult();
            var physicalAssignments = new Dictionary<CharacterId, AssignmentState>();
            foreach (var organization in organizations.OrderedOrganizations)
            {
                foreach (var membership in organization.Memberships)
                {
                    CharacterState character;
                    try { character = characters.GetRequired(membership.CharacterId); }
                    catch (KeyNotFoundException) { result.AddError("ORGANIZATION_MEMBER_DANGLING", "Organization membership references a missing Character."); continue; }
                    if (membership.IsActive && character.IsDead) result.AddError("DEAD_ORGANIZATION_MEMBER_ACTIVE", "Dead Character cannot retain active organization membership.");
                }
                foreach (var assignment in organization.OrderedAssignments)
                {
                    CharacterState character;
                    try { character = characters.GetRequired(assignment.CharacterId); }
                    catch (KeyNotFoundException) { result.AddError("ASSIGNMENT_CHARACTER_DANGLING", "Assignment references a missing Character."); continue; }
                    if (!assignment.IsActive) continue;
                    if (character.IsDead) result.AddError("DEAD_ASSIGNMENT_ACTIVE", "Dead Character cannot retain an active assignment.");
                    if (assignment.Presence == AssignmentPresence.PhysicalPresenceRequired)
                    {
                        if (character.Captivity != null) result.AddError("CAPTIVE_PHYSICAL_ASSIGNMENT", "Captive Character cannot perform a physical-presence assignment.");
                        else if (!AssignmentFeasibilityRules.IsPhysicalTargetSatisfied(character.Location, assignment.Target)) result.AddError("PHYSICAL_ASSIGNMENT_LOCATION_MISMATCH", "Physical assignment conflicts with authoritative CharacterLocation.");
                        if (physicalAssignments.TryGetValue(assignment.CharacterId, out var existing) && !existing.Target.Equals(assignment.Target)) result.AddError("PHYSICAL_ASSIGNMENT_CONFLICT", "Character has physically impossible active assignments.");
                        else physicalAssignments[assignment.CharacterId] = assignment;
                    }
                }
            }
            foreach (var house in houses.OrderedHouses)
            {
                foreach (var member in house.OrderedMembers) { try { characters.GetRequired(member.CharacterId); } catch (KeyNotFoundException) { result.AddError("HOUSE_MEMBER_DANGLING", "House member references a missing Character."); } }
                if (house.HeadId.HasValue)
                {
                    try { if (characters.GetRequired(house.HeadId.Value).IsDead) result.AddError("HOUSE_HEAD_DEAD", "House head must be living."); }
                    catch (KeyNotFoundException) { result.AddError("HOUSE_HEAD_DANGLING", "House head references a missing Character."); }
                }
                else if (house.Lifecycle == HouseLifecycle.Active && !house.SuccessionPending) result.AddError("HOUSE_HEAD_OR_SUCCESSION_REQUIRED", "Active House needs a head or explicit succession-pending state.");
            }
            var cliqueIds = new HashSet<CliqueId>();
            foreach (var clique in cliques.OrderedCliques)
            {
                cliqueIds.Add(clique.Id);
                foreach (var membership in clique.OrderedMemberships) { try { characters.GetRequired(membership.CharacterId); } catch (KeyNotFoundException) { result.AddError("CLIQUE_MEMBER_DANGLING", "Clique membership references a missing Character."); } }
                if (clique.LeaderId.HasValue) { try { if (characters.GetRequired(clique.LeaderId.Value).IsDead) result.AddError("CLIQUE_LEADER_DEAD", "Clique leader must be living."); } catch (KeyNotFoundException) { result.AddError("CLIQUE_LEADER_DANGLING", "Clique leader references a missing Character."); } }
            }
            foreach (var clique in cliques.OrderedCliques) if (clique.ParentId.HasValue && !cliqueIds.Contains(clique.ParentId.Value)) result.AddError("CLIQUE_PARENT_DANGLING", "Clique parent is missing.");
            return result;
        }
    }
}
