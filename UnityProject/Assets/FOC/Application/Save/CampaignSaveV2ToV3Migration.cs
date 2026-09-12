using System;
using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveV2ToV3Migration : ISaveMigration
    {
        public int FromVersion => 2;
        public int ToVersion => 3;
        public CampaignSaveData Apply(CampaignSaveData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CampaignSaveData
            {
                SaveVersion = ToVersion, CampaignId = source.CampaignId, GameVersion = source.GameVersion,
                ContentDataVersion = source.ContentDataVersion, WorldSeed = source.WorldSeed,
                WorldGenRevision = source.WorldGenRevision, WorldTime = source.WorldTime,
                RngState = source.RngState, RngDrawCount = source.RngDrawCount,
                Characters = source.Characters ?? new List<CharacterSaveData>(),
                CharacterRelations = source.CharacterRelations ?? new List<CharacterRelationSaveData>(),
                Organizations = new List<OrganizationSaveData>(), Houses = new List<HouseSaveData>(), Cliques = new List<CliqueSaveData>(),
            };
        }
    }
}
