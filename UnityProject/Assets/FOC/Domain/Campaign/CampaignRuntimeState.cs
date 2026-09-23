using System;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Domain.Organizations;
using FOC.Domain.Houses;
using FOC.Domain.Cliques;
using FOC.Domain.Religion;
using FOC.Domain.Cities;
using FOC.Domain.Economy;
using FOC.Domain.Diplomacy;
using FOC.Domain.Military;
using FOC.Domain.Soldiers;
using FOC.Domain.Battle;
using FOC.Domain.EncountersContracts;

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
            CliqueRegistry? cliques = null,
            ReligionCampaignState? religion = null,
            CityRegistry? cities = null,
            EconomyState? economy = null,
            DiplomacyState? diplomacy = null,
            CampaignMilitaryState? military = null,
            SoldierCampaignState? soldiers = null,
            BattleCampaignState? battles = null,
            EncounterContractCampaignState? encounterContracts = null)
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
            Religion = religion ?? new ReligionCampaignState();
            Cities = cities ?? new CityRegistry();
            Economy = economy ?? new EconomyState();
            Diplomacy = diplomacy ?? new DiplomacyState();
            Military = military ?? new CampaignMilitaryState();
            Soldiers = soldiers ?? new SoldierCampaignState();
            Battles = battles ?? new BattleCampaignState();
            EncounterContracts = encounterContracts ?? new EncounterContractCampaignState();
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

        public ReligionCampaignState Religion { get; }

        public CityRegistry Cities { get; }

        public EconomyState Economy { get; }

        public DiplomacyState Diplomacy { get; }

        public CampaignMilitaryState Military { get; }

        public SoldierCampaignState Soldiers { get; }

        public BattleCampaignState Battles { get; }

        public EncounterContractCampaignState EncounterContracts { get; }

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
