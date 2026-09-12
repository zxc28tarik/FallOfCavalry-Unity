using System;
using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveV3ToV4Migration : ISaveMigration
    {
        public int FromVersion=>3; public int ToVersion=>4;
        public CampaignSaveData Apply(CampaignSaveData source){if(source==null)throw new ArgumentNullException(nameof(source));return new CampaignSaveData{SaveVersion=4,CampaignId=source.CampaignId,GameVersion=source.GameVersion,ContentDataVersion=source.ContentDataVersion,WorldSeed=source.WorldSeed,WorldGenRevision=source.WorldGenRevision,WorldTime=source.WorldTime,RngState=source.RngState,RngDrawCount=source.RngDrawCount,Characters=source.Characters??new List<CharacterSaveData>(),CharacterRelations=source.CharacterRelations??new List<CharacterRelationSaveData>(),Organizations=source.Organizations??new List<OrganizationSaveData>(),Houses=source.Houses??new List<HouseSaveData>(),Cliques=source.Cliques??new List<CliqueSaveData>()};}
    }
}
