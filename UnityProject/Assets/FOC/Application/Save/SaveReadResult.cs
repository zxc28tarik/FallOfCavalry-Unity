using System;

namespace FOC.Application.Save
{
    public sealed class SaveReadResult
    {
        private SaveReadResult(bool success, CampaignSaveData? data, string? error)
        {
            Success = success;
            Data = data;
            Error = error;
        }

        public bool Success { get; }

        public CampaignSaveData? Data { get; }

        public string? Error { get; }

        public static SaveReadResult Succeeded(CampaignSaveData data) =>
            new SaveReadResult(true, data ?? throw new ArgumentNullException(nameof(data)), null);

        public static SaveReadResult Failed(string error) =>
            new SaveReadResult(false, null, string.IsNullOrWhiteSpace(error) ? "Unknown save read error." : error);
    }
}

