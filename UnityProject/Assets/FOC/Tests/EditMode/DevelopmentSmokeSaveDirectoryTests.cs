#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using FOC.Infrastructure.Save;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class DevelopmentSmokeSaveDirectoryTests
    {
        private string _playerRoot = string.Empty;
        private readonly List<string> _ownedRoots = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _ownedRoots.Clear();
            _playerRoot = Path.Combine(Path.GetTempPath(), "foc-smoke-policy-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _ownedRoots)
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(_playerRoot)) Directory.Delete(_playerRoot, true);
        }

        [Test]
        public void NormalPlayerLocation_IsUnchanged()
        {
            Assert.That(DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, false), Is.EqualTo(_playerRoot));
            Assert.That(Directory.Exists(_playerRoot), Is.False);
        }

        [Test]
        public void AutomaticSmokeRoots_AreUniqueTemporaryAndNotCreatedByResolution()
        {
            var first = DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true);
            var second = DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true);
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(Path.GetDirectoryName(first), Is.EqualTo(Path.Combine(Path.GetTempPath(), "foc-windows-smoke")));
            Assert.That(Guid.TryParseExact(Path.GetFileName(first), "N", out _), Is.True);
            Assert.That(Directory.Exists(first), Is.False);
            Assert.That(Directory.Exists(second), Is.False);
        }

        [Test]
        public void ExplicitFreshRoot_StoresOnlyTestFilesAndPreservesPlayerSentinel()
        {
            Directory.CreateDirectory(_playerRoot);
            var sentinel = Path.Combine(_playerRoot, "existing-player.focsave");
            File.WriteAllBytes(sentinel, new byte[] { 1, 2, 3, 4 });
            var root = FreshRoot();
            var resolved = DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true, root);
            var store = new AtomicFileSaveStore(resolved);
            Assert.That(store.Write("smoke", "test-payload", x => x == "test-payload").Success, Is.True);
            Assert.That(store.Read("smoke", x => x == "test-payload").Content, Is.EqualTo("test-payload"));
            Assert.That(File.ReadAllBytes(sentinel), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(Directory.GetFiles(_playerRoot), Has.Length.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("relative")]
        public void BlankOrRelativeRequestedRoot_IsRejected(string root) =>
            Assert.Throws<ArgumentException>(() => DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true, root));

        [Test]
        public void RequestedRootOutsideTemporarySmokeParent_IsRejected() =>
            Assert.Throws<ArgumentException>(() => DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true, Path.Combine(_playerRoot, Guid.NewGuid().ToString("N"))));

        [Test]
        public void NonGuidDirectory_IsRejected() =>
            Assert.Throws<ArgumentException>(() => DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true, Path.Combine(Path.GetTempPath(), "foc-windows-smoke", "existing-saves")));

        [Test]
        public void OverrideWithoutSmokeMode_IsRejected() =>
            Assert.Throws<ArgumentException>(() => DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, false, FreshRoot()));

        [Test]
        public void ExistingDirectory_IsRejectedWithoutChangingItsData()
        {
            var root = FreshRoot();
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "do-not-overwrite");
            File.WriteAllText(file, "keep");
            Assert.Throws<InvalidOperationException>(() => DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true, root));
            Assert.That(File.ReadAllText(file), Is.EqualTo("keep"));
        }

        [Test]
        public void ExistingFile_IsRejected()
        {
            var root = FreshRoot();
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            File.WriteAllText(root, "keep");
            Assert.Throws<InvalidOperationException>(() => DevelopmentSmokeSaveDirectory.Resolve(_playerRoot, true, root));
            Assert.That(File.ReadAllText(root), Is.EqualTo("keep"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PlayerRootOverlap_IsRejectedEvenForFreshGuidPath(int relation)
        {
            var root = FreshRoot();
            var player = relation == 0 ? root : relation == 1 ? Path.Combine(root, "player") : Path.GetDirectoryName(root)!;
            Assert.Throws<ArgumentException>(() => DevelopmentSmokeSaveDirectory.Resolve(player, true, root));
        }

        private string FreshRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "foc-windows-smoke", Guid.NewGuid().ToString("N"));
            _ownedRoots.Add(root);
            return root;
        }
    }
}
