using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using FOC.Visuals.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Technical pilot assertions, never historical/art/animation acceptance.</summary>
    public sealed class MeshyHasanPilotTests
    {
        private const string ProductionCatalog = "Assets/FOC/Content/Resources/FOC/Visuals/FOC_VisualCatalog.asset";

        [Test]
        public void ImportedPilotRetainsItsOwnHumanAvatarAndTwentyThreeBoneSkin()
        {
            var prefab = Prefab();
            var animator = prefab.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.EqualTo(1), "The supplied pilot is one whole-clothed LOD0, not duplicated body/head/clothing actors.");
            Assert.That(skins[0].sharedMesh, Is.Not.Null);
            Assert.That(skins[0].bones.Length, Is.EqualTo(23));
            Assert.That(skins[0].sharedMesh.bindposes.Length, Is.EqualTo(23));
            Assert.That(skins[0].sharedMaterials.All(m => m != null && AssetDatabase.Contains(m)), Is.True);
            var names = prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (var socket in CanonicalRig.HumanSockets.Values)
                Assert.That(names.Count(n => n == socket), Is.EqualTo(1), socket);
        }

        [Test]
        public void CommittedPilotVerifiesTrueBindMatricesAndIndependentSourceRestHeight()
        {
            // Verify writes only its diagnostic report outside Assets. It does
            // not regenerate or repair the asset that this test is checking.
            var report = MeshyHasanPilotPipeline.Verify();
            Assert.That(report.maximumBindMatrixError, Is.LessThan(.0001f));
            Assert.That(report.heightMeters, Is.EqualTo(1.7800007f).Within(.002f));
            Assert.That(report.preservedSourceBones, Is.EqualTo(23));
        }

        [Test]
        public void SourceBindMatricesRepairPerturbedCloneWithoutChangingMeshOrWeights()
        {
            var prefab = Prefab();
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.GetComponent<Animator>().enabled = false;
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh = skin.sharedMesh;
                var weights = mesh.boneWeights;
                var vertices = mesh.vertices;
                var bindposes = mesh.bindposes;
                Assert.That(BakedActorHeight(instance), Is.EqualTo(1.7800007f).Within(.002f),
                    "The source export's saved Running pose (about1.726m) is not its bind/reference pose.");
                var hips = skin.bones.Single(b => b.name == "Hips");
                hips.localPosition += new Vector3(3f, 7f, -2f);
                hips.localRotation *= Quaternion.Euler(17f, -11f, 9f);
                MeshyHasanPilotPipeline.RestoreSourceBindPose(instance);
                for (var i = 0; i < skin.bones.Length; i++)
                {
                    var expected = skin.transform.localToWorldMatrix * bindposes[i].inverse;
                    var actual = skin.bones[i].localToWorldMatrix;
                    for (var element = 0; element < 16; element++)
                        Assert.That(actual[element], Is.EqualTo(expected[element]).Within(.0001f), skin.bones[i].name);
                }
                Assert.That(BakedActorHeight(instance), Is.EqualTo(1.7800007f).Within(.002f));
                Assert.That(skin.sharedMesh, Is.SameAs(mesh));
                CollectionAssert.AreEqual(weights, mesh.boneWeights);
                CollectionAssert.AreEqual(vertices, mesh.vertices);
                CollectionAssert.AreEqual(bindposes, mesh.bindposes);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void PlayerRestCaptureRestoresExactSourceTrsWithoutMovingPresentationRoots()
        {
            var prefab = Prefab();
            var host = new GameObject("MeshyRestCaptureExternalPlacement");
            host.transform.position = new Vector3(7f, 2f, -3f);
            host.transform.rotation = Quaternion.Euler(0f, 31f, 0f);
            var instance = Object.Instantiate(prefab, host.transform, false);
            instance.transform.localPosition = new Vector3(.17f, .3f, -.2f);
            instance.transform.localRotation = Quaternion.Euler(0f, 13f, 0f);
            instance.transform.localScale = Vector3.one * .85f;
            var sourceRootPosition = instance.transform.localPosition;
            var sourceRootRotation = instance.transform.localRotation;
            var sourceRootScale = instance.transform.localScale;
            var outerPosition = host.transform.position;
            var outerRotation = host.transform.rotation;
            try
            {
                var animator = instance.GetComponent<Animator>();
                animator.Rebind();
                animator.Update(0f);
                animator.enabled = false;
                var expected = new HashSet<Transform>();
                var sourceBones = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.bones).Distinct().ToArray();
                Assert.That(sourceBones.Length, Is.EqualTo(23));
                foreach (var bone in sourceBones)
                    for (var node = bone; node != prefab.transform; node = node.parent) expected.Add(node);
                foreach (var source in expected)
                {
                    var path = AnimationUtility.CalculateTransformPath(source, prefab.transform);
                    var target = instance.transform.Find(path);
                    Assert.That(target, Is.Not.Null, path);
                    target.localPosition += new Vector3(.03f, -.02f, .01f);
                    target.localRotation *= Quaternion.Euler(9f, -8f, 7f);
                    target.localScale *= 1.07f;
                }
                var restored = MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab, instance);
                Assert.That(restored, Is.EqualTo(expected.Count));
                foreach (var source in expected)
                {
                    var path = AnimationUtility.CalculateTransformPath(source, prefab.transform);
                    var target = instance.transform.Find(path);
                    Assert.That(target.localPosition, Is.EqualTo(source.localPosition), path);
                    // Transform normalizes assigned quaternions; verify source
                    // orientation to float precision, not struct bit equality.
                    for (var component = 0; component < 4; component++)
                        Assert.That(target.localRotation[component], Is.EqualTo(source.localRotation[component]).Within(.000001f), path);
                    Assert.That(target.localScale, Is.EqualTo(source.localScale), path);
                }
                Assert.That(instance.transform.parent, Is.SameAs(host.transform));
                Assert.That(instance.transform.localPosition, Is.EqualTo(sourceRootPosition));
                Assert.That(instance.transform.localRotation, Is.EqualTo(sourceRootRotation));
                Assert.That(instance.transform.localScale, Is.EqualTo(sourceRootScale));
                Assert.That(host.transform.position, Is.EqualTo(outerPosition));
                Assert.That(host.transform.rotation, Is.EqualTo(outerRotation));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ForeignHumanAvatarStillFailsUnchangedCanonicalModularRigContract()
        {
            var prefab = Prefab();
            var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var names = skin.bones.Select(b => b.name).ToArray();
            var sockets = prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name).Where(n => n.StartsWith("Socket_", StringComparison.Ordinal));
            var descriptor = new RigDescriptor(RigKind.Humanoid, names, sockets,
                skin.sharedMesh.bindposes.Length > 0, 4, "meters-1.0", "+Z");
            var result = RigValidator.Validate(descriptor);
            Assert.That(CanonicalRig.HumanBones.Count, Is.EqualTo(18));
            Assert.That(result.IsValid, Is.False, "Unity Human compatibility must not silently certify the foreign rig as FOC canonical.");
            Assert.That(result.Errors.Any(e => e.StartsWith("Unknown bone:", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void CatalogHonestlyUsesOneWholeCharacterAndDoesNotInventModularPartsOrLods()
        {
            using (var trial = new Trial())
            {
                var catalog = trial.Catalog;
                MeshyHasanPilotCatalog.Validate(catalog);
                Assert.That(AssetDatabase.Contains(catalog), Is.False);
                Assert.That(catalog.Assets.Count, Is.EqualTo(1));
                Assert.That(catalog.Assets[0].category, Is.EqualTo(VisualAssetCategory.ConsolidatedCharacter));
                Assert.That(catalog.Assets[0].prefab, Is.SameAs(Prefab()));
                Assert.That(catalog.Assets[0].lodCount, Is.EqualTo(1), "Only the supplied LOD0 exists; three LODs remain a later gate.");
                Assert.That(catalog.Assets[0].rendererCount, Is.EqualTo(1));
                Assert.That(catalog.Profiles.Count, Is.EqualTo(1));
                var profile = catalog.Profiles[0];
                Assert.That(profile.qualityTier, Is.EqualTo(VisualQualityTier.Standard));
                Assert.That(profile.bodyAssetId, Is.EqualTo(MeshyHasanPilotCatalog.CharacterId));
                Assert.That(profile.headAssetId, Is.EqualTo(profile.bodyAssetId));
                Assert.That(profile.clothingAssetId, Is.EqualTo(profile.bodyAssetId));
                Assert.That(profile.headgearAssetId, Is.Empty);
                Assert.Throws<KeyNotFoundException>(() => catalog.GetPrefab(catalog.CrowdAssetId));
            }
        }

        [TestCase(VisualQualityTier.Narrative)]
        [TestCase(VisualQualityTier.Crowd)]
        public void UnsupportedTierIsRejectedBeforeAnyActorIsInstantiated(VisualQualityTier tier)
        {
            using (var trial = new Trial())
            {
                trial.Catalog.Profiles[0].qualityTier = tier;
                var view = trial.NewView();
                Assert.Throws<InvalidOperationException>(() => trial.Assemble(view, Soldier(0)));
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(trial.Assembler.Metrics.CreatedRepresentations, Is.Zero);
                Assert.That(view.ActiveRepresentationRoot, Is.Null);
            }
        }

        [Test]
        public void ExistingAssemblerReusesExactlyOneWholeActorAndClearsOldIdentity()
        {
            using (var trial = new Trial())
            {
                var view = trial.NewView();
                var firstSoldier = Soldier(0);
                var before = firstSoldier.Loadout;
                var originalProfile = trial.SourceTroop.VisualProfileId;
                var first = trial.Assemble(view, firstSoldier);
                var root = view.ActiveRepresentationRoot;
                Assert.That(first.RepresentationKind, Is.EqualTo(VisualRepresentationKind.Consolidated));
                Assert.That(view.SpawnedModules.Count, Is.EqualTo(1));
                Assert.That(view.SpawnedModules[0].name, Is.EqualTo(MeshyHasanPilotCatalog.CharacterId));
                Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Animator>(true).Single().avatar, Is.SameAs(Prefab().GetComponent<Animator>().avatar));
                view.ReleaseVisual();
                Assert.That(view.Binding.IsBound, Is.False);
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(trial.Assembler.PooledInstanceCount, Is.EqualTo(1));
                var secondSoldier = Soldier(1);
                var second = trial.Assemble(view, secondSoldier);
                Assert.That(view.ActiveRepresentationRoot, Is.SameAs(root));
                Assert.That(second.Signature, Is.EqualTo(first.Signature));
                Assert.That(view.Binding.SoldierId.Value, Is.EqualTo(secondSoldier.Id));
                Assert.That(trial.Assembler.Metrics.CreatedRepresentations, Is.EqualTo(1));
                Assert.That(trial.Assembler.Metrics.PoolHits, Is.EqualTo(1));
                Assert.That(trial.Assembler.Metrics.DestroyedRepresentations, Is.Zero);
                Assert.That(firstSoldier.Loadout, Is.SameAs(before));
                Assert.That(trial.SourceTroop.VisualProfileId, Is.EqualTo(originalProfile));
            }
        }

        [Test]
        public void TwelveSimultaneousSoldiersHaveTwelveDistinctActorsThenReusePool()
        {
            using (var trial = new Trial())
            {
                var views = Enumerable.Range(0, 12).Select(_ => trial.NewView()).ToArray();
                var soldiers = Enumerable.Range(0, 12).Select(i => Soldier(i)).ToArray();
                for (var i = 0; i < views.Length; i++) trial.Assemble(views[i], soldiers[i]);
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.EqualTo(12));
                Assert.That(views.Select(v => v.ActiveRepresentationRoot.GetInstanceID()).Distinct().Count(), Is.EqualTo(12));
                Assert.That(views.All(v => v.SpawnedModules.Count == 1), Is.True);
                Assert.That(views.All(v => v.ActiveRepresentationRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 1), Is.True);
                Assert.That(trial.Assembler.CachedVariantCount, Is.EqualTo(1));
                foreach (var view in views) view.ReleaseVisual();
                Assert.That(trial.Assembler.PooledInstanceCount, Is.EqualTo(12));
                for (var i = 0; i < views.Length; i++) trial.Assemble(views[i], Soldier(i + 12));
                Assert.That(trial.Assembler.Metrics.CreatedRepresentations, Is.EqualTo(12));
                Assert.That(trial.Assembler.Metrics.PoolHits, Is.EqualTo(12));
                Assert.That(views.Select(v => v.Binding.SoldierId.Value).Distinct().Count(), Is.EqualTo(12));
            }
        }

        [Test]
        public void ExistingOuterViewPoolAndVariantPoolBothReuseWithoutDuplicateSkin()
        {
            using (var trial = new Trial())
            {
                var template = new GameObject("MeshyPilotEmptyViewTemplate").AddComponent<VisualSoldier3D>();
                template.transform.SetParent(trial.Host.transform, false);
                template.gameObject.SetActive(false);
                var pool = trial.Host.AddComponent<VisualSoldierPool>();
                pool.Configure(template, 2);
                var first = pool.Rent();
                trial.Assemble(first, Soldier(0));
                var root = first.ActiveRepresentationRoot;
                pool.Return(first);
                Assert.That(first.Binding.IsBound, Is.False);
                var second = pool.Rent();
                trial.Assemble(second, Soldier(1));
                Assert.That(second, Is.SameAs(first));
                Assert.That(second.ActiveRepresentationRoot, Is.SameAs(root));
                Assert.That(second.ActiveRepresentationRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
                Assert.That(pool.CreatedViewCount, Is.EqualTo(1));
                Assert.That(pool.ReusedViewCount, Is.EqualTo(1));
                pool.Return(second);
            }
        }

        [Test]
        public void PersistentWeaponLoadoutControlsSeparateSocketModuleWithoutMutation()
        {
            // A proof weapon is used only as an architecture fixture. This does
            // not accept its art, socket grip or any mounted/equipment fit.
            var fixture = VisualSmokeRunner.CreateProof("deli");
            var proof = AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath);
            var sourceJson = EditorJsonUtility.ToJson(proof);
            var sword = fixture.soldier.Loadout.OrderedWeapons.Single(w => w.Slot == WeaponSlot.Sidearm);
            var loadout = new SoldierLoadout(new[] { new WeaponSlotAssignment(WeaponSlot.Main, sword.EquipmentId) });
            var soldier = new SoldierInstance(fixture.soldier.Id, fixture.soldier.UnitGroupId, fixture.soldier.TroopDefinitionId,
                fixture.soldier.Recruitment, loadout, fixture.soldier.CombatRoleId);
            using (var trial = new Trial(proof))
            {
                var view = trial.NewView();
                var plan = MeshyHasanPilotCatalog.Assemble(trial.Assembler, trial.Catalog, view, soldier, fixture.troop, fixture.equipment);
                var visualId = trial.Catalog.BuildCoreCatalog().GetEquipment(fixture.equipment[sword.EquipmentId].Definition).AssetId.Value;
                Assert.That(view.SpawnedModules.Count, Is.EqualTo(2));
                var weapon = view.SpawnedModules.Single(m => m.name == visualId);
                Assert.That(view.TryGetSocket(VisualSocket.RightHand, out var socket), Is.True);
                Assert.That(weapon.transform.parent, Is.SameAs(socket));
                Assert.That(soldier.Loadout, Is.SameAs(loadout));
                Assert.That(fixture.soldier.Loadout.Mount.HasValue, Is.True, "Do not edit the source soldier to prepare an infantry pilot.");
                var unarmed = new SoldierInstance(soldier.Id, soldier.UnitGroupId, soldier.TroopDefinitionId, soldier.Recruitment,
                    new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), soldier.CombatRoleId);
                var next = MeshyHasanPilotCatalog.Assemble(trial.Assembler, trial.Catalog, view, unarmed, fixture.troop, fixture.equipment);
                Assert.That(next.Signature, Is.Not.EqualTo(plan.Signature));
                Assert.That(view.SpawnedModules.Count, Is.EqualTo(1));
                Assert.That(view.SpawnedModules.Any(m => m.name == visualId), Is.False);
                Assert.That(EditorJsonUtility.ToJson(proof), Is.EqualTo(sourceJson));
            }
        }

        [Test]
        public void WholeClothedPilotFailsClosedForUnsupportedBodyArmor()
        {
            var fixture = VisualSmokeRunner.CreateProof("bostanci");
            var proof = AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath);
            using (var trial = new Trial(proof))
            {
                Assert.That(trial.Catalog.Assets.Any(a => a.category == VisualAssetCategory.BodyArmor), Is.False);
                var view = trial.NewView();
                Assert.Throws<KeyNotFoundException>(() => MeshyHasanPilotCatalog.Assemble(trial.Assembler, trial.Catalog,
                    view, fixture.soldier, fixture.troop, fixture.equipment));
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(view.ActiveRepresentationRoot, Is.Null);
            }
        }

        [Test]
        public void PilotFailsClosedForMountedLoadoutWithoutRiderFitOrMountedClip()
        {
            var fixture = VisualSmokeRunner.CreateProof("deli");
            var proof = AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath);
            using (var trial = new Trial(proof))
            {
                Assert.That(trial.Catalog.Assets.Any(a => a.category == VisualAssetCategory.Mount ||
                    a.category == VisualAssetCategory.Harness || a.category == VisualAssetCategory.MountArmor), Is.False);
                var view = trial.NewView();
                Assert.Throws<KeyNotFoundException>(() => MeshyHasanPilotCatalog.Assemble(trial.Assembler, trial.Catalog,
                    view, fixture.soldier, fixture.troop, fixture.equipment));
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(view.ActiveRepresentationRoot, Is.Null);
                Assert.That(fixture.soldier.Loadout.Mount.HasValue, Is.True);
            }
        }

        [Test]
        public void PilotDoesNotChangeProductionReferencesOrPersistCatalogAliases()
        {
            var production = AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(ProductionCatalog);
            Assert.That(production, Is.Not.Null);
            var bytes = File.ReadAllBytes(ProductionCatalog);
            var json = EditorJsonUtility.ToJson(production);
            using (var trial = new Trial())
            {
                trial.Assemble(trial.NewView(), Soldier(0));
                Assert.That(ReferenceEquals(trial.Catalog, production), Is.False);
                Assert.That(production.Profiles.Any(p => p.visualProfileId == MeshyHasanPilotCatalog.ProfileId), Is.False);
                Assert.That(production.Assets.Any(a => a.id == MeshyHasanPilotCatalog.CharacterId), Is.False);
            }
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(ProductionCatalog));
            Assert.That(EditorJsonUtility.ToJson(production), Is.EqualTo(json));
        }

        private static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotCatalog.PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run the isolated Meshy importer first; a missing real asset must fail, not skip.");
            return prefab;
        }

        private static float BakedActorHeight(GameObject instance)
        {
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked);
                var vertices = baked.vertices.Select(v => instance.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                return vertices.Max(v => v.y) - vertices.Min(v => v.y);
            }
            finally { Object.DestroyImmediate(baked); }
        }

        private static TroopDefinition Troop() => new TroopDefinition(TroopDefinitionId.Create("meshy-hasan-review"), "Meshy Hasan pilot",
            UnitClassId.Create("pilot-review-class"), CombatRoleId.Create("pilot-review-role"), MountContext.InfantryOnly,
            VisualProfileId.Create("pilot-original-profile-not-mutated"));

        private static SoldierInstance Soldier(int index)
        {
            var troop = Troop();
            return new SoldierInstance(SoldierId.Create("meshy-hasan-review-" + index), UnitGroupId.Create("pilot-review-group"), troop.Id,
                new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("pilot-review-source"), RecruitmentRecordId.Create("pilot-review-record-" + index)),
                new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), troop.DefaultCombatRoleId);
        }

        private sealed class Trial : IDisposable
        {
            public readonly GameObject Host;
            public readonly VisualCatalogAsset Catalog;
            public readonly VisualSoldier3DAssembler Assembler;
            public readonly TroopDefinition SourceTroop = Troop();
            private readonly Dictionary<EquipmentInstanceId, EquipmentInstance> equipment = new Dictionary<EquipmentInstanceId, EquipmentInstance>();

            public Trial(VisualCatalogAsset externalEquipment = null)
            {
                Catalog = MeshyHasanPilotCatalog.Create(Prefab(), externalEquipment);
                Host = new GameObject("IsolatedMeshyHasanPilotTest");
                Assembler = Host.AddComponent<VisualSoldier3DAssembler>();
            }

            public VisualSoldier3D NewView()
            {
                var view = new GameObject("MeshyPilotView").AddComponent<VisualSoldier3D>();
                view.transform.SetParent(Host.transform, false);
                return view;
            }

            public VisualAssemblyPlan Assemble(VisualSoldier3D view, SoldierInstance soldier) =>
                MeshyHasanPilotCatalog.Assemble(Assembler, Catalog, view, soldier, SourceTroop, equipment);

            public void Dispose()
            {
                foreach (var view in Host.GetComponentsInChildren<VisualSoldier3D>(true)) view.ReleaseVisual();
                Assembler.DisposeCache();
                Object.DestroyImmediate(Host);
                Object.DestroyImmediate(Catalog);
            }
        }
    }
}
