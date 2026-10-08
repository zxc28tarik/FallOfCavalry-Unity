#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using FOC.Application.Save;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace FOC.Bootstrap.Unity
{
    /// <summary>Opt-in real-player read-only character directory test. Synthetic UI input is not physical OS input acceptance.</summary>
    public sealed class DevelopmentCharacterDirectoryAcceptance : MonoBehaviour
    {
        private DevelopmentCampaignBootstrap _bootstrap = null!;
        private string _root = string.Empty;
        private string? _unexpectedError;
        private RenderTexture? _target;
        private readonly Report _report = new Report();
        public void Configure(DevelopmentCampaignBootstrap bootstrap, string root) { _bootstrap = bootstrap; _root = root; Directory.CreateDirectory(root); }
        private VisualElement Document => GetComponent<UIDocument>().rootVisualElement;

        private IEnumerator Start()
        {
            UnityEngine.Application.runInBackground = true; UnityEngine.Application.targetFrameRate = 30; UnityEngine.Application.logMessageReceived += OnLog;
            _report.unityVersion = UnityEngine.Application.unityVersion; _report.graphicsDevice = SystemInfo.graphicsDeviceType.ToString();
            var work = Exercise();
            while (true) { object? step; try { if (!work.MoveNext()) break; step = work.Current; } catch (Exception e) { Finish(e.Message); yield break; } yield return step; }
            Finish(_unexpectedError);
        }

        private IEnumerator Exercise()
        {
            yield return null; yield return new WaitForSecondsRealtime(1);
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "REAL_GRAPHICS_REQUIRED"); Require(Document.panel != null, "UI_NOT_ATTACHED");
            _report.width = CaptureDimension("-screen-width", Screen.width); _report.height = CaptureDimension("-screen-height", Screen.height);
            _target = new RenderTexture(_report.width, _report.height, 0, RenderTextureFormat.ARGB32); _target.Create(); GetComponent<UIDocument>().panelSettings.targetTexture = _target;
            var host = GetComponent<PresentationRuntimeHost>(); var before = Fingerprint();
            host.Open(new PresentationRoute(PresentationScreenId.Character, new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga")));
            yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<ListView>("character-directory-rows").itemsSource.Count == 3, "KNOWN_CHARACTER_COUNT_CHANGED");
            Require(Document.Q<Label>("character-directory-summary").text.Contains("Hasan Ağa") && Document.Q<Label>("character-directory-summary").text.Contains("Yaralanma yok"), "HASAN_DETAIL_MISSING");
            Require(Current("presentation.character.location").Contains("Şehir:"), "CONCRETE_LOCATION_MISSING");
            Require(Current("presentation.character.injury").Contains("Yaralanma yok"), "INJURY_DETAIL_MISSING");
            Require(Current("presentation.character.captivity").Contains("Esir değil"), "CAPTIVITY_DETAIL_MISSING"); Capture("01-hasan-details.png");

            Document.Q<TextField>("character-search").value = "ÇAVUŞ"; Require(Document.Q<ListView>("character-directory-rows").itemsSource.Count == 1, "TURKISH_SEARCH_FAILED");
            Activate(Document.Q<ListView>("character-directory-rows").Query<Button>().ToList().Single()); yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<Label>("character-directory-summary").text.Contains("Ali Çavuş"), "ROW_SELECTION_FAILED"); Capture("02-ali-selected.png");
            Document.Q<TextField>("character-search").value = "YUSUF"; Require(Document.Q<ListView>("character-directory-rows").itemsSource.Count == 0, "UNKNOWN_CHARACTER_LEAK"); Capture("03-hidden-search-empty.png");
            Document.Q<TextField>("character-search").value = "";
            for (var i = 0; i < 10; i++) { Activate(Document.Q<Button>("nav-map")); Activate(Document.Q<Button>("nav-character")); }
            Require(Document.Query<TextField>("character-search").ToList().Count == 1, "DUPLICATE_CHARACTER_CONTROLS"); Require(Fingerprint() == before, "READ_ONLY_FLOW_MUTATED_CAMPAIGN"); _report.readOnlyFlows++;
            Document.Q<TextField>("save-slot").value = "s4-character"; Activate(Document.Q<Button>("save-campaign")); Require(Document.Q<Label>("save-feedback").text == "SAVED", "REAL_SAVE_FAILED");
            Activate(Document.Q<Button>("load-campaign")); yield return null; Require(Document.Q<Label>("save-feedback").text == "LOADED", "REAL_LOAD_FAILED");
            host = GetComponent<PresentationRuntimeHost>(); host.Open(new PresentationRoute(PresentationScreenId.Character, new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"))); yield return null;
            Require(Document.Q<ListView>("character-directory-rows").itemsSource.Count == 3, "RELOADED_CHARACTER_COUNT_CHANGED"); Require(Fingerprint() == before, "LOAD_CHANGED_CHARACTER_STATE"); _report.saveRoundtrips++;
            yield return new WaitForEndOfFrame(); Capture("04-reloaded-hasan.png");
            var data = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!); Require(new CampaignSaveValidator().Validate(data).IsValid, "SAVE_INVALID");
            _report.finalFingerprint = Fingerprint(); _report.saveVersion = data.SaveVersion; Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }

        private string Current(string label)
        {
            return Document.Q<VisualElement>("current-content").Q<ListView>().itemsSource.Cast<PresentationField>().Single(x => x.LabelKey == label).DisplayValue;
        }
        private static void Activate(Button button) { if (button == null || !button.enabledInHierarchy || button.panel == null) throw new InvalidOperationException("CONTROL_UNAVAILABLE"); button.Focus(); using var submit = NavigationSubmitEvent.GetPooled(); button.SendEvent(submit); }
        private string Fingerprint() => SavePayloadFingerprint.Compute(new CampaignSaveTextSerializer(), CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!));
        private void Capture(string file)
        {
            Require(_target != null, "RENDER_TARGET_MISSING"); var texture = new Texture2D(_target!.width, _target.height, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try { RenderTexture.active = _target; texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply(); var p = texture.GetPixels32(); Require(p.Max(x => (int)x.r + x.g + x.b) - p.Min(x => (int)x.r + x.g + x.b) > 30, "BLANK_CAPTURE"); File.WriteAllBytes(Path.Combine(_root, file), texture.EncodeToPNG()); _report.screenshots++; }
            finally { RenderTexture.active = previous; Destroy(texture); }
        }
        private static int CaptureDimension(string name, int fallback) { var args = Environment.GetCommandLineArgs(); var i = Array.FindIndex(args, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)); if (i < 0) return fallback; if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var size) || size < 640 || size > 4096) throw new InvalidOperationException("INVALID_CAPTURE_SIZE"); return size; }
        private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _unexpectedError ??= message; }
        private void OnDestroy() { UnityEngine.Application.logMessageReceived -= OnLog; if (_target != null) { GetComponent<UIDocument>().panelSettings.targetTexture = null; _target.Release(); Destroy(_target); } }
        private void Finish(string? error) { UnityEngine.Application.logMessageReceived -= OnLog; _report.status = error == null ? "PASS" : "FAIL"; _report.error = error ?? ""; File.WriteAllText(Path.Combine(_root, "character-directory.json"), JsonUtility.ToJson(_report, true)); Debug.Log("FOC_CHARACTER_DIRECTORY_" + _report.status + " root=" + _root + " error=" + error); UnityEngine.Application.Quit(error == null ? 0 : 1); }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }

        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Exact-readable character identity, location, assignments, health and captivity; no portraits or hidden-person live knowledge";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events; not physical OS input";
            public int readOnlyFlows, saveRoundtrips, screenshots, saveVersion, width, height;
        }
    }
}
