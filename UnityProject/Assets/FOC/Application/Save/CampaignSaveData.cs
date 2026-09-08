using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveData
    {
        public const int CurrentSaveVersion = 2;

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
}
