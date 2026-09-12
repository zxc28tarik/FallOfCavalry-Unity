using System;
using FOC.Domain.Characters;
using FOC.Domain.Organizations;

namespace FOC.Application.Organizations
{
    public sealed class OrganizationCommandService
    {
        private readonly CharacterRoster _characters;
        private readonly OrganizationRegistry _organizations;
        public OrganizationCommandService(CharacterRoster characters, OrganizationRegistry organizations) { _characters = characters ?? throw new ArgumentNullException(nameof(characters)); _organizations = organizations ?? throw new ArgumentNullException(nameof(organizations)); }
        public void AddMembership(FOC.Domain.Common.OrganizationId id, OrganizationMembershipState membership) { var character = _characters.GetRequired(membership.CharacterId); if (membership.IsActive && character.IsDead) throw new InvalidOperationException("Dead Character cannot receive active membership."); _organizations.GetRequired(id).AddMembership(membership); }
        public void AddAssignment(FOC.Domain.Common.OrganizationId id, AssignmentState assignment) { var character = _characters.GetRequired(assignment.CharacterId); if (assignment.IsActive && character.IsDead) throw new InvalidOperationException("Dead Character cannot receive active assignment."); if (assignment.IsActive && assignment.Presence == AssignmentPresence.PhysicalPresenceRequired && character.Captivity != null) throw new InvalidOperationException("Captive Character cannot receive a physical-presence assignment."); if (assignment.IsActive && assignment.Presence == AssignmentPresence.PhysicalPresenceRequired && !AssignmentFeasibilityRules.IsPhysicalTargetSatisfied(character.Location, assignment.Target)) throw new InvalidOperationException("Physical assignment target conflicts with authoritative CharacterLocation."); _organizations.GetRequired(id).AddAssignment(assignment); }
    }
}
