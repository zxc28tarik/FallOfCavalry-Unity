using System;
using FOC.Domain.Characters;
using FOC.Domain.Random;
using FOC.Domain.Time;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CharacterLocationLifecycleTests
    {
        [Test]
        public void LocationContract_ExposesTypedTargetsInsteadOfRawTargetString()
        {
            Assert.That(typeof(CharacterLocation).GetProperty("TargetId"), Is.Null);
            Assert.That(typeof(CharacterLocation).GetProperty("CityId"), Is.Not.Null);
            Assert.That(typeof(CharacterLocation).GetProperty("ArmyId"), Is.Not.Null);
            Assert.That(typeof(CharacterLocation).GetProperty("CaravanId"), Is.Not.Null);
        }

        [Test]
        public void Moving_ReplacesTheSingleAuthoritativeLocation()
        {
            var character = CharacterTestFactory.Named();
            character.MoveTo(CharacterLocation.WithArmy(ArmyId.Create("army-1")), new WorldTimestamp(1));
            Assert.That(character.Location.Kind, Is.EqualTo(CharacterLocationKind.Army));
            Assert.That(character.Location.ArmyId!.Value.Value, Is.EqualTo("army-1"));
            character.MoveTo(CharacterLocation.TravellingAt(new WorldPosition(10, 20)), new WorldTimestamp(2));
            Assert.That(character.Location.Kind, Is.EqualTo(CharacterLocationKind.Travelling));
            Assert.That(character.Location.ArmyId.HasValue, Is.False);
            Assert.That(character.Location.Position, Is.EqualTo(new WorldPosition(10, 20)));
        }

        [Test]
        public void Captivity_IsASeparateLifecycleOutcomeAndOwnsItsLocationPayload()
        {
            var character = CharacterTestFactory.Named();
            var captor = CharacterId.Create("captor");
            character.Capture(new CaptivityState(captor, CaptivitySite.InCity(CityId.Create("city-prison"))), new WorldTimestamp(4));
            Assert.That(character.LifeStatus, Is.EqualTo(CharacterLifeStatus.Alive));
            Assert.That(character.Location.Kind, Is.EqualTo(CharacterLocationKind.Captivity));
            Assert.That(character.Captivity!.CaptorId, Is.EqualTo(captor));
            Assert.That(character.Captivity.Site.Kind, Is.EqualTo(CaptivitySiteKind.City));
        }

        [Test]
        public void Injury_RepresentsLightSeriousPermanentAndUnfitStates()
        {
            Assert.That(new InjuryState(CharacterInjurySeverity.Light, new WorldTimestamp(1), new WorldTimestamp(2)).Severity, Is.EqualTo(CharacterInjurySeverity.Light));
            Assert.That(new InjuryState(CharacterInjurySeverity.Serious, new WorldTimestamp(1), new WorldTimestamp(5)).Severity, Is.EqualTo(CharacterInjurySeverity.Serious));
            Assert.That(new InjuryState(CharacterInjurySeverity.Permanent, new WorldTimestamp(1), null).Severity, Is.EqualTo(CharacterInjurySeverity.Permanent));
            Assert.That(new InjuryState(CharacterInjurySeverity.UnfitForDuty, new WorldTimestamp(1), null).Severity, Is.EqualTo(CharacterInjurySeverity.UnfitForDuty));
        }

        [Test]
        public void PermanentInjury_RejectsRecoveryTimestamp()
        {
            Assert.Throws<ArgumentException>(() => new InjuryState(CharacterInjurySeverity.Permanent, new WorldTimestamp(1), new WorldTimestamp(2)));
        }

        [Test]
        public void Death_IsTerminalButIdentityAndHistoryRemainAddressable()
        {
            var character = CharacterTestFactory.Named(importance: CharacterImportance.C);
            var id = character.Id;
            var result = CharacterDeathRules.TryApply(
                character,
                new CharacterDeathContext(CharacterDeathCause.Battle, new WorldTimestamp(8), CharacterLocation.At(new WorldPosition(8, 9)), "Died after battle."),
                new ImportanceStoryGuardDeathPolicy(),
                new SeededRandomSource(1648));
            Assert.That(result.Allowed, Is.True);
            Assert.That(character.IsDead, Is.True);
            Assert.That(character.Id, Is.EqualTo(id));
            Assert.That(character.History.Entries[character.History.Entries.Count - 1].Kind, Is.EqualTo(CharacterHistoryEventKind.Died));
            Assert.Throws<InvalidOperationException>(() => character.MoveTo(CharacterLocation.TravellingAt(new WorldPosition(9, 10)), new WorldTimestamp(9)));
            Assert.Throws<InvalidOperationException>(() => character.SetCurrentLoyalty(1));
            Assert.Throws<InvalidOperationException>(() => character.SetSatisfaction(1));
            Assert.Throws<InvalidOperationException>(() => character.ClearInjury());
            Assert.That(CharacterDeathRules.TryApply(character, new CharacterDeathContext(CharacterDeathCause.Battle, new WorldTimestamp(10), CharacterLocation.At(new WorldPosition(8, 9)), "Again."), new ImportanceStoryGuardDeathPolicy(), new SeededRandomSource(1)).Allowed, Is.False);
        }

        [Test]
        public void Death_RejectsActiveTravellingFinalLocation()
        {
            var character = CharacterTestFactory.Named(importance: CharacterImportance.C);
            Assert.Throws<ArgumentException>(() => CharacterDeathRules.TryApply(
                character,
                new CharacterDeathContext(CharacterDeathCause.Battle, new WorldTimestamp(8), CharacterLocation.TravellingAt(new WorldPosition(8, 9)), "Invalid."),
                new ImportanceStoryGuardDeathPolicy(),
                new SeededRandomSource(1)));
        }

        [Test]
        public void StoryGuard_DeterministicallyBlocksCheapRandomDeathForAAndB()
        {
            foreach (var importance in new[] { CharacterImportance.A, CharacterImportance.B })
            {
                var first = CharacterTestFactory.Named("first-" + importance, importance);
                var second = CharacterTestFactory.Named("second-" + importance, importance);
                var firstRandom = new SeededRandomSource(99);
                var secondRandom = new SeededRandomSource(99);
                var context = new CharacterDeathContext(CharacterDeathCause.ArbitraryRandom, new WorldTimestamp(3), CharacterLocation.At(new WorldPosition(1, 1)), "Cheap random event.");
                var firstResult = CharacterDeathRules.TryApply(first, context, new ImportanceStoryGuardDeathPolicy(), firstRandom);
                var secondResult = CharacterDeathRules.TryApply(second, context, new ImportanceStoryGuardDeathPolicy(), secondRandom);
                Assert.That(firstResult.Allowed, Is.False);
                Assert.That(firstResult.PreferredAlternative, Is.EqualTo(CharacterProtectedOutcomePreference.InjuryOrCaptivity));
                Assert.That(secondResult.Allowed, Is.EqualTo(firstResult.Allowed));
                Assert.That(first.IsDead, Is.False);
                Assert.That(firstRandom.CaptureState(), Is.EqualTo(secondRandom.CaptureState()));
            }
        }

        [Test]
        public void CharacterHistory_RemainsBoundedAndDeterministicallyOrdered()
        {
            var history = new CharacterHistory(2);
            history.Add(new WorldTimestamp(1), CharacterHistoryEventKind.Created, "one");
            history.Add(new WorldTimestamp(2), CharacterHistoryEventKind.Moved, "two");
            history.Add(new WorldTimestamp(3), CharacterHistoryEventKind.Injured, "three");
            Assert.That(history.Entries.Count, Is.EqualTo(2));
            Assert.That(history.Entries[0].Sequence, Is.EqualTo(1));
            Assert.That(history.Entries[1].Sequence, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => history.Add(new WorldTimestamp(2), CharacterHistoryEventKind.Moved, "backwards"));
        }


        [Test]
        public void StoryGuard_DoesNotMakeProtectedCharacterUniversallyImmortal()
        {
            var character = CharacterTestFactory.Named("story-authorized-a", CharacterImportance.A);
            var result = CharacterDeathRules.TryApply(
                character,
                new CharacterDeathContext(CharacterDeathCause.StoryAuthorized, new WorldTimestamp(5), CharacterLocation.At(new WorldPosition(2, 3)), "Authorized consequence."),
                new ImportanceStoryGuardDeathPolicy(),
                new SeededRandomSource(4));
            Assert.That(result.Allowed, Is.True);
            Assert.That(character.IsDead, Is.True);
        }
    }
}
