namespace FOC.Application.Save
{
    public sealed class SaveMigrationResult
    {
        private SaveMigrationResult(bool success, CampaignSaveData? data, string? error)
        {
            Success = success;
            Data = data;
            Error = error;
        }

        public bool Success { get; }

        public CampaignSaveData? Data { get; }

        public string? Error { get; }

        public static SaveMigrationResult Succeeded(CampaignSaveData data) => new SaveMigrationResult(true, data, null);

        public static SaveMigrationResult Failed(string error) => new SaveMigrationResult(false, null, error);
    }
}

