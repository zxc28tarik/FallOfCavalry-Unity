#nullable enable
using System;
using FOC.Domain.Campaign;

namespace FOC.Application.Save
{
    public sealed class CampaignSaveService
    {
        private readonly ISaveSerializer _serializer;
        private readonly IAtomicSaveStore _store;
        private readonly CampaignSaveValidator _validator;
        private readonly SaveMigrationPipeline _migrations;

        public CampaignSaveService(
            ISaveSerializer serializer,
            IAtomicSaveStore store,
            CampaignSaveValidator validator,
            SaveMigrationPipeline migrations)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _migrations = migrations ?? throw new ArgumentNullException(nameof(migrations));
        }

        public SaveStoreResult Save(string slotName, CampaignRuntimeState state)
        {
            var data = CampaignSaveMapper.ToSaveData(state);
            if (!_validator.Validate(data).IsValid)
            {
                return SaveStoreResult.Failed("Runtime state produced invalid save data.");
            }

            var content = _serializer.Serialize(data);
            var contentValidation = new OperationContentValidation(_serializer, _validator);
            return _store.Write(slotName, content, contentValidation.Validate);
        }

        public SaveReadResult Load(string slotName)
        {
            var contentValidation = new OperationContentValidation(_serializer, _validator);
            var stored = _store.Read(slotName, contentValidation.Validate);
            if (!stored.Success || stored.Content == null)
            {
                return SaveReadResult.Failed(stored.Error ?? "Save slot could not be read.");
            }

            var read = contentValidation.Read(stored.Content);
            if (!read.Success || read.Data == null)
            {
                return read;
            }

            var migrated = _migrations.Migrate(read.Data, CampaignSaveData.CurrentSaveVersion);
            if (!migrated.Success || migrated.Data == null)
            {
                return SaveReadResult.Failed(migrated.Error ?? "Save migration failed.");
            }

            var validation = _validator.Validate(migrated.Data);
            return validation.IsValid
                ? SaveReadResult.Succeeded(
                    migrated.Data,
                    stored.RecoveredFromBackup ? SaveRecoveryStatus.RecoveredFromBackup : SaveRecoveryStatus.LoadedCurrent,
                    stored.RecoveredFromBackup ? "Current save was invalid or unavailable; the validated backup was loaded." : null)
                : SaveReadResult.Failed("Loaded save violates campaign invariants.");
        }

        // Atomic storage still reads and validates every disk generation. Within
        // ONE operation, equal payload bytes need not reconstruct identical DTO
        // graphs repeatedly (temporary/committed and current/returned reads).
        // Never persist a cache on the service, use hashes as equality, or reuse
        // a decoded graph for a different payload or a later load.
        private sealed class OperationContentValidation
        {
            private readonly ISaveSerializer _serializer;
            private readonly CampaignSaveValidator _validator;
            private readonly Entry?[] _entries = new Entry?[2];
            private int _next;

            public OperationContentValidation(ISaveSerializer serializer, CampaignSaveValidator validator)
            {
                _serializer = serializer;
                _validator = validator;
            }

            public bool Validate(string content)
            {
                var existing = Find(content);
                if (existing != null) return existing.IsValid;
                var read = _serializer.Deserialize(content);
                var valid = read.Success && read.Data != null && _validator.Validate(read.Data).IsValid;
                _entries[_next] = new Entry(content, read, valid);
                _next = (_next + 1) % _entries.Length;
                return valid;
            }

            public SaveReadResult Read(string content) => Find(content)?.Result ?? _serializer.Deserialize(content);

            private Entry? Find(string content)
            {
                foreach (var entry in _entries)
                    if (entry != null && StringComparer.Ordinal.Equals(entry.Content, content)) return entry;
                return null;
            }

            private sealed class Entry
            {
                public Entry(string content, SaveReadResult result, bool isValid)
                {
                    Content = content;
                    Result = result;
                    IsValid = isValid;
                }
                public string Content { get; }
                public SaveReadResult Result { get; }
                public bool IsValid { get; }
            }
        }
    }
}
