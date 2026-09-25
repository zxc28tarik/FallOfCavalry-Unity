using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    public sealed class HistoricalArtRepairTests
    {
        [Test]public void RepeatedProofGenerationPreservesFixtureFileIds()
        {
            // Unity matches prefab children by name when overwriting. Reusing
            // "Silhouette" at every LOD caused random internal fileID churn.
            var paths=new[]{"BODY_Human_Proof","CHR_Consolidated_Proof","MNT_Horse_Proof"}.Select(id=>VisualProofAssetGenerator.Root+"/Prefabs/"+id+".prefab").ToArray();
            var snapshot=Directory.GetFiles(VisualProofAssetGenerator.Root,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);
            try
            {
                VisualProofAssetGenerator.GenerateAll();AssetDatabase.SaveAssets();var first=paths.ToDictionary(p=>p,File.ReadAllBytes);
                VisualProofAssetGenerator.GenerateAll();AssetDatabase.SaveAssets();
                foreach(var path in paths)Assert.That(File.ReadAllBytes(path),Is.EqualTo(first[path]),path);
            }
            finally{foreach(var item in snapshot)File.WriteAllBytes(item.Key,item.Value);AssetDatabase.Refresh();}
        }
        [Test]public void HandAndMetacarpalSkinCannotFollowHeadBone()
        {
            foreach(var id in new[]{"BODY_OttomanMale_Standard","CHR_HasanAga_01","CHR_Sipahi_01"})
            {
                var source=Read(id);var head=Array.FindIndex(source.bones,b=>b.name=="Head");var checkedVertices=0;
                foreach(var part in source.lods[0].parts.Where(p=>p.material.StartsWith("Skin",StringComparison.Ordinal)))
                for(var i=0;i<part.positions.Length/3;i++)
                {
                    var p=Point(part,i);if(Mathf.Abs(p.x)<.46f||p.y>1.20f)continue;checkedVertices++;
                    for(var w=0;w<4;w++)Assert.That(part.boneIndices[i*4+w]==head&&part.boneWeights[i*4+w]>.00001f,Is.False,"Palm incorrectly weighted to Head: "+id);
                }
                Assert.That(checkedVertices,Is.GreaterThan(100));
            }
        }
        [Test]public void DiagnosticLimbPosePreservesLengthsAndReachesTarget()
        {
            var root=new GameObject("Pose test");
            try
            {
                var upper=new GameObject("upper").transform;upper.SetParent(root.transform,false);
                var lower=new GameObject("lower").transform;lower.SetParent(upper,false);lower.localPosition=Vector3.down*.45f;
                var end=new GameObject("end").transform;end.SetParent(lower,false);end.localPosition=Vector3.down*.44f;
                var target=new Vector3(.28f,-.72f,.14f);
                HistoricalArtPoseReview.Limb(upper,lower,end,target,new Vector3(1,0,1));
                Assert.That(Vector3.Distance(end.position,target),Is.LessThan(.0001f));
                Assert.That(lower.localPosition.magnitude,Is.EqualTo(.45f).Within(.0001f));Assert.That(end.localPosition.magnitude,Is.EqualTo(.44f).Within(.0001f));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test]public void AuthoredRiderSocketIsAboveActualBackSurface()
        {
            var source=Read("MNT_Horse_Anatolian_01");var rider=source.bones.Single(b=>b.name=="Socket_Rider").position;
            var coat=source.lods[0].parts.Single(p=>p.material=="HorseCoat");
            var top=Enumerable.Range(0,coat.positions.Length/3).Select(i=>Point(coat,i)).Where(p=>Mathf.Abs(p.x-rider[0])<.15f&&Mathf.Abs(p.z-rider[2])<.15f).Max(p=>p.y);
            Assert.That(rider[1]-top,Is.InRange(.025f,.14f),"Seat must not remain embedded in the licensed horse. This is not gait acceptance.");
        }
        [Test]public void LicensedSkinAndHairAreActualCatalogIndependentDependencies()
        {
            foreach(var name in new[]{"Skin","SkinMature","HairCards01","HairCards02"})
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(HistoricalArtCandidatePipeline.MaterialRoot+"/MAT_"+name+".mat");
                Assert.That(mat,Is.Not.Null);Assert.That(AssetDatabase.GetAssetPath(mat.mainTexture),Does.EndWith("/"+name+"_D.png"));
                Assert.That(mat.mainTextureScale,Is.EqualTo(Vector2.one));
                if(name.StartsWith("HairCards",StringComparison.Ordinal))Assert.That(mat.IsKeywordEnabled("_ALPHATEST_ON"),Is.True);
            }
        }
        [Test]public void ReviewBuildRestoresSettingsWithoutTruncatingMappedFile()
        {
            var path=Path.GetTempFileName();var expected=new byte[]{9,8,7,6};
            try
            {
                File.WriteAllBytes(path,new byte[]{1,2,3,4});
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using(var map=MemoryMappedFile.CreateFromFile(stream,null,0,MemoryMappedFileAccess.Read,HandleInheritability.None,true))
                using(var view=map.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read))
                {
                    HistoricalArtReviewBuild.RestoreSettingsSnapshot(path,expected);
                    Assert.That(File.ReadAllBytes(path),Is.EqualTo(expected));
                    Assert.That(view.ReadByte(0),Is.EqualTo(1),"Old mapping survives replacement.");
                }
            }
            finally{File.Delete(path);}
        }
        [TestCase("CLTH_LightSoldier_01")]
        [TestCase("CLTH_Infantry_01")]
        [TestCase("ARM_OttomanMail_01")]
        public void ShoulderAndArmpitAreContinuousAuthoredTopology(string id)
        {
            var part=Read(id).lods[0].parts[0];
            var edges=Edges(part);
            foreach(var edge in edges.Values.Where(e=>e.count==1))
            {
                var mid=(edge.a+edge.b)*.5f;
                Assert.That(mid.y>1.14f&&mid.y<1.46f&&Mathf.Abs(mid.x)>.13f&&Mathf.Abs(mid.x)<.40f,Is.False,"Open shoulder/armpit at "+mid+" in "+id);
            }
            Assert.That(part.positions.Length,Is.GreaterThan(3000));
        }
        [TestCase("CLTH_LightSoldier_01")]
        [TestCase("ARM_OttomanMail_01")]
        public void SkirtCannotAccidentallyBridgeWaistToCuff(string id)
        {
            var p=Read(id).lods[0].parts[0];
            foreach(var edge in Edges(p).Values)Assert.That(Vector3.Distance(edge.a,edge.b),Is.LessThan(.26f),"Long invalid garment bridge at "+edge.a+" -> "+edge.b);
        }
        [Test]public void WaistAndSkirtAreWeldedNotOverlappingDisconnectedCaps()
        {
            var part=Read("ARM_OttomanMail_01").lods[0].parts[0];
            var seam=Edges(part).Values.Where(e=>Mathf.Abs(e.a.y-1.01f)<.00001f&&Mathf.Abs(e.b.y-1.01f)<.00001f).ToArray();
            Assert.That(seam.Length,Is.GreaterThan(20));foreach(var edge in seam)Assert.That(edge.count,Is.EqualTo(2));
        }
        [Test]public void BootsAreTailoredClosedSolesNotOffsetBareFeet()
        {
            var source=Read("CLTH_InnerGarment_01");Assert.That(source.lods[0].parts.Length,Is.EqualTo(4));
            foreach(var boot in source.lods[0].parts.Skip(2))
            {
                Assert.That(boot.material,Is.EqualTo("Leather"));
                var points=Enumerable.Range(0,boot.positions.Length/3).Select(i=>Point(boot,i)).ToArray();
                Assert.That(points.Min(p=>p.y),Is.EqualTo(.018f).Within(.0001f));
                Assert.That(points.Max(p=>p.y),Is.EqualTo(.355f).Within(.0001f));
                Assert.That(points.Max(p=>p.z)-points.Min(p=>p.z),Is.InRange(.28f,.32f));
                Assert.That(Edges(boot).Values.Any(e=>e.count==1&&Mathf.Min(e.a.y,e.b.y)<.019f),Is.False,"Open sole.");
            }
        }
        [TestCase("Mail")][TestCase("Skin")][TestCase("Wood")][TestCase("Leather")]
        public void SurfaceAuthoringIsDeterministicAndTiles(string category)
        {
            for(var y=0;y<256;y+=17)for(var x=0;x<256;x+=13)
            {
                var h=HistoricalArtSurfaceAuthoring.Height(category,x,y);
                Assert.That(h,Is.EqualTo(HistoricalArtSurfaceAuthoring.Height(category,x+256,y-256)));
                Assert.That(float.IsNaN(h)||float.IsInfinity(h),Is.False);
            }
        }
        [Test]public void SurfaceStudiesHaveDifferentMaterialResponse()
        {
            var mail=Enumerable.Range(0,256).Select(x=>HistoricalArtSurfaceAuthoring.Height("Mail",x,16)).ToArray();
            var wood=Enumerable.Range(0,256).Select(x=>HistoricalArtSurfaceAuthoring.Height("Wood",x,16)).ToArray();
            Assert.That(mail,Is.Not.EqualTo(wood));Assert.That(mail.Max()-mail.Min(),Is.GreaterThan(.5f));
        }
        private static HistoricalArtCandidatePipeline.SourceAsset Read(string id)=>JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(Directory.GetFiles(HistoricalArtCandidatePipeline.SourceRoot,id+".focmesh.json",SearchOption.AllDirectories).Single()));
        private static Vector3 Point(HistoricalArtCandidatePipeline.SourcePart p,int i)=>new Vector3(p.positions[i*3],p.positions[i*3+1],p.positions[i*3+2]);
        private static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*100000)+":"+Mathf.RoundToInt(p.y*100000)+":"+Mathf.RoundToInt(p.z*100000);
        private static Dictionary<string,(Vector3 a,Vector3 b,int count)> Edges(HistoricalArtCandidatePipeline.SourcePart p)
        {
            var result=new Dictionary<string,(Vector3 a,Vector3 b,int count)>(StringComparer.Ordinal);
            for(var t=0;t<p.triangles.Length;t+=3)for(var j=0;j<3;j++)
            {
                var a=Point(p,p.triangles[t+j]);var b=Point(p,p.triangles[t+(j+1)%3]);var ka=Key(a);var kb=Key(b);
                if(ka==kb)continue;var key=string.CompareOrdinal(ka,kb)<0?ka+"|"+kb:kb+"|"+ka;
                result[key]=result.TryGetValue(key,out var old)?(old.a,old.b,old.count+1):(a,b,1);
            }
            return result;
        }
    }
}
