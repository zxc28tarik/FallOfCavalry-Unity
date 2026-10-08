#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>Approved right-hand-only additive shape, never a replacement body or skeleton.</summary>
    public static class MeshyKilicGripCorrectivePipeline
    {
        public const string Root=MeshyHasanPilotPipeline.OutputRoot+"/Candidates/KilicGrip";
        public const string PrefabPath=Root+"/CHR_HasanAga_MeshyPilot_KilicGrip.prefab";
        public const string ProvenancePath=Root+"/GripAuthoringProvenance.json";
        public static readonly Vector3 PalmAnchor=MeshyKilicGripAttachment.CorrectivePalmInHand;
        public static Quaternion WeaponFrame=>MeshyKilicGripAttachment.CorrectiveWeaponRotationInHand;
        private static readonly Vector3 ThumbRoot=new Vector3(-.033f,.074f,-.0395f);

        [Serializable] public sealed class LodAudit
        {
            public int lod,vertices,triangles,changedVertices;
            public string originalMesh="",derivedMesh="";
            public float maximumDeltaMeters;
        }
        [Serializable] public sealed class Provenance
        {
            public string status="RIGHT_HAND_CORRECTIVE_CANDIDATE_VISUAL_QA_REQUIRED";
            public string authority="User explicitly approved only right-hand grasp correction; original source/rig/body/clothing/textures/gameplay remain protected.";
            public string tool="Unity Editor 6000.3.16f1; continuous spatial finger bend and thumb opposition, welded vertex clearance. Right-hand-only additive shape; no new rig/topology. Does not claim exact digit segment length or face-intersection elimination.";
            public string source="",sourceSha256="",sourcePrefabSha256="",dateUtc="",revision="4";
            public string shape=MeshyRightHandGripVisual.ShapeName;
            public string license="User-owned Meshy Pro derivative; existing source policy retained. No external asset introduced.";
            public Vector3 gripInHandFrame;
            public Quaternion weaponRotationInHandFrame;
            public LodAudit[] lods=Array.Empty<LodAudit>();
        }

        public static void Run()
        {
            var code=0;try{Generate();}catch(Exception e){Debug.LogException(e);code=1;}EditorApplication.Exit(code);
        }
        public static void RunAndBuild()
        {
            try{Generate();MeshyHasanPilotBuild.RunUserMotion();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void Generate()
        {
            var sourceHash=Hash(MeshyHasanPilotBuild.SourcePath);
            if(sourceHash!=MeshyHasanPilotLodImport.RuntimeHash)throw new InvalidOperationException("Protected Meshy FBX changed.");
            var prefabHash=Hash(MeshyHasanPilotBuild.PrefabPath);
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotBuild.PrefabPath)
                ??throw new InvalidOperationException("Original retained pilot missing.");
            var instance=Object.Instantiate(source);
            try
            {
                instance.name="CHR_HasanAga_MeshyPilot_KilicGrip";
                instance.GetComponent<Animator>().enabled=false;
                MeshyHasanPilotPipeline.RestoreSourceBindPose(instance);
                var skins=instance.GetComponent<LODGroup>().GetLODs().Select(l=>l.renderers.OfType<SkinnedMeshRenderer>().Single()).ToArray();
                var report=new Provenance{source=MeshyHasanPilotBuild.SourcePath,sourceSha256=sourceHash,sourcePrefabSha256=prefabHash,
                    dateUtc=DateTime.UtcNow.ToString("o"),gripInHandFrame=PalmAnchor,weaponRotationInHandFrame=WeaponFrame,
                    lods=new LodAudit[skins.Length]};
                for(var i=0;i<skins.Length;i++)
                {
                    var original=skins[i].sharedMesh;
                    var derived=CreateCorrective(skins[i],out var changed,out var maximum);
                    var path=Root+"/Hasan_KilicGrip_LOD"+i+".asset";
                    var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(stored==null){AssetDatabase.CreateAsset(derived,path);stored=derived;}
                    else{EditorUtility.CopySerialized(derived,stored);Object.DestroyImmediate(derived);EditorUtility.SetDirty(stored);}
                    skins[i].sharedMesh=stored;
                    report.lods[i]=new LodAudit{lod=i,vertices=original.vertexCount,triangles=original.triangles.Length/3,
                        originalMesh=AssetDatabase.GetAssetPath(original),derivedMesh=path,changedVertices=changed,maximumDeltaMeters=maximum};
                }
                var driver=instance.AddComponent<MeshyRightHandGripVisual>();driver.Configure(skins);
                instance.GetComponent<Animator>().enabled=true;
                PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
                File.WriteAllText(ProvenancePath,JsonUtility.ToJson(report,true));
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                if(Hash(MeshyHasanPilotBuild.SourcePath)!=sourceHash||Hash(MeshyHasanPilotBuild.PrefabPath)!=prefabHash)
                    throw new InvalidOperationException("Original source was modified during derivative authoring.");
                Debug.Log("FOC_MESHY_RIGHT_HAND_GRIP_CANDIDATE_CREATED "+PrefabPath);
            }
            finally{Object.DestroyImmediate(instance);}
        }

        public static Mesh CreateCorrective(SkinnedMeshRenderer skin,out int changed,out float maximum)
        {
            var original=skin.sharedMesh;
            if(original.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)>=0)throw new InvalidOperationException("Never derive from an already corrected mesh.");
            var handIndex=Array.FindIndex(skin.bones,b=>b.name=="RightHand");
            if(handIndex<0)throw new InvalidOperationException("RightHand bind missing.");
            var toHand=original.bindposes[handIndex];var fromHand=toHand.inverse;
            var vertices=original.vertices;var normals=original.normals;var weights=original.boneWeights;
            var positions=new Vector3[vertices.Length];var normalDeltas=new Vector3[vertices.Length];
            var posedInHand=new Vector3[vertices.Length];var editable=new bool[vertices.Length];
            changed=0;maximum=0;
            for(var i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var weight=(w.boneIndex0==handIndex?w.weight0:0)+(w.boneIndex1==handIndex?w.weight1:0)
                    +(w.boneIndex2==handIndex?w.weight2:0)+(w.boneIndex3==handIndex?w.weight3:0);
                if(weight<.8f)continue;
                var handPoint=toHand.MultiplyPoint3x4(vertices[i]);
                var posed=PoseHandPoint(handPoint,out var rotation);
                var delta=fromHand.MultiplyPoint3x4(posed)-vertices[i];
                if(delta.magnitude<.000001f)continue;
                if(!Finite(delta)||delta.magnitude>.15f||handPoint.y<.08f)throw new InvalidOperationException("Grip corrective exceeded protected hand envelope.");
                positions[i]=delta;maximum=Mathf.Max(maximum,delta.magnitude);changed++;
                editable[i]=true;posedInHand[i]=posed;
                var handNormal=fromHand.transpose.MultiplyVector(normals[i]).normalized;
                var posedNormal=toHand.transpose.MultiplyVector(rotation*handNormal).normalized;
                normalDeltas[i]=posedNormal-normals[i];
            }
            if(changed<8||maximum<.02f)throw new InvalidOperationException("Empty grip corrective.");
            for(var i=0;i<vertices.Length;i++)if(!editable[i])posedInHand[i]=toHand.MultiplyPoint3x4(vertices[i]);
            var contact=new MeshyKilicGripContact(PalmAnchor,WeaponFrame);
            // Do not repeatedly displace face vertices: the earlier triangle
            // projection accumulated into spikes. Preserve a continuous bend
            // and resolve each welded surface vertex once against the hilt.
            for(var i=0;i<posedInHand.Length;i++)if(editable[i])
                posedInHand[i]+=contact.Push(posedInHand[i],.002f);
            maximum=0;
            for(var i=0;i<vertices.Length;i++)if(editable[i])
            {
                positions[i]=fromHand.MultiplyPoint3x4(posedInHand[i])-vertices[i];
                maximum=Mathf.Max(maximum,positions[i].magnitude);
                if(!Finite(positions[i])||positions[i].magnitude>.15f)throw new InvalidOperationException("Contact cleanup exceeded hand envelope.");
            }
            // Recompute only the changed hand's closed normals; the base mesh
            // (and all original body normals) remains byte-for-byte untouched.
            var normalProbe=Object.Instantiate(original);
            try
            {
                normalProbe.vertices=vertices.Select((v,i)=>v+positions[i]).ToArray();normalProbe.RecalculateNormals();
                var closedNormals=normalProbe.normals;
                var weldedNormals=new System.Collections.Generic.Dictionary<Vector3,Vector3>();
                for(var i=0;i<vertices.Length;i++)if(editable[i])
                {
                    var key=Quantized(vertices[i]);weldedNormals.TryGetValue(key,out var sum);weldedNormals[key]=sum+closedNormals[i];
                }
                for(var i=0;i<vertices.Length;i++)if(editable[i])normalDeltas[i]=weldedNormals[Quantized(vertices[i])].normalized-normals[i];
            }
            finally{Object.DestroyImmediate(normalProbe);}
            var result=Object.Instantiate(original);result.name=original.name+"_KilicGripCandidate";
            result.AddBlendShapeFrame(MeshyRightHandGripVisual.ShapeName,100,positions,normalDeltas,null);
            return result;
        }

        public static Vector3 PoseHandPoint(Vector3 point,out Quaternion rotation)
        {
            rotation=Quaternion.identity;
            if(point.y<.08f)return point;
            var thumbWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.025f,-.039f,point.x))
                *Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.021f,-.034f,point.z))
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.142f,.153f,point.y)));
            return PoseHandPoint(point,thumbWeight,out rotation);
        }
        public static Vector3 PoseHandPoint(Vector3 point,float thumbWeight,out Quaternion rotation)
        {
            rotation=Quaternion.identity;if(point.y<.08f)return point;
            // One continuous spatial field across all four fingers. There is
            // no per-vertex nearest-digit/nearest-segment switch to tear a face.
            var rootY=Mathf.Lerp(.127f,.116f,Mathf.InverseLerp(-.018f,.063f,point.z));
            var amount=Mathf.Max(0,point.y-rootY);
            var angle=amount/.032f;
            var sourceCenterX=Mathf.Lerp(.007f,-.009f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.140f,.195f,point.y)));
            var radius=Mathf.Max(.026f,.035f+point.x-sourceCenterX);
            var curled=new Vector3(-.030f+radius*Mathf.Cos(angle),rootY+radius*1.25f*Mathf.Sin(angle),point.z);
            var fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(rootY-.005f,rootY+.008f,point.y));
            var finger=Vector3.Lerp(point,curled,fade);
            var fingerRotation=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.forward);
            var opposed=PoseThumbContinuous(point,out var thumbRotation);
            var thumbFade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.082f,.106f,point.y));
            opposed=Vector3.Lerp(point,opposed,thumbFade);
            rotation=Quaternion.Slerp(Quaternion.Slerp(Quaternion.identity,fingerRotation,fade),
                Quaternion.Slerp(Quaternion.identity,thumbRotation,thumbFade),thumbWeight);
            return Vector3.Lerp(finger,opposed,thumbWeight);
        }
        private static Vector3 PoseThumbContinuous(Vector3 p,out Quaternion rotation)
        {
            var u=Mathf.Clamp01((p.y-ThumbRoot.y)/(.140f-ThumbRoot.y));
            var restCenter=new Vector3(Mathf.Lerp(-.033f,-.051f,Mathf.SmoothStep(0,1,u)),p.y,
                Mathf.Lerp(-.0395f,-.052f,u));
            var targetCenter=new Vector3(Mathf.Lerp(-.033f,-.053f,Mathf.SmoothStep(0,1,u)),
                ThumbRoot.y+.052f*Mathf.Sin(u*Mathf.PI*.66f),Mathf.Lerp(-.0395f,.001f,Mathf.SmoothStep(0,1,u)));
            rotation=Quaternion.AngleAxis(-u*75f,Vector3.right);
            return targetCenter+rotation*(p-restCenter);
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
        private static Vector3 Quantized(Vector3 p)=>new Vector3(Mathf.Round(p.x*1000000),Mathf.Round(p.y*1000000),Mathf.Round(p.z*1000000));
        private static string Hash(string path){using var h=SHA256.Create();return string.Concat(h.ComputeHash(File.ReadAllBytes(path)).Select(b=>b.ToString("x2")));}
    }
}
