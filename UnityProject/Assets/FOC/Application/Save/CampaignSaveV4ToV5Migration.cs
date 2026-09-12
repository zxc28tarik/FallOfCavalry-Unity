using System;
using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveV4ToV5Migration:ISaveMigration
    {
        public int FromVersion=>4;public int ToVersion=>5;
        public CampaignSaveData Apply(CampaignSaveData source){if(source==null)throw new ArgumentNullException(nameof(source));return new CampaignSaveData{SaveVersion=5,CampaignId=source.CampaignId,GameVersion=source.GameVersion,ContentDataVersion=source.ContentDataVersion,WorldSeed=source.WorldSeed,WorldGenRevision=source.WorldGenRevision,WorldTime=source.WorldTime,RngState=source.RngState,RngDrawCount=source.RngDrawCount,Characters=source.Characters??new List<CharacterSaveData>(),CharacterRelations=source.CharacterRelations??new List<CharacterRelationSaveData>(),Organizations=source.Organizations??new List<OrganizationSaveData>(),Houses=source.Houses??new List<HouseSaveData>(),Cliques=source.Cliques??new List<CliqueSaveData>(),Religions=source.Religions??new List<ReligionDefinitionSaveData>(),Sects=source.Sects??new List<SectDefinitionSaveData>(),CharacterReligions=source.CharacterReligions??new List<CharacterReligionSaveData>(),ReligionProfiles=source.ReligionProfiles??new List<ReligionProfileSaveData>(),ReligionPolicies=source.ReligionPolicies??new List<ReligionPolicySaveData>(),ReligiousCliqueAssociations=source.ReligiousCliqueAssociations??new List<ReligiousCliqueAssociationSaveData>(),Cities=new List<CitySaveData>()};}
    }
}
