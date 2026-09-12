using System;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Houses;
using FOC.Domain.Organizations;
using FOC.Domain.Time;

namespace FOC.Application.Save
{
    public static partial class CampaignSaveMapper
    {
        private sealed class SocialRuntime
        {
            public SocialRuntime(OrganizationRegistry organizations, HouseRegistry houses, CliqueRegistry cliques) { Organizations = organizations; Houses = houses; Cliques = cliques; }
            public OrganizationRegistry Organizations { get; }
            public HouseRegistry Houses { get; }
            public CliqueRegistry Cliques { get; }
        }

        private static void AddSocialSaveData(CampaignRuntimeState state, CampaignSaveData data)
        {
            foreach (var organization in state.Organizations.OrderedOrganizations)
            {
                var saved = new OrganizationSaveData { OrganizationId = organization.Id.Value, Name = organization.Name };
                foreach (var membership in organization.Memberships) saved.Memberships.Add(new OrganizationMembershipSaveData { CharacterId = membership.CharacterId.Value, Branch = (int)membership.Branch, MembershipType = (int)membership.MembershipType, StartedAt = membership.StartedAt.Ticks, IsActive = membership.IsActive });
                foreach (var assignment in organization.OrderedAssignments) saved.Assignments.Add(new AssignmentSaveData { AssignmentId = assignment.Id.Value, CharacterId = assignment.CharacterId.Value, Branch = (int)assignment.Branch, RoleCode = assignment.RoleCode, Authority = (int)assignment.Authority, TargetKind = (int)assignment.Target.Kind, TargetId = assignment.Target.TargetId, TargetX = assignment.Target.Position.X, TargetY = assignment.Target.Position.Y, Presence = (int)assignment.Presence, StartedAt = assignment.StartedAt.Ticks, Status = (int)assignment.Status });
                data.Organizations.Add(saved);
            }
            foreach (var house in state.Houses.OrderedHouses)
            {
                var saved = new HouseSaveData { HouseId = house.Id.Value, Name = house.Definition.Name, HeadCharacterId = house.HeadId?.Value ?? string.Empty, SuccessionPending = house.SuccessionPending, Prestige = house.Prestige.Value, Wealth = house.Wealth.Value, Lifecycle = (int)house.Lifecycle };
                foreach (var member in house.OrderedMembers) saved.Members.Add(new HouseMemberSaveData { CharacterId = member.CharacterId.Value, JoinedAt = member.JoinedAt.Ticks, IsActive = member.IsActive });
                foreach (var marriage in house.Marriages) saved.Marriages.Add(new MarriageSaveData { FirstCharacterId = marriage.First.Value, SecondCharacterId = marriage.Second.Value, StartedAt = marriage.StartedAt.Ticks, IsActive = marriage.IsActive });
                foreach (var link in house.FamilyLinks) saved.FamilyLinks.Add(new FamilyLinkSaveData { FirstCharacterId = link.First.Value, SecondCharacterId = link.Second.Value, Kind = (int)link.Kind });
                foreach (var property in house.Properties) saved.Properties.Add(new HousePropertySaveData { AssetId = property.AssetId, Kind = (int)property.Kind });
                foreach (var inheritance in house.Inheritances) saved.Inheritances.Add(new InheritanceSaveData { AssetId = inheritance.AssetId, Kind = (int)inheritance.Kind, HeirCharacterId = inheritance.Heir?.Value ?? string.Empty, Status = (int)inheritance.Status });
                data.Houses.Add(saved);
            }
            foreach (var clique in state.Cliques.OrderedCliques)
            {
                var saved = new CliqueSaveData { CliqueId = clique.Id.Value, Name = clique.Definition.Name, Type = (int)clique.Definition.Type, Lifecycle = (int)clique.Lifecycle, Attitude = (int)clique.Attitude, ParentCliqueId = clique.ParentId?.Value ?? string.Empty, LeaderCharacterId = clique.LeaderId?.Value ?? string.Empty };
                foreach (var member in clique.OrderedMemberships) saved.Memberships.Add(new CliqueMembershipSaveData { CharacterId = member.CharacterId.Value, RoleCode = member.RoleCode, JoinedAt = member.JoinedAt.Ticks, IsActive = member.IsActive });
                foreach (var source in clique.InfluenceSources) saved.InfluenceSources.Add(new CliqueInfluenceSourceSaveData { CharacterId = source.CharacterId.Value, Kind = (int)source.Kind, Contribution = source.Contribution });
                data.Cliques.Add(saved);
            }
        }

