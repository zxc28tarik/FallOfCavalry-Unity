using System;

namespace FOC.Application.Save
{
    public enum SaveRecoveryStatus
    {
        LoadedCurrent = 0,
        RecoveredFromBackup = 1,
        Failed = 2,
    }

    public sealed class SaveReadResult
    {
        private SaveReadResult(bool success, CampaignSaveData? data, SaveRecoveryStatus recoveryStatus, string? recoveryReason, string? error)
        {
            Success = success;
            Data = data;
            RecoveryStatus = recoveryStatus;
            RecoveryReason = recoveryReason;
            Error = error;
        }

        public bool Success { get; }

        public CampaignSaveData? Data { get; }

        public SaveRecoveryStatus RecoveryStatus { get; }

        public string? RecoveryReason { get; }

        public string? Error { get; }

        public static SaveReadResult Succeeded(CampaignSaveData data, SaveRecoveryStatus recoveryStatus = SaveRecoveryStatus.LoadedCurrent, string? recoveryReason = null) =>
            new SaveReadResult(true, data ?? throw new ArgumentNullException(nameof(data)), recoveryStatus, recoveryReason, null);

        public static SaveReadResult Failed(string error) =>
            new SaveReadResult(false, null, SaveRecoveryStatus.Failed, null, string.IsNullOrWhiteSpace(error) ? "Unknown save read error." : error);
    }
}
