#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Read-only comparison of Unity's built-in Humanoid foot-IK option in the
    /// same Animator/Playable path used by the actual review player. This does
    /// not add floor targets, modify clips or claim terrain/contact acceptance.
    /// </summary>
    public static class MeshyTargetContactComparison
    {
        public static string ReportPath=>Path.GetFullPath("../TestResults/MeshyContact/foot-ik-comparison.json");
        public static readonly HumanBodyBones[] Bones={HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
        [Serializable] public sealed class Sample
        {
            public float phase;public float minimumSkinY;public float leftFootMinimumY;public float rightFootMinimumY;public Vector3 rootPosition;public Quaternion rootRotation;
            public Vector3[] joints=Array.Empty<Vector3>();public Quaternion[] rotations=Array.Empty<Quaternion>();
        }
        [Serializable] public sealed class Case
        {
            public string clip="";public string sourcePath="";public string sourceHash="";public bool applyFootIK;public string avatar="";public float duration;public float humanScale;public int steps;public int triangles;
            public int leftFootVertices;public int rightFootVertices;public string[] jointPaths=Array.Empty<string>();public float minimumSkinY;public float minimumLeftFootY;public float minimumRightFootY;public float maximumRootTravel;public Sample[] samples=Array.Empty<Sample>();
        }
        [Serializable] public sealed class PairDelta
        {
            public string clip="";public float minimumSkinYWithoutIK;public float minimumSkinYWithIK;public float minimumSkinImprovementMeters;
            public float maximumPairedSkinMinimumDelta;public float maximumPairedFootJointPositionDelta;public float maximumPairedToePositionDelta;public float maximumPairedHipsPositionDelta;
            public string interpretation="Measured difference only; enabling Humanoid FootIK is not a terrain raycast or proof of ground contact.";
        }
        [Serializable] public sealed class Report
        {
            public string status="NOT_COMPLETED";public string utc="";public string unityVersion="";public string error="";public bool originalInputsUnchanged;
            public string targetPrefab=MeshyHasanPilotPipeline.PrefabPath;public string avatar=MeshyHumanoidCalibration.TargetAvatarPath;
            public string method="Same chosen calibrated target and source clip; active scene clone, Animator.Rebind, manual AnimationClipPlayable, applyRootMotion=false, no playable IK callback, phase set then graph.Evaluate(0), actual LOD0 BakeMesh. Only SetApplyFootIK differs.";
            public string limitation="No ground mesh/physics, terrain raycast, OnAnimatorIK targets or source asset edits. Actual Windows follow-up required. Negative skin/foot Y is retained, not offset or clamped.";
            public string[] jointOrder=Bones.Select(b=>b.ToString()).ToArray();public Case[] cases=Array.Empty<Case>();public PairDelta[] pairedDeltas=Array.Empty<PairDelta>();
        }

        public static void RunFootIkComparison()
        {
            try{Compare();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static Report Compare()
        {
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath)??throw new InvalidOperationException("Contact comparison target missing.");var avatar=MeshyHumanoidCalibration.LoadTargetAvatar();var clips=MeshyMotionLibraryIntake.ReviewClips();
                if(clips.Length!=6)throw new InvalidOperationException("Expected all six reviewed licensed clips, not a partial comparison.");
                var inputs=new[]{AssetDatabase.GetAssetPath(prefab),AssetDatabase.GetAssetPath(avatar)}.Concat(clips.Select(AssetDatabase.GetAssetPath)).Distinct().SelectMany(p=>new[]{p,p+".meta"}).Where(File.Exists).ToDictionary(p=>p,Hash,StringComparer.Ordinal);
                var cases=new List<Case>();var deltas=new List<PairDelta>();
                foreach(var clip in clips)
                {
                    var without=Measure(prefab,avatar,clip,false);var with=Measure(prefab,avatar,clip,true);cases.Add(without);cases.Add(with);deltas.Add(ComparePair(without,with));
                }
                foreach(var input in inputs)if(Hash(input.Key)!=input.Value)throw new InvalidOperationException("Foot-IK comparison changed original input: "+input.Key);
                report.cases=cases.ToArray();report.pairedDeltas=deltas.ToArray();report.originalInputsUnchanged=true;report.status="FOOT_IK_COMPARISON_COMPLETE_NOT_CONTACT_ACCEPTANCE";
                foreach(var delta in deltas)Debug.Log("FOC_FOOT_IK "+delta.clip+" offMinY="+delta.minimumSkinYWithoutIK+" onMinY="+delta.minimumSkinYWithIK+" maxFootDelta="+delta.maximumPairedFootJointPositionDelta);
                return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
        }

        private static Case Measure(GameObject prefab,Avatar avatar,AnimationClip clip,bool footIK)
        {
            GameObject? actor=null;var graph=default(PlayableGraph);Mesh? baked=null;
            try
            {
                actor=Object.Instantiate(prefab);actor.SetActive(true);actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var animator=actor.GetComponent<Animator>()??throw new InvalidOperationException("Target Animator missing.");animator.enabled=false;animator.runtimeAnimatorController=null;
                FOC.Presentation.Visuals.MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab,actor);animator.avatar=avatar;animator.Rebind();
                if(!avatar.isValid||!avatar.isHuman||!clip.isHumanMotion)throw new InvalidOperationException("Foot-IK comparison requires valid real Humanoid path.");
                animator.enabled=true;animator.Rebind();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Read-only Meshy foot-IK comparison");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(footIK);playable.SetApplyPlayableIK(false);AnimationPlayableOutput.Create(graph,"Humanoid foot IK "+footIK,animator).SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
                var joints=Bones.Select(animator.GetBoneTransform).ToArray();if(joints.Any(j=>j==null))throw new InvalidOperationException("Humanoid foot-IK target joint missing after actual graph binding: "+string.Join(",",Bones.Where((b,i)=>joints[i]==null)));
                var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.sharedMesh!=null).OrderByDescending(s=>s.sharedMesh.triangles.Length).First();skin.updateWhenOffscreen=true;
                var left=WeightedFootVertices(skin,joints[3],joints[4]);var right=WeightedFootVertices(skin,joints[7],joints[8]);baked=new Mesh();
                var steps=Mathf.Max(32,Mathf.CeilToInt(clip.length*60));var samples=new List<Sample>();
                for(var i=0;i<=steps;i++)
                {
                    var phase=i/(float)steps;playable.SetTime(phase*clip.length);graph.Evaluate(0);skin.BakeMesh(baked);var vertices=baked.vertices.Select(skin.transform.TransformPoint).ToArray();
                    if(vertices.Length==0||vertices.Any(v=>!Finite(v)))throw new InvalidOperationException("Foot-IK comparison skin empty/nonfinite.");
                    samples.Add(new Sample{phase=phase,minimumSkinY=vertices.Min(v=>v.y),leftFootMinimumY=left.Min(v=>vertices[v].y),rightFootMinimumY=right.Min(v=>vertices[v].y),rootPosition=actor.transform.position,rootRotation=actor.transform.rotation,joints=joints.Select(j=>j.position).ToArray(),rotations=joints.Select(j=>j.rotation).ToArray()});
                }
                var path=AssetDatabase.GetAssetPath(clip);return new Case{clip=clip.name,sourcePath=path,sourceHash=Hash(path),applyFootIK=footIK,avatar=avatar.name,duration=clip.length,humanScale=animator.humanScale,steps=steps,triangles=skin.sharedMesh.triangles.Length/3,leftFootVertices=left.Length,rightFootVertices=right.Length,jointPaths=joints.Select(j=>AnimationUtility.CalculateTransformPath(j,actor.transform)).ToArray(),minimumSkinY=samples.Min(s=>s.minimumSkinY),minimumLeftFootY=samples.Min(s=>s.leftFootMinimumY),minimumRightFootY=samples.Min(s=>s.rightFootMinimumY),maximumRootTravel=samples.Max(s=>s.rootPosition.magnitude),samples=samples.ToArray()};
            }
            finally{if(graph.IsValid())graph.Destroy();if(baked!=null)Object.DestroyImmediate(baked);if(actor!=null)Object.DestroyImmediate(actor);}
        }
        private static int[] WeightedFootVertices(SkinnedMeshRenderer skin,Transform foot,Transform toes)
        {
            var indices=new HashSet<int>();for(var i=0;i<skin.bones.Length;i++)if(skin.bones[i]==foot||skin.bones[i]==toes)indices.Add(i);
            var result=skin.sharedMesh.boneWeights.Select((w,i)=>new{w,i}).Where(p=>(indices.Contains(p.w.boneIndex0)?p.w.weight0:0)+(indices.Contains(p.w.boneIndex1)?p.w.weight1:0)+(indices.Contains(p.w.boneIndex2)?p.w.weight2:0)+(indices.Contains(p.w.boneIndex3)?p.w.weight3:0)>=.5f).Select(p=>p.i).ToArray();
            if(result.Length<6)throw new InvalidOperationException("Insufficient actual weighted foot vertices.");return result;
        }
        private static PairDelta ComparePair(Case without,Case with)
        {
            if(without.samples.Length!=with.samples.Length)throw new InvalidOperationException("Foot-IK comparison phase grids differ.");
            var delta=new PairDelta{clip=without.clip,minimumSkinYWithoutIK=without.minimumSkinY,minimumSkinYWithIK=with.minimumSkinY,minimumSkinImprovementMeters=with.minimumSkinY-without.minimumSkinY};
            for(var i=0;i<without.samples.Length;i++)
            {
                var a=without.samples[i];var b=with.samples[i];delta.maximumPairedSkinMinimumDelta=Mathf.Max(delta.maximumPairedSkinMinimumDelta,Mathf.Abs(a.minimumSkinY-b.minimumSkinY));delta.maximumPairedHipsPositionDelta=Mathf.Max(delta.maximumPairedHipsPositionDelta,Vector3.Distance(a.joints[0],b.joints[0]));
                foreach(var j in new[]{3,7})delta.maximumPairedFootJointPositionDelta=Mathf.Max(delta.maximumPairedFootJointPositionDelta,Vector3.Distance(a.joints[j],b.joints[j]));foreach(var j in new[]{4,8})delta.maximumPairedToePositionDelta=Mathf.Max(delta.maximumPairedToePositionDelta,Vector3.Distance(a.joints[j],b.joints[j]));
            }
            return delta;
        }
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);private static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        private static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
}
