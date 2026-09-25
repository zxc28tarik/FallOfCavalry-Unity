#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Common;
using FOC.Domain.Characters;
using FOC.Domain.Geography;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Bootstrap.Unity
{
    /// <summary>
    /// Development-only executable bootstrap for the historical-geography vertical slice.
    /// It uses production serializers, validation, presentation queries and atomic storage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevelopmentCampaignBootstrap : MonoBehaviour
    {
        private const string ReadyMarker = "FOC_DEVELOPMENT_BOOTSTRAP_READY";
        private PresentationRuntimeHost? _host;
        private CampaignSaveCoordinator? _saveCoordinator;
        private CampaignRuntimeState? _campaign;
        private PresentationViewerContext? _viewer;
        private IMapPresentationDataProvider? _map;
        private PresentationCommandBindingRegistry? _bindings;
        private TravelCommandService? _travel;
        private Label? _feedback;
        private TextField? _slot;

        public CampaignRuntimeState? CurrentCampaign => _campaign;

        private void Start()
        {
            try
            {
                var locations=Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations") ?? throw new InvalidOperationException("Vertical-slice location content is missing.");
                var routes=Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes") ?? throw new InvalidOperationException("Vertical-slice route content is missing.");
                _campaign = VerticalSliceCampaignFactory.Create(locations.text,routes.text);
                var root = Path.Combine(UnityEngine.Application.persistentDataPath, "FOC", "VerticalSliceSaves");
                var serializer = new CampaignSaveTextSerializer();
                var service = new CampaignSaveService(
                    serializer,
                    new AtomicFileSaveStore(root),
                    new CampaignSaveValidator(),
                    CampaignSaveDefaults.CreateMigrationPipeline());
                _saveCoordinator = new CampaignSaveCoordinator(
                    service,
                    VerticalSliceCampaignFactory.ContentVersion,
                    _campaign.WorldGenRevision);

                _viewer = CreateViewer();
                ConfigureTravelPresentation();
                _host = GetComponent<PresentationRuntimeHost>() ?? gameObject.AddComponent<PresentationRuntimeHost>();
                PresentCampaign();
                if (HasCommandLineArgument("-focTravelDemo"))
                {
                    StartPlayerTravel(WorldLocationId.Create("edirne"));
                    _travel!.Advance(WorldDuration.FromMinutes(60));
                    PresentCampaign();
                    Debug.Log("FOC_TRAVEL_DEMO_READY");
                }
                Debug.Log(ReadyMarker + " campaign=" + _campaign.CampaignId.Value + " saveRoot=" + root);
                if (HasCommandLineArgument("-focCaptureScreenshot")) StartCoroutine(CaptureScreenshot());
                else if (HasCommandLineArgument("-focSmokeTest")) StartCoroutine(RunSmoke());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FOC_DEVELOPMENT_BOOTSTRAP_FAILED " + exception.Message);
                enabled = false;
            }
        }

        private void PresentCampaign()
        {
            if (_host == null || _campaign == null || _viewer == null || _map == null) return;
            _host.Configure(new CampaignPresentationScreenSource(_campaign, _viewer, _map),dispatcher:_bindings);
            BindSaveLoadSurface();
        }

        private void BindSaveLoadSurface()
        {
            var document = GetComponent<UIDocument>();
            if (document == null) throw new InvalidOperationException("Presentation UIDocument is unavailable.");
            var root = document.rootVisualElement;
            _slot = root.Q<TextField>("save-slot") ?? throw new InvalidOperationException("Save slot field is missing.");
            _feedback = root.Q<Label>("save-feedback") ?? throw new InvalidOperationException("Save feedback label is missing.");
            var save = root.Q<Button>("save-campaign") ?? throw new InvalidOperationException("Save button is missing.");
            var load = root.Q<Button>("load-campaign") ?? throw new InvalidOperationException("Load button is missing.");
            save.clicked += Save;
            load.clicked += Load;
        }

        private void Save()
        {
            if (_saveCoordinator == null || _campaign == null || _slot == null) return;
            var result = _saveCoordinator.Save(_slot.value, _campaign);
            SetFeedback(result.Success ? "SAVED" : "SAVE FAILED", result.Error);
        }

        private void Load()
        {
            if (_saveCoordinator == null || _slot == null) return;
            var result = _saveCoordinator.Load(_slot.value);
            if (!result.Success || result.Campaign == null)
            {
                SetFeedback("LOAD FAILED", result.Error);
                return;
            }

            _campaign = result.Campaign;
            ConfigureTravelPresentation();
            PresentCampaign();
            SetFeedback(result.RecoveryStatus == SaveRecoveryStatus.RecoveredFromBackup ? "BACKUP RECOVERED" : "LOADED", result.RecoveryReason);
        }

        private void SetFeedback(string state, string? detail)
        {
            if (_feedback == null) return;
            _feedback.text = string.IsNullOrWhiteSpace(detail) ? state : state + ": " + detail;
            Debug.Log("FOC_SAVE_UI " + _feedback.text);
        }

        private IEnumerator RunSmoke()
        {
            yield return null;
            yield return null;
            Save();
            Load();
            Debug.Log("FOC_DEVELOPMENT_SMOKE_PASS campaign=" + (_campaign?.CampaignId.Value ?? "missing"));
            UnityEngine.Application.Quit(0);
        }

        private IEnumerator CaptureScreenshot()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var path = CommandLineValue("-focScreenshotPath");
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("-focScreenshotPath is required with -focCaptureScreenshot.");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            WriteBmp(path, texture.GetPixels32(), texture.width, texture.height);
            Destroy(texture);
            Debug.Log("FOC_SCREENSHOT_CAPTURE_PASS path=" + path);
            UnityEngine.Application.Quit(0);
        }

        private static void WriteBmp(string path, Color32[] pixels, int width, int height)
        {
            var rowSize = (width * 3 + 3) & ~3;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B'); writer.Write((byte)'M'); writer.Write(54 + rowSize * height);
            writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(width); writer.Write(height);
            writer.Write((short)1); writer.Write((short)24); writer.Write(0); writer.Write(rowSize * height);
            writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
            var padding = rowSize - width * 3;
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var color = pixels[row + x]; writer.Write(color.b); writer.Write(color.g); writer.Write(color.r);
                }
                for (var index = 0; index < padding; index++) writer.Write((byte)0);
            }
        }

        private static bool HasCommandLineArgument(string argument) =>
            Array.Exists(Environment.GetCommandLineArgs(), value => StringComparer.OrdinalIgnoreCase.Equals(value, argument));

        private static string? CommandLineValue(string argument)
        {
            var values = Environment.GetCommandLineArgs();
            for (var index = 0; index < values.Length - 1; index++)
                if (StringComparer.OrdinalIgnoreCase.Equals(values[index], argument)) return values[index + 1];
            return null;
        }

        private static PresentationViewerContext CreateViewer()
        {
            var controlled = new PresentationEntityRef(PresentationEntityKind.Character, "slice-player-sipahi");
            var exact = new List<PresentationEntityRef>
            {
                controlled,
                new PresentationEntityRef(PresentationEntityKind.Character, "slice-player-sipahi"),
                new PresentationEntityRef(PresentationEntityKind.Character, "commander-b"),
                new PresentationEntityRef(PresentationEntityKind.City, "city-home"),
                new PresentationEntityRef(PresentationEntityKind.City, "city-other"),
                new PresentationEntityRef(PresentationEntityKind.Organization, "org-a"),
                new PresentationEntityRef(PresentationEntityKind.Organization, "org-b"),
                new PresentationEntityRef(PresentationEntityKind.Army, "army-a"),
                new PresentationEntityRef(PresentationEntityKind.Army, "army-b"),
                new PresentationEntityRef(PresentationEntityKind.Battle, "battle-main")
            };
            return new PresentationViewerContext(FactionId.Create("faction-a"), controlled, exact, developmentDebug: true);
        }

        private void ConfigureTravelPresentation()
        {
            if(_campaign==null)return;_bindings?.Dispose();_travel=new TravelCommandService(_campaign);_map=new WorldMapPresentationDataProvider(_campaign);_bindings=new PresentationCommandBindingRegistry();
            foreach(var location in _campaign.Geography.World.OrderedLocations)
            {
                if(location.Id.Value=="istanbul")continue;var destination=location.Id;_bindings.Register("travel.start:"+destination.Value,()=>StartPlayerTravel(destination));
            }
            _bindings.Register("travel.advance-one-hour",()=>{try{_travel!.Advance(WorldDuration.FromMinutes(60));return PresentationActionResult.Success("presentation.travel.advanced");}catch(InvalidOperationException){return PresentationActionResult.Rejected("presentation.action.validation-rejected");}});
        }

        private PresentationActionResult StartPlayerTravel(WorldLocationId destination)
        {
            try
            {
                if(_campaign==null||_travel==null)throw new InvalidOperationException();var player=_campaign.Characters.GetRequired(CharacterId.Create("slice-player-sipahi"));if(player.Location.Kind!=CharacterLocationKind.City||!player.Location.CityId.HasValue)throw new InvalidOperationException();var origin=_campaign.Geography.World.LocationForCity(player.Location.CityId.Value).Id;_travel.Start(JourneyId.Create("journey-player-"+_campaign.Clock.Now.Ticks+"-"+destination.Value),TravelActorRef.Character(player.Id),origin,destination);return PresentationActionResult.Success("presentation.travel.started");
            }
            catch(InvalidOperationException){return PresentationActionResult.Rejected("presentation.action.validation-rejected");}
        }
    }
}
