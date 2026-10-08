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
    /// <summary>Opt-in isolated real-player UI army test. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentArmyAcceptance : MonoBehaviour
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
            host.Open(Route());
            yield return null;
            var before = Fingerprint();
            var c = _bootstrap.CurrentCampaign!;
            var army = c.Military.Armies.GetRequired(ArmyId.Create("army-hasan-retinue"));
            var source = c.Military.RecruitmentSources.GetRequired(RecruitmentSourceId.Create("recruitment-hasan-retinue"));
            var grain = TradeGoodId.Create("grain");
            var stock = c.Economy.GetRequiredMarket(CityId.Create("city-istanbul")).Stock;
            var personnel = army.Headcount + source.AvailableHeadcount;
            var goods = army.Supply.QuantityOf(grain) + stock.QuantityOf(grain);
            Document.Q<TextField>("army-quantity").value = "2";
            Select("army-detail", "Cebeli");
            Require(Fingerprint() == before, "SELECTION_MUTATED_CAMPAIGN");
            Activate("army-prepare");
            Require(Fingerprint() == before, "PREVIEW_MUTATED_CAMPAIGN");
            yield return null; yield return new WaitForEndOfFrame(); Capture("01-recruit-confirmation.png");
            Activate("army-confirm");
            Require(army.Headcount == 14 && source.AvailableHeadcount == 18, "RECRUITMENT_NOT_APPLIED");
            Require(army.Headcount + source.AvailableHeadcount == personnel, "PERSONNEL_NOT_CONSERVED");
            Require(c.Soldiers.Soldiers.OrderedSoldiers.Count == 6, "UNAUTHORIZED_SOLDIER_CREATION");
            Require(!Document.Q<Button>("army-confirm").enabledInHierarchy, "DUPLICATE_CONFIRM_ENABLED");
            _report.recruitments++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("02-recruit-result.png");
            Document.Q<TextField>("army-quantity").value = "-1";
            Require(!Document.Q<Button>("army-prepare").enabledInHierarchy, "INVALID_QUANTITY_ENABLED");
            Document.Q<TextField>("army-quantity").value = "1";
            Activate("army-prepare"); Activate("army-cancel");
            Require(!Document.Q<Button>("army-confirm").enabledInHierarchy, "CANCEL_FAILED");
            Select("army-kind", "Şehir ikmali");
            Select("army-source", "İstanbul");
            Select("army-detail", c.Economy.Goods.GetRequired(grain).Name);
            Document.Q<TextField>("army-quantity").value = "3";
            Activate("army-prepare");
            yield return null; yield return new WaitForEndOfFrame(); Capture("03-supply-confirmation.png");
            Activate("army-confirm"); _report.supplyTransfers++;
            Require(army.Supply.QuantityOf(grain) == 19, "SUPPLY_NOT_APPLIED");
            Require(army.Supply.QuantityOf(grain) + stock.QuantityOf(grain) == goods, "GOODS_NOT_CONSERVED");
            var saved = Fingerprint();
            Document.Q<TextField>("save-slot").value = "y3-army";
            Activate("save-campaign");
            Require(Document.Q<Label>("save-feedback").text == "SAVED", "REAL_SAVE_FAILED");
            // Leave a preview pending, then load: the new session must discard it.
            Activate("army-prepare");
            Activate("load-campaign");
            yield return null;
            Require(Document.Q<Label>("save-feedback").text == "LOADED", "REAL_LOAD_FAILED");
            Require(Fingerprint() == saved, "LOAD_CHANGED_ARMY_OR_RESOURCES");
            Require(!ReferenceEquals(c, _bootstrap.CurrentCampaign), "LOAD_DID_NOT_REBUILD_RUNTIME");
            host.Open(Route()); yield return null;
            Require(!Document.Q<Button>("army-confirm").enabledInHierarchy, "OLD_CONFIRMATION_SURVIVED_LOAD");
            _report.saveRoundtrips++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("04-army-reloaded.png");
            Select("army-kind", "Maaş ödemesi");
            Require(Document.Q<DropdownField>("army-source").choices.Count == 0, "INVENTED_PAYROLL");
            Require(!Document.Q<Button>("army-prepare").enabledInHierarchy, "NONEXISTENT_PAYROLL_ENABLED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("05-no-invented-payroll.png");
            Select("army-kind", "Kervan ikmali");
            Require(Document.Q<DropdownField>("army-source").choices.Count == 0, "NPC_CARGO_AUTHORITY_GRANTED");
            Require(!Document.Q<Button>("army-prepare").enabledInHierarchy, "NPC_TRANSFER_ENABLED");
            yield return null; yield return new WaitForEndOfFrame(); Capture("06-npc-cargo-protected.png");
            var data = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign!);
            Require(new CampaignSaveValidator().Validate(data).IsValid, "SAVE_INVALID");
            _report.finalFingerprint = Fingerprint();
            _report.saveVersion = data.SaveVersion;
            Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }
        private static PresentationRoute Route() => new PresentationRoute(PresentationScreenId.Army, new PresentationEntityRef(PresentationEntityKind.Army, "army-hasan-retinue"));
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
            File.WriteAllText(Path.Combine(_root,"army.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_ARMY_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Actual Hasan commander, finite recruitment and city supply; no NPC control or new payroll";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, recruitments, supplyTransfers, saveRoundtrips, screenshots, saveVersion;
            public int screenWidth, screenHeight, renderWidth, renderHeight;
        }
    }
}
