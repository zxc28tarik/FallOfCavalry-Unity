namespace FOC.Application.Save
{
    public sealed class CampaignSaveData
    {
        public const int CurrentSaveVersion = 1;

        public int SaveVersion { get; set; }

        public string CampaignId { get; set; } = string.Empty;

        public string GameVersion { get; set; } = string.Empty;

        public string ContentDataVersion { get; set; } = string.Empty;

        public ulong WorldSeed { get; set; }

        public int WorldGenRevision { get; set; }

        public long WorldTime { get; set; }

        public ulong RngState { get; set; }

        public ulong RngDrawCount { get; set; }
    }
}