        private static SocialRuntime RestoreSocial(CampaignSaveData data, CharacterRoster characters)
        {
            var organizations = new OrganizationRegistry();
            foreach (var saved in data.Organizations)
            {
                var state = new OrganizationState(OrganizationId.Create(saved.OrganizationId), saved.Name);
                foreach (var membership in saved.Memberships) { var item = new OrganizationMembershipState(CharacterId.Create(membership.CharacterId), (OrganizationBranch)membership.Branch, (OrganizationMembershipType)membership.MembershipType, new WorldTimestamp(membership.StartedAt), membership.IsActive); state.AddMembership(item); }
                foreach (var assignment in saved.Assignments) state.AddAssignment(new AssignmentState(AssignmentId.Create(assignment.AssignmentId), CharacterId.Create(assignment.CharacterId), (OrganizationBranch)assignment.Branch, assignment.RoleCode, (AssignmentAuthority)assignment.Authority, RestoreTarget(assignment), (AssignmentPresence)assignment.Presence, new WorldTimestamp(assignment.StartedAt), (AssignmentStatus)assignment.Status));
                organizations.Add(state);
            }
            var houses = new HouseRegistry();
            foreach (var saved in data.Houses)
            {
                var state = new HouseState(new HouseDefinition(HouseId.Create(saved.HouseId), saved.Name), saved.Prestige, saved.Wealth, (HouseLifecycle)saved.Lifecycle);
                foreach (var member in saved.Members) { var item = new HouseMember(CharacterId.Create(member.CharacterId), new WorldTimestamp(member.JoinedAt), member.IsActive); state.AddMember(item); }
                foreach (var marriage in saved.Marriages) state.AddMarriage(new MarriageLink(CharacterId.Create(marriage.FirstCharacterId), CharacterId.Create(marriage.SecondCharacterId), new WorldTimestamp(marriage.StartedAt), marriage.IsActive));
                foreach (var link in saved.FamilyLinks) state.AddFamilyLink(new FamilyLink(CharacterId.Create(link.FirstCharacterId), CharacterId.Create(link.SecondCharacterId), (FamilyLinkKind)link.Kind));
                foreach (var property in saved.Properties) state.AddProperty(new HousePropertyRef(property.AssetId, (HousePropertyKind)property.Kind));
                foreach (var inheritance in saved.Inheritances) state.AddInheritance(new InheritanceState(inheritance.AssetId, (HousePropertyKind)inheritance.Kind, string.IsNullOrEmpty(inheritance.HeirCharacterId) ? (CharacterId?)null : CharacterId.Create(inheritance.HeirCharacterId), (InheritanceStatus)inheritance.Status));
                if (!string.IsNullOrEmpty(saved.HeadCharacterId)) state.SetHead(CharacterId.Create(saved.HeadCharacterId), characters); else if (saved.SuccessionPending) state.MarkSuccessionPending();
                houses.Add(state);
            }
            var cliques = new CliqueRegistry();
            var pending = new System.Collections.Generic.List<CliqueSaveData>(data.Cliques);
            while (pending.Count > 0)
            {
                var progressed = false;
                for (var index = pending.Count - 1; index >= 0; index--)
                {
                    var saved = pending[index];
                    if (!string.IsNullOrEmpty(saved.ParentCliqueId)) { try { cliques.GetRequired(CliqueId.Create(saved.ParentCliqueId)); } catch (System.Collections.Generic.KeyNotFoundException) { continue; } }
                    var state = new CliqueState(new CliqueDefinition(CliqueId.Create(saved.CliqueId), saved.Name, (CliqueType)saved.Type), (CliqueLifecycle)saved.Lifecycle, (CliqueAttitude)saved.Attitude, string.IsNullOrEmpty(saved.ParentCliqueId) ? (CliqueId?)null : CliqueId.Create(saved.ParentCliqueId));
                    foreach (var member in saved.Memberships) state.AddMembership(new CliqueMembership(CharacterId.Create(member.CharacterId), member.RoleCode, new WorldTimestamp(member.JoinedAt), member.IsActive));
                    if (!string.IsNullOrEmpty(saved.LeaderCharacterId)) state.SetLeader(CharacterId.Create(saved.LeaderCharacterId));
                    foreach (var source in saved.InfluenceSources) state.AddInfluenceSource(new CliqueInfluenceSourceState(CharacterId.Create(source.CharacterId), (InfluenceSourceKind)source.Kind, source.Contribution));
                    cliques.Add(state); pending.RemoveAt(index); progressed = true;
                }
                if (!progressed) throw new InvalidOperationException("Clique hierarchy contains a missing parent or cycle.");
            }
            return new SocialRuntime(organizations, houses, cliques);
        }

        private static AssignmentTarget RestoreTarget(AssignmentSaveData saved)
        {
            switch ((AssignmentTargetKind)saved.TargetKind)
            {
                case AssignmentTargetKind.Organization: return AssignmentTarget.Organization(OrganizationId.Create(saved.TargetId));
                case AssignmentTargetKind.City: return AssignmentTarget.City(CityId.Create(saved.TargetId));
                case AssignmentTargetKind.Army: return AssignmentTarget.Army(ArmyId.Create(saved.TargetId));
                case AssignmentTargetKind.Caravan: return AssignmentTarget.Caravan(CaravanId.Create(saved.TargetId));
                case AssignmentTargetKind.House: return AssignmentTarget.House(HouseId.Create(saved.TargetId));
                case AssignmentTargetKind.Clique: return AssignmentTarget.Clique(CliqueId.Create(saved.TargetId));
                case AssignmentTargetKind.WorldPosition: return AssignmentTarget.At(new WorldPosition(saved.TargetX, saved.TargetY));
                default: throw new InvalidOperationException("Unknown assignment target kind.");
            }
        }
    }
}
