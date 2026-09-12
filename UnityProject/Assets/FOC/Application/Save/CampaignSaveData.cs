using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveData
    {
        public const int CurrentSaveVersion = 4;

        public int SaveVersion { get; set; }

        public string CampaignId { get; set; } = string.Empty;

        public string GameVersion { get; set; } = string.Empty;

        public string ContentDataVersion { get; set; } = string.Empty;

        public ulong WorldSeed { get; set; }

        public int WorldGenRevision { get; set; }

        public long WorldTime { get; set; }

        public ulong RngState { get; set; }

        public ulong RngDrawCount { get; set; }

        public List<CharacterSaveData> Characters { get; set; } = new List<CharacterSaveData>();

        public List<CharacterRelationSaveData> CharacterRelations { get; set; } = new List<CharacterRelationSaveData>();

        public List<OrganizationSaveData> Organizations { get; set; } = new List<OrganizationSaveData>();
        public List<HouseSaveData> Houses { get; set; } = new List<HouseSaveData>();
        public List<CliqueSaveData> Cliques { get; set; } = new List<CliqueSaveData>();
        public List<ReligionDefinitionSaveData> Religions { get; set; } = new List<ReligionDefinitionSaveData>();
        public List<SectDefinitionSaveData> Sects { get; set; } = new List<SectDefinitionSaveData>();
        public List<CharacterReligionSaveData> CharacterReligions { get; set; } = new List<CharacterReligionSaveData>();
        public List<ReligionProfileSaveData> ReligionProfiles { get; set; } = new List<ReligionProfileSaveData>();
        public List<ReligionPolicySaveData> ReligionPolicies { get; set; } = new List<ReligionPolicySaveData>();
        public List<ReligiousCliqueAssociationSaveData> ReligiousCliqueAssociations { get; set; } = new List<ReligiousCliqueAssociationSaveData>();
    }

    public sealed class CharacterSaveData
    {
        public string CharacterId { get; set; } = string.Empty;
        public int IdentityKind { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int Provenance { get; set; }
        public int? Importance { get; set; }
        public int Intelligence { get; set; }
        public int Observation { get; set; }
        public int Persuasion { get; set; }
        public int Leadership { get; set; }
        public int Command { get; set; }
        public int Trade { get; set; }
        public int Administration { get; set; }
        public int Courage { get; set; }
        public int Experience { get; set; }
        public int BaseLoyalty { get; set; }
        public int CurrentLoyalty { get; set; }
        public int Satisfaction { get; set; }
        public int BaseReputation { get; set; }
        public int CurrentStanding { get; set; }
        public CharacterLocationSaveData Location { get; set; } = new CharacterLocationSaveData();
        public InjurySaveData? Injury { get; set; }
        public DeathSaveData? Death { get; set; }
        public int HistoryCapacity { get; set; }
        public List<CharacterHistorySaveData> History { get; set; } = new List<CharacterHistorySaveData>();
    }

    public sealed class CharacterLocationSaveData
    {
        public int Kind { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public long X { get; set; }
        public long Y { get; set; }
        public string CaptorId { get; set; } = string.Empty;
        public int CaptivityStatus { get; set; }
        public int CaptivitySiteKind { get; set; }
        public string CaptivitySiteTargetId { get; set; } = string.Empty;
        public long CaptivitySiteX { get; set; }
        public long CaptivitySiteY { get; set; }
    }

    public sealed class InjurySaveData
    {
        public int Severity { get; set; }
        public long OccurredAt { get; set; }
        public long? ExpectedRecoveryAt { get; set; }
    }

    public sealed class DeathSaveData
    {
        public int Cause { get; set; }
        public long OccurredAt { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class CharacterHistorySaveData
    {
        public long Sequence { get; set; }
        public long OccurredAt { get; set; }
        public int Kind { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class CharacterRelationSaveData
    {
        public string FirstCharacterId { get; set; } = string.Empty;
        public string SecondCharacterId { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public sealed class OrganizationSaveData
    {
        public string OrganizationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<OrganizationMembershipSaveData> Memberships { get; set; } = new List<OrganizationMembershipSaveData>();
        public List<AssignmentSaveData> Assignments { get; set; } = new List<AssignmentSaveData>();
    }
    public sealed class OrganizationMembershipSaveData
    {
        public string CharacterId { get; set; } = string.Empty;
        public int Branch { get; set; }
        public int MembershipType { get; set; }
        public long StartedAt { get; set; }
        public bool IsActive { get; set; }
    }
    public sealed class AssignmentSaveData
    {
        public string AssignmentId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public int Branch { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public int Authority { get; set; }
        public int TargetKind { get; set; }
        public string TargetId { get; set; } = string.Empty;
        public long TargetX { get; set; }
        public long TargetY { get; set; }
        public int Presence { get; set; }
        public long StartedAt { get; set; }
        public int Status { get; set; }
    }
    public sealed class HouseSaveData
    {
        public string HouseId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string HeadCharacterId { get; set; } = string.Empty;
        public bool SuccessionPending { get; set; }
        public int Prestige { get; set; }
        public long Wealth { get; set; }
        public int Lifecycle { get; set; }
        public List<HouseMemberSaveData> Members { get; set; } = new List<HouseMemberSaveData>();
        public List<MarriageSaveData> Marriages { get; set; } = new List<MarriageSaveData>();
        public List<FamilyLinkSaveData> FamilyLinks { get; set; } = new List<FamilyLinkSaveData>();
        public List<HousePropertySaveData> Properties { get; set; } = new List<HousePropertySaveData>();
        public List<InheritanceSaveData> Inheritances { get; set; } = new List<InheritanceSaveData>();
    }
    public sealed class HouseMemberSaveData { public string CharacterId { get; set; } = string.Empty; public long JoinedAt { get; set; } public bool IsActive { get; set; } }
    public sealed class MarriageSaveData { public string FirstCharacterId { get; set; } = string.Empty; public string SecondCharacterId { get; set; } = string.Empty; public long StartedAt { get; set; } public bool IsActive { get; set; } }
    public sealed class FamilyLinkSaveData { public string FirstCharacterId { get; set; } = string.Empty; public string SecondCharacterId { get; set; } = string.Empty; public int Kind { get; set; } }
    public sealed class HousePropertySaveData { public string AssetId { get; set; } = string.Empty; public int Kind { get; set; } }
    public sealed class InheritanceSaveData { public string AssetId { get; set; } = string.Empty; public int Kind { get; set; } public string HeirCharacterId { get; set; } = string.Empty; public int Status { get; set; } }
    public sealed class CliqueSaveData
    {
        public string CliqueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
        public int Lifecycle { get; set; }
        public int Attitude { get; set; }
        public string ParentCliqueId { get; set; } = string.Empty;
        public string LeaderCharacterId { get; set; } = string.Empty;
        public List<CliqueMembershipSaveData> Memberships { get; set; } = new List<CliqueMembershipSaveData>();
        public List<CliqueInfluenceSourceSaveData> InfluenceSources { get; set; } = new List<CliqueInfluenceSourceSaveData>();
    }
    public sealed class CliqueMembershipSaveData { public string CharacterId { get; set; } = string.Empty; public string RoleCode { get; set; } = string.Empty; public long JoinedAt { get; set; } public bool IsActive { get; set; } }
    public sealed class CliqueInfluenceSourceSaveData { public string CharacterId { get; set; } = string.Empty; public int Kind { get; set; } public int Contribution { get; set; } }
    public sealed class ReligionDefinitionSaveData { public string ReligionId { get; set; }=string.Empty; public string Name { get; set; }=string.Empty; public int Status { get; set; } }
    public sealed class SectDefinitionSaveData { public string SectId { get; set; }=string.Empty; public string ParentReligionId { get; set; }=string.Empty; public string Name { get; set; }=string.Empty; public int Status { get; set; } }
    public sealed class CharacterReligionSaveData { public string CharacterId { get; set; }=string.Empty; public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; }
    public sealed class ReligionProfileEntrySaveData { public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; public int RelativePresence { get; set; } }
    public sealed class ReligionProfileSaveData { public int TargetKind { get; set; } public string TargetId { get; set; }=string.Empty; public List<ReligionProfileEntrySaveData> Entries { get; set; }=new List<ReligionProfileEntrySaveData>(); }
    public sealed class ReligionPolicyRuleSaveData { public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; public int Recognition { get; set; } public int Treatment { get; set; } public int Enforcement { get; set; } }
    public sealed class ReligionPolicySaveData { public int TargetKind { get; set; } public string TargetId { get; set; }=string.Empty; public List<ReligionPolicyRuleSaveData> Rules { get; set; }=new List<ReligionPolicyRuleSaveData>(); }
    public sealed class ReligiousCliqueAssociationSaveData { public string CliqueId { get; set; }=string.Empty; public string ReligionId { get; set; }=string.Empty; public string SectId { get; set; }=string.Empty; }
}
