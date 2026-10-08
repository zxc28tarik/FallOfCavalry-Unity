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
    /// <summary>Opt-in isolated real-player UI trade test. Synthetic UI input is explicitly not OS input acceptance.</summary>
    public sealed class DevelopmentTradeAcceptance : MonoBehaviour
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
            host.Open(Route("city-bursa"));
            yield return null;
            Require(!Document.Q<Button>("trade-purchase").enabledInHierarchy, "HASAN_MUST_NOT_CONTROL_NPC");
            Require(Document.Q<DropdownField>("trade-caravan").choices.Count == 0, "FOREIGN_CARGO_DISCLOSED");
            yield return new WaitForEndOfFrame(); Capture("01-hasan-market-readonly.png");
            ConfigureManager("city-bursa");
            yield return null;
            var before = Fingerprint();
            Select("trade-good", "İpekli Kumaş");
            Document.Q<TextField>("trade-quantity").value = "2";
            yield return null;
            Require(Fingerprint() == before, "SELECTION_MUTATED_CAMPAIGN");
            Activate("trade-purchase");
            Require(Fingerprint() == before, "PREVIEW_MUTATED_CAMPAIGN");
            yield return null; yield return new WaitForEndOfFrame(); Capture("02-manager-purchase-confirmation.png");
            Activate("trade-confirm");
            var caravan = _bootstrap.CurrentCampaign!.Economy.Caravans.GetRequired(CaravanId.Create("caravan-bursa-istanbul"));
            Require(caravan.Cargo.QuantityOf(TradeGoodId.Create("silk-cloth")) == 6, "PURCHASE_NOT_APPLIED");
            Require(!Document.Q<Button>("trade-confirm").enabledInHierarchy, "DUPLICATE_CONFIRM_ENABLED");
            _report.purchases++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("03-manager-purchase-result.png");
            Document.Q<TextField>("trade-quantity").value = "-1";
            yield return null;
            Require(!Document.Q<Button>("trade-purchase").enabledInHierarchy, "INVALID_QUANTITY_ENABLED");
            Document.Q<TextField>("trade-quantity").value = "1";
            Activate("trade-purchase"); Activate("trade-cancel");
            Require(!Document.Q<Button>("trade-confirm").enabledInHierarchy, "CANCEL_DID_NOT_INVALIDATE");
            // Rebuild the same production campaign from its v14 save. No normal save folder is used.
            // Manager is a labelled QA viewer only; normal bootstrap remains Hasan.
            var serializer = new CampaignSaveTextSerializer();
            var data = CampaignSaveMapper.ToSaveData(_bootstrap.CurrentCampaign);
            var serialized = serializer.Serialize(data);
            Require(new CampaignSaveValidator().Validate(data).IsValid, "SAVE_INVALID");
            File.WriteAllText(Path.Combine(_root, "y2-roundtrip.json"), serialized);
            var restored = CampaignSaveMapper.ToRuntimeState(serializer.Deserialize(File.ReadAllText(Path.Combine(_root, "y2-roundtrip.json"))).Data!);
            Require(serializer.Serialize(CampaignSaveMapper.ToSaveData(restored)) == serialized, "TRADE_RELOAD_MISMATCH");
            _report.saveRoundtrips++;
            // Sale fixture setup uses the actual travel service, not teleport or cargo/cash mutation.
            // This does NOT claim a player-facing Y4 caravan departure command has been implemented.
            var travel = new TravelCommandService(restored);
            var journey = travel.Start(JourneyId.Create("y2-qa-caravan"), TravelActorRef.Caravan(caravan.Id), WorldLocationId.Create("bursa"), WorldLocationId.Create("istanbul"));
            travel.Advance(new WorldDuration(travel.TotalJourneyTicks(journey)));
            ConfigureManager("city-istanbul", restored);
            yield return null;
            Select("trade-good", "İpekli Kumaş"); Document.Q<TextField>("trade-quantity").value = "2";
            Activate("trade-sale");
            yield return null; yield return new WaitForEndOfFrame(); Capture("04-manager-sale-confirmation.png");
            Activate("trade-confirm");
            var sold = restored.Economy.Caravans.GetRequired(caravan.Id);
            Require(sold.Cargo.QuantityOf(TradeGoodId.Create("silk-cloth")) == 4, "SALE_NOT_APPLIED");
            Require(sold.Accounting.SaleRevenue.Value > 0, "SALE_ACCOUNTING_MISSING");
            _report.sales++;
            yield return null; yield return new WaitForEndOfFrame(); Capture("05-manager-sale-result.png");
            _report.finalFingerprint = SavePayloadFingerprint.Compute(serializer, CampaignSaveMapper.ToSaveData(restored));
            _report.saveVersion = CampaignSaveMapper.ToSaveData(restored).SaveVersion;
            Require(_unexpectedError == null, _unexpectedError ?? string.Empty);
        }
        private static PresentationRoute Route(string city) => new PresentationRoute(PresentationScreenId.Trade, new PresentationEntityRef(PresentationEntityKind.City, city));
        private void ConfigureManager(string city, FOC.Domain.Campaign.CampaignRuntimeState? campaign = null)
        {
            var state = campaign ?? _bootstrap.CurrentCampaign!;
            var viewer = new PresentationViewerContext(FactionId.Create("faction-ottoman-state"),
                new PresentationEntityRef(PresentationEntityKind.Character, "mehmed-celebi-tacir"),
                new[] { new PresentationEntityRef(PresentationEntityKind.City, "city-bursa"),
                    new PresentationEntityRef(PresentationEntityKind.City, "city-istanbul"),
                    new PresentationEntityRef(PresentationEntityKind.Caravan, "caravan-bursa-istanbul") });
            var host = GetComponent<PresentationRuntimeHost>();
            host.Configure(new CampaignPresentationScreenSource(state, viewer, new WorldMapPresentationDataProvider(state)),
                new SlicePresentationLocalizer(), trade: new TradePanelSession(state, viewer));
            host.Open(Route(city));
            Document.Q<Label>("trade-heading").text = "İZOLE YÖNETİCİ TESTİ · Mehmed Çelebi · Oyuncu yetkisi değildir";
        }
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
            File.WriteAllText(Path.Combine(_root,"trade.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("FOC_TRADE_" + _report.status + " root=" + _root + " error=" + error);
            UnityEngine.Application.Quit(error == null ? 0 : 1);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report
        {
            public string status = "RUNNING", error = "", unityVersion = "", graphicsDevice = "", finalFingerprint = "";
            public string authority = "Hasan readonly; isolated Mehmed manager test, not player authority or Y4 acceptance";
            public string input = "Synthetic UI Toolkit change and NavigationSubmit events on attached controls; not physical OS input";
            public string rendering = "Actual Windows D3D11 runtime UI Toolkit panel rendered to PanelSettings.targetTexture, not a system desktop capture";
            public int selections, activations, purchases, sales, saveRoundtrips, screenshots, saveVersion;
            public int screenWidth, screenHeight, renderWidth, renderHeight;
        }
    }
}


