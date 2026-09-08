using System;
using System.Collections.Generic;
using System.IO;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class AtomicSaveStoreTests
    {
        private string _directory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "foc-foundation-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void InvalidTemporaryContent_DoesNotReplaceCurrentSave()
        {
            var store = new AtomicFileSaveStore(_directory);
            Assert.That(store.Write("slot", "valid-v1", IsValid).Success, Is.True);

            var failed = store.Write("slot", "corrupt-v2", IsValid);
            var read = store.Read("slot", IsValid);

            Assert.That(failed.Success, Is.False);
            Assert.That(read.Content, Is.EqualTo("valid-v1"));
            Assert.That(File.Exists(Path.Combine(_directory, "slot.focsave.tmp")), Is.False);
        }

        [Test]
        public void Replacement_CreatesBackupOfPreviousValidSave()
        {
            var store = new AtomicFileSaveStore(_directory);
            store.Write("slot", "valid-v1", IsValid);

            var result = store.Write("slot", "valid-v2", IsValid);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "slot.focsave")), Is.EqualTo("valid-v2"));
            Assert.That(File.ReadAllText(Path.Combine(_directory, "slot.focsave.bak")), Is.EqualTo("valid-v1"));
        }

        [Test]
        public void CorruptedCurrentSave_RecoversValidBackup()
        {
            var store = new AtomicFileSaveStore(_directory);
            store.Write("slot", "valid-v1", IsValid);
            store.Write("slot", "valid-v2", IsValid);
            File.WriteAllText(Path.Combine(_directory, "slot.focsave"), "corrupt");

            var result = store.Read("slot", IsValid);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.RecoveredFromBackup, Is.True);
            Assert.That(result.Content, Is.EqualTo("valid-v1"));
        }

        [Test]
        public void PathTraversalSlot_IsRejected()
        {
            var store = new AtomicFileSaveStore(_directory);

            var result = store.Write("../outside", "valid", IsValid);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void CampaignService_IntegratesRuntimeMappingSerializationValidationAndAtomicStore()
        {
            var serializer = new CampaignSaveTextSerializer();
            var service = new CampaignSaveService(
                serializer,
                new AtomicFileSaveStore(_directory),
                new CampaignSaveValidator(),
                new SaveMigrationPipeline(new List<ISaveMigration>()));
            var runtime = new CampaignRuntimeState(
                StableId<CampaignTag>.Create("campaign-integration"),
                "0.0.1",
                "core-1",
                1648,
                1,
                new WorldClock(new WorldTimestamp(7200)),
                new SeededRandomSource(1648));
            runtime.Random.NextUInt64();

            var write = service.Save("integration", runtime);
            var read = service.Load("integration");
            var restored = CampaignSaveMapper.ToRuntimeState(read.Data!);

            Assert.That(write.Success, Is.True, write.Error);
            Assert.That(read.Success, Is.True, read.Error);
            Assert.That(restored.CampaignId, Is.EqualTo(runtime.CampaignId));
            Assert.That(restored.Clock.Now, Is.EqualTo(runtime.Clock.Now));
            Assert.That(restored.Random.NextUInt64(), Is.EqualTo(runtime.Random.NextUInt64()));
        }

        private static bool IsValid(string content) => content.StartsWith("valid", StringComparison.Ordinal);
    }
}
