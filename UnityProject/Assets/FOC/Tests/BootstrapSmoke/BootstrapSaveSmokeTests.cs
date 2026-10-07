#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FOC.Application.Proof;
using FOC.Application.Save;
using FOC.Bootstrap.Unity;
using FOC.Infrastructure.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FOC.Tests.BootstrapSmoke
{
    public sealed class BootstrapSaveSmokeTests
    {
        private string _root = string.Empty;
        private GameObject? _object;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "foc-windows-smoke", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (_object != null) UnityEngine.Object.DestroyImmediate(_object);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void FailedSave_MustNotEmitPassOrContinueToLoad()
        {
            var store = new ReadCountingStore(new AtomicFileSaveStore(_root), false);
            var bootstrap = Create(store, "../outside");
            LogAssert.Expect(LogType.Error, new Regex("FOC_DEVELOPMENT_SMOKE_FAIL.*SAVE_STEP_FAILED"));
            Run(bootstrap);
            Assert.That(store.ReadCount, Is.Zero, "A failed save must stop before load.");
            Assert.That(Directory.Exists(_root), Is.False);
        }

        [Test]
        public void FailedLoad_MustNotEmitPass()
        {
            var bootstrap = Create(new ReadCountingStore(new AtomicFileSaveStore(_root), true), "smoke");
            LogAssert.Expect(LogType.Error, new Regex("FOC_DEVELOPMENT_SMOKE_FAIL.*LOAD_STEP_FAILED"));
            Run(bootstrap);
        }

        [Test]
        public void SuccessfulSmoke_ReconstructsSavedCampaignAndEmitsPass()
        {
            var bootstrap = Create(new AtomicFileSaveStore(_root), "smoke");
            var original = bootstrap.CurrentCampaign;
            var savedTime = original!.Clock.Now;
            var expected = SavePayloadFingerprint.Compute(new CampaignSaveTextSerializer(), CampaignSaveMapper.ToSaveData(original!));
            LogAssert.Expect(LogType.Log, new Regex("FOC_DEVELOPMENT_SMOKE_PASS.*fingerprint=" + expected));
            Run(bootstrap);
            Assert.That(bootstrap.CurrentCampaign, Is.Not.SameAs(original));
            Assert.That(original.Clock.Now.Ticks, Is.EqualTo(savedTime.Ticks + 1), "The runtime probe must change before load.");
            Assert.That(bootstrap.CurrentCampaign!.Clock.Now, Is.EqualTo(savedTime), "Load must restore the saved clock.");
            Assert.That(SavePayloadFingerprint.Compute(new CampaignSaveTextSerializer(), CampaignSaveMapper.ToSaveData(bootstrap.CurrentCampaign!)), Is.EqualTo(expected));
        }

        [Test]
        public void SuccessfulLoadOfWrongGeneration_MustFailCanonicalComparison()
        {
            var different = IntegratedProofCampaignFactory.Create();
            different.Clock.Advance(new FOC.Domain.Time.WorldDuration(9));
            var payload = new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(different));
            var bootstrap = Create(new WrongGenerationStore(new AtomicFileSaveStore(_root), payload), "smoke");
            LogAssert.Expect(LogType.Error, new Regex("FOC_DEVELOPMENT_SMOKE_FAIL.*SMOKE_ROUND_TRIP_MISMATCH"));
            Run(bootstrap);
        }

        [Test]
        public void RebindingSaveSurface_OneUiActivationMustWriteOnce()
        {
            var store = new ReadCountingStore(new AtomicFileSaveStore(_root), false);
            var bootstrap = Create(store, "smoke");
            var document = _object!.AddComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.Add(new TextField { name = "save-slot", value = "smoke" });
            root.Add(new Label { name = "save-feedback" });
            var save = new Button { name = "save-campaign" };
            root.Add(save);
            root.Add(new Button { name = "load-campaign" });
            var bind = typeof(DevelopmentCampaignBootstrap).GetMethod("BindSaveLoadSurface", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var i = 0; i < 5; i++) bind.Invoke(bootstrap, null);
            InvokeDetachedButtonCallbacks(save);
            Assert.That(store.WriteCount, Is.EqualTo(1), "A reload/rebind must not multiply one UI activation.");
        }

        [Test]
        public void DisableCallback_MustDetachSaveSurface()
        {
            var store = new ReadCountingStore(new AtomicFileSaveStore(_root), false);
            var bootstrap = Create(store, "smoke");
            var document = _object!.AddComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.Add(new TextField { name = "save-slot", value = "smoke" });
            root.Add(new Label { name = "save-feedback" });
            var save = new Button { name = "save-campaign" };
            root.Add(save);
            root.Add(new Button { name = "load-campaign" });
            typeof(DevelopmentCampaignBootstrap).GetMethod("BindSaveLoadSurface", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bootstrap, null);
            // Non-ExecuteAlways behaviours do not receive runtime lifecycle callbacks
            // in EditMode. Exercise the callback itself here; the real player soak
            // separately disables the component and submits the attached button.
            var disable = typeof(DevelopmentCampaignBootstrap).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(disable, Is.Not.Null, "The runtime disable callback must exist.");
            disable!.Invoke(bootstrap, null);
            InvokeDetachedButtonCallbacks(save);
            Assert.That(store.WriteCount, Is.Zero, "Detached/disabled surfaces must not save.");
        }

        [TestCase("10", 10)]
        [TestCase("7200", 7200)]
        [TestCase("14400", 14400)]
        public void SoakDuration_AcceptsBoundedIntegerSeconds(string value, int expected) =>
            Assert.That(DevelopmentPlayerSoak.ParseDuration(value), Is.EqualTo(expected));

        [TestCase("9")]
        [TestCase("14401")]
        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase(" ")]
        public void SoakDuration_RejectsMalformedOrUnboundedRun(string value) =>
            Assert.Throws<ArgumentException>(() => DevelopmentPlayerSoak.ParseDuration(value));

        [TestCase(false, false, "PASS")]
        [TestCase(true, false, "DIAGNOSTIC_COMPLETE")]
        [TestCase(false, true, "DIAGNOSTIC_COMPLETE")]
        public void SoakOutcome_MemoryCollectionDiagnosticCannotBeAcceptancePass(bool memoryAudit, bool allocationAudit, string expected)
        {
            var soak = CreateSoak(memoryAudit, allocationAudit);
            FinishSoak(soak, null);
            var report = JsonUtility.FromJson<SoakOutcome>(File.ReadAllText(Path.Combine(_root, "player-soak.json")));
            Assert.That(report.status, Is.EqualTo(expected));
            Assert.That(report.memoryAudit, Is.EqualTo(memoryAudit));
            Assert.That(report.allocationAudit, Is.EqualTo(allocationAudit));
            Assert.That(report.auditFinalCollectedBytes > 0, Is.EqualTo(memoryAudit));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SoakOutcome_MemoryAuditMustNotHideFailure(bool memoryAudit)
        {
            var soak = CreateSoak(memoryAudit);
            FinishSoak(soak, "KNOWN_GUARD_FAILURE");
            var report = JsonUtility.FromJson<SoakOutcome>(File.ReadAllText(Path.Combine(_root, "player-soak.json")));
            Assert.That(report.status, Is.EqualTo("FAIL"));
            Assert.That(report.error, Is.EqualTo("KNOWN_GUARD_FAILURE"));
        }

        [UnityTest]
        public IEnumerator SoakAllocationRecorder_RecordsRealEventsAndFrameBytesAndReleasesStorage()
        {
            var soak = CreateSoak(false, true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DevelopmentPlayerSoak).GetMethod("InitializeAllocationRecorders", flags)!.Invoke(soak, null);
            typeof(DevelopmentPlayerSoak).GetMethod("BeginAllocationMeasurement", flags)!.Invoke(soak, null);
            GC.KeepAlive(new byte[4096]);
            var events = (long)typeof(DevelopmentPlayerSoak).GetMethod("EndAllocationMeasurement", flags)!.Invoke(soak, null)!;
            Assert.That(events, Is.GreaterThanOrEqualTo(1), "Marker counts events; its timing values are not bytes.");
            yield return null;
            var frame = (Unity.Profiling.ProfilerRecorder)typeof(DevelopmentPlayerSoak).GetField("_frameAllocationRecorder", flags)!.GetValue(soak)!;
            Assert.That(frame.LastValue, Is.GreaterThanOrEqualTo(4096), "Require a live Unity frame-byte counter, not fabricated zero.");
            typeof(DevelopmentPlayerSoak).GetMethod("ReleaseAllocationRecorders", flags)!.Invoke(soak, null);
            foreach (var field in new[] { "_allocationRecorder", "_frameAllocationRecorder" })
                Assert.That(((Unity.Profiling.ProfilerRecorder)typeof(DevelopmentPlayerSoak).GetField(field, flags)!.GetValue(soak)!).Valid, Is.False);
        }

        [Test]
        public void SoakAllocationFailure_StillWritesOriginalFailureWithoutRestartingAnUnavailableRecorder()
        {
            var soak = CreateSoak(false, true);
            var bootstrap = _object!.AddComponent<DevelopmentCampaignBootstrap>();
            bootstrap.enabled = false;
            Set(bootstrap, "_campaign", IntegratedProofCampaignFactory.Create());
            typeof(DevelopmentPlayerSoak).GetField("_bootstrap", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, bootstrap);
            FinishSoak(soak, "ALLOCATION_MARKER_UNAVAILABLE");
            var report = JsonUtility.FromJson<SoakOutcome>(File.ReadAllText(Path.Combine(_root, "player-soak.json")));
            Assert.That(report.status, Is.EqualTo("FAIL"));
            Assert.That(report.error, Is.EqualTo("ALLOCATION_MARKER_UNAVAILABLE"));
        }

        [Test]
        public void SoakLifetimeDiagnostics_AreBoundedWeakReferences()
        {
            var references = new List<WeakReference>();
            var held = new List<object>();
            var remember = typeof(DevelopmentPlayerSoak).GetMethod("RememberRetired", BindingFlags.Static | BindingFlags.NonPublic)!;
            for (var i = 0; i < 600; i++)
            {
                var value = new object();
                held.Add(value);
                remember.Invoke(null, new object[] { references, value });
            }
            Assert.That(references.Count, Is.EqualTo(512));
            Assert.That(references[0].Target, Is.SameAs(held[88]));
            Assert.That(references[511].Target, Is.SameAs(held[599]));
        }

        [Test]
        public void SoakSample_GuardFailureMustKeepCurrentMemoryMeasurements()
        {
            var soak = CreateSoak(false);
            _object!.AddComponent<UIDocument>();
            var report = typeof(DevelopmentPlayerSoak).GetField("_report", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(soak)!;
            report.GetType().GetField("cycles")!.SetValue(report, 4);
            report.GetType().GetField("warmUiElements")!.SetValue(report, 10000);
            typeof(DevelopmentPlayerSoak).GetField("_managedBaseline", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, -128L * 1024 * 1024);
            typeof(DevelopmentPlayerSoak).GetField("_nativeBaseline", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
            // A low diagnostic idle minimum must never replace the raw guarded sample.
            typeof(DevelopmentPlayerSoak).GetField("_idleMinimumManagedBytes", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, 1L);
            typeof(DevelopmentPlayerSoak).GetField("_idleSamples", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, 20);
            var failure = Assert.Throws<TargetInvocationException>(() => typeof(DevelopmentPlayerSoak).GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(soak, null));
            Assert.That(failure!.InnerException!.Message, Is.EqualTo("MANAGED_GROWTH_OVER_64_MIB"));
            Assert.That((long)report.GetType().GetField("lastManagedBytes")!.GetValue(report)!, Is.GreaterThan(0));
            Assert.That((long)report.GetType().GetField("monoUsedBytes")!.GetValue(report)!, Is.GreaterThan(0));
            Assert.That((long)report.GetType().GetField("idleMinimumManagedBytes")!.GetValue(report)!, Is.EqualTo(1));
            Assert.That((int)report.GetType().GetField("gc0")!.GetValue(report)!, Is.EqualTo(GC.CollectionCount(0)));
        }

        private DevelopmentPlayerSoak CreateSoak(bool memoryAudit, bool allocationAudit = false)
        {
            _object = new GameObject("Soak diagnostic outcome regression");
            var soak = _object.AddComponent<DevelopmentPlayerSoak>();
            soak.enabled = false;
            typeof(DevelopmentPlayerSoak).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, _root);
            typeof(DevelopmentPlayerSoak).GetField("_memoryAudit", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, memoryAudit);
            typeof(DevelopmentPlayerSoak).GetField("_allocationAudit", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(soak, allocationAudit);
            var report = typeof(DevelopmentPlayerSoak).GetField("_report", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(soak)!;
            report.GetType().GetField("memoryAudit")!.SetValue(report, memoryAudit);
            report.GetType().GetField("allocationAudit")!.SetValue(report, allocationAudit);
            return soak;
        }

        private static void FinishSoak(DevelopmentPlayerSoak soak, string? error) =>
            typeof(DevelopmentPlayerSoak).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(soak, new object?[] { error });

        [Serializable]
        private sealed class SoakOutcome
        {
            public string status = string.Empty, error = string.Empty;
            public bool memoryAudit;
            public bool allocationAudit;
            public long auditFinalCollectedBytes;
        }

        private DevelopmentCampaignBootstrap Create(IAtomicSaveStore store, string slot)
        {
            _object = new GameObject("Isolated save smoke regression");
            var bootstrap = _object.AddComponent<DevelopmentCampaignBootstrap>();
            bootstrap.enabled = false; // Tests invoke only the coroutine, never normal player startup.
            var campaign = IntegratedProofCampaignFactory.Create();
            var service = new CampaignSaveService(new CampaignSaveTextSerializer(), store, new CampaignSaveValidator(), CampaignSaveDefaults.CreateMigrationPipeline());
            Set(bootstrap, "_campaign", campaign);
            Set(bootstrap, "_saveCoordinator", new CampaignSaveCoordinator(service));
            Set(bootstrap, "_slot", new TextField { value = slot });
            Set(bootstrap, "_feedback", new Label());
            return bootstrap;
        }

        private static void Set(DevelopmentCampaignBootstrap target, string field, object value) =>
            typeof(DevelopmentCampaignBootstrap).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        // Detached EditMode roots have no event dispatcher. Inspect/invoke the real
        // registered callback list; the Windows soak separately uses attached UI events.
        private static void InvokeDetachedButtonCallbacks(Button button)
        {
            var field = typeof(Clickable).GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Unity callback backing field changed; this diagnostic must be updated, not skipped.");
            ((Action?)field!.GetValue(button.clickable))?.Invoke();
        }

        private static void Run(DevelopmentCampaignBootstrap bootstrap)
        {
            var routine = (IEnumerator)typeof(DevelopmentCampaignBootstrap).GetMethod("RunSmoke", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bootstrap, null);
            while (routine.MoveNext()) { }
        }

        private sealed class ReadCountingStore : IAtomicSaveStore
        {
            private readonly IAtomicSaveStore _inner;
            private readonly bool _failRead;
            public ReadCountingStore(IAtomicSaveStore inner, bool failRead) { _inner = inner; _failRead = failRead; }
            public int ReadCount { get; private set; }
            public int WriteCount { get; private set; }
            public SaveStoreResult Write(string slotName, string content, Func<string, bool> validateContent)
            {
                WriteCount++;
                return _inner.Write(slotName, content, validateContent);
            }
            public SaveStoreResult Read(string slotName, Func<string, bool> validateContent)
            {
                ReadCount++;
                return _failRead ? SaveStoreResult.Failed("INJECTED_SMOKE_READ_FAILURE") : _inner.Read(slotName, validateContent);
            }
        }

        private sealed class WrongGenerationStore : IAtomicSaveStore
        {
            private readonly IAtomicSaveStore _inner;
            private readonly string _payload;
            public WrongGenerationStore(IAtomicSaveStore inner, string payload) { _inner = inner; _payload = payload; }
            public SaveStoreResult Write(string slotName, string content, Func<string, bool> validateContent) => _inner.Write(slotName, content, validateContent);
            public SaveStoreResult Read(string slotName, Func<string, bool> validateContent) => SaveStoreResult.Read(_payload, false);
        }
    }
}
