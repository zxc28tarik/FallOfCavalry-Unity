namespace FOC.Application.Save
{
    public sealed class SaveStoreResult
    {
        private SaveStoreResult(bool success, string? content, bool recoveredFromBackup, string? error)
        {
            Success = success;
            Content = content;
            RecoveredFromBackup = recoveredFromBackup;
            Error = error;
        }

        public bool Success { get; }

        public string? Content { get; }

        public bool RecoveredFromBackup { get; }

        public string? Error { get; }

        public static SaveStoreResult Written() => new SaveStoreResult(true, null, false, null);

        public static SaveStoreResult Read(string content, bool recoveredFromBackup) =>
            new SaveStoreResult(true, content, recoveredFromBackup, null);

        public static SaveStoreResult Failed(string error) => new SaveStoreResult(false, null, false, error);
    }
}

