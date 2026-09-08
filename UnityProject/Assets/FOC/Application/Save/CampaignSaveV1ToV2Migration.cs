using System;
using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveV1ToV2Migration : ISaveMigration
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public CampaignSaveData Apply(CampaignSaveData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CampaignSaveData
            {
                SaveVersion = ToVersion,
                CampaignId = source.CampaignId,
                GameVersion = source.GameVersion,
                ContentDataVersion = source.ContentDataVersion,
                WorldSeed = source.WorldSeed,
                WorldGenRevision = source.WorldGenRevision,
                WorldTime = source.WorldTime,
                RngState = source.RngState,
                RngDrawCount = source.RngDrawCount,
                Characters = new List<CharacterSaveData>(),
                CharacterRelations = new List<CharacterRelationSaveData>(),
            };
        }
    }
}
