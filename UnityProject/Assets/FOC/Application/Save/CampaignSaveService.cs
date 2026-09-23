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
            return _store.Write(slotName, content, IsSerializedContentValid);
        }

        public SaveReadResult Load(string slotName)
        {
            var stored = _store.Read(slotName, IsSerializedContentValid);
            if (!stored.Success || stored.Content == null)
            {
                return SaveReadResult.Failed(stored.Error ?? "Save slot could not be read.");
            }

            var read = _serializer.Deserialize(stored.Content);
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

        private bool IsSerializedContentValid(string content)
        {
            var read = _serializer.Deserialize(content);
            return read.Success && read.Data != null && _validator.Validate(read.Data).IsValid;
        }
    }
}
