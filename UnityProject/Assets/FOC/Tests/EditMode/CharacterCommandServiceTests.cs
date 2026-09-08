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
    public sealed class CharacterCommandServiceTests
    {
        [Test]
        public void SameSeedStateAndCommands_ProduceIdenticalCharacterOutcome()
        {
            var first = RunDeterministicCommands(1648);
            var second = RunDeterministicCommands(1648);
            var serializer = new CampaignSaveTextSerializer();
            Assert.That(
                serializer.Serialize(CampaignSaveMapper.ToSaveData(second)),
                Is.EqualTo(serializer.Serialize(CampaignSaveMapper.ToSaveData(first))));
        }

        [Test]
        public void CommandService_UsesStoryGuardWithoutHardcodingDeathIntoState()
        {
            var roster = new CharacterRoster();
            var protectedCharacter = CharacterTestFactory.Named("protected-a", CharacterImportance.A);
            roster.Add(protectedCharacter);
            var service = new CharacterCommandService(roster, new WorldClock(new WorldTimestamp(10)), new SeededRandomSource(5), new ImportanceStoryGuardDeathPolicy());
            var result = service.Kill(protectedCharacter.Id, CharacterDeathCause.ArbitraryRandom, CharacterLocation.At(new WorldPosition(1, 2)), "Minor random event.");
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("story guard"));
            Assert.That(protectedCharacter.IsDead, Is.False);
        }

        private static CampaignRuntimeState RunDeterministicCommands(ulong seed)
        {
            var roster = new CharacterRoster();
            var generated = CharacterTestFactory.Generated("generated-command");
            var other = CharacterTestFactory.Named("other-command", CharacterImportance.C);
            roster.Add(other);
            roster.Add(generated);
            var clock = new WorldClock(new WorldTimestamp(20));
            var random = new SeededRandomSource(seed);
            var service = new CharacterCommandService(roster, clock, random, new ImportanceStoryGuardDeathPolicy());
            service.SetRelation(generated.Id, other.Id, 14);
            service.Move(generated.Id, CharacterLocation.TravellingAt(new WorldPosition(7, 9)));
            service.PromoteGenerated(generated.Id);
            return new CampaignRuntimeState(
                StableId<CampaignTag>.Create("deterministic-character-commands"),
                "0.1.0",
                "character-core-1",
                seed,
                1,
                clock,
                random,
                roster);
        }
    }
}
