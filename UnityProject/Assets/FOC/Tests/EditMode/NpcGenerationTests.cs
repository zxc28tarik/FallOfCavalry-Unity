using System;
using System.Linq;
using FOC.Application.Characters;
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
    public sealed class NpcGenerationTests
    {
        private static NpcGenerationRequest Request(int firstSerial = 1, int count = 12, int commandBias = 16) =>
            new NpcGenerationRequest(
                "edirne-1648",
                firstSerial,
                count,
                new NpcNamePool("ottoman", new[] { "Mehmed", "Ayşe", "Ali", "Fatma" }, new[] { "Çelebi", "Hatun", "Efendi" }),
                new[] {
                    new NpcArchetype("artisan", 5, new int[9]),
                    new NpcArchetype("military", 3, new[] { 0, 0, 0, 8, commandBias, 0, 0, 9, 4 }),
                    new NpcArchetype("merchant", 2, new[] { 0, 4, 8, 0, 0, 17, 0, 0, 3 }),
                },
                CharacterLocation.InCity(CityId.Create("city-edirne")),
                8);

        private static CampaignRuntimeState Campaign(ulong seed)
        {
            return new CampaignRuntimeState(
                StableId<CampaignTag>.Create("campaign-npc-generation"),
                "0.1.0", "test-content", seed, 1,
                new WorldClock(new WorldTimestamp(60)),
                new SeededRandomSource(seed));
        }

        [Test]
        public void Batch_GeneratesRegisteredWorldPeopleNotPlayerOrSoldiers()
        {
            var state = Campaign(1648);
            var generated = new NpcGenerationService(state.Characters, state.Clock, state.Random).Generate(Request());
            Assert.That(generated.Count, Is.EqualTo(12));
            Assert.That(state.Characters.Count, Is.EqualTo(12));
            Assert.That(state.Characters.Relations.Count, Is.EqualTo(0));
            Assert.That(state.Soldiers.Soldiers.OrderedSoldiers.Count, Is.EqualTo(0));
            Assert.That(generated.Select(x => x.Character.Id.Value).Distinct().Count(), Is.EqualTo(12));
            foreach (var item in generated)
            {
                Assert.That(item.Character.Definition.IdentityKind, Is.EqualTo(CharacterIdentityKind.Generated));
                Assert.That(item.Character.Definition.Provenance, Is.EqualTo(CharacterProvenance.Generated));
                Assert.That(item.Character.Importance, Is.Null);
                Assert.That(item.Character.Location.Kind, Is.EqualTo(CharacterLocationKind.City));
                Assert.That(item.Character.Location.CityId!.Value.Value, Is.EqualTo("city-edirne"));
                Assert.That(item.Character.Id.Value, Does.StartWith("npc-edirne-1648-"));
                Assert.That(item.CultureCode, Is.EqualTo("ottoman"));
                Assert.That(item.Character.History.Entries.Single().Kind, Is.EqualTo(CharacterHistoryEventKind.Created));
                Assert.That(new CharacterInvariantValidator().Validate(item.Character).IsValid, Is.True);
            }
        }

        [Test]
        public void IdenticalSeedAndContent_ProduceByteForByteIdenticalCampaignAndRngState()
        {
            var first = Campaign(99);
            var second = Campaign(99);
            new NpcGenerationService(first.Characters, first.Clock, first.Random).Generate(Request());
            new NpcGenerationService(second.Characters, second.Clock, second.Random).Generate(Request());
            var serializer = new CampaignSaveTextSerializer();
            Assert.That(serializer.Serialize(CampaignSaveMapper.ToSaveData(first)),
                Is.EqualTo(serializer.Serialize(CampaignSaveMapper.ToSaveData(second))));
            Assert.That(first.Random.CaptureState(), Is.EqualTo(second.Random.CaptureState()));
        }

        [Test]
        public void NamesAndStats_AreValidAndArchetypeChangesInitialCapabilities()
        {
            var untrained = Campaign(32);
            var trained = Campaign(32);
            var city = CharacterLocation.InCity(CityId.Create("city-edirne"));
            NpcGenerationRequest WithBias(int command) => new NpcGenerationRequest(
                "test", 1, 1,
                new NpcNamePool("test", new[] { "Nuri" }, new[] { "Bey" }),
                new[] { new NpcArchetype("commander", 1, new[] { 0, 0, 0, 0, command, 0, 0, 0, 0 }) },
                city, 3);
            var a = new NpcGenerationService(untrained.Characters, untrained.Clock, untrained.Random)
                .Generate(WithBias(0))[0].Character;
            var b = new NpcGenerationService(trained.Characters, trained.Clock, trained.Random)
                .Generate(WithBias(20))[0].Character;
            Assert.That(a.Definition.DisplayName, Is.EqualTo("Nuri Bey"));
            Assert.That(b.Stats.Command.Value, Is.EqualTo(a.Stats.Command.Value + 20));
            Assert.That(b.Stats.Trade.Value, Is.EqualTo(a.Stats.Trade.Value));
            Assert.That(new[] { b.Stats.Intelligence, b.Stats.Observation, b.Stats.Persuasion,
                b.Stats.Leadership, b.Stats.Command, b.Stats.Trade, b.Stats.Administration,
                b.Stats.Courage, b.Stats.Experience }.All(x => x.Value >= 0 && x.Value <= 100), Is.True);
        }

        [Test]
        public void DuplicateRange_IsRejectedBeforeAnyRandomDrawOrAdditionalCharacter()
        {
            var state = Campaign(34);
            var service = new NpcGenerationService(state.Characters, state.Clock, state.Random);
            service.Generate(Request(1, 3));
            var before = state.Random.CaptureState();
            Assert.Throws<InvalidOperationException>(() => service.Generate(Request(2, 4)));
            Assert.That(state.Characters.Count, Is.EqualTo(3));
            Assert.That(state.Random.CaptureState(), Is.EqualTo(before));
        }

        [Test]
        public void DisjointSerialRanges_CreateDistinctPeopleWithoutDuplicateRelationEdges()
        {
            var state = Campaign(51);
            var service = new NpcGenerationService(state.Characters, state.Clock, state.Random);
            service.Generate(Request(1, 8));
            service.Generate(Request(9, 8));
            Assert.That(state.Characters.Count, Is.EqualTo(16));
            Assert.That(state.Characters.Relations.Count, Is.EqualTo(0));
            Assert.That(state.Characters.Contains(CharacterId.Create("npc-edirne-1648-0000016")), Is.True);
        }

        [Test]
        public void Promotion_PreservesGeneratedIdentityStatsAndCreatedHistory()
        {
            var state = Campaign(52);
            var npc = new NpcGenerationService(state.Characters, state.Clock, state.Random).Generate(Request(1, 1))[0].Character;
            var oldId = npc.Id;
            var oldStats = npc.Stats;
            var oldLocation = npc.Location;
            var oldHistory = npc.History.Entries.Single().Summary;
            var promoted = CharacterPromotionRules.PromoteGeneratedToNamed(npc, state.Clock.Now);
            Assert.That(promoted, Is.SameAs(npc));
            Assert.That(promoted.Id, Is.EqualTo(oldId));
            Assert.That(promoted.Importance, Is.EqualTo(CharacterImportance.D));
            Assert.That(promoted.Stats, Is.SameAs(oldStats));
            Assert.That(promoted.Location, Is.SameAs(oldLocation));
            Assert.That(promoted.History.Entries[0].Summary, Is.EqualTo(oldHistory));
            Assert.That(state.Characters.Count, Is.EqualTo(1));
        }

        [Test]
        public void SaveAndLoad_KeepsAllGeneratedPeopleIdentityStatsAndProvenance()
        {
            var state = Campaign(53);
            new NpcGenerationService(state.Characters, state.Clock, state.Random).Generate(Request(101, 25));
            var serializer = new CampaignSaveTextSerializer();
            var save = serializer.Serialize(CampaignSaveMapper.ToSaveData(state));
            var read = serializer.Deserialize(save);
            Assert.That(read.Success, Is.True, read.Error);
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!);
            Assert.That(restored.Characters.Count, Is.EqualTo(25));
            Assert.That(restored.Characters.Relations.Count, Is.EqualTo(0));
            var original = state.Characters.OrderedCharacters.First();
            var loaded = restored.Characters.GetRequired(original.Id);
            Assert.That(loaded.Definition.DisplayName, Is.EqualTo(original.Definition.DisplayName));
            Assert.That(loaded.Definition.Provenance, Is.EqualTo(CharacterProvenance.Generated));
            Assert.That(loaded.Stats.Command, Is.EqualTo(original.Stats.Command));
            Assert.That(loaded.History.Entries[0].Summary, Is.EqualTo(original.History.Entries[0].Summary));
            Assert.That(serializer.Serialize(CampaignSaveMapper.ToSaveData(restored)), Is.EqualTo(save));
        }

        [Test]
        public void InvalidContent_IsRejectedBeforeGeneration()
        {
            Assert.Throws<ArgumentException>(() => new NpcNamePool("Ottoman", new[] { "Ali" }, new[] { "Efendi" }));
            Assert.Throws<ArgumentException>(() => new NpcNamePool("ottoman", Array.Empty<string>(), new[] { "Efendi" }));
            Assert.Throws<ArgumentException>(() => new NpcNamePool("ottoman", new[] { "Ali\n" }, new[] { "Efendi" }));
            Assert.Throws<ArgumentException>(() => new NpcArchetype("test", 1, new[] { 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcArchetype("test", 0, new int[9]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcGenerationRequest("test", 1, 2049,
                new NpcNamePool("test", new[] { "Ali" }, new[] { "Efendi" }),
                new[] { new NpcArchetype("civilian", 1, new int[9]) }, CharacterLocation.At(new WorldPosition(1, 2))));
            Assert.That(new CharacterRoster().Contains(default(CharacterId)), Is.False);
        }
    }
}
