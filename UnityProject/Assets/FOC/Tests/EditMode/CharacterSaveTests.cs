using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CharacterSaveTests
    {
        [Test]
        public void CharacterCore_SemanticRoundTripPreservesEveryPersistentField()
        {
            var runtime = CreatePopulatedRuntime(reverseInsertion: false);
            var serializer = new CampaignSaveTextSerializer();
            var serialized = serializer.Serialize(CampaignSaveMapper.ToSaveData(runtime));
            var read = serializer.Deserialize(serialized);
            Assert.That(read.Success, Is.True, read.Error);
            Assert.That(new CampaignSaveValidator().Validate(read.Data!).IsValid, Is.True);
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!);
            var promoted = restored.Characters.GetRequired(CharacterId.Create("character-a"));
            Assert.That(promoted.Definition.IdentityKind, Is.EqualTo(CharacterIdentityKind.Named));
            Assert.That(promoted.Definition.Provenance, Is.EqualTo(CharacterProvenance.PromotedGenerated));
            Assert.That(promoted.Importance, Is.EqualTo(CharacterImportance.D));
            Assert.That(promoted.Stats.Intelligence.Value, Is.EqualTo(11));
            Assert.That(promoted.Stats.Observation.Value, Is.EqualTo(22));
            Assert.That(promoted.Stats.Persuasion.Value, Is.EqualTo(33));
            Assert.That(promoted.Stats.Leadership.Value, Is.EqualTo(44));
            Assert.That(promoted.Stats.Command.Value, Is.EqualTo(55));
            Assert.That(promoted.Stats.Trade.Value, Is.EqualTo(66));
            Assert.That(promoted.Stats.Administration.Value, Is.EqualTo(77));
            Assert.That(promoted.Stats.Courage.Value, Is.EqualTo(88));
            Assert.That(promoted.Stats.Experience.Value, Is.EqualTo(99));
            Assert.That(promoted.BaseLoyalty.Value, Is.EqualTo(61));
            Assert.That(promoted.CurrentLoyalty.Value, Is.EqualTo(62));
            Assert.That(promoted.Satisfaction.Value, Is.EqualTo(23));
            Assert.That(promoted.BaseReputation.Value, Is.EqualTo(31));
            Assert.That(promoted.CurrentStanding.Value, Is.EqualTo(44));
            Assert.That(promoted.Location.Kind, Is.EqualTo(CharacterLocationKind.Captivity));
            Assert.That(promoted.Captivity!.CaptorId.Value, Is.EqualTo("character-b"));
            Assert.That(promoted.Injury!.Severity, Is.EqualTo(CharacterInjurySeverity.Serious));
            Assert.That(promoted.History.Entries.Any(x => x.Kind == CharacterHistoryEventKind.PromotedToNamed), Is.True);
            Assert.That(restored.Characters.Relations.TryGet(CharacterId.Create("character-a"), CharacterId.Create("character-b"), out var relation), Is.True);
            Assert.That(relation!.Value, Is.EqualTo(17));
            var dead = restored.Characters.GetRequired(CharacterId.Create("character-c"));
            Assert.That(dead.IsDead, Is.True);
            Assert.That(dead.Death!.Cause, Is.EqualTo(CharacterDeathCause.Battle));
            Assert.That(dead.Location.Kind, Is.EqualTo(CharacterLocationKind.WorldPosition));
            Assert.That(restored.Characters.GetRequired(CharacterId.Create("character-d")).Definition, Is.TypeOf<RegisteredPerson>());
        }

        [Test]
        public void SaveSerialization_IsStableAcrossCharacterInsertionOrder()
        {
            var serializer = new CampaignSaveTextSerializer();
            var first = serializer.Serialize(CampaignSaveMapper.ToSaveData(CreatePopulatedRuntime(false)));
            var second = serializer.Serialize(CampaignSaveMapper.ToSaveData(CreatePopulatedRuntime(true)));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SchemaOneFixture_MigratesDeterministicallyToSchemaTwoWithoutInventedCharacters()
        {
            const string fixture = "FOC_CAMPAIGN_SAVE\nSaveVersion=1\nCampaignId=Y2FtcGFpZ24tYWxwaGE=\nGameVersion=MC4wLjE=\nContentDataVersion=Y29udGVudC0x\nWorldSeed=1648\nWorldGenRevision=1\nWorldTime=987654\nRngState=123456789\nRngDrawCount=42\n";
            var read = new CampaignSaveTextSerializer().Deserialize(fixture);
            Assert.That(read.Success, Is.True, read.Error);
            var pipeline = new SaveMigrationPipeline(new ISaveMigration[] { new CampaignSaveV1ToV2Migration(), new CampaignSaveV2ToV3Migration(), new CampaignSaveV3ToV4Migration(), new CampaignSaveV4ToV5Migration() });
            var first = pipeline.Migrate(read.Data!, CampaignSaveData.CurrentSaveVersion);
            var second = pipeline.Migrate(read.Data!, CampaignSaveData.CurrentSaveVersion);
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(first.Data!.SaveVersion, Is.EqualTo(5));
            Assert.That(first.Data.CampaignId, Is.EqualTo("campaign-alpha"));
            Assert.That(first.Data.Characters.Count, Is.EqualTo(0));
            Assert.That(second.Data!.Characters.Count, Is.EqualTo(first.Data.Characters.Count));
            Assert.Throws<InvalidOperationException>(() => CampaignSaveMapper.ToRuntimeState(read.Data!));
        }

        [Test]
        public void SaveValidator_RejectsInvalidStatsDanglingRelationsAndDualLocationPayload()
        {
            var data = CampaignSaveMapper.ToSaveData(CreatePopulatedRuntime(false));
            data.Characters[0].Intelligence = 101;
            data.Characters[1].Location.CaptivitySiteTargetId = "unexpected-second-place";
            data.CharacterRelations[0].SecondCharacterId = "missing-character";
            var validation = new CampaignSaveValidator().Validate(data);
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Issues.Any(x => x.Code == "CHARACTER_INTELLIGENCE_INVALID"), Is.True);
            Assert.That(validation.Issues.Any(x => x.Code == "CHARACTER_DUAL_LOCATION_PAYLOAD"), Is.True);
            Assert.That(validation.Issues.Any(x => x.Code == "CHARACTER_RELATION_DANGLING"), Is.True);
        }

        [Test]
        public void RuntimeAndSaveCharacterTypes_RemainSeparate()
        {
            Assert.That(typeof(CharacterSaveData).IsAssignableFrom(typeof(CharacterState)), Is.False);
            Assert.That(typeof(CharacterState).IsAssignableFrom(typeof(CharacterSaveData)), Is.False);
        }

        private static CampaignRuntimeState CreatePopulatedRuntime(bool reverseInsertion)
        {
            var roster = new CharacterRoster();
            var generated = new CharacterState(
                new GeneratedPerson(CharacterId.Create("character-a"), "Generated A"),
                new CharacterStats(11, 22, 33, 44, 55, 66, 77, 88, 99),
                CharacterLocation.TravellingAt(new WorldPosition(4, 5)),
                61, 62, 23, 31, 44);
            generated.History.Add(new WorldTimestamp(1), CharacterHistoryEventKind.Created, "Generated.");
            generated.ApplyInjury(new InjuryState(CharacterInjurySeverity.Serious, new WorldTimestamp(2), new WorldTimestamp(12)));
            generated.Capture(new CaptivityState(CharacterId.Create("character-b"), CaptivitySite.InCity(CityId.Create("city-prison"))), new WorldTimestamp(3));
            CharacterPromotionRules.PromoteGeneratedToNamed(generated, new WorldTimestamp(4));
            var captor = CharacterTestFactory.Named("character-b", CharacterImportance.B);
            var dead = CharacterTestFactory.Named("character-c", CharacterImportance.C);
            CharacterDeathRules.TryApply(dead, new CharacterDeathContext(CharacterDeathCause.Battle, new WorldTimestamp(7), CharacterLocation.At(new WorldPosition(20, 30)), "Fell in battle."), new ImportanceStoryGuardDeathPolicy(), new SeededRandomSource(7));
            var registered = new CharacterState(
                new RegisteredPerson(CharacterId.Create("character-d"), "Registered D"),
                CharacterTestFactory.Stats(10),
                CharacterLocation.InCity(CityId.Create("city-record")),
                10, 10, 10, 10, 10);
            var characters = reverseInsertion ? new[] { registered, dead, captor, generated } : new[] { generated, captor, dead, registered };
            foreach (var character in characters) roster.Add(character);
            roster.SetRelation(generated.Id, captor.Id, 17);
            return new CampaignRuntimeState(
                StableId<CampaignTag>.Create("campaign-character-test"),
                "0.1.0",
                "character-core-1",
                1648,
                1,
                new WorldClock(new WorldTimestamp(100)),
                new SeededRandomSource(42),
                roster);
        }
    }
}
