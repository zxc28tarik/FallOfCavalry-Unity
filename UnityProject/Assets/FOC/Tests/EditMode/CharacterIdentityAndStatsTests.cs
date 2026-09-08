using System;
using FOC.Domain.Characters;
using FOC.Domain.Time;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CharacterIdentityAndStatsTests
    {
        [Test]
        public void CharacterId_UsesOrdinalEqualityAndStableOrdering()
        {
            var first = CharacterId.Create("character-a");
            var same = CharacterId.Create("character-a");
            var second = CharacterId.Create("character-b");
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.CompareTo(second), Is.LessThan(0));
            Assert.That(first == same, Is.True);
        }

        [Test]
        public void CharacterId_RejectsEmptyAndDefaultPersistentIdentity()
        {
            Assert.Throws<ArgumentException>(() => CharacterId.Create(" "));
            var invalid = default(CharacterId);
            Assert.That(invalid.IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => { var unused = invalid.Value; });
        }

        [Test]
        public void IdentityDefinitions_RepresentRegisteredNamedAndGeneratedPeople()
        {
            var registered = new RegisteredPerson(CharacterId.Create("registered"), "Registered");
            var named = new NamedCharacter(CharacterId.Create("named"), "Named", CharacterProvenance.Historical);
            var generated = new GeneratedPerson(CharacterId.Create("generated"), "Generated");
            Assert.That(registered.IdentityKind, Is.EqualTo(CharacterIdentityKind.Registered));
            Assert.That(named.IdentityKind, Is.EqualTo(CharacterIdentityKind.Named));
            Assert.That(generated.IdentityKind, Is.EqualTo(CharacterIdentityKind.Generated));
        }

        [Test]
        public void CharacterStats_AcceptInclusiveZeroAndHundredBoundaries()
        {
            var minimum = CharacterTestFactory.Stats(0);
            var maximum = CharacterTestFactory.Stats(100);
            Assert.That(minimum.Intelligence.Value, Is.EqualTo(0));
            Assert.That(maximum.Experience.Value, Is.EqualTo(100));
        }

        [Test]
        public void CharacterStats_RejectValuesBelowZeroAndAboveHundred()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterTestFactory.Stats(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterTestFactory.Stats(101));
        }

        [Test]
        public void LoyaltySatisfactionAndRelation_AreIndependentValues()
        {
            var character = CharacterTestFactory.Named();
            var other = CharacterTestFactory.Named("other");
            var roster = new CharacterRoster();
            roster.Add(character);
            roster.Add(other);
            roster.SetRelation(character.Id, other.Id, -25);
            character.SetCurrentLoyalty(80);
            character.SetSatisfaction(15);
            Assert.That(character.BaseLoyalty.Value, Is.EqualTo(55));
            Assert.That(character.CurrentLoyalty.Value, Is.EqualTo(80));
            Assert.That(character.Satisfaction.Value, Is.EqualTo(15));
            Assert.That(roster.Relations.TryGet(character.Id, other.Id, out var relation), Is.True);
            Assert.That(relation!.Value, Is.EqualTo(-25));
        }

        [Test]
        public void ImportancePromotion_OnlyAllowsAdjacentProgression()
        {
            Assert.That(CharacterImportanceRules.CanPromote(CharacterImportance.D, CharacterImportance.C), Is.True);
            Assert.That(CharacterImportanceRules.CanPromote(CharacterImportance.C, CharacterImportance.B), Is.True);
            Assert.That(CharacterImportanceRules.CanPromote(CharacterImportance.B, CharacterImportance.A), Is.True);
            Assert.That(CharacterImportanceRules.CanPromote(CharacterImportance.D, CharacterImportance.A), Is.False);
            Assert.That(CharacterImportanceRules.CanPromote(CharacterImportance.C, CharacterImportance.A), Is.False);
            var character = CharacterTestFactory.Named(importance: CharacterImportance.D);
            Assert.Throws<InvalidOperationException>(() => CharacterPromotionRules.PromoteImportance(character, CharacterImportance.A, new WorldTimestamp(1)));
        }

        [Test]
        public void ImportancePromotion_DoesNotModifyAnyCharacterStat()
        {
            var character = CharacterTestFactory.Named(importance: CharacterImportance.C);
            var stats = character.Stats;
            CharacterPromotionRules.PromoteImportance(character, CharacterImportance.B, new WorldTimestamp(5));
            Assert.That(character.Stats, Is.SameAs(stats));
            Assert.That(character.Stats.Command.Value, Is.EqualTo(50));
            Assert.That(character.Importance, Is.EqualTo(CharacterImportance.B));
        }
    }
}
