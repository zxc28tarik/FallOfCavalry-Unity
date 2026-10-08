#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Characters;
using FOC.Domain.Geography;
using FOC.Infrastructure.Save;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace FOC.Bootstrap.Unity
{
    /// <summary>Opt-in isolated real-player UI journey. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentMapTravelAcceptance : MonoBehaviour
    {
        private DevelopmentCampaignBootstrap _bootstrap = null!;
        private string _root = string.Empty;
        private string? _unexpectedError;
        private RenderTexture? _captureTarget;
        private readonly Report _report = new Report();
        public void Configure(DevelopmentCampaignBootstrap bootstrap, string root)
        { _bootstrap = bootstrap; _root = root; Directory.CreateDirectory(_root); }
        private VisualElement Document => GetComponent<UIDocument>().rootVisualElement;
        private IEnumerator Start()
        {
            UnityEngine.Application.runInBackground = true;
            UnityEngine.Application.targetFrameRate = 30;
            UnityEngine.Application.logMessageReceived += OnLog;
            _report.unityVersion = UnityEngine.Application.unityVersion;
            _report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            var work = Exercise();
            while (true)
            {
                object? step;
                try { if (!work.MoveNext()) break; step = work.Current; }
                catch (Exception error) { Finish(error.Message); yield break; }
                yield return step;
            }
            Finish(_unexpectedError);
        }
        private IEnumerator Exercise()
        {
            yield return null; yield return new WaitForSecondsRealtime(1);
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "REAL_GRAPHICS_REQUIRED");
            Require(Document.panel != null, "UI_NOT_ATTACHED");
            // Batch players do not expose a readable system framebuffer. Render the actual shipped
            // UI Toolkit panel into its own D3D11 target; no alternate UI or mockup is constructed.
            _captureTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            _captureTarget.Create();
            GetComponent<UIDocument>().panelSettings.targetTexture = _captureTarget;
            yield return null; yield return new WaitForEndOfFrame();
            var campaign = _bootstrap.CurrentCampaign!;
            var before = Fingerprint();
            Select("Edirne");
            yield return null;
            Require(Fingerprint() == before, "SELECTION_MUTATED_CAMPAIGN");
            Require(Document.Q<Label>("travel-route").text.Contains("Edirne"), "PREVIEW_TARGET_WRONG");
            yield return new WaitForEndOfFrame(); Capture("01-edirne-preview.png");
            Activate("travel-start");
            Require(campaign.Geography.Travel.OrderedJourneys.Count == 1, "JOURNEY_NOT_STARTED");
            Require(!Document.Q<Button>("travel-start").enabledInHierarchy, "SECOND_START_ENABLED");
            Activate("travel-advance"); yield return null;
            Require(campaign.Clock.Now.Ticks == 3600, "CLOCK_NOT_ONE_HOUR");
            Require(campaign.Characters.GetRequired(CharacterId.Create("hasan-aga")).Location.Kind == CharacterLocationKind.Travelling, "NO_REAL_MOVEMENT");
            Document.Q<TextField>("save-slot").value = "y1-travel";
            before = Fingerprint();
            Activate("save-campaign"); Activate("load-campaign");
            Require(Fingerprint() == before, "MID_JOURNEY_RELOAD_MISMATCH");
            Require(!ReferenceEquals(campaign, _bootstrap.CurrentCampaign), "LOAD_DID_NOT_RECONSTRUCT");
            _report.saveRoundtrips++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("02-reloaded-journey.png");
            foreach (var unused in AdvanceUntilArrival()) yield return unused;
            Require(_bootstrap.CurrentCampaign!.Characters.GetRequired(CharacterId.Create("hasan-aga")).Location.CityId!.Value.Value == "city-edirne", "EDIRNE_ARRIVAL_WRONG");
            _report.arrivals++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("03-edirne-arrival.png");
            Select("İstanbul"); yield return null;
            Require(Document.Q<Button>("travel-start").enabledInHierarchy, "ISTANBUL_RETURN_NOT_OFFERED");
            yield return new WaitForEndOfFrame(); Capture("04-return-preview.png");
            Select("Çorlu"); yield return null; Activate("travel-start");
            foreach (var unused in AdvanceUntilArrival()) yield return unused;
            Require(_bootstrap.CurrentCampaign!.Characters.GetRequired(CharacterId.Create("hasan-aga")).Location.Kind == CharacterLocationKind.WorldPosition, "WAYSTATION_ARRIVAL_WRONG");
            _report.arrivals++;
            before = Fingerprint(); Activate("save-campaign"); Activate("load-campaign");
            Require(Fingerprint() == before, "WAYSTATION_RELOAD_MISMATCH"); _report.saveRoundtrips++;
            yield return null; Select("İstanbul"); yield return null;
            Require(Document.Q<Button>("travel-start").enabledInHierarchy, "WAYSTATION_DEPARTURE_BLOCKED");
            yield return new WaitForEndOfFrame(); Capture("05-waystation-return.png");
            Activate("travel-start");
            foreach (var unused in AdvanceUntilArrival()) yield return unused;
            Require(_bootstrap.CurrentCampaign!.Characters.GetRequired(CharacterId.Create("hasan-aga")).Location.CityId!.Value.Value == "city-istanbul", "ISTANBUL_ARRIVAL_WRONG");
            _report.arrivals++;
            Require(!Document.Q<Button>("travel-start").enabledInHierarchy, "SAME_DESTINATION_ENABLED");
            Require(!Document.Q<Button>("travel-advance").enabledInHierarchy, "IDLE_ADVANCE_ENABLED");
            before = Fingerprint(); Activate("save-campaign"); Activate("load-campaign");
            Require(Fingerprint() == before, "ARRIVAL_RELOAD_MISMATCH"); _report.saveRoundtrips++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("06-istanbul-arrival.png");
            _report.finalFingerprint = before;
            _report.saveVersion = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!).SaveVersion;
            Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }
        private IEnumerable AdvanceUntilArrival()
        {
            var steps = 0;
            while (_bootstrap.CurrentCampaign!.Geography.Travel.OrderedJourneys.Any(x => x.Lifecycle == TravelLifecycle.Active))
            {
                Require(steps++ < 100, "ARRIVAL_DID_NOT_TERMINATE");
                Activate("travel-advance"); _report.hourSteps++; yield return null;
            }
        }
        private void Select(string label)
        {
            var field = Document.Q<DropdownField>("travel-destination");
            Require(field != null && field.choices.Contains(label), "DESTINATION_MISSING_" + label);
            field!.value = label; _report.selections++;
        }
        private void Activate(string name)
        {
            var button = Document.Q<Button>(name);
            Require(button != null && button.enabledInHierarchy && button.panel != null, "CONTROL_UNAVAILABLE_" + name);
            button!.Focus();
            using var submit = NavigationSubmitEvent.GetPooled(); button.SendEvent(submit);
            _report.activations++;
        }
        private string Fingerprint() => SavePayloadFingerprint.Compute(new CampaignSaveTextSerializer(), CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!));
        private void Capture(string file)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _captureTarget;
                texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); texture.Apply();
                var pixels = texture.GetPixels32();
                var min = pixels.Min(x => (int)x.r + x.g + x.b); var max = pixels.Max(x => (int)x.r + x.g + x.b);
                Require(max - min > 30, "BLANK_RUNTIME_PANEL_CAPTURE");
                File.WriteAllBytes(Path.Combine(_root,file), texture.EncodeToPNG()); _report.screenshots++;
            }
            finally { RenderTexture.active = previous; Destroy(texture); }
        }
        private void OnLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _unexpectedError ??= message; }
        private void OnDestroy()
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
            if (_captureTarget != null) { GetComponent<UIDocument>().panelSettings.targetTexture = null; _captureTarget.Release(); Destroy(_captureTarget); }
        }
        private void Finish(string? error)
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
            _report.status = error == null ? "PASS" : "FAIL"; _report.error = error ?? string.Empty;
            File.WriteAllText(Path.Combine(_root,"map-travel.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_MAP_TRAVEL_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, hourSteps, arrivals, saveRoundtrips, screenshots, saveVersion;
        }
    }
}
