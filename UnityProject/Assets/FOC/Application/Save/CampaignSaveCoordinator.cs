#nullable enable
using System;
using FOC.Domain.Campaign;

namespace FOC.Application.Save
{
    public sealed class CampaignLoadRuntimeResult
    {
        private CampaignLoadRuntimeResult(bool success, CampaignRuntimeState? campaign, SaveRecoveryStatus recoveryStatus, string? recoveryReason, string? error)
        {
            Success = success;
            Campaign = campaign;
            RecoveryStatus = recoveryStatus;
            RecoveryReason = recoveryReason;
            Error = error;
        }

        public bool Success { get; }
        public CampaignRuntimeState? Campaign { get; }
        public SaveRecoveryStatus RecoveryStatus { get; }
        public string? RecoveryReason { get; }
        public string? Error { get; }

        public static CampaignLoadRuntimeResult Loaded(CampaignRuntimeState campaign, SaveRecoveryStatus status, string? reason) =>
            new CampaignLoadRuntimeResult(true, campaign ?? throw new ArgumentNullException(nameof(campaign)), status, reason, null);

        public static CampaignLoadRuntimeResult Failed(string error) =>
            new CampaignLoadRuntimeResult(false, null, SaveRecoveryStatus.Failed, null, string.IsNullOrWhiteSpace(error) ? "SAVE_LOAD_UNKNOWN" : error);
    }

    public sealed class CampaignSaveCoordinator
    {
        private readonly CampaignSaveService _service;
        private readonly string? _expectedContentDataVersion;
        private readonly int? _expectedWorldGenRevision;

        public CampaignSaveCoordinator(CampaignSaveService service, string? expectedContentDataVersion = null, int? expectedWorldGenRevision = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _expectedContentDataVersion = expectedContentDataVersion;
            _expectedWorldGenRevision = expectedWorldGenRevision;
        }

        public SaveStoreResult Save(string slotName, CampaignRuntimeState campaign) => _service.Save(slotName, campaign);

        public CampaignLoadRuntimeResult Load(string slotName)
        {
            var read = _service.Load(slotName);
            if (!read.Success || read.Data == null) return CampaignLoadRuntimeResult.Failed(read.Error ?? "SAVE_LOAD_FAILED");
            if (_expectedContentDataVersion != null && !StringComparer.Ordinal.Equals(read.Data.ContentDataVersion, _expectedContentDataVersion))
                return CampaignLoadRuntimeResult.Failed("SAVE_CONTENT_VERSION_MISMATCH");
            if (_expectedWorldGenRevision.HasValue && read.Data.WorldGenRevision != _expectedWorldGenRevision.Value)
                return CampaignLoadRuntimeResult.Failed("SAVE_WORLD_GEN_REVISION_MISMATCH");

            try
            {
                return CampaignLoadRuntimeResult.Loaded(CampaignSaveMapper.ToRuntimeState(read.Data), read.RecoveryStatus, read.RecoveryReason);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return CampaignLoadRuntimeResult.Failed("SAVE_RUNTIME_RECONSTRUCTION_FAILED: " + exception.Message);
            }
        }
    }
}
