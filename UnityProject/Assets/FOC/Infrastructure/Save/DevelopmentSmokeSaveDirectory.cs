#nullable enable
using System;
using System.IO;

namespace FOC.Infrastructure.Save
{
    // Development validation only. Ordinary player save locations remain unchanged.
    public static class DevelopmentSmokeSaveDirectory
    {
        public static string Resolve(string playerSaveRoot, bool smoke, string? requestedRoot = null)
        {
            if (!Path.IsPathRooted(playerSaveRoot)) throw new ArgumentException("Player save root must be absolute.", nameof(playerSaveRoot));
            var playerRoot = Normalize(playerSaveRoot);
            if (!smoke)
            {
                if (requestedRoot != null) throw new ArgumentException("A smoke save root requires -focSmokeTest.", nameof(requestedRoot));
                return playerRoot;
            }

            var parent = Normalize(Path.Combine(Path.GetTempPath(), "foc-windows-smoke"));
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Smoke parent must not be a link or junction.");
            var root = requestedRoot == null ? Path.Combine(parent, Guid.NewGuid().ToString("N")) : requestedRoot;
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                throw new ArgumentException("Smoke save root must be absolute.", nameof(requestedRoot));
            root = Normalize(root);
            if (!Same(Path.GetDirectoryName(root) ?? string.Empty, parent) || !Guid.TryParseExact(Path.GetFileName(root), "N", out _))
                throw new ArgumentException("Smoke saves require a GUID-named direct child of the temporary foc-windows-smoke directory.", nameof(requestedRoot));
            if (Related(root, playerRoot)) throw new ArgumentException("Smoke saves must not overlap player saves.", nameof(requestedRoot));
            if (Directory.Exists(root) || File.Exists(root)) throw new InvalidOperationException("Smoke save root must be new; existing data will not be overwritten.");
            return root;
        }

        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static bool Same(string a, string b) => string.Equals(a, b, PathComparison);
        private static bool Related(string a, string b) => Same(a, b) || a.StartsWith(b + Path.DirectorySeparatorChar, PathComparison) || b.StartsWith(a + Path.DirectorySeparatorChar, PathComparison);
    }
}
