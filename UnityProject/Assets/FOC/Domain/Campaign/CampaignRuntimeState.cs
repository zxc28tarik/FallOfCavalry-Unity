using System;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Domain.Organizations;
using FOC.Domain.Houses;
using FOC.Domain.Cliques;

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
            CharacterRoster? characters = null,
            OrganizationRegistry? organizations = null,
            HouseRegistry? houses = null,
            CliqueRegistry? cliques = null)
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
            Organizations = organizations ?? new OrganizationRegistry();
            Houses = houses ?? new HouseRegistry();
            Cliques = cliques ?? new CliqueRegistry();
        }

        public StableId<CampaignTag> CampaignId { get; }

        public string GameVersion { get; }

        public string ContentDataVersion { get; }

        public ulong WorldSeed { get; }

        public int WorldGenRevision { get; }

        public WorldClock Clock { get; }

        public SeededRandomSource Random { get; }

        public CharacterRoster Characters { get; }

        public OrganizationRegistry Organizations { get; }

        public HouseRegistry Houses { get; }

        public CliqueRegistry Cliques { get; }

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
