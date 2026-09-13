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
        public void CharacterDomain_DoesNotReferencePresentationOrPresentationTypes()
        {
            var domain = Path.Combine(FindRepositoryRoot(), "UnityProject", "Assets", "FOC", "Domain", "Characters");
            foreach (var path in Directory.GetFiles(domain, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("FOC.Presentation"), path);
                Assert.That(source, Does.Not.Contain("MonoBehaviour"), path);
                Assert.That(source, Does.Not.Contain("GameObject"), path);
                Assert.That(source, Does.Not.Contain("System.Random"), path);
                Assert.That(source, Does.Not.Contain("DateTime.Now"), path);
            }
        }

        [Test]
        public void SocialDomain_DoesNotExposeForbiddenOwnershipOrLaterPackageContracts()
        {
            var root = Path.Combine(FindRepositoryRoot(), "UnityProject", "Assets", "FOC", "Domain");
            var clique = File.ReadAllText(Path.Combine(root, "Cliques", "CliqueContracts.cs"));
            Assert.That(clique, Does.Not.Contain("Inventory {"));
            Assert.That(clique, Does.Not.Contain("SoldierIds {"));
            Assert.That(clique, Does.Not.Contain("MoraleBonus {"));
            Assert.That(clique, Does.Not.Contain("DamageBonus {"));
            Assert.That(clique, Does.Not.Contain("ReligionId"));
            Assert.That(clique, Does.Not.Contain("SectId"));
            Assert.That(clique, Does.Not.Contain("Assassination"));
            Assert.That(clique, Does.Not.Contain("Sabotage"));
        }

        [Test]
        public void CityDomain_DoesNotDependOnPresentationEconomyArmyBattleOrEngine()
        {
            var domain=Path.Combine(FindRepositoryRoot(),"UnityProject","Assets","FOC","Domain","Cities");
            foreach(var path in Directory.GetFiles(domain,"*.cs",SearchOption.AllDirectories))
            {
                var source=File.ReadAllText(path);
                Assert.That(source,Does.Not.Contain("UnityEngine"),path);
                Assert.That(source,Does.Not.Contain("FOC.Presentation"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Economy"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Armies"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Battle"),path);
                Assert.That(source,Does.Not.Contain("DateTime.Now"),path);
                Assert.That(source,Does.Not.Contain("System.Random"),path);
            }
        }

        [Test]
        public void EconomyDomain_DoesNotDependOnPresentationArmyBattleDiplomacyOrEngine()
        {
            var domain=Path.Combine(FindRepositoryRoot(),"UnityProject","Assets","FOC","Domain","Economy");
            foreach(var path in Directory.GetFiles(domain,"*.cs",SearchOption.AllDirectories))
            {
                var source=File.ReadAllText(path);
                Assert.That(source,Does.Not.Contain("UnityEngine"),path);
                Assert.That(source,Does.Not.Contain("FOC.Presentation"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Armies"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Battle"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Diplomacy"),path);
                Assert.That(source,Does.Not.Contain("System.Random"),path);
                Assert.That(source,Does.Not.Contain("DateTime.Now"),path);
            }
        }

        [Test]
        public void DiplomacyDomain_DoesNotDependOnPresentationArmyBattleAiOrEngine()
        {
            var domain=Path.Combine(FindRepositoryRoot(),"UnityProject","Assets","FOC","Domain","Diplomacy");
            foreach(var path in Directory.GetFiles(domain,"*.cs",SearchOption.AllDirectories))
            {
                var source=File.ReadAllText(path);
                Assert.That(source,Does.Not.Contain("UnityEngine"),path);
                Assert.That(source,Does.Not.Contain("FOC.Presentation"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Armies"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Battle"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.AI"),path);
                Assert.That(source,Does.Not.Contain("CampaignRuntimeState"),"Domain diplomacy contracts must not expose an omniscient world-state query.");
                Assert.That(source,Does.Not.Contain("System.Random"),path);
                Assert.That(source,Does.Not.Contain("DateTime.Now"),path);
            }
        }

        [Test]
        public void MilitaryDomain_DoesNotDependOnPresentationBattleAiOrEngine()
        {
            var domain=Path.Combine(FindRepositoryRoot(),"UnityProject","Assets","FOC","Domain","Military");
            foreach(var path in Directory.GetFiles(domain,"*.cs",SearchOption.AllDirectories))
            {
                var source=File.ReadAllText(path);
                Assert.That(source,Does.Not.Contain("UnityEngine"),path);
                Assert.That(source,Does.Not.Contain("FOC.Presentation"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Battle"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.AI"),path);
                Assert.That(source,Does.Not.Contain("System.Random"),path);
                Assert.That(source,Does.Not.Contain("DateTime.Now"),path);
                Assert.That(source,Does.Not.Contain("SoldierInstance"),path);
                Assert.That(source,Does.Not.Contain("WeaponState"),path);
                Assert.That(source,Does.Not.Contain("ArmorState"),path);
            }
        }

        [Test]
        public void SoldierDomain_DoesNotDependOnPresentationBattleAiEngineOrHiddenRandomness()
        {
            var domain=Path.Combine(FindRepositoryRoot(),"UnityProject","Assets","FOC","Domain","Soldiers");
            foreach(var path in Directory.GetFiles(domain,"*.cs",SearchOption.AllDirectories))
            {
                var source=File.ReadAllText(path);
                Assert.That(source,Does.Not.Contain("UnityEngine"),path);
                Assert.That(source,Does.Not.Contain("FOC.Presentation"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.Battle"),path);
                Assert.That(source,Does.Not.Contain("FOC.Domain.AI"),path);
                Assert.That(source,Does.Not.Contain("System.Random"),path);
                Assert.That(source,Does.Not.Contain("DateTime.Now"),path);
                Assert.That(source,Does.Not.Contain("VisualSoldier"),path);
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
            Assert.That(script, Does.Not.Contain("'-quit'"), "Unity Test Framework owns shutdown for -runTests; early -quit can cancel first import/package resolution.");
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
