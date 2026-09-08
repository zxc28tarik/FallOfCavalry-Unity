using System;
using System.Linq;
using FOC.Domain.Characters;
using FOC.Domain.Time;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CharacterRelationsAndPromotionTests
    {
        [Test]
        public void SparseRelations_AreCreatedOnlyByExplicitInteraction()
        {
            var roster = CreateThreeCharacterRoster();
            Assert.That(roster.Relations.Count, Is.EqualTo(0));
            roster.SetRelation(CharacterId.Create("a"), CharacterId.Create("c"), 12);
            Assert.That(roster.Relations.Count, Is.EqualTo(1));
            Assert.That(roster.Relations.TryGet(CharacterId.Create("a"), CharacterId.Create("b"), out _), Is.False);
        }

        [Test]
        public void RelationPair_IsCanonicalRegardlessOfInputOrder()
        {
            var roster = CreateThreeCharacterRoster();
            roster.SetRelation(CharacterId.Create("c"), CharacterId.Create("a"), 20);
            var relation = roster.Relations.OrderedRelations.Single();
            Assert.That(relation.Key.First.Value, Is.EqualTo("a"));
            Assert.That(relation.Key.Second.Value, Is.EqualTo("c"));
        }

        [Test]
        public void RelationValue_RejectsValuesOutsideMinusHundredToHundred()
        {
            var roster = CreateThreeCharacterRoster();
            Assert.Throws<ArgumentOutOfRangeException>(() => roster.SetRelation(CharacterId.Create("a"), CharacterId.Create("b"), 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => roster.SetRelation(CharacterId.Create("a"), CharacterId.Create("b"), -101));
        }

        [Test]
        public void GeneratedPromotion_PreservesSameObjectIdHistoryLocationMetricsInjuryCaptivityAndRelation()
        {
            var generated = CharacterTestFactory.Generated("generated-a");
            var other = CharacterTestFactory.Named("named-b");
            var roster = new CharacterRoster();
            roster.Add(generated);
            roster.Add(other);
            roster.SetRelation(generated.Id, other.Id, 37);
            generated.History.Add(new WorldTimestamp(1), CharacterHistoryEventKind.Created, "Generated at a city.");
            generated.SetCurrentLoyalty(72);
            generated.SetSatisfaction(28);
            generated.ApplyInjury(new InjuryState(CharacterInjurySeverity.Serious, new WorldTimestamp(2), new WorldTimestamp(12)));
            generated.Capture(new CaptivityState(other.Id, CaptivitySite.InCity(CityId.Create("city-prison"))), new WorldTimestamp(3));
            var id = generated.Id;
            var historyBefore = generated.History.Entries.Count;
            var location = generated.Location;
            var injury = generated.Injury;
            var captivity = generated.Captivity;

            var promoted = CharacterPromotionRules.PromoteGeneratedToNamed(generated, new WorldTimestamp(4));

            Assert.That(promoted, Is.SameAs(generated), "Promotion must not create a replacement character state.");
            Assert.That(promoted.Id, Is.EqualTo(id));
            Assert.That(promoted.Definition, Is.TypeOf<NamedCharacter>());
            Assert.That(promoted.Definition.Provenance, Is.EqualTo(CharacterProvenance.PromotedGenerated));
            Assert.That(promoted.Importance, Is.EqualTo(CharacterImportance.D));
            Assert.That(promoted.History.Entries.Count, Is.EqualTo(historyBefore + 1));
            Assert.That(promoted.Location, Is.SameAs(location));
            Assert.That(promoted.CurrentLoyalty.Value, Is.EqualTo(72));
            Assert.That(promoted.Satisfaction.Value, Is.EqualTo(28));
            Assert.That(promoted.Injury, Is.SameAs(injury));
            Assert.That(promoted.Captivity, Is.SameAs(captivity));
            Assert.That(roster.Relations.TryGet(id, other.Id, out var relation), Is.True);
            Assert.That(relation!.Value, Is.EqualTo(37));
        }

        [Test]
        public void GeneratedPromotion_CannotRunTwice()
        {
            var character = CharacterTestFactory.Generated();
            CharacterPromotionRules.PromoteGeneratedToNamed(character, new WorldTimestamp(1));
            Assert.Throws<InvalidOperationException>(() => CharacterPromotionRules.PromoteGeneratedToNamed(character, new WorldTimestamp(2)));
        }

        private static CharacterRoster CreateThreeCharacterRoster()
        {
            var roster = new CharacterRoster();
            roster.Add(CharacterTestFactory.Named("c"));
            roster.Add(CharacterTestFactory.Named("a"));
            roster.Add(CharacterTestFactory.Named("b"));
            return roster;
        }
    }
}
