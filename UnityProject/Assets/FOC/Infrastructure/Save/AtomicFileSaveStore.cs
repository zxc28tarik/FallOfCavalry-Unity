using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using FOC.Application.Save;

namespace FOC.Infrastructure.Save
{
    public enum AtomicSaveStage
    {
        TempCreate,
        TempWrite,
        DurableFlush,
        TempValidation,
        Backup,
        Replace,
        FinalRead,
    }

    public interface IAtomicSaveFaultInjector
    {
        void BeforeStage(AtomicSaveStage stage);
    }

    public sealed class AtomicFileSaveStore : IAtomicSaveStore
    {
        private static readonly ConcurrentDictionary<string, object> SlotGates = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly string _rootDirectory;
        private readonly Encoding _encoding = new UTF8Encoding(false, true);
        private readonly IAtomicSaveFaultInjector? _faultInjector;

        public AtomicFileSaveStore(string rootDirectory, IAtomicSaveFaultInjector? faultInjector = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Save root directory is required.", nameof(rootDirectory));
            }

            _rootDirectory = Path.GetFullPath(rootDirectory);
            _faultInjector = faultInjector;
        }

        public SaveStoreResult Write(string slotName, string content, Func<string, bool> validateContent)
        {
            if (validateContent == null)
            {
                throw new ArgumentNullException(nameof(validateContent));
            }

            try
            {
                var paths = ResolvePaths(slotName);
                lock (SlotGates.GetOrAdd(paths.Current, _ => new object()))
                {
                    return WriteLocked(paths, content, validateContent);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return SaveStoreResult.Failed(exception.Message);
            }
        }

        private SaveStoreResult WriteLocked(SavePaths paths, string content, Func<string, bool> validateContent)
        {
            Directory.CreateDirectory(_rootDirectory);
            Inject(AtomicSaveStage.TempCreate);

            using (var stream = new FileStream(paths.Temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, _encoding))
            {
                Inject(AtomicSaveStage.TempWrite);
                writer.Write(content);
                writer.Flush();
                Inject(AtomicSaveStage.DurableFlush);
                stream.Flush(true);
            }

            Inject(AtomicSaveStage.TempValidation);
            var written = File.ReadAllText(paths.Temp, _encoding);
            if (!validateContent(written))
            {
                File.Delete(paths.Temp);
                return SaveStoreResult.Failed("Temporary save failed validation; current save was not replaced.");
            }

            if (File.Exists(paths.Current)) ReplaceWithBackup(paths);
            else
            {
                Inject(AtomicSaveStage.Replace);
                File.Move(paths.Temp, paths.Current);
            }

            Inject(AtomicSaveStage.FinalRead);
            var committed = File.ReadAllText(paths.Current, _encoding);
            return validateContent(committed)
                ? SaveStoreResult.Written()
                : SaveStoreResult.Failed("Committed save failed final validation; backup remains available.");
        }

        public SaveStoreResult Read(string slotName, Func<string, bool> validateContent)
        {
            if (validateContent == null)
            {
                throw new ArgumentNullException(nameof(validateContent));
            }

            try
            {
                var paths = ResolvePaths(slotName);
                lock (SlotGates.GetOrAdd(paths.Current, _ => new object()))
                {
                    if (TryReadValid(paths.Current, validateContent, out var current)) return SaveStoreResult.Read(current!, false);

                    if (TryReadValid(paths.Backup, validateContent, out var backup)) return SaveStoreResult.Read(backup!, true);

                    return SaveStoreResult.Failed("Neither current save nor backup is present and valid.");
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return SaveStoreResult.Failed(exception.Message);
            }
        }

        private static void ValidateSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                throw new ArgumentException("Save slot name is required.", nameof(slotName));
            }

            foreach (var character in slotName)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    throw new ArgumentException("Save slot names may contain only letters, digits, '-' and '_'.", nameof(slotName));
                }
            }
        }

        private SavePaths ResolvePaths(string slotName)
        {
            ValidateSlotName(slotName);
            var current = Path.Combine(_rootDirectory, slotName + ".focsave");
            return new SavePaths(current, current + ".tmp", current + ".bak");
        }

        private bool TryReadValid(string path, Func<string, bool> validateContent, out string? content)
        {
            if (!File.Exists(path))
            {
                content = null;
                return false;
            }

            content = File.ReadAllText(path, _encoding);
            return validateContent(content);
        }

        private void ReplaceWithBackup(SavePaths paths)
        {
            Inject(AtomicSaveStage.Backup);
            if (File.Exists(paths.Backup))
            {
                File.Delete(paths.Backup);
            }

            Inject(AtomicSaveStage.Replace);
            try
            {
                File.Replace(paths.Temp, paths.Current, paths.Backup, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(paths.Current, paths.Backup, true);
                File.Copy(paths.Temp, paths.Current, true);
                File.Delete(paths.Temp);
            }
        }

        private void Inject(AtomicSaveStage stage) => _faultInjector?.BeforeStage(stage);

        private readonly struct SavePaths
        {
            public SavePaths(string current, string temp, string backup)
            {
                Current = current;
                Temp = temp;
                Backup = backup;
            }

            public string Current { get; }

            public string Temp { get; }

            public string Backup { get; }
        }
    }
}
