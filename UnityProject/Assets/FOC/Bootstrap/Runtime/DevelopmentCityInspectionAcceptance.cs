#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using FOC.Application.Save;
using FOC.Application.Geography;
using FOC.Domain.Common;
using FOC.Domain.Time;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using FOC.Domain.Characters;
using FOC.Domain.Geography;
using FOC.Infrastructure.Save;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace FOC.Bootstrap.Unity
{
    /// <summary>Opt-in isolated real-player read-only city inspection test. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentCityInspectionAcceptance : MonoBehaviour
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
            _report.screenWidth = Screen.width; _report.screenHeight = Screen.height;
            _report.renderWidth = CaptureDimension("-screen-width", Screen.width);
            _report.renderHeight = CaptureDimension("-screen-height", Screen.height);
            _captureTarget = new RenderTexture(_report.renderWidth, _report.renderHeight, 0, RenderTextureFormat.ARGB32);
            _captureTarget.Create(); GetComponent<UIDocument>().panelSettings.targetTexture = _captureTarget;
            var host = GetComponent<PresentationRuntimeHost>();
            var before = Fingerprint();
            var original = _bootstrap.CurrentCampaign!;
            host.Open(Route("city-istanbul")); yield return null;
            Require(Document.Q<DropdownField>("city-selector").choices.Count == 2, "UNKNOWN_CITY_IN_CHOICES");
            for (var i = 0; i < 9; i++) Activate("city-area-" + i);
            Activate("city-area-6");
            Require(Document.Q<Label>("city-detail-heading").text.Contains("Üretim"), "AREA_NOT_SELECTED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("01-istanbul-areas.png");
            Activate("city-tab-1"); Select("city-recipe", "Deri Tabaklama");
            Require(Document.Q<Label>("city-production-reason").text.Contains("Girdi stoku yetersiz"), "BLOCKED_RECIPE_NOT_EXPLAINED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("02-istanbul-production-blocked.png");
            Select("city-selector", "Bursa"); Select("city-recipe", "İpekli Dokuma");
            Require(Document.Q<Label>("city-production-reason").text.StartsWith("UYGUN"), "VALID_RECIPE_NOT_EXPLAINED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("03-bursa-production-ready.png");
            Activate("city-tab-2"); Document.Q<TextField>("city-stock-search").value = "İPEK";
            Require(Document.Q<ListView>("city-inspection-rows").itemsSource.Count == 2, "SEARCH_NOT_APPLIED");
            Document.Q<Toggle>("city-stock-shortages").value = true;
            Require(Document.Q<Label>("city-detail-heading").text.StartsWith("0 "), "INVENTED_SHORTAGE");
            Document.Q<Toggle>("city-stock-shortages").value = false;
            yield return null; yield return new WaitForEndOfFrame(); Capture("04-stock-filter.png");
            Select("city-selector", "İstanbul"); Activate("city-tab-3");
            Require(Document.Q<Label>("city-inspection-note").text.Contains("görev referansı"), "OFFICIAL_REFERENCE_MISSING");
            Require(!Document.Q<Label>("city-inspection-note").text.Contains("ahmed-efendi"), "HIDDEN_OFFICIAL_LEAK");
            Require(Document.Q<ListView>("city-inspection-rows").itemsSource.Count == 9, "INFRASTRUCTURE_MISSING");
            yield return null; yield return new WaitForEndOfFrame(); Capture("05-infrastructure-and-officials.png");
            Activate("city-open-market"); yield return null;
            Require(Document.Q<DropdownField>("trade-market").value == "İstanbul", "MARKET_NOT_SAME_CITY");
            Activate("nav-back"); yield return null;
            Require(Document.Q<DropdownField>("city-selector").value == "İstanbul", "CITY_BACK_FAILED");
            Require(Fingerprint() == before, "READ_ONLY_FLOW_MUTATED_CAMPAIGN");
            _report.readOnlyFlows++;
            Document.Q<TextField>("save-slot").value = "y5a-city";
            Activate("save-campaign");
            Require(Document.Q<Label>("save-feedback").text == "SAVED", "REAL_SAVE_FAILED");
            Activate("load-campaign"); yield return null;
            Require(Document.Q<Label>("save-feedback").text == "LOADED", "REAL_LOAD_FAILED");
            Require(!ReferenceEquals(original, _bootstrap.CurrentCampaign), "LOAD_DID_NOT_REBUILD_RUNTIME");
            Require(Fingerprint() == before, "LOAD_CHANGED_CITY");
            host.Open(Route("city-bursa")); yield return null;
            Activate("city-tab-1"); Select("city-recipe", "Tahıldan Un");
            Require(Document.Q<Label>("city-production-reason").text.StartsWith("UYGUN"), "RELOADED_RECIPE_CHANGED");
            Require(Fingerprint() == before, "RELOADED_INSPECTION_MUTATED_CAMPAIGN");
            _report.saveRoundtrips++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("06-city-reloaded.png");
            var data = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!);
            Require(new CampaignSaveValidator().Validate(data).IsValid, "SAVE_INVALID");
            _report.finalFingerprint = Fingerprint();
            _report.saveVersion = data.SaveVersion;
            Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }
        private static PresentationRoute Route(string city) => new PresentationRoute(PresentationScreenId.City, new PresentationEntityRef(PresentationEntityKind.City, city));
        private void Select(string name, string label)
        {
            var field = Document.Q<DropdownField>(name);
            Require(field != null && field.choices.Contains(label), "CHOICE_MISSING_" + label);
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
            Require(_captureTarget != null, "RENDER_TARGET_MISSING");
            var texture = new Texture2D(_captureTarget!.width, _captureTarget.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _captureTarget;
                texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); texture.Apply();
                var pixels = texture.GetPixels32();
                var min = pixels.Min(x => (int)x.r + x.g + x.b); var max = pixels.Max(x => (int)x.r + x.g + x.b);
                Require(max - min > 30, "BLANK_RUNTIME_PANEL_CAPTURE");
                File.WriteAllBytes(Path.Combine(_root,file), texture.EncodeToPNG()); _report.screenshots++;
            }
            finally { RenderTexture.active = previous; Destroy(texture); }
        }
        private static int CaptureDimension(string name, int fallback)
        {
            var args=Environment.GetCommandLineArgs();
            var index=Array.FindIndex(args,x=>string.Equals(x,name,StringComparison.OrdinalIgnoreCase));
            if(index<0)return fallback;
            if(index+1>=args.Length||!int.TryParse(args[index+1],out var size)||size<640||size>4096)
                throw new InvalidOperationException("INVALID_CAPTURE_SIZE_"+name);
            return size;
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
            File.WriteAllText(Path.Combine(_root,"city-inspection.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_CITY_INSPECTION_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Exact known-city inspection only; no production execution, new cadence or authority";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, readOnlyFlows, saveRoundtrips, screenshots, saveVersion;
            public int screenWidth, screenHeight, renderWidth, renderHeight;
        }
    }
}
