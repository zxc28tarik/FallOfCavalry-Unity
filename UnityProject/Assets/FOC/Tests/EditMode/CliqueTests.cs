using System;
using System.Linq;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Houses;
using FOC.Domain.Time;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CliqueTests
    {
        [Test] public void FinalCliqueTypes_AreExact() => Assert.That(Enum.GetNames(typeof(CliqueType)), Is.EqualTo(new[] { "Merchant", "Military", "Religious", "GuildCraft", "UrbanNotables", "PatronageHouseholdCircle", "TribalNomadic" }));
        [Test] public void Ocak_IsNotCliqueType() => Assert.That(Enum.GetNames(typeof(CliqueType)), Does.Not.Contain("Ocak"));
        [Test] public void Character_CanJoinMultipleCliques() { var id = CharacterId.Create("c"); var a = Clique("a"); var b = Clique("b"); a.AddMembership(Member(id)); b.AddMembership(Member(id)); Assert.That(a.OrderedMemberships.Single().CharacterId, Is.EqualTo(b.OrderedMemberships.Single().CharacterId)); }
        [Test] public void Character_CanHaveZeroCliqueMemberships() => Assert.That(Clique("a").OrderedMemberships, Is.Empty);
        [Test] public void Leader_IsOptional() => Assert.That(Clique("a").LeaderId, Is.Null);
        [Test] public void Leader_MustBeMember() => Assert.Throws<InvalidOperationException>(() => Clique("a").SetLeader(CharacterId.Create("missing")));
        [Test] public void ParentToSubclique_IsAllowed() { var registry = new CliqueRegistry(); registry.Add(Clique("parent")); registry.Add(Clique("child", CliqueId.Create("parent"))); Assert.That(registry.Count, Is.EqualTo(2)); }
        [Test] public void ThirdHierarchyLevel_IsRejected() { var registry = new CliqueRegistry(); registry.Add(Clique("root")); registry.Add(Clique("child", CliqueId.Create("root"))); Assert.Throws<InvalidOperationException>(() => registry.Add(Clique("grandchild", CliqueId.Create("child")))); }
        [Test] public void Clique_CannotOwnGoodsSoldiersOrArmy() { var clique = Clique("a"); Assert.That(clique.CanOwnGoods, Is.False); Assert.That(clique.CanOwnSoldiers, Is.False); Assert.That(clique.CanOwnArmy, Is.False); }
        [Test] public void MerchantClique_HasNoInventoryContract() { Assert.That(typeof(CliqueState).GetProperty("Inventory"), Is.Null); Assert.That(typeof(CliqueState).GetProperty("Goods"), Is.Null); Assert.That(typeof(CliqueState).GetProperty("Stock"), Is.Null); }
        [Test] public void MilitaryClique_HasNoSoldierOrArmyCollection() { Assert.That(typeof(CliqueState).GetProperty("Soldiers"), Is.Null); Assert.That(typeof(CliqueState).GetProperty("SoldierIds"), Is.Null); Assert.That(typeof(CliqueState).GetProperty("Army"), Is.Null); }
        [Test] public void MilitaryClique_ProducesNoCombatBonus() { var clique = new CliqueState(new CliqueDefinition(CliqueId.Create("m"), "Military", CliqueType.Military)); Assert.That(clique.CombatBonus, Is.Zero); Assert.That(typeof(CliqueState).GetProperty("MoraleBonus"), Is.Null); Assert.That(typeof(CliqueState).GetProperty("DamageBonus"), Is.Null); }
        [Test] public void ReligiousClique_IsOnlyNetworkType() { Assert.That(typeof(CliqueDefinition).GetProperties().Any(x => x.Name.Contains("Religion") || x.Name.Contains("Sect")), Is.False); Assert.That(new CliqueDefinition(CliqueId.Create("r"), "Religious", CliqueType.Religious).Type, Is.EqualTo(CliqueType.Religious)); }
        [Test] public void CliqueAndHouse_UseDifferentTypedIdentity() => Assert.That(typeof(CliqueId), Is.Not.EqualTo(typeof(HouseId)));
        [Test] public void CliqueHasNoFactionIdentity() => Assert.That(typeof(CliqueState).GetProperties().Any(x => x.Name.Contains("Faction")), Is.False);
        [Test] public void Dissolution_PreservesIdentityAndMembershipHistory() { var clique = Clique("a"); clique.AddMembership(Member(CharacterId.Create("c"))); clique.SetLifecycle(CliqueLifecycle.Dissolved); Assert.That(clique.Id.Value, Is.EqualTo("a")); Assert.That(clique.OrderedMemberships.Count, Is.EqualTo(1)); }
        [Test] public void Memberships_AreDeterministicallyOrdered() { var clique = Clique("a"); clique.AddMembership(Member(CharacterId.Create("z"))); clique.AddMembership(Member(CharacterId.Create("a"))); Assert.That(clique.OrderedMemberships.Select(x => x.CharacterId.Value), Is.EqualTo(new[] { "a", "z" })); }
        [Test] public void Influence_IsSourceDrivenAndBounded() { var clique = Clique("a"); var id = CharacterId.Create("c"); clique.AddMembership(Member(id)); clique.AddInfluenceSource(new CliqueInfluenceSourceState(id, InfluenceSourceKind.MemberOffice, 70)); clique.AddInfluenceSource(new CliqueInfluenceSourceState(id, InfluenceSourceKind.MemberReputation, 60)); Assert.That(clique.Influence, Is.EqualTo(100)); }
        [Test] public void InfluenceWithoutMemberSource_IsRejected() => Assert.Throws<InvalidOperationException>(() => Clique("a").AddInfluenceSource(new CliqueInfluenceSourceState(CharacterId.Create("c"), InfluenceSourceKind.MemberOffice, 10)));
        [TestCase(false, false, false)] [TestCase(true, false, false)] [TestCase(false, true, false)] [TestCase(true, true, true)] public void RebellionReadiness_RequiresPowerAndCause(bool power, bool cause, bool expected) => Assert.That(new RebellionReadiness(power, cause).IsReady, Is.EqualTo(expected));
        [Test] public void PressureFamily_ContainsNoDeepIntrigue() { var names = Enum.GetNames(typeof(CliquePressure)); Assert.That(names, Does.Contain("Protest")); Assert.That(names, Does.Not.Contain("Assassination")); Assert.That(names, Does.Not.Contain("Sabotage")); }

        private static CliqueState Clique(string id, CliqueId? parent = null) => new CliqueState(new CliqueDefinition(CliqueId.Create(id), id, CliqueType.Merchant), parentId: parent);
        private static CliqueMembership Member(CharacterId id) => new CliqueMembership(id, "member", new WorldTimestamp(1));
    }
}
