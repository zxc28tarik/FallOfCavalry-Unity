using System;
using System.IO;
using System.Text;
using FOC.Application.Save;

namespace FOC.Infrastructure.Save
{
    public sealed class AtomicFileSaveStore : IAtomicSaveStore
    {
        private readonly string _rootDirectory;
        private readonly Encoding _encoding = new UTF8Encoding(false, true);

        public AtomicFileSaveStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Save root directory is required.", nameof(rootDirectory));
            }

            _rootDirectory = Path.GetFullPath(rootDirectory);
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
                Directory.CreateDirectory(_rootDirectory);

                using (var stream = new FileStream(paths.Temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, _encoding))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true);
                }

                var written = File.ReadAllText(paths.Temp, _encoding);
                if (!validateContent(written))
                {
                    File.Delete(paths.Temp);
                    return SaveStoreResult.Failed("Temporary save failed validation; current save was not replaced.");
                }

                if (File.Exists(paths.Current))
                {
                    ReplaceWithBackup(paths);
                }
                else
                {
                    File.Move(paths.Temp, paths.Current);
                }

                return SaveStoreResult.Written();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return SaveStoreResult.Failed(exception.Message);
            }
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
                if (TryReadValid(paths.Current, validateContent, out var current))
                {
                    return SaveStoreResult.Read(current!, false);
                }

                if (TryReadValid(paths.Backup, validateContent, out var backup))
                {
                    return SaveStoreResult.Read(backup!, true);
                }

                return SaveStoreResult.Failed("Neither current save nor backup is present and valid.");
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

        private static void ReplaceWithBackup(SavePaths paths)
        {
            if (File.Exists(paths.Backup))
            {
                File.Delete(paths.Backup);
            }

            try
            {
                File.Replace(paths.Temp, paths.Current, paths.Backup, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(paths.Current, paths.Backup, true);
                File.Delete(paths.Current);
                File.Move(paths.Temp, paths.Current);
            }
        }

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

