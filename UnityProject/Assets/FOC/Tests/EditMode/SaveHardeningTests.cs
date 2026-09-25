#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class SaveHardeningTests
    {
        private string _directory = string.Empty;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "foc-save-hardening", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void AuthoritativeMigrationChain_IsContinuousOneThroughFourteenAndInventsNoState()
        {
            var migrations = CampaignSaveDefaults.OrderedMigrations;
            Assert.That(migrations.Select(x => x.FromVersion), Is.EqualTo(Enumerable.Range(1, 13)));
            Assert.That(migrations.Select(x => x.ToVersion), Is.EqualTo(Enumerable.Range(2, 13)));
            var source = MinimalData(1);
            var result = CampaignSaveDefaults.CreateMigrationPipeline().Migrate(source, 14);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Data!.SaveVersion, Is.EqualTo(14));
            Assert.That(result.Data.Characters, Is.Empty);
            Assert.That(result.Data.Cities, Is.Empty);
            Assert.That(result.Data.Armies, Is.Empty);
            Assert.That(result.Data.Soldiers, Is.Empty);
            Assert.That(result.Data.Battles, Is.Empty);
            Assert.That(result.Data.Encounters, Is.Empty);
            Assert.That(result.Data.Contracts, Is.Empty);
            Assert.That(result.Data.AIControllers, Is.Empty);
            Assert.That(result.Data.WorldLocations, Is.Empty);
            Assert.That(result.Data.TravelJourneys, Is.Empty);
        }

        [TestCase(AtomicSaveStage.TempCreate)]
        [TestCase(AtomicSaveStage.TempWrite)]
        [TestCase(AtomicSaveStage.DurableFlush)]
        [TestCase(AtomicSaveStage.TempValidation)]
        [TestCase(AtomicSaveStage.Backup)]
        [TestCase(AtomicSaveStage.Replace)]
        [TestCase(AtomicSaveStage.FinalRead)]
        public void FaultAtEveryWriteBoundary_LeavesAtLeastOneValidGeneration(AtomicSaveStage stage)
        {
            var normal = new AtomicFileSaveStore(_directory);
            Assert.That(normal.Write("slot", "valid-old", Valid).Success, Is.True);
            var faulted = new AtomicFileSaveStore(_directory, new ThrowOnce(stage));
            Assert.That(faulted.Write("slot", "valid-new", Valid).Success, Is.False);
            var result = normal.Read("slot", Valid);
            Assert.That(result.Success, Is.True, $"No valid generation after injected {stage} fault.");
            Assert.That(new[] { "valid-old", "valid-new" }, Does.Contain(result.Content));
        }

        [Test]
        public void RecoveryMatrix_HandlesCurrentBackupCorruptionAndStaleTempWithoutRepair()
        {
            var store = new AtomicFileSaveStore(_directory);
            store.Write("slot", "valid-old", Valid);
            store.Write("slot", "valid-current", Valid);
            File.WriteAllText(Path.Combine(_directory, "slot.focsave.tmp"), "valid-uncommitted");
            Assert.That(store.Read("slot", Valid).Content, Is.EqualTo("valid-current"), "Stale tmp must not become authoritative.");

            File.WriteAllText(Path.Combine(_directory, "slot.focsave.bak"), "corrupt");
            var current = store.Read("slot", Valid);
            Assert.That(current.Success, Is.True);
            Assert.That(current.RecoveredFromBackup, Is.False);

            File.WriteAllText(Path.Combine(_directory, "slot.focsave.bak"), "valid-backup");
            File.WriteAllText(Path.Combine(_directory, "slot.focsave"), "corrupt");
            var recovered = store.Read("slot", Valid);
            Assert.That(recovered.Content, Is.EqualTo("valid-backup"));
            Assert.That(recovered.RecoveredFromBackup, Is.True);

            File.WriteAllText(Path.Combine(_directory, "slot.focsave.bak"), "corrupt-too");
            var failed = store.Read("slot", Valid);
            Assert.That(failed.Success, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "slot.focsave")), Is.EqualTo("corrupt"));
            Assert.That(File.ReadAllText(Path.Combine(_directory, "slot.focsave.bak")), Is.EqualTo("corrupt-too"));
        }

        [TestCase("")]
        [TestCase("FOC_CAMPAIGN_SAVE")]
        [TestCase("FOC_CAMPAIGN_SAVE\nSaveVersion=oops")]
        [TestCase("FOC_CAMPAIGN_SAVE\nSaveVersion=12\nSaveVersion=12")]
        public void TruncatedOrMalformedPayload_FailsExplicitly(string payload)
        {
            var result = new CampaignSaveTextSerializer().Deserialize(payload);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.Not.Empty);
        }

        [Test]
        public void FutureVersionAndOversizedCount_AreRejected()
        {
            var future = MinimalData(15);
            Assert.That(new CampaignSaveValidator().Validate(future).Issues.Select(x => x.Code), Does.Contain("SAVE_VERSION_FUTURE"));
            var payload = new CampaignSaveTextSerializer().Serialize(MinimalData(2)).Replace("CharacterCount=0", "CharacterCount=1000001");
            Assert.That(new CampaignSaveTextSerializer().Deserialize(payload).Success, Is.False);
        }

        [TestCase("../outside")]
        [TestCase("..\\outside")]
        [TestCase("C:\\absolute")]
        [TestCase("nested/slot")]
        [TestCase("nested\\slot")]
        [TestCase("slot.txt")]
        public void InvalidSlotOrTraversal_IsRejected(string slot)
        {
            Assert.That(new AtomicFileSaveStore(_directory).Write(slot, "valid", Valid).Success, Is.False);
        }

        [Test]
        public void ConcurrentSameSlotWriters_AreSerializedAndLeaveValidCurrentAndBackup()
        {
            var store = new AtomicFileSaveStore(_directory);
            Parallel.For(0, 24, index => Assert.That(store.Write("slot", "valid-" + index.ToString(CultureInfo.InvariantCulture), Valid).Success, Is.True));
            Assert.That(store.Read("slot", Valid).Success, Is.True);
            Assert.That(Valid(File.ReadAllText(Path.Combine(_directory, "slot.focsave"))), Is.True);
            Assert.That(Valid(File.ReadAllText(Path.Combine(_directory, "slot.focsave.bak"))), Is.True);
        }

        [Test]
        public void LoadDuringSave_SeesOnlyACommittedGeneration()
        {
            var normal = new AtomicFileSaveStore(_directory);
            normal.Write("slot", "valid-old", Valid);
            var blocker = new BlockingInjector(AtomicSaveStage.TempValidation);
            var writing = Task.Run(() => new AtomicFileSaveStore(_directory, blocker).Write("slot", "valid-new", Valid));
            Assert.That(blocker.Entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            var reading = Task.Run(() => normal.Read("slot", Valid));
            Assert.That(reading.Wait(TimeSpan.FromMilliseconds(100)), Is.False, "Read should serialize behind the same-slot save transaction.");
            blocker.Release.Set();
            Assert.That(writing.Result.Success, Is.True);
            Assert.That(reading.Result.Content, Is.EqualTo("valid-new"));
        }

        [Test]
        public void OneHundredSaveLoadCycles_HaveStableSizeIdentityFingerprintAndNoHandleLeak()
        {
            var serializer = new CampaignSaveTextSerializer();
            var service = Service(serializer);
            var runtime = MinimalRuntime();
            long? length = null;
            string? fingerprint = null;
            for (var index = 0; index < 100; index++)
            {
                Assert.That(service.Save("cycle", runtime).Success, Is.True);
                var read = service.Load("cycle");
                Assert.That(read.Success, Is.True, read.Error);
                runtime = CampaignSaveMapper.ToRuntimeState(read.Data!);
                var path = Path.Combine(_directory, "cycle.focsave");
                length ??= new FileInfo(path).Length;
                fingerprint ??= SavePayloadFingerprint.Compute(File.ReadAllText(path));
                Assert.That(new FileInfo(path).Length, Is.EqualTo(length));
                Assert.That(SavePayloadFingerprint.Compute(File.ReadAllText(path)), Is.EqualTo(fingerprint));
            }
            var current = Path.Combine(_directory, "cycle.focsave");
            var renamed = current + ".renamed";
            File.Move(current, renamed);
            File.Delete(renamed);
            Assert.That(runtime.CampaignId.Value, Is.EqualTo("integrated-proof"));
        }

        [Test]
        public void CanonicalSerialization_IsCultureIndependentAndFingerprintStable()
        {
            var serializer = new CampaignSaveTextSerializer();
            var data = CampaignSaveMapper.ToSaveData(MinimalRuntime());
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR"); var turkish = serializer.Serialize(data);
                CultureInfo.CurrentCulture = new CultureInfo("en-US"); var english = serializer.Serialize(data);
                Assert.That(english, Is.EqualTo(turkish));
                Assert.That(SavePayloadFingerprint.Compute(english), Is.EqualTo(SavePayloadFingerprint.Compute(turkish)));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Test]
        public void Coordinator_ReportsBackupRecoveryAndVersionMismatchWithoutSilentFallback()
        {
            var serializer = new CampaignSaveTextSerializer();
            var service = Service(serializer);
            var runtime = MinimalRuntime();
            service.Save("slot", runtime);
            service.Save("slot", runtime);
            File.WriteAllText(Path.Combine(_directory, "slot.focsave"), "corrupt");
            var recovered = new CampaignSaveCoordinator(service, runtime.ContentDataVersion, runtime.WorldGenRevision).Load("slot");
            Assert.That(recovered.Success, Is.True, recovered.Error);
            Assert.That(recovered.RecoveryStatus, Is.EqualTo(SaveRecoveryStatus.RecoveredFromBackup));
            Assert.That(recovered.RecoveryReason, Is.Not.Empty);
            var mismatch = new CampaignSaveCoordinator(service, "different-content", runtime.WorldGenRevision).Load("slot");
            Assert.That(mismatch.Success, Is.False);
            Assert.That(mismatch.Error, Is.EqualTo("SAVE_CONTENT_VERSION_MISMATCH"));
        }

        [Test]
        public void SaveSchemaDocumentation_CodeAndMigrationChainAgreeOnVersionFourteen()
        {
            var path = FindRepositoryFile("Docs", "SAVE_SCHEMA.md");
            var text = File.ReadAllText(path);
            Assert.That(CampaignSaveData.CurrentSaveVersion, Is.EqualTo(14));
            Assert.That(CampaignSaveDefaults.OrderedMigrations.Last().ToVersion, Is.EqualTo(14));
            Assert.That(text, Does.Contain("CampaignSaveData.CurrentSaveVersion = 14"));
        }

        private CampaignSaveService Service(CampaignSaveTextSerializer serializer) => new CampaignSaveService(serializer, new AtomicFileSaveStore(_directory), new CampaignSaveValidator(), CampaignSaveDefaults.CreateMigrationPipeline());
        private static bool Valid(string content) => content.StartsWith("valid-", StringComparison.Ordinal) || content == "valid";
        private static CampaignRuntimeState MinimalRuntime() => new CampaignRuntimeState(StableId<CampaignTag>.Create("integrated-proof"), "0.13-dev", "proof-content-1", 130013, 1, new WorldClock(new WorldTimestamp(1300)), new SeededRandomSource(130013));
        private static CampaignSaveData MinimalData(int version) => new CampaignSaveData { SaveVersion = version, CampaignId = "fixture", GameVersion = "0.13", ContentDataVersion = "proof", WorldSeed = 13, WorldGenRevision = 1, WorldTime = 3, RngState = 13, RngDrawCount = 0 };

        private static string FindRepositoryFile(params string[] parts)
        {
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
                if (File.Exists(candidate)) return candidate;
                current = current.Parent;
            }
            throw new FileNotFoundException("Repository file was not found.", Path.Combine(parts));
        }

        private sealed class ThrowOnce : IAtomicSaveFaultInjector
        {
            private readonly AtomicSaveStage _target; private bool _thrown;
            public ThrowOnce(AtomicSaveStage target) { _target = target; }
            public void BeforeStage(AtomicSaveStage stage) { if (!_thrown && stage == _target) { _thrown = true; throw new IOException("INJECTED_" + stage); } }
        }

        private sealed class BlockingInjector : IAtomicSaveFaultInjector
        {
            private readonly AtomicSaveStage _target;
            public BlockingInjector(AtomicSaveStage target) { _target = target; }
            public ManualResetEventSlim Entered { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);
            public void BeforeStage(AtomicSaveStage stage) { if (stage == _target) { Entered.Set(); Release.Wait(TimeSpan.FromSeconds(5)); } }
        }
    }
}
