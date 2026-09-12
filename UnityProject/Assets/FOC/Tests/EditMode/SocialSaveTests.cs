using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Houses;
using FOC.Domain.Organizations;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class SocialSaveTests
    {
        [Test]
        public void SchemaThree_SemanticRoundTripPreservesOrganizationHouseCliqueAndCharacterReferences()
        {
            var runtime = CreateRuntime(false);
            var serializer = new CampaignSaveTextSerializer();
            var text = serializer.Serialize(CampaignSaveMapper.ToSaveData(runtime));
            var read = serializer.Deserialize(text);
            Assert.That(read.Success, Is.True, read.Error);
            Assert.That(new CampaignSaveValidator().Validate(read.Data!).IsValid, Is.True);
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!);
            var organization = restored.Organizations.GetRequired(OrganizationId.Create("org-main"));
            var house = restored.Houses.GetRequired(HouseId.Create("house-main"));
            var clique = restored.Cliques.GetRequired(CliqueId.Create("clique-child"));
            Assert.Multiple(() =>
            {
                Assert.That(organization.Memberships.Single().CharacterId.Value, Is.EqualTo("character-a"));
                Assert.That(organization.OrderedAssignments.Single().Authority, Is.EqualTo(AssignmentAuthority.Deputy));
                Assert.That(house.HeadId!.Value.Value, Is.EqualTo("character-a"));
                Assert.That(house.Wealth.Value, Is.EqualTo(900));
                Assert.That(house.Prestige.Value, Is.EqualTo(12));
                Assert.That(house.Properties.Single().Kind, Is.EqualTo(HousePropertyKind.TimarDirlikServiceGrant));
                Assert.That(house.Marriages.Single().CreatesAlliance, Is.False);
                Assert.That(clique.ParentId!.Value.Value, Is.EqualTo("clique-parent"));
                Assert.That(clique.OrderedMemberships.Single().CharacterId.Value, Is.EqualTo("character-b"));
                Assert.That(clique.InfluenceSources.Single().Contribution, Is.EqualTo(25));
                Assert.That(clique.Attitude, Is.EqualTo(CliqueAttitude.Supportive));
            });
        }

        [Test]
        public void SocialSerialization_IsStableAcrossAggregateInsertionOrder()
        {
            var serializer = new CampaignSaveTextSerializer();
            Assert.That(serializer.Serialize(CampaignSaveMapper.ToSaveData(CreateRuntime(false))), Is.EqualTo(serializer.Serialize(CampaignSaveMapper.ToSaveData(CreateRuntime(true)))));
        }

        [Test]
        public void SchemaTwo_MigratesToThreePreservingCharacterIdsWithoutInventingSocialEntities()
        {
            var source = CampaignSaveMapper.ToSaveData(CreateRuntime(false));
            source.SaveVersion = 2;
            source.Organizations.Clear(); source.Houses.Clear(); source.Cliques.Clear();
            var serializer = new CampaignSaveTextSerializer();
            var legacyText = serializer.Serialize(source);
            Assert.That(legacyText, Does.Not.Contain("SocialState="));
            var read = serializer.Deserialize(legacyText);
            var migration = new CampaignSaveV2ToV3Migration().Apply(read.Data!);
            Assert.That(migration.Characters.Select(x => x.CharacterId), Is.EqualTo(new[] { "character-a", "character-b" }));
            Assert.That(migration.Organizations, Is.Empty); Assert.That(migration.Houses, Is.Empty); Assert.That(migration.Cliques, Is.Empty);
        }

        [Test]
        public void HardcodedSchemaTwoFixture_LoadsAndMigratesWithoutLosingCharacterId()
        {
            const string fixture = "FOC_CAMPAIGN_SAVE\nSaveVersion=2\nCampaignId=bGVnYWN5LXYy\nGameVersion=MC4x\nContentDataVersion=Y2hhcmFjdGVyLTE=\nWorldSeed=1648\nWorldGenRevision=1\nWorldTime=10\nRngState=123\nRngDrawCount=2\nCharacterCount=1\nCharacter.0=EGxlZ2FjeS1jaGFyYWN0ZXIBAAAABkxlZ2FjeQMAAAABAwAAAAoAAAAKAAAACgAAAAoAAAAKAAAACgAAAAoAAAAKAAAACgAAAAoAAAAKAAAACgAAAAoAAAAKAAAAAAAAAAljaXR5LWhvbWUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAA\nCharacterRelationCount=0\n";
            var read = new CampaignSaveTextSerializer().Deserialize(fixture);
            Assert.That(read.Success, Is.True, read.Error);
            var migrated = new CampaignSaveV2ToV3Migration().Apply(read.Data!);
            Assert.That(migrated.Characters.Single().CharacterId, Is.EqualTo("legacy-character"));
            Assert.That(migrated.Houses, Is.Empty); Assert.That(migrated.Cliques, Is.Empty); Assert.That(migrated.Organizations, Is.Empty);
        }

        [Test]
        public void SchemaOne_MigratesThroughCompleteChainWithoutInventingPeopleOrSocialEntities()
        {
            const string fixture = "FOC_CAMPAIGN_SAVE\nSaveVersion=1\nCampaignId=Y2FtcGFpZ24tbGVnYWN5\nGameVersion=MC4wLjE=\nContentDataVersion=Y29udGVudC0x\nWorldSeed=1648\nWorldGenRevision=1\nWorldTime=9\nRngState=123\nRngDrawCount=2\n";
            var read = new CampaignSaveTextSerializer().Deserialize(fixture);
            var pipeline = new SaveMigrationPipeline(new ISaveMigration[] { new CampaignSaveV1ToV2Migration(), new CampaignSaveV2ToV3Migration() });
            var migrated = pipeline.Migrate(read.Data!, 3);
            Assert.That(migrated.Success, Is.True, migrated.Error);
            Assert.That(migrated.Data!.Characters, Is.Empty); Assert.That(migrated.Data.Organizations, Is.Empty); Assert.That(migrated.Data.Houses, Is.Empty); Assert.That(migrated.Data.Cliques, Is.Empty);
        }

        [Test]
        public void SaveDataAndRuntimeSocialTypes_RemainSeparate()
        {
            Assert.That(typeof(OrganizationSaveData).IsAssignableFrom(typeof(OrganizationState)), Is.False);
            Assert.That(typeof(HouseSaveData).IsAssignableFrom(typeof(HouseState)), Is.False);
            Assert.That(typeof(CliqueSaveData).IsAssignableFrom(typeof(CliqueState)), Is.False);
        }

        private static CampaignRuntimeState CreateRuntime(bool reverse)
        {
            var characters = new CharacterRoster();
            var a = CharacterTestFactory.Named("character-a", CharacterImportance.A);
            var b = CharacterTestFactory.Named("character-b", CharacterImportance.B);
            if (reverse) { characters.Add(b); characters.Add(a); } else { characters.Add(a); characters.Add(b); }

            var organization = new OrganizationState(OrganizationId.Create("org-main"), "Main Organization");
            organization.AddMembership(new OrganizationMembershipState(a.Id, OrganizationBranch.Household, OrganizationMembershipType.Full, new WorldTimestamp(2)));
            organization.AddAssignment(new AssignmentState(AssignmentId.Create("assignment-a"), a.Id, OrganizationBranch.Settlements, "steward", AssignmentAuthority.Deputy, AssignmentTarget.City(CityId.Create("city-home")), AssignmentPresence.RemoteCapable, new WorldTimestamp(3)));
            var organizations = new OrganizationRegistry(); organizations.Add(organization);

            var house = new HouseState(new HouseDefinition(HouseId.Create("house-main"), "Main House"), 12, 900);
            house.AddMember(new HouseMember(a.Id, new WorldTimestamp(1)));
            house.AddMember(new HouseMember(b.Id, new WorldTimestamp(1)));
            house.SetHead(a.Id, characters);
            house.AddMarriage(new MarriageLink(a.Id, b.Id, new WorldTimestamp(4)));
            house.AddFamilyLink(new FamilyLink(a.Id, b.Id, FamilyLinkKind.Marriage));
            house.AddProperty(new HousePropertyRef("timar-ref", HousePropertyKind.TimarDirlikServiceGrant));
            house.AddInheritance(new InheritanceState("timar-ref", HousePropertyKind.TimarDirlikServiceGrant, null, InheritanceStatus.Pending));
            var houses = new HouseRegistry(); houses.Add(house);

            var parent = new CliqueState(new CliqueDefinition(CliqueId.Create("clique-parent"), "Parent", CliqueType.Merchant));
            parent.AddMembership(new CliqueMembership(a.Id, "patron", new WorldTimestamp(5)));
            var child = new CliqueState(new CliqueDefinition(CliqueId.Create("clique-child"), "Child", CliqueType.UrbanNotables), attitude: CliqueAttitude.Supportive, parentId: parent.Id);
            child.AddMembership(new CliqueMembership(b.Id, "member", new WorldTimestamp(6)));
            child.SetLeader(b.Id);
            child.AddInfluenceSource(new CliqueInfluenceSourceState(b.Id, InfluenceSourceKind.MemberReputation, 25));
            var cliques = new CliqueRegistry(); cliques.Add(parent); cliques.Add(child);
            return new CampaignRuntimeState(StableId<CampaignTag>.Create("campaign-social"), "0.2.0", "social-1", 1648, 2, new WorldClock(new WorldTimestamp(100)), new SeededRandomSource(42), characters, organizations, houses, cliques);
        }
    }
}
