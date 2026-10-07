using System;
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
    public sealed class SaveValidationAllocationTests
    {
        private string _directory = string.Empty;
        [SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "foc-y0-save-validation", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

        [Test]
        public void IdenticalDiskGenerations_DecodeOncePerSaveButNotAcrossSaves()
        {
            var serializer = new CountingSerializer();
            var service = Service(serializer, new AtomicFileSaveStore(_directory));
            var runtime = Runtime();
            Assert.That(service.Save("slot", runtime).Success, Is.True);
            Assert.That(serializer.ReadCount, Is.EqualTo(1), "Temp and final reads must both validate, but equal bytes share decoding.");
            Assert.That(service.Save("slot", runtime).Success, Is.True);
            Assert.That(serializer.ReadCount, Is.EqualTo(2), "No validation state may survive between operations.");
            runtime.Clock.Advance(new WorldDuration(7));
            Assert.That(service.Save("slot", runtime).Success, Is.True);
            Assert.That(serializer.ReadCount, Is.EqualTo(4), "The previous generation differs and must be independently decoded.");
        }

        [Test]
        public void Load_ReusesValidatedBytesButReturnsFreshDataOnEveryOperation()
        {
            var serializer = new CountingSerializer();
            var service = Service(serializer, new AtomicFileSaveStore(_directory));
            Assert.That(service.Save("slot", Runtime()).Success, Is.True);
            serializer.ReadCount = 0;
            var first = service.Load("slot");
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(serializer.ReadCount, Is.EqualTo(1));
            var second = service.Load("slot");
            Assert.That(second.Success, Is.True, second.Error);
            Assert.That(serializer.ReadCount, Is.EqualTo(2));
            Assert.That(second.Data, Is.Not.SameAs(first.Data));
            Assert.That(new CampaignSaveTextSerializer().Serialize(second.Data!), Is.EqualTo(new CampaignSaveTextSerializer().Serialize(first.Data!)));
        }

        [Test]
        public void CorruptCurrent_StillValidatesAndRecoversBackupWithoutRepairingFiles()
        {
            var serializer = new CountingSerializer();
            var service = Service(serializer, new AtomicFileSaveStore(_directory));
            var runtime = Runtime();
            Assert.That(service.Save("slot", runtime).Success, Is.True);
            runtime.Clock.Advance(new WorldDuration(7));
            Assert.That(service.Save("slot", runtime).Success, Is.True);
            var current = Path.Combine(_directory, "slot.focsave");
            File.WriteAllText(current, "CORRUPTED_CURRENT");
            serializer.ReadCount = 0;
            var read = service.Load("slot");
            Assert.That(read.Success, Is.True, read.Error);
            Assert.That(read.RecoveryStatus, Is.EqualTo(SaveRecoveryStatus.RecoveredFromBackup));
            Assert.That(read.Data!.WorldTime, Is.Zero);
            Assert.That(serializer.ReadCount, Is.EqualTo(2), "Invalid current and valid backup are distinct payloads.");
            Assert.That(File.ReadAllText(current), Is.EqualTo("CORRUPTED_CURRENT"));
        }

        [Test]
        public void CorruptCommittedBytes_CannotReuseTemporaryValidation()
        {
            var serializer = new CountingSerializer();
            var normal = Service(serializer, new AtomicFileSaveStore(_directory));
            Assert.That(normal.Save("slot", Runtime()).Success, Is.True);
            var faulted = Service(serializer, new AtomicFileSaveStore(_directory, new CorruptFinalRead(Path.Combine(_directory, "slot.focsave"))));
            serializer.ReadCount = 0;
            var write = faulted.Save("slot", Runtime());
            Assert.That(write.Success, Is.False, "Final disk validation must not be skipped.");
            Assert.That(serializer.ReadCount, Is.EqualTo(2));
            var recovered = normal.Load("slot");
            Assert.That(recovered.Success, Is.True, recovered.Error);
            Assert.That(recovered.RecoveryStatus, Is.EqualTo(SaveRecoveryStatus.RecoveredFromBackup));
        }

        [Test]
        public void StoreReturnsDifferentBytes_LoadMustDecodeReturnedPayload()
        {
            var serializer = new CountingSerializer();
            var runtime = Runtime();
            var first = serializer.Serialize(CampaignSaveMapper.ToSaveData(runtime));
            runtime.Clock.Advance(new WorldDuration(9));
            var second = serializer.Serialize(CampaignSaveMapper.ToSaveData(runtime));
            var read = Service(serializer, new SubstitutingReadStore(first, second)).Load("slot");
            Assert.That(read.Success, Is.True, read.Error);
            Assert.That(read.Data!.WorldTime, Is.EqualTo(9));
            Assert.That(serializer.ReadCount, Is.EqualTo(2), "No cached graph for another string may be used.");
        }

        [Test]
        public void ValidationCache_IsBoundedAndEvictedPayloadIsDecodedAgain()
        {
            var serializer = new CountingSerializer();
            var store = new RevisitingWriteStore();
            Assert.That(Service(serializer, store).Save("slot", Runtime()).Success, Is.True);
            Assert.That(store.Validations, Is.EqualTo(5));
            Assert.That(serializer.ReadCount, Is.EqualTo(4), "Only two payloads may be retained per operation.");
        }

        private static CampaignSaveService Service(ISaveSerializer serializer, IAtomicSaveStore store) => new CampaignSaveService(serializer, store, new CampaignSaveValidator(), CampaignSaveDefaults.CreateMigrationPipeline());
        private static CampaignRuntimeState Runtime() => new CampaignRuntimeState(StableId<CampaignTag>.Create("y0-save-validation"), "0.14-dev", "test", 13, 1, new WorldClock(new WorldTimestamp(0)), new SeededRandomSource(13));
        private sealed class CountingSerializer : ISaveSerializer
        {
            private readonly CampaignSaveTextSerializer _inner = new CampaignSaveTextSerializer();
            public int ReadCount { get; set; }
            public string Serialize(CampaignSaveData data) => _inner.Serialize(data);
            public SaveReadResult Deserialize(string content) { ReadCount++; return _inner.Deserialize(content); }
        }
        private sealed class CorruptFinalRead : IAtomicSaveFaultInjector
        {
            private readonly string _path;
            public CorruptFinalRead(string path) => _path = path;
            public void BeforeStage(AtomicSaveStage stage) { if (stage == AtomicSaveStage.FinalRead) File.WriteAllText(_path, "CORRUPTED_COMMIT"); }
        }
        private sealed class SubstitutingReadStore : IAtomicSaveStore
        {
            private readonly string _validated, _returned;
            public SubstitutingReadStore(string validated, string returned) { _validated = validated; _returned = returned; }
            public SaveStoreResult Write(string slot, string content, Func<string, bool> validate) => throw new NotSupportedException();
            public SaveStoreResult Read(string slot, Func<string, bool> validate) { Assert.That(validate(_validated), Is.True); return SaveStoreResult.Read(_returned, false); }
        }
        private sealed class RevisitingWriteStore : IAtomicSaveStore
        {
            public int Validations { get; private set; }
            public SaveStoreResult Write(string slot, string content, Func<string, bool> validate)
            {
                var second = content.Replace("WorldTime=0", "WorldTime=1");
                var third = content.Replace("WorldTime=0", "WorldTime=2");
                foreach (var value in new[] { content, second, third, new string(content.ToCharArray()), third })
                { Validations++; Assert.That(validate(value), Is.True); }
                return SaveStoreResult.Written();
            }
            public SaveStoreResult Read(string slot, Func<string, bool> validate) => throw new NotSupportedException();
        }
    }
}
