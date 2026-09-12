using System;
using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveV6ToV7Migration:ISaveMigration
    {
        public int FromVersion=>6;public int ToVersion=>7;
        public CampaignSaveData Apply(CampaignSaveData s){if(s==null)throw new ArgumentNullException(nameof(s));return new CampaignSaveData{SaveVersion=7,CampaignId=s.CampaignId,GameVersion=s.GameVersion,ContentDataVersion=s.ContentDataVersion,WorldSeed=s.WorldSeed,WorldGenRevision=s.WorldGenRevision,WorldTime=s.WorldTime,RngState=s.RngState,RngDrawCount=s.RngDrawCount,Characters=s.Characters??new List<CharacterSaveData>(),CharacterRelations=s.CharacterRelations??new List<CharacterRelationSaveData>(),Organizations=s.Organizations??new List<OrganizationSaveData>(),Houses=s.Houses??new List<HouseSaveData>(),Cliques=s.Cliques??new List<CliqueSaveData>(),Religions=s.Religions??new List<ReligionDefinitionSaveData>(),Sects=s.Sects??new List<SectDefinitionSaveData>(),CharacterReligions=s.CharacterReligions??new List<CharacterReligionSaveData>(),ReligionProfiles=s.ReligionProfiles??new List<ReligionProfileSaveData>(),ReligionPolicies=s.ReligionPolicies??new List<ReligionPolicySaveData>(),ReligiousCliqueAssociations=s.ReligiousCliqueAssociations??new List<ReligiousCliqueAssociationSaveData>(),Cities=s.Cities??new List<CitySaveData>(),TradeGoods=s.TradeGoods??new List<TradeGoodSaveData>(),ProductionRecipes=s.ProductionRecipes??new List<ProductionRecipeSaveData>(),CityMarkets=s.CityMarkets??new List<CityMarketSaveData>(),Caravans=s.Caravans??new List<CaravanSaveData>(),DiplomaticActors=new List<DiplomaticActorSaveData>(),DiplomaticRelations=new List<DiplomaticRelationSaveData>(),EnvoyMissions=new List<EnvoyMissionSaveData>(),DiplomaticMessages=new List<DiplomaticMessageSaveData>(),DiplomaticActions=new List<DiplomaticActionSaveData>(),Reports=new List<ReportSaveData>(),ActorInformation=new List<ActorInformationSaveData>(),DiplomaticAgreements=new List<DiplomaticAgreementSaveData>()};}
    }
}
