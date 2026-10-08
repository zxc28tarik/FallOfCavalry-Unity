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
    /// <summary>Opt-in isolated real-player read-only report inbox test. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentReportInboxAcceptance : MonoBehaviour
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
            host.Open(new PresentationRoute(PresentationScreenId.Reports)); yield return null;
            var normal = Fingerprint();
            Require(Document.Q<ListView>("report-inbox-rows").itemsSource.Count == 0, "UNDELIVERED_STARTUP_REPORT_LEAKED");
            Require(Document.Q<Label>("report-inbox-empty").text.Contains("Henüz"), "STARTUP_EMPTY_MESSAGE_MISSING");
            yield return new WaitForEndOfFrame(); Capture("01-startup-empty.png");
            Require(Fingerprint() == normal, "EMPTY_INSPECTION_MUTATED_CAMPAIGN");
            _report.normalStartupUnchanged = true;

            // Explicit opt-in isolated test setup only. No production bootstrap report delivery.
            var campaign = _bootstrap.CurrentCampaign!;
            var report = campaign.Diplomacy.Reports.OrderedReports.Single();
            var delivery = new FOC.Application.Diplomacy.ReportDeliveryService(campaign);
            delivery.Dispatch(report.Id, campaign.Clock.Now); campaign.Clock.Advance(new WorldDuration(60));
            delivery.Deliver(report.Id, report.RecipientActorId, campaign.Clock.Now);
            var trade = new FOC.Domain.Diplomacy.ReportState(ReportId.Create("s1-isolated-trade-report"), FOC.Domain.Diplomacy.ReportType.Trade,
                FOC.Domain.Diplomacy.ReportSourceRef.Character(FOC.Domain.Diplomacy.ReportSourceKind.Merchant, CharacterId.Create("mehmed-celebi-tacir")),
                report.RecipientActorId, FOC.Domain.Diplomacy.ReportQuality.Low, FOC.Domain.Diplomacy.ReportDetailLevel.Summary, new WorldTimestamp(30),
                new[] { new FOC.Domain.Diplomacy.ReportObservation(new FOC.Domain.Diplomacy.ReportSubjectRef(FOC.Domain.Diplomacy.ReportSubjectKind.Market, "city-bursa"),
                    FOC.Domain.Diplomacy.ReportObservationKind.MarketCondition, FOC.Domain.Diplomacy.ObservationPrecision.Range, 10, 20) });
            campaign.Diplomacy.Reports.Add(trade); delivery.Dispatch(trade.Id, campaign.Clock.Now); campaign.Clock.Advance(new WorldDuration(1));
            delivery.Deliver(trade.Id, trade.RecipientActorId, campaign.Clock.Now);
            var foreign = new FOC.Domain.Diplomacy.ReportState(ReportId.Create("s1-foreign-secret"), FOC.Domain.Diplomacy.ReportType.City,
                report.Source, FactionId.Create("faction-republic-of-venice"), FOC.Domain.Diplomacy.ReportQuality.High, FOC.Domain.Diplomacy.ReportDetailLevel.Detailed,
                new WorldTimestamp(0), report.OrderedObservations);
            campaign.Diplomacy.Reports.Add(foreign); delivery.Dispatch(foreign.Id, campaign.Clock.Now); campaign.Clock.Advance(new WorldDuration(1));
            delivery.Deliver(foreign.Id, foreign.RecipientActorId, campaign.Clock.Now);
            var before = Fingerprint(); var original = campaign;
            host.Open(new PresentationRoute(PresentationScreenId.Map)); host.Open(new PresentationRoute(PresentationScreenId.Reports)); yield return null;
            Require(Document.Q<ListView>("report-inbox-rows").itemsSource.Count == 2, "DELIVERED_INBOX_COUNT_INVALID");
            Require(Document.Q<Label>("report-detail-metadata").text.Contains("gözlemden itibaren"), "STALE_OBSERVATION_PROVENANCE_MISSING");
            Require(Document.Q<Label>("report-detail-title").text.Contains(trade.Id.Value), "LATEST_DELIVERY_NOT_FIRST");
            yield return new WaitForEndOfFrame(); Capture("02-delivered-inbox.png");
            Select("report-type", "Şehir"); yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<ListView>("report-inbox-rows").itemsSource.Count == 1, "TYPE_FILTER_FAILED");
            var row = Document.Q<ListView>("report-inbox-rows").Query<Button>().ToList().First(x => x.ClassListContains("foc-report-row"));
            Activate(row); yield return null;
            Require(Document.Q<Label>("report-detail-title").text.Contains(report.Id.Value), "ACTUAL_ROW_SELECTION_FAILED");
            yield return new WaitForEndOfFrame();
            Capture("03-city-detail.png");
            Document.Q<TextField>("report-search").value = "İSTANBUL";
            Require(Document.Q<ListView>("report-inbox-rows").itemsSource.Count == 1, "TURKISH_SEARCH_FAILED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("04-turkish-search.png");
            Select("report-type", "Tüm türler"); Document.Q<TextField>("report-search").value = "secret";
            Require(Document.Q<ListView>("report-inbox-rows").itemsSource.Count == 0, "FOREIGN_REPORT_SEARCH_LEAK");
            Require(Document.Q<ListView>("report-observations").itemsSource == null, "OLD_DETAIL_REMAINED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("05-no-results.png");
            Document.Q<TextField>("report-search").value = ""; Select("report-type", "Ticaret");
            Require(Document.Q<ListView>("report-observations").itemsSource.Cast<string>().Single().Contains("Aralık · 10–20"), "RANGE_PRECISION_LOST");
            yield return null; yield return new WaitForEndOfFrame(); Capture("06-trade-range-detail.png");
            for (var i = 0; i < 10; i++) { Activate("nav-map"); Activate("nav-reports"); }
            yield return null;
            Require(Document.Query<TextField>("report-search").ToList().Count == 1, "DUPLICATE_REPORT_CONTROLS");
            Require(Fingerprint() == before, "READ_ONLY_REPORT_FLOW_MUTATED_CAMPAIGN");
            _report.readOnlyFlows++;
            Document.Q<TextField>("save-slot").value = "s1-reports";
            Activate("save-campaign"); Require(Document.Q<Label>("save-feedback").text == "SAVED", "REAL_SAVE_FAILED");
            Activate("load-campaign"); yield return null;
            Require(Document.Q<Label>("save-feedback").text == "LOADED", "REAL_LOAD_FAILED");
            Require(!ReferenceEquals(original, _bootstrap.CurrentCampaign), "LOAD_DID_NOT_REBUILD_RUNTIME");
            Require(Fingerprint() == before, "LOAD_CHANGED_INFORMATION");
            host.Open(new PresentationRoute(PresentationScreenId.Reports, new PresentationEntityRef(PresentationEntityKind.Report, report.Id.Value))); yield return null;
            Require(Document.Q<Label>("report-detail-metadata").text.Contains("yusuf-katip"), "RELOADED_SOURCE_MISSING");
            Require(Document.Q<ListView>("report-inbox-rows").itemsSource.Count == 2, "RELOADED_INBOX_CHANGED");
            Require(Fingerprint() == before, "RELOADED_INSPECTION_MUTATED_CAMPAIGN");
            _report.saveRoundtrips++;
            yield return new WaitForEndOfFrame(); Capture("07-reloaded-detail.png");
            var data = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!);
            Require(new CampaignSaveValidator().Validate(data).IsValid, "SAVE_INVALID");
            _report.finalFingerprint = Fingerprint(); _report.saveVersion = data.SaveVersion;
            Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }
        private void Select(string name, string label)
        {
            var field = Document.Q<DropdownField>(name);
            Require(field != null && field.choices.Contains(label), "CHOICE_MISSING_" + label);
            field!.value = label; _report.selections++;
        }
        private void Activate(string name)
        {
            Activate(Document.Q<Button>(name));
        }
        private void Activate(Button button)
        {
            Require(button != null && button.enabledInHierarchy && button.panel != null, "CONTROL_UNAVAILABLE");
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
            File.WriteAllText(Path.Combine(_root,"report-inbox.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_REPORT_INBOX_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Only delivered own-actor information; no live world enrichment, new cadence or authority";
            public bool normalStartupUnchanged;
            public string testSetup = "Opt-in isolated delivery through existing ReportDeliveryService after verifying normal startup empty inbox. Not production report dispatch gameplay.";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, readOnlyFlows, saveRoundtrips, screenshots, saveVersion;
            public int screenWidth, screenHeight, renderWidth, renderHeight;
        }
    }
}
