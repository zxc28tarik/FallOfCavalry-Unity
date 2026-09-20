using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FOC.Domain.Soldiers;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using FOC.Visuals.Core;

namespace FOC.Tests.VisualPipeline
{
    [TestFixture]
    public sealed class VisualPipelineUnityTests
    {
        [Test]public void GeneratedCatalogLoadsAndValidates(){var asset=AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath);Assert.That(asset,Is.Not.Null);var validation=asset.BuildCoreCatalog().Validate();Assert.That(validation.IsValid,Is.True,string.Join("\n",validation.Errors));Assert.That(asset.Assets.Count,Is.GreaterThanOrEqualTo(20));Assert.That(typeof(VisualSoldier3D),Is.Not.EqualTo(typeof(SoldierInstance)));Assert.That(typeof(CharacterView3D).Name,Is.Not.EqualTo("CharacterState"));}
        [Test]public void FullProjectVisualValidatorPasses(){var result=VisualProjectValidator.ValidateAll();Assert.That(result.IsValid,Is.True,string.Join("\n",result.Errors));}
        [Test]public void GenericHistoricalSmokeConfigurationsAssemble(){var result=VisualSmokeRunner.Run();Assert.That(result.Count,Is.EqualTo(3));Assert.That(result.All(x=>x.Contains("PASS")),Is.True);}
        [Test]public void CanonicalHumanPrefabHasSkinRigSocketsLodAvatarAndCulling(){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(VisualProofAssetGenerator.Root+"/Prefabs/BODY_Human_Proof.prefab");Assert.That(prefab,Is.Not.Null);Assert.That(prefab.GetComponentInChildren<SkinnedMeshRenderer>(true),Is.Not.Null);Assert.That(prefab.GetComponent<LODGroup>().GetLODs().Length,Is.EqualTo(3));Assert.That(prefab.GetComponentsInChildren<Transform>(true).Select(x=>x.name),Does.Contain("Socket_RightHand").And.Contain("Socket_Head"));var animator=prefab.GetComponent<Animator>();Assert.That(animator.cullingMode,Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms));Assert.That(animator.avatar,Is.Not.Null);Assert.That(animator.avatar.isValid&&animator.avatar.isHuman,Is.True);}
        [Test]public void MountPrefabKeepsRiderSocketAndSeparateRig(){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(VisualProofAssetGenerator.Root+"/Prefabs/MNT_Horse_Proof.prefab");Assert.That(prefab.GetComponentsInChildren<Transform>(true).Select(x=>x.name),Does.Contain(CanonicalRig.RiderSocket).And.Contain("MountRoot"));Assert.That(prefab.GetComponentsInChildren<Transform>(true).Select(x=>x.name),Does.Not.Contain("Pelvis"));}
        [Test]public void PoolReturnClearsIdentityBeforeRebind(){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(VisualProofAssetGenerator.ViewPrefabPath).GetComponent<VisualSoldier3D>();var host=new GameObject("PoolTest");try{var pool=host.AddComponent<VisualSoldierPool>();pool.Configure(prefab,2);var first=pool.Rent();Assert.That(pool.LeasedCount,Is.EqualTo(1));pool.Return(first);Assert.That(first.Binding.IsBound,Is.False);var second=pool.Rent();Assert.That(second,Is.SameAs(first));Assert.That(second.Binding.IsBound,Is.False);pool.Return(second);}finally{Object.DestroyImmediate(host);}}
        [Test]public void GeneratedMaterialsAreSharedAssetsNotPerSoldierInstances(){var asset=AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath);foreach(var entry in asset.Assets){var renderers=entry.prefab.GetComponentsInChildren<Renderer>(true);Assert.That(renderers.SelectMany(x=>x.sharedMaterials).All(x=>x!=null&&AssetDatabase.Contains(x)),Is.True,entry.id);}}
        [Test]public void BenchmarkSceneAndMachineReadableSpecsExist(){Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(VisualProofAssetGenerator.BenchmarkScenePath),Is.Not.Null);Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(VisualProofAssetGenerator.Root+"/Specs/BODY_Human_Proof.json"),Is.Not.Null);Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(VisualProofAssetGenerator.Root+"/Specs/WPN_Sword_Proof.json"),Is.Not.Null);}
    }
}
