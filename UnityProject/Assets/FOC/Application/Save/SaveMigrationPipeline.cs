using System;
using System.Collections.Generic;

namespace FOC.Application.Save
{
    public sealed class SaveMigrationPipeline
    {
        private readonly SortedDictionary<int, ISaveMigration> _migrations = new SortedDictionary<int, ISaveMigration>();

        public SaveMigrationPipeline(IEnumerable<ISaveMigration> migrations)
        {
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            foreach (var migration in migrations)
            {
                if (migration == null)
                {
                    throw new ArgumentException("Migration collection cannot contain null.", nameof(migrations));
                }

                if (migration.ToVersion != migration.FromVersion + 1)
                {
                    throw new ArgumentException("Each migration must advance exactly one schema version.", nameof(migrations));
                }

                if (_migrations.ContainsKey(migration.FromVersion))
                {
                    throw new ArgumentException($"Duplicate migration from version {migration.FromVersion}.", nameof(migrations));
                }

                _migrations.Add(migration.FromVersion, migration);
            }
        }

        public SaveMigrationResult Migrate(CampaignSaveData source, int targetVersion)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targetVersion < source.SaveVersion)
            {
                return SaveMigrationResult.Failed("Save downgrades are not supported.");
            }

            var current = source;
            while (current.SaveVersion < targetVersion)
            {
                if (!_migrations.TryGetValue(current.SaveVersion, out var migration))
                {
                    return SaveMigrationResult.Failed($"Missing migration from save version {current.SaveVersion}.");
                }

                var expectedVersion = migration.ToVersion;
                current = migration.Apply(current);
                if (current == null || current.SaveVersion != expectedVersion)
                {
                    return SaveMigrationResult.Failed($"Migration {migration.FromVersion}->{migration.ToVersion} returned an invalid version.");
                }
            }

            return SaveMigrationResult.Succeeded(current);
        }
    }
}

