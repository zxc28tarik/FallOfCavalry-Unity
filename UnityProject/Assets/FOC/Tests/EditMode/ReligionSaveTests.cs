using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Religion;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class ReligionSaveTests
    {
        [Test] public void SchemaFour_SemanticRoundTripPreservesAllReligionState(){var serializer=new CampaignSaveTextSerializer();var text=serializer.Serialize(CampaignSaveMapper.ToSaveData(CreateRuntime(false)));var read=serializer.Deserialize(text);Assert.That(read.Success,Is.True,read.Error);Assert.That(new CampaignSaveValidator().Validate(read.Data!).IsValid,Is.True);var restored=CampaignSaveMapper.ToRuntimeState(read.Data!);var character=restored.Religion.Characters.GetRequired(CharacterId.Create("character"));Assert.That(character.ReligionId.Value,Is.EqualTo("faith-a"));Assert.That(character.SectId!.Value.Value,Is.EqualTo("sect-a"));Assert.That(restored.Religion.OrderedProfiles.Single().Entries.Count,Is.EqualTo(2));Assert.That(restored.Religion.OrderedPolicies.Single().Rules.Single().Enforcement,Is.EqualTo(ReligionEnforcement.LocalDiscretion));Assert.That(restored.Religion.OrderedCliqueAssociations.Single().CliqueId.Value,Is.EqualTo("religious-clique"));}
        [Test] public void SchemaFourSerialization_IsStableAcrossInsertionOrder(){var s=new CampaignSaveTextSerializer();Assert.That(s.Serialize(CampaignSaveMapper.ToSaveData(CreateRuntime(false))),Is.EqualTo(s.Serialize(CampaignSaveMapper.ToSaveData(CreateRuntime(true)))));}
        [Test] public void HardcodedSchemaThreeFixture_MigratesWithoutInventingReligion(){const string fixture="FOC_CAMPAIGN_SAVE\nSaveVersion=3\nCampaignId=Y2FtcGFpZ24tbGVnYWN5\nGameVersion=MC4wLjE=\nContentDataVersion=Y29udGVudC0z\nWorldSeed=1648\nWorldGenRevision=1\nWorldTime=9\nRngState=123\nRngDrawCount=2\nCharacterCount=0\nCharacterRelationCount=0\nSocialState=AAAAAAAAAAAAAAAA\n";var read=new CampaignSaveTextSerializer().Deserialize(fixture);Assert.That(read.Success,Is.True,read.Error);var migrated=new CampaignSaveV3ToV4Migration().Apply(read.Data!);Assert.That(migrated.SaveVersion,Is.EqualTo(4));Assert.That(migrated.Religions,Is.Empty);Assert.That(migrated.Sects,Is.Empty);Assert.That(migrated.CharacterReligions,Is.Empty);Assert.That(migrated.Organizations,Is.Empty);}
        [Test] public void FullMigrationChain_ReachesFourWithoutInventingContent(){var source=new CampaignSaveData{SaveVersion=1,CampaignId="legacy",GameVersion="0.1",ContentDataVersion="one",WorldSeed=1,WorldGenRevision=0,WorldTime=0,RngState=1};var pipeline=new SaveMigrationPipeline(new ISaveMigration[]{new CampaignSaveV1ToV2Migration(),new CampaignSaveV2ToV3Migration(),new CampaignSaveV3ToV4Migration()});var result=pipeline.Migrate(source,4);Assert.That(result.Success,Is.True,result.Error);Assert.That(result.Data!.SaveVersion,Is.EqualTo(4));Assert.That(result.Data.Religions,Is.Empty);Assert.That(result.Data.Characters,Is.Empty);}
        [Test] public void MigrationThreeToFour_PreservesExistingSocialReferences(){var data=CampaignSaveMapper.ToSaveData(CreateRuntime(false));data.SaveVersion=3;var migrated=new CampaignSaveV3ToV4Migration().Apply(data);Assert.That(migrated.Characters.Single().CharacterId,Is.EqualTo("character"));Assert.That(migrated.Cliques.Single().CliqueId,Is.EqualTo("religious-clique"));Assert.That(migrated.Religions,Is.Empty);}
        [Test] public void SaveDataAndRuntimeReligionTypes_RemainSeparate(){Assert.That(typeof(ReligionDefinitionSaveData).IsAssignableFrom(typeof(ReligionDefinition)),Is.False);Assert.That(typeof(CharacterReligionSaveData).IsAssignableFrom(typeof(CharacterReligionState)),Is.False);Assert.That(typeof(ReligionProfileSaveData).IsAssignableFrom(typeof(ReligionProfile)),Is.False);}
        [Test] public void ValidatorRejectsWrongReligionSectPair(){var data=CampaignSaveMapper.ToSaveData(CreateRuntime(false));data.CharacterReligions.Single().ReligionId="faith-b";Assert.That(new CampaignSaveValidator().Validate(data).IsValid,Is.False);}
        private static CampaignRuntimeState CreateRuntime(bool reverse)
        {
            var roster=new CharacterRoster();roster.Add(CharacterTestFactory.Named("character"));var cliques=new CliqueRegistry();cliques.Add(new CliqueState(new CliqueDefinition(CliqueId.Create("religious-clique"),"Circle",CliqueType.Religious)));
            var definitions=new ReligionRegistry();var a=new ReligionDefinition(ReligionId.Create("faith-a"),"Faith A");var b=new ReligionDefinition(ReligionId.Create("faith-b"),"Faith B");if(reverse){definitions.AddReligion(b);definitions.AddReligion(a);}else{definitions.AddReligion(a);definitions.AddReligion(b);}definitions.AddSect(new SectDefinition(SectId.Create("sect-a"),a.Id,"Sect A"));var characters=new CharacterReligionRegistry();characters.Add(new CharacterReligionState(CharacterId.Create("character"),a.Id,SectId.Create("sect-a")));var religion=new ReligionCampaignState(definitions,characters);
            var profile=new ReligionProfile(ReligionProfileTarget.City(CityId.Create("city")));if(reverse){profile.Add(new ReligionProfileEntry(b.Id,null,2));profile.Add(new ReligionProfileEntry(a.Id,SectId.Create("sect-a"),3));}else{profile.Add(new ReligionProfileEntry(a.Id,SectId.Create("sect-a"),3));profile.Add(new ReligionProfileEntry(b.Id,null,2));}religion.AddProfile(profile);var policy=new ReligionPolicy(ReligionProfileTarget.Region(RegionId.Create("region")));policy.Add(new ReligionPolicyRule(a.Id,null,ReligionRecognition.Recognized,ReligionTreatment.Tolerated,ReligionEnforcement.LocalDiscretion));religion.AddPolicy(policy);religion.AddCliqueAssociation(new ReligiousCliqueAssociation(CliqueId.Create("religious-clique"),a.Id,SectId.Create("sect-a")));
            return new CampaignRuntimeState(StableId<CampaignTag>.Create("campaign"),"0.3","religion-1",10,1,new WorldClock(new WorldTimestamp(0)),new SeededRandomSource(10),roster,null,null,cliques,religion);
        }
    }
}
