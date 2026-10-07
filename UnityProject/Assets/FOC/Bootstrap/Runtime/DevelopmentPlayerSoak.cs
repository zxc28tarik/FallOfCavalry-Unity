#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Geography;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace FOC.Bootstrap.Unity
{
    // Opt-in Development validation only. Uses the shipped document/callbacks,
    // not replacement screens, a second campaign, or direct gameplay mutators.
    public sealed class DevelopmentPlayerSoak : MonoBehaviour
    {
        private DevelopmentCampaignBootstrap? _bootstrap;
        private string _root = string.Empty;
        private readonly Report _report = new Report();
        private readonly int[] _frameHistogram = new int[2001];
        private readonly SlicePresentationLocalizer _localizer = new SlicePresentationLocalizer();
        private readonly List<WeakReference> _retiredCampaigns = new List<WeakReference>();
        private readonly List<WeakReference> _retiredFieldLists = new List<WeakReference>();
        private double _started;
        private double _lastFrame;
        private long _nativeBaseline;
        private long _managedBaseline;
        private bool _finished;
        private string? _unexpectedError;
        private bool _memoryAudit;
        private bool _samplingIdle;
        private long _idleMinimumManagedBytes;
        private int _idleSamples;
        private bool _allocationAudit;
        private ProfilerRecorder _allocationRecorder;
        private ProfilerRecorder _frameAllocationRecorder;

        public static int ParseDuration(string? value)
        {
            if (value == null) return 7200;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds < 10 || seconds > 14400)
                throw new ArgumentException("Soak duration must be an integer between 10 and 14400 seconds.");
            return seconds;
        }

        public void Configure(DevelopmentCampaignBootstrap bootstrap, string root, int seconds)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            _root = root;
            _report.requestedSeconds = ParseDuration(seconds.ToString(CultureInfo.InvariantCulture));
            _report.startedUtc = DateTime.UtcNow.ToString("o");
            _report.unityVersion = UnityEngine.Application.unityVersion;
            _report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            _memoryAudit = Array.Exists(Environment.GetCommandLineArgs(), x => StringComparer.Ordinal.Equals(x, "-focSoakMemoryAudit"));
            _report.memoryAudit = _memoryAudit;
            _allocationAudit = Array.Exists(Environment.GetCommandLineArgs(), x => StringComparer.Ordinal.Equals(x, "-focSoakAllocationAudit"));
            _report.allocationAudit = _allocationAudit;
        }

        private IEnumerator Start()
        {
            UnityEngine.Application.runInBackground = true;
            UnityEngine.Application.targetFrameRate = 30;
            UnityEngine.Application.logMessageReceived += OnLog;
            _started = _lastFrame = Time.realtimeSinceStartupAsDouble;
            var work = Exercise();
            while (!_finished)
            {
                bool next;
                try { next = work.MoveNext(); }
                catch (Exception error) { Finish(error.Message); yield break; }
                if (!next) { Finish(null); yield break; }
                yield return work.Current;
            }
        }

        private IEnumerator Exercise()
        {
            if (_allocationAudit) InitializeAllocationRecorders();
            yield return null;
            yield return null;
            Require(_bootstrap?.CurrentCampaign != null, "CAMPAIGN_MISSING");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GRAPHICS_NOT_ACTIVE");
            Require(Document.panel != null && Document.worldBound.width >= 1024, "RUNTIME_PANEL_NOT_LAID_OUT");
            Debug.Log("FOC_PLAYER_SOAK_READY seconds=" + _report.requestedSeconds + " root=" + _root + " graphics=" + _report.graphicsDevice);
            var screens = new[] { "map", "city", "character", "organization", "trade", "army", "diplomacy", "battle", "reports", "ledger" };
            do
            {
                var fingerprint = Fingerprint();
                foreach (var screen in screens)
                {
                    Activate(Document.Q<Button>("nav-" + screen));
                    yield return null;
                    Require(Title == _localizer.Get("presentation.screen." + screen), "NAVIGATION_TITLE_" + screen);
                    _report.navigationChecks++;
                }
                Activate(Document.Q<Button>("nav-back"));
                Require(Title == _localizer.Get("presentation.screen.reports"), "BACK_ROUTE");
                Activate(Document.Q<Button>("nav-forward"));
                Require(Title == _localizer.Get("presentation.screen.ledger"), "FORWARD_ROUTE");
                using (var escape = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) Document.SendEvent(escape);
                Require(Title == _localizer.Get("presentation.screen.reports"), "ESCAPE_ROUTE");
                Require(Fingerprint() == fingerprint, "NAVIGATION_MUTATED_CAMPAIGN");
                _report.navigationChecks += 3;
                Activate(Document.Q<Button>("nav-map"));
                yield return null;

                if (_report.cycles == 0)
                {
                    var start = Actions.FirstOrDefault(x => x.text.Contains("Edirne"));
                    Require(start != null && start.enabledInHierarchy, "TRAVEL_START_BUTTON_MISSING");
                    Activate(start);
                    Require(Feedback("action-feedback") == _localizer.Get("presentation.travel.started"), "TRAVEL_START_FEEDBACK");
                    Require(_bootstrap!.CurrentCampaign!.Geography.Travel.OrderedJourneys.Count(x => x.Lifecycle == TravelLifecycle.Active) == 1, "TRAVEL_NOT_STARTED");
                    _report.travelChecks++;
                    yield return null;
                }
                if (_report.cycles < 50 && _bootstrap!.CurrentCampaign!.Geography.Travel.OrderedJourneys.Any(x => x.Lifecycle == TravelLifecycle.Active))
                {
                    var advance = Actions.FirstOrDefault(x => x.text == _localizer.Get("presentation.action.advance-one-hour"));
                    var before = _bootstrap.CurrentCampaign.Clock.Now.Ticks;
                    Activate(advance);
                    Require(_bootstrap.CurrentCampaign.Clock.Now.Ticks > before, "TRAVEL_CLOCK_NOT_ADVANCED");
                    Require(Feedback("action-feedback") == _localizer.Get("presentation.travel.advanced"), "TRAVEL_ADVANCE_FEEDBACK");
                    _report.travelChecks++;
                    yield return null;
                }

                if (_report.cycles == 0)
                {
                    var unchanged = Fingerprint();
                    Slot.value = "../outside";
                    Activate(Document.Q<Button>("save-campaign"));
                    Require(Feedback("save-feedback").StartsWith("SAVE FAILED", StringComparison.Ordinal), "INVALID_SAVE_NOT_REJECTED");
                    Require(Fingerprint() == unchanged, "INVALID_SAVE_MUTATED_CAMPAIGN");
                    Slot.value = "missing";
                    Activate(Document.Q<Button>("load-campaign"));
                    Require(Feedback("save-feedback").StartsWith("LOAD FAILED", StringComparison.Ordinal), "MISSING_LOAD_NOT_REJECTED");
                    Require(Fingerprint() == unchanged, "MISSING_LOAD_MUTATED_CAMPAIGN");
                    _report.rejectionChecks += 2;
                }

                Slot.value = "soak";
                var expected = Fingerprint();
                var old = _bootstrap!.CurrentCampaign;
                RememberRetired(_retiredCampaigns, old!);
                foreach (var list in Document.Query<ListView>().ToList()) RememberRetired(_retiredFieldLists, list);
                Activate(Document.Q<Button>("save-campaign"));
                Require(Feedback("save-feedback") == "SAVED", "UI_SAVE_FAILED");
                Activate(Document.Q<Button>("load-campaign"));
                Require(Feedback("save-feedback") == "LOADED", "UI_LOAD_FAILED");
                Require(!ReferenceEquals(old, _bootstrap.CurrentCampaign), "UI_LOAD_DID_NOT_RECONSTRUCT");
                Require(Fingerprint() == expected, "UI_ROUNDTRIP_MISMATCH");
                _report.roundtrips++;
                yield return null;
                Require(Title == _localizer.Get("presentation.screen.map"), "RELOAD_MAP_NOT_REBUILT");
                Require(Document.panel != null, "RELOAD_PANEL_DETACHED");
                _report.cycles++;
                if (_memoryAudit && _report.cycles == 3) _report.auditWarmCollectedBytes = DiagnosticCollectedMemory();
                // Compare like-for-like quiescent phases, rather than the garbage burst
                // immediately after graph deserialization and rebuilding every screen.
                // The same five-second pause already belongs to the workload. No GC is forced.
                _samplingIdle = true;
                _idleMinimumManagedBytes = long.MaxValue;
                _idleSamples = 0;
                yield return new WaitForSecondsRealtime(5f);
                _samplingIdle = false;
                Sample();
                WriteState();
                Require(_unexpectedError == null, "UNEXPECTED_PLAYER_ERROR " + _unexpectedError);
                if (_allocationAudit && _report.cycles == 3)
                {
                    // Diagnostic-only frame windows separate callbacks whose
                    // byte counter is flushed at frame end. Never acceptance.
                    var probe = CaptureAllocationProbe();
                    while (probe.MoveNext()) yield return probe.Current;
                }
            } while (Time.realtimeSinceStartupAsDouble - _started < _report.requestedSeconds);
            var feedbackEvents = _report.saveFeedbackEvents;
            var saveButton = Document.Q<Button>("save-campaign");
            _bootstrap!.enabled = false; // Actual runtime lifecycle, not a reflected callback.
            Activate(saveButton);
            yield return null;
            Require(_report.saveFeedbackEvents == feedbackEvents, "DISABLED_BOOTSTRAP_STILL_HANDLES_UI");
            _report.lifecycleChecks++;
            Require(_report.cycles >= 2 && _report.rejectionChecks == 2 && _report.travelChecks >= 2, "INCOMPLETE_UI_COVERAGE");
            Require(_report.frames >= _report.requestedSeconds * 5, "INSUFFICIENT_FRAME_SAMPLES");
            Require(_report.maxFrameMilliseconds < 15000, "FRAME_STALL_OVER_15_SECONDS");
        }

        private VisualElement Document => GetComponent<UIDocument>().rootVisualElement;
        private TextField Slot => Document.Q<TextField>("save-slot") ?? throw new InvalidOperationException("SLOT_MISSING");
        private string Title => Feedback("screen-title");
        private List<Button> Actions => Document.Q<VisualElement>("action-content").Query<Button>().ToList();
        private string Feedback(string name) => Document.Q<Label>(name)?.text ?? throw new InvalidOperationException("LABEL_MISSING " + name);
        private string Fingerprint()
        {
            BeginAllocationMeasurement();
            var result = SavePayloadFingerprint.Compute(new CampaignSaveTextSerializer(), CampaignSaveMapper.ToSaveData(_bootstrap!.CurrentCampaign!));
            if (_allocationAudit && !_finished) { _report.fingerprintAllocationEvents += EndAllocationMeasurement(); _report.fingerprintAllocationSamples++; }
            return result;
        }
        private void Activate(Button? button)
        {
            Require(button != null && button.enabledInHierarchy && button.panel != null, "BUTTON_UNAVAILABLE");
            var started = Time.realtimeSinceStartupAsDouble;
            BeginAllocationMeasurement();
            button!.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            var events = EndAllocationMeasurement();
            // GC.Alloc marker samples count allocation events, not bytes. Its
            // timing sample values must not be mislabeled as byte measurements.
            if (_allocationAudit && button.name.StartsWith("nav-", StringComparison.Ordinal))
            { _report.navigationAllocationEvents += events; _report.navigationAllocationSamples++; }
            else if (_allocationAudit && button.name == "save-campaign")
            { _report.saveAllocationEvents += events; _report.saveAllocationSamples++; }
            else if (_allocationAudit && button.name == "load-campaign")
            { _report.loadAllocationEvents += events; _report.loadAllocationSamples++; }
            _report.maxUiOperationMilliseconds = Math.Max(_report.maxUiOperationMilliseconds, (Time.realtimeSinceStartupAsDouble - started) * 1000);
            _report.uiActivations++;
        }
        private void Update()
        {
            if (_finished || _started == 0) return;
            var now = Time.realtimeSinceStartupAsDouble;
            var ms = (now - _lastFrame) * 1000;
            _lastFrame = now;
            _report.frames++;
            if (_allocationAudit && _frameAllocationRecorder.Valid) _report.frameAllocatedBytes += _frameAllocationRecorder.LastValue;
            _report.maxFrameMilliseconds = Math.Max(_report.maxFrameMilliseconds, ms);
            _frameHistogram[Math.Min(2000, Math.Max(0, (int)Math.Ceiling(ms)))]++;
            if (_report.frames % 10 == 0)
            {
                var managed = GC.GetTotalMemory(false);
                _report.peakManagedBytes = Math.Max(_report.peakManagedBytes, managed);
                if (_samplingIdle)
                {
                    _idleMinimumManagedBytes = Math.Min(_idleMinimumManagedBytes, managed);
                    _idleSamples++;
                }
            }
        }
        private void Sample()
        {
            var native = Profiler.GetTotalAllocatedMemoryLong();
            var managed = GC.GetTotalMemory(false);
            var nodes = Document.Query<VisualElement>().ToList().Count;
            _report.peakNativeAllocatedBytes = Math.Max(_report.peakNativeAllocatedBytes, native);
            _report.peakManagedBytes = Math.Max(_report.peakManagedBytes, managed);
            _report.maxUiElements = Math.Max(_report.maxUiElements, nodes);
            _report.lastNativeAllocatedBytes = native;
            _report.lastManagedBytes = managed;
            _report.monoUsedBytes = Profiler.GetMonoUsedSizeLong();
            _report.monoHeapBytes = Profiler.GetMonoHeapSizeLong();
            _report.retiredCampaignsTracked = _retiredCampaigns.Count;
            _report.retiredCampaignsAlive = _retiredCampaigns.Count(x => x.IsAlive);
            _report.retiredFieldListsTracked = _retiredFieldLists.Count;
            _report.retiredFieldListsAlive = _retiredFieldLists.Count(x => x.IsAlive);
            _report.gc0 = GC.CollectionCount(0); _report.gc1 = GC.CollectionCount(1); _report.gc2 = GC.CollectionCount(2);
            _report.idleMinimumManagedBytes = _idleSamples == 0 ? managed : _idleMinimumManagedBytes;
            _report.idleSamples = _idleSamples;
            // A workload-specific guard, not a universal leak or performance guarantee.
            // Do not force GC: a real accumulating problem must remain observable.
            if (_report.cycles == 3) { _nativeBaseline = native; _managedBaseline = managed; _report.warmUiElements = nodes; }
            if (_report.cycles >= 3)
            {
                _report.nativeGrowthBytes = native - _nativeBaseline;
                _report.managedGrowthBytes = managed - _managedBaseline;
                Require(_report.nativeGrowthBytes <= 128L * 1024 * 1024, "NATIVE_GROWTH_OVER_128_MIB");
                Require(_report.managedGrowthBytes <= 64L * 1024 * 1024, "MANAGED_GROWTH_OVER_64_MIB");
                Require(nodes <= _report.warmUiElements + 50, "UI_ELEMENT_GROWTH");
            }
        }
        private static void RememberRetired(List<WeakReference> references, object value)
        {
            // Bounded diagnostics, never a strong owner of replaced campaign/UI state.
            if (references.Count == 512) references.RemoveAt(0);
            references.Add(new WeakReference(value));
        }
        private void WriteState()
        {
            Directory.CreateDirectory(_root);
            _report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - _started;
            _report.updatedUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(Path.Combine(_root, "player-soak.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_PLAYER_SOAK_HEARTBEAT cycles=" + _report.cycles + " seconds=" + _report.elapsedSeconds.ToString("F1", CultureInfo.InvariantCulture)
                + " managed=" + _report.lastManagedBytes + " monoUsed=" + _report.monoUsedBytes + " monoHeap=" + _report.monoHeapBytes
                + " retiredCampaigns=" + _report.retiredCampaignsAlive + "/" + _report.retiredCampaignsTracked
                + " retiredLists=" + _report.retiredFieldListsAlive + "/" + _report.retiredFieldListsTracked);
        }
        private void Finish(string? error)
        {
            if (_finished) return;
            _finished = true;
            UnityEngine.Application.logMessageReceived -= OnLog;
            _report.error = error ?? _unexpectedError ?? string.Empty;
            _report.status = string.IsNullOrEmpty(_report.error) ? (_memoryAudit || _allocationAudit ? "DIAGNOSTIC_COMPLETE" : "PASS") : "FAIL";
            if (_memoryAudit)
            {
                _report.auditFinalCollectedBytes = DiagnosticCollectedMemory();
                _report.auditFinalRetiredCampaignsAlive = _retiredCampaigns.Count(x => x.IsAlive);
                _report.auditFinalRetiredFieldListsAlive = _retiredFieldLists.Count(x => x.IsAlive);
            }
            var count = 0;
            for (var i = 0; i < _frameHistogram.Length; i++) { count += _frameHistogram[i]; if (count >= Math.Ceiling(_report.frames * .95)) { _report.p95FrameMilliseconds = i; break; } }
            _report.finalFingerprint = _bootstrap?.CurrentCampaign == null ? string.Empty : Fingerprint();
            try { WriteState(); }
            catch (Exception failure) { _report.status = "FAIL"; _report.error += " REPORT_WRITE_FAILED " + failure.Message; }
            ReleaseAllocationRecorders();
            Debug.Log("FOC_PLAYER_SOAK_" + _report.status + " cycles=" + _report.cycles + " seconds=" + _report.elapsedSeconds.ToString("F1", CultureInfo.InvariantCulture) + " error=" + _report.error);
            UnityEngine.Application.Quit(string.IsNullOrEmpty(_report.error) ? 0 : 1);
        }
        private void OnLog(string condition, string trace, LogType type)
        {
            if (condition.StartsWith("FOC_SAVE_UI ", StringComparison.Ordinal)) _report.saveFeedbackEvents++;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _unexpectedError ??= condition;
        }
        private void OnDisable() { UnityEngine.Application.logMessageReceived -= OnLog; ReleaseAllocationRecorders(); }
        private void InitializeAllocationRecorders()
        {
            // Raw per-allocation EVENT samples. Byte values come separately
            // from the frame counter. Bounded native storage; no managed array.
            _allocationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            _allocationRecorder.Stop();
            _frameAllocationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            Require(_allocationRecorder.Valid, "ALLOCATION_MARKER_UNAVAILABLE");
            _report.allocationMarkerUnit = _allocationRecorder.UnitType.ToString();
            Require(_frameAllocationRecorder.Valid && _frameAllocationRecorder.UnitType == ProfilerMarkerDataUnit.Bytes, "FRAME_ALLOCATION_COUNTER_UNAVAILABLE");
        }
        private void BeginAllocationMeasurement()
        {
            if (!_allocationAudit || _finished) return;
            _allocationRecorder.Reset();
            _allocationRecorder.Start();
        }
        private long EndAllocationMeasurement()
        {
            if (!_allocationAudit || _finished) return 0;
            _allocationRecorder.Stop();
            Require(_allocationRecorder.Count < _allocationRecorder.Capacity, "ALLOCATION_SAMPLE_CAPACITY_EXCEEDED");
            return _allocationRecorder.Count;
        }
        private IEnumerator CaptureAllocationProbe()
        {
            var expected = Fingerprint();
            var screens = new[] { "map", "city", "character", "organization", "trade", "army", "diplomacy", "battle", "reports", "ledger" };
            for (var round = 0; round < 5; round++)
            {
                // Frame windows include engine/layout work, not exclusively the
                // named callback. Compare like windows; do not infer live heap.
                yield return null;
                _report.probeIdleFrameBytes += _frameAllocationRecorder.LastValue;
                _report.probeIdleSamples++;
                foreach (var screen in screens)
                {
                    Activate(Document.Q<Button>("nav-" + screen));
                    yield return null;
                    _report.probeNavigationFrameBytes += _frameAllocationRecorder.LastValue;
                    _report.probeNavigationSamples++;
                }
                yield return null;
                Slot.value = "soak";
                Activate(Document.Q<Button>("save-campaign"));
                yield return null;
                _report.probeSaveFrameBytes += _frameAllocationRecorder.LastValue;
                _report.probeSaveSamples++;
                Require(Feedback("save-feedback") == "SAVED", "ALLOCATION_PROBE_SAVE_FAILED");
                Activate(Document.Q<Button>("load-campaign"));
                yield return null;
                _report.probeLoadFrameBytes += _frameAllocationRecorder.LastValue;
                _report.probeLoadSamples++;
                Require(Feedback("save-feedback") == "LOADED", "ALLOCATION_PROBE_LOAD_FAILED");
                Require(Fingerprint() == expected, "ALLOCATION_PROBE_ROUNDTRIP_MISMATCH");
                yield return null;
                Fingerprint();
                yield return null;
                _report.probeFingerprintFrameBytes += _frameAllocationRecorder.LastValue;
                _report.probeFingerprintSamples++;
            }
        }
        private void ReleaseAllocationRecorders()
        {
            if (_allocationRecorder.Valid) _allocationRecorder.Dispose();
            if (_frameAllocationRecorder.Valid) _frameAllocationRecorder.Dispose();
        }
        private static long DiagnosticCollectedMemory()
        {
            // Explicitly separate, opt-in diagnostic. Never executed by an acceptance run.
            // Compare collectable garbage with retained state without certifying this workload.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(false);
        }
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

        [Serializable]
        private sealed class Report
        {
            public string status = "RUNNING", error = string.Empty, startedUtc = string.Empty, updatedUtc = string.Empty, unityVersion = string.Empty, graphicsDevice = string.Empty, finalFingerprint = string.Empty;
            public int requestedSeconds, cycles, navigationChecks, travelChecks, rejectionChecks, roundtrips, uiActivations, frames, gc0, gc1, gc2, warmUiElements, maxUiElements, lifecycleChecks, saveFeedbackEvents;
            public double elapsedSeconds, maxFrameMilliseconds, p95FrameMilliseconds, maxUiOperationMilliseconds;
            public long peakNativeAllocatedBytes, peakManagedBytes, lastNativeAllocatedBytes, lastManagedBytes, nativeGrowthBytes, managedGrowthBytes;
            public long monoUsedBytes, monoHeapBytes;
            public int retiredCampaignsTracked, retiredCampaignsAlive, retiredFieldListsTracked, retiredFieldListsAlive;
            public bool memoryAudit;
            public bool allocationAudit;
            public string allocationMarkerUnit = string.Empty;
            public long auditWarmCollectedBytes, auditFinalCollectedBytes;
            public int auditFinalRetiredCampaignsAlive, auditFinalRetiredFieldListsAlive;
            public long idleMinimumManagedBytes;
            public int idleSamples;
            public long frameAllocatedBytes, navigationAllocationEvents, saveAllocationEvents, loadAllocationEvents, fingerprintAllocationEvents;
            public int navigationAllocationSamples, saveAllocationSamples, loadAllocationSamples, fingerprintAllocationSamples;
            public long probeIdleFrameBytes, probeNavigationFrameBytes, probeSaveFrameBytes, probeLoadFrameBytes, probeFingerprintFrameBytes;
            public int probeIdleSamples, probeNavigationSamples, probeSaveSamples, probeLoadSamples, probeFingerprintSamples;
        }
    }
}
