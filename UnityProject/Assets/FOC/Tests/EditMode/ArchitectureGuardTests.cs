using System;
using System.IO;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class ArchitectureGuardTests
    {
        [Test]
        public void DomainSource_DoesNotReferenceUnityEngine()
        {
            var domain = Path.Combine(FindRepositoryRoot(), "UnityProject", "Assets", "FOC", "Domain");
            foreach (var path in Directory.GetFiles(domain, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("UnityEngine"), path);
            }
        }

        [Test]
        public void DomainAssemblyDefinition_HasNoEngineReferencesAndNoDependencies()
        {
            var path = Path.Combine(FindRepositoryRoot(), "UnityProject", "Assets", "FOC", "Domain", "FOC.Domain.asmdef");
            var definition = File.ReadAllText(path).Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

            Assert.That(definition, Does.Contain("\"references\":[]"));
            Assert.That(definition, Does.Contain("\"noEngineReferences\":true"));
        }

        [Test]
        public void UnityTestRunner_WaitsForEditorAndPropagatesItsExitCode()
        {
            var path = Path.Combine(FindRepositoryRoot(), "Tools", "Test-Unity.ps1");
            var script = File.ReadAllText(path);

            Assert.That(script, Does.Contain("-Wait -PassThru"));
            Assert.That(script, Does.Contain("exit $process.ExitCode"));
            Assert.That(script, Does.Not.Contain("exit $LASTEXITCODE"));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "UnityProject")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root could not be located from the test directory.");
        }
    }
}
