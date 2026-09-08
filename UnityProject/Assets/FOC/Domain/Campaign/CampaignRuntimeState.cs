using System;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Domain.Campaign
{
    public sealed class CampaignRuntimeState : IDomainState
    {
        public CampaignRuntimeState(
            StableId<CampaignTag> campaignId,
            string gameVersion,
            string contentDataVersion,
            ulong worldSeed,
            int worldGenRevision,
            WorldClock clock,
            SeededRandomSource random,
            CharacterRoster? characters = null)
        {
            if (!campaignId.IsValid)
            {
                throw new ArgumentException("Campaign identifier is invalid.", nameof(campaignId));
            }

            CampaignId = campaignId;
            GameVersion = RequireText(gameVersion, nameof(gameVersion));
            ContentDataVersion = RequireText(contentDataVersion, nameof(contentDataVersion));
            WorldSeed = worldSeed;
            if (worldGenRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldGenRevision));
            }

            WorldGenRevision = worldGenRevision;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Characters = characters ?? new CharacterRoster();
        }

        public StableId<CampaignTag> CampaignId { get; }

        public string GameVersion { get; }

        public string ContentDataVersion { get; }

        public ulong WorldSeed { get; }

        public int WorldGenRevision { get; }

        public WorldClock Clock { get; }

        public SeededRandomSource Random { get; }

        public CharacterRoster Characters { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }
}
