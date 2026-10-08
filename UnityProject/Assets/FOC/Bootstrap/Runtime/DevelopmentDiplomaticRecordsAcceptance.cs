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
    /// <summary>Opt-in isolated real-player read-only diplomatic records test. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentDiplomaticRecordsAcceptance : MonoBehaviour
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
            var host = GetComponent<PresentationRuntimeHost>(); var campaign = _bootstrap.CurrentCampaign!;
            var relation = campaign.Diplomacy.Relations.OrderedRelations.Single();
            var relationRef = new PresentationEntityRef(PresentationEntityKind.DiplomaticRelation, relation.Id.Value);
            var normal = Fingerprint();
            host.Open(new PresentationRoute(PresentationScreenId.Diplomacy, relationRef));
            yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<ListView>("diplomatic-rows").itemsSource.Count == 1, "NORMAL_RELATION_CHANGED");
            Require(Document.Q<Label>("diplomatic-detail-metadata").text.Contains("Gergin"), "NORMAL_TUTUM_MISSING");
            Capture("01-startup-relation.png");
            Select("diplomatic-type", "Anlaşmalar");
            Require(Document.Q<ListView>("diplomatic-rows").itemsSource.Count == 0, "STARTUP_TREATY_FIXTURE_LEAKED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("02-startup-empty-agreements.png");
            Require(Fingerprint() == normal, "NORMAL_INSPECTION_MUTATED_CAMPAIGN"); _report.normalStartupUnchanged = true;

            // Only this opt-in isolated diagnostic adds sample party records.
            var own = FactionId.Create("faction-ottoman-state"); var venice = FactionId.Create("faction-republic-of-venice");
            var other = FactionId.Create("s3-hidden-actor"); campaign.Diplomacy.Actors.Add(new FOC.Domain.Diplomacy.DiplomaticActorState(other, "Hidden test actor"));
            var pair = new FOC.Domain.Diplomacy.DiplomaticActorPair(own, venice);
            relation.AddFactor(new FOC.Domain.Diplomacy.DiplomaticFactor("s3-known-trade-ref", FOC.Domain.Diplomacy.DiplomaticFactorSource.TradeRelationship, FOC.Domain.Diplomacy.DiplomaticFactorDirection.Positive, campaign.Clock.Now));
            relation.AddFactor(new FOC.Domain.Diplomacy.DiplomaticFactor("s3-future-factor", FOC.Domain.Diplomacy.DiplomaticFactorSource.Insult, FOC.Domain.Diplomacy.DiplomaticFactorDirection.Negative, new WorldTimestamp(60)));
            campaign.Diplomacy.Relations.Add(new FOC.Domain.Diplomacy.DiplomaticRelationState(new FOC.Domain.Diplomacy.DiplomaticActorPair(other, venice), FOC.Domain.Diplomacy.DiplomaticDisposition.Hostile, campaign.Clock.Now));
            var agreement = new FOC.Domain.Diplomacy.DiplomaticAgreementState(DiplomaticAgreementId.Create("s3-isolated-trade"), pair, FOC.Domain.Diplomacy.DiplomaticAgreementKind.TradeAgreement,
                campaign.Clock.Now, new WorldTimestamp(10), new[] { new FOC.Domain.Diplomacy.DiplomaticAgreementTerm(FOC.Domain.Diplomacy.DiplomaticAgreementTermKind.MarketAccess), new FOC.Domain.Diplomacy.DiplomaticAgreementTerm(FOC.Domain.Diplomacy.DiplomaticAgreementTermKind.CaravanProtection) }, new WorldTimestamp(30));
            campaign.Diplomacy.Agreements.Add(agreement);
            campaign.Diplomacy.Agreements.Add(new FOC.Domain.Diplomacy.DiplomaticAgreementState(DiplomaticAgreementId.Create("s3-secret-agreement"), new FOC.Domain.Diplomacy.DiplomaticActorPair(other, venice),
                FOC.Domain.Diplomacy.DiplomaticAgreementKind.Alliance, campaign.Clock.Now, campaign.Clock.Now, new[] { new FOC.Domain.Diplomacy.DiplomaticAgreementTerm(FOC.Domain.Diplomacy.DiplomaticAgreementTermKind.MutualSupport) }));
            var before = Fingerprint(); var original = campaign;
            host.Open(new PresentationRoute(PresentationScreenId.Map)); host.Open(new PresentationRoute(PresentationScreenId.Diplomacy, relationRef));
            Select("diplomatic-type", "İlişkiler"); yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<ListView>("diplomatic-entries").itemsSource.Cast<string>().Single().Contains("s3-known-trade-ref"), "KNOWN_FACTOR_OR_FUTURE_BOUNDARY_FAILED");
            Capture("03-known-factor.png");
            Select("diplomatic-type", "Anlaşmalar"); yield return null; yield return new WaitForEndOfFrame();
            var row = Document.Q<ListView>("diplomatic-rows").Query<Button>().ToList().First(x => x.ClassListContains("foc-diplomatic-row"));
            Activate(row); yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<Label>("diplomatic-detail-title").text.Contains(agreement.Id.Value), "ACTUAL_ROW_SELECTION_FAILED");
            Require(Document.Q<Label>("diplomatic-detail-metadata").text.Contains("Şu anda yürürlükte: Hayır"), "FUTURE_EFFECTIVENESS_MISREPRESENTED");
            Require(Document.Q<ListView>("diplomatic-entries").itemsSource.Count == 2, "TERMS_MISSING");
            Capture("04-agreement-details.png");
            Document.Q<TextField>("diplomatic-search").value = "TİCARET";
            Require(Document.Q<ListView>("diplomatic-rows").itemsSource.Count == 1, "TURKISH_SEARCH_FAILED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("05-turkish-search.png");
            Document.Q<TextField>("diplomatic-search").value = "secret";
            Require(Document.Q<ListView>("diplomatic-rows").itemsSource.Count == 0, "FOREIGN_AGREEMENT_LEAK");
            Require(Document.Q<ListView>("diplomatic-entries").itemsSource == null, "OLD_DETAIL_REMAINED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("06-no-results.png");
            Document.Q<TextField>("diplomatic-search").value = "";
            for (var i = 0; i < 10; i++) { Activate("nav-map"); Activate("nav-diplomacy"); }
            Require(Document.Query<TextField>("diplomatic-search").ToList().Count == 1, "DUPLICATE_RECORD_CONTROLS");
            Require(Fingerprint() == before, "READ_ONLY_RECORD_FLOW_MUTATED_CAMPAIGN"); _report.readOnlyFlows++;
            Document.Q<TextField>("save-slot").value = "s3-diplomacy";
            Activate("save-campaign"); Require(Document.Q<Label>("save-feedback").text == "SAVED", "REAL_SAVE_FAILED");
            Activate("load-campaign"); yield return null;
            Require(Document.Q<Label>("save-feedback").text == "LOADED", "REAL_LOAD_FAILED");
            Require(!ReferenceEquals(original, _bootstrap.CurrentCampaign), "LOAD_DID_NOT_REBUILD_RUNTIME");
            Require(Fingerprint() == before, "LOAD_CHANGED_RECORDS");
            host.Open(new PresentationRoute(PresentationScreenId.Diplomacy, new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, agreement.Id.Value))); yield return null;
            Require(Document.Q<Label>("diplomatic-detail-metadata").text.Contains("Şu anda yürürlükte: Hayır"), "RELOADED_STATUS_CHANGED");
            Require(Document.Q<ListView>("diplomatic-entries").itemsSource.Count == 2, "RELOADED_TERMS_CHANGED");
            Require(Fingerprint() == before, "RELOADED_INSPECTION_MUTATED_CAMPAIGN"); _report.saveRoundtrips++;
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
            File.WriteAllText(Path.Combine(_root,"diplomatic-records.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_DIPLOMATIC_RECORDS_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Own-party relation records and signed agreements; no foreign-pair live knowledge or treaty effects";
            public bool normalStartupUnchanged;
            public string testSetup = "Opt-in isolated relation factors and signed party fixtures after unchanged historical startup. Not new agreement gameplay.";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, readOnlyFlows, saveRoundtrips, screenshots, saveVersion;
            public int screenWidth, screenHeight, renderWidth, renderHeight;
        }
    }
}
