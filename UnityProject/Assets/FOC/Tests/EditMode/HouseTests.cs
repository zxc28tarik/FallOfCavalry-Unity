using System;
using System.Linq;
using FOC.Application.Houses;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Houses;
using FOC.Domain.Organizations;
using FOC.Domain.Cliques;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Domain.Validation;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class HouseTests
    {
        [Test] public void HouseId_IsStableTypedIdentity() { var a = HouseId.Create("h"); var b = HouseId.Create("h"); Assert.That(a, Is.EqualTo(b)); Assert.That(a.Value, Is.EqualTo("h")); }
        [Test] public void HouseHouseholdCliqueAndFaction_AreNotAliased() { Assert.That(typeof(HouseId), Is.Not.EqualTo(typeof(OrganizationId))); Assert.That(typeof(HouseId), Is.Not.EqualTo(typeof(CliqueId))); Assert.That(Enum.GetNames(typeof(OrganizationBranch)), Does.Contain("Household")); Assert.That(Enum.GetNames(typeof(HouseLifecycle)), Does.Not.Contain("Faction")); }
        [Test] public void HeadMustBeRealActiveMember() { var roster = Roster(); var house = House(); house.AddMember(new HouseMember(CharacterId.Create("a"), new WorldTimestamp(1))); house.SetHead(CharacterId.Create("a"), roster); Assert.That(house.HeadId!.Value.Value, Is.EqualTo("a")); }
        [Test] public void DanglingHead_IsRejected() { var house = House(); Assert.Throws<InvalidOperationException>(() => house.SetHead(CharacterId.Create("missing"), Roster())); }
        [Test] public void DeadHead_IsRejected() { var roster = Roster(); var dead = roster.GetRequired(CharacterId.Create("a")); CharacterDeathRules.TryApply(dead, new CharacterDeathContext(CharacterDeathCause.Battle, new WorldTimestamp(2), CharacterLocation.At(default), "Dead."), new ImportanceStoryGuardDeathPolicy(), new SeededRandomSource(2)); var house = House(); house.AddMember(new HouseMember(dead.Id, new WorldTimestamp(1))); Assert.Throws<InvalidOperationException>(() => house.SetHead(dead.Id, roster)); }
        [Test] public void DuplicateHouseMember_IsRejected() { var house = House(); house.AddMember(new HouseMember(CharacterId.Create("a"), new WorldTimestamp(1))); Assert.Throws<InvalidOperationException>(() => house.AddMember(new HouseMember(CharacterId.Create("a"), new WorldTimestamp(2)))); }
        [Test] public void WealthBoundaries_AreDifferentTypes() { Assert.That(typeof(PersonalWealth), Is.Not.EqualTo(typeof(HouseWealth))); Assert.That(typeof(HouseWealth), Is.Not.EqualTo(typeof(StateTreasuryBalance))); Assert.That(new HouseWealth(7).Value, Is.EqualTo(7)); }
        [Test] public void Marriage_DoesNotCreateAlliance() { var link = new MarriageLink(CharacterId.Create("a"), CharacterId.Create("b"), new WorldTimestamp(1)); Assert.That(link.CreatesAlliance, Is.False); Assert.That(link.First.Value, Is.EqualTo("a")); }
        [Test] public void MarriageSelfLink_IsRejected() => Assert.Throws<ArgumentException>(() => new MarriageLink(CharacterId.Create("a"), CharacterId.Create("a"), new WorldTimestamp(1)));
        [Test] public void SuccessionAndInheritance_AreSeparateContracts() { Assert.That(typeof(HouseHead), Is.Not.EqualTo(typeof(InheritanceState))); Assert.That(House().SuccessionPending, Is.False); }
        [Test] public void PrivateProperty_CanUseOrdinaryInheritance() { var state = new InheritanceState("estate", HousePropertyKind.PrivateProperty, CharacterId.Create("a"), InheritanceStatus.Resolved); Assert.That(state.Heir!.Value.Value, Is.EqualTo("a")); Assert.That(state.Status, Is.EqualTo(InheritanceStatus.Resolved)); }
        [TestCase(HousePropertyKind.StateOffice)] [TestCase(HousePropertyKind.TimarDirlikServiceGrant)] public void NonPrivateGrant_CannotPassAutomatically(HousePropertyKind kind) => Assert.Throws<InvalidOperationException>(() => new InheritanceState("grant", kind, CharacterId.Create("a"), InheritanceStatus.Resolved));
        [Test] public void TimarWithoutHeir_RequiresRegrant() { var state = new InheritanceState("timar", HousePropertyKind.TimarDirlikServiceGrant, null, InheritanceStatus.Pending); Assert.That(state.Status, Is.EqualTo(InheritanceStatus.RegrantRequired)); }
        [Test] public void DeadImportantMember_RemainsHistoricallyReferenceable() { var roster = Roster(); var house = House(); house.AddMember(new HouseMember(CharacterId.Create("a"), new WorldTimestamp(1))); CharacterDeathRules.TryApply(roster.GetRequired(CharacterId.Create("a")), new CharacterDeathContext(CharacterDeathCause.Battle, new WorldTimestamp(2), CharacterLocation.At(default), "Dead."), new ImportanceStoryGuardDeathPolicy(), new SeededRandomSource(3)); Assert.That(house.OrderedMembers.Single().CharacterId.Value, Is.EqualTo("a")); }
        [TestCase(HouseLifecycle.Extinguished)] [TestCase(HouseLifecycle.Dissolved)] public void Lifecycle_PreservesIdentityAndMembers(HouseLifecycle lifecycle) { var house = House(); house.AddMember(new HouseMember(CharacterId.Create("a"), new WorldTimestamp(1))); house.SetLifecycle(lifecycle); Assert.That(house.Id.Value, Is.EqualTo("house")); Assert.That(house.OrderedMembers.Count, Is.EqualTo(1)); }
        [Test] public void ActiveHouseWithoutHeadOrSuccession_FailsInvariant() { var roster = Roster(); var houses = new HouseRegistry(); houses.Add(House()); var result = new SocialInvariantValidator().Validate(roster, new OrganizationRegistry(), houses, new CliqueRegistry()); Assert.That(result.Issues.Any(x => x.Code == "HOUSE_HEAD_OR_SUCCESSION_REQUIRED"), Is.True); }
        [Test] public void SuccessionPending_SatisfiesExplicitLifecycleInvariant() { var roster = Roster(); var house = House(); house.MarkSuccessionPending(); var houses = new HouseRegistry(); houses.Add(house); Assert.That(new SocialInvariantValidator().Validate(roster, new OrganizationRegistry(), houses, new CliqueRegistry()).IsValid, Is.True); }
        [Test] public void HouseCommandService_DissolvesWithoutDeleting() { var houses = new HouseRegistry(); houses.Add(House()); new HouseCommandService(Roster(), houses).Dissolve(HouseId.Create("house")); Assert.That(houses.GetRequired(HouseId.Create("house")).Lifecycle, Is.EqualTo(HouseLifecycle.Dissolved)); }

        private static HouseState House() => new HouseState(new HouseDefinition(HouseId.Create("house"), "House"), 3, 10);
        private static CharacterRoster Roster() { var roster = new CharacterRoster(); roster.Add(CharacterTestFactory.Named("a", CharacterImportance.A)); roster.Add(CharacterTestFactory.Named("b")); return roster; }
    }
}
