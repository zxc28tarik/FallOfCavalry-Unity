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
    /// <summary>Opt-in isolated real-player read-only envoy tracking test. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentEnvoyTrackingAcceptance : MonoBehaviour
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
            host.Open(new PresentationRoute(PresentationScreenId.Diplomacy)); yield return null;
            var normal = Fingerprint();
            Require(Document.Q<ListView>("envoy-inbox-rows").itemsSource.Count == 0, "STARTUP_MISSION_FIXTURE_LEAKED");
            Require(Document.Q<Label>("envoy-inbox-empty").text.Contains("Henüz"), "STARTUP_EMPTY_MESSAGE_MISSING");
            yield return new WaitForEndOfFrame(); Capture("01-startup-empty.png");
            Require(Fingerprint() == normal, "EMPTY_INSPECTION_MUTATED_CAMPAIGN");
            _report.normalStartupUnchanged = true;

            // Opt-in isolated fixture. Normal campaign and bootstrap never register these orders.
            var campaign = _bootstrap.CurrentCampaign!;
            var mission = AddMission(campaign, "s2-isolated-demand");
            var service = new FOC.Application.Diplomacy.DiplomacyCommandService(campaign);
            campaign.Clock.Advance(new WorldDuration(1));
            service.Dispatch(DiplomaticActionId.Create("action-" + mission.Id.Value), campaign.Clock.Now);
            campaign.Clock.Advance(new WorldDuration(59)); service.Arrive(mission.Id, campaign.Clock.Now);
            service.DeliverAndResolve(DiplomaticActionId.Create("action-" + mission.Id.Value), FOC.Domain.Diplomacy.DiplomaticActionOutcome.Accepted, campaign.Clock.Now);
            mission.AwaitAudience(); // These remote events must not reach the player's mission projection.
            AddMission(campaign, "s2-foreign-secret", true);
            host.Open(new PresentationRoute(PresentationScreenId.Map)); host.Open(new PresentationRoute(PresentationScreenId.Diplomacy));
            yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<ListView>("envoy-inbox-rows").itemsSource.Count == 1, "FOREIGN_MISSION_LEAKED");
            Require(Document.Q<Label>("envoy-detail-metadata").text.Contains("Sevk edildi; uzak durum bilinmiyor"), "REMOTE_PHASE_LEAKED");
            Require(Document.Q<ListView>("envoy-messages").itemsSource.Cast<string>().Single().Contains("teslim teyidi: bilinmiyor"), "REMOTE_DELIVERY_LEAKED");
            Capture("02-remote-unknown.png");

            var reply = new FOC.Domain.Diplomacy.DiplomaticMessageState(DiplomaticMessageId.Create("s2-received-response"), mission.TargetActorId, mission.SourceActorId,
                FOC.Domain.Diplomacy.MessageKind.Response, FOC.Domain.Diplomacy.MessageCarrierKind.MessengerCharacter, CharacterId.Create("mehmed-celebi-tacir"),
                campaign.Clock.Now, responseTo: DiplomaticMessageId.Create("message-" + mission.Id.Value));
            campaign.Diplomacy.Messages.Add(reply); reply.Dispatch(campaign.Clock.Now);
            campaign.Clock.Advance(new WorldDuration(1)); reply.Deliver(mission.SourceActorId, campaign.Clock.Now);
            var pending = AddMission(campaign, "s2-isolated-message");
            var before = Fingerprint(); var original = campaign;
            host.Open(new PresentationRoute(PresentationScreenId.Map)); host.Open(new PresentationRoute(PresentationScreenId.Diplomacy));
            yield return null;
            Select("envoy-type", "Sevk edildi; uzak durum bilinmiyor"); yield return null; yield return new WaitForEndOfFrame();
            var row = Document.Q<ListView>("envoy-inbox-rows").Query<Button>().ToList().First(x => x.ClassListContains("foc-envoy-row"));
            Activate(row); yield return null; yield return new WaitForEndOfFrame();
            Require(Document.Q<Label>("envoy-detail-title").text.Contains(mission.Id.Value), "ACTUAL_ROW_SELECTION_FAILED");
            Require(Document.Q<ListView>("envoy-messages").itemsSource.Cast<string>().Any(x => x.Contains("s2-received-response")), "RECEIVED_RESPONSE_MISSING");
            Capture("03-linked-response.png");

            Document.Q<TextField>("envoy-search").value = "İLETME";
            Require(Document.Q<ListView>("envoy-inbox-rows").itemsSource.Count == 1, "TURKISH_SEARCH_FAILED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("04-turkish-search.png");
            Select("envoy-type", "Tüm bilinen durumlar"); Document.Q<TextField>("envoy-search").value = "secret";
            Require(Document.Q<ListView>("envoy-inbox-rows").itemsSource.Count == 0, "FOREIGN_MISSION_SEARCH_LEAK");
            Require(Document.Q<ListView>("envoy-messages").itemsSource == null, "OLD_DETAIL_REMAINED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("05-no-results.png");
            Document.Q<TextField>("envoy-search").value = ""; Select("envoy-type", "Sevk bekliyor");
            Require(Document.Q<Label>("envoy-detail-title").text.Contains(pending.Id.Value), "PENDING_FILTER_FAILED");
            Require(Document.Q<VisualElement>("action-content").Query<Button>().ToList().All(x => !x.enabledSelf), "MISSING_DISPATCH_ADAPTER_ENABLED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("06-local-order.png");
            for (var i = 0; i < 10; i++) { Activate("nav-map"); Activate("nav-diplomacy"); }
            yield return null;
            Require(Document.Query<TextField>("envoy-search").ToList().Count == 1, "DUPLICATE_ENVOY_CONTROLS");
            Require(Fingerprint() == before, "READ_ONLY_ENVOY_FLOW_MUTATED_CAMPAIGN");
            _report.readOnlyFlows++;
            Document.Q<TextField>("save-slot").value = "s2-envoys";
            Activate("save-campaign"); Require(Document.Q<Label>("save-feedback").text == "SAVED", "REAL_SAVE_FAILED");
            Activate("load-campaign"); yield return null;
            Require(Document.Q<Label>("save-feedback").text == "LOADED", "REAL_LOAD_FAILED");
            Require(!ReferenceEquals(original, _bootstrap.CurrentCampaign), "LOAD_DID_NOT_REBUILD_RUNTIME");
            Require(Fingerprint() == before, "LOAD_CHANGED_MISSION_RECORDS");
            host.Open(new PresentationRoute(PresentationScreenId.Diplomacy, new PresentationEntityRef(PresentationEntityKind.EnvoyMission, mission.Id.Value))); yield return null;
            Require(Document.Q<Label>("envoy-detail-metadata").text.Contains("Sevk edildi; uzak durum bilinmiyor"), "RELOADED_KNOWLEDGE_CHANGED");
            Require(Document.Q<ListView>("envoy-messages").itemsSource.Count == 2, "RELOADED_RESPONSE_CHANGED");
            Require(Fingerprint() == before, "RELOADED_INSPECTION_MUTATED_CAMPAIGN");
            _report.saveRoundtrips++;
            yield return new WaitForEndOfFrame(); Capture("07-reloaded-detail.png");
            var data = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!);
            Require(new CampaignSaveValidator().Validate(data).IsValid, "SAVE_INVALID");
            _report.finalFingerprint = Fingerprint(); _report.saveVersion = data.SaveVersion;
            Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }
        private static FOC.Domain.Diplomacy.EnvoyMissionState AddMission(FOC.Domain.Campaign.CampaignRuntimeState campaign, string id, bool foreign = false)
        {
            var org = new FOC.Domain.Organizations.OrganizationState(OrganizationId.Create("org-" + id), "Isolated acceptance office");
            var assignment = AssignmentId.Create("assignment-" + id);
            org.AddAssignment(new FOC.Domain.Organizations.AssignmentState(assignment, CharacterId.Create("yusuf-katip"), FOC.Domain.Organizations.OrganizationBranch.Diplomacy,
                "test-envoy", FOC.Domain.Organizations.AssignmentAuthority.Responsible, FOC.Domain.Organizations.AssignmentTarget.Organization(org.Id),
                FOC.Domain.Organizations.AssignmentPresence.RemoteCapable, campaign.Clock.Now));
            campaign.Organizations.Add(org);
            var own = FactionId.Create("faction-ottoman-state"); var other = FactionId.Create("faction-republic-of-venice");
            var source = foreign ? other : own; var target = foreign ? own : other;
            var mission = new FOC.Domain.Diplomacy.EnvoyMissionState(EnvoyMissionId.Create(id), CharacterId.Create("yusuf-katip"), org.Id, assignment,
                source, target, FOC.Domain.Diplomacy.EnvoyMissionType.DeliverDemand, new FOC.Domain.Diplomacy.DiplomaticMandate(FOC.Domain.Diplomacy.DiplomaticAuthorityScope.Negotiate,
                    new[] { FOC.Domain.Diplomacy.DiplomaticActionKind.Demand }), campaign.Clock.Now);
            var message = new FOC.Domain.Diplomacy.DiplomaticMessageState(DiplomaticMessageId.Create("message-" + id), source, target,
                FOC.Domain.Diplomacy.MessageKind.Demand, FOC.Domain.Diplomacy.MessageCarrierKind.EnvoyMission, mission.CharacterId, campaign.Clock.Now, mission.Id);
            var action = new FOC.Domain.Diplomacy.DiplomaticActionState(DiplomaticActionId.Create("action-" + id), source, target,
                FOC.Domain.Diplomacy.DiplomaticActionKind.Demand, mission.Id, message.Id, campaign.Clock.Now);
            new FOC.Application.Diplomacy.DiplomacyCommandService(campaign).RegisterOrder(mission, message, action);
            return mission;
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
            File.WriteAllText(Path.Combine(_root,"envoy-tracking.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_ENVOY_TRACKING_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Own mission orders plus explicitly delivered linked responses; no remote phase or delivery omniscience";
            public bool normalStartupUnchanged;
            public string testSetup = "Opt-in isolated orders through existing DiplomacyCommandService after normal empty startup. Not new envoy dispatch gameplay.";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, readOnlyFlows, saveRoundtrips, screenshots, saveVersion;
            public int screenWidth, screenHeight, renderWidth, renderHeight;
        }
    }
}
