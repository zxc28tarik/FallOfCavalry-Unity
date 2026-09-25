using FOC.Application.Proof;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Geography;

namespace FOC.Application.Geography
{
    public static class VerticalSliceCampaignFactory
    {
        public const string ContentVersion = "VERTICAL_SLICE_DEVELOPMENT-geography-1";
        public const string PlayerMarker = "SLICE_PLAYER";
        public static CampaignRuntimeState Create(string locations, string routes)
        {
            var foundation = IntegratedProofCampaignFactory.Create();
            foundation.Characters.Add(new CharacterState(
                new NamedCharacter(CharacterId.Create("slice-player-sipahi"), "Hasan Ağa — " + PlayerMarker, CharacterProvenance.Registered),
                new CharacterStats(50, 55, 48, 52, 58, 42, 45, 62, 40), CharacterLocation.InCity(CityId.Create("city-home")),
                55, 55, 50, 35, 35, CharacterImportance.C));
            var geography = new GeographyCampaignState(new HistoricalGeographyContentLoader().Load(locations, routes));
            return new CampaignRuntimeState(
                StableId<CampaignTag>.Create("vertical-slice-development"), "0.14a-dev", ContentVersion, foundation.WorldSeed, foundation.WorldGenRevision,
                foundation.Clock, foundation.Random, foundation.Characters, foundation.Organizations, foundation.Houses, foundation.Cliques, foundation.Religion,
                foundation.Cities, foundation.Economy, foundation.Diplomacy, foundation.Military, foundation.Soldiers, foundation.Battles,
                foundation.EncounterContracts, foundation.AI, geography);
        }
    }
}
