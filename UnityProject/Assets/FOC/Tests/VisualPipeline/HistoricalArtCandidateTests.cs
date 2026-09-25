using System;
using System.IO;
using System.Linq;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    public sealed class HistoricalArtCandidateTests
    {
        [TestCase("BODY_Human_Proof")][TestCase("Assets/FOC/Generated/VisualProof/Meshes/Renamed.asset")]
        public void ProductionGateRejectsFixtureIdsAndRenamedFixturePaths(string path)=>Assert.That(ProductionVisualCatalogGate.IsProof(path),Is.True);
        [Test]public void HistoricalBaselineCannotPassProductionGate(){var proof=AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath);Assert.That(ProductionVisualCatalogGate.Audit(proof).Any(e=>e.StartsWith("Proof dependency:",StringComparison.Ordinal)),Is.True);}
        [Test]public void EveryDccSourceHasFiniteTopologyNormalizedWeightsAndThreeLods(){foreach(var path in Directory.GetFiles(HistoricalArtCandidatePipeline.SourceRoot,"*.focmesh.json",SearchOption.AllDirectories)){var source=JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(path));Assert.DoesNotThrow(()=>HistoricalArtCandidatePipeline.ValidateSource(source),path);Assert.That(source.status,Is.EqualTo("Draft"),"Technical tests must not silently promote authoring status.");}}
        [Test]public void HorseDraftUsesVisibleSkinnedTopologyNotDisabledValidationProxy(){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.MountRoot+"/MNT_Horse_Anatolian_01.prefab");Assert.That(prefab,Is.Not.Null);var lods=prefab.GetComponent<LODGroup>().GetLODs();Assert.That(lods.Length,Is.EqualTo(3));foreach(var lod in lods){Assert.That(lod.renderers.Length,Is.EqualTo(1));var skin=lod.renderers[0] as SkinnedMeshRenderer;Assert.That(skin,Is.Not.Null);Assert.That(skin.enabled,Is.True);Assert.That(skin.sharedMesh.vertexCount,Is.GreaterThan(1000));Assert.That(skin.sharedMesh.bindposes.Length,Is.EqualTo(skin.bones.Length));}Assert.That(prefab.GetComponentsInChildren<Transform>().Select(t=>t.name),Does.Contain("FrontLowerLeg_L").And.Contain("BackLowerLeg_R").And.Contain("Tail_02").And.Contain("Socket_Rider"));}
        [Test]public void HorseDraftHasFiveDistinctAnimationStatesWithLegMotion(){var root=HistoricalArtCandidatePipeline.MountRoot;foreach(var name in new[]{"Idle","Walk","Trot","Gallop","Turn"}){var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(root+"/ANM_Horse_"+name+".anim");Assert.That(clip,Is.Not.Null);Assert.That(clip.length,Is.GreaterThan(0));if(name!="Idle")Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b=>b.path.Contains("LowerLeg")),Is.True);}}
        [Test]public void HumanDraftsUseValidSharedHumanoidAvatars(){foreach(var id in new[]{"BODY_OttomanMale_Standard","BODY_OttomanMale_Lean","BODY_OttomanMale_Stocky","CHR_HasanAga_01"}){var p=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/"+id+".prefab");Assert.That(p,Is.Not.Null);Assert.That(p.GetComponent<Animator>().avatar.isHuman,Is.True);Assert.That(p.GetComponent<Animator>().avatar.isValid,Is.True);Assert.That(p.GetComponentsInChildren<Transform>().Select(t=>t.name),Does.Contain("Socket_RightHand").And.Contain("Socket_Head"));}}
        [Test]public void CandidateDependencyGraphDoesNotUseProofMeshes(){foreach(var folder in new[]{HistoricalArtCandidatePipeline.MountRoot,HistoricalArtCandidatePipeline.CharacterRoot})foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{folder})){var path=AssetDatabase.GUIDToAssetPath(guid);Assert.That(AssetDatabase.GetDependencies(path,true).Any(ProductionVisualCatalogGate.IsProof),Is.False,path);}}
        [Test]public void SourceValidationRejectsFlippedWinding(){var path=Directory.GetFiles(HistoricalArtCandidatePipeline.SourceRoot,"MNT_Horse_Anatolian_01.focmesh.json",SearchOption.AllDirectories).Single();var source=JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(path));foreach(var part in source.lods[0].parts)for(var i=0;i<part.triangles.Length;i+=3){var a=part.triangles[i];part.triangles[i]=part.triangles[i+2];part.triangles[i+2]=a;}Assert.Throws<InvalidOperationException>(()=>HistoricalArtCandidatePipeline.ValidateSource(source));}
        [Test]public void ProofRegenerationCannotOverwriteRuntimeCatalog()
        {
            var before=File.ReadAllBytes(VisualProofAssetGenerator.RuntimeCatalogPath);
            var fixtureSnapshot=Directory.GetFiles(VisualProofAssetGenerator.Root,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);
            try{VisualProofAssetGenerator.GenerateAll();AssetDatabase.SaveAssets();Assert.That(File.ReadAllBytes(VisualProofAssetGenerator.RuntimeCatalogPath),Is.EqualTo(before));}
            finally{foreach(var entry in fixtureSnapshot)File.WriteAllBytes(entry.Key,entry.Value);AssetDatabase.Refresh();}
        }
    }
}
