#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FOC.Application.Proof;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Common;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using FOC.Presentation.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOC.Bootstrap.Unity
{
    /// <summary>
    /// Development-only executable bootstrap for the integrated proof campaign.
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
        private Label? _feedback;
        private TextField? _slot;

        public CampaignRuntimeState? CurrentCampaign => _campaign;

        private void Start()
        {
            try
            {
                _campaign = IntegratedProofCampaignFactory.Create();
                var root = Path.Combine(UnityEngine.Application.persistentDataPath, "FOC", "Saves");
                var serializer = new CampaignSaveTextSerializer();
                var service = new CampaignSaveService(
                    serializer,
                    new AtomicFileSaveStore(root),
                    new CampaignSaveValidator(),
                    CampaignSaveDefaults.CreateMigrationPipeline());
                _saveCoordinator = new CampaignSaveCoordinator(
                    service,
                    IntegratedProofCampaignFactory.ContentVersion,
                    _campaign.WorldGenRevision);

                _viewer = CreateViewer();
                _map = CreateMap();
                _host = GetComponent<PresentationRuntimeHost>() ?? gameObject.AddComponent<PresentationRuntimeHost>();
                PresentCampaign();
                Debug.Log(ReadyMarker + " campaign=" + _campaign.CampaignId.Value + " saveRoot=" + root);
                if (HasCommandLineArgument("-focSmokeTest")) StartCoroutine(RunSmoke());
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
            _host.Configure(new CampaignPresentationScreenSource(_campaign, _viewer, _map));
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

        private static bool HasCommandLineArgument(string argument) =>
            Array.Exists(Environment.GetCommandLineArgs(), value => StringComparer.OrdinalIgnoreCase.Equals(value, argument));

        private static PresentationViewerContext CreateViewer()
        {
            var controlled = new PresentationEntityRef(PresentationEntityKind.Character, "commander-a");
            var exact = new List<PresentationEntityRef>
            {
                controlled,
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

        private static IMapPresentationDataProvider CreateMap() => new ProofOnlyMapPresentationDataProvider(new[]
        {
            new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.City, "city-home"), "city-home", 0.32f, 0.55f, PresentationKnowledge.ExactSelf, proofOnly: true),
            new MapMarkerPresentation(new PresentationEntityRef(PresentationEntityKind.City, "city-other"), "city-other", 0.68f, 0.45f, PresentationKnowledge.ExactSelf, proofOnly: true)
        });
    }
}
