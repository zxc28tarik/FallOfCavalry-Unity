using System;
using System.Collections.Generic;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class SaveFoundationTests
    {
        [Test]
        public void MinimalSave_RoundTripsEveryMetadataField()
        {
            var source = CreateSaveData();
            var serializer = new CampaignSaveTextSerializer();

            var serialized = serializer.Serialize(source);
            var result = serializer.Deserialize(serialized);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Data, Is.Not.Null);
            AssertSaveDataEqual(source, result.Data!);
        }

        [Test]
        public void RuntimeState_RoundTripPreservesStableIdClockAndRngContinuation()
        {
            var random = new SeededRandomSource(1648);
            random.NextUInt64();
            var runtime = new CampaignRuntimeState(
                StableId<CampaignTag>.Create("campaign-alpha"),
                "0.0.1",
                "core-1",
                1648,
                1,
                new WorldClock(new WorldTimestamp(900)),
                random);

            var restored = CampaignSaveMapper.ToRuntimeState(CampaignSaveMapper.ToSaveData(runtime));

            Assert.That(restored.CampaignId, Is.EqualTo(runtime.CampaignId));
            Assert.That(restored.Clock.Now, Is.EqualTo(runtime.Clock.Now));
            Assert.That(restored.Random.NextUInt64(), Is.EqualTo(runtime.Random.NextUInt64()));
        }

        [Test]
        public void SaveData_IsNotRuntimeState()
        {
            Assert.That(typeof(CampaignSaveData).IsAssignableFrom(typeof(CampaignRuntimeState)), Is.False);
            Assert.That(typeof(CampaignRuntimeState).IsAssignableFrom(typeof(CampaignSaveData)), Is.False);
        }

        [Test]
        public void MigrationChain_AdvancesOneVersionAtATimeDeterministically()
        {
            var source = CreateSaveData();
            source.SaveVersion = 1;
            var pipeline = new SaveMigrationPipeline(new ISaveMigration[]
            {
                new CopyMigration(1, 2, "content-2"),
                new CopyMigration(2, 3, "content-3"),
            });

            var result = pipeline.Migrate(source, 3);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Data!.SaveVersion, Is.EqualTo(3));
            Assert.That(result.Data.ContentDataVersion, Is.EqualTo("content-3"));
            Assert.That(source.SaveVersion, Is.EqualTo(1), "Migration must not mutate its input fixture.");
        }

        [Test]
        public void MissingMigration_FailsExplicitly()
        {
            var source = CreateSaveData();
            var pipeline = new SaveMigrationPipeline(new[] { new CopyMigration(2, 3, "content-3") });

            var result = pipeline.Migrate(source, 3);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("Missing migration from save version 1"));
        }

        private static CampaignSaveData CreateSaveData() => new CampaignSaveData
        {
            SaveVersion = 1,
            CampaignId = "campaign-alpha",
            GameVersion = "0.0.1",
            ContentDataVersion = "content-1",
            WorldSeed = 1648,
            WorldGenRevision = 1,
            WorldTime = 987654,
            RngState = 123456789,
            RngDrawCount = 42,
        };

        private static void AssertSaveDataEqual(CampaignSaveData expected, CampaignSaveData actual)
        {
            Assert.That(actual.SaveVersion, Is.EqualTo(expected.SaveVersion));
            Assert.That(actual.CampaignId, Is.EqualTo(expected.CampaignId));
            Assert.That(actual.GameVersion, Is.EqualTo(expected.GameVersion));
            Assert.That(actual.ContentDataVersion, Is.EqualTo(expected.ContentDataVersion));
            Assert.That(actual.WorldSeed, Is.EqualTo(expected.WorldSeed));
            Assert.That(actual.WorldGenRevision, Is.EqualTo(expected.WorldGenRevision));
            Assert.That(actual.WorldTime, Is.EqualTo(expected.WorldTime));
            Assert.That(actual.RngState, Is.EqualTo(expected.RngState));
            Assert.That(actual.RngDrawCount, Is.EqualTo(expected.RngDrawCount));
        }

        private sealed class CopyMigration : ISaveMigration
        {
            private readonly string _contentVersion;

            public CopyMigration(int fromVersion, int toVersion, string contentVersion)
            {
                FromVersion = fromVersion;
                ToVersion = toVersion;
                _contentVersion = contentVersion;
            }

            public int FromVersion { get; }

            public int ToVersion { get; }

            public CampaignSaveData Apply(CampaignSaveData source) => new CampaignSaveData
            {
                SaveVersion = ToVersion,
                CampaignId = source.CampaignId,
                GameVersion = source.GameVersion,
                ContentDataVersion = _contentVersion,
                WorldSeed = source.WorldSeed,
                WorldGenRevision = source.WorldGenRevision,
                WorldTime = source.WorldTime,
                RngState = source.RngState,
                RngDrawCount = source.RngDrawCount,
            };
        }
    }
}
